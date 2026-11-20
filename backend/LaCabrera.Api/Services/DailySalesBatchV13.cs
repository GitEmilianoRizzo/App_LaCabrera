using LaCabrera.Api.Models.DTOs;

namespace LaCabrera.Api.Services;

/// <summary>
/// Reglas del contrato daily-batch v1.3 (T-380, Manila / Aquarius POS). Solo aplican cuando
/// schema_version = "1.3": los envíos v1.0-v1.2 (Toast, Drive, MICROS, Lima) siguen igual.
/// </summary>
public static class DailySalesBatchV13
{
    public const string SchemaVersion = "1.3";

    /// <summary>Tolerancia para la aritmética de una línea (redondeo a 2 decimales).</summary>
    public const decimal ToleranciaLinea = 0.05m;

    /// <summary>Tolerancia para sumas de líneas o de pagos contra la cabecera.</summary>
    public const decimal ToleranciaTicket = 0.10m;

    public static bool Es(DailySalesBatchRequest request) => request.SchemaVersion == SchemaVersion;

    /// <summary>
    /// v1.3: VOIDED/CANCELLED quedan anulados (no suman). REFUNDED NO queda anulado: trae importes
    /// negativos que restan, igual que los reembolsos de Ágora y como espera el Informe Ejecutivo.
    /// </summary>
    public static bool EstaAnulado(string status) => status is "VOIDED" or "CANCELLED";

    /// <summary>
    /// Neto sin impuesto informado o derivado. A diferencia de v1.2, un tax_amount = 0
    /// (ticket o línea exenta) también sirve para derivarlo.
    /// </summary>
    public static decimal? NetoSinImpuesto(bool? importesIncluyenImpuesto, decimal neto, decimal? netoSinImpuesto, decimal? impuesto)
    {
        if (netoSinImpuesto.HasValue) return netoSinImpuesto.Value;
        if (!impuesto.HasValue) return null;
        return importesIncluyenImpuesto == false ? neto : neto - impuesto.Value;
    }

    /// <summary>
    /// Convención interna (migración 033): el dashboard calcula la venta neta como
    /// COALESCE(ImporteNetoSinImpuesto, ImporteNeto) − ImporteServicio. En v1.3 el net_amount NO
    /// incluye el service charge, así que se le suma al guardar: el dashboard termina mostrando
    /// exactamente net_amount_excl_tax, sin restar el servicio dos veces.
    /// </summary>
    public static decimal? NetoSinImpuestoInterno(decimal? netoSinImpuesto, decimal? servicio)
        => netoSinImpuesto.HasValue ? netoSinImpuesto.Value + (servicio ?? 0) : null;

    /// <summary>
    /// Avisos de consistencia (no rechazan el ticket): aritmética de cada línea, suma de líneas contra
    /// la cabecera, suma de pagos contra total_paid_amount y neto sin impuesto faltante.
    /// </summary>
    public static List<BatchWarning> Consistencia(TicketDto ticket)
    {
        var avisos = new List<BatchWarning>();
        var anulado = EstaAnulado(ticket.Status);

        foreach (var item in ticket.Items)
        {
            var esperado = Math.Round(item.Quantity * item.UnitPrice, 2);
            if (Math.Abs(esperado - item.GrossAmount) > ToleranciaLinea)
            {
                avisos.Add(Aviso(ticket, item, "LINE_AMOUNT_MISMATCH",
                    $"line {item.LineId}: quantity × unit_price = {esperado:0.00} but gross_amount = {item.GrossAmount:0.00}"));
            }
            if (Math.Abs(item.GrossAmount - item.DiscountAmount - item.NetAmount) > ToleranciaLinea)
            {
                avisos.Add(Aviso(ticket, item, "LINE_AMOUNT_MISMATCH",
                    $"line {item.LineId}: gross_amount − discount_amount = {item.GrossAmount - item.DiscountAmount:0.00} but net_amount = {item.NetAmount:0.00}"));
            }
        }

        // Un ticket anulado conserva sus líneas (anuladas); en uno válido no cuentan las líneas anuladas
        var lineas = ticket.Items.Where(i => anulado || i.IsVoided != true).ToList();
        if (lineas.Count > 0 && ticket.Amounts != null)
        {
            CompararSuma(avisos, ticket, "gross_amount", lineas.Sum(i => i.GrossAmount), ticket.Amounts.GrossAmount);
            CompararSuma(avisos, ticket, "discount_amount", lineas.Sum(i => i.DiscountAmount), ticket.Amounts.DiscountAmount);
            CompararSuma(avisos, ticket, "net_amount", lineas.Sum(i => i.NetAmount), ticket.Amounts.NetAmount);
        }

        if (ticket.Amounts != null && !anulado && ticket.Status != "OPEN")
        {
            var totalPagado = TotalPagado(ticket.Amounts);
            var pagos = ticket.PaymentMethods ?? new List<PaymentMethodDto>();
            var sumaPagos = pagos.Sum(p => p.Amount);
            if (Math.Abs(sumaPagos - totalPagado) > ToleranciaTicket)
            {
                avisos.Add(new BatchWarning
                {
                    Code = "PAYMENTS_MISMATCH",
                    Message = pagos.Count == 0
                        ? $"ticket {ticket.TicketId}: no payment_methods but total_paid_amount = {totalPagado:0.00}"
                        : $"ticket {ticket.TicketId}: sum of payment_methods = {sumaPagos:0.00} but total_paid_amount = {totalPagado:0.00}",
                    TicketId = ticket.TicketId
                });
            }
        }

        if (ticket.Amounts != null && !ticket.Amounts.NetAmountExclTax.HasValue && !ticket.Amounts.TaxAmount.HasValue)
        {
            avisos.Add(new BatchWarning
            {
                Code = "MISSING_NET_EXCL_TAX",
                Message = $"ticket {ticket.TicketId}: amounts.net_amount_excl_tax and amounts.tax_amount are missing; net sales excluding tax cannot be determined",
                TicketId = ticket.TicketId
            });
        }

        return avisos;
    }

    /// <summary>total_paid_amount informado o, si falta, neto + servicio + propina (misma regla que la ingesta).</summary>
    public static decimal TotalPagado(TicketAmounts amounts)
        => amounts.TotalPaidAmount ?? (amounts.NetAmount + (amounts.ServiceChargeAmount ?? 0) + (amounts.TipAmount ?? 0));

    private static void CompararSuma(List<BatchWarning> avisos, TicketDto ticket, string campo, decimal sumaLineas, decimal cabecera)
    {
        if (Math.Abs(sumaLineas - cabecera) > ToleranciaTicket)
        {
            avisos.Add(new BatchWarning
            {
                Code = "TICKET_LINES_MISMATCH",
                Message = $"ticket {ticket.TicketId}: sum of items {campo} = {sumaLineas:0.00} but amounts.{campo} = {cabecera:0.00}",
                TicketId = ticket.TicketId
            });
        }
    }

    private static BatchWarning Aviso(TicketDto ticket, TicketItemDto item, string codigo, string mensaje)
        => new() { Code = codigo, Message = $"ticket {ticket.TicketId}: {mensaje}", TicketId = ticket.TicketId, LineId = item.LineId };
}
