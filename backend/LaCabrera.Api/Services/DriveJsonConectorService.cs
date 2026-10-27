using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Dapper;
using LaCabrera.Api.Configuration;
using LaCabrera.Api.Models.DTOs;
using LaCabrera.Api.Repositories;

namespace LaCabrera.Api.Services;

/// <summary>
/// Carga automática de los JSON de ventas (daily_sales_batch v1) que las franquicias dejan en una carpeta
/// de Google Drive (Colombia: Bogotá Colcabrera, Bogotá Foodify y Cartagena).
///
/// - El nodo se configura con una sección "drive" en ConfiguracionJson: carpeta_id, codigo_tienda y fecha_inicio.
/// - Los archivos se llaman LC_{TIENDA}_{yyyy-MM-dd}_{BATCH_ID}.json; el BATCH_ID es el batch_id del JSON.
/// - Lo que ya está cargado se reconoce por el batch_id en stg.IngestionBatch, sin descargar el archivo.
/// - Copias manuales ("... (1).json") y archivos con otro nombre se ignoran.
/// - Se procesa con el mismo IngestionService que el endpoint daily-batch (valida y descarta duplicados).
/// </summary>
public interface IDriveJsonConectorService
{
    Task<EjecucionResultDto> SincronizarAsync(int nodoConexionId, string modo = "MANUAL", CancellationToken ct = default);
    Task<DriveEstadoDto> GetEstadoAsync(int nodoConexionId, CancellationToken ct = default);
}

public class DriveJsonConectorService : IDriveJsonConectorService
{
    public const string MarcaEjecucion = "DRIVE_JSON";

    private static readonly SemaphoreSlim _candado = new(1, 1);
    private static readonly JsonSerializerOptions _jsonOpciones = new() { PropertyNameCaseInsensitive = true };

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IConexionRepository _conexionRepository;
    private readonly IGoogleDriveClient _drive;
    private readonly IIngestionService _ingestionService;
    private readonly ILogger<DriveJsonConectorService> _logger;

    public DriveJsonConectorService(
        IDbConnectionFactory connectionFactory,
        IConexionRepository conexionRepository,
        IGoogleDriveClient drive,
        IIngestionService ingestionService,
        ILogger<DriveJsonConectorService> logger)
    {
        _connectionFactory = connectionFactory;
        _conexionRepository = conexionRepository;
        _drive = drive;
        _ingestionService = ingestionService;
        _logger = logger;
    }

    public async Task<EjecucionResultDto> SincronizarAsync(int nodoConexionId, string modo = "MANUAL", CancellationToken ct = default)
    {
        await _candado.WaitAsync(ct);
        try
        {
            var (nodo, config, error) = await CargarAsync(nodoConexionId);
            if (error != null) return new EjecucionResultDto { Success = false, Message = error };

            var archivos = await _drive.ListarArchivosAsync(config!.CarpetaId, ct);
            var cargados = await GetBatchesCargadosAsync(nodo!.FranquiciaId);
            var plan = DriveArchivosPlan.Armar(archivos, config.CodigoTienda, config.FechaInicio, cargados);

            if (plan.Pendientes.Count == 0)
                return new EjecucionResultDto { Success = true, Message = $"Sin archivos nuevos en Drive ({plan.YaCargados} ya cargados)" };

            var resumen = new ResumenRangoExtraccion();
            foreach (var archivo in plan.Pendientes)
            {
                if (ct.IsCancellationRequested) break;
                resumen.Agregar(archivo.Fecha, await CargarArchivoAsync(nodo, config, archivo, modo, ct));
            }

            var resultado = resumen.Resultado();
            resultado.Message = $"{plan.Pendientes.Count} archivos nuevos en Drive. " + resultado.Message
                + (plan.Ignorados.Count > 0 ? $"; {plan.Ignorados.Count} archivos ignorados (copias o nombre no reconocido)" : "");
            await ActualizarEstadoSincronizacionAsync(nodo.NodoConexionId, nodo.FranquiciaId, resultado.Success ? "SUCCESS" : (resultado.TicketsProcesados > 0 ? "WARNING" : "ERROR"));
            return resultado;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Drive: error sincronizando nodo {NodoId}", nodoConexionId);
            return new EjecucionResultDto { Success = false, Message = ex.Message, ErrorsCount = 1 };
        }
        finally
        {
            _candado.Release();
        }
    }

