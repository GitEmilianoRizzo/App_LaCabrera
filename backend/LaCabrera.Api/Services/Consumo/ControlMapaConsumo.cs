using System.Text.Json;
using Dapper;
using LaCabrera.Api.Configuration;

namespace LaCabrera.Api.Services.Consumo;

/// <summary>
/// Regla del sistema: toda franquicia que entra (nueva conexión) tiene que acomodarse al Mapa de Consumo y a la
/// clasificación de productos. Este control lo verifica por franquicia y lo corre todos los días ControlMapaConsumoJob.
///
/// Chequeos (en la ventana de los últimos N días con ventas de cada franquicia):
/// - UBICACION: PaisIso, ZonaMapa, Latitud y Longitud en dim.Franquicia. Sin eso no aparece en el mapa. Error.
///   También se exige para activar el nodo (ConexionService).
/// - COTIZACION: días con ventas sin tipo de cambio de su moneda: su venta queda en 0 USD. Error.
/// - CLASIFICACION: parte de la venta de las líneas que el clasificador no reconoce (Sin clasificar).
///   Más del 10 %: atención; más del 50 % (p. ej. el POS manda códigos): error. Se listan los nombres a clasificar.
/// - NO_PRODUCTO: más del 15 % de la venta en ítems que no son producto (propinas, ajustes, eventos): atención.
/// - DETALLE: más del 20 % de los días con tickets pero sin líneas de producto: atención (van a "Ajuste a tickets").
/// - CUBIERTOS: menos del 50 % de los tickets con cubiertos: atención (no se puede medir "cada 100 cubiertos").
/// </summary>
public static class ReglaMapaConsumo
{
    public const decimal SinClasificarAtencion = 0.10m;
    public const decimal SinClasificarError = 0.50m;
    public const decimal NoProductoAtencion = 0.15m;
    public const decimal DiasSinDetalleAtencion = 0.20m;
    public const decimal TicketsConCubiertosMinimo = 0.50m;

    public const string Ok = "OK", Atencion = "ATENCION", Error = "ERROR", SinDatos = "SIN_DATOS";

    public static string MensajeSinUbicacion(string codigo) =>
        $"La franquicia {codigo} no tiene ubicación para el Mapa de Consumo (PaisIso, ZonaMapa, Latitud y Longitud en dim.Franquicia). " +
        "Es regla del sistema: cargala antes de activar la conexión.";

    public static int Gravedad(string estado) => estado switch { Error => 3, Atencion => 2, SinDatos => 1, _ => 0 };

