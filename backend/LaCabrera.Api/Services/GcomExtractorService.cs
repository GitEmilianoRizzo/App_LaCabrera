using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using LaCabrera.Api.Configuration;
using LaCabrera.Api.Repositories;

namespace LaCabrera.Api.Services;

/// <summary>
/// Extractor para GCOM (franquicias de Brasil, p. ej. Porto Alegre, tienda 8346).
/// API: POST {base_url}/Vendas/Vendas2, header Authorization con el token (sin "Bearer").
///
/// Cuidados de la API:
/// - 1 día por request y CUPO de requests por día por tienda (5 para la 8346). El cupo es de la tienda,
///   así que lo comparten todos los ambientes que usen el mismo token (desarrollo y producción).
/// - Por eso cada día se pide UNA sola vez: la respuesta se guarda cruda (sin datos del consumidor)
///   y cualquier reproceso se hace desde el crudo guardado, sin volver a llamar a la API.
/// - Cada request queda registrado en log.EjecucionNodo con ApiVersionOrigen = GCOM_VENDAS2 antes de
///   enviarse; ese registro es el que se cuenta para el cupo del día (en la zona horaria de la tienda).
/// - Si la API responde error, se corta la corrida para no gastar cupo en reintentos.
/// </summary>
public interface IGcomExtractorService
{
    /// <summary>Un día: si ya hay crudo guardado se reprocesa desde ahí; si no, consume 1 request.</summary>
    Task<EjecucionResultDto> EjecutarExtraccionAsync(int nodoConexionId, DateTime? fechaNegocio = null, string modo = "MANUAL");
    /// <summary>Rango: pide a la API solo los días sin crudo, del más viejo al más nuevo, hasta agotar el cupo.</summary>
    Task<EjecucionResultDto> EjecutarExtraccionRangoAsync(int nodoConexionId, DateTime fechaDesde, DateTime fechaHasta, string modo = "CATCHUP");
    /// <summary>Días pendientes desde sync.fecha_inicio hasta ayer: primero ayer, después los más viejos.</summary>
    Task<EjecucionResultDto> EjecutarExtraccionDesdeUltimoAsync(int nodoConexionId, string modo = "MANUAL");
    /// <summary>Vuelve a cargar desde el crudo guardado. No llama a la API.</summary>
    Task<EjecucionResultDto> ReprocesarDesdeCrudoAsync(int nodoConexionId, DateTime fechaDesde, DateTime fechaHasta);
    /// <summary>Carga un día a partir de una respuesta de Vendas2 bajada en otro ambiente. No llama a la API.</summary>
    Task<EjecucionResultDto> ImportarCrudoAsync(int nodoConexionId, DateTime fechaNegocio, string json);
    Task<GcomCupoDto> GetCupoAsync(int nodoConexionId);
}

public class GcomExtractorService : IGcomExtractorService
{
    public const string MarcaRequestApi = "GCOM_VENDAS2";
    public const string MarcaCrudo = "GCOM_CRUDO";
    private const string SchemaVersion = "GCOM_V1";

    // Serializa las corridas en el proceso: el job y una ejecución manual no pueden pisarse el cupo
    private static readonly SemaphoreSlim _candado = new(1, 1);

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IConexionRepository _conexionRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GcomExtractorService> _logger;
    private readonly Func<DateTime> _utcNow;

    public GcomExtractorService(
        IDbConnectionFactory connectionFactory,
        IConexionRepository conexionRepository,
        IHttpClientFactory httpClientFactory,
        ILogger<GcomExtractorService> logger)
    {
        _connectionFactory = connectionFactory;
        _conexionRepository = conexionRepository;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _utcNow = () => DateTime.UtcNow;
    }

