using LaCabrera.Api.Models.DTOs;
using LaCabrera.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace LaCabrera.Api.Controllers;

[ApiController]
[Route("api/v1")]
[Produces("application/json")]
public class IntegrationController : ControllerBase
{
    private readonly IDashboardService _dashboardService;
    private readonly IWebHostEnvironment _environment;

    public IntegrationController(IDashboardService dashboardService, IWebHostEnvironment environment)
    {
        _dashboardService = dashboardService;
        _environment = environment;
    }

    /// <summary>
    /// Obtiene el JSON Schema para validacion de batches
    /// </summary>
    [HttpGet("integration/schema/daily-sales-batch")]
    [SwaggerOperation(Summary = "JSON Schema de batch", Description = "Retorna el JSON Schema para validar batches de ventas")]
    [SwaggerResponse(200, "JSON Schema")]
    public ActionResult GetSchema()
    {
        // Return a reference to the schema
        return Ok(new
        {
            schema_url = "https://lacabrera.com/schemas/daily-sales-batch/v1.0",
            version = "1.0",
            description = "JSON Schema para validacion de batches de ventas diarias",
            documentation = "/docs/integration/lacabrera_daily_sales_batch_contract.md"
        });
    }

    /// <summary>
    /// Obtiene estado de integracion de todas las franquicias
    /// </summary>
    [HttpGet("dashboard/integraciones/estado-franquicias")]
    [SwaggerOperation(Summary = "Estado de integracion", Description = "Estado de sincronizacion de cada franquicia con alertas")]
    [SwaggerResponse(200, "Estado de integracion", typeof(IEnumerable<EstadoIntegracionDto>))]
    public async Task<ActionResult<IEnumerable<EstadoIntegracionDto>>> GetEstadoIntegracion()
    {
        var data = await _dashboardService.GetEstadoIntegracionAsync();
        return Ok(data);
    }

    /// <summary>
    /// Health check de la API
    /// </summary>
    [HttpGet("/api/health")]
    [SwaggerOperation(Summary = "Health check", Description = "Verifica que la API esta funcionando")]
    public ActionResult HealthCheck()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            environment = _environment.EnvironmentName,
            version = "1.0.0"
        });
    }
}