    /// <summary>Evalúa una franquicia a partir de sus datos ya agregados (sin base de datos, para poder testearlo).</summary>
    public static ControlFranquiciaDto Evaluar(ControlFranquiciaDatos d, IReadOnlyList<ControlLineaCruda> lineas, int topSinClasificar = 15)
    {
        var chequeos = new List<ControlChequeoDto>();

        var tieneUbicacion = !string.IsNullOrWhiteSpace(d.PaisIso) && !string.IsNullOrWhiteSpace(d.ZonaMapa) && d.Latitud.HasValue && d.Longitud.HasValue;
        chequeos.Add(tieneUbicacion
            ? new("UBICACION", Ok, $"{d.ZonaMapa} ({d.Pais})")
            : new("UBICACION", Error, "Sin ubicación: no aparece en el mapa. Cargar PaisIso, ZonaMapa, Latitud y Longitud en dim.Franquicia."));

        // Clasificación de las líneas por importe (moneda local)
        decimal total = 0, sinClasificar = 0, noProducto = 0;
        var pendientes = new Dictionary<string, ControlNombreDto>();
        foreach (var l in lineas)
        {
            var c = ClasificadorProductos.Clasificar(l.Nombre, l.Categoria, l.Familia);
            var importe = Math.Abs(l.Importe);
            total += importe;
            if (c.Rubro == ClasificadorProductos.RubroNoProducto) noProducto += importe;
            else if (c.Rubro == ClasificadorProductos.RubroSinClasificar)
            {
                sinClasificar += importe;
                var clave = $"{l.Nombre}\u001f{l.Categoria}\u001f{l.Familia}";
                pendientes[clave] = pendientes.TryGetValue(clave, out var p)
                    ? p with { Importe = p.Importe + l.Importe, Cantidad = p.Cantidad + l.Cantidad }
                    : new ControlNombreDto(l.Nombre, NullSiVacio(l.Categoria), NullSiVacio(l.Familia), l.Importe, l.Cantidad);
            }
        }

        decimal? pctSin = total > 0 ? sinClasificar / total : null;
        decimal? pctNo = total > 0 ? noProducto / total : null;
        decimal? pctDiasSinDetalle = d.DiasConVentas > 0 ? (decimal)(d.DiasConVentas - d.DiasConLineas) / d.DiasConVentas : null;
        decimal? pctCubiertos = d.Tickets > 0 ? (decimal)d.TicketsConCubiertos / d.Tickets : null;

        if (d.DiasConVentas > 0)
        {
            chequeos.Add(d.DiasSinCotizacion > 0
                ? new("COTIZACION", Error, $"{d.DiasSinCotizacion} de {d.DiasConVentas} días sin cotización {d.Moneda}/USD: esa venta queda en 0 USD")
                : new("COTIZACION", Ok, d.Moneda == "USD" ? "Vende en USD" : $"Cotización {d.Moneda}/USD disponible"));

            if (total == 0)
                chequeos.Add(new("CLASIFICACION", Atencion, "Sin líneas de producto en la ventana: no se puede clasificar"));
            else
            {
                var estado = pctSin > SinClasificarError ? Error : pctSin > SinClasificarAtencion ? Atencion : Ok;
                chequeos.Add(new("CLASIFICACION", estado,
                    $"{1 - pctSin:P0} de la venta clasificada" + (pendientes.Count > 0 ? $"; {pendientes.Count} nombres sin clasificar" : "")
                    + (pctSin > SinClasificarError ? ". El POS parece mandar códigos en lugar de nombres" : "")));
                chequeos.Add(new("NO_PRODUCTO", pctNo > NoProductoAtencion ? Atencion : Ok, $"{pctNo:P0} de la venta en ítems que no son producto"));
            }

            chequeos.Add(new("DETALLE", pctDiasSinDetalle > DiasSinDetalleAtencion ? Atencion : Ok,
                d.DiasConLineas == d.DiasConVentas ? "Todos los días tienen detalle de productos"
                    : $"{d.DiasConVentas - d.DiasConLineas} de {d.DiasConVentas} días con tickets pero sin detalle de productos"));

            chequeos.Add(new("CUBIERTOS", pctCubiertos < TicketsConCubiertosMinimo ? Atencion : Ok,
                $"{pctCubiertos:P0} de los tickets con cubiertos"));
        }

        var peor = chequeos.MaxBy(c => Gravedad(c.Estado))!.Estado;
        var estadoFinal = d.DiasConVentas == 0 && peor == Ok ? SinDatos : peor;

        return new ControlFranquiciaDto(
            d.FranquiciaId, d.Codigo, d.Nombre, d.Pais, d.ZonaMapa, d.Moneda, d.SistemaOrigen, d.NodoActivo, estadoFinal,
            d.Desde, d.Hasta, d.DiasConVentas,
            pctSin is { } ps ? Math.Round((1 - ps) * 100, 1) : null,
            pctSin is { } ps2 ? Math.Round(ps2 * 100, 1) : null,
            pctNo is { } pn ? Math.Round(pn * 100, 1) : null,
            pctDiasSinDetalle is { } pd ? Math.Round(pd * 100, 1) : null,
            pctCubiertos is { } pc ? Math.Round(pc * 100, 1) : null,
            chequeos,
            pendientes.Values.OrderByDescending(p => Math.Abs(p.Importe)).Take(topSinClasificar)
                .Select(p => p with { Importe = Math.Round(p.Importe, 2) }).ToList(),
            pendientes.Count);
    }

    private static string? NullSiVacio(string s) => string.IsNullOrWhiteSpace(s) ? null : s;
}

#region DTOs

