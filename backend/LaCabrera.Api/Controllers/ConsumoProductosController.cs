using System.Text.Json;
using LaCabrera.Api.Services.Consumo;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace LaCabrera.Api.Controllers;

/// <summary>Consumo por producto canónico y zona: alimenta el mapa de consumo (sección "Mapa de Consumo").</summary>
[ApiController]
[Route("api/v1/dashboard/consumo-productos")]
[Produces("application/json")]
public class ConsumoProductosController : ControllerBase
{
    // Sin indentar: la respuesta trae miles de filas compactas.
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private readonly IConsumoProductosService _service;

    public ConsumoProductosController(IConsumoProductosService service) => _service = service;

    [HttpGet]
    [SwaggerOperation(Summary = "Consumo por producto canónico",
        Description = "Unidades, kg y venta (USD y ARS) por día, franquicia y producto canónico. Por defecto, los últimos 60 días con datos.")]
    public async Task<IActionResult> Get([FromQuery] DateTime? fechaDesde, [FromQuery] DateTime? fechaHasta, [FromQuery] int? franquiciaId)
    {
        var data = await _service.GetAsync(fechaDesde, fechaHasta, franquiciaId);
        return new JsonResult(data, Json);
    }
}
