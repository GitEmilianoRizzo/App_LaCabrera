namespace LaCabrera.Api.Services;

/// <summary>
/// Qué hacer con los tickets de Vinson cuyas líneas son todas negativas
/// (devoluciones / anulaciones). El POS los informa con importe 0 en la cabecera.
/// </summary>
public enum TratamientoTicketNegativo
{
    /// <summary>Estado REFUNDED, anulado (como hace el SP de ingesta) e importes de la cabecera del POS (0). Las líneas quedan anuladas.</summary>
    MarcarDevolucion,
    /// <summary>Ticket CLOSED con los importes de la cabecera del POS (0). Las líneas quedan como vienen.</summary>
    ImporteCero,
    /// <summary>Comportamiento anterior: el ticket resta la suma de sus líneas negativas.</summary>
    Restar
}

public class VinsonSettings
{
    public TratamientoTicketNegativo TratamientoTicketsNegativos { get; set; } = TratamientoTicketNegativo.MarcarDevolucion;
}

public record VinsonLineaCalculada(
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal ImporteBruto,
    decimal ImporteDescuento,
    decimal ImporteNeto,
    decimal ImporteImpuesto,
    bool EstaAnulado);

public record VinsonTicketCalculado(
    string Estado,
    bool EstaAnulado,
    bool EsDevolucion,
    string OrigenImportes,
    decimal ImporteBruto,
    decimal ImporteDescuento,
    decimal ImporteNeto,
    decimal ImporteImpuesto)
{
    /// <summary>En Vinson el neto ya viene sin IVA (montoNeto = base imponible).</summary>
    public decimal ImporteNetoSinImpuesto => ImporteNeto;
}

/// <summary>
/// Cálculo de importes de tickets y líneas de Vinson.
///
/// Vinson informa:
/// - Cabecera (GetTransactionsByDate): montoFinal = total CON IVA, montoNeto = total SIN IVA,
///   ambos después de descuentos. Es lo que coincide con lo que informa el franquiciado.
/// - Productos (GetSalesFullInforAndProducts): grossAmount / netAmount / discountAmount son
///   importes UNITARIOS (no vienen multiplicados por quantity) y netAmount es antes de descuento.
///   unitPrice no viene informado.
/// </summary>
public static class VinsonImportes
{
    public static VinsonLineaCalculada CalcularLinea(VinsonProduct producto)
    {
        var cantidad = producto.Quantity ?? 1;
        var brutoUnitario = producto.GrossAmount ?? 0;
        var netoUnitario = producto.NetAmount ?? 0;

        var bruto = cantidad * brutoUnitario;
        var netoAntesDescuento = cantidad * netoUnitario;

        // El descuento también es unitario; se acota al neto para no dejar líneas negativas
        // (hay descuentos de consumo interno que vienen mayores que el producto).
        var descuento = netoAntesDescuento > 0
            ? Math.Min(Math.Abs(producto.DiscountAmount ?? 0) * cantidad, netoAntesDescuento)
            : 0;
        var neto = netoAntesDescuento - descuento;

        // Impuesto proporcional a lo efectivamente vendido
        var impuesto = netoAntesDescuento == 0 ? 0 : (bruto - netoAntesDescuento) * neto / netoAntesDescuento;

        return new VinsonLineaCalculada(
            Cantidad: cantidad,
            PrecioUnitario: Math.Round(producto.UnitPrice is > 0 ? producto.UnitPrice.Value : brutoUnitario, 2),
            ImporteBruto: Math.Round(bruto, 2),
            ImporteDescuento: Math.Round(descuento, 2),
            ImporteNeto: Math.Round(neto, 2),
            ImporteImpuesto: Math.Round(impuesto, 2),
            EstaAnulado: string.Equals(producto.CanceledItem, "True", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Un ticket es devolución si todas sus líneas son negativas o cero y al menos una es negativa.</summary>
    public static bool EsDevolucion(IReadOnlyCollection<VinsonLineaCalculada> lineas) =>
        lineas.Count > 0 && lineas.All(l => l.ImporteBruto <= 0) && lineas.Any(l => l.ImporteBruto < 0);

    public static string MapearEstado(int? estado) => estado switch
    {
        // 0 = Open, 1..4 = variantes de cerrado, 5 = Voided (anulado real)
        0 => "OPEN",
        5 => "VOIDED",
        _ => "CLOSED"
    };

    public static VinsonTicketCalculado CalcularTicket(
        VinsonTransaccion transaccion,
        VinsonSalesFullInfo? detalle,
        IReadOnlyCollection<VinsonLineaCalculada> lineas,
        TratamientoTicketNegativo tratamiento)
    {
        var estado = MapearEstado(transaccion.Estado);
        var esDevolucion = EsDevolucion(lineas);
        var descuento = lineas.Sum(l => l.ImporteDescuento);

        decimal bruto, neto;
        string origen;

        if (esDevolucion && tratamiento == TratamientoTicketNegativo.Restar)
        {
            (bruto, neto, origen) = (lineas.Sum(l => l.ImporteBruto), lineas.Sum(l => l.ImporteNeto), "LINEAS");
        }
        else if (transaccion.MontoFinal.HasValue && transaccion.MontoNeto.HasValue)
        {
            // Fuente de verdad: la cabecera del POS (coincide con lo informado por el franquiciado)
            (bruto, neto, origen) = (transaccion.MontoFinal.Value, transaccion.MontoNeto.Value, "CABECERA");
        }
        else if (detalle?.TotalAmount != null && detalle.NetAmount != null)
        {
            (bruto, neto, origen) = (detalle.TotalAmount.Value, detalle.NetAmount.Value, "DETALLE");
        }
        else
        {
            (bruto, neto, origen) = (lineas.Sum(l => l.ImporteBruto), lineas.Sum(l => l.ImporteNeto), "LINEAS");
        }

        if (esDevolucion && tratamiento == TratamientoTicketNegativo.MarcarDevolucion)
            estado = "REFUNDED";

        bruto = Math.Round(bruto, 2);
        neto = Math.Round(neto, 2);

        return new VinsonTicketCalculado(
            Estado: estado,
            // Mismo criterio que el SP de ingesta: CANCELLED, VOIDED y REFUNDED quedan anulados
            EstaAnulado: estado is "VOIDED" or "CANCELLED" or "REFUNDED",
            EsDevolucion: esDevolucion,
            OrigenImportes: origen,
            ImporteBruto: bruto,
            ImporteDescuento: esDevolucion ? 0 : descuento,
            ImporteNeto: neto,
            ImporteImpuesto: bruto - neto);
    }
}