public class ControlFranquiciaDatos
{
    public int FranquiciaId { get; set; }
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string? Pais { get; set; }
    public string Moneda { get; set; } = "";
    public string? SistemaOrigen { get; set; }
    public string? PaisIso { get; set; }
    public string? ZonaMapa { get; set; }
    public decimal? Latitud { get; set; }
    public decimal? Longitud { get; set; }
    public bool NodoActivo { get; set; }
    public DateTime? Desde { get; set; }
    public DateTime? Hasta { get; set; }
    public int DiasConVentas { get; set; }
    public int DiasConLineas { get; set; }
    public int DiasSinCotizacion { get; set; }
    public int Tickets { get; set; }
    public int TicketsConCubiertos { get; set; }
}

public class ControlLineaCruda
{
    public int FranquiciaId { get; set; }
    public string Nombre { get; set; } = "";
    public string Categoria { get; set; } = "";
    public string Familia { get; set; } = "";
    public decimal Importe { get; set; }
    public decimal Cantidad { get; set; }
}

public record ControlChequeoDto(string Codigo, string Estado, string Mensaje);

public record ControlNombreDto(string Nombre, string? Categoria, string? Familia, decimal Importe, decimal Cantidad);

public record ControlFranquiciaDto(
    int FranquiciaId, string Codigo, string Nombre, string? Pais, string? Zona, string Moneda, string? SistemaOrigen, bool NodoActivo,
    string Estado, DateTime? Desde, DateTime? Hasta, int DiasConVentas,
    decimal? PctClasificado, decimal? PctSinClasificar, decimal? PctNoProducto, decimal? PctDiasSinDetalle, decimal? PctTicketsConCubiertos,
    IReadOnlyList<ControlChequeoDto> Chequeos, IReadOnlyList<ControlNombreDto> SinClasificar, int NombresSinClasificar);

public record ControlResumenDto(int Ok, int Atencion, int Error, int SinDatos);

public record ControlMapaConsumoDto(
    DateTime FechaControl, int Dias, DateTime? UltimoControlAutomatico, ControlResumenDto Resumen, IReadOnlyList<ControlFranquiciaDto> Franquicias);

#endregion

public interface IControlMapaConsumoService
{
    Task<ControlMapaConsumoDto> ControlarAsync(int dias = 30, bool soloConexion = false);
    /// <summary>Corre el control y guarda el resultado en log.ControlMapaConsumo.</summary>
    Task<ControlMapaConsumoDto> ControlarYRegistrarAsync(int dias = 30);
}

