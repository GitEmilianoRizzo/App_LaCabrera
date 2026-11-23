using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using LaCabrera.Api.Configuration;
using LaCabrera.Api.Repositories;

namespace LaCabrera.Api.Services;

/// <summary>
/// Extractor para NEO (NeoKernel API, POS de La Cabrera Málaga), T-381.
/// API REST: GET http://restaurantelacabrera.webreports.cloud:3001/api/v1/b2b/..., header x-api-key.
///
/// Cuidados:
/// - SOLO GET. Nunca llamar a los POST workingdaydone ni workingdayticketdone, que marcan jornadas o tickets como
///   exportados del lado del proveedor: no hacen falta, la idempotencia es nuestra.
/// - La API key NO va en la base ni en el repo: se lee de la variable de entorno que indica connection.api_key_env
///   (NEO_MALAGA_API_KEY). Nunca se loguea; si un error de la API la incluye, se enmascara.
/// - El servidor es el del local: una llamada por vez y al menos 1 s entre llamadas (connection.pausa_ms).
/// - En cada corrida se releen las últimas sync.dias_releer jornadas (cierres tardíos) y las jornadas sin crudo desde
///   sync.fecha_inicio (carga inicial, que así no se repite). No se carga la jornada en curso (hora de España).
/// - Upsert idempotente: cabecera por orderId (ExternalTicketId); líneas y pagos se reemplazan por ticket.
/// - El crudo de cada jornada se guarda (sin nombres de clientes) y se puede reprocesar sin llamar a la API.
/// </summary>
public interface INeoExtractorService
{
    Task<EjecucionResultDto> EjecutarExtraccionAsync(int nodoConexionId, DateTime? fechaNegocio = null, string modo = "MANUAL");
    Task<EjecucionResultDto> EjecutarExtraccionRangoAsync(int nodoConexionId, DateTime fechaDesde, DateTime fechaHasta, string modo = "CATCHUP");
    Task<EjecucionResultDto> EjecutarExtraccionDesdeUltimoAsync(int nodoConexionId, string modo = "MANUAL");
    Task<EjecucionResultDto> ReprocesarDesdeCrudoAsync(int nodoConexionId, DateTime fechaDesde, DateTime fechaHasta);
}

public class NeoExtractorService : INeoExtractorService
{
    public const string SchemaVersion = "NEO_V1";
    private const string Fuente = "NEO";

    private static readonly SemaphoreSlim _candado = new(1, 1);
    private static DateTime _ultimaLlamadaUtc = DateTime.MinValue;
    private static (DateTime Cargado, NeoImportes.Catalogo Catalogo)? _catalogoCache;

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IConexionRepository _conexionRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NeoExtractorService> _logger;

