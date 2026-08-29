using LaCabrera.Api.Models;
using LaCabrera.Api.Repositories;
using LaCabrera.Api.Services.ExchangeRate;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System.Text.Json.Serialization;

namespace LaCabrera.Api.Controllers;

[ApiController]
[Route("api/v1/fx")]
[Produces("application/json")]
public class ExchangeRateController : ControllerBase
{
    private readonly IExchangeRateService _exchangeRateService;
    private readonly IExchangeRateRepository _repository;
    private readonly ILogger<ExchangeRateController> _logger;

    public ExchangeRateController(
        IExchangeRateService exchangeRateService,
        IExchangeRateRepository repository,
        ILogger<ExchangeRateController> logger)
    {
        _exchangeRateService = exchangeRateService;
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene las tasas de cambio para un rango de fechas
    /// </summary>
    [HttpGet("rates")]
    [SwaggerOperation(
        Summary = "Consultar tasas de cambio",
        Description = "Obtiene las tasas de cambio almacenadas para un rango de fechas y opcionalmente una moneda específica")]
    [SwaggerResponse(200, "Tasas de cambio encontradas", typeof(IEnumerable<ExchangeRateDto>))]
    public async Task<ActionResult<IEnumerable<ExchangeRateDto>>> GetRates(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? currency)
    {
        var fechaDesde = from ?? DateTime.Today.AddDays(-30);
        var fechaHasta = to ?? DateTime.Today;

        var rates = await _exchangeRateService.GetRatesAsync(fechaDesde, fechaHasta, currency);

        var result = rates.Select(r => new ExchangeRateDto
        {
            CodigoMoneda = r.CodigoMoneda,
            Fecha = r.Fecha,
            FechaCotizacionOrigen = r.FechaCotizacionOrigen,
            UnidadesPorUsd = r.UnidadesPorUsd,
            TipoTasa = r.TipoTasa,
            Proveedor = r.Proveedor,
            EsArrastrada = r.EsArrastrada,
            EsOverrideManual = r.EsOverrideManual,
            ObservacionOverride = r.ObservacionOverride
        });

        return Ok(result);
    }

    /// <summary>
    /// Obtiene la tasa de cambio para una moneda y fecha específica
    /// </summary>
    [HttpGet("rates/{currency}/{date}")]
    [SwaggerOperation(
        Summary = "Consultar tasa específica",
        Description = "Obtiene la tasa de cambio para una moneda y fecha específica")]
    [SwaggerResponse(200, "Tasa encontrada", typeof(ExchangeRateDto))]
    [SwaggerResponse(404, "Tasa no encontrada")]
    public async Task<ActionResult<ExchangeRateDto>> GetRate(string currency, DateTime date)
    {
        var rate = await _exchangeRateService.GetRateAsync(currency.ToUpperInvariant(), date);

        if (rate == null)
        {
            return NotFound(new { message = $"No hay tasa para {currency} en {date:yyyy-MM-dd}" });
        }

        return Ok(new ExchangeRateDto
        {
            CodigoMoneda = rate.CodigoMoneda,
            Fecha = rate.Fecha,
            FechaCotizacionOrigen = rate.FechaCotizacionOrigen,
            UnidadesPorUsd = rate.UnidadesPorUsd,
            TipoTasa = rate.TipoTasa,
            Proveedor = rate.Proveedor,
            EsArrastrada = rate.EsArrastrada,
            EsOverrideManual = rate.EsOverrideManual,
            ObservacionOverride = rate.ObservacionOverride
        });
    }

    /// <summary>
    /// Fuerza la recarga de tasas de cambio para una fecha
    /// </summary>
    [HttpPost("rates/refresh")]
    [SwaggerOperation(
        Summary = "Refrescar tasas",
        Description = "Fuerza la recarga de tasas de cambio desde los proveedores. Si no se especifica fecha, recarga los últimos 7 días.")]
    [SwaggerResponse(200, "Refresh completado", typeof(RefreshResultDto))]
    public async Task<ActionResult<RefreshResultDto>> RefreshRates([FromBody] RefreshRatesRequest? request)
    {
        var fechaDesde = request?.FechaDesde ?? DateTime.Today.AddDays(-7);
        var fechaHasta = request?.FechaHasta ?? DateTime.Today;

        _logger.LogInformation(
            "Refresh manual de tasas solicitado: {FechaDesde} a {FechaHasta}",
            fechaDesde, fechaHasta);

        var log = await _exchangeRateService.RefreshRatesAsync(fechaDesde, fechaHasta);

        return Ok(new RefreshResultDto
        {
            Estado = log.Estado,
            MonedasObtenidas = log.MonedasObtenidas,
            DiasObtenidos = log.DiasObtenidos,
            DiasArrastrados = log.DiasArrastrados,
            MonedasFaltantes = log.MonedasFaltantes,
            MensajeError = log.MensajeError,
            DuracionMs = log.DuracionMs
        });
    }

    /// <summary>
    /// Aplica un override manual para una tasa de cambio
    /// </summary>
    [HttpPut("rates/override")]
    [SwaggerOperation(
        Summary = "Override manual de tasa",
        Description = "Aplica una tasa de cambio manual. Requiere observación obligatoria explicando el motivo.")]
    [SwaggerResponse(200, "Override aplicado")]
    [SwaggerResponse(400, "Datos inválidos")]
    public async Task<ActionResult> ApplyOverride([FromBody] OverrideRateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Observacion))
        {
            return BadRequest(new { message = "El campo 'observacion' es obligatorio para override manual" });
        }

