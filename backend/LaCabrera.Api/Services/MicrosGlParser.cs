using System.Globalization;
using LaCabrera.Api.Models.DTOs;

namespace LaCabrera.Api.Services;

/// <summary>
/// Convierte un archivo standard.GeneralLedger de Oracle MICROS Simphony (GL_{TIENDA}_{MMDDYY}.txt)
/// al formato daily_sales_batch v1.2, para cargarlo con IngestionService.
///
/// Layout: diccionario de Oracle E90896 (Standard Export User Guide, Release 1.22). La posición 0 es el
/// tipo de registro. Campos usados (verificados contra los archivos de Chile en T-371):
/// - GLID: [1] tienda, [3] fecha de negocio, [5]/[6] fecha y hora de generación, [9]/[10] sistema y versión.
/// - CHDR (cheque): [3] número, [4] fecha de negocio, [6]/[7] apertura/cierre (cierre vacío = abierto),
///   [8] empleado, [9] cubiertos, [11] total cobrado, [15] propina, [23] subtotal (con IVA y descuentos),
///   [48] Guest Check Number (id único), [50] mesa.
/// - CMI (ítem): [1] cheque, [3] nombre, [5] código, [9] familia, [13] subfamilia, [19] motivo de anulación,
///   [32] hora, [42] Detail Id, [43] Parent Detail Id (modificadores), [51] void, [52] return,
///   [77] Report Line Total, [78] Report Line Count, [80] IVA incluido (6 decimales).
/// - CDSC (descuento): [1] cheque, [3] nombre, [32] Parent Detail Id (= CMI[42]), [63] Report Line Total (negativo).
/// - CTND (pago): [1] cheque, [3] medio, [55] Report Line Total (el vuelto viene negativo).
/// - CVAT (IVA): [1] cheque, [18] impuesto.
///
/// Las anulaciones y devoluciones vienen como líneas negativas que compensan a la original. El formato de
/// ventas no admite cantidades negativas: se descuentan de la línea original del mismo producto y se
/// informan aparte como línea anulada (con su motivo), que los reportes de productos excluyen.
/// No se toman nombres de empleados ni datos de clientes.
/// </summary>
public static class MicrosGlParser
{
    public record Resultado(DailySalesBatchRequest Batch, string CodigoTienda, DateTime FechaNegocio, List<string> Avisos);

    /// <summary>Código de tienda (GLID[1]) sin parsear el archivo completo.</summary>
    public static string? LeerCodigoTienda(string contenido)
    {
        var primera = contenido.Split('\n', 2)[0].TrimEnd('\r');
        var campos = primera.Split('|');
        return campos.Length > 1 && campos[0] == "GLID" ? campos[1].Trim() : null;
    }