    public NeoExtractorService(
        IDbConnectionFactory connectionFactory,
        IConexionRepository conexionRepository,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<NeoExtractorService> logger)
    {
        _connectionFactory = connectionFactory;
        _conexionRepository = conexionRepository;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<EjecucionResultDto> EjecutarExtraccionAsync(int nodoConexionId, DateTime? fechaNegocio = null, string modo = "MANUAL")
    {
        await _candado.WaitAsync();
        try
        {
            var ctx = await CargarContextoAsync(nodoConexionId);
            if (ctx.Error != null) return Fallo(ctx.Error);
            var dia = (fechaNegocio ?? NeoSync.Ayer(DateTime.UtcNow, ctx.Zona)).Date;
            if (dia > NeoSync.Ayer(DateTime.UtcNow, ctx.Zona)) return Fallo($"La jornada {dia:yyyy-MM-dd} todavía no cerró (hora de España)");
            return await PedirDiasAsync(ctx, new[] { dia }, modo);
        }
        finally { _candado.Release(); }
    }

    public async Task<EjecucionResultDto> EjecutarExtraccionRangoAsync(int nodoConexionId, DateTime fechaDesde, DateTime fechaHasta, string modo = "CATCHUP")
    {
        await _candado.WaitAsync();
        try
        {
            var ctx = await CargarContextoAsync(nodoConexionId);
            if (ctx.Error != null) return Fallo(ctx.Error);
            var ayer = NeoSync.Ayer(DateTime.UtcNow, ctx.Zona);
            var hasta = fechaHasta.Date > ayer ? ayer : fechaHasta.Date;
            var dias = new List<DateTime>();
            for (var d = fechaDesde.Date; d <= hasta; d = d.AddDays(1)) dias.Add(d);
            return await PedirDiasAsync(ctx, dias, modo);
        }
        finally { _candado.Release(); }
    }

    public async Task<EjecucionResultDto> EjecutarExtraccionDesdeUltimoAsync(int nodoConexionId, string modo = "MANUAL")
    {
        await _candado.WaitAsync();
        try
        {
            var ctx = await CargarContextoAsync(nodoConexionId);
            if (ctx.Error != null) return Fallo(ctx.Error);
            if (ctx.Config!.Sync!.FechaInicio is not { } inicio) return Fallo("Falta sync.fecha_inicio en la configuración del nodo");

            var ayer = NeoSync.Ayer(DateTime.UtcNow, ctx.Zona);
            var conCrudo = await GetDiasConCrudoAsync(ctx.Nodo!.FranquiciaId, inicio.Date, ayer);
            var dias = NeoSync.DiasAPedir(inicio.Date, ayer, conCrudo, ctx.Config.Sync.DiasReleer, ctx.Config.Sync.MaxDiasPorCorrida);
            if (dias.Count == 0) return new EjecucionResultDto { Success = true, Message = "Datos al día: no hay jornadas para pedir a NEO" };
            return await PedirDiasAsync(ctx, dias, modo);
        }
        finally { _candado.Release(); }
    }

    public async Task<EjecucionResultDto> ReprocesarDesdeCrudoAsync(int nodoConexionId, DateTime fechaDesde, DateTime fechaHasta)
    {
        await _candado.WaitAsync();
        try
        {
            var ctx = await CargarContextoAsync(nodoConexionId, exigirKey: false);
            if (ctx.Error != null) return Fallo(ctx.Error);
            var total = new EjecucionResultDto { Success = true };
            int dias = 0, sinCrudo = 0;
            for (var dia = fechaDesde.Date; dia <= fechaHasta.Date; dia = dia.AddDays(1))
            {
                var crudo = await GetCrudoAsync(ctx.Nodo!.FranquiciaId, dia);
                if (crudo == null) { sinCrudo++; continue; }
                var neoDia = JsonSerializer.Deserialize<NeoDia>(crudo, NeoImportes.Json)!;
                Acumular(total, await ProcesarDiaAsync(ctx, dia, neoDia, crudo, NeoImportes.Catalogo.Vacio, "RAW", "MANUAL", usarCategoriasGuardadas: true));
                dias++;
            }
            total.Message = $"Reproceso desde crudo: {dias} jornadas, {total.TicketsProcesados} tickets, {total.LineasProcesadas} líneas"
                + (sinCrudo > 0 ? $"; {sinCrudo} jornadas sin crudo" : "");
            return total;
        }
        finally { _candado.Release(); }
    }

    #region Requests a la API

    private async Task<EjecucionResultDto> PedirDiasAsync(Contexto ctx, IReadOnlyList<DateTime> dias, string modo)
    {
        var total = new EjecucionResultDto { Success = true };
        var resumen = new List<string>();
        NeoImportes.Catalogo? catalogo = null;
        int pedidos = 0;

        foreach (var dia in dias)
        {
            var ejecucionId = await RegistrarInicioEjecucionAsync(ctx.Nodo!.NodoConexionId, dia, modo);
            try
            {
                var neoDia = await BajarDiaAsync(ctx, dia);
                if (neoDia.TicketHeaders.Count > 0 && catalogo == null)
                    catalogo = await GetCatalogoAsync(ctx);
                var crudo = JsonSerializer.Serialize(neoDia, NeoImportes.Json);
                var r = await ProcesarDiaAsync(ctx, dia, neoDia, crudo, catalogo ?? NeoImportes.Catalogo.Vacio, "API", modo, ejecucionId: ejecucionId);
                Acumular(total, r);
                resumen.Add(r.Message);
                pedidos++;
            }
            catch (NeoApiException ex)
            {
                await RegistrarFinEjecucionAsync(ejecucionId, "ERROR", 0, 0, 1, ex.Message);
                total.Success = false;
                resumen.Add($"{dia:yyyy-MM-dd}: {ex.Message}");
                // Key rechazada o licencia: no tiene sentido seguir con otras jornadas
                if (ex.Cortar) break;
            }
            catch (Exception ex)
            {
                _logger.LogError("NEO {Fecha}: {Error}", dia.ToString("yyyy-MM-dd"), Enmascarar(ctx, ex.Message));
                await RegistrarFinEjecucionAsync(ejecucionId, "ERROR", 0, 0, 1, Enmascarar(ctx, ex.Message));
                total.Success = false;
                resumen.Add($"{dia:yyyy-MM-dd}: {Enmascarar(ctx, ex.Message)}");
            }
        }

        total.Message = $"{pedidos}/{dias.Count} jornadas pedidas a NEO, {total.TicketsProcesados} tickets, {total.LineasProcesadas} líneas. "
            + string.Join(" | ", resumen.TakeLast(10));
        return total;
    }

    private async Task<NeoDia> BajarDiaAsync(Contexto ctx, DateTime dia)
    {
        var shop = ctx.Config!.Connection!.Shop;
        var j = NeoImportes.Jornada(dia);
        var q = $"shop={Uri.EscapeDataString(shop)}&date={j}";
        var neoDia = new NeoDia
        {
            Shop = shop,
            Jornada = j,
            TicketHeaders = await GetDataAsync<List<NeoTicketHeader>>(ctx, $"/api/v1/b2b/sales/ticketheaders?{q}") ?? new(),
            TicketLines = await GetDataAsync<List<NeoTicketLine>>(ctx, $"/api/v1/b2b/sales/ticketlines?{q}") ?? new(),
            TicketPayments = await GetDataAsync<List<NeoTicketPayment>>(ctx, $"/api/v1/b2b/sales/ticketpayments?{q}") ?? new(),
            TicketTaxes = await GetDataAsync<List<NeoTicketTax>>(ctx, $"/api/v1/b2b/sales/tickettaxes?{q}") ?? new(),
            CashReports = await GetDataAsync<List<NeoCashReport>>(ctx, $"/api/v1/b2b/sales/cashreports?{q}") ?? new(),
            DailySummary = await GetDataAsync<NeoDailySummary>(ctx, $"/api/v1/b2b/sales/dailysummary?{q}"),
        };

        // docs viene paginado (offset/limit + total); de cada documento solo se guardan pedido, estado y comensales
        if (neoDia.TicketHeaders.Count > 0)
        {
            const int limite = 100;
            for (var offset = 0; ; offset += limite)
            {
                var (docs, totalDocs) = await GetPaginaAsync<NeoDoc>(ctx, $"/api/v1/b2b/sales/docs?{q}&offset={offset}&limit={limite}");
                neoDia.Docs.AddRange(docs.Select(d => new NeoDocResumen { Order = d.Order, Status = d.Status, SubType = d.SubType, TotalClients = d.BuyerParty?.TotalClients }));
                if (docs.Count < limite || (totalDocs is { } t && offset + limite >= t)) break;
            }
        }
        return neoDia;
    }

    /// <summary>Productos, familias y subfamilias. El catálogo es pesado (~32 MB): se baja como mucho una vez cada 12 h.</summary>
    private async Task<NeoImportes.Catalogo> GetCatalogoAsync(Contexto ctx)
    {
        if (_catalogoCache is { } c && DateTime.UtcNow - c.Cargado < TimeSpan.FromHours(12)) return c.Catalogo;
        try
        {
            var productos = new Dictionary<string, (string?, string?)>();
            const int limite = 1000;
            for (var offset = 0; ; offset += limite)
            {
                var (pagina, total) = await GetPaginaAsync<NeoProducto>(ctx, $"/api/v1/b2b/products?offset={offset}&limit={limite}");
                foreach (var p in pagina.Where(p => !string.IsNullOrWhiteSpace(p.Id)))
                    productos[p.Id!.Trim()] = (p.Family, p.Subfamily);
                if (pagina.Count < limite || (total is { } t && offset + limite >= t)) break;
            }
            static Dictionary<string, string> Nombres(List<NeoFamilia>? fs) => (fs ?? new())
                .Where(f => !string.IsNullOrWhiteSpace(f.Code))
                .ToDictionary(f => f.Code!, f => (f.Name?.FirstOrDefault(n => n.Language == "es") ?? f.Name?.FirstOrDefault())?.Text?.Trim() ?? f.Code!);
            var familias = Nombres(await GetDataAsync<List<NeoFamilia>>(ctx, "/api/v1/b2b/families"));
            var subfamilias = Nombres(await GetDataAsync<List<NeoFamilia>>(ctx, "/api/v1/b2b/subfamilies"));
            var catalogo = new NeoImportes.Catalogo(productos, familias, subfamilias);
            _catalogoCache = (DateTime.UtcNow, catalogo);
            return catalogo;
        }
        catch (NeoApiException ex) when (!ex.Cortar)
        {
            // Sin catálogo se carga igual: las líneas quedan sin familia y se reprocesan después
            _logger.LogWarning("NEO: no se pudo bajar el catálogo de productos: {Error}", ex.Message);
            return NeoImportes.Catalogo.Vacio;
        }
    }

    private async Task<T?> GetDataAsync<T>(Contexto ctx, string ruta)
    {
        var json = await GetAsync(ctx, ruta);
        var env = JsonSerializer.Deserialize<NeoEnvelope<T>>(json, NeoImportes.Json);
        if (env?.Error is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined } err)
            throw new NeoApiException($"NEO devolvió error en {RutaSinQuery(ruta)}: {Enmascarar(ctx, Recortar(err.ToString(), 300))}", cortar: false);
        return env == null ? default : env.Data;
    }

