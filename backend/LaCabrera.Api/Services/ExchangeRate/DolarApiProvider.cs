using System.Text.Json;
using System.Text.Json.Serialization;
using LaCabrera.Api.Models;
using Microsoft.Extensions.Logging;

namespace LaCabrera.Api.Services.ExchangeRate;

/// <summary>
/// Proveedor de tipo de cambio usando DolarApi.com (API gratuita argentina).
///
/// Endpoints utilizados:
/// - /v1/dolares/oficial - Dólar oficial (compra/venta)
/// - /v1/cotizaciones - Todas las cotizaciones (USD, EUR, BRL, CLP, UYU)
///
/// SEMÁNTICA DE LOS CAMPOS:
/// - compra/venta: ARS necesarios para comprar 1 unidad de la moneda
///
/// CONVERSIÓN A UnidadesPorUsd:
/// - Para ARS: usar venta del USD directamente (ej: 1530 significa 1530 ARS = 1 USD)
/// - Para otras monedas: calcular ratio vs USD (ej: si EUR=1763 y USD=1530, entonces EUR/USD = 1530/1763 = 0.868)
/// </summary>
public class DolarApiProvider : IExchangeRateProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DolarApiProvider> _logger;
    private const string BaseUrl = "https://dolarapi.com/v1/";

    // Monedas soportadas por DolarApi
    private static readonly string[] _supportedCurrencies = { "USD", "ARS", "EUR", "BRL", "CLP", "UYU" };

    public string ProviderCode => "DOLARAPI";
    public IReadOnlyList<string> SupportedCurrencies => _supportedCurrencies;

    public DolarApiProvider(HttpClient httpClient, ILogger<DolarApiProvider> logger)
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
            _logger.LogWarning("No hay monedas soportadas por DolarApi en la lista solicitada");
            return results;
        }

        try
        {
            // DolarApi solo devuelve cotizaciones actuales, no históricas
            // Para fechas pasadas, devolvemos error (debería usarse otro proveedor)
            if (fecha.Date != DateTime.Today)
            {
                _logger.LogInformation("DolarApi solo soporta cotizaciones del día actual, fecha solicitada: {Fecha}", fecha);
                return monedasToFetch.Select(m => new ExchangeRateResult
                {
                    CodigoMoneda = m,
                    Fecha = fecha,
                    Success = false,
                    ErrorMessage = "SOLO_COTIZACION_ACTUAL",
                    Proveedor = ProviderCode
                }).ToList();
            }

            // Obtener todas las cotizaciones
            var response = await _httpClient.GetAsync("cotizaciones", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Error al consultar DolarApi: {StatusCode}", response.StatusCode);
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
            var cotizaciones = JsonSerializer.Deserialize<List<DolarApiCotizacion>>(content);

            if (cotizaciones == null || !cotizaciones.Any())
            {
                _logger.LogWarning("DolarApi devolvió respuesta vacía");
                return monedasToFetch.Select(m => new ExchangeRateResult
                {
                    CodigoMoneda = m,
                    Fecha = fecha,
                    Success = false,
                    ErrorMessage = "RESPUESTA_VACIA",
                    Proveedor = ProviderCode
                }).ToList();
            }

            // Obtener cotización del USD (dólar oficial) para calcular ratios
            var usdCotizacion = cotizaciones.FirstOrDefault(c => c.Moneda == "USD" && c.Casa == "oficial");
            if (usdCotizacion == null)
            {
                _logger.LogError("No se encontró cotización del USD oficial en DolarApi");
                return monedasToFetch.Select(m => new ExchangeRateResult
                {
                    CodigoMoneda = m,
                    Fecha = fecha,
                    Success = false,
                    ErrorMessage = "USD_NO_ENCONTRADO",
                    Proveedor = ProviderCode
                }).ToList();
            }

            var usdVenta = usdCotizacion.Venta;
            _logger.LogInformation("DolarApi USD oficial venta: {Venta}", usdVenta);

            foreach (var moneda in monedasToFetch)
            {
                decimal unidadesPorUsd;

                if (moneda == "USD")
                {
                    unidadesPorUsd = 1.0m;
                }
                else if (moneda == "ARS")
                {
                    // Para ARS, usar directamente el valor del USD
                    unidadesPorUsd = usdVenta;
                }
                else
                {
                    // Para otras monedas, buscar su cotización y calcular ratio vs USD
                    var cotizacion = cotizaciones.FirstOrDefault(c => c.Moneda == moneda && c.Casa == "oficial");

                    if (cotizacion == null || cotizacion.Venta == 0)
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

                    // Si 1 EUR = 1763 ARS y 1 USD = 1530 ARS
                    // Entonces 1 USD = (1530/1763) EUR = 0.868 EUR
                    unidadesPorUsd = usdVenta / cotizacion.Venta;
                }

                results.Add(new ExchangeRateResult
                {
                    CodigoMoneda = moneda,
                    Fecha = fecha,
                    UnidadesPorUsd = Math.Round(unidadesPorUsd, 8),
                    Success = true,
                    Proveedor = ProviderCode
                });

                _logger.LogDebug("DolarApi {Moneda}: {UnidadesPorUsd} unidades por USD", moneda, unidadesPorUsd);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener cotizaciones de DolarApi");
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
            var response = await _httpClient.GetAsync("dolares/oficial", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    #region DolarApi Response Models

    private class DolarApiCotizacion
    {
        [JsonPropertyName("moneda")]
        public string Moneda { get; set; } = string.Empty;

        [JsonPropertyName("casa")]
        public string Casa { get; set; } = string.Empty;

        [JsonPropertyName("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [JsonPropertyName("compra")]
        public decimal Compra { get; set; }

        [JsonPropertyName("venta")]
        public decimal Venta { get; set; }

        [JsonPropertyName("fechaActualizacion")]
        public DateTime FechaActualizacion { get; set; }
    }

    #endregion
}