    public async Task<DriveEstadoDto> GetEstadoAsync(int nodoConexionId, CancellationToken ct = default)
    {
        var (nodo, config, error) = await CargarAsync(nodoConexionId);
        if (error != null) throw new InvalidOperationException(error);

        var archivos = await _drive.ListarArchivosAsync(config!.CarpetaId, ct);
        var cargados = await GetBatchesCargadosAsync(nodo!.FranquiciaId);
        var plan = DriveArchivosPlan.Armar(archivos, config.CodigoTienda, config.FechaInicio, cargados);

        return new DriveEstadoDto
        {
            ArchivosEnCarpeta = archivos.Count,
            YaCargados = plan.YaCargados,
            Pendientes = plan.Pendientes.Select(p => p.Nombre).ToList(),
            Ignorados = plan.Ignorados
        };
    }

    private async Task<EjecucionResultDto> CargarArchivoAsync(
        Models.DTOs.NodoConexionDto nodo, DriveConfig config, DriveArchivoVentas archivo, string modo, CancellationToken ct)
    {
        var ejecucionId = await RegistrarInicioEjecucionAsync(nodo.NodoConexionId, archivo.Fecha, modo);
        try
        {
            var json = await _drive.DescargarTextoAsync(archivo.Id, ct);
            var request = JsonSerializer.Deserialize<DailySalesBatchRequest>(json, _jsonOpciones);
            if (request?.BatchHeader == null)
                return await FalloAsync(ejecucionId, $"{archivo.Nombre}: no es un daily_sales_batch válido");

            // El archivo tiene que ser de esta tienda y del lote que dice el nombre
            if (!string.Equals(request.BatchHeader.Franchise?.FranchiseCode, config.CodigoTienda, StringComparison.OrdinalIgnoreCase))
                return await FalloAsync(ejecucionId, $"{archivo.Nombre}: franchise_code {request.BatchHeader.Franchise?.FranchiseCode} no es {config.CodigoTienda}");
            if (!string.Equals(request.BatchHeader.BatchId, archivo.BatchId, StringComparison.OrdinalIgnoreCase))
                return await FalloAsync(ejecucionId, $"{archivo.Nombre}: batch_id {request.BatchHeader.BatchId} no coincide con el nombre del archivo");

            var respuesta = await _ingestionService.ProcessBatchAsync(request, nodo.FranquiciaId, json);
            var resumenErrores = respuesta.Errors?.Count > 0
                ? string.Join("; ", respuesta.Errors.Take(3).Select(e => e.Message))
                : null;

            if (respuesta.Status is "REJECTED")
                return await FalloAsync(ejecucionId, $"{archivo.Nombre}: rechazado. {resumenErrores}");

            var tickets = respuesta.Summary?.TicketsProcessed ?? 0;
            var mensaje = respuesta.Status == "DUPLICATE" ? $"{archivo.Nombre}: ya estaba cargado" : archivo.Nombre;
            await RegistrarFinEjecucionAsync(ejecucionId, "SUCCESS", tickets, respuesta.Summary?.ItemsProcessed ?? 0, 0, mensaje);
            _logger.LogInformation("Drive: {Archivo} -> {Estado}, {Tickets} tickets", archivo.Nombre, respuesta.Status, tickets);

            return new EjecucionResultDto
            {
                Success = true,
                Message = mensaje,
                TicketsProcesados = tickets,
                LineasProcesadas = respuesta.Summary?.ItemsProcessed ?? 0,
                EjecucionId = ejecucionId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Drive: error cargando {Archivo}", archivo.Nombre);
            return await FalloAsync(ejecucionId, $"{archivo.Nombre}: {ex.Message}");
        }
    }

    private async Task<EjecucionResultDto> FalloAsync(long ejecucionId, string mensaje)
    {
        await RegistrarFinEjecucionAsync(ejecucionId, "ERROR", 0, 0, 1, mensaje);
        return new EjecucionResultDto { Success = false, Message = mensaje, ErrorsCount = 1, EjecucionId = ejecucionId };
    }

    private async Task<(Models.DTOs.NodoConexionDto? Nodo, DriveConfig? Config, string? Error)> CargarAsync(int nodoConexionId)
    {
        var nodo = await _conexionRepository.GetNodoByIdAsync(nodoConexionId);
        if (nodo == null) return (null, null, "Nodo no encontrado");
        if (!_drive.EstaConfigurado) return (nodo, null, "Falta la clave de la cuenta de servicio de Google Drive en el servidor");

        var json = await _conexionRepository.GetConfiguracionJsonAsync(nodoConexionId);
        var config = string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<DriveConfigNodo>(json, _jsonOpciones)?.Drive;
        if (config == null || string.IsNullOrWhiteSpace(config.CarpetaId) || string.IsNullOrWhiteSpace(config.CodigoTienda))
            return (nodo, null, "El nodo no tiene configurada la sección drive (carpeta_id, codigo_tienda)");

        return (nodo, config, null);
    }

    private async Task<HashSet<string>> GetBatchesCargadosAsync(int franquiciaId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var ids = await connection.QueryAsync<string>(
            "SELECT BatchId FROM stg.IngestionBatch WHERE FranquiciaId = @FranquiciaId",
            new { FranquiciaId = franquiciaId });
        return ids.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<long> RegistrarInicioEjecucionAsync(int nodoConexionId, DateTime fechaNegocio, string modo)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleAsync<long>(@"
            INSERT INTO log.EjecucionNodo (NodoConexionId, FechaNegocio, InicioEjecucion, Estado, ModoEjecucion, ApiVersionOrigen)
            OUTPUT INSERTED.EjecucionNodoId
            VALUES (@NodoConexionId, @FechaNegocio, GETUTCDATE(), 'RUNNING', @Modo, @Marca)",
            new { NodoConexionId = nodoConexionId, FechaNegocio = fechaNegocio, Modo = modo, Marca = MarcaEjecucion });
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

    private async Task ActualizarEstadoSincronizacionAsync(int nodoConexionId, int franquiciaId, string estado)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(@"
            UPDATE dim.NodoConexion SET UltimaSincronizacion = GETUTCDATE(), UltimoEstado = @Estado, ModificadoEn = GETUTCDATE()
            WHERE NodoConexionId = @NodoId;
            UPDATE dim.Franquicia SET EstadoIntegracion = @EstadoIntegracion, UltimaSincronizacion = GETUTCDATE()
            WHERE FranquiciaId = @FranquiciaId;",
            new
            {
                NodoId = nodoConexionId, Estado = estado, FranquiciaId = franquiciaId,
                EstadoIntegracion = estado == "SUCCESS" ? "OK" : (estado == "WARNING" ? "WARNING" : "ERROR")
            });
    }
}

/// <summary>Qué archivos de la carpeta hay que cargar (sin base de datos ni Drive, para poder testearlo).</summary>
public static class DriveArchivosPlan
{
    // LC_BOG_ZG01_2026-10-25_LC-BOG_ZG01-20261025-001.json
    private static readonly Regex Nombre = new(
        @"^LC_(?<tienda>[A-Z0-9_]+?)_(?<fecha>\d{4}-\d{2}-\d{2})_(?<batch>LC-[A-Z0-9_]+-\d{8}-\d{3})\.json$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public record Resultado(List<DriveArchivoVentas> Pendientes, int YaCargados, List<string> Ignorados);

    public static Resultado Armar(IEnumerable<DriveArchivo> archivos, string codigoTienda, DateTime? fechaInicio, IReadOnlySet<string> batchesCargados)
    {
        var pendientes = new List<DriveArchivoVentas>();
        var ignorados = new List<string>();
        var yaCargados = 0;
        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var a in archivos)
        {
            var m = Nombre.Match(a.Nombre.Trim());
            if (!m.Success || !string.Equals(m.Groups["tienda"].Value, codigoTienda, StringComparison.OrdinalIgnoreCase))
            {
                ignorados.Add(a.Nombre);
                continue;
            }

            var fecha = DateTime.ParseExact(m.Groups["fecha"].Value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            if (fechaInicio.HasValue && fecha < fechaInicio.Value.Date) continue;

            var batch = m.Groups["batch"].Value;
            if (batchesCargados.Contains(batch)) { yaCargados++; continue; }
            if (!vistos.Add(batch)) { ignorados.Add(a.Nombre); continue; } // mismo lote dos veces en la carpeta

            pendientes.Add(new DriveArchivoVentas(a.Id, a.Nombre, fecha, batch));
        }

        return new Resultado(pendientes.OrderBy(p => p.Fecha).ThenBy(p => p.BatchId).ToList(), yaCargados, ignorados);
    }
}

public record DriveArchivoVentas(string Id, string Nombre, DateTime Fecha, string BatchId);

public class DriveConfigNodo
{
    [JsonPropertyName("drive")] public DriveConfig? Drive { get; set; }
}

public class DriveConfig
{
    [JsonPropertyName("carpeta_id")] public string CarpetaId { get; set; } = string.Empty;
    [JsonPropertyName("codigo_tienda")] public string CodigoTienda { get; set; } = string.Empty;
    [JsonPropertyName("fecha_inicio")] public DateTime? FechaInicio { get; set; }
    /// <summary>Si la sincronización diaria (ConectoresSyncJob) incluye este nodo.</summary>
    [JsonPropertyName("auto")] public bool Auto { get; set; }
}

public class DriveEstadoDto
{
    [JsonPropertyName("archivos_en_carpeta")] public int ArchivosEnCarpeta { get; set; }
    [JsonPropertyName("ya_cargados")] public int YaCargados { get; set; }
    [JsonPropertyName("pendientes")] public List<string> Pendientes { get; set; } = new();
    [JsonPropertyName("ignorados")] public List<string> Ignorados { get; set; } = new();
}
