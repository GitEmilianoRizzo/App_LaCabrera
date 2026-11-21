using Dapper;
using LaCabrera.Api.Configuration;
using LaCabrera.Api.Services.Consumo;

namespace LaCabrera.Api.Repositories;

public interface IConsumoProductosRepository
{
    Task<DateTime?> GetFechaMaximaAsync();
    Task<ConsumoDatosCrudos> GetDatosAsync(DateTime desde, DateTime hasta, int? franquiciaId);
}

public class ConsumoProductosRepository : IConsumoProductosRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ConsumoProductosRepository(IDbConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<DateTime?> GetFechaMaximaAsync()
    {
        using var cn = _connectionFactory.CreateConnection();
        return await cn.ExecuteScalarAsync<DateTime?>("SELECT MAX(FechaNegocio) FROM fact.VentaTicket WHERE EstaAnulado = 0");
    }

    public async Task<ConsumoDatosCrudos> GetDatosAsync(DateTime desde, DateTime hasta, int? franquiciaId)
    {
        using var cn = _connectionFactory.CreateConnection();
        var p = new { Desde = desde.Date, Hasta = hasta.Date, FranquiciaId = franquiciaId };

        // Líneas agrupadas por franquicia, día y nombre del POS. Importe = neto sin impuesto de la línea (cuando el POS lo
        // informa); el servicio después escala estas líneas al neto del ticket para usar la misma venta que el Dashboard.
        const string sqlLineas = @"
            SELECT t.FranquiciaId, t.FechaNegocio,
                   ISNULL(d.NombreProducto, '') AS Nombre,
                   ISNULL(d.CategoriaProducto, '') AS Categoria,
                   ISNULL(d.FamiliaProducto, '') AS Familia,
                   SUM(d.Cantidad) AS Cantidad,
                   SUM(CASE WHEN COALESCE(d.ImporteNetoSinImpuesto, d.ImporteNeto) > 0 THEN d.Cantidad ELSE 0 END) AS CantidadCobrada,
                   SUM(COALESCE(d.ImporteNetoSinImpuesto, d.ImporteNeto)) AS Importe
            FROM fact.VentaTicketDetalle d
            JOIN fact.VentaTicket t ON t.VentaTicketId = d.VentaTicketId
            WHERE t.EstaAnulado = 0 AND ISNULL(d.EstaAnulado, 0) = 0
              AND t.FechaNegocio BETWEEN @Desde AND @Hasta
              AND (@FranquiciaId IS NULL OR t.FranquiciaId = @FranquiciaId)
            GROUP BY t.FranquiciaId, t.FechaNegocio, ISNULL(d.NombreProducto, ''), ISNULL(d.CategoriaProducto, ''), ISNULL(d.FamiliaProducto, '')";

        // Día por franquicia: venta neta con la definición del Dashboard (sin impuestos ni servicio) y cotizaciones del día.
        const string sqlDias = @"
            SELECT t.FranquiciaId, t.FechaNegocio,
                   COUNT(*) AS Tickets,
                   SUM(ISNULL(t.CantidadCubiertos, 0)) AS Cubiertos,
                   SUM(COALESCE(t.ImporteNetoSinImpuesto, t.ImporteNeto) - ISNULL(t.ImporteServicio, 0)) AS VentaNeta,
                   MAX(CASE WHEN m.CodigoISO = 'USD' THEN 1 ELSE tc.UnidadesPorUsd END) AS UnidadesPorUsd,
                   MAX(ars.UnidadesPorUsd) AS ArsPorUsd
            FROM fact.VentaTicket t
            JOIN dim.Franquicia f ON f.FranquiciaId = t.FranquiciaId
            JOIN dim.Moneda m ON m.MonedaId = f.MonedaId
            OUTER APPLY (SELECT TOP 1 x.UnidadesPorUsd FROM dim.TipoCambio x
                         WHERE x.CodigoMoneda = m.CodigoISO AND x.Fecha <= t.FechaNegocio ORDER BY x.Fecha DESC) tc
            OUTER APPLY (SELECT TOP 1 x.UnidadesPorUsd FROM dim.TipoCambio x
                         WHERE x.CodigoMoneda = 'ARS' AND x.Fecha <= t.FechaNegocio ORDER BY x.Fecha DESC) ars
            WHERE t.EstaAnulado = 0
              AND t.FechaNegocio BETWEEN @Desde AND @Hasta
              AND (@FranquiciaId IS NULL OR t.FranquiciaId = @FranquiciaId)
            GROUP BY t.FranquiciaId, t.FechaNegocio";

        const string sqlFranquicias = @"
            SELECT f.FranquiciaId, f.Codigo, f.Nombre, f.Pais, f.Ciudad, m.CodigoISO AS Moneda
            FROM dim.Franquicia f
            JOIN dim.Moneda m ON m.MonedaId = f.MonedaId
            WHERE EXISTS (SELECT 1 FROM fact.VentaTicket t WHERE t.FranquiciaId = f.FranquiciaId
                          AND t.FechaNegocio BETWEEN @Desde AND @Hasta)
              AND (@FranquiciaId IS NULL OR f.FranquiciaId = @FranquiciaId)";

        var lineas = (await cn.QueryAsync<ConsumoLineaCruda>(sqlLineas, p, commandTimeout: 180)).ToList();
        var dias = (await cn.QueryAsync<ConsumoDiaCrudo>(sqlDias, p, commandTimeout: 180)).ToList();
        var franquicias = (await cn.QueryAsync<ConsumoFranquiciaCruda>(sqlFranquicias, p)).ToList();
        return new ConsumoDatosCrudos(lineas, dias, franquicias);
    }
}