    private async Task<(List<T> Datos, int? Total)> GetPaginaAsync<T>(Contexto ctx, string ruta)
    {
        var json = await GetAsync(ctx, ruta);
        var env = JsonSerializer.Deserialize<NeoEnvelope<List<T>>>(json, NeoImportes.Json);
        return (env?.Data ?? new List<T>(), env?.Total);
    }

    /// <summary>GET con pausa entre llamadas, timeout y reintentos con backoff ante errores transitorios.</summary>
    private async Task<string> GetAsync(Contexto ctx, string ruta)
    {
        var conn = ctx.Config!.Connection!;
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(conn.TimeoutSeconds > 0 ? conn.TimeoutSeconds : 120);
        var url = $"{conn.BaseUrl.TrimEnd('/')}{ruta}";

        for (var intento = 1; ; intento++)
        {
            await EsperarTurnoAsync(conn.PausaMs);
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.TryAddWithoutValidation("x-api-key", ctx.ApiKey);
                request.Headers.TryAddWithoutValidation("Accept", "application/json");
                using var response = await client.SendAsync(request);
                var cuerpo = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode) return cuerpo;

                var codigo = (int)response.StatusCode;
                var detalle = Enmascarar(ctx, Recortar(cuerpo, 300));
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                    throw new NeoApiException($"NEO rechazó la API key (HTTP {codigo}) en {RutaSinQuery(ruta)}: {detalle}", cortar: true);
                if (detalle.Contains("licen", StringComparison.OrdinalIgnoreCase))
                    throw new NeoApiException($"NEO informa un problema de licencia B2B (HTTP {codigo}): {detalle}", cortar: true);
                if (codigo is >= 500 or 429 && intento < conn.Reintentos)
                {
                    _logger.LogWarning("NEO HTTP {Codigo} en {Ruta}, reintento {Intento}", codigo, RutaSinQuery(ruta), intento);
                    await Task.Delay(NeoSync.Backoff(intento));
                    continue;
                }
                throw new NeoApiException($"NEO respondió HTTP {codigo} en {RutaSinQuery(ruta)}: {detalle}", cortar: false);
            }
            catch (Exception ex) when (ex is TaskCanceledException or HttpRequestException && intento < conn.Reintentos)
            {
                _logger.LogWarning("NEO sin respuesta en {Ruta} ({Tipo}), reintento {Intento}", RutaSinQuery(ruta), ex.GetType().Name, intento);
                await Task.Delay(NeoSync.Backoff(intento));
            }
        }
    }

    private static async Task EsperarTurnoAsync(int pausaMs)
    {
        var espera = _ultimaLlamadaUtc.AddMilliseconds(Math.Max(1000, pausaMs)) - DateTime.UtcNow;
        if (espera > TimeSpan.Zero) await Task.Delay(espera);
        _ultimaLlamadaUtc = DateTime.UtcNow;
    }

    #endregion

    #region Carga

    private async Task<EjecucionResultDto> ProcesarDiaAsync(
        Contexto ctx, DateTime dia, NeoDia neoDia, string crudo, NeoImportes.Catalogo catalogo, string origen, string modo,
        long? ejecucionId = null, bool usarCategoriasGuardadas = false)
    {
        var nodo = ctx.Nodo!;
        ejecucionId ??= await RegistrarInicioEjecucionAsync(nodo.NodoConexionId, dia, modo);
        var batchId = await CrearBatchIngestaAsync(nodo.FranquiciaId, dia, crudo, origen);

        var avisos = NeoImportes.Controlar(neoDia);
        var tickets = NeoImportes.CalcularTickets(neoDia, catalogo);
        var mapeoPagos = await GetMapeoMediosPagoAsync(nodo.NodoConexionId);

        int cargados = 0, lineas = 0, errores = 0;
        foreach (var t in tickets)
        {
            try
            {
                lineas += await GuardarTicketAsync(t, nodo.FranquiciaId, batchId, nodo.NodoConexionId, nodo.Moneda, mapeoPagos, usarCategoriasGuardadas);
                cargados++;
            }
            catch (Exception ex)
            {
                errores++;
                _logger.LogError(ex, "NEO: error cargando ticket {OrderId}", t.ExternalTicketId);
                await RegistrarErrorIngestaAsync(batchId, "ERROR", $"Ticket {t.ExternalTicketId}: {ex.Message}");
            }
        }

        var totalDia = tickets.Sum(t => t.ImporteBruto);
        var arqueo = NeoImportes.VentasArqueo(neoDia);
        if (neoDia.CashReports.Count > 0 && Math.Abs(arqueo - totalDia) > NeoImportes.Tolerancia)
            avisos.Add($"arqueo de caja {arqueo:0.00} vs Σ tickets {totalDia:0.00}");
        foreach (var a in avisos) await RegistrarErrorIngestaAsync(batchId, "WARNING", a);

        await ActualizarBatchAsync(batchId, cargados, lineas, errores);
        // La diferencia con el arqueo sola no es un problema de la carga (la caja puede cerrarse antes del último cobro)
        var avisosCarga = avisos.Count(a => !a.StartsWith("arqueo"));
        var estado = errores > 0 ? (cargados > 0 ? "WARNING" : "ERROR") : (avisosCarga > 0 ? "WARNING" : "SUCCESS");
        var mensaje = avisos.Count > 0 ? Recortar(string.Join("; ", avisos), 1500) : null;
        await RegistrarFinEjecucionAsync(ejecucionId.Value, estado, cargados, lineas, errores, mensaje);
        await ActualizarEstadoSincronizacionAsync(nodo.NodoConexionId, nodo.FranquiciaId, estado, batchId);

        _logger.LogInformation("NEO {Jornada}: {Tickets} tickets, total {Total:0.00} EUR, {Avisos} avisos", neoDia.Jornada, cargados, totalDia, avisos.Count);
        return new EjecucionResultDto
        {
            Success = estado != "ERROR",
            Message = $"{dia:yyyy-MM-dd}: {cargados} tickets, {totalDia:0.00} EUR" + (errores > 0 ? $", {errores} errores" : "") + (avisos.Count > 0 ? $" ({avisos.Count} avisos)" : ""),
            TicketsProcesados = cargados,
            LineasProcesadas = lineas,
            ErrorsCount = errores,
            EjecucionId = ejecucionId,
            BatchId = batchId
        };
    }

    private async Task<int> GuardarTicketAsync(
        NeoImportes.TicketCalculado t, int franquiciaId, long batchId, int nodoConexionId, string moneda,
        Dictionary<string, string> mapeoPagos, bool usarCategoriasGuardadas)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();

        var existente = await connection.QueryFirstOrDefaultAsync<long?>(@"
            SELECT VentaTicketId FROM fact.VentaTicket WHERE FranquiciaId = @FranquiciaId AND ExternalTicketId = @ExternalTicketId",
            new { FranquiciaId = franquiciaId, t.ExternalTicketId }, tx);

        // En un reproceso sin catálogo se conservan las familias que ya tenían las líneas
        var categoriasPrevias = new Dictionary<string, (string? Cat, string? Fam, string? Sub)>();
        if (existente.HasValue && usarCategoriasGuardadas)
        {
            var previas = await connection.QueryAsync<(string ExternalLineId, string? Cat, string? Fam, string? Sub)>(@"
                SELECT ExternalLineId, CategoriaProducto, FamiliaProducto, SubFamiliaProducto FROM fact.VentaTicketDetalle WHERE VentaTicketId = @Id",
                new { Id = existente.Value }, tx);
            foreach (var p in previas) categoriasPrevias[p.ExternalLineId] = (p.Cat, p.Fam, p.Sub);
        }

        var datos = new
        {
            BatchId = batchId, FranquiciaId = franquiciaId, t.ExternalTicketId, t.NumeroTicket, t.TipoDocumentoFiscal, t.NumeroDocumentoFiscal,
            Estado = t.EsDevolucion ? "REFUNDED" : "CLOSED", t.FechaNegocio, t.FechaApertura, t.FechaCierre, t.PeriodoComida,
            NumeroMesa = RecortarN(t.Mesa, 20), AreaMesa = RecortarN(t.Zona, 100), NombreMozo = RecortarN(t.Operador, 200), TurnoCodigo = RecortarN(t.Turno, 50),
            CodigoMoneda = moneda, t.ImporteBruto, t.ImporteNeto, t.ImporteImpuesto, t.ImportePropina, t.ImporteTotalPagado,
            t.Cubiertos, TieneDatosCubiertos = t.Cubiertos.HasValue, TieneDatosMesa = t.Mesa != null
        };

        long ticketId;
        if (existente.HasValue)
        {
            ticketId = existente.Value;
            await connection.ExecuteAsync(@"
                UPDATE fact.VentaTicket SET
                    IngestionBatchId = @BatchId, NumeroTicket = @NumeroTicket, TipoDocumentoFiscal = @TipoDocumentoFiscal,
                    NumeroDocumentoFiscal = @NumeroDocumentoFiscal, Estado = @Estado, FechaNegocio = @FechaNegocio,
                    FechaApertura = @FechaApertura, FechaCierre = @FechaCierre, PeriodoComida = @PeriodoComida,
                    NumeroMesa = @NumeroMesa, AreaMesa = @AreaMesa, NombreMozo = @NombreMozo, TurnoCodigo = @TurnoCodigo,
                    CantidadCubiertos = @Cubiertos, TieneDatosCubiertos = @TieneDatosCubiertos, TieneDatosMesa = @TieneDatosMesa,
                    CodigoMoneda = @CodigoMoneda, ImporteBruto = @ImporteBruto, ImporteDescuento = 0, ImporteNeto = @ImporteNeto,
                    ImporteImpuesto = @ImporteImpuesto, ImporteNetoSinImpuesto = @ImporteNeto, ImportesIncluyenImpuesto = 0, CalidadImpuesto = 'POS',
                    ImportePropina = @ImportePropina, ImporteTotalPagado = @ImporteTotalPagado, TieneDescuento = 0, EstaAnulado = 0,
                    FechaModificacion = SYSDATETIME(), UsuarioModificacion = 'NEO'
                WHERE VentaTicketId = @TicketId;
                DELETE FROM fact.VentaTicketDetalle WHERE VentaTicketId = @TicketId;
                DELETE FROM fact.VentaTicketMedioPago WHERE VentaTicketId = @TicketId;",
                new
                {
                    datos.BatchId, datos.NumeroTicket, datos.TipoDocumentoFiscal, datos.NumeroDocumentoFiscal, datos.Estado, datos.FechaNegocio,
                    datos.FechaApertura, datos.FechaCierre, datos.PeriodoComida, datos.NumeroMesa, datos.AreaMesa, datos.NombreMozo, datos.TurnoCodigo,
                    datos.Cubiertos, datos.TieneDatosCubiertos, datos.TieneDatosMesa, datos.CodigoMoneda, datos.ImporteBruto, datos.ImporteNeto,
                    datos.ImporteImpuesto, datos.ImportePropina, datos.ImporteTotalPagado, TicketId = ticketId
                }, tx);
        }
        else
        {
            ticketId = await connection.QuerySingleAsync<long>(@"
                INSERT INTO fact.VentaTicket (
                    IngestionBatchId, FranquiciaId, ExternalTicketId, NumeroTicket, TipoDocumentoFiscal, NumeroDocumentoFiscal, Estado,
                    FechaNegocio, FechaApertura, FechaCierre, PeriodoComida, NumeroMesa, AreaMesa, NombreMozo, TurnoCodigo,
                    CantidadCubiertos, CodigoMoneda, ImporteBruto, ImporteDescuento, ImporteNeto, ImporteImpuesto,
                    ImporteNetoSinImpuesto, ImportesIncluyenImpuesto, CalidadImpuesto, ImporteServicio, ImportePropina, ImporteTotalPagado,
                    TieneDescuento, EstaAnulado, TieneDatosCubiertos, TieneDatosMozo, TieneDatosMesa, FuenteSistema)
                OUTPUT INSERTED.VentaTicketId
                VALUES (
                    @BatchId, @FranquiciaId, @ExternalTicketId, @NumeroTicket, @TipoDocumentoFiscal, @NumeroDocumentoFiscal, @Estado,
                    @FechaNegocio, @FechaApertura, @FechaCierre, @PeriodoComida, @NumeroMesa, @AreaMesa, @NombreMozo, @TurnoCodigo,
                    @Cubiertos, @CodigoMoneda, @ImporteBruto, 0, @ImporteNeto, @ImporteImpuesto,
                    @ImporteNeto, 0, 'POS', 0, @ImportePropina, @ImporteTotalPagado,
                    0, 0, @TieneDatosCubiertos, 0, @TieneDatosMesa, 'NEO')", datos, tx);
        }

        foreach (var l in t.Lineas)
        {
            var (cat, fam, sub) = (l.Categoria, l.Familia, l.SubFamilia);
            if (fam == null && categoriasPrevias.TryGetValue(l.ExternalLineId, out var previa)) (cat, fam, sub) = previa;
            await connection.ExecuteAsync(@"
                INSERT INTO fact.VentaTicketDetalle (
                    VentaTicketId, IngestionBatchId, FranquiciaId, ExternalLineId, CodigoProducto, NombreProducto,
                    CategoriaProducto, FamiliaProducto, SubFamiliaProducto, Cantidad, PrecioUnitario,
                    ImporteBruto, ImporteDescuento, ImporteNeto, ImporteImpuesto, ImporteNetoSinImpuesto, ImportesIncluyenImpuesto,
                    CalidadImpuesto, TasaImpuesto, TieneDescuento, EstaAnulado, MotivoAnulacion, Notas)
                VALUES (
                    @TicketId, @BatchId, @FranquiciaId, @ExternalLineId, @CodigoProducto, @NombreProducto,
                    @Cat, @Fam, @Sub, @Cantidad, @PrecioUnitario,
                    @ImporteBruto, 0, @ImporteNeto, @ImporteImpuesto, @ImporteNeto, 0,
                    'POS', @TasaImpuesto, 0, @EstaAnulado, @MotivoAnulacion, @Notas)",
                new
                {
                    TicketId = ticketId, BatchId = batchId, FranquiciaId = franquiciaId, l.ExternalLineId,
                    CodigoProducto = Recortar(l.CodigoProducto, 100), NombreProducto = Recortar(l.NombreProducto, 200),
                    Cat = cat, Fam = RecortarN(fam, 100), Sub = RecortarN(sub, 100), l.Cantidad, l.PrecioUnitario,
                    l.ImporteBruto, l.ImporteNeto, l.ImporteImpuesto, l.TasaImpuesto, l.EstaAnulado,
                    MotivoAnulacion = l.EstaAnulado ? "NO_AFECTA_IMPORTE" : null, Notas = RecortarN(l.Notas, 500)
                }, tx);
        }

        foreach (var p in t.Pagos)
        {
            var codigo = mapeoPagos.GetValueOrDefault(p.Medio, "OTHER");
            if (!mapeoPagos.ContainsKey(p.Medio))
                await RegistrarValorNoMapeadoAsync(connection, tx, nodoConexionId, "MEDIO_PAGO", p.Medio);
            // Sin columna Referencia: llega con la migración de T-380, que todavía no está en producción. El código NEO
            // original (TCR, CG, FORK…) queda en el crudo de la jornada y en dim.ValorNoMapeado; se remapea reprocesando.
            await connection.ExecuteAsync(@"
                INSERT INTO fact.VentaTicketMedioPago (VentaTicketId, IngestionBatchId, FranquiciaId, CodigoMedioPago, Importe)
                VALUES (@TicketId, @BatchId, @FranquiciaId, @CodigoMedioPago, @Importe)",
                new { TicketId = ticketId, BatchId = batchId, FranquiciaId = franquiciaId, CodigoMedioPago = codigo, p.Importe }, tx);
        }

        tx.Commit();
        return t.Lineas.Count;
    }

    #endregion

    #region Configuración y consultas

    private record Contexto(Models.DTOs.NodoConexionDto? Nodo, NeoConfiguracion? Config, TimeZoneInfo Zona, string ApiKey, string? Error);

    private async Task<Contexto> CargarContextoAsync(int nodoConexionId, bool exigirKey = true)
    {
        var nodo = await _conexionRepository.GetNodoByIdAsync(nodoConexionId);
        if (nodo == null) return new Contexto(null, null, TimeZoneInfo.Utc, "", "Nodo no encontrado");

        var json = await _conexionRepository.GetConfiguracionJsonAsync(nodoConexionId);
        if (string.IsNullOrEmpty(json)) return new Contexto(nodo, null, TimeZoneInfo.Utc, "", "Nodo sin configuración");
        var config = JsonSerializer.Deserialize<NeoConfiguracion>(json, NeoImportes.Json);
        if (config?.Connection == null) return new Contexto(nodo, config, TimeZoneInfo.Utc, "", "Configuración de conexión inválida");
        config.Sync ??= new NeoSyncConfig();

        var key = string.IsNullOrWhiteSpace(config.Connection.ApiKeyEnv) ? "" : (_configuration[config.Connection.ApiKeyEnv] ?? "").Trim();
        if (exigirKey && (key.Length == 0 || string.IsNullOrWhiteSpace(config.Connection.BaseUrl) || string.IsNullOrWhiteSpace(config.Connection.Shop)))
            return new Contexto(nodo, config, TimeZoneInfo.Utc, "", $"Faltan base_url, shop o la variable de entorno {config.Connection.ApiKeyEnv} con la API key de NEO");

        return new Contexto(nodo, config, NeoSync.Zona(config.Sync.ZonaHoraria ?? nodo.Timezone), key, null);
    }

    private async Task<HashSet<DateTime>> GetDiasConCrudoAsync(int franquiciaId, DateTime desde, DateTime hasta)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dias = await connection.QueryAsync<DateTime>(@"
            SELECT DISTINCT CAST(FechaNegocio AS DATE) FROM stg.IngestionBatch
            WHERE FranquiciaId = @FranquiciaId AND SchemaVersion = @Schema AND OrigenIngesta = 'API'
              AND Estado IN ('COMPLETED', 'COMPLETED_WITH_ERRORS') AND FechaNegocio >= @Desde AND FechaNegocio <= @Hasta",
            new { FranquiciaId = franquiciaId, Schema = SchemaVersion, Desde = desde, Hasta = hasta });
        return dias.Select(d => d.Date).ToHashSet();
    }

    private async Task<string?> GetCrudoAsync(int franquiciaId, DateTime dia)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<string?>(@"
            SELECT TOP 1 r.JsonContent FROM stg.IngestionBatch b
            JOIN stg.IngestionBatchRawJson r ON r.IngestionBatchId = b.IngestionBatchId
            WHERE b.FranquiciaId = @FranquiciaId AND b.SchemaVersion = @Schema AND CAST(b.FechaNegocio AS DATE) = @Dia AND b.OrigenIngesta = 'API'
            ORDER BY b.IngestionBatchId DESC",
            new { FranquiciaId = franquiciaId, Schema = SchemaVersion, Dia = dia.Date });
    }

    private async Task<Dictionary<string, string>> GetMapeoMediosPagoAsync(int nodoConexionId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var filas = await connection.QueryAsync<(string CodigoOrigen, string MedioPagoDestino)>(
            "SELECT CodigoOrigen, MedioPagoDestino FROM dim.MapeoMedioPago WHERE NodoConexionId = @NodoId AND Activo = 1",
            new { NodoId = nodoConexionId });
        return filas.ToDictionary(f => f.CodigoOrigen, f => f.MedioPagoDestino, StringComparer.OrdinalIgnoreCase);
    }

    #endregion

    #region Helpers de base de datos

    private async Task<long> RegistrarInicioEjecucionAsync(int nodoConexionId, DateTime fechaNegocio, string modo)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleAsync<long>(@"
            INSERT INTO log.EjecucionNodo (NodoConexionId, FechaNegocio, InicioEjecucion, Estado, ModoEjecucion, ApiVersionOrigen)
            OUTPUT INSERTED.EjecucionNodoId
            VALUES (@NodoConexionId, @FechaNegocio, GETUTCDATE(), 'RUNNING', @Modo, @Marca)",
            new { NodoConexionId = nodoConexionId, FechaNegocio = fechaNegocio, Modo = modo, Marca = SchemaVersion });
    }

    private async Task RegistrarFinEjecucionAsync(long ejecucionId, string estado, int tickets, int lineas, int errores, string? mensaje)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(@"
            UPDATE log.EjecucionNodo
            SET FinEjecucion = GETUTCDATE(), Estado = @Estado, TicketsProcesados = @Tickets,
                LineasProcesadas = @Lineas, ErrorsCount = @Errores, Mensajes = @Mensaje
            WHERE EjecucionNodoId = @EjecucionId",
            new { EjecucionId = ejecucionId, Estado = estado, Tickets = tickets, Lineas = lineas, Errores = errores, Mensaje = mensaje });
    }

    private async Task ActualizarEstadoSincronizacionAsync(int nodoConexionId, int franquiciaId, string estado, long batchId)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(@"
            UPDATE dim.NodoConexion
            SET UltimaSincronizacion = GETUTCDATE(), UltimoEstado = @Estado, UltimoBatchId = @BatchId, ModificadoEn = GETUTCDATE()
            WHERE NodoConexionId = @NodoId;
            UPDATE dim.Franquicia
            SET EstadoIntegracion = @EstadoIntegracion, UltimaSincronizacion = GETUTCDATE()
            WHERE FranquiciaId = @FranquiciaId;",
            new
            {
                NodoId = nodoConexionId, Estado = estado, BatchId = batchId.ToString(), FranquiciaId = franquiciaId,
                EstadoIntegracion = estado == "SUCCESS" ? "OK" : (estado == "WARNING" ? "WARNING" : "ERROR")
            });
    }

    private async Task<long> CrearBatchIngestaAsync(int franquiciaId, DateTime fechaNegocio, string rawJson, string origen)
    {
        using var connection = _connectionFactory.CreateConnection();
        var batchId = await connection.QuerySingleAsync<long>(@"
            INSERT INTO stg.IngestionBatch (BatchId, FranquiciaId, FechaNegocio, SchemaVersion, TipoCarga, OrigenIngesta, Estado)
            OUTPUT INSERTED.IngestionBatchId
            VALUES (@BatchGuid, @FranquiciaId, @FechaNegocio, @Schema, @TipoCarga, @Origen, 'PROCESSING')",
            new
            {
                BatchGuid = Guid.NewGuid().ToString(), FranquiciaId = franquiciaId, FechaNegocio = fechaNegocio, Schema = SchemaVersion,
                TipoCarga = origen == "RAW" ? "CORRECTION" : "FULL_DAY", Origen = origen
            });
        if (origen != "RAW")
            await connection.ExecuteAsync("INSERT INTO stg.IngestionBatchRawJson (IngestionBatchId, JsonContent) VALUES (@BatchId, @RawJson)",
                new { BatchId = batchId, RawJson = rawJson });
        return batchId;
    }

    private async Task ActualizarBatchAsync(long batchId, int tickets, int lineas, int errores)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(@"
            UPDATE stg.IngestionBatch SET Estado = @Estado, TicketCountCalculado = @Tickets, ItemLineCountCalculado = @Lineas
            WHERE IngestionBatchId = @BatchId",
            new { BatchId = batchId, Tickets = tickets, Lineas = lineas, Estado = errores > 0 ? (tickets > 0 ? "COMPLETED_WITH_ERRORS" : "ERROR") : "COMPLETED" });
    }

    private async Task RegistrarErrorIngestaAsync(long batchId, string severidad, string mensaje)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(@"
            INSERT INTO stg.IngestionError (IngestionBatchId, Severidad, CodigoError, MensajeError)
            VALUES (@BatchId, @Severidad, @Codigo, @Mensaje)",
            new { BatchId = batchId, Severidad = severidad, Codigo = severidad == "WARNING" ? "CONTROL_TOTALES" : "PROCESSING_ERROR", Mensaje = Recortar(mensaje, 1000) });
    }

    private static async Task RegistrarValorNoMapeadoAsync(System.Data.IDbConnection connection, System.Data.IDbTransaction tx, int nodoConexionId, string tipo, string codigo)
    {
        await connection.ExecuteAsync(@"
            MERGE dim.ValorNoMapeado AS target
            USING (SELECT @NodoId AS NodoConexionId, @Tipo AS TipoMapeo, @Codigo AS CodigoOrigen) AS source
            ON target.NodoConexionId = source.NodoConexionId AND target.TipoMapeo = source.TipoMapeo AND target.CodigoOrigen = source.CodigoOrigen
            WHEN MATCHED THEN UPDATE SET Ocurrencias = target.Ocurrencias + 1, UltimaVez = GETUTCDATE()
            WHEN NOT MATCHED THEN
                INSERT (NodoConexionId, TipoMapeo, CodigoOrigen, NombreOrigen, Ocurrencias, PrimeraVez, UltimaVez)
                VALUES (@NodoId, @Tipo, @Codigo, @Codigo, 1, GETUTCDATE(), GETUTCDATE());",
            new { NodoId = nodoConexionId, Tipo = tipo, Codigo = codigo }, tx);
    }

    private static string Enmascarar(Contexto ctx, string texto) =>
        string.IsNullOrEmpty(ctx.ApiKey) ? texto : texto.Replace(ctx.ApiKey, "***");

    private static string RutaSinQuery(string ruta) => ruta.Split('?')[0];

    private static string Recortar(string valor, int largo) => valor.Length <= largo ? valor : valor[..largo];
    private static string? RecortarN(string? valor, int largo) => valor == null ? null : (valor.Length <= largo ? valor : valor[..largo]);

    private static EjecucionResultDto Fallo(string mensaje) => new() { Success = false, Message = mensaje };

    private static void Acumular(EjecucionResultDto total, EjecucionResultDto dia)
    {
        total.TicketsProcesados += dia.TicketsProcesados;
        total.LineasProcesadas += dia.LineasProcesadas;
        total.ErrorsCount += dia.ErrorsCount;
        if (!dia.Success) total.Success = false;
    }

    #endregion
}

