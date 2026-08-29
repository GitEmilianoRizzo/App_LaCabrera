using System.Text.Json.Serialization;

namespace LaCabrera.Api.Models.DTOs;

/// <summary>
/// Request principal para ingesta de batch diario de ventas
/// </summary>
public class DailySalesBatchRequest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = string.Empty;

    [JsonPropertyName("batch_header")]
    public BatchHeader BatchHeader { get; set; } = new();

    [JsonPropertyName("tickets")]
    public List<TicketDto> Tickets { get; set; } = new();
}

public class BatchHeader
{
    [JsonPropertyName("batch_id")]
    public string BatchId { get; set; } = string.Empty;

    [JsonPropertyName("business_date")]
    public string BusinessDate { get; set; } = string.Empty;

    [JsonPropertyName("generated_at")]
    public string GeneratedAt { get; set; } = string.Empty;

    [JsonPropertyName("source_system")]
    public SourceSystem? SourceSystem { get; set; }

    [JsonPropertyName("franchise")]
    public FranchiseInfo Franchise { get; set; } = new();

    [JsonPropertyName("extraction_window")]
    public ExtractionWindow? ExtractionWindow { get; set; }

    [JsonPropertyName("upload_type")]
    public string UploadType { get; set; } = "FULL_DAY";

    [JsonPropertyName("control_totals")]
    public ControlTotals ControlTotals { get; set; } = new();

    /// <summary>
    /// v1.2: Indica si los importes en el batch incluyen impuesto.
    /// true = precios con IVA/tax incluido (España, Argentina)
    /// false = precios sin tax, tax se suma al final (EEUU)
    /// null = desconocido (default para v1.0/v1.1)
    /// </summary>
    [JsonPropertyName("amounts_include_tax")]
    public bool? AmountsIncludeTax { get; set; }
}

public class SourceSystem
{
    [JsonPropertyName("system_name")]
    public string? SystemName { get; set; }

    [JsonPropertyName("system_version")]
    public string? SystemVersion { get; set; }

    [JsonPropertyName("exported_by")]
    public string? ExportedBy { get; set; }
}

public class FranchiseInfo
{
    [JsonPropertyName("franchise_code")]
    public string FranchiseCode { get; set; } = string.Empty;

    [JsonPropertyName("franchise_name")]
    public string? FranchiseName { get; set; }

    [JsonPropertyName("economic_group_code")]
    public string? EconomicGroupCode { get; set; }

    [JsonPropertyName("economic_group_name")]
    public string? EconomicGroupName { get; set; }

    [JsonPropertyName("country")]
    public string? Country { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("timezone")]
    public string Timezone { get; set; } = string.Empty;

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = string.Empty;
}

public class ExtractionWindow
{
    [JsonPropertyName("from")]
    public string? From { get; set; }

    [JsonPropertyName("to")]
    public string? To { get; set; }
}

public class ControlTotals
{
    [JsonPropertyName("ticket_count")]
    public int TicketCount { get; set; }

    [JsonPropertyName("item_line_count")]
    public int ItemLineCount { get; set; }

    [JsonPropertyName("gross_sales_amount")]
    public decimal? GrossSalesAmount { get; set; }

    [JsonPropertyName("discount_amount")]
    public decimal? DiscountAmount { get; set; }

    [JsonPropertyName("net_sales_amount")]
    public decimal? NetSalesAmount { get; set; }

    [JsonPropertyName("tax_amount")]
    public decimal? TaxAmount { get; set; }

    [JsonPropertyName("service_charge_amount")]
    public decimal? ServiceChargeAmount { get; set; }

    [JsonPropertyName("tip_amount")]
    public decimal? TipAmount { get; set; }

    [JsonPropertyName("covers_total")]
    public int? CoversTotal { get; set; }

    [JsonPropertyName("cancelled_ticket_count")]
    public int? CancelledTicketCount { get; set; }
}

public class TicketDto
{
    [JsonPropertyName("ticket_id")]
    public string TicketId { get; set; } = string.Empty;

    [JsonPropertyName("ticket_number")]
    public string TicketNumber { get; set; } = string.Empty;

    [JsonPropertyName("external_order_id")]
    public string? ExternalOrderId { get; set; }

    [JsonPropertyName("fiscal_document")]
    public FiscalDocument? FiscalDocument { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("opened_at")]
    public string OpenedAt { get; set; } = string.Empty;

    [JsonPropertyName("closed_at")]
    public string? ClosedAt { get; set; }

    [JsonPropertyName("business_date")]
    public string BusinessDate { get; set; } = string.Empty;

    [JsonPropertyName("meal_period")]
    public string? MealPeriod { get; set; }

    /// <summary>
    /// v1.2: Informacion del turno operativo de caja.
    /// Nota: shift (turno de caja) y meal_period (almuerzo/cena) son conceptos distintos.
    /// </summary>
    [JsonPropertyName("shift")]
    public ShiftInfo? Shift { get; set; }

    [JsonPropertyName("table")]
    public TableInfo? Table { get; set; }

    [JsonPropertyName("waiter")]
    public WaiterInfo? Waiter { get; set; }

