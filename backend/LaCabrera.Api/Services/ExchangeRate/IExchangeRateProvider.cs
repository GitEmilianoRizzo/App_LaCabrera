using LaCabrera.Api.Models;

namespace LaCabrera.Api.Services.ExchangeRate;

/// <summary>
/// Interface para proveedores de tipo de cambio (BCRA, OpenExchange, etc.)
/// </summary>
public interface IExchangeRateProvider
{
    /// <summary>
    /// Código único del proveedor (BCRA, EXCHANGERATE_HOST, etc.)
    /// </summary>
    string ProviderCode { get; }

    /// <summary>
    /// Monedas soportadas por este proveedor
    /// </summary>
    IReadOnlyList<string> SupportedCurrencies { get; }

    /// <summary>
    /// Verifica si el proveedor soporta una moneda específica
    /// </summary>
    bool SupportsCurrency(string currencyCode);

    /// <summary>
    /// Obtiene las tasas de cambio para una fecha específica
    /// </summary>
    /// <param name="fecha">Fecha para la cual obtener las tasas</param>
    /// <param name="monedas">Lista de monedas a consultar</param>
    /// <param name="cancellationToken">Token de cancelación</param>
    /// <returns>Lista de resultados de tasa de cambio</returns>
    Task<IReadOnlyList<ExchangeRateResult>> GetRatesAsync(
        DateTime fecha,
        IEnumerable<string> monedas,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica la salud/disponibilidad del proveedor
    /// </summary>
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
}