        if (request.UnidadesPorUsd <= 0)
        {
            return BadRequest(new { message = "UnidadesPorUsd debe ser mayor a 0" });
        }

        _logger.LogWarning(
            "Override manual solicitado: {Moneda} {Fecha} = {Tasa}. Motivo: {Observacion}",
            request.CodigoMoneda, request.Fecha, request.UnidadesPorUsd, request.Observacion);

        await _exchangeRateService.ApplyOverrideAsync(
            request.CodigoMoneda,
            request.Fecha,
            request.UnidadesPorUsd,
            request.Observacion);

        return Ok(new { message = "Override aplicado correctamente" });
    }

    /// <summary>
    /// Verifica el estado de los proveedores de tipo de cambio
    /// </summary>
    [HttpGet("health")]
    [SwaggerOperation(
        Summary = "Estado de proveedores FX",
        Description = "Verifica la disponibilidad de cada proveedor de tipo de cambio")]
    [SwaggerResponse(200, "Estado de proveedores", typeof(Dictionary<string, bool>))]
    public async Task<ActionResult<Dictionary<string, bool>>> HealthCheck()
    {
        var results = await _exchangeRateService.HealthCheckProvidersAsync();
        return Ok(results);
    }

    /// <summary>
    /// Obtiene los logs recientes de ingesta de tasas
    /// </summary>
    [HttpGet("logs")]
    [SwaggerOperation(
        Summary = "Logs de ingesta FX",
        Description = "Obtiene los logs más recientes de ejecución del job de tipo de cambio")]
    [SwaggerResponse(200, "Logs encontrados", typeof(IEnumerable<ExchangeRateIngestLog>))]
    public async Task<ActionResult<IEnumerable<ExchangeRateIngestLog>>> GetLogs([FromQuery] int count = 10)
    {
        var logs = await _exchangeRateService.GetRecentLogsAsync(count);
        return Ok(logs);
    }

    #region Currencies

    /// <summary>
    /// Obtiene todas las monedas del sistema
    /// </summary>
    [HttpGet("currencies")]
    [SwaggerOperation(
        Summary = "Listar monedas",
        Description = "Obtiene todas las monedas configuradas en el sistema")]
    [SwaggerResponse(200, "Monedas encontradas", typeof(IEnumerable<CurrencyDto>))]
    public async Task<ActionResult<IEnumerable<CurrencyDto>>> GetCurrencies()
    {
        var currencies = await _repository.GetCurrenciesAsync();
        var result = currencies.Select(c => new CurrencyDto
        {
            CodigoISO = c.CodigoISO.Trim(),
            Nombre = c.Nombre,
            Simbolo = c.Simbolo,
            EsMonedaBase = c.EsMonedaBase,
            Activo = c.Activo,
            ProveedorPreferidoId = c.ProveedorPreferidoId,
            ProveedorPreferidoCodigo = c.ProveedorPreferidoCodigo
        });
        return Ok(result);
    }

    /// <summary>
    /// Obtiene una moneda específica
    /// </summary>
    [HttpGet("currencies/{code}")]
    [SwaggerOperation(
        Summary = "Obtener moneda",
        Description = "Obtiene los detalles de una moneda específica")]
    [SwaggerResponse(200, "Moneda encontrada", typeof(CurrencyDto))]
    [SwaggerResponse(404, "Moneda no encontrada")]
    public async Task<ActionResult<CurrencyDto>> GetCurrency(string code)
    {
        var currency = await _repository.GetCurrencyAsync(code);
        if (currency == null)
        {
            return NotFound(new { message = $"Moneda {code} no encontrada" });
        }

        return Ok(new CurrencyDto
        {
            CodigoISO = currency.CodigoISO.Trim(),
            Nombre = currency.Nombre,
            Simbolo = currency.Simbolo,
            EsMonedaBase = currency.EsMonedaBase,
            Activo = currency.Activo,
            ProveedorPreferidoId = currency.ProveedorPreferidoId,
            ProveedorPreferidoCodigo = currency.ProveedorPreferidoCodigo
        });
    }

    /// <summary>
    /// Agrega una nueva moneda al sistema
    /// </summary>
    [HttpPost("currencies")]
    [SwaggerOperation(
        Summary = "Agregar moneda",
        Description = "Agrega una nueva moneda al sistema")]
    [SwaggerResponse(201, "Moneda creada")]
    [SwaggerResponse(400, "Datos inválidos")]
    public async Task<ActionResult> AddCurrency([FromBody] CreateCurrencyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CodigoISO) || request.CodigoISO.Length != 3)
        {
            return BadRequest(new { message = "Código ISO debe tener 3 caracteres" });
        }

        var existing = await _repository.GetCurrencyAsync(request.CodigoISO);
        if (existing != null)
        {
            return BadRequest(new { message = $"Ya existe una moneda con código {request.CodigoISO}" });
        }

        var currency = new Currency
        {
            CodigoISO = request.CodigoISO.ToUpperInvariant(),
            Nombre = request.Nombre,
            Simbolo = request.Simbolo,
            Activo = request.Activo,
            ProveedorPreferidoId = request.ProveedorPreferidoId
        };

        var id = await _repository.AddCurrencyAsync(currency);
        _logger.LogInformation("Moneda {CodigoISO} creada con ID {Id}", request.CodigoISO, id);

        return CreatedAtAction(nameof(GetCurrency), new { code = request.CodigoISO }, new { id, codigo = request.CodigoISO });
    }

    /// <summary>
    /// Actualiza una moneda existente
    /// </summary>
    [HttpPut("currencies/{code}")]
    [SwaggerOperation(
        Summary = "Actualizar moneda",
        Description = "Actualiza los datos de una moneda existente")]
    [SwaggerResponse(200, "Moneda actualizada")]
    [SwaggerResponse(404, "Moneda no encontrada")]
    public async Task<ActionResult> UpdateCurrency(string code, [FromBody] UpdateCurrencyRequest request)
    {
        var existing = await _repository.GetCurrencyAsync(code);
        if (existing == null)
        {
            return NotFound(new { message = $"Moneda {code} no encontrada" });
        }

        existing.Nombre = request.Nombre ?? existing.Nombre;
        existing.Simbolo = request.Simbolo ?? existing.Simbolo;
        existing.Activo = request.Activo ?? existing.Activo;
        existing.ProveedorPreferidoId = request.ProveedorPreferidoId;

        await _repository.UpdateCurrencyAsync(existing);
        _logger.LogInformation("Moneda {CodigoISO} actualizada. Proveedor preferido: {Proveedor}",
            code, request.ProveedorPreferidoId);

        return Ok(new { message = "Moneda actualizada correctamente" });
    }

    /// <summary>
    /// Elimina una moneda y sus tasas de cambio
    /// </summary>
    [HttpDelete("currencies/{code}")]
    [SwaggerOperation(
        Summary = "Eliminar moneda",
        Description = "Elimina una moneda y todas sus tasas de cambio históricas")]
    [SwaggerResponse(200, "Moneda eliminada")]
    [SwaggerResponse(400, "No se puede eliminar la moneda base")]
    [SwaggerResponse(404, "Moneda no encontrada")]
    public async Task<ActionResult> DeleteCurrency(string code)
    {
        var existing = await _repository.GetCurrencyAsync(code);
        if (existing == null)
        {
            return NotFound(new { message = $"Moneda {code} no encontrada" });
        }

        if (existing.EsMonedaBase)
        {
            return BadRequest(new { message = "No se puede eliminar la moneda base del sistema" });
        }

        // Eliminar tasas de cambio asociadas
        await _repository.DeleteRatesForCurrencyAsync(code);
        await _repository.DeleteCurrencyAsync(code);

        _logger.LogWarning("Moneda {CodigoISO} eliminada junto con sus tasas de cambio", code);

        return Ok(new { message = $"Moneda {code} eliminada correctamente" });
    }

    #endregion

    #region Providers

    /// <summary>
    /// Obtiene todos los proveedores de tipo de cambio
    /// </summary>
    [HttpGet("providers")]
    [SwaggerOperation(
        Summary = "Listar proveedores FX",
        Description = "Obtiene todos los proveedores de tipo de cambio activos")]
    [SwaggerResponse(200, "Proveedores encontrados", typeof(IEnumerable<ProviderDto>))]
    public async Task<ActionResult<IEnumerable<ProviderDto>>> GetProviders()
    {
        var providers = await _repository.GetProvidersAsync();
        var result = providers.Select(p => new ProviderDto
        {
            ProveedorId = p.ProveedorId,
            Codigo = p.Codigo,
            Nombre = p.Nombre,
            Prioridad = p.Prioridad,
            MonedasSoportadas = p.MonedasSoportadas?.Split(',').Select(m => m.Trim()).ToList() ?? new List<string>(),
            Activo = p.Activo
        });
        return Ok(result);
    }

    #endregion
}

