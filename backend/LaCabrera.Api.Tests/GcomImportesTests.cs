using System.Text.Json.Nodes;
using LaCabrera.Api.Services;

namespace LaCabrera.Api.Tests;

public class GcomImportesTests
{
    // Venta con la forma de Vendas2 (datos inventados): 2 ítems vendidos, 1 cancelado, propina aparte
    private const string RespuestaVendas2 = """
    {
      "totalCupons": 1,
      "totalItens": 3,
      "vendas": [{
        "codLoja": 8346, "controle": 1797, "dataVenda": "2026-10-20",
        "VEN_HRINICIOVENDA": "20:15:00", "VEN_HRFIMVENDA": "22:40:10",
        "cancelada": "N", "statusVenda": "F",
        "valProduto": 300.00, "valDesconto": 0.0, "valAcrescimo": 0.0, "valLiquido": 300.00,
        "VEN_GORJETA": 34.50,
        "VNE_TOTICMS": 10.50, "VNE_TOTPIS": 0.0, "VNE_TOTCOFINS": 0.0, "VNE_TOTFCP": 0.0,
        "docConsumidor": "00000000000", "cliente": { "nome": "X" },
        "documento": { "modelo": "65", "numero": 1553 },
        "itens": [
          { "numLancto": 1, "codProduto": "100", "descricaoProduto": "BIFE DE CHORIZO", "quantidade": 2.0, "valUnitario": 100.0, "valDesconto": 0.0, "valTotal": 200.0, "cancelado": "N", "datHoraLancamento": "2026-10-20 20:20:00" },
          { "numLancto": 2, "codProduto": "200", "descricaoProduto": "VINHO TACA", "quantidade": 1.0, "valUnitario": 100.0, "valDesconto": 0.0, "valTotal": 100.0, "cancelado": "N", "datHoraLancamento": "2026-10-20 20:21:00" },
          { "numLancto": 3, "codProduto": "200", "descricaoProduto": "VINHO TACA", "quantidade": 1.0, "valUnitario": 100.0, "valDesconto": 0.0, "valTotal": 100.0, "cancelado": "S", "datHoraLancamento": "2026-10-20 20:21:30" }
        ],
        "formaPagamento": [
          { "formaPgto": "CARTAO DE CREDITO", "bandeira": "VISA", "valor": 250.00 },
          { "formaPgto": "DINHEIRO", "bandeira": "", "valor": 50.00 }
        ]
      }]
    }
    """;

    private static GcomVenda Venta(string json = RespuestaVendas2) => GcomImportes.Deserializar(json)!.Vendas![0];

    [Fact]
    public void Ticket_toma_valLiquido_como_bruto_y_saca_impuestos_de_la_cabecera()
    {
        var t = GcomImportes.CalcularTicket(Venta());

        Assert.Equal("8346-1797", t.ExternalTicketId);
        Assert.Equal("1553", t.NumeroTicket);
        Assert.Equal("NFCE", t.TipoDocumentoFiscal);
        Assert.Equal("CLOSED", t.Estado);
        Assert.Equal(300.00m, t.ImporteBruto);
        Assert.Equal(10.50m, t.ImporteImpuesto);
        Assert.Equal(289.50m, t.ImporteNeto);
        Assert.Equal(34.50m, t.ImportePropina);
        Assert.Equal(300.00m, t.ImporteTotalPagado); // los pagos cubren valLiquido, sin la propina
        Assert.Equal(new DateTime(2026, 10, 20, 20, 15, 0), t.FechaApertura);
        Assert.Equal("DINNER", t.PeriodoComida);
    }

    [Fact]
    public void Item_cancelado_queda_anulado_y_sin_impuesto()
    {
        var t = GcomImportes.CalcularTicket(Venta());

        Assert.Equal(3, t.Lineas.Count);
        Assert.Equal(300.00m, t.Lineas.Where(l => !l.EstaAnulado).Sum(l => l.ImporteBruto));
        var cancelado = t.Lineas.Single(l => l.ExternalLineId == "3");
        Assert.True(cancelado.EstaAnulado);
        Assert.Equal(0m, cancelado.ImporteImpuesto);
        // el impuesto de la cabecera se reparte según lo vendido
        Assert.Equal(7.00m, t.Lineas.Single(l => l.ExternalLineId == "1").ImporteImpuesto);
        Assert.Equal(3.50m, t.Lineas.Single(l => l.ExternalLineId == "2").ImporteImpuesto);
        Assert.Equal(t.ImporteImpuesto, t.Lineas.Sum(l => l.ImporteImpuesto));
    }

