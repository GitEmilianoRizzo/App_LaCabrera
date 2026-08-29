namespace LaCabrera.Api.Models;

/// <summary>
/// Representa una tasa de cambio diaria.
/// DEFINICIÓN CRÍTICA: UnidadesPorUsd = cuántas unidades de esa moneda equivalen a 1 USD
/// Por lo tanto: ImporteUsd = ImporteLocal / UnidadesPorUsd
/// </summary>
public class ExchangeRate
{
    public int TipoCambioId { get; set; }
    public string CodigoMoneda { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public DateTime FechaCotizacionOrigen { get; set; }
    public decimal UnidadesPorUsd { get; set; }
    public string TipoTasa { get; set; } = "OFICIAL";
    public string Proveedor { get; set; } = string.Empty;
    public bool EsArrastrada { get; set; }
    public bool EsOverrideManual { get; set; }
    public string? ObservacionOverride { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime FechaModificacion { get; set; }
}

/// <summary>
/// Resultado de una consulta al proveedor de tipo de cambio
/// </summary>
public class ExchangeRateResult
{
    public string CodigoMoneda { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public decimal UnidadesPorUsd { get; set; }
    public string Proveedor { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Log de ejecución del job de tipo de cambio
/// </summary>
public class ExchangeRateIngestLog
{
    public int IngestaLogId { get; set; }
    public DateTime FechaEjecucion { get; set; }
    public string Proveedor { get; set; } = string.Empty;
    public DateTime FechaDesde { get; set; }
    public DateTime FechaHasta { get; set; }
    public string? MonedasConsultadas { get; set; }
    public string Estado { get; set; } = string.Empty; // OK, PARCIAL, ERROR
    public int MonedasObtenidas { get; set; }
    public int DiasObtenidos { get; set; }
    public int DiasArrastrados { get; set; }
    public string? MensajeError { get; set; }
    public string? MonedasFaltantes { get; set; }
    public int? DuracionMs { get; set; }
    public int RequestsRealizados { get; set; }
}

/// <summary>
/// Configuración de proveedor de tipo de cambio
/// </summary>
public class ExchangeRateProvider
{
    public int ProveedorId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public int Prioridad { get; set; }
    public string? MonedasSoportadas { get; set; }
    public bool RequiereApiKey { get; set; }
    public string? ApiKeyConfigName { get; set; }
}

/// <summary>
/// Moneda del sistema
/// </summary>
public class Currency
{
    public int MonedaId { get; set; }
    public string CodigoISO { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Simbolo { get; set; }
    public bool EsMonedaBase { get; set; }
    public bool Activo { get; set; } = true;
    public int? ProveedorPreferidoId { get; set; }
    public string? ProveedorPreferidoCodigo { get; set; }
}
