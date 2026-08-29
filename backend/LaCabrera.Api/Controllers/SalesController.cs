using System.Text.Json;
using LaCabrera.Api.Models.DTOs;
using LaCabrera.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace LaCabrera.Api.Controllers;

[ApiController]
[Route("api/v1/sales")]
[Produces("application/json")]
public class SalesController : ControllerBase
{
    private readonly IIngestionService _ingestionService;
    private readonly IApiKeyService _apiKeyService;
    private readonly ILogger<SalesController> _logger;

    public SalesController(
        IIngestionService ingestionService,
        IApiKeyService apiKeyService,
        ILogger<SalesController> logger)
    {
        _ingestionService = ingestionService;
        _apiKeyService = apiKeyService;
        _logger = logger;
    }

    /// <summary>
    /// Procesa un batch diario de ventas de una franquicia
    /// </summary>
    /// <param name="request">Batch de ventas diarias</param>
    /// <returns>Resultado del procesamiento</returns>
    [HttpPost("daily-batch")]
    [SwaggerOperation(
        Summary = "Enviar batch diario de ventas",
        Description = "Procesa un batch completo de ventas del dia de una franquicia. Requiere API Key valida en header X-API-KEY.")]
    [SwaggerResponse(200, "Batch procesado exitosamente", typeof(BatchResponse))]
    [SwaggerResponse(400, "Error de validacion", typeof(BatchResponse))]
    [SwaggerResponse(401, "API Key invalida o no proporcionada")]
    [SwaggerResponse(409, "Batch duplicado", typeof(BatchResponse))]
    public async Task<ActionResult<BatchResponse>> ProcessDailyBatch([FromBody] DailySalesBatchRequest request)
    {
        // Validate API Key
        var apiKey = Request.Headers["X-API-KEY"].FirstOrDefault();
        var apiKeyInfo = await _apiKeyService.ValidateApiKeyAsync(apiKey);

        if (apiKeyInfo == null)
        {
            return Unauthorized(new { error = "API Key invalida o no proporcionada" });
        }

        // Validate franchise code matches API Key
        if (!apiKeyInfo.FranquiciaCodigo.Equals(request.BatchHeader.Franchise.FranchiseCode, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Mismatch entre API Key y franchise_code: {ApiKeyFranchise} vs {RequestFranchise}",
                apiKeyInfo.FranquiciaCodigo, request.BatchHeader.Franchise.FranchiseCode);

            return BadRequest(BatchResponse.Rejected(new List<BatchError>
            {
                new BatchError
                {
                    Code = "FRANCHISE_MISMATCH",
                    Message = "El franchise_code no coincide con la API Key proporcionada",
                    Field = "batch_header.franchise.franchise_code",
                    ReceivedValue = request.BatchHeader.Franchise.FranchiseCode
                }
            }));
        }

        // Get raw JSON for storage
        var rawJson = JsonSerializer.Serialize(request, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        });

        var result = await _ingestionService.ProcessBatchAsync(request, apiKeyInfo.FranquiciaId, rawJson);

        if (result.Status == "DUPLICATE")
        {
            return Conflict(result);
        }

        if (result.Status == "REJECTED")
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Consulta el estado de un batch
    /// </summary>
    /// <param name="batchId">ID del batch a consultar</param>
    /// <returns>Estado del batch</returns>
    [HttpGet("daily-batch/{batchId}/status")]
    [SwaggerOperation(
        Summary = "Consultar estado de batch",
        Description = "Obtiene el estado de procesamiento de un batch enviado anteriormente")]
    [SwaggerResponse(200, "Estado del batch", typeof(BatchStatusResponse))]
    [SwaggerResponse(401, "API Key invalida o no proporcionada")]
    [SwaggerResponse(404, "Batch no encontrado")]
    public async Task<ActionResult<BatchStatusResponse>> GetBatchStatus(string batchId)
    {
        var apiKey = Request.Headers["X-API-KEY"].FirstOrDefault();
        var apiKeyInfo = await _apiKeyService.ValidateApiKeyAsync(apiKey);

        if (apiKeyInfo == null)
        {
            return Unauthorized(new { error = "API Key invalida o no proporcionada" });
        }

        var status = await _ingestionService.GetBatchStatusAsync(batchId, apiKeyInfo.FranquiciaId);

        if (status == null)
        {
            return NotFound(new { error = "Batch no encontrado", batch_id = batchId });
        }

        return Ok(status);
    }
}
