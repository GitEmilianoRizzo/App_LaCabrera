using System.Text.Json.Serialization;

namespace LaCabrera.Api.Models.DTOs;

/// <summary>
/// DTO para parser registrado en base de datos
/// </summary>
public class ParserDto
{
    [JsonPropertyName("parser_id")]
    public int ParserId { get; set; }

    [JsonPropertyName("codigo")]
    public string Codigo { get; set; } = string.Empty;

    [JsonPropertyName("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("descripcion")]
    public string? Descripcion { get; set; }

    [JsonPropertyName("archivo_script")]
    public string ArchivoScript { get; set; } = string.Empty;

    [JsonPropertyName("extensiones_permitidas")]
    public string ExtensionesPermitidas { get; set; } = ".txt";

    [JsonPropertyName("paises_aplica")]
    public string? PaisesAplica { get; set; }

    [JsonPropertyName("moneda_default")]
    public string MonedaDefault { get; set; } = "USD";

    [JsonPropertyName("timezone_default")]
    public string TimezoneDefault { get; set; } = "America/New_York";

    [JsonPropertyName("activo")]
    public bool Activo { get; set; }
}

/// <summary>
/// Preview de archivo parseado
/// </summary>
public class ParsePreviewDto
{
    [JsonPropertyName("franchise_code")]
    public string? FranchiseCode { get; set; }

    [JsonPropertyName("franchise_name")]
    public string? FranchiseName { get; set; }

    [JsonPropertyName("business_date")]
    public string? BusinessDate { get; set; }

    [JsonPropertyName("ticket_count")]
    public int TicketCount { get; set; }

    [JsonPropertyName("net_sales_amount")]
    public decimal NetSalesAmount { get; set; }

    [JsonPropertyName("tax_amount")]
    public decimal TaxAmount { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "USD";
}

/// <summary>
/// Resultado de parsear un archivo
/// </summary>
public class ParseFileResultDto
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("filename")]
    public string Filename { get; set; } = string.Empty;

    [JsonPropertyName("preview")]
    public ParsePreviewDto? Preview { get; set; }

    [JsonPropertyName("data")]
    public object? Data { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

/// <summary>
/// Resultado de parsear multiples archivos
/// </summary>
public class ParseBatchResultDto
{
    [JsonPropertyName("total_files")]
    public int TotalFiles { get; set; }

    [JsonPropertyName("successful")]
    public int Successful { get; set; }

    [JsonPropertyName("failed")]
    public int Failed { get; set; }

    [JsonPropertyName("results")]
    public List<ParseFileResultDto> Results { get; set; } = new();
}

/// <summary>
/// Request para confirmar ingestion de archivos parseados
/// </summary>
public class IngestParsedRequest
{
    [JsonPropertyName("files")]
    public List<IngestFileDto> Files { get; set; } = new();
}

/// <summary>
/// Archivo a ingestar
/// </summary>
public class IngestFileDto
{
    [JsonPropertyName("filename")]
    public string Filename { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public object Data { get; set; } = new();
}

/// <summary>
/// Resultado de ingestion
/// </summary>
public class IngestResultDto
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("filename")]
    public string Filename { get; set; } = string.Empty;

    [JsonPropertyName("batch_id")]
    public string? BatchId { get; set; }

    [JsonPropertyName("tickets_processed")]
    public int TicketsProcessed { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

/// <summary>
/// Resultado de ingestion de multiples archivos
/// </summary>
public class IngestBatchResultDto
{
    [JsonPropertyName("total_files")]
    public int TotalFiles { get; set; }

    [JsonPropertyName("successful")]
    public int Successful { get; set; }

    [JsonPropertyName("failed")]
    public int Failed { get; set; }

    [JsonPropertyName("results")]
    public List<IngestResultDto> Results { get; set; } = new();
}
