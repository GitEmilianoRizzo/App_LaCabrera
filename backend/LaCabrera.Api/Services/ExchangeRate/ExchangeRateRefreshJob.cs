using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace LaCabrera.Api.Services.ExchangeRate;

/// <summary>
/// Job de background que refresca las tasas de cambio diariamente.
/// Horario configurable: por defecto 12:00 hora Argentina (UTC-3).
///
/// Configuración en appsettings.json:
/// {
///   "ExchangeRate": {
///     "RefreshHourUtc": 15,  // 15:00 UTC = 12:00 ARG
///     "RefreshMinute": 0,
///     "Enabled": true
///   }
/// }
/// </summary>
public class ExchangeRateRefreshJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ExchangeRateRefreshJob> _logger;
    private readonly int _refreshHourUtc;
    private readonly int _refreshMinute;
    private readonly bool _enabled;

    public ExchangeRateRefreshJob(
        IServiceProvider serviceProvider,
        ILogger<ExchangeRateRefreshJob> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        // Configuración del job (default: 15:00 UTC = 12:00 ARG)
        _refreshHourUtc = configuration.GetValue("ExchangeRate:RefreshHourUtc", 15);
        _refreshMinute = configuration.GetValue("ExchangeRate:RefreshMinute", 0);
        _enabled = configuration.GetValue("ExchangeRate:Enabled", true);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("ExchangeRateRefreshJob está deshabilitado");
            return;
        }

        _logger.LogInformation(
            "ExchangeRateRefreshJob iniciado. Programado para {Hour}:{Minute} UTC",
            _refreshHourUtc, _refreshMinute);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                var nextRun = GetNextRunTime(now);
                var delay = nextRun - now;

                if (delay > TimeSpan.Zero)
                {
                    _logger.LogDebug(
                        "Próxima ejecución de FX refresh: {NextRun} UTC (en {Hours}h {Minutes}m)",
                        nextRun, delay.Hours, delay.Minutes);

                    await Task.Delay(delay, stoppingToken);
                }

                if (stoppingToken.IsCancellationRequested)
                    break;

                await ExecuteRefreshAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ExchangeRateRefreshJob");
                // Esperar 5 minutos antes de reintentar
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        _logger.LogInformation("ExchangeRateRefreshJob detenido");
    }

    private async Task ExecuteRefreshAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Iniciando refresh diario de tasas de cambio");

        using var scope = _serviceProvider.CreateScope();
        var exchangeRateService = scope.ServiceProvider.GetRequiredService<IExchangeRateService>();

        // Refrescar la fecha de hoy (hora Argentina)
        var argentinaTime = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("Argentina Standard Time"));

        var fechaHoy = argentinaTime.Date;

        // También verificar si hay días anteriores sin tasa (por si hubo fallas)
        var fechaDesde = fechaHoy.AddDays(-7); // Última semana

        var result = await exchangeRateService.RefreshRatesAsync(fechaDesde, fechaHoy, stoppingToken);

        if (result.Estado == "OK")
        {
            _logger.LogInformation(
                "Refresh completado exitosamente: {Monedas} monedas, {Dias} días",
                result.MonedasObtenidas, result.DiasObtenidos);
        }
        else if (result.Estado == "PARCIAL")
        {
            _logger.LogWarning(
                "Refresh parcial: {Monedas} monedas, {Dias} días. Faltantes: {Faltantes}",
                result.MonedasObtenidas, result.DiasObtenidos, result.MonedasFaltantes);
        }
        else
        {
            _logger.LogError("Refresh fallido: {Error}", result.MensajeError);
        }
    }

    private DateTime GetNextRunTime(DateTime now)
    {
        var today = now.Date;
        var scheduledTime = today.AddHours(_refreshHourUtc).AddMinutes(_refreshMinute);

        // Si ya pasó la hora programada hoy, programar para mañana
        if (now >= scheduledTime)
        {
            scheduledTime = scheduledTime.AddDays(1);
        }

        return scheduledTime;
    }
}
