using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LaCabrera.Api.Services;

/// <summary>
/// Cálculo de las ventas de NEO (NeoKernel, POS de La Cabrera Málaga) a partir de los endpoints planos de
/// /api/v1/b2b/sales (ticketheaders, ticketlines, ticketpayments, tickettaxes) más docs (comensales).
///
/// Reglas verificadas con la jornada 20261031 (T-381):
/// - ticketheaders.total = netAmount + taxAmount = venta CON IVA (EUR). netAmount es la venta sin IVA.
/// - ticketlines.amount = quantity × unitPrice (total de la línea, con IVA). Solo suman las líneas con affectAmount = 'S';
///   las 'N' (subtotales, componentes de pack) se guardan como detalle anulado para que no sumen.
/// - ticketpayments: affectAmount = 'S' suma a la venta (Σ = total; el vuelto en efectivo viene como pago negativo del
///   mismo medio); 'N' es propina.
/// - Σ tickettaxes.amount = taxAmount.
/// - El total de venta es el de la cabecera: las líneas se usan solo como control.
/// - Horas en formato "HHmmss" sobre la jornada (yyyyMMdd, hora de España).
/// - Comensales: docs[].buyerParty.totalClients.
/// </summary>
public static class NeoImportes
{
    public const decimal Tolerancia = 0.02m;

    public record LineaCalculada(
        string ExternalLineId, string CodigoProducto, string NombreProducto,
        string? Categoria, string? Familia, string? SubFamilia,
        decimal Cantidad, decimal PrecioUnitario, decimal ImporteBruto, decimal ImporteNeto, decimal ImporteImpuesto,
        decimal? TasaImpuesto, bool EstaAnulado, string? Notas);

    public record PagoCalculado(string Medio, decimal Importe);

    public record TicketCalculado(
        string ExternalTicketId, string NumeroTicket, string? TipoDocumentoFiscal, string? NumeroDocumentoFiscal,
        DateTime FechaNegocio, DateTime FechaApertura, DateTime? FechaCierre, string PeriodoComida,
        string? Mesa, string? Zona, string? Turno, string? Operador,
        decimal ImporteBruto, decimal ImporteNeto, decimal ImporteImpuesto, decimal ImportePropina, decimal ImporteTotalPagado,
        int? Cubiertos, bool EsDevolucion,
        IReadOnlyList<LineaCalculada> Lineas, IReadOnlyList<PagoCalculado> Pagos);

    /// <summary>Datos de catálogo para clasificar las líneas (product → familia/subfamilia).</summary>
    public record Catalogo(
        IReadOnlyDictionary<string, (string? Familia, string? SubFamilia)> Productos,
        IReadOnlyDictionary<string, string> Familias,
        IReadOnlyDictionary<string, string> SubFamilias)
    {
        public static readonly Catalogo Vacio = new(new Dictionary<string, (string?, string?)>(), new Dictionary<string, string>(), new Dictionary<string, string>());
    }

