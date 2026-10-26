using Dapper;
using LaCabrera.Api.Configuration;

namespace LaCabrera.Api.Services;

/// <summary>
/// Job diario de los nodos GCOM con sync.auto = true.
/// Corre una vez por día y pide los días pendientes hasta agotar el cupo de la tienda:
/// primero ayer y después la carga histórica desde sync.fecha_inicio, del día más viejo al más nuevo.
///
/// Configuración en appsettings.json (deshabilitado por defecto):
/// {
///   "GcomSync": {
///     "Enabled": false,
///     "HourUtc": 10,   // 10:00 UTC = 07:00 en Porto Alegre, después del cierre del día de GCOM
///     "Minute": 0
///   }
/// }
/// </summary>
public class GcomSyncJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<GcomSyncJob> _logger;
    private readonly bool _enabled;
    private readonly int _hourUtc;
    private readonly int _minute;

    public GcomSyncJob(IServiceProvider serviceProvider, ILogger<GcomSyncJob> logger, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _enabled = configuration.GetValue("GcomSync:Enabled", false);
        _hourUtc = configuration.GetValue("GcomSync:HourUtc", 10);
        _minute = configuration.GetValue("GcomSync:Minute", 0);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("GcomSyncJob está deshabilitado");
            return;
        }

        _logger.LogInformation("GcomSyncJob iniciado. Programado para {Hour}:{Minute:D2} UTC", _hourUtc, _minute);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                var next = now.Date.AddHours(_hourUtc).AddMinutes(_minute);
                if (next <= now) next = next.AddDays(1);
                await Task.Delay(next - now, stoppingToken);

                await SincronizarNodosAsync();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en GcomSyncJob");
                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
            }
        }
    }

    private async Task SincronizarNodosAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var connectionFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
        var extractor = scope.ServiceProvider.GetRequiredService<IGcomExtractorService>();

        using var connection = connectionFactory.CreateConnection();
        var nodos = await connection.QueryAsync<int>(@"
            SELECT NodoConexionId FROM dim.NodoConexion
            WHERE TipoConector = 'GCOM' AND Estado = 'ACTIVE' AND Activo = 1
              AND JSON_VALUE(ConfiguracionJson, '$.sync.auto') = 'true'");

        foreach (var nodoId in nodos)
        {
            var resultado = await extractor.EjecutarExtraccionDesdeUltimoAsync(nodoId, "SCHEDULED");
            _logger.LogInformation("GcomSyncJob nodo {NodoId}: {Mensaje}", nodoId, resultado.Message);
        }
    }
}