    public async Task<EjecucionResultDto> EjecutarExtraccionAsync(int nodoConexionId, DateTime? fechaNegocio = null, string modo = "MANUAL")
    {
        await _candado.WaitAsync();
        try
        {
            var ctx = await CargarContextoAsync(nodoConexionId);
            if (ctx.Error != null) return Fallo(ctx.Error);
            var dia = (fechaNegocio ?? GcomCupo.HoyLocal(_utcNow(), ctx.Zona).AddDays(-1)).Date;

            var crudo = await GetCrudoAsync(ctx.Nodo!.FranquiciaId, dia);
            if (crudo != null)
            {
                _logger.LogInformation("GCOM {Fecha}: ya hay crudo guardado, se reprocesa sin llamar a la API", dia.ToString("yyyy-MM-dd"));
                return await ProcesarJsonAsync(ctx, dia, crudo, MarcaCrudo, "CORRECTION", "RAW", modo);
            }

            var (r, _) = await PedirDiaAsync(ctx, dia, modo);
            return r;
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
            var conCrudo = await GetDiasConCrudoAsync(ctx.Nodo!.FranquiciaId, fechaDesde.Date, fechaHasta.Date);
            var pendientes = GcomCupo.DiasPendientes(fechaDesde.Date, fechaHasta.Date, conCrudo, priorizarUltimo: false);
            return await PedirDiasAsync(ctx, pendientes, modo, conCrudo.Count);
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
            if (ctx.Config!.Sync?.FechaInicio is not { } inicio)
                return Fallo("Falta sync.fecha_inicio en la configuración del nodo");

            var ayer = GcomCupo.HoyLocal(_utcNow(), ctx.Zona).AddDays(-1);
            var conCrudo = await GetDiasConCrudoAsync(ctx.Nodo!.FranquiciaId, inicio.Date, ayer);
            var pendientes = GcomCupo.DiasPendientes(inicio.Date, ayer, conCrudo, priorizarUltimo: true);
            if (pendientes.Count == 0)
                return new EjecucionResultDto { Success = true, Message = "Datos al día: no hay días pendientes de pedir a GCOM" };
            return await PedirDiasAsync(ctx, pendientes, modo, conCrudo.Count);
        }
        finally { _candado.Release(); }
    }

    public async Task<EjecucionResultDto> ReprocesarDesdeCrudoAsync(int nodoConexionId, DateTime fechaDesde, DateTime fechaHasta)
    {
        await _candado.WaitAsync();
        try
        {
            var ctx = await CargarContextoAsync(nodoConexionId, exigirToken: false);
            if (ctx.Error != null) return Fallo(ctx.Error);

            var total = new EjecucionResultDto { Success = true };
            int dias = 0, sinCrudo = 0;
            for (var dia = fechaDesde.Date; dia <= fechaHasta.Date; dia = dia.AddDays(1))
            {
                var crudo = await GetCrudoAsync(ctx.Nodo!.FranquiciaId, dia);
                if (crudo == null) { sinCrudo++; continue; }
                Acumular(total, await ProcesarJsonAsync(ctx, dia, crudo, MarcaCrudo, "CORRECTION", "RAW", "MANUAL"));
                dias++;
            }
            total.Message = $"Reproceso desde crudo: {dias} días, {total.TicketsProcesados} tickets, {total.LineasProcesadas} líneas"
                + (sinCrudo > 0 ? $"; {sinCrudo} días sin crudo guardado (no se pidieron a la API)" : "");
            return total;
        }
        finally { _candado.Release(); }
    }

    public async Task<EjecucionResultDto> ImportarCrudoAsync(int nodoConexionId, DateTime fechaNegocio, string json)
    {
        await _candado.WaitAsync();
        try
        {
            var ctx = await CargarContextoAsync(nodoConexionId, exigirToken: false);
            if (ctx.Error != null) return Fallo(ctx.Error);

            GcomVendasResponse? respuesta;
            try { respuesta = GcomImportes.Deserializar(json); }
            catch (JsonException ex) { return Fallo($"El archivo no es una respuesta válida de Vendas2: {ex.Message}"); }
            if (respuesta?.Vendas == null) return Fallo("El archivo no trae la lista 'vendas'");

            var otraFecha = respuesta.Vendas.Select(v => v.DataVenda).FirstOrDefault(d => d != fechaNegocio.ToString("yyyy-MM-dd"));
            if (otraFecha != null) return Fallo($"El archivo trae ventas del {otraFecha}, no del {fechaNegocio:yyyy-MM-dd}");

            return await ProcesarJsonAsync(ctx, fechaNegocio.Date, GcomImportes.SanitizarCrudo(json), MarcaCrudo, "FULL_DAY", "RAW_IMPORT", "MANUAL");
        }
        finally { _candado.Release(); }
    }

    public async Task<GcomCupoDto> GetCupoAsync(int nodoConexionId)
    {
        var ctx = await CargarContextoAsync(nodoConexionId, exigirToken: false);
        if (ctx.Error != null) throw new InvalidOperationException(ctx.Error);

        var hoy = GcomCupo.HoyLocal(_utcNow(), ctx.Zona);
        var usados = await ContarRequestsHoyAsync(nodoConexionId, ctx.Zona);
        var dto = new GcomCupoDto
        {
            FechaLocal = hoy,
            ZonaHoraria = ctx.Zona.Id,
            MaxRequestsDia = ctx.Config!.Sync!.MaxRequestsDia,
            RequestsUsadosHoy = usados,
            RequestsDisponibles = Math.Max(0, ctx.Config.Sync.MaxRequestsDia - usados)
        };
        if (ctx.Config.Sync.FechaInicio is { } inicio)
        {
            var ayer = hoy.AddDays(-1);
            var conCrudo = await GetDiasConCrudoAsync(ctx.Nodo!.FranquiciaId, inicio.Date, ayer);
            dto.DiasPendientes = GcomCupo.DiasPendientes(inicio.Date, ayer, conCrudo, priorizarUltimo: true).Count;
            dto.DiasConDatos = conCrudo.Count;
        }
        return dto;
    }