    public static List<TicketCalculado> CalcularTickets(NeoDia dia, Catalogo catalogo)
    {
        var jornada = ParseJornada(dia.Jornada);
        var lineas = dia.TicketLines.ToLookup(l => l.OrderId);
        var pagos = dia.TicketPayments.ToLookup(p => p.OrderId);
        var comensales = dia.Docs
            .Where(d => d.Order != null)
            .GroupBy(d => d.Order!)
            .ToDictionary(g => g.Key, g => g.First().TotalClients);

        var tickets = new List<TicketCalculado>();
        foreach (var h in dia.TicketHeaders)
        {
            var apertura = CombinarHora(jornada, h.HourStarted) ?? jornada;
            var cierre = CombinarHora(jornada, h.HourPayed);
            if (cierre < apertura) cierre = cierre.Value.AddDays(1); // cobrado pasada la medianoche

            var ps = pagos[h.OrderId].ToList();
            var pagosVenta = ps.Where(p => Afecta(p.AffectAmount))
                .GroupBy(p => string.IsNullOrWhiteSpace(p.Type) ? "SIN_TIPO" : p.Type!.Trim())
                .Select(g => new PagoCalculado(g.Key, Math.Round(g.Sum(p => p.Amount), 2)))
                .Where(p => p.Importe != 0)
                .ToList();
            var propina = ps.Where(p => !Afecta(p.AffectAmount)).Sum(p => p.Amount);

            var lineasCalc = lineas[h.OrderId].Select(l =>
            {
                var prod = (l.Product ?? "").Trim();
                catalogo.Productos.TryGetValue(prod, out var cat);
                var familia = cat.Familia != null && catalogo.Familias.TryGetValue(cat.Familia, out var f) ? f : null;
                var subFamilia = cat.SubFamilia != null && catalogo.SubFamilias.TryGetValue(cat.SubFamilia, out var s) ? s : null;
                var neto = l.NetAmount ?? (l.Tax is > 0 ? l.Amount / (1 + l.Tax.Value / 100m) : l.Amount);
                return new LineaCalculada(
                    ExternalLineId: l.Line ?? "",
                    CodigoProducto: prod.Length == 0 ? "SIN_CODIGO" : prod,
                    NombreProducto: string.IsNullOrWhiteSpace(l.ProductName) ? "Sin nombre" : l.ProductName!.Trim(),
                    Categoria: CategoriaDeFamilia(familia),
                    Familia: familia,
                    SubFamilia: subFamilia,
                    Cantidad: l.Quantity,
                    PrecioUnitario: Math.Round(l.UnitPrice, 4),
                    ImporteBruto: Math.Round(l.Amount, 2),
                    ImporteNeto: Math.Round(neto, 2),
                    ImporteImpuesto: Math.Round(l.Amount - neto, 2),
                    TasaImpuesto: l.Tax,
                    // Las líneas que no afectan al importe se guardan, pero marcadas para que no sumen
                    EstaAnulado: !Afecta(l.AffectAmount),
                    Notas: string.IsNullOrWhiteSpace(l.Notes) ? null : l.Notes!.Trim());
            }).ToList();

            tickets.Add(new TicketCalculado(
                ExternalTicketId: h.OrderId,
                NumeroTicket: string.IsNullOrWhiteSpace(h.InvoiceNumber) ? h.Id.ToString(CultureInfo.InvariantCulture) : h.InvoiceNumber!.Trim(),
                TipoDocumentoFiscal: h.DocType,
                NumeroDocumentoFiscal: string.Join(" ", new[] { h.Series, h.InvoiceNumber }.Where(x => !string.IsNullOrWhiteSpace(x))),
                FechaNegocio: jornada,
                FechaApertura: apertura,
                FechaCierre: cierre,
                PeriodoComida: PeriodoComida(apertura.Hour),
                Mesa: Vacio(h.Spot), Zona: Vacio(h.Zone), Turno: Vacio(h.Turn), Operador: Vacio(h.Operator),
                ImporteBruto: Math.Round(h.Total, 2),
                ImporteNeto: Math.Round(h.NetAmount, 2),
                ImporteImpuesto: Math.Round(h.TaxAmount, 2),
                ImportePropina: Math.Round(propina, 2),
                ImporteTotalPagado: pagosVenta.Sum(p => p.Importe),
                Cubiertos: comensales.TryGetValue(h.Id.ToString(CultureInfo.InvariantCulture), out var c) && c is > 0 ? c : null,
                EsDevolucion: h.Total < 0,
                Lineas: lineasCalc,
                Pagos: pagosVenta));
        }
        return tickets;
    }

