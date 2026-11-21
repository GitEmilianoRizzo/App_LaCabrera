using System.Text.Json;
using LaCabrera.Api.Services;

namespace LaCabrera.Api.Tests;

public class NeoImportesTests
{
    // Jornada con la forma de los endpoints planos de NEO (datos inventados): un ticket con tarjeta y otro en efectivo
    // con vuelto (pago negativo), propina aparte y una línea de subtotal que no afecta al importe.
    private const string Dia = """
    {
      "shop": "0001", "jornada": "20261031",
      "ticketheaders": [
        { "orderId": "000120261031100001", "id": 100001, "series": "FS 2601A01", "invoiceNumber": "0010071", "docType": "CON",
          "hourStarted": "130652", "hourPayed": "135649", "operator": "Manager", "turn": "C0101", "zone": "Z0102", "spot": "16",
          "currency": "EUR", "netAmount": 20.45, "taxAmount": 2.05, "total": 22.5 },
        { "orderId": "000120261031100002", "id": 100002, "series": "FS 2601A01", "invoiceNumber": "0010072", "docType": "CON",
          "hourStarted": "232000", "hourPayed": "001510", "operator": "Manager", "turn": "C0101", "zone": "Z0101", "spot": "03",
          "currency": "EUR", "netAmount": 105.45, "taxAmount": 10.55, "total": 116 }
      ],
      "ticketlines": [
        { "orderId": "000120261031100001", "line": "1", "product": "01000285", "productName": "CUBIERTO", "quantity": 4, "unitPrice": 2.5, "amount": 10, "netAmount": 9.09, "tax": 10, "type": "0", "affectAmount": "S" },
        { "orderId": "000120261031100001", "line": "2", "product": "01000080", "productName": "AGUA CON GAS", "quantity": 2, "unitPrice": 3.5, "amount": 7, "netAmount": 6.36, "tax": 10, "type": "0", "affectAmount": "S" },
        { "orderId": "000120261031100001", "line": "3", "product": "01000999", "productName": "FLAN", "quantity": 1, "unitPrice": 5.5, "amount": 5.5, "netAmount": 5, "tax": 10, "type": "0", "affectAmount": "S", "notes": "sin crema" },
        { "orderId": "000120261031100001", "line": "4", "product": "SUBT", "productName": "SUBTOTAL", "associatedLine": 3, "modifierLine": 0, "quantity": 1, "unitPrice": 22.5, "amount": 22.5, "type": "10", "affectAmount": "N" },
        { "orderId": "000120261031100002", "line": "1", "product": "01000500", "productName": "OJO DE BIFE", "quantity": 2, "unitPrice": 58, "amount": 116, "netAmount": 105.45, "tax": 10, "type": "0", "affectAmount": "S" }
      ],
      "ticketpayments": [
        { "orderId": "000120261031100001", "line": "1", "type": "TCR", "amount": 22.5, "affectAmount": "S" },
        { "orderId": "000120261031100002", "line": "1", "type": "CG", "code": "ok", "amount": 120, "affectAmount": "S" },
        { "orderId": "000120261031100002", "line": "2", "type": "CG", "code": "ok", "amount": -4, "affectAmount": "S" },
        { "orderId": "000120261031100002", "line": "3", "type": "TCR", "amount": 10, "affectAmount": "N" }
      ],
      "tickettaxes": [
        { "orderId": "000120261031100001", "taxCodeId": "IVA", "type": "IVA10", "percentage": 10, "netAmount": "20.450", "amount": "2.050" },
        { "orderId": "000120261031100002", "taxCodeId": "IVA", "type": "IVA10", "percentage": 10, "netAmount": "105.450", "amount": "10.550" }
      ],
      "cashreports": [
        { "line": 1, "lineType": "RV", "meanPayment": "CG", "amount": 116 },
        { "line": 2, "lineType": "RV", "meanPayment": "TCR", "amount": 22.5 }
      ],
      "dailysummary": { "code": "0001", "date": "20261031", "baseAmount": 125.9, "taxAmount": 12.6, "totalAmount": 138.5 },
      "docs": [ { "order": "100001", "status": "closed", "totalClients": 4 }, { "order": "100002", "status": "closed", "totalClients": 0 } ]
    }
    """;

    private static NeoDia Leer() => JsonSerializer.Deserialize<NeoDia>(Dia, NeoImportes.Json)!;

    private static readonly NeoImportes.Catalogo Catalogo = new(
        new Dictionary<string, (string?, string?)> { ["01000500"] = ("CABR04", "CABR0401"), ["01000080"] = ("BEB01", null) },
        new Dictionary<string, string> { ["CABR04"] = "14 GRILLADOS", ["BEB01"] = "01 REFRESCOS" },
        new Dictionary<string, string> { ["CABR0401"] = "CARNES" });

    [Fact]
    public void Jornada_que_cierra_no_tiene_avisos()
    {
        Assert.Empty(NeoImportes.Controlar(Leer()));
    }

    [Fact]
    public void Total_de_venta_es_el_de_cabecera_y_neto_sin_iva()
    {
        var t = NeoImportes.CalcularTickets(Leer(), Catalogo);
        Assert.Equal(2, t.Count);
        Assert.Equal(22.5m, t[0].ImporteBruto);
        Assert.Equal(20.45m, t[0].ImporteNeto);
        Assert.Equal(2.05m, t[0].ImporteImpuesto);
        Assert.Equal("0010071", t[0].NumeroTicket);
        Assert.Equal("FS 2601A01 0010071", t[0].NumeroDocumentoFiscal);
        Assert.Equal("000120261031100001", t[0].ExternalTicketId);
    }