    public static Resultado Parsear(string contenido, string franchiseCode, string zonaHoraria, string moneda)
    {
        var registros = contenido.Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .Where(l => l.Length > 0)
            .Select(l => l.Split('|'))
            .ToLookup(c => c[0]);

        var glid = registros["GLID"].FirstOrDefault()
            ?? throw new FormatException("El archivo no tiene registro GLID: no es un export standard.GeneralLedger");
        var tienda = glid[1].Trim();
        var fechaNegocio = ParseFecha(glid[3]);
        var generado = DateTime.ParseExact(glid[5] + glid[6], "yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var avisos = new List<string>();

        var items = registros["CMI"].ToLookup(r => r[1]);
        var descuentos = registros["CDSC"].ToLookup(r => r[1]);
        var pagos = registros["CTND"].ToLookup(r => r[1]);
        var iva = registros["CVAT"].ToLookup(r => r[1]);

        var cheques = registros["CHDR"].ToList();
        var numeros = cheques.Select(c => c[3]).ToHashSet();
        foreach (var huerfano in descuentos.Select(g => g.Key).Where(k => !numeros.Contains(k)))
            avisos.Add($"Descuento del cheque {huerfano} sin cabecera: no se carga");

        var tickets = cheques.Select(c => ArmarTicket(c, items[c[3]], descuentos[c[3]], pagos[c[3]], iva[c[3]], moneda, avisos)).ToList();

        var batch = new DailySalesBatchRequest
        {
            SchemaVersion = "1.2",
            BatchHeader = new BatchHeader
            {
                // La fecha y hora de generación identifican el envío: un reenvío idéntico es duplicado
                BatchId = $"MICROS-{SoloLetrasYNumeros(tienda)}-{fechaNegocio:yyyyMMdd}-{generado:yyyyMMddHHmmss}",
                BusinessDate = fechaNegocio.ToString("yyyy-MM-dd"),
                GeneratedAt = generado.ToString("yyyy-MM-ddTHH:mm:ss"),
                SourceSystem = new SourceSystem { SystemName = glid.ElementAtOrDefault(9)?.Trim(), SystemVersion = glid.ElementAtOrDefault(10)?.Trim(), ExportedBy = "standard.GeneralLedger" },
                Franchise = new FranchiseInfo { FranchiseCode = franchiseCode, FranchiseName = glid[2].Trim(), Country = "Chile", Timezone = zonaHoraria, Currency = moneda },
                UploadType = "FULL_DAY",
                AmountsIncludeTax = true,
                ControlTotals = new ControlTotals
                {
                    TicketCount = tickets.Count,
                    ItemLineCount = tickets.Sum(t => t.Items.Count),
                    GrossSalesAmount = tickets.Sum(t => t.Amounts.GrossAmount),
                    DiscountAmount = tickets.Sum(t => t.Amounts.DiscountAmount),
                    NetSalesAmount = tickets.Sum(t => t.Amounts.NetAmount),
                    TaxAmount = tickets.Sum(t => t.Amounts.TaxAmount ?? 0),
                    TipAmount = tickets.Sum(t => t.Amounts.TipAmount ?? 0),
                    CoversTotal = tickets.Sum(t => t.Covers ?? 0)
                }
            },
            Tickets = tickets
        };

        return new Resultado(batch, tienda, fechaNegocio, avisos);
    }

    private static TicketDto ArmarTicket(
        string[] c, IEnumerable<string[]> cmi, IEnumerable<string[]> cdsc, IEnumerable<string[]> ctnd, IEnumerable<string[]> cvat,
        string moneda, List<string> avisos)
    {
        var apertura = ParseFechaHora(c[6]) ?? ParseFecha(c[4]);
        var cierre = ParseFechaHora(c[7]);
        var lineas = ArmarLineas(c[3], cmi.ToList(), cdsc.ToList(), avisos);

        var bruto = cmi.Sum(r => Num(r[77]));
        var descuento = -cdsc.Sum(r => Num(r[63]));
        var neto = Num(c[23]);
        var impuesto = cvat.Sum(r => Num(r[18]));
        var mesa = c.ElementAtOrDefault(50)?.Trim();

        return new TicketDto
        {
            TicketId = c[48].Trim(),
            TicketNumber = c[3].Trim(),
            Status = cierre.HasValue ? "CLOSED" : "OPEN",
            OpenedAt = apertura.ToString("yyyy-MM-ddTHH:mm:ss"),
            ClosedAt = cierre?.ToString("yyyy-MM-ddTHH:mm:ss"),
            BusinessDate = ParseFecha(c[4]).ToString("yyyy-MM-dd"),
            MealPeriod = PeriodoComida(apertura.Hour),
            Table = string.IsNullOrEmpty(mesa) || mesa == "0" ? null : new TableInfo { TableNumber = mesa },
            Waiter = string.IsNullOrWhiteSpace(c[8]) || c[8].Trim() == "0" ? null : new WaiterInfo { WaiterId = c[8].Trim() },
            Covers = int.TryParse(c[9], out var cub) ? cub : null,
            Currency = moneda,
            Amounts = new TicketAmounts
            {
                GrossAmount = Math.Max(0, Math.Round(bruto, 2)),
                DiscountAmount = Math.Max(0, Math.Round(descuento, 2)),
                NetAmount = Math.Round(neto, 2),
                TaxAmount = Math.Round(impuesto, 2),
                NetAmountExclTax = Math.Round(neto - impuesto, 2),
                TipAmount = Math.Round(Num(c[15]), 2),
                TotalPaidAmount = Math.Round(Num(c[11]), 2)
            },
            Discounts = cdsc
                .GroupBy(d => d[3].Trim())
                .Select(g => new DiscountDto { DiscountName = g.Key, DiscountAmount = Math.Round(-g.Sum(d => Num(d[63])), 2) })
                .Where(d => d.DiscountAmount != 0)
                .ToList(),
            PaymentMethods = ctnd
                .Select(p => new PaymentMethodDto { PaymentMethod = p[3].Trim(), Amount = Math.Round(Num(p[55]), 2) })
                .ToList(),
            Items = lineas
        };
    }

    private class Linea
    {
        public required string[] Cmi;
        public decimal Cantidad;
        public decimal Importe;
    }

    private static List<TicketItemDto> ArmarLineas(string cheque, List<string[]> cmi, List<string[]> cdsc, List<string> avisos)
    {
        var descuentoPorLinea = cdsc.GroupBy(d => d[32]).ToDictionary(g => g.Key, g => -g.Sum(d => Num(d[63])));
        var vendidas = new List<Linea>();
        var anuladas = new List<(string[] Cmi, decimal Cantidad, decimal Importe)>();

        foreach (var r in cmi.OrderBy(r => int.TryParse(r[42], out var id) ? id : 0))
        {
            var importe = Num(r[77]);
            var cantidad = Num(r[78]);
            if (importe == 0 && cantidad == 0) continue;               // líneas que no se muestran (Do Not Show)
            if (importe == 0 && r[43].Trim() != "0") continue;        // modificadores sin precio ("A punto", "Sin sal")

            if (importe >= 0 && cantidad >= 0)
            {
                vendidas.Add(new Linea { Cmi = r, Cantidad = cantidad, Importe = importe });
                continue;
            }

            // Anulación o devolución: descuenta de las líneas del mismo producto, de la más nueva a la más vieja.
            // Si no hay (a veces la devolución usa otro código, p. ej. "$$Chelada" para el modificador "Chelada"),
            // de una línea del cheque con el mismo importe.
            var restanteImporte = -importe;
            var restanteCantidad = Math.Abs(cantidad);
            var candidatas = vendidas.Where(v => v.Cmi[5] == r[5] && v.Importe > 0).Reverse().ToList();
            if (candidatas.Count == 0)
                candidatas = vendidas.Where(v => v.Importe == -importe).Reverse().Take(1).ToList();
            foreach (var v in candidatas)
            {
                var imp = Math.Min(v.Importe, restanteImporte);
                var cant = Math.Min(v.Cantidad, restanteCantidad);
                v.Importe -= imp; v.Cantidad -= cant;
                restanteImporte -= imp; restanteCantidad -= cant;
                if (restanteImporte <= 0) break;
            }
            if (restanteImporte > 0)
                avisos.Add($"Cheque {cheque}: anulación de {r[3].Trim()} ({-importe:0.##}) sin línea original para descontar");

            anuladas.Add((r, Math.Abs(cantidad), -importe));
        }

        var resultado = new List<TicketItemDto>();
        foreach (var v in vendidas.Where(v => v.Importe > 0 || v.Cantidad > 0))
        {
            var r = v.Cmi;
            var original = Num(r[77]);
            var desc = Math.Min(descuentoPorLinea.GetValueOrDefault(r[42]), v.Importe);
            var neto = v.Importe - desc;
            // IVA del ítem proporcional a lo que quedó vendido (después de anulaciones y descuentos)
            var imp = original == 0 ? 0 : Num(r[80]) * neto / original;
            resultado.Add(Item(r, v.Cantidad, v.Importe, desc, neto, imp, false, null));
        }
        foreach (var (r, cantidad, importe) in anuladas)
            resultado.Add(Item(r, cantidad, importe, 0, importe, 0, true, NullSiVacio(r[19]) ?? (r[52].Trim() == "1" ? "DEVOLUCION_POS" : "ANULACION_POS")));

        return resultado;
    }

    private static TicketItemDto Item(string[] r, decimal cantidad, decimal bruto, decimal descuento, decimal neto, decimal impuesto, bool anulado, string? motivo) => new()
    {
        LineId = r[42].Trim() + (anulado ? "-A" : ""),
        ProductCode = string.IsNullOrWhiteSpace(r[5]) ? r[2].Trim() : r[5].Trim(),
        ProductName = string.IsNullOrWhiteSpace(r[3]) ? "Sin nombre" : r[3].Trim(),
        ProductCategory = Categoria(r[9], r[13]),
        ProductFamily = NullSiVacio(r[9]),
        ProductSubfamily = NullSiVacio(r[13]),
        Quantity = Math.Round(cantidad, 3),
        UnitPrice = cantidad == 0 ? 0 : Math.Round(bruto / cantidad, 2),
        GrossAmount = Math.Round(bruto, 2),
        DiscountAmount = Math.Round(descuento, 2),
        NetAmount = Math.Round(neto, 2),
        TaxAmount = Math.Round(impuesto, 2),
        NetAmountExclTax = Math.Round(neto - impuesto, 2),
        IsDiscounted = descuento > 0,
        IsVoided = anulado,
        VoidReason = motivo,
        OrderedAt = ParseFechaHora(r[32])?.ToString("yyyy-MM-ddTHH:mm:ss")
    };

    /// <summary>Categoría estándar a partir de la familia y subfamilia de MICROS (lo que no se reconoce queda OTHER).</summary>
    public static string Categoria(string familia, string subfamilia)
    {
        var f = familia.Trim().ToUpperInvariant();
        var s = subfamilia.Trim().ToUpperInvariant();
        if (f.Contains("VINO") || s.Contains("COPA") || s is "CARMENERE" or "CABERNET SAUVIGNON" or "MERLOT" or "MALBEC") return "WINE";
        if (s.Contains("COCTEL") || s is "GIN" or "PISCO" or "WHISKY" or "RON" or "VODKA" or "TEQUILA") return "COCKTAIL";
        if (s.Contains("CAFE")) return "COFFEE";
        if (s.Contains("POSTRE")) return "DESSERT";
        if (s.Contains("GUARNICION") || s.Contains("AGREGADO")) return "SIDE_DISH";
        if (s.Contains("ENTRADA") || s.Contains("ENTRANTE") || s.Contains("PICOTEO") || s.Contains("ENSALADA")) return "STARTER";
        if (s.Contains("CARNE") || s.Contains("POLLO") || s.Contains("PESCADO") || s.Contains("HAMBURG") || s.Contains("PASTA")) return "MAIN_COURSE";
        if (f.Contains("BEBESTIBLE") || f.Contains("BEBIDA")) return "BEVERAGE";
        return "OTHER";
    }

    private static string PeriodoComida(int hora) => hora switch
    {
        >= 6 and < 12 => "BREAKFAST",
        >= 12 and < 17 => "LUNCH",
        >= 17 and < 21 => "DINNER",
        _ => "LATE_NIGHT"
    };

    private static decimal Num(string? valor) =>
        decimal.TryParse(valor?.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 0;

    private static DateTime ParseFecha(string valor) =>
        DateTime.ParseExact(valor.Trim()[..8], "yyyyMMdd", CultureInfo.InvariantCulture);

    private static DateTime? ParseFechaHora(string? valor) =>
        valor != null && valor.Trim().Length >= 14
        && DateTime.TryParseExact(valor.Trim()[..14], "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var f) ? f : null;

    private static string? NullSiVacio(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static string SoloLetrasYNumeros(string valor) =>
        new string(valor.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
}