#region DTOs

public class ExchangeRateDto
{
    [JsonPropertyName("codigo_moneda")]
    public string CodigoMoneda { get; set; } = string.Empty;

    [JsonPropertyName("fecha")]
    public DateTime Fecha { get; set; }

    [JsonPropertyName("fecha_cotizacion_origen")]
    public DateTime FechaCotizacionOrigen { get; set; }

    [JsonPropertyName("unidades_por_usd")]
    public decimal UnidadesPorUsd { get; set; }

    [JsonPropertyName("tipo_tasa")]
    public string TipoTasa { get; set; } = string.Empty;

    [JsonPropertyName("proveedor")]
    public string Proveedor { get; set; } = string.Empty;

    [JsonPropertyName("es_arrastrada")]
    public bool EsArrastrada { get; set; }

    [JsonPropertyName("es_override_manual")]
    public bool EsOverrideManual { get; set; }

    [JsonPropertyName("observacion_override")]
    public string? ObservacionOverride { get; set; }
}

public class RefreshRatesRequest
{
    [JsonPropertyName("fecha_desde")]
    public DateTime? FechaDesde { get; set; }

    [JsonPropertyName("fecha_hasta")]
    public DateTime? FechaHasta { get; set; }
}

public class RefreshResultDto
{
    [JsonPropertyName("estado")]
    public string Estado { get; set; } = string.Empty;