    [Fact]
    public void Horas_formato_neo_y_cobro_pasada_la_medianoche()
    {
        var t = NeoImportes.CalcularTickets(Leer(), Catalogo);
        Assert.Equal(new DateTime(2026, 10, 31, 13, 6, 52), t[0].FechaApertura);
        Assert.Equal(new DateTime(2026, 10, 31, 13, 56, 49), t[0].FechaCierre);
        Assert.Equal("LUNCH", t[0].PeriodoComida);
        Assert.Equal(new DateTime(2026, 11, 1, 0, 15, 10), t[1].FechaCierre);
        Assert.Equal(new DateTime(2026, 10, 31), t[1].FechaNegocio);
    }

    [Fact]
    public void Vuelto_neteado_por_medio_y_propina_aparte()
    {
        var t = NeoImportes.CalcularTickets(Leer(), Catalogo)[1];
        var pago = Assert.Single(t.Pagos);
        Assert.Equal(("CG", 116m), (pago.Medio, pago.Importe));
        Assert.Equal(10m, t.ImportePropina);
        Assert.Equal(116m, t.ImporteTotalPagado);
    }

    [Fact]
    public void Lineas_que_no_afectan_quedan_anuladas_y_con_familia_del_catalogo()
    {
        var t = NeoImportes.CalcularTickets(Leer(), Catalogo);
        var subtotal = t[0].Lineas.Single(l => l.CodigoProducto == "SUBT");
        Assert.True(subtotal.EstaAnulado);
        Assert.Equal("3", Leer().TicketLines.Single(l => l.Product == "SUBT").AssociatedLine);
        Assert.Equal(22.5m, t[0].Lineas.Where(l => !l.EstaAnulado).Sum(l => l.ImporteBruto));
        Assert.Equal("sin crema", t[0].Lineas.Single(l => l.CodigoProducto == "01000999").Notas);

        var bife = t[1].Lineas.Single();
        Assert.Equal(("14 GRILLADOS", "CARNES", "MAIN_COURSE"), (bife.Familia, bife.SubFamilia, bife.Categoria));
        Assert.Equal("BEVERAGE", t[0].Lineas.Single(l => l.CodigoProducto == "01000080").Categoria);
        Assert.Null(t[0].Lineas.Single(l => l.CodigoProducto == "01000999").Familia);
    }

    [Fact]
    public void Comensales_desde_docs_y_nulos_si_no_hay()
    {
        var t = NeoImportes.CalcularTickets(Leer(), Catalogo);
        Assert.Equal(4, t[0].Cubiertos);
        Assert.Null(t[1].Cubiertos);
    }

    [Fact]
    public void Controles_detectan_diferencias()
    {
        var dia = Leer();
        dia.TicketHeaders[0].Total = 25m;
        dia.TicketTaxes[1].Amount = 9m;
        var avisos = NeoImportes.Controlar(dia);
        Assert.Contains(avisos, a => a.Contains("líneas 22.50 vs total 25.00"));
        Assert.Contains(avisos, a => a.Contains("pagos 22.50 vs total 25.00"));
        Assert.Contains(avisos, a => a.Contains("neto + IVA"));
        Assert.Contains(avisos, a => a.Contains("tickettaxes"));
        Assert.Contains(avisos, a => a.Contains("dailysummary"));
    }

    [Fact]
    public void Ventas_del_arqueo()
    {
        Assert.Equal(138.5m, NeoImportes.VentasArqueo(Leer()));
    }

    [Theory]
    [InlineData("14 GRILLADOS", "MAIN_COURSE")]
    [InlineData("11 ENTRADAS", "STARTER")]
    [InlineData("13 ENSALADAS Y PASTAS", "SIDE_DISH")]
    [InlineData("05 V. TINTOS", "WINE")]
    [InlineData("07 CHAMPAGNE CAVA FINO", "WINE")]
    [InlineData("02 CERVEZAS", "BEVERAGE")]
    [InlineData("10 COCKTAILS", "COCKTAIL")]
    [InlineData("15 POSTRES", "DESSERT")]
    [InlineData("99 VARIOS", "OTHER")]
    public void Categoria_por_familia(string familia, string categoria)
    {
        Assert.Equal(categoria, NeoImportes.CategoriaDeFamilia(familia));
    }

    [Fact]
    public void Dias_a_pedir_relee_ultimos_y_completa_faltantes()
    {
        var inicio = new DateTime(2026, 9, 1);
        var ayer = new DateTime(2026, 9, 20);
        var conCrudo = Enumerable.Range(0, 10).Select(i => inicio.AddDays(i)).ToHashSet(); // 1 al 10 de septiembre
        var dias = NeoSync.DiasAPedir(inicio, ayer, conCrudo, diasReleer: 7, maxDias: 62);
        Assert.Equal(ayer, dias[0]);
        Assert.Equal(new DateTime(2026, 9, 14), dias[6]);
        Assert.Equal(new[] { 11, 12, 13 }, dias.Skip(7).Select(d => d.Day));
        Assert.Equal(10, dias.Count);

        Assert.Equal(3, NeoSync.DiasAPedir(inicio, ayer, conCrudo, 7, maxDias: 3).Count);
    }

    [Fact]
    public void Ayer_es_en_hora_de_espana()
    {
        var zona = NeoSync.Zona("Europe/Madrid");
        // 31/10 23:30 UTC = 01/11 00:30 en Madrid (CET): la última jornada cerrada es el 31/10
        Assert.Equal(new DateTime(2026, 10, 31), NeoSync.Ayer(new DateTime(2026, 10, 31, 23, 30, 0, DateTimeKind.Utc), zona));
    }
}