    #region Requests a la API

    private async Task<EjecucionResultDto> PedirDiasAsync(Contexto ctx, IReadOnlyList<DateTime> dias, string modo, int diasYaGuardados)
    {
        var total = new EjecucionResultDto { Success = true };
        int pedidos = 0;
        string? corte = null;

        foreach (var dia in dias)
        {
            var (resultado, seguir) = await PedirDiaAsync(ctx, dia, modo);
            if (resultado.Message.StartsWith("Cupo"))
            {
                corte = resultado.Message;
                break;
            }
            Acumular(total, resultado);
            pedidos++;
            if (!seguir)
            {
                total.Success = false;
                corte = $"Se cortó en {dia:yyyy-MM-dd}: {resultado.Message}";
                break;
            }
        }

        var faltan = dias.Count - pedidos;
        total.Message = $"{pedidos} días pedidos a GCOM, {total.TicketsProcesados} tickets, {total.LineasProcesadas} líneas; "
            + $"{diasYaGuardados} días ya estaban guardados; quedan {faltan} días pendientes"
            + (corte != null ? $". {corte}" : "");
        return total;
    }

    /// <summary>Pide un día a la API. Devuelve si conviene seguir con otros días.</summary>
    private async Task<(EjecucionResultDto Resultado, bool Seguir)> PedirDiaAsync(Contexto ctx, DateTime dia, string modo)
    {
        var nodoId = ctx.Nodo!.NodoConexionId;
        var usados = await ContarRequestsHoyAsync(nodoId, ctx.Zona);
        if (usados >= ctx.Config!.Sync!.MaxRequestsDia)
        {
            return (new EjecucionResultDto
            {
                Success = true,
                Message = $"Cupo del día agotado ({usados}/{ctx.Config.Sync.MaxRequestsDia} requests a GCOM hoy, {ctx.Zona.Id})"
            }, false);
        }

        // El registro se crea ANTES de enviar: cuenta para el cupo aunque la request falle
        var ejecucionId = await RegistrarInicioEjecucionAsync(nodoId, dia, modo, MarcaRequestApi);
        var conn = ctx.Config.Connection!;

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(conn.TimeoutSeconds > 0 ? conn.TimeoutSeconds : 120);

            var cuerpo = JsonSerializer.Serialize(new
            {
                DataInicial = $"{dia:yyyy-MM-dd} 00:00:00",
                DataFinal = $"{dia:yyyy-MM-dd} 23:59:59",
                ListaLojas = conn.ListaLojas,
                IdIntegracao = conn.IdIntegracao
            });
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{conn.BaseUrl.TrimEnd('/')}/Vendas/Vendas2")
            {
                Content = new StringContent(cuerpo, Encoding.UTF8, "application/json")
            };
            // El token lleva caracteres especiales (#): se agrega tal cual, sin validar ni prefijo Bearer
            request.Headers.TryAddWithoutValidation("Authorization", conn.Token);
            request.Headers.TryAddWithoutValidation("Accept", "application/json");

            _logger.LogInformation("GCOM: request {Usado}/{Max} del día, fecha {Fecha}, tienda {Loja}",
                usados + 1, ctx.Config.Sync.MaxRequestsDia, dia.ToString("yyyy-MM-dd"), conn.ListaLojas);

            using var response = await client.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var detalle = json.Length > 500 ? json[..500] : json;
                await RegistrarFinEjecucionAsync(ejecucionId, "ERROR", 0, 0, 1, $"HTTP {(int)response.StatusCode}: {detalle}");
                return (new EjecucionResultDto { Success = false, Message = $"GCOM respondió HTTP {(int)response.StatusCode}", EjecucionId = ejecucionId }, false);
            }

