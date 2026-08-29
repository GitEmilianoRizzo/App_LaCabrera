using System.Text.Json.Serialization;

namespace LaCabrera.Api.Models.DTOs;

public class BatchResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("batch_id")]
    public string? BatchId { get; set; }

    [JsonPropertyName("ingestion_batch_id")]
    public long? IngestionBatchId { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public BatchSummary? Summary { get; set; }

    [JsonPropertyName("warnings")]
    public List<BatchWarning>? Warnings { get; set; }

    [JsonPropertyName("errors")]
    public List<BatchError>? Errors { get; set; }

    public static BatchResponse Accepted(string batchId, long ingestionBatchId, BatchSummary summary)
    {
        return new BatchResponse
        {
            Success = true,
            BatchId = batchId,
            IngestionBatchId = ingestionBatchId,
            Status = "ACCEPTED",
            Message = "Batch procesado exitosamente",
            Summary = summary
        };
    }

    public static BatchResponse AcceptedWithWarnings(string batchId, long ingestionBatchId, BatchSummary summary, List<BatchWarning> warnings)
    {
        return new BatchResponse
        {
            Success = true,
            BatchId = batchId,
            IngestionBatchId = ingestionBatchId,
            Status = "ACCEPTED_WITH_WARNINGS",
            Message = "Batch procesado con advertencias",
            Summary = summary,
            Warnings = warnings
        };
    }

    public static BatchResponse Rejected(List<BatchError> errors)
    {
        return new BatchResponse
        {
            Success = false,
            Status = "REJECTED",
            Message = "Batch rechazado por errores de validacion",
            Errors = errors
        };
    }

    public static BatchResponse Duplicate(string batchId, long existingIngestionBatchId)
    {
        return new BatchResponse
        {
            Success = false,
            BatchId = batchId,
            IngestionBatchId = existingIngestionBatchId,
            Status = "DUPLICATE",
            Message = "El batch ya fue procesado anteriormente"
        };
    }

    public static BatchResponse Failed(string message)
    {
        return new BatchResponse
        {
            Success = false,
            Status = "FAILED",
            Message = message
        };
    }
}

public class BatchSummary
{
    [JsonPropertyName("tickets_received")]
    public int TicketsReceived { get; set; }

    [JsonPropertyName("tickets_processed")]
    public int TicketsProcessed { get; set; }

    [JsonPropertyName("items_processed")]
    public int ItemsProcessed { get; set; }

    [JsonPropertyName("gross_amount")]
    public decimal GrossAmount { get; set; }

    [JsonPropertyName("net_amount")]
    public decimal NetAmount { get; set; }
}

public class BatchWarning
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("ticket_id")]
    public string? TicketId { get; set; }

    [JsonPropertyName("line_id")]
    public string? LineId { get; set; }
}

public class BatchError
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("field")]
    public string? Field { get; set; }

    [JsonPropertyName("received_value")]
    public string? ReceivedValue { get; set; }
}

public class BatchStatusResponse
{
    [JsonPropertyName("ingestion_batch_id")]
    public long IngestionBatchId { get; set; }

    [JsonPropertyName("batch_id")]
    public string BatchId { get; set; } = string.Empty;

    [JsonPropertyName("franchise_code")]
    public string FranchiseCode { get; set; } = string.Empty;

    [JsonPropertyName("franchise_name")]
    public string FranchiseName { get; set; } = string.Empty;

    [JsonPropertyName("business_date")]
    public string BusinessDate { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("error_count")]
    public int ErrorCount { get; set; }

    [JsonPropertyName("warning_count")]
    public int WarningCount { get; set; }

    [JsonPropertyName("tickets_received")]
    public int? TicketsReceived { get; set; }

    [JsonPropertyName("tickets_processed")]
    public int? TicketsProcessed { get; set; }

    [JsonPropertyName("received_at")]
    public DateTime ReceivedAt { get; set; }

    [JsonPropertyName("processed_at")]
    public DateTime? ProcessedAt { get; set; }

    [JsonPropertyName("processing_duration_seconds")]
    public int? ProcessingDurationSeconds { get; set; }
}