    [Fact]
    public void Venta_cancelada_queda_VOIDED_con_todas_las_lineas_anuladas()
    {
        var t = GcomImportes.CalcularTicket(Venta(RespuestaVendas2.Replace("\"cancelada\": \"N\"", "\"cancelada\": \"S\"")));

        Assert.Equal("VOIDED", t.Estado);
        Assert.True(t.EstaAnulado);
        Assert.All(t.Lineas, l => Assert.True(l.EstaAnulado));
    }

    [Fact]
    public void Venta_que_cierra_pasada_la_medianoche_cierra_al_dia_siguiente()
    {
        var t = GcomImportes.CalcularTicket(Venta(RespuestaVendas2.Replace("\"22:40:10\"", "\"00:35:00\"")));

        Assert.Equal(new DateTime(2026, 10, 21, 0, 35, 0), t.FechaCierre);
        Assert.Equal(new DateTime(2026, 10, 20), t.FechaNegocio);
    }

    [Fact]
    public void Pagos_mantienen_forma_y_marca()
    {
        var t = GcomImportes.CalcularTicket(Venta());

        Assert.Equal(2, t.Pagos.Count);
        Assert.Equal(("CARTAO DE CREDITO", "VISA", 250.00m), (t.Pagos[0].FormaPago, t.Pagos[0].Marca, t.Pagos[0].Importe));
        Assert.Null(t.Pagos[1].Marca);
    }

    [Fact]
    public void Crudo_se_guarda_sin_datos_del_consumidor()
    {
        var limpio = JsonNode.Parse(GcomImportes.SanitizarCrudo(RespuestaVendas2))!;
        var venta = limpio["vendas"]![0]!.AsObject();

        Assert.False(venta.ContainsKey("cliente"));
        Assert.False(venta.ContainsKey("docConsumidor"));
        Assert.Equal(1797, venta["controle"]!.GetValue<long>());
        Assert.Equal(3, venta["itens"]!.AsArray().Count);
    }
}

public class GcomCupoTests
{
    private static readonly TimeZoneInfo Sp = GcomCupo.Zona("America/Sao_Paulo");

    [Fact]
    public void El_dia_del_cupo_es_el_de_la_tienda_y_no_el_UTC()
    {
        // 25/10 23:30 en Porto Alegre = 26/10 02:30 UTC: sigue siendo el 25/10 para el cupo
        var utc = new DateTime(2026, 10, 26, 2, 30, 0, DateTimeKind.Utc);

        Assert.Equal(new DateTime(2026, 10, 25), GcomCupo.HoyLocal(utc, Sp));
        var (desde, hasta) = GcomCupo.LimitesDiaUtc(utc, Sp);
        Assert.Equal(new DateTime(2026, 10, 25, 3, 0, 0), desde);
        Assert.Equal(new DateTime(2026, 10, 26, 3, 0, 0), hasta);
    }

    [Fact]
    public void Pendientes_saltean_los_dias_con_crudo_y_priorizan_ayer()
    {
        var conCrudo = new HashSet<DateTime> { new(2026, 8, 2), new(2026, 8, 4) };

        var dias = GcomCupo.DiasPendientes(new DateTime(2026, 8, 1), new DateTime(2026, 8, 5), conCrudo, priorizarUltimo: true);

        Assert.Equal(new[] { new DateTime(2026, 8, 5), new DateTime(2026, 8, 1), new DateTime(2026, 8, 3) }, dias);
    }

    [Fact]
    public void Sin_priorizar_van_del_mas_viejo_al_mas_nuevo()
    {
        var dias = GcomCupo.DiasPendientes(new DateTime(2026, 8, 1), new DateTime(2026, 8, 3), new HashSet<DateTime>(), priorizarUltimo: false);

        Assert.Equal(new[] { new DateTime(2026, 8, 1), new DateTime(2026, 8, 2), new DateTime(2026, 8, 3) }, dias);
    }
}
