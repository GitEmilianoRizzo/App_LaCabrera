using Dapper;
using LaCabrera.Api.Configuration;
using LaCabrera.Api.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LaCabrera.Api.Tests;

/// <summary>
/// Prueba de integración del reproceso contra una base de TESTING.
/// Se ejecuta solo si está definida la variable LACABRERA_TEST_DB con la cadena de conexión,
/// y se niega a correr contra LaCabreraDB (base real / copia de producción).
/// </summary>
public class VinsonReprocesoTests
{
    private const int FranquiciaAeroparque = 8;

    private static string? CadenaTesting()
    {
        var cs = Environment.GetEnvironmentVariable("LACABRERA_TEST_DB");
        if (string.IsNullOrWhiteSpace(cs)) return null;
        var db = new SqlConnectionStringBuilder(cs).InitialCatalog;
        if (string.Equals(db, "LaCabreraDB", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("LACABRERA_TEST_DB apunta a LaCabreraDB: usar una base de testing.");
        return cs;
    }

    [SkippableFact]
    public async Task Reproceso_actualiza_el_ticket_existente_sin_duplicarlo()
    {
        var cs = CadenaTesting();
        Skip.If(cs is null, "Definir LACABRERA_TEST_DB para correr la prueba de integración");

        var factory = new SqlConnectionFactory(cs!);
        var servicio = new VinsonExtractorService(factory, null!, null!, NullLogger<VinsonExtractorService>.Instance,
            Options.Create(new VinsonSettings()));

        using var db = new SqlConnection(cs);
        var externalId = -Math.Abs(Guid.NewGuid().GetHashCode() % 1_000_000_000L) - 1; // id negativo: no choca con Vinson
        var batchId = await db.QuerySingleAsync<long>(@"
            INSERT INTO stg.IngestionBatch (BatchId, FranquiciaId, FechaNegocio, SchemaVersion, TipoCarga, OrigenIngesta, Estado)
            OUTPUT INSERTED.IngestionBatchId
            VALUES (@Guid, @F, '2026-08-14', 'TEST', 'CORRECTION', 'TEST', 'COMPLETED')",
            new { Guid = Guid.NewGuid().ToString(), F = FranquiciaAeroparque });

        try
        {
            var cabecera = new VinsonTransaccion
            {
                IdTransaccion = externalId, OpenDate = new DateTime(2026, 8, 14), Estado = 3, Clientes = 2,
                Inicio = new DateTime(2026, 8, 14, 21, 29, 0), MontoFinal = 1077000m, MontoNeto = 890082.64m
            };
            var detalleViejo = new VinsonSalesFullInfo
            {
                TransactionId = externalId,
                Products = new() { new VinsonProduct { Name = "Sand Ojo Bife C/Prov", Quantity = 1, GrossAmount = 33000m, NetAmount = 27272.73m } }
            };
            var detalleNuevo = new VinsonSalesFullInfo
            {
                TransactionId = externalId,
                Products = new()
                {
                    new VinsonProduct { Name = "Sand Ojo Bife C/Prov", Quantity = 22, GrossAmount = 33000m, NetAmount = 27272.73m },
                    new VinsonProduct { Name = "Agua S/Gas", Quantity = 13, GrossAmount = 5000m, NetAmount = 4132.23m },
                }
            };

            // 1. Alta
            var (ticketId, lineas) = await servicio.ProcesarTransaccionAsync(cabecera, detalleViejo, FranquiciaAeroparque, batchId, 0, "ARS");
            Assert.True(ticketId > 0);
            Assert.Equal(1, lineas);

            // 2. Carga normal de un ticket existente: se omite, como antes
            var (omitido, _) = await servicio.ProcesarTransaccionAsync(cabecera, detalleNuevo, FranquiciaAeroparque, batchId, 0, "ARS");
            Assert.Equal(0, omitido);

            // 3. Reproceso (dos veces: tiene que ser idempotente)
            for (int i = 0; i < 2; i++)
            {
                var (reprocesado, lineasRep) = await servicio.ProcesarTransaccionAsync(cabecera, detalleNuevo, FranquiciaAeroparque, batchId, 0, "ARS", reprocesar: true);
                Assert.Equal(ticketId, reprocesado);
                Assert.Equal(2, lineasRep);
            }

            // 4. Reproceso sin detalle de productos (Vinson no lo devolvió): se conservan las líneas
            var (sinDetalle, lineasSinDetalle) = await servicio.ProcesarTransaccionAsync(cabecera, null, FranquiciaAeroparque, batchId, 0, "ARS", reprocesar: true);
            Assert.Equal(ticketId, sinDetalle);
            Assert.Equal(0, lineasSinDetalle);

            var tickets = (await db.QueryAsync<(long Id, decimal Bruto, decimal Neto, decimal NetoSinImp, string Calidad)>(@"
                SELECT VentaTicketId, ImporteBruto, ImporteNeto, ImporteNetoSinImpuesto, CalidadImpuesto
                FROM fact.VentaTicket WHERE FranquiciaId = @F AND ExternalTicketId = @E",
                new { F = FranquiciaAeroparque, E = externalId.ToString() })).ToList();

            var t = Assert.Single(tickets);
            Assert.Equal(ticketId, t.Id);
            Assert.Equal(1077000m, t.Bruto);
            Assert.Equal(890082.64m, t.Neto);
            Assert.Equal(890082.64m, t.NetoSinImp);
            Assert.Equal("POS", t.Calidad);

            var lineasDb = (await db.QueryAsync<(string Nombre, decimal Bruto)>(@"
                SELECT NombreProducto, ImporteBruto FROM fact.VentaTicketDetalle WHERE VentaTicketId = @Id",
                new { Id = ticketId })).ToList();
            Assert.Equal(2, lineasDb.Count);
            Assert.Equal(726000m, lineasDb.Single(l => l.Nombre == "Sand Ojo Bife C/Prov").Bruto);
        }
        finally
        {
            await db.ExecuteAsync(@"
                DELETE d FROM fact.VentaTicketDetalle d JOIN fact.VentaTicket v ON v.VentaTicketId = d.VentaTicketId
                    WHERE v.FranquiciaId = @F AND v.ExternalTicketId = @E;
                DELETE m FROM fact.VentaTicketMedioPago m JOIN fact.VentaTicket v ON v.VentaTicketId = m.VentaTicketId
                    WHERE v.FranquiciaId = @F AND v.ExternalTicketId = @E;
                DELETE FROM fact.VentaTicket WHERE FranquiciaId = @F AND ExternalTicketId = @E;
                DELETE FROM stg.IngestionBatch WHERE IngestionBatchId = @B;",
                new { F = FranquiciaAeroparque, E = externalId.ToString(), B = batchId });
        }
    }
}