            var resultado = await ProcesarJsonAsync(ctx, dia, GcomImportes.SanitizarCrudo(json), MarcaRequestApi, "FULL_DAY", "API", modo, ejecucionId);
            return (resultado, resultado.Success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GCOM: error pidiendo {Fecha}", dia.ToString("yyyy-MM-dd"));
            await RegistrarFinEjecucionAsync(ejecucionId, "ERROR", 0, 0, 1, ex.Message);
            return (new EjecucionResultDto { Success = false, Message = ex.Message, EjecucionId = ejecucionId }, false);
        }
    }

    #endregion

    #region Carga

    private async Task<EjecucionResultDto> ProcesarJsonAsync(
        Contexto ctx, DateTime dia, string json, string marca, string tipoCarga, string origen, string modo, long? ejecucionId = null)
    {
        var nodo = ctx.Nodo!;
        ejecucionId ??= await RegistrarInicioEjecucionAsync(nodo.NodoConexionId, dia, modo, marca);

        GcomVendasResponse? respuesta;
        try { respuesta = GcomImportes.Deserializar(json); }
        catch (JsonException ex)
        {
            await RegistrarFinEjecucionAsync(ejecucionId.Value, "ERROR", 0, 0, 1, $"JSON inválido: {ex.Message}");
            return new EjecucionResultDto { Success = false, Message = "Respuesta de GCOM con formato inválido", EjecucionId = ejecucionId };
        }

        var ventas = respuesta?.Vendas ?? new List<GcomVenda>();
        // El crudo se guarda aunque el día no tenga ventas: así no se vuelve a pedir
        var batchId = await CrearBatchIngestaAsync(nodo.FranquiciaId, dia, json, tipoCarga, origen);

        int tickets = 0, lineas = 0, errores = 0;
        var avisos = new List<string>();
        if (respuesta != null && respuesta.TotalCupons != ventas.Count)
            avisos.Add($"totalCupons={respuesta.TotalCupons} pero vinieron {ventas.Count} ventas");

        var mapeoPagos = await GetMapeoMediosPagoAsync(nodo.NodoConexionId);
        foreach (var venta in ventas)
        {
            try
            {
                var ticket = GcomImportes.CalcularTicket(venta);
                if (ticket.FechaNegocio != dia)
                    avisos.Add($"venta {ticket.ExternalTicketId} con fecha {ticket.FechaNegocio:yyyy-MM-dd}");
                lineas += await GuardarTicketAsync(ticket, nodo.FranquiciaId, batchId, nodo.NodoConexionId, nodo.Moneda, mapeoPagos);
                tickets++;
            }
            catch (Exception ex)
            {
                errores++;
                _logger.LogError(ex, "GCOM: error cargando venta {Controle}", venta.Controle);
                await RegistrarErrorIngestaAsync(batchId, $"Venta {venta.CodLoja}-{venta.Controle}: {ex.Message}");
            }
        }

        await ActualizarBatchAsync(batchId, tickets, lineas, errores);
        var estado = errores > 0 ? (tickets > 0 ? "WARNING" : "ERROR") : (avisos.Count > 0 ? "WARNING" : "SUCCESS");
        var mensaje = avisos.Count > 0 ? string.Join("; ", avisos) : null;
        await RegistrarFinEjecucionAsync(ejecucionId.Value, estado, tickets, lineas, errores, mensaje);
        await ActualizarEstadoSincronizacionAsync(nodo.NodoConexionId, nodo.FranquiciaId, estado, batchId);

        return new EjecucionResultDto
        {
            Success = estado != "ERROR",
            Message = $"{dia:yyyy-MM-dd}: {tickets} tickets, {lineas} líneas" + (errores > 0 ? $", {errores} errores" : "") + (mensaje != null ? $" ({mensaje})" : ""),
            TicketsProcesados = tickets,
            LineasProcesadas = lineas,
            ErrorsCount = errores,
            EjecucionId = ejecucionId,
            BatchId = batchId
        };
    }

    /// <summary>Alta o actualización del ticket (idempotente por tienda + controle). Devuelve las líneas cargadas.</summary>
    private async Task<int> GuardarTicketAsync(
        GcomImportes.TicketCalculado t, int franquiciaId, long batchId, int nodoConexionId, string moneda, Dictionary<string, string> mapeoPagos)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();

        var existente = await connection.QueryFirstOrDefaultAsync<long?>(@"
            SELECT VentaTicketId FROM fact.VentaTicket
            WHERE FranquiciaId = @FranquiciaId AND ExternalTicketId = @ExternalTicketId",
            new { FranquiciaId = franquiciaId, t.ExternalTicketId }, tx);

        var datos = new
        {
            BatchId = batchId, FranquiciaId = franquiciaId, t.ExternalTicketId, t.NumeroTicket, t.Estado, t.TipoDocumentoFiscal,
            t.FechaNegocio, t.FechaApertura, t.FechaCierre, t.PeriodoComida, CodigoMoneda = moneda,
            t.ImporteBruto, t.ImporteDescuento, t.ImporteNeto, t.ImporteImpuesto, t.ImportePropina, t.ImporteTotalPagado,
            TieneDescuento = t.ImporteDescuento > 0, t.EstaAnulado
        };

        long ticketId;
        if (existente.HasValue)
        {
            ticketId = existente.Value;
            await connection.ExecuteAsync(@"
                UPDATE fact.VentaTicket SET
                    IngestionBatchId = @BatchId, NumeroTicket = @NumeroTicket, Estado = @Estado, TipoDocumentoFiscal = @TipoDocumentoFiscal,
                    FechaNegocio = @FechaNegocio, FechaApertura = @FechaApertura, FechaCierre = @FechaCierre, PeriodoComida = @PeriodoComida,
                    CodigoMoneda = @CodigoMoneda,
                    ImporteBruto = @ImporteBruto, ImporteDescuento = @ImporteDescuento, ImporteNeto = @ImporteNeto,
                    ImporteImpuesto = @ImporteImpuesto, ImporteNetoSinImpuesto = @ImporteNeto,
                    ImportesIncluyenImpuesto = 0, CalidadImpuesto = 'POS',
                    ImportePropina = @ImportePropina, ImporteTotalPagado = @ImporteTotalPagado,
                    TieneDescuento = @TieneDescuento, EstaAnulado = @EstaAnulado,
                    FechaModificacion = SYSDATETIME(), UsuarioModificacion = 'GCOM'
                WHERE VentaTicketId = @TicketId;
                DELETE FROM fact.VentaTicketDetalle WHERE VentaTicketId = @TicketId;
                DELETE FROM fact.VentaTicketMedioPago WHERE VentaTicketId = @TicketId;",
                new
                {
                    datos.BatchId, datos.NumeroTicket, datos.Estado, datos.TipoDocumentoFiscal, datos.FechaNegocio, datos.FechaApertura,
                    datos.FechaCierre, datos.PeriodoComida, datos.CodigoMoneda, datos.ImporteBruto, datos.ImporteDescuento, datos.ImporteNeto,
                    datos.ImporteImpuesto, datos.ImportePropina, datos.ImporteTotalPagado, datos.TieneDescuento, datos.EstaAnulado,
                    TicketId = ticketId
                }, tx);
        }
        else
        {
            ticketId = await connection.QuerySingleAsync<long>(@"
                INSERT INTO fact.VentaTicket (
                    IngestionBatchId, FranquiciaId, ExternalTicketId, NumeroTicket, Estado, TipoDocumentoFiscal,
                    FechaNegocio, FechaApertura, FechaCierre, PeriodoComida,
                    NumeroMesa, AreaMesa, MozoId, CantidadCubiertos, CodigoMoneda,
                    ImporteBruto, ImporteDescuento, ImporteNeto, ImporteImpuesto,
                    ImporteNetoSinImpuesto, ImportesIncluyenImpuesto, CalidadImpuesto,
                    ImporteServicio, ImportePropina, ImporteTotalPagado,
                    TieneDescuento, EstaAnulado, TieneDatosCubiertos, TieneDatosMozo, TieneDatosMesa,
                    FuenteSistema
                )
                OUTPUT INSERTED.VentaTicketId
                VALUES (
                    @BatchId, @FranquiciaId, @ExternalTicketId, @NumeroTicket, @Estado, @TipoDocumentoFiscal,
                    @FechaNegocio, @FechaApertura, @FechaCierre, @PeriodoComida,
                    NULL, NULL, NULL, 0, @CodigoMoneda,
                    @ImporteBruto, @ImporteDescuento, @ImporteNeto, @ImporteImpuesto,
                    @ImporteNeto, 0, 'POS',
                    0, @ImportePropina, @ImporteTotalPagado,
                    @TieneDescuento, @EstaAnulado, 0, 0, 0,
                    'GCOM'
                )", datos, tx);
        }

        foreach (var l in t.Lineas)
        {
            await connection.ExecuteAsync(@"
                INSERT INTO fact.VentaTicketDetalle (
                    VentaTicketId, IngestionBatchId, FranquiciaId,
                    ExternalLineId, CodigoProducto, NombreProducto,
                    Cantidad, PrecioUnitario,
                    ImporteBruto, ImporteDescuento, ImporteNeto, ImporteImpuesto,
                    ImporteNetoSinImpuesto, ImportesIncluyenImpuesto, CalidadImpuesto,
                    TieneDescuento, EstaAnulado, MotivoAnulacion, FechaPedido
                )
                VALUES (
                    @TicketId, @BatchId, @FranquiciaId,
                    @ExternalLineId, @CodigoProducto, @NombreProducto,
                    @Cantidad, @PrecioUnitario,
                    @ImporteBruto, @ImporteDescuento, @ImporteNeto, @ImporteImpuesto,
                    @ImporteNeto, 0, 'DERIVADO',
                    @TieneDescuento, @EstaAnulado, @MotivoAnulacion, @FechaPedido
                )",
                new
                {
                    TicketId = ticketId, BatchId = batchId, FranquiciaId = franquiciaId,
                    l.ExternalLineId, CodigoProducto = Recortar(l.CodigoProducto, 100), NombreProducto = Recortar(l.NombreProducto, 200),
                    l.Cantidad, l.PrecioUnitario, l.ImporteBruto, l.ImporteDescuento, l.ImporteNeto, l.ImporteImpuesto,
                    TieneDescuento = l.ImporteDescuento > 0, l.EstaAnulado,
                    MotivoAnulacion = l.EstaAnulado ? (t.EstaAnulado ? "VENTA_CANCELADA_POS" : "ITEM_CANCELADO_POS") : null,
                    l.FechaPedido
                }, tx);
        }

        foreach (var p in t.Pagos)
        {
            var codigo = mapeoPagos.GetValueOrDefault(p.FormaPago, "OTHER");
            if (codigo == "OTHER" && !mapeoPagos.ContainsKey(p.FormaPago))
                await RegistrarValorNoMapeadoAsync(connection, tx, nodoConexionId, "MEDIO_PAGO", p.FormaPago);

            await connection.ExecuteAsync(@"
                INSERT INTO fact.VentaTicketMedioPago (VentaTicketId, IngestionBatchId, FranquiciaId, CodigoMedioPago, MarcaTarjeta, Importe)
                VALUES (@TicketId, @BatchId, @FranquiciaId, @CodigoMedioPago, @MarcaTarjeta, @Importe)",
                new { TicketId = ticketId, BatchId = batchId, FranquiciaId = franquiciaId, CodigoMedioPago = codigo, MarcaTarjeta = p.Marca, p.Importe }, tx);
        }

        tx.Commit();
        return t.Lineas.Count;
    }

    private static string Recortar(string valor, int largo) => valor.Length <= largo ? valor : valor[..largo];

    #endregion

    #region Configuración y consultas

    private record Contexto(Models.DTOs.NodoConexionDto? Nodo, GcomConfiguracion? Config, TimeZoneInfo Zona, string? Error);

    private async Task<Contexto> CargarContextoAsync(int nodoConexionId, bool exigirToken = true)
    {
        var nodo = await _conexionRepository.GetNodoByIdAsync(nodoConexionId);
        if (nodo == null) return new Contexto(null, null, TimeZoneInfo.Utc, "Nodo no encontrado");

        var json = await _conexionRepository.GetConfiguracionJsonAsync(nodoConexionId);
        if (string.IsNullOrEmpty(json)) return new Contexto(nodo, null, TimeZoneInfo.Utc, "Nodo sin configuración");

        var config = JsonSerializer.Deserialize<GcomConfiguracion>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (config?.Connection == null) return new Contexto(nodo, config, TimeZoneInfo.Utc, "Configuración de conexión inválida");
        config.Sync ??= new GcomSyncConfig();

        if (exigirToken && (string.IsNullOrWhiteSpace(config.Connection.Token) || string.IsNullOrWhiteSpace(config.Connection.BaseUrl)
                            || string.IsNullOrWhiteSpace(config.Connection.ListaLojas)))
            return new Contexto(nodo, config, TimeZoneInfo.Utc, "Faltan base_url, token o lista_lojas para GCOM");

        return new Contexto(nodo, config, GcomCupo.Zona(config.Sync.ZonaHoraria ?? nodo.Timezone), null);
    }

    private async Task<int> ContarRequestsHoyAsync(int nodoConexionId, TimeZoneInfo zona)
    {
        var (desdeUtc, hastaUtc) = GcomCupo.LimitesDiaUtc(_utcNow(), zona);
        using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) FROM log.EjecucionNodo
            WHERE NodoConexionId = @NodoId AND ApiVersionOrigen = @Marca
              AND InicioEjecucion >= @Desde AND InicioEjecucion < @Hasta",
            new { NodoId = nodoConexionId, Marca = MarcaRequestApi, Desde = desdeUtc, Hasta = hastaUtc });
    }

    private async Task<HashSet<DateTime>> GetDiasConCrudoAsync(int franquiciaId, DateTime desde, DateTime hasta)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dias = await connection.QueryAsync<DateTime>(@"
            SELECT DISTINCT CAST(b.FechaNegocio AS DATE)
            FROM stg.IngestionBatch b
            JOIN stg.IngestionBatchRawJson r ON r.IngestionBatchId = b.IngestionBatchId
            WHERE b.FranquiciaId = @FranquiciaId AND b.SchemaVersion = @Schema
              AND b.FechaNegocio >= @Desde AND b.FechaNegocio <= @Hasta",
            new { FranquiciaId = franquiciaId, Schema = SchemaVersion, Desde = desde, Hasta = hasta });
        return dias.Select(d => d.Date).ToHashSet();
    }

    /// <summary>Último crudo guardado del día (de la API o importado).</summary>
    private async Task<string?> GetCrudoAsync(int franquiciaId, DateTime dia)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<string?>(@"
            SELECT TOP 1 r.JsonContent
            FROM stg.IngestionBatch b
            JOIN stg.IngestionBatchRawJson r ON r.IngestionBatchId = b.IngestionBatchId
            WHERE b.FranquiciaId = @FranquiciaId AND b.SchemaVersion = @Schema AND CAST(b.FechaNegocio AS DATE) = @Dia
              AND b.OrigenIngesta IN ('API', 'RAW_IMPORT')
            ORDER BY b.IngestionBatchId DESC",
            new { FranquiciaId = franquiciaId, Schema = SchemaVersion, Dia = dia.Date });
    }

    private async Task<Dictionary<string, string>> GetMapeoMediosPagoAsync(int nodoConexionId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var filas = await connection.QueryAsync<(string CodigoOrigen, string MedioPagoDestino)>(@"
            SELECT CodigoOrigen, MedioPagoDestino FROM dim.MapeoMedioPago WHERE NodoConexionId = @NodoId",
            new { NodoId = nodoConexionId });
        return filas.ToDictionary(f => f.CodigoOrigen, f => f.MedioPagoDestino, StringComparer.OrdinalIgnoreCase);
    }

    #endregion

    #region Helpers de base de datos

    private async Task<long> RegistrarInicioEjecucionAsync(int nodoConexionId, DateTime fechaNegocio, string modo, string marca)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleAsync<long>(@"
            INSERT INTO log.EjecucionNodo (NodoConexionId, FechaNegocio, InicioEjecucion, Estado, ModoEjecucion, ApiVersionOrigen)
            OUTPUT INSERTED.EjecucionNodoId
            VALUES (@NodoConexionId, @FechaNegocio, GETUTCDATE(), 'RUNNING', @Modo, @Marca)",
            new { NodoConexionId = nodoConexionId, FechaNegocio = fechaNegocio, Modo = modo, Marca = marca });
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

    private async Task<long> CrearBatchIngestaAsync(int franquiciaId, DateTime fechaNegocio, string rawJson, string tipoCarga, string origen)
    {
        using var connection = _connectionFactory.CreateConnection();
        var batchId = await connection.QuerySingleAsync<long>(@"
            INSERT INTO stg.IngestionBatch (BatchId, FranquiciaId, FechaNegocio, SchemaVersion, TipoCarga, OrigenIngesta, Estado)
            OUTPUT INSERTED.IngestionBatchId
            VALUES (@BatchGuid, @FranquiciaId, @FechaNegocio, @Schema, @TipoCarga, @Origen, 'PROCESSING')",
            new { BatchGuid = Guid.NewGuid().ToString(), FranquiciaId = franquiciaId, FechaNegocio = fechaNegocio, Schema = SchemaVersion, TipoCarga = tipoCarga, Origen = origen });

        // Un reproceso desde crudo no vuelve a guardar el JSON: el original ya está
        if (origen != "RAW")
        {
            await connection.ExecuteAsync(
                "INSERT INTO stg.IngestionBatchRawJson (IngestionBatchId, JsonContent) VALUES (@BatchId, @RawJson)",
                new { BatchId = batchId, RawJson = rawJson });
        }
        return batchId;
    }

    private async Task ActualizarBatchAsync(long batchId, int tickets, int lineas, int errores)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(@"
            UPDATE stg.IngestionBatch
            SET Estado = @Estado, TicketCountCalculado = @Tickets, ItemLineCountCalculado = @Lineas
            WHERE IngestionBatchId = @BatchId",
            new
            {
                BatchId = batchId, Tickets = tickets, Lineas = lineas,
                Estado = errores > 0 ? (tickets > 0 ? "COMPLETED_WITH_ERRORS" : "ERROR") : "COMPLETED"
            });
    }

    private async Task RegistrarErrorIngestaAsync(long batchId, string mensaje)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(@"
            INSERT INTO stg.IngestionError (IngestionBatchId, Severidad, CodigoError, MensajeError)
            VALUES (@BatchId, 'ERROR', 'PROCESSING_ERROR', @Mensaje)",
            new { BatchId = batchId, Mensaje = mensaje });
    }

    private static async Task RegistrarValorNoMapeadoAsync(System.Data.IDbConnection connection, System.Data.IDbTransaction tx, int nodoConexionId, string tipo, string codigo)
    {
        await connection.ExecuteAsync(@"
            MERGE dim.ValorNoMapeado AS target
            USING (SELECT @NodoId AS NodoConexionId, @Tipo AS TipoMapeo, @Codigo AS CodigoOrigen) AS source
            ON target.NodoConexionId = source.NodoConexionId AND target.TipoMapeo = source.TipoMapeo AND target.CodigoOrigen = source.CodigoOrigen
            WHEN MATCHED THEN
                UPDATE SET Ocurrencias = target.Ocurrencias + 1, UltimaVez = GETUTCDATE()
            WHEN NOT MATCHED THEN
                INSERT (NodoConexionId, TipoMapeo, CodigoOrigen, NombreOrigen, Ocurrencias, PrimeraVez, UltimaVez)
                VALUES (@NodoId, @Tipo, @Codigo, @Codigo, 1, GETUTCDATE(), GETUTCDATE());",
            new { NodoId = nodoConexionId, Tipo = tipo, Codigo = codigo }, tx);
    }

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

