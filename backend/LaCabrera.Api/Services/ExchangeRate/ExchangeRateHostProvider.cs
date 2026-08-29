using System.Text.Json;
using System.Text.Json.Serialization;
using LaCabrera.Api.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LaCabrera.Api.Services.ExchangeRate;

/// <summary>
/// Proveedor secundario de tipo de cambio usando exchangerate.host API.
/// Usado para monedas no disponibles en BCRA (ej: PHP - Peso Filipino).
///
/// API: https://exchangerate.host
/// Documentación: https://exchangerate.host/documentation
///
/// Este proveedor devuelve tasas basadas en USD como moneda base.
/// </summary>
public class ExchangeRateHostProvider : IExchangeRateProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ExchangeRateHostProvider> _logger;
    private readonly string? _apiKey;
    private const string BaseUrl = "https://api.exchangerate.host";

    // Monedas que este proveedor maneja (las que BCRA no tiene)
    private static readonly string[] _supportedCurrencies = { "PHP" };

    public string ProviderCode => "EXCHANGERATE_HOST";
    public IReadOnlyList<string> SupportedCurrencies => _supportedCurrencies;

    public ExchangeRateHostProvider(
        HttpClient httpClient,
        ILogger<ExchangeRateHostProvider> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["ExchangeRate:ApiKey"];
        _httpClient.BaseAddress = new Uri(BaseUrl);
    }

    public bool SupportsCurrency(string currencyCode)
        => _supportedCurrencies.Contains(currencyCode.ToUpperInvariant());

    public async Task<IReadOnlyList<ExchangeRateResult>> GetRatesAsync(
        DateTime fecha,
        IEnumerable<string> monedas,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ExchangeRateResult>();
        var monedasToFetch = monedas.Where(SupportsCurrency).ToList();

        if (!monedasToFetch.Any())
        {
            return results;
        }

        try
        {
            var fechaStr = fecha.ToString("yyyy-MM-dd");
            var symbols = string.Join(",", monedasToFetch);

            // Usar endpoint histórico con USD como base
            var url = $"/historical?date={fechaStr}&base=USD&symbols={symbols}";
            if (!string.IsNullOrEmpty(_apiKey))
            {
                url += $"&access_key={_apiKey}";
            }

            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Error al consultar exchangerate.host: {StatusCode}", response.StatusCode);
                return monedasToFetch.Select(m => new ExchangeRateResult
                {
                    CodigoMoneda = m,
                    Fecha = fecha,
                    Success = false,
                    ErrorMessage = $"HTTP {(int)response.StatusCode}",
                    Proveedor = ProviderCode
                }).ToList();
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var apiResponse = JsonSerializer.Deserialize<ExchangeRateHostResponse>(content);

            if (apiResponse == null || !apiResponse.Success)
            {
                _logger.LogWarning("Respuesta inválida de exchangerate.host para {Fecha}", fechaStr);
                return monedasToFetch.Select(m => new ExchangeRateResult
                {
                    CodigoMoneda = m,
                    Fecha = fecha,
                    Success = false,
                    ErrorMessage = "RESPUESTA_INVALIDA",
                    Proveedor = ProviderCode
                }).ToList();
            }

            foreach (var moneda in monedasToFetch)
            {
                if (apiResponse.Rates != null && apiResponse.Rates.TryGetValue(moneda, out var rate))
                {
                    // La API devuelve cuántas unidades de la moneda = 1 USD
                    // Esto es exactamente lo que necesitamos para UnidadesPorUsd
                    results.Add(new ExchangeRateResult
                    {
                        CodigoMoneda = moneda,
                        Fecha = fecha,
                        UnidadesPorUsd = rate,
                        Success = true,
                        Proveedor = ProviderCode
                    });
                }
                else
                {
                    results.Add(new ExchangeRateResult
                    {
                        CodigoMoneda = moneda,
                        Fecha = fecha,
                        Success = false,
                        ErrorMessage = "MONEDA_NO_ENCONTRADA",
                        Proveedor = ProviderCode
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener cotizaciones de exchangerate.host para {Fecha}", fecha);
            return monedasToFetch.Select(m => new ExchangeRateResult
            {
                CodigoMoneda = m,
                Fecha = fecha,
                Success = false,
                ErrorMessage = ex.Message,
                Proveedor = ProviderCode
            }).ToList();
        }

        return results;
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var url = "/latest?base=USD&symbols=PHP";
            if (!string.IsNullOrEmpty(_apiKey))
            {
                url += $"&access_key={_apiKey}";
            }

            var response = await _httpClient.GetAsync(url, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    #region API Response Models

    private class ExchangeRateHostResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("historical")]
        public bool Historical { get; set; }

        [JsonPropertyName("date")]
        public string? Date { get; set; }

        [JsonPropertyName("base")]
        public string? Base { get; set; }

        [JsonPropertyName("rates")]
        public Dictionary<string, decimal>? Rates { get; set; }
    }

    #endregion
}