    /// <summary>
    /// Controles de la Fase 3 sobre una jornada. Devuelve la lista de diferencias (vacía si todo cierra).
    /// </summary>
    public static List<string> Controlar(NeoDia dia)
    {
        var avisos = new List<string>();
        var lineas = dia.TicketLines.ToLookup(l => l.OrderId);
        var pagos = dia.TicketPayments.ToLookup(p => p.OrderId);
        var impuestos = dia.TicketTaxes.ToLookup(t => t.OrderId);

        foreach (var h in dia.TicketHeaders)
        {
            var ls = lineas[h.OrderId].ToList();
            var malMultiplicadas = ls.Count(l => Math.Abs(l.Quantity * l.UnitPrice - l.Amount) > Tolerancia);
            if (malMultiplicadas > 0) avisos.Add($"ticket {h.OrderId}: {malMultiplicadas} líneas con amount ≠ quantity × unitPrice");

            var sumaLineas = ls.Where(l => Afecta(l.AffectAmount)).Sum(l => l.Amount);
            if (Math.Abs(sumaLineas - h.Total) > Tolerancia) avisos.Add($"ticket {h.OrderId}: líneas {sumaLineas:0.00} vs total {h.Total:0.00}");

            var sumaPagos = pagos[h.OrderId].Where(p => Afecta(p.AffectAmount)).Sum(p => p.Amount);
            if (Math.Abs(sumaPagos - h.Total) > Tolerancia) avisos.Add($"ticket {h.OrderId}: pagos {sumaPagos:0.00} vs total {h.Total:0.00}");

            if (Math.Abs(h.NetAmount + h.TaxAmount - h.Total) > Tolerancia)
                avisos.Add($"ticket {h.OrderId}: neto + IVA {h.NetAmount + h.TaxAmount:0.00} vs total {h.Total:0.00}");

            var sumaImpuestos = impuestos[h.OrderId].Sum(t => t.Amount);
            if (Math.Abs(sumaImpuestos - h.TaxAmount) > Tolerancia)
                avisos.Add($"ticket {h.OrderId}: tickettaxes {sumaImpuestos:0.00} vs taxAmount {h.TaxAmount:0.00}");
        }

        var totalDia = dia.TicketHeaders.Sum(h => h.Total);
        if (dia.DailySummary is { } ds && Math.Abs(ds.TotalAmount - totalDia) > Tolerancia)
            avisos.Add($"jornada {dia.Jornada}: Σ tickets {totalDia:0.00} vs dailysummary {ds.TotalAmount:0.00}");
        return avisos;
    }

    /// <summary>
    /// Ventas (RV) del arqueo de caja. Puede no coincidir con Σ tickets si la caja se cerró antes del último cobro
    /// (en 20261031 un ticket de 67,50 se cobró después del arqueo): se informa, no corta la carga.
    /// </summary>
    public static decimal VentasArqueo(NeoDia dia) => dia.CashReports.Where(c => c.LineType == "RV").Sum(c => c.Amount);

    /// <summary>Categoría del modelo (la misma que usan los otros conectores) según la familia de NEO.</summary>
    public static string? CategoriaDeFamilia(string? familia)
    {
        if (string.IsNullOrWhiteSpace(familia)) return null;
        var f = familia.ToUpperInvariant();
        if (f.Contains("GRILL")) return "MAIN_COURSE";
        if (f.Contains("ENTRADA")) return "STARTER";
        if (f.Contains("GUARNICION") || f.Contains("ENSALADA") || f.Contains("PASTA")) return "SIDE_DISH";
        if (f.Contains("POSTRE")) return "DESSERT";
        if (f.Contains("COCKTAIL")) return "COCKTAIL";
        if (f.Contains("CAFE") || f.Contains("INFUSION")) return "COFFEE";
        if (f.Contains("V. ") || f.Contains("VINO") || f.Contains("CHAMPAGNE") || f.Contains("CAVA")) return "WINE";
        if (f.Contains("REFRESCO") || f.Contains("CERVEZA") || f.Contains("ALCOHOL") || f.Contains("BEBIDA")) return "BEVERAGE";
        return "OTHER";
    }

    public static DateTime ParseJornada(string jornada) =>
        DateTime.ParseExact(jornada, "yyyyMMdd", CultureInfo.InvariantCulture);

    public static string Jornada(DateTime dia) => dia.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

    /// <summary>"HHmmss" (formato hora Neo) sobre la jornada.</summary>
    public static DateTime? CombinarHora(DateTime jornada, string? hora)
    {
        if (string.IsNullOrWhiteSpace(hora)) return null;
        var h = hora.Trim().PadLeft(6, '0');
        return TimeSpan.TryParseExact(h, "hhmmss", CultureInfo.InvariantCulture, out var t) ? jornada.Date + t : null;
    }

    private static bool Afecta(string? affectAmount) => !string.Equals(affectAmount?.Trim(), "N", StringComparison.OrdinalIgnoreCase);