    [JsonPropertyName("monedas_obtenidas")]
    public int MonedasObtenidas { get; set; }

    [JsonPropertyName("dias_obtenidos")]
    public int DiasObtenidos { get; set; }

    [JsonPropertyName("dias_arrastrados")]
    public int DiasArrastrados { get; set; }

    [JsonPropertyName("monedas_faltantes")]
    public string? MonedasFaltantes { get; set; }

    [JsonPropertyName("mensaje_error")]
    public string? MensajeError { get; set; }

    [JsonPropertyName("duracion_ms")]
    public int? DuracionMs { get; set; }
}

public class OverrideRateRequest
{
    [JsonPropertyName("codigo_moneda")]
    public string CodigoMoneda { get; set; } = string.Empty;

    [JsonPropertyName("fecha")]
    public DateTime Fecha { get; set; }

    [JsonPropertyName("unidades_por_usd")]
    public decimal UnidadesPorUsd { get; set; }

    [JsonPropertyName("observacion")]
    public string Observacion { get; set; } = string.Empty;
}

public class CurrencyDto
{
    [JsonPropertyName("codigo_iso")]
    public string CodigoISO { get; set; } = string.Empty;

    [JsonPropertyName("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("simbolo")]
    public string? Simbolo { get; set; }

    [JsonPropertyName("es_moneda_base")]
    public bool EsMonedaBase { get; set; }

    [JsonPropertyName("activo")]
    public bool Activo { get; set; }

    [JsonPropertyName("proveedor_preferido_id")]
    public int? ProveedorPreferidoId { get; set; }

    [JsonPropertyName("proveedor_preferido_codigo")]
    public string? ProveedorPreferidoCodigo { get; set; }
}

public class CreateCurrencyRequest
{
    [JsonPropertyName("codigo_iso")]
    public string CodigoISO { get; set; } = string.Empty;

    [JsonPropertyName("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("simbolo")]
    public string? Simbolo { get; set; }

    [JsonPropertyName("activo")]
    public bool Activo { get; set; } = true;

    [JsonPropertyName("proveedor_preferido_id")]
    public int? ProveedorPreferidoId { get; set; }
}

public class UpdateCurrencyRequest
{
    [JsonPropertyName("nombre")]
    public string? Nombre { get; set; }

    [JsonPropertyName("simbolo")]
    public string? Simbolo { get; set; }

    [JsonPropertyName("activo")]
    public bool? Activo { get; set; }

    [JsonPropertyName("proveedor_preferido_id")]
    public int? ProveedorPreferidoId { get; set; }
}

public class ProviderDto
{
    [JsonPropertyName("proveedor_id")]
    public int ProveedorId { get; set; }

    [JsonPropertyName("codigo")]
    public string Codigo { get; set; } = string.Empty;

    [JsonPropertyName("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("prioridad")]
    public int Prioridad { get; set; }

    [JsonPropertyName("monedas_soportadas")]
    public List<string> MonedasSoportadas { get; set; } = new();

    [JsonPropertyName("activo")]
    public bool Activo { get; set; }
}

#endregion