    [JsonPropertyName("covers")]
    public int? Covers { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = string.Empty;

    [JsonPropertyName("amounts")]
    public TicketAmounts Amounts { get; set; } = new();

    [JsonPropertyName("discounts")]
    public List<DiscountDto>? Discounts { get; set; }

    [JsonPropertyName("payment_methods")]
    public List<PaymentMethodDto>? PaymentMethods { get; set; }

    [JsonPropertyName("items")]
    public List<TicketItemDto> Items { get; set; } = new();
}

public class FiscalDocument
{
    [JsonPropertyName("document_type")]
    public string? DocumentType { get; set; }

    [JsonPropertyName("document_number")]
    public string? DocumentNumber { get; set; }
}

public class TableInfo
{
    [JsonPropertyName("table_number")]
    public string? TableNumber { get; set; }

    [JsonPropertyName("table_area")]
    public string? TableArea { get; set; }
}

public class WaiterInfo
{
    [JsonPropertyName("waiter_id")]
    public string? WaiterId { get; set; }

    [JsonPropertyName("waiter_name")]
    public string? WaiterName { get; set; }
}

/// <summary>
/// v1.2: Informacion del turno operativo de caja.
/// </summary>
public class ShiftInfo
{
    [JsonPropertyName("shift_id")]
    public string? ShiftId { get; set; }

    [JsonPropertyName("shift_name")]
    public string? ShiftName { get; set; }

    [JsonPropertyName("opened_at")]
    public string? OpenedAt { get; set; }

    [JsonPropertyName("closed_at")]
    public string? ClosedAt { get; set; }
}

public class TicketAmounts
{
    [JsonPropertyName("gross_amount")]
    public decimal GrossAmount { get; set; }

    [JsonPropertyName("discount_amount")]
    public decimal DiscountAmount { get; set; }

    [JsonPropertyName("net_amount")]
    public decimal NetAmount { get; set; }

    /// <summary>
    /// v1.2: Importe neto sin impuesto (comparable entre franquicias).
    /// Si el POS lo informa directamente, usarlo. Si no, se calcula en ingesta.
    /// </summary>
    [JsonPropertyName("net_amount_excl_tax")]
    public decimal? NetAmountExclTax { get; set; }

    [JsonPropertyName("tax_amount")]
    public decimal? TaxAmount { get; set; }

    [JsonPropertyName("service_charge_amount")]
    public decimal? ServiceChargeAmount { get; set; }

    [JsonPropertyName("tip_amount")]
    public decimal? TipAmount { get; set; }

    [JsonPropertyName("total_paid_amount")]
    public decimal? TotalPaidAmount { get; set; }
}

public class DiscountDto
{
    [JsonPropertyName("discount_code")]
    public string? DiscountCode { get; set; }

    [JsonPropertyName("discount_name")]
    public string? DiscountName { get; set; }

    [JsonPropertyName("discount_type")]
    public string? DiscountType { get; set; }

    [JsonPropertyName("discount_value")]
    public decimal? DiscountValue { get; set; }

    [JsonPropertyName("discount_amount")]
    public decimal DiscountAmount { get; set; }
}

public class PaymentMethodDto
{
    [JsonPropertyName("payment_method")]
    public string PaymentMethod { get; set; } = string.Empty;

    [JsonPropertyName("payment_brand")]
    public string? PaymentBrand { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }
}

public class TicketItemDto
{
    [JsonPropertyName("line_id")]
    public string LineId { get; set; } = string.Empty;

    [JsonPropertyName("product_code")]
    public string ProductCode { get; set; } = string.Empty;

    [JsonPropertyName("product_name")]
    public string ProductName { get; set; } = string.Empty;

    [JsonPropertyName("product_category")]
    public string ProductCategory { get; set; } = string.Empty;

    [JsonPropertyName("product_family")]
    public string? ProductFamily { get; set; }

    [JsonPropertyName("product_subfamily")]
    public string? ProductSubfamily { get; set; }

    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }

    [JsonPropertyName("unit_price")]
    public decimal UnitPrice { get; set; }

    [JsonPropertyName("gross_amount")]
    public decimal GrossAmount { get; set; }

    [JsonPropertyName("discount_amount")]
    public decimal DiscountAmount { get; set; }

    [JsonPropertyName("net_amount")]
    public decimal NetAmount { get; set; }

    /// <summary>
    /// v1.2: Importe neto sin impuesto para esta linea.
    /// </summary>
    [JsonPropertyName("net_amount_excl_tax")]
    public decimal? NetAmountExclTax { get; set; }

    [JsonPropertyName("tax_amount")]
    public decimal? TaxAmount { get; set; }

    /// <summary>
    /// v1.2: Alicuota de impuesto aplicada a esta linea (decimal, ej: 0.21 para 21%).
    /// </summary>
    [JsonPropertyName("tax_rate")]
    public decimal? TaxRate { get; set; }

    [JsonPropertyName("is_discounted")]
    public bool? IsDiscounted { get; set; }

    [JsonPropertyName("is_voided")]
    public bool? IsVoided { get; set; }

    [JsonPropertyName("void_reason")]
    public string? VoidReason { get; set; }

    [JsonPropertyName("ordered_at")]
    public string? OrderedAt { get; set; }

    [JsonPropertyName("served_at")]
    public string? ServedAt { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}