    private static string? Vacio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string PeriodoComida(int hora) => hora switch
    {
        >= 6 and < 12 => "BREAKFAST",
        >= 12 and < 17 => "LUNCH",
        >= 17 and < 21 => "DINNER",
        _ => "LATE_NIGHT"
    };

    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new TextoONumeroConverter() }
    };

    /// <summary>NEO manda algunos campos de texto a veces como número (p. ej. associatedLine): se leen como texto.</summary>
    public sealed class TextoONumeroConverter : JsonConverter<string>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.TryGetInt64(out var n) ? n.ToString(CultureInfo.InvariantCulture) : reader.GetDecimal().ToString(CultureInfo.InvariantCulture),
            JsonTokenType.True => "true",
            JsonTokenType.False => "false",
            JsonTokenType.Null => null,
            _ => JsonDocument.ParseValue(ref reader).RootElement.GetRawText()
        };

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) => writer.WriteStringValue(value);
    }
}

#region DTOs de la API de NEO (crudo guardado por jornada)

/// <summary>Lo que se guarda crudo por jornada (sin datos personales) y desde donde se reprocesa.</summary>
public class NeoDia
{
    [JsonPropertyName("shop")] public string Shop { get; set; } = "";
    [JsonPropertyName("jornada")] public string Jornada { get; set; } = "";
    [JsonPropertyName("ticketheaders")] public List<NeoTicketHeader> TicketHeaders { get; set; } = new();
    [JsonPropertyName("ticketlines")] public List<NeoTicketLine> TicketLines { get; set; } = new();
    [JsonPropertyName("ticketpayments")] public List<NeoTicketPayment> TicketPayments { get; set; } = new();
    [JsonPropertyName("tickettaxes")] public List<NeoTicketTax> TicketTaxes { get; set; } = new();
    [JsonPropertyName("cashreports")] public List<NeoCashReport> CashReports { get; set; } = new();
    [JsonPropertyName("dailysummary")] public NeoDailySummary? DailySummary { get; set; }
    /// <summary>De docs solo se guarda lo que se usa: número de pedido, estado y comensales.</summary>
    [JsonPropertyName("docs")] public List<NeoDocResumen> Docs { get; set; } = new();
}

public class NeoEnvelope<T>
{
    [JsonPropertyName("apiVersion")] public string? ApiVersion { get; set; }
    [JsonPropertyName("total")] public int? Total { get; set; }
    [JsonPropertyName("data")] public T? Data { get; set; }
    [JsonPropertyName("error")] public JsonElement? Error { get; set; }
}

public class NeoTicketHeader
{
    [JsonPropertyName("orderId")] public string OrderId { get; set; } = "";
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("series")] public string? Series { get; set; }
    [JsonPropertyName("invoiceNumber")] public string? InvoiceNumber { get; set; }
    [JsonPropertyName("docType")] public string? DocType { get; set; }
    [JsonPropertyName("businessSource")] public string? BusinessSource { get; set; }
    [JsonPropertyName("hourStarted")] public string? HourStarted { get; set; }
    [JsonPropertyName("hourPayed")] public string? HourPayed { get; set; }
    [JsonPropertyName("operator")] public string? Operator { get; set; }
    [JsonPropertyName("till")] public string? Till { get; set; }
    [JsonPropertyName("tillPayed")] public string? TillPayed { get; set; }
    [JsonPropertyName("turn")] public string? Turn { get; set; }
    [JsonPropertyName("zone")] public string? Zone { get; set; }
    [JsonPropertyName("spot")] public string? Spot { get; set; }
    [JsonPropertyName("tariff")] public string? Tariff { get; set; }
    [JsonPropertyName("customer")] public string? Customer { get; set; }
    [JsonPropertyName("currency")] public string? Currency { get; set; }
    [JsonPropertyName("netAmount")] public decimal NetAmount { get; set; }
    [JsonPropertyName("taxAmount")] public decimal TaxAmount { get; set; }
    [JsonPropertyName("total")] public decimal Total { get; set; }
}