public class NeoApiException : Exception
{
    public bool Cortar { get; }
    public NeoApiException(string mensaje, bool cortar) : base(mensaje) { Cortar = cortar; }
}

/// <summary>Reglas de jornadas a pedir (sin base de datos, para poder testearlas).</summary>
public static class NeoSync
{
    public static TimeZoneInfo Zona(string? id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(id) ? "Europe/Madrid" : id); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.CreateCustomTimeZone("CET", TimeSpan.FromHours(1), "CET", "CET"); }
    }

    /// <summary>Última jornada cerrada: ayer en hora de España.</summary>
    public static DateTime Ayer(DateTime utcNow, TimeZoneInfo zona) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), zona).Date.AddDays(-1);

    /// <summary>
    /// Jornadas a pedir: las últimas diasReleer (siempre, para tomar cierres tardíos), más las que no tienen crudo desde
    /// el inicio (carga inicial, de la más vieja a la más nueva), como mucho maxDias por corrida.
    /// </summary>
    public static List<DateTime> DiasAPedir(DateTime inicio, DateTime ayer, IReadOnlySet<DateTime> conCrudo, int diasReleer, int maxDias)
    {
        var recientes = new List<DateTime>();
        for (var d = ayer.Date; d >= inicio.Date && recientes.Count < Math.Max(0, diasReleer); d = d.AddDays(-1)) recientes.Add(d);
        var faltantes = new List<DateTime>();
        for (var d = inicio.Date; d <= ayer.Date; d = d.AddDays(1))
            if (!conCrudo.Contains(d) && !recientes.Contains(d)) faltantes.Add(d);
        return recientes.Concat(faltantes).Take(maxDias > 0 ? maxDias : int.MaxValue).ToList();
    }

    public static TimeSpan Backoff(int intento) => TimeSpan.FromSeconds(Math.Min(60, 2 * Math.Pow(2, intento - 1)));
}

