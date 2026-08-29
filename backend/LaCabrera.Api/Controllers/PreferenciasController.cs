using LaCabrera.Api.Repositories;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System.Text.Json.Serialization;

namespace LaCabrera.Api.Controllers;

[ApiController]
[Route("api/v1/preferencias")]
[Produces("application/json")]
public class PreferenciasController : ControllerBase
{
    private readonly IPreferenciasRepository _preferenciasRepository;

    public PreferenciasController(IPreferenciasRepository preferenciasRepository)
    {
        _preferenciasRepository = preferenciasRepository;
    }

    /// <summary>
    /// Obtiene las preferencias de columnas para una vista
    /// </summary>
    [HttpGet("columnas/{vistaId}")]
    [SwaggerOperation(Summary = "Obtener preferencias de columnas", Description = "Devuelve las columnas visibles y su orden para una vista")]
    [SwaggerResponse(200, "Preferencias encontradas", typeof(PreferenciaColumnasDto))]
    [SwaggerResponse(404, "No hay preferencias guardadas")]
    public async Task<ActionResult<PreferenciaColumnasDto>> GetPreferenciaColumnas(string vistaId)
    {
        var preferencia = await _preferenciasRepository.GetPreferenciaColumnasAsync(vistaId);

        if (preferencia == null)
        {
            return NotFound(new { message = "No hay preferencias guardadas para esta vista" });
        }

        return Ok(new PreferenciaColumnasDto
        {
            VistaId = preferencia.VistaId,
            ColumnasVisibles = preferencia.ColumnasVisibles.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
            OrdenColumnas = preferencia.OrdenColumnas?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
        });
    }

    /// <summary>
    /// Guarda las preferencias de columnas para una vista
    /// </summary>
    [HttpPut("columnas/{vistaId}")]
    [SwaggerOperation(Summary = "Guardar preferencias de columnas", Description = "Guarda las columnas visibles y su orden para una vista")]
    [SwaggerResponse(200, "Preferencias guardadas")]
    public async Task<ActionResult> SavePreferenciaColumnas(string vistaId, [FromBody] SavePreferenciaColumnasRequest request)
    {
        var columnasVisibles = string.Join(",", request.ColumnasVisibles);
        var ordenColumnas = request.OrdenColumnas != null ? string.Join(",", request.OrdenColumnas) : null;

        await _preferenciasRepository.UpsertPreferenciaColumnasAsync(vistaId, columnasVisibles, ordenColumnas);

        return Ok(new { message = "Preferencias guardadas correctamente" });
    }
}

public class PreferenciaColumnasDto
{
    [JsonPropertyName("vista_id")]
    public string VistaId { get; set; } = string.Empty;

    [JsonPropertyName("columnas_visibles")]
    public List<string> ColumnasVisibles { get; set; } = new();

    [JsonPropertyName("orden_columnas")]
    public List<string>? OrdenColumnas { get; set; }
}

public class SavePreferenciaColumnasRequest
{
    [JsonPropertyName("columnas_visibles")]
    public List<string> ColumnasVisibles { get; set; } = new();

    [JsonPropertyName("orden_columnas")]
    public List<string>? OrdenColumnas { get; set; }
}