/// <summary>Reglas de cupo y días pendientes (sin base de datos, para poder testearlas).</summary>
public static class GcomCupo
{
    public static TimeZoneInfo Zona(string? id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(id) ? "America/Sao_Paulo" : id); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.CreateCustomTimeZone("BRT", TimeSpan.FromHours(-3), "BRT", "BRT"); }
    }

    public static DateTime HoyLocal(DateTime utcNow, TimeZoneInfo zona) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), zona).Date;

    /// <summary>Inicio y fin (UTC) del día local actual de la tienda.</summary>
    public static (DateTime DesdeUtc, DateTime HastaUtc) LimitesDiaUtc(DateTime utcNow, TimeZoneInfo zona)
    {
        var hoy = HoyLocal(utcNow, zona);
        return (TimeZoneInfo.ConvertTimeToUtc(hoy, zona), TimeZoneInfo.ConvertTimeToUtc(hoy.AddDays(1), zona));
    }

    /// <summary>
    /// Días sin crudo guardado entre desde y hasta. Con priorizarUltimo, el último día (ayer) va primero
    /// para que el día a día no espere a que termine la carga histórica.
    /// </summary>
    public static List<DateTime> DiasPendientes(DateTime desde, DateTime hasta, IReadOnlySet<DateTime> conCrudo, bool priorizarUltimo)
    {
        var dias = new List<DateTime>();
        for (var d = desde.Date; d <= hasta.Date; d = d.AddDays(1))
            if (!conCrudo.Contains(d)) dias.Add(d);

        if (priorizarUltimo && dias.Count > 1 && dias[^1] == hasta.Date)
        {
            dias.RemoveAt(dias.Count - 1);
            dias.Insert(0, hasta.Date);
        }
        return dias;
    }
}