public class ControlMapaConsumoService : IControlMapaConsumoService
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ControlMapaConsumoService(IDbConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<ControlMapaConsumoDto> ControlarAsync(int dias = 30, bool soloConexion = false)
    {
        dias = Math.Clamp(dias, 7, 120);
        using var cn = _connectionFactory.CreateConnection();

        // Ventana por franquicia: los últimos N días hasta su último día con ventas (una franquicia recién conectada
        // con carga histórica se evalúa sobre sus datos, no sobre "hoy").
        const string sqlFranquicias = @"
            WITH ultimo AS (
                SELECT FranquiciaId, MAX(FechaNegocio) AS Hasta FROM fact.VentaTicket WHERE EstaAnulado = 0 GROUP BY FranquiciaId)
            SELECT f.FranquiciaId, f.Codigo, f.Nombre, f.Pais, m.CodigoISO AS Moneda, f.SistemaOrigen,
                   f.PaisIso, f.ZonaMapa, f.Latitud, f.Longitud,
                   CAST(CASE WHEN EXISTS (SELECT 1 FROM dim.NodoConexion n WHERE n.FranquiciaId = f.FranquiciaId AND n.Estado = 'ACTIVE' AND n.Activo = 1)
                        THEN 1 ELSE 0 END AS BIT) AS NodoActivo,
                   DATEADD(DAY, 1 - @Dias, u.Hasta) AS Desde, u.Hasta,
                   ISNULL(dd.DiasConVentas, 0) AS DiasConVentas, ISNULL(dd.DiasConLineas, 0) AS DiasConLineas,
                   ISNULL(dd.DiasSinCotizacion, 0) AS DiasSinCotizacion,
                   ISNULL(dd.Tickets, 0) AS Tickets, ISNULL(dd.TicketsConCubiertos, 0) AS TicketsConCubiertos
            FROM dim.Franquicia f
            JOIN dim.Moneda m ON m.MonedaId = f.MonedaId
            LEFT JOIN ultimo u ON u.FranquiciaId = f.FranquiciaId
            OUTER APPLY (
                SELECT COUNT(*) AS DiasConVentas, SUM(x.ConLineas) AS DiasConLineas, SUM(x.SinCotizacion) AS DiasSinCotizacion,
                       SUM(x.Tickets) AS Tickets, SUM(x.ConCubiertos) AS TicketsConCubiertos
                FROM (
                    SELECT t.FechaNegocio, COUNT(*) AS Tickets,
                           SUM(CASE WHEN ISNULL(t.CantidadCubiertos, 0) > 0 THEN 1 ELSE 0 END) AS ConCubiertos,
                           CASE WHEN EXISTS (SELECT 1 FROM fact.VentaTicketDetalle d JOIN fact.VentaTicket t2 ON t2.VentaTicketId = d.VentaTicketId
                                             WHERE t2.FranquiciaId = f.FranquiciaId AND t2.FechaNegocio = t.FechaNegocio AND t2.EstaAnulado = 0)
                                THEN 1 ELSE 0 END AS ConLineas,
                           CASE WHEN m.CodigoISO <> 'USD' AND NOT EXISTS (SELECT 1 FROM dim.TipoCambio tc
                                             WHERE tc.CodigoMoneda = m.CodigoISO AND tc.Fecha <= t.FechaNegocio)
                                THEN 1 ELSE 0 END AS SinCotizacion
                    FROM fact.VentaTicket t
                    WHERE t.FranquiciaId = f.FranquiciaId AND t.EstaAnulado = 0
                      AND t.FechaNegocio BETWEEN DATEADD(DAY, 1 - @Dias, u.Hasta) AND u.Hasta
                    GROUP BY t.FechaNegocio) x) dd
            WHERE f.Activo = 1";

        const string sqlLineas = @"
            WITH ultimo AS (
                SELECT FranquiciaId, MAX(FechaNegocio) AS Hasta FROM fact.VentaTicket WHERE EstaAnulado = 0 GROUP BY FranquiciaId)
            SELECT t.FranquiciaId, ISNULL(d.NombreProducto, '') AS Nombre, ISNULL(d.CategoriaProducto, '') AS Categoria,
                   ISNULL(d.FamiliaProducto, '') AS Familia,
                   SUM(COALESCE(d.ImporteNetoSinImpuesto, d.ImporteNeto)) AS Importe, SUM(d.Cantidad) AS Cantidad
            FROM fact.VentaTicketDetalle d
            JOIN fact.VentaTicket t ON t.VentaTicketId = d.VentaTicketId
            JOIN ultimo u ON u.FranquiciaId = t.FranquiciaId
            WHERE t.EstaAnulado = 0 AND ISNULL(d.EstaAnulado, 0) = 0
              AND t.FechaNegocio BETWEEN DATEADD(DAY, 1 - @Dias, u.Hasta) AND u.Hasta
            GROUP BY t.FranquiciaId, ISNULL(d.NombreProducto, ''), ISNULL(d.CategoriaProducto, ''), ISNULL(d.FamiliaProducto, '')";

        var franquicias = (await cn.QueryAsync<ControlFranquiciaDatos>(sqlFranquicias, new { Dias = dias }, commandTimeout: 180)).ToList();
        var lineas = (await cn.QueryAsync<ControlLineaCruda>(sqlLineas, new { Dias = dias }, commandTimeout: 180)).ToLookup(l => l.FranquiciaId);
        var ultimoAuto = await cn.ExecuteScalarAsync<DateTime?>("SELECT MAX(FechaControl) FROM log.ControlMapaConsumo");

        var resultado = franquicias
            .Where(f => !soloConexion || f.NodoActivo || f.DiasConVentas > 0)
            .Select(f => ReglaMapaConsumo.Evaluar(f, lineas[f.FranquiciaId].ToList()))
            .OrderByDescending(f => ReglaMapaConsumo.Gravedad(f.Estado)).ThenBy(f => f.Pais).ThenBy(f => f.Nombre)
            .ToList();

        return new ControlMapaConsumoDto(DateTime.UtcNow, dias, ultimoAuto,
            new ControlResumenDto(
                resultado.Count(r => r.Estado == ReglaMapaConsumo.Ok), resultado.Count(r => r.Estado == ReglaMapaConsumo.Atencion),
                resultado.Count(r => r.Estado == ReglaMapaConsumo.Error), resultado.Count(r => r.Estado == ReglaMapaConsumo.SinDatos)),
            resultado);
    }

    public async Task<ControlMapaConsumoDto> ControlarYRegistrarAsync(int dias = 30)
    {
        var control = await ControlarAsync(dias);
        using var cn = _connectionFactory.CreateConnection();
        foreach (var f in control.Franquicias)
        {
            var problemas = f.Chequeos.Where(c => c.Estado != ReglaMapaConsumo.Ok).ToList();
            await cn.ExecuteAsync(@"
                INSERT INTO log.ControlMapaConsumo (FranquiciaId, Estado, Problemas, PctClasificado)
                VALUES (@FranquiciaId, @Estado, @Problemas, @PctClasificado)",
                new
                {
                    f.FranquiciaId, f.Estado, f.PctClasificado,
                    Problemas = problemas.Count == 0 && f.NombresSinClasificar == 0 ? null : JsonSerializer.Serialize(new
                    {
                        chequeos = problemas,
                        sin_clasificar = f.SinClasificar.Take(5).Select(n => n.Nombre)
                    })
                });
        }
        return control;
    }
}

