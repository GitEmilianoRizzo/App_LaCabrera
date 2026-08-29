using System.Diagnostics;
using LaCabrera.Api.Models;
using LaCabrera.Api.Repositories;
using Microsoft.Extensions.Logging;

namespace LaCabrera.Api.Services.ExchangeRate;

public interface IExchangeRateService
{
    /// <summary>
    /// Obtiene la tasa de cambio para una moneda y fecha específica
    /// </summary>
    Task<Models.ExchangeRate?> GetRateAsync(string codigoMoneda, DateTime fecha);

    /// <summary>
    /// Obtiene todas las tasas para un rango de fechas
    /// </summary>
    Task<IEnumerable<Models.ExchangeRate>> GetRatesAsync(DateTime fechaDesde, DateTime fechaHasta, string? codigoMoneda = null);

    /// <summary>
    /// Refresca las tasas de cambio para un rango de fechas
    /// </summary>
    Task<ExchangeRateIngestLog> RefreshRatesAsync(DateTime fechaDesde, DateTime fechaHasta, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refresca las tasas de cambio para una fecha específica
    /// </summary>
    Task<ExchangeRateIngestLog> RefreshRatesForDateAsync(DateTime fecha, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aplica un override manual para una tasa de cambio
    /// </summary>
    Task ApplyOverrideAsync(string codigoMoneda, DateTime fecha, decimal unidadesPorUsd, string observacion);

    /// <summary>
    /// Verifica el estado de los proveedores
    /// </summary>
    Task<Dictionary<string, bool>> HealthCheckProvidersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene los logs recientes de ingesta
    /// </summary>
    Task<IEnumerable<ExchangeRateIngestLog>> GetRecentLogsAsync(int count = 10);
}

public class ExchangeRateService : IExchangeRateService
{
    private readonly IEnumerable<IExchangeRateProvider> _providers;
    private readonly IExchangeRateRepository _repository;
    private readonly ILogger<ExchangeRateService> _logger;

    public ExchangeRateService(
        IEnumerable<IExchangeRateProvider> providers,
        IExchangeRateRepository repository,
        ILogger<ExchangeRateService> logger)
    {
        _providers = providers;
        _repository = repository;
        _logger = logger;
    }

    public async Task<Models.ExchangeRate?> GetRateAsync(string codigoMoneda, DateTime fecha)
    {
        return await _repository.GetRateAsync(codigoMoneda, fecha.Date);
    }

    public async Task<IEnumerable<Models.ExchangeRate>> GetRatesAsync(
        DateTime fechaDesde,
        DateTime fechaHasta,
        string? codigoMoneda = null)
    {
        return await _repository.GetRatesForDateRangeAsync(fechaDesde, fechaHasta, codigoMoneda);
    }

    public async Task<ExchangeRateIngestLog> RefreshRatesAsync(
        DateTime fechaDesde,
        DateTime fechaHasta,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var log = new ExchangeRateIngestLog
        {
            FechaEjecucion = DateTime.Now,
            Proveedor = string.Join(",", _providers.Select(p => p.ProviderCode)),
            FechaDesde = fechaDesde.Date,
            FechaHasta = fechaHasta.Date
        };

        try
        {
            // Obtener monedas activas de franquicias
            var monedas = (await _repository.GetActiveCurrenciesAsync()).ToList();
            log.MonedasConsultadas = string.Join(",", monedas);

            _logger.LogInformation(
                "Iniciando refresh de tasas de {FechaDesde} a {FechaHasta} para {Monedas}",
                fechaDesde, fechaHasta, log.MonedasConsultadas);

            var allRates = new List<Models.ExchangeRate>();
            var monedasFaltantes = new HashSet<string>();
            int requestCount = 0;

            // Iterar por cada día en el rango
            for (var fecha = fechaDesde.Date; fecha <= fechaHasta.Date; fecha = fecha.AddDays(1))
            {
                // Obtener tasas para cada moneda usando su proveedor preferido
                var ratesForDate = new List<ExchangeRateResult>();
                foreach (var moneda in monedas)
                {
                    var monedaRates = await FetchRatesForDateWithPreferredProviderAsync(fecha, moneda, cancellationToken);
                    ratesForDate.AddRange(monedaRates);
                    requestCount++;
                }

                foreach (var moneda in monedas)
                {
                    var rate = ratesForDate.FirstOrDefault(r => r.CodigoMoneda == moneda && r.Success);

                    if (rate != null)
                    {
                        allRates.Add(new Models.ExchangeRate
                        {
                            CodigoMoneda = moneda,
                            Fecha = fecha,
                            FechaCotizacionOrigen = fecha,
                            UnidadesPorUsd = rate.UnidadesPorUsd,
                            TipoTasa = "OFICIAL",
                            Proveedor = rate.Proveedor,
                            EsArrastrada = false,
                            EsOverrideManual = false
                        });
                    }
                    else
                    {
                        // Buscar última tasa conocida para arrastrar
                        var lastRate = await GetLastKnownRateAsync(moneda, fecha, allRates);

                        if (lastRate != null)
                        {
                            allRates.Add(new Models.ExchangeRate
                            {
                                CodigoMoneda = moneda,
                                Fecha = fecha,
                                FechaCotizacionOrigen = lastRate.FechaCotizacionOrigen,
                                UnidadesPorUsd = lastRate.UnidadesPorUsd,
                                TipoTasa = "OFICIAL",
                                Proveedor = lastRate.Proveedor,
                                EsArrastrada = true,
                                EsOverrideManual = false
                            });
                            log.DiasArrastrados++;
                        }
                        else
                        {
                            monedasFaltantes.Add(moneda);
                            _logger.LogWarning("Sin tasa disponible para {Moneda} en {Fecha}", moneda, fecha);
                        }
                    }
                }
            }

            // Guardar todas las tasas
            await _repository.UpsertRatesAsync(allRates);

            log.MonedasObtenidas = allRates.Select(r => r.CodigoMoneda).Distinct().Count();
            log.DiasObtenidos = allRates.Where(r => !r.EsArrastrada).Select(r => r.Fecha).Distinct().Count();
            log.RequestsRealizados = requestCount;
            log.MonedasFaltantes = monedasFaltantes.Any() ? string.Join(",", monedasFaltantes) : null;
            log.Estado = monedasFaltantes.Any() ? "PARCIAL" : "OK";

            _logger.LogInformation(
                "Refresh completado: {MonedasObtenidas} monedas, {DiasObtenidos} días reales, {DiasArrastrados} días arrastrados",
                log.MonedasObtenidas, log.DiasObtenidos, log.DiasArrastrados);
        }
        catch (Exception ex)
        {
            log.Estado = "ERROR";
            log.MensajeError = ex.Message;
            _logger.LogError(ex, "Error en refresh de tasas de cambio");
        }
        finally
        {
            stopwatch.Stop();
            log.DuracionMs = (int)stopwatch.ElapsedMilliseconds;
            await _repository.LogIngestAsync(log);
        }

        return log;
    }

    public async Task<ExchangeRateIngestLog> RefreshRatesForDateAsync(
        DateTime fecha,
        CancellationToken cancellationToken = default)
    {
        return await RefreshRatesAsync(fecha, fecha, cancellationToken);
    }

    public async Task ApplyOverrideAsync(
        string codigoMoneda,
        DateTime fecha,
        decimal unidadesPorUsd,
        string observacion)
    {
        if (string.IsNullOrWhiteSpace(observacion))
        {
            throw new ArgumentException("El override manual requiere una observación", nameof(observacion));
        }

        var rate = new Models.ExchangeRate
        {
            CodigoMoneda = codigoMoneda.ToUpperInvariant(),
            Fecha = fecha.Date,
            FechaCotizacionOrigen = fecha.Date,
            UnidadesPorUsd = unidadesPorUsd,
            TipoTasa = "OFICIAL",
            Proveedor = "MANUAL",
            EsArrastrada = false,
            EsOverrideManual = true,
            ObservacionOverride = observacion
        };

        await _repository.UpsertRateAsync(rate);

        _logger.LogWarning(
            "Override manual aplicado: {Moneda} {Fecha} = {Tasa} USD. Motivo: {Observacion}",
            codigoMoneda, fecha, unidadesPorUsd, observacion);
    }

    public async Task<Dictionary<string, bool>> HealthCheckProvidersAsync(
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, bool>();

        foreach (var provider in _providers)
        {
            try
            {
                results[provider.ProviderCode] = await provider.HealthCheckAsync(cancellationToken);
            }
            catch
            {
                results[provider.ProviderCode] = false;
            }
        }

        return results;
    }

    public async Task<IEnumerable<ExchangeRateIngestLog>> GetRecentLogsAsync(int count = 10)
    {
        return await _repository.GetRecentLogsAsync(count);
    }

    private async Task<Models.ExchangeRate?> GetLastKnownRateAsync(
        string codigoMoneda,
        DateTime fecha,
        List<Models.ExchangeRate> currentBatchRates)
    {
        // Primero buscar en las tasas del batch actual
        var fromBatch = currentBatchRates
            .Where(r => r.CodigoMoneda == codigoMoneda && r.Fecha < fecha && !r.EsArrastrada)
            .OrderByDescending(r => r.Fecha)
            .FirstOrDefault();

        if (fromBatch != null)
            return fromBatch;

        // Si no hay en el batch, buscar en la base de datos
        var lastDate = await _repository.GetLastRateDateAsync(codigoMoneda);
        if (lastDate.HasValue)
        {
            return await _repository.GetRateAsync(codigoMoneda, lastDate.Value);
        }

        return null;
    }

    private async Task<int> GetProviderPriorityAsync(string providerCode, string? codigoMoneda = null)
    {
        // Si hay una moneda específica, verificar si tiene proveedor preferido
        if (!string.IsNullOrEmpty(codigoMoneda))
        {
            var preferredProviderId = await _repository.GetPreferredProviderIdAsync(codigoMoneda);
            if (preferredProviderId.HasValue)
            {
                var preferredProvider = await _repository.GetProviderAsync(preferredProviderId.Value);
                if (preferredProvider != null && preferredProvider.Codigo == providerCode)
                {
                    return 0; // Máxima prioridad para el proveedor preferido
                }
            }
        }

        // Obtener prioridad de la base de datos
        var providers = await _repository.GetProvidersAsync();
        var provider = providers.FirstOrDefault(p => p.Codigo == providerCode);
        return provider?.Prioridad ?? 100;
    }

    private async Task<IReadOnlyList<ExchangeRateResult>> FetchRatesForDateWithPreferredProviderAsync(
        DateTime fecha,
        string moneda,
        CancellationToken cancellationToken)
    {
        var results = new List<ExchangeRateResult>();

        // Obtener el proveedor preferido para esta moneda
        var preferredProviderId = await _repository.GetPreferredProviderIdAsync(moneda);

        // Ordenar proveedores: preferido primero, luego por prioridad de BD
        var orderedProviders = new List<IExchangeRateProvider>();

        if (preferredProviderId.HasValue)
        {
            var preferredProviderInfo = await _repository.GetProviderAsync(preferredProviderId.Value);
            if (preferredProviderInfo != null)
            {
                var preferredProvider = _providers.FirstOrDefault(p => p.ProviderCode == preferredProviderInfo.Codigo);
                if (preferredProvider != null && preferredProvider.SupportsCurrency(moneda))
                {
                    orderedProviders.Add(preferredProvider);
                }
            }
        }

        // Agregar el resto de proveedores ordenados por prioridad
        var allProviders = await _repository.GetProvidersAsync();
        var priorityOrder = allProviders.ToDictionary(p => p.Codigo, p => p.Prioridad);

        var otherProviders = _providers
            .Where(p => !orderedProviders.Contains(p) && p.SupportsCurrency(moneda))
            .OrderBy(p => priorityOrder.GetValueOrDefault(p.ProviderCode, 100));

        orderedProviders.AddRange(otherProviders);

        // Intentar con cada proveedor hasta obtener resultado
        foreach (var provider in orderedProviders)
        {
            var providerResults = await provider.GetRatesAsync(fecha, new[] { moneda }, cancellationToken);
            var successResult = providerResults.FirstOrDefault(r => r.Success);

            if (successResult != null)
            {
                results.Add(successResult);
                break;
            }
        }

        return results;
    }
}
