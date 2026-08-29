using System.Text.Json;
using System.Text.Json.Serialization;
using LaCabrera.Api.Models;
using Microsoft.Extensions.Logging;

namespace LaCabrera.Api.Services.ExchangeRate;

/// <summary>
/// Proveedor de tipo de cambio del BCRA (Banco Central de la República Argentina).
///
/// SEMÁNTICA DE LOS CAMPOS DEL BCRA:
/// - tipoPase: USD equivalentes a 1 unidad de la moneda (ej: EUR tipoPase=1.168 significa 1 EUR = 1.168 USD)
/// - tipoCotizacion: ARS necesarios para comprar 1 unidad de la moneda
///
/// CONVERSIÓN A UnidadesPorUsd:
/// - Para ARS: usar tipoCotizacion del USD directamente (ej: 1499 significa 1499 ARS = 1 USD)
/// - Para otras monedas: invertir tipoPase (ej: 1/1.168 = 0.856 EUR = 1 USD)
/// </summary>
public class BcraExchangeRateProvider : IExchangeRateProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BcraExchangeRateProvider> _logger;
    private const string BaseUrl = "https://api.bcra.gob.ar/estadisticascambiarias/v1.0";
    private const int DelayBetweenRequestsMs = 150;

    // Monedas soportadas por el BCRA (confirmadas en discovery)
    private static readonly string[] _supportedCurrencies = { "USD", "ARS", "EUR", "COP", "PYG" };

    public string ProviderCode => "BCRA";
    public IReadOnlyList<string> SupportedCurrencies => _supportedCurrencies;

    public BcraExchangeRateProvider(HttpClient httpClient, ILogger<BcraExchangeRateProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.BaseAddress = new Uri(BaseUrl);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "LaCabrera-Central/1.0");
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
            _logger.LogWarning("No hay monedas soportadas por BCRA en la lista solicitada");
            return results;
        }

        try
        {
            var fechaStr = fecha.ToString("yyyy-MM-dd");
            var response = await _httpClient.GetAsync($"/Cotizaciones?fecha={fechaStr}", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Error al consultar BCRA: {StatusCode}", response.StatusCode);
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
            var bcraResponse = JsonSerializer.Deserialize<BcraApiResponse>(content);

            if (bcraResponse?.Results?.Detalle == null || !bcraResponse.Results.Detalle.Any())
            {
                // Fecha sin cotización (fin de semana o feriado)
                _logger.LogInformation("Sin cotización BCRA para {Fecha} (probablemente fin de semana/feriado)", fechaStr);
                return monedasToFetch.Select(m => new ExchangeRateResult
                {
                    CodigoMoneda = m,
                    Fecha = fecha,
                    Success = false,
                    ErrorMessage = "SIN_COTIZACION",
                    Proveedor = ProviderCode
                }).ToList();
            }

            // Obtener tipoCotizacion del USD para calcular ARS
            var usdCotizacion = bcraResponse.Results.Detalle
                .FirstOrDefault(d => d.CodigoMoneda == "USD")?.TipoCotizacion ?? 0;

            foreach (var moneda in monedasToFetch)
            {
                var cotizacion = bcraResponse.Results.Detalle
                    .FirstOrDefault(d => d.CodigoMoneda == moneda);

                if (cotizacion == null)
                {
                    results.Add(new ExchangeRateResult
                    {
                        CodigoMoneda = moneda,
                        Fecha = fecha,
                        Success = false,
                        ErrorMessage = "MONEDA_NO_ENCONTRADA",
                        Proveedor = ProviderCode
                    });
                    continue;
                }

                decimal unidadesPorUsd;

                if (moneda == "USD")
                {
                    unidadesPorUsd = 1.0m;
                }
                else if (moneda == "ARS")
                {
                    // Para ARS, usar tipoCotizacion del USD
                    unidadesPorUsd = usdCotizacion;
                }
                else
                {
                    // Para otras monedas, invertir tipoPase
                    if (cotizacion.TipoPase == 0)
                    {
                        results.Add(new ExchangeRateResult
                        {
                            CodigoMoneda = moneda,
                            Fecha = fecha,
                            Success = false,
                            ErrorMessage = "TIPO_PASE_CERO",
                            Proveedor = ProviderCode
                        });
                        continue;
                    }
                    unidadesPorUsd = 1.0m / cotizacion.TipoPase;
                }

                results.Add(new ExchangeRateResult
                {
                    CodigoMoneda = moneda,
                    Fecha = fecha,
                    UnidadesPorUsd = unidadesPorUsd,
                    Success = true,
                    Proveedor = ProviderCode
                });
            }

            await Task.Delay(DelayBetweenRequestsMs, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener cotizaciones del BCRA para {Fecha}", fecha);
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
            var response = await _httpClient.GetAsync("/Maestros/Divisas", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    #region BCRA API Response Models

    private class BcraApiResponse
    {
        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonPropertyName("results")]
        public BcraResults? Results { get; set; }
    }

    private class BcraResults
    {
        [JsonPropertyName("fecha")]
        public string? Fecha { get; set; }

        [JsonPropertyName("detalle")]
        public List<BcraCotizacion>? Detalle { get; set; }
    }

    private class BcraCotizacion
    {
        [JsonPropertyName("codigoMoneda")]
        public string CodigoMoneda { get; set; } = string.Empty;

        [JsonPropertyName("descripcion")]
        public string Descripcion { get; set; } = string.Empty;

        [JsonPropertyName("tipoPase")]
        public decimal TipoPase { get; set; }

        [JsonPropertyName("tipoCotizacion")]
        public decimal TipoCotizacion { get; set; }
    }

    #endregion
}