/// <summary>
/// "Agente" del Mapa de Consumo: una vez por día (después de la sincronización de las 10:00) corre el control de la regla
/// para todas las franquicias, lo guarda en log.ControlMapaConsumo y deja un Warning en el log por cada franquicia fuera
/// de regla. Configuración: ControlMapaConsumo:Enabled (true), HoraLocal (11), Minuto (30), Dias (30).
/// </summary>
public class ControlMapaConsumoJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ControlMapaConsumoJob> _logger;
    private readonly bool _enabled;
    private readonly int _hora, _minuto, _dias;
    private readonly TimeZoneInfo _zona;

    public ControlMapaConsumoJob(IServiceProvider serviceProvider, ILogger<ControlMapaConsumoJob> logger, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _enabled = configuration.GetValue("ControlMapaConsumo:Enabled", true);
        _hora = configuration.GetValue("ControlMapaConsumo:HoraLocal", 11);
        _minuto = configuration.GetValue("ControlMapaConsumo:Minuto", 30);
        _dias = configuration.GetValue("ControlMapaConsumo:Dias", 30);
        try { _zona = TimeZoneInfo.FindSystemTimeZoneById(configuration.GetValue("SyncConectores:ZonaHoraria", "America/Argentina/Buenos_Aires")!); }
        catch (TimeZoneNotFoundException) { _zona = TimeZoneInfo.CreateCustomTimeZone("ART", TimeSpan.FromHours(-3), "ART", "ART"); }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("ControlMapaConsumoJob está deshabilitado");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var ahora = DateTime.UtcNow;
                await Task.Delay(ConectoresSyncJob.ProximaEjecucionUtc(ahora, _hora, _minuto, _zona) - ahora, stoppingToken);

                using var scope = _serviceProvider.CreateScope();
                var control = await scope.ServiceProvider.GetRequiredService<IControlMapaConsumoService>().ControlarYRegistrarAsync(_dias);
                _logger.LogWarning("ControlMapaConsumo: {Ok} OK, {Atencion} con atención, {Error} con error, {SinDatos} sin datos",
                    control.Resumen.Ok, control.Resumen.Atencion, control.Resumen.Error, control.Resumen.SinDatos);
                foreach (var f in control.Franquicias.Where(f => f.Estado is ReglaMapaConsumo.Error or ReglaMapaConsumo.Atencion))
                    _logger.LogWarning("ControlMapaConsumo {Codigo} {Estado}: {Problemas}", f.Codigo, f.Estado,
                        string.Join("; ", f.Chequeos.Where(c => c.Estado != ReglaMapaConsumo.Ok).Select(c => $"{c.Codigo}: {c.Mensaje}")));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ControlMapaConsumoJob");
                await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
            }
        }
    }
}