#region Configuración

public class NeoConfiguracion
{
    [JsonPropertyName("connection")] public NeoConnectionConfig? Connection { get; set; }
    [JsonPropertyName("sync")] public NeoSyncConfig? Sync { get; set; }
}

public class NeoConnectionConfig
{
    [JsonPropertyName("base_url")] public string BaseUrl { get; set; } = "http://restaurantelacabrera.webreports.cloud:3001";
    [JsonPropertyName("shop")] public string Shop { get; set; } = "0001";
    /// <summary>Nombre de la variable de entorno con la API key (la key no se guarda en la base).</summary>
    [JsonPropertyName("api_key_env")] public string ApiKeyEnv { get; set; } = "NEO_MALAGA_API_KEY";
    [JsonPropertyName("timeout_seconds")] public int TimeoutSeconds { get; set; } = 120;
    [JsonPropertyName("pausa_ms")] public int PausaMs { get; set; } = 1000;
    [JsonPropertyName("reintentos")] public int Reintentos { get; set; } = 3;
}

public class NeoSyncConfig
{
    [JsonPropertyName("fecha_inicio")] public DateTime? FechaInicio { get; set; }
    [JsonPropertyName("dias_releer")] public int DiasReleer { get; set; } = 7;
    [JsonPropertyName("max_dias_por_corrida")] public int MaxDiasPorCorrida { get; set; } = 62;
    [JsonPropertyName("zona_horaria")] public string? ZonaHoraria { get; set; } = "Europe/Madrid";
    [JsonPropertyName("auto")] public bool Auto { get; set; }
}

#endregion