#region Configuración y DTOs

public class GcomConfiguracion
{
    [JsonPropertyName("connection")] public GcomConnectionConfig? Connection { get; set; }
    [JsonPropertyName("sync")] public GcomSyncConfig? Sync { get; set; }
}

public class GcomConnectionConfig
{
    [JsonPropertyName("base_url")] public string BaseUrl { get; set; } = "https://servicos.gcom.com.br/wsrest/api";
    [JsonPropertyName("token")] public string Token { get; set; } = string.Empty;
    [JsonPropertyName("lista_lojas")] public string ListaLojas { get; set; } = string.Empty;
    [JsonPropertyName("id_integracao")] public int IdIntegracao { get; set; } = 50;
    [JsonPropertyName("timeout_seconds")] public int TimeoutSeconds { get; set; } = 120;
}

public class GcomSyncConfig
{
    /// <summary>Primer día de datos a cargar (carga histórica).</summary>
    [JsonPropertyName("fecha_inicio")] public DateTime? FechaInicio { get; set; }
    /// <summary>Requests por día que puede usar ESTE ambiente. Si dos ambientes usan el mismo token, la suma no debe pasar el cupo de GCOM.</summary>
    [JsonPropertyName("max_requests_dia")] public int MaxRequestsDia { get; set; } = 5;
    [JsonPropertyName("zona_horaria")] public string? ZonaHoraria { get; set; } = "America/Sao_Paulo";
    /// <summary>Si el job automático (GcomSync:Enabled) sincroniza este nodo.</summary>
    [JsonPropertyName("auto")] public bool Auto { get; set; }
}

public class GcomCupoDto
{
    [JsonPropertyName("fecha_local")] public DateTime FechaLocal { get; set; }
    [JsonPropertyName("zona_horaria")] public string ZonaHoraria { get; set; } = string.Empty;
    [JsonPropertyName("max_requests_dia")] public int MaxRequestsDia { get; set; }
    [JsonPropertyName("requests_usados_hoy")] public int RequestsUsadosHoy { get; set; }
    [JsonPropertyName("requests_disponibles")] public int RequestsDisponibles { get; set; }
    [JsonPropertyName("dias_con_datos")] public int DiasConDatos { get; set; }
    [JsonPropertyName("dias_pendientes")] public int DiasPendientes { get; set; }
}

#endregion