public class NeoTicketLine
{
    [JsonPropertyName("orderId")] public string OrderId { get; set; } = "";
    [JsonPropertyName("line")] public string? Line { get; set; }
    [JsonPropertyName("product")] public string? Product { get; set; }
    [JsonPropertyName("productName")] public string? ProductName { get; set; }
    [JsonPropertyName("quantity")] public decimal Quantity { get; set; }
    [JsonPropertyName("unitPrice")] public decimal UnitPrice { get; set; }
    [JsonPropertyName("basePrice")] public decimal? BasePrice { get; set; }
    [JsonPropertyName("amount")] public decimal Amount { get; set; }
    [JsonPropertyName("netAmount")] public decimal? NetAmount { get; set; }
    [JsonPropertyName("tax")] public decimal? Tax { get; set; }
    [JsonPropertyName("taxRuleId")] public string? TaxRuleId { get; set; }
    [JsonPropertyName("discount")] public decimal? Discount { get; set; }
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("affectAmount")] public string? AffectAmount { get; set; }
    [JsonPropertyName("modifierLine")] public string? ModifierLine { get; set; }
    [JsonPropertyName("associatedLine")] public string? AssociatedLine { get; set; }
    [JsonPropertyName("operator")] public string? Operator { get; set; }
    [JsonPropertyName("notes")] public string? Notes { get; set; }
}

public class NeoTicketPayment
{
    [JsonPropertyName("orderId")] public string OrderId { get; set; } = "";
    [JsonPropertyName("line")] public string? Line { get; set; }
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("code")] public string? Code { get; set; }
    [JsonPropertyName("amount")] public decimal Amount { get; set; }
    [JsonPropertyName("currency")] public string? Currency { get; set; }
    [JsonPropertyName("affectAmount")] public string? AffectAmount { get; set; }
}

public class NeoTicketTax
{
    [JsonPropertyName("orderId")] public string OrderId { get; set; } = "";
    [JsonPropertyName("taxCodeId")] public string? TaxCodeId { get; set; }
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("percentage")] public decimal? Percentage { get; set; }
    [JsonPropertyName("netAmount")] public decimal NetAmount { get; set; }
    [JsonPropertyName("amount")] public decimal Amount { get; set; }
}

public class NeoCashReport
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("line")] public int Line { get; set; }
    [JsonPropertyName("lineType")] public string? LineType { get; set; }
    [JsonPropertyName("meanPayment")] public string? MeanPayment { get; set; }
    [JsonPropertyName("amount")] public decimal Amount { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("time")] public string? Time { get; set; }
    [JsonPropertyName("turn")] public string? Turn { get; set; }
    [JsonPropertyName("workingday")] public string? Workingday { get; set; }
}

public class NeoDailySummary
{
    [JsonPropertyName("code")] public string? Code { get; set; }
    [JsonPropertyName("date")] public string? Date { get; set; }
    [JsonPropertyName("baseAmount")] public decimal BaseAmount { get; set; }
    [JsonPropertyName("taxAmount")] public decimal TaxAmount { get; set; }
    [JsonPropertyName("totalAmount")] public decimal TotalAmount { get; set; }
}

public class NeoDocResumen
{
    [JsonPropertyName("order")] public string? Order { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("subType")] public string? SubType { get; set; }
    [JsonPropertyName("totalClients")] public int? TotalClients { get; set; }
}

/// <summary>Documento completo de /sales/docs: solo los campos que se leen.</summary>
public class NeoDoc
{
    [JsonPropertyName("order")] public string? Order { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("subType")] public string? SubType { get; set; }
    [JsonPropertyName("buyerParty")] public NeoBuyerParty? BuyerParty { get; set; }
}

public class NeoBuyerParty
{
    [JsonPropertyName("totalClients")] public int? TotalClients { get; set; }
}

public class NeoProducto
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("family")] public string? Family { get; set; }
    [JsonPropertyName("subfamily")] public string? Subfamily { get; set; }
}

public class NeoNombreIdioma
{
    [JsonPropertyName("language")] public string? Language { get; set; }
    [JsonPropertyName("text")] public string? Text { get; set; }
}

public class NeoFamilia
{
    [JsonPropertyName("code")] public string? Code { get; set; }
    [JsonPropertyName("name")] public List<NeoNombreIdioma>? Name { get; set; }
}

#endregion
