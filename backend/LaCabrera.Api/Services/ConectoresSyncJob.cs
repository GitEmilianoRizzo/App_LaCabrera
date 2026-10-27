using Dapper;
using LaCabrera.Api.Configuration;

namespace LaCabrera.Api.Services;

/// <summary>
/// Sincronización diaria de los conectores por API (Ágora, Vinson, Ayres IT y GCOM) y de las carpetas de Drive (Colombia).
/// Una vez por día, a la hora local configurada, ejecuta "desde el último día con datos"
/// en cada nodo PULL activo, uno por vez:
/// - Ágora, Vinson y Ayres IT: piden desde el primer hueco de los últimos 60 días hasta ayer.
/// - GCOM: solo los nodos con sync.auto = true; pide ayer y la carga histórica hasta agotar el cupo de la tienda.
///
/// Configuración en appsettings.json (deshabilitado por defecto):
/// {
///   "SyncConectores": {
///     "Enabled": false,
///     "HoraLocal": 10,
///     "Minuto": 0,
///     "ZonaHoraria": "America/Argentina/Buenos_Aires"
///   }
/// }
/// </summary>
public class ConectoresSyncJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ConectoresSyncJob> _logger;
    private readonly bool _enabled;
    private readonly int _hora;
    private readonly int _minuto;
    private readonly TimeZoneInfo _zona;

    public ConectoresSyncJob(IServiceProvider serviceProvider, ILogger<ConectoresSyncJob> logger, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _enabled = configuration.GetValue("SyncConectores:Enabled", false);
        _hora = configuration.GetValue("SyncConectores:HoraLocal", 10);
        _minuto = configuration.GetValue("SyncConectores:Minuto", 0);
        _zona = ZonaHoraria(configuration.GetValue("SyncConectores:ZonaHoraria", "America/Argentina/Buenos_Aires")!);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("ConectoresSyncJob está deshabilitado");
            return;
        }

        // Se loguea como Warning a propósito: en producción el nivel mínimo es Warning y estos avisos tienen que verse
        _logger.LogWarning("ConectoresSyncJob iniciado. Corre todos los días a las {Hora}:{Minuto:D2} ({Zona})", _hora, _minuto, _zona.Id);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var ahora = DateTime.UtcNow;
                var proxima = ProximaEjecucionUtc(ahora, _hora, _minuto, _zona);
                await Task.Delay(proxima - ahora, stoppingToken);

                await SincronizarAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ConectoresSyncJob");
                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
            }
        }
    }

    /// <summary>Próximo horario de corrida (en UTC) a partir de ahora.</summary>
    public static DateTime ProximaEjecucionUtc(DateTime utcNow, int hora, int minuto, TimeZoneInfo zona)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), zona);
        var objetivo = local.Date.AddHours(hora).AddMinutes(minuto);
        if (objetivo <= local) objetivo = objetivo.AddDays(1);
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(objetivo, DateTimeKind.Unspecified), zona);
    }

    private async Task SincronizarAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var sp = scope.ServiceProvider;

        using var connection = sp.GetRequiredService<IDbConnectionFactory>().CreateConnection();
        var nodos = (await connection.QueryAsync<(int Id, string Codigo, string TipoConector)>(@"
            SELECT NodoConexionId, Codigo, TipoConector FROM dim.NodoConexion
            WHERE Modo = 'PULL' AND Estado = 'ACTIVE' AND Activo = 1
              AND (TipoConector LIKE 'AGORA%' OR TipoConector LIKE 'VINSON%' OR TipoConector = 'AYRESIT'
                   OR (TipoConector = 'GCOM' AND JSON_VALUE(ConfiguracionJson, '$.sync.auto') = 'true'))
            ORDER BY NodoConexionId")).ToList();

        // Conectores que leen JSON de una carpeta de Google Drive (Colombia)
        var nodosDrive = (await connection.QueryAsync<(int Id, string Codigo)>(@"
            SELECT NodoConexionId, Codigo FROM dim.NodoConexion
            WHERE Estado = 'ACTIVE' AND Activo = 1 AND JSON_VALUE(ConfiguracionJson, '$.drive.auto') = 'true'
            ORDER BY NodoConexionId")).ToList();

        _logger.LogWarning("ConectoresSyncJob: sincronizando {Cantidad} conectores por API y {Drive} por Drive", nodos.Count, nodosDrive.Count);

        foreach (var nodo in nodos)
        {
            if (stoppingToken.IsCancellationRequested) break;
            try
            {
                var tipo = nodo.TipoConector.ToUpperInvariant();
                EjecucionResultDto resultado = tipo switch
                {
                    _ when tipo.StartsWith("AGORA") => await sp.GetRequiredService<IAgoraExtractorService>().EjecutarExtraccionDesdeUltimoAsync(nodo.Id),
                    _ when tipo.StartsWith("VINSON") => await sp.GetRequiredService<IVinsonExtractorService>().EjecutarExtraccionDesdeUltimoAsync(nodo.Id),
                    "AYRESIT" => await sp.GetRequiredService<IAyresItExtractorService>().EjecutarExtraccionDesdeUltimoAsync(nodo.Id),
                    "GCOM" => await sp.GetRequiredService<IGcomExtractorService>().EjecutarExtraccionDesdeUltimoAsync(nodo.Id, "SCHEDULED"),
                    _ => new EjecucionResultDto { Success = false, Message = $"Tipo {nodo.TipoConector} sin sincronización automática" }
                };

                if (resultado.Success)
                    _logger.LogWarning("ConectoresSyncJob {Codigo}: {Mensaje}", nodo.Codigo, resultado.Message);
                else
                    _logger.LogError("ConectoresSyncJob {Codigo} con error: {Mensaje}", nodo.Codigo, resultado.Message);
            }
            catch (Exception ex)
            {
                // Un conector que falla no frena a los demás
                _logger.LogError(ex, "ConectoresSyncJob {Codigo}: error inesperado", nodo.Codigo);
            }
        }

        foreach (var nodo in nodosDrive)
        {
            if (stoppingToken.IsCancellationRequested) break;
            try
            {
                var resultado = await sp.GetRequiredService<IDriveJsonConectorService>().SincronizarAsync(nodo.Id, "SCHEDULED", stoppingToken);
                if (resultado.Success)
                    _logger.LogWarning("ConectoresSyncJob {Codigo}: {Mensaje}", nodo.Codigo, resultado.Message);
                else
                    _logger.LogError("ConectoresSyncJob {Codigo} con error: {Mensaje}", nodo.Codigo, resultado.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ConectoresSyncJob {Codigo}: error inesperado", nodo.Codigo);
            }
        }
    }

    private static TimeZoneInfo ZonaHoraria(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.CreateCustomTimeZone("ART", TimeSpan.FromHours(-3), "ART", "ART"); }
    }
}
