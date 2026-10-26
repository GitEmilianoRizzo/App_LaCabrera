using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace LaCabrera.Api.Services;

/// <summary>
/// Cálculo de importes de las ventas de GCOM (Brasil, API wsrest Vendas2).
///
/// GCOM informa por venta:
/// - valProduto = Σ ítems no cancelados (valTotal), valLiquido = valProduto - valDesconto + valAcrescimo.
///   Es el total cobrado SIN propina: Σ formaPagamento.valor = valLiquido.
/// - VEN_GORJETA = propina (va aparte del total).
/// - Impuestos (ICMS, PIS, COFINS, FCP) incluidos en el precio, totalizados en la cabecera (VNE_TOT*).
///   Los impuestos por ítem no suman lo de la cabecera, así que en las líneas se prorratea.
/// - Ítems con cancelado = "S" vienen en el detalle pero no suman en valProduto.
/// </summary>
public static class GcomImportes
{
    public record LineaCalculada(
        string ExternalLineId,
        string CodigoProducto,
        string NombreProducto,
        decimal Cantidad,
        decimal PrecioUnitario,
        decimal ImporteBruto,
        decimal ImporteDescuento,
        decimal ImporteNeto,
        decimal ImporteImpuesto,
        bool EstaAnulado,
        DateTime? FechaPedido);

    public record PagoCalculado(string FormaPago, string? Marca, decimal Importe);

    public record TicketCalculado(
        string ExternalTicketId,
        string NumeroTicket,
        string Estado,
        bool EstaAnulado,
        DateTime FechaNegocio,
        DateTime? FechaApertura,
        DateTime? FechaCierre,
        string PeriodoComida,
        string? TipoDocumentoFiscal,
        decimal ImporteBruto,
        decimal ImporteDescuento,
        decimal ImporteNeto,
        decimal ImporteImpuesto,
        decimal ImportePropina,
        decimal ImporteTotalPagado,
        IReadOnlyList<LineaCalculada> Lineas,
        IReadOnlyList<PagoCalculado> Pagos);

    public static TicketCalculado CalcularTicket(GcomVenda venda)
    {
        var fechaNegocio = DateTime.ParseExact(venda.DataVenda ?? "", "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var apertura = CombinarFechaHora(fechaNegocio, venda.HoraInicio);
        var cierre = CombinarFechaHora(fechaNegocio, venda.HoraFin);
        if (apertura.HasValue && cierre.HasValue && cierre < apertura)
            cierre = cierre.Value.AddDays(1); // la venta cerró pasada la medianoche

        var anulada = string.Equals(venda.Cancelada, "S", StringComparison.OrdinalIgnoreCase);
        var bruto = venda.ValLiquido ?? 0;
        var impuesto = (venda.TotIcms ?? 0) + (venda.TotPis ?? 0) + (venda.TotCofins ?? 0) + (venda.TotFcp ?? 0);
        var neto = bruto - impuesto;

        var items = venda.Itens ?? new List<GcomItem>();
        var totalVendido = items.Where(i => !EsCancelado(i)).Sum(i => i.ValTotal ?? 0);

        var lineas = items.Select((item, idx) =>
        {
            var cancelado = EsCancelado(item);
            var importe = item.ValTotal ?? 0;
            // Impuesto de la cabecera repartido según lo vendido en cada línea
            var impLinea = cancelado || totalVendido == 0 ? 0 : impuesto * importe / totalVendido;
            return new LineaCalculada(
                ExternalLineId: (item.NumLancto ?? idx + 1).ToString(CultureInfo.InvariantCulture),
                CodigoProducto: string.IsNullOrWhiteSpace(item.CodProduto) ? "SIN_CODIGO" : item.CodProduto!.Trim(),
                NombreProducto: string.IsNullOrWhiteSpace(item.DescricaoProduto) ? "Sin nombre" : item.DescricaoProduto!.Trim(),
                Cantidad: item.Quantidade ?? 1,
                PrecioUnitario: Math.Round(item.ValUnitario ?? 0, 2),
                ImporteBruto: Math.Round(importe, 2),
                ImporteDescuento: Math.Round(item.ValDesconto ?? 0, 2),
                ImporteNeto: Math.Round(importe - impLinea, 2),
                ImporteImpuesto: Math.Round(impLinea, 2),
                EstaAnulado: cancelado || anulada,
                FechaPedido: ParseFechaHora(item.DatHoraLancamento));
        }).ToList();

        // Lo que se pierde al redondear línea por línea va a la línea vendida más grande,
        // para que Σ impuesto de las líneas = impuesto de la cabecera
        var residuo = Math.Round(impuesto, 2) - lineas.Sum(l => l.ImporteImpuesto);
        if (residuo != 0 && lineas.Any(l => !l.EstaAnulado))
        {
            var idx = lineas.FindIndex(l => l == lineas.Where(x => !x.EstaAnulado).MaxBy(x => x.ImporteBruto));
            var l = lineas[idx];
            lineas[idx] = l with { ImporteImpuesto = l.ImporteImpuesto + residuo, ImporteNeto = l.ImporteNeto - residuo };
        }

        var pagos = (venda.FormaPagamento ?? new List<GcomPagamento>())
            .Select(p => new PagoCalculado(
                string.IsNullOrWhiteSpace(p.FormaPgto) ? "SIN_FORMA" : p.FormaPgto!.Trim(),
                string.IsNullOrWhiteSpace(p.Bandeira) ? null : p.Bandeira!.Trim(),
                Math.Round(p.Valor ?? 0, 2)))
            .ToList();

        return new TicketCalculado(
            ExternalTicketId: $"{venda.CodLoja}-{venda.Controle}",
            NumeroTicket: venda.Documento?.Numero?.ToString(CultureInfo.InvariantCulture) ?? venda.Controle.ToString(CultureInfo.InvariantCulture),
            Estado: anulada ? "VOIDED" : "CLOSED",
            EstaAnulado: anulada,
            FechaNegocio: fechaNegocio,
            FechaApertura: apertura,
            FechaCierre: cierre,
            PeriodoComida: PeriodoComida(apertura?.Hour ?? 12),
            TipoDocumentoFiscal: venda.Documento?.Modelo == "65" ? "NFCE" : venda.Documento?.Modelo,
            ImporteBruto: Math.Round(bruto, 2),
            ImporteDescuento: Math.Round(venda.ValDesconto ?? 0, 2),
            ImporteNeto: Math.Round(neto, 2),
            ImporteImpuesto: Math.Round(impuesto, 2),
            ImportePropina: Math.Round(venda.Gorjeta ?? 0, 2),
            ImporteTotalPagado: pagos.Sum(p => p.Importe),
            Lineas: lineas,
            Pagos: pagos);
    }

    /// <summary>
    /// Quita del JSON crudo los datos personales del consumidor (CPF y ficha de cliente) antes de guardarlo.
    /// No se usan para la carga y no hace falta conservarlos.
    /// </summary>
    public static string SanitizarCrudo(string json)
    {
        var raiz = JsonNode.Parse(json);
        if (raiz?["vendas"] is JsonArray vendas)
        {
            foreach (var venta in vendas.OfType<JsonObject>())
            {
                venta.Remove("cliente");
                venta.Remove("docConsumidor");
                venta.Remove("docConsumidorDesconto");
            }
        }
        return raiz?.ToJsonString() ?? json;
    }

    public static GcomVendasResponse? Deserializar(string json) =>
        JsonSerializer.Deserialize<GcomVendasResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

    private static bool EsCancelado(GcomItem item) =>
        string.Equals(item.Cancelado, "S", StringComparison.OrdinalIgnoreCase);

    private static DateTime? CombinarFechaHora(DateTime fecha, string? hora) =>
        TimeSpan.TryParseExact(hora, @"hh\:mm\:ss", CultureInfo.InvariantCulture, out var t) ? fecha.Date + t : null;

    private static DateTime? ParseFechaHora(string? valor) =>
        DateTime.TryParseExact(valor, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var f) ? f : null;

    private static string PeriodoComida(int hora) => hora switch
    {
        >= 6 and < 12 => "BREAKFAST",
        >= 12 and < 17 => "LUNCH",
        >= 17 and < 21 => "DINNER",
        _ => "LATE_NIGHT"
    };
}

#region DTOs de la API de GCOM (wsrest/api/Vendas/Vendas2)

public class GcomVendasResponse
{
    [JsonPropertyName("totalCupons")] public int TotalCupons { get; set; }
    [JsonPropertyName("totalItens")] public int TotalItens { get; set; }
    [JsonPropertyName("vendas")] public List<GcomVenda>? Vendas { get; set; }
}

public class GcomVenda
{
    [JsonPropertyName("codLoja")] public int CodLoja { get; set; }
    [JsonPropertyName("controle")] public long Controle { get; set; }
    [JsonPropertyName("dataVenda")] public string? DataVenda { get; set; }
    [JsonPropertyName("VEN_HRINICIOVENDA")] public string? HoraInicio { get; set; }
    [JsonPropertyName("VEN_HRFIMVENDA")] public string? HoraFin { get; set; }
    [JsonPropertyName("cancelada")] public string? Cancelada { get; set; }
    [JsonPropertyName("statusVenda")] public string? StatusVenda { get; set; }
    [JsonPropertyName("valProduto")] public decimal? ValProduto { get; set; }
    [JsonPropertyName("valDesconto")] public decimal? ValDesconto { get; set; }
    [JsonPropertyName("valAcrescimo")] public decimal? ValAcrescimo { get; set; }
    [JsonPropertyName("valLiquido")] public decimal? ValLiquido { get; set; }
    [JsonPropertyName("VEN_GORJETA")] public decimal? Gorjeta { get; set; }
    [JsonPropertyName("VNE_TOTICMS")] public decimal? TotIcms { get; set; }
    [JsonPropertyName("VNE_TOTPIS")] public decimal? TotPis { get; set; }
    [JsonPropertyName("VNE_TOTCOFINS")] public decimal? TotCofins { get; set; }
    [JsonPropertyName("VNE_TOTFCP")] public decimal? TotFcp { get; set; }
    [JsonPropertyName("documento")] public GcomDocumento? Documento { get; set; }
    [JsonPropertyName("itens")] public List<GcomItem>? Itens { get; set; }
    [JsonPropertyName("formaPagamento")] public List<GcomPagamento>? FormaPagamento { get; set; }
}

public class GcomDocumento
{
    [JsonPropertyName("modelo")] public string? Modelo { get; set; }
    [JsonPropertyName("numero")] public long? Numero { get; set; }
}

public class GcomItem
{
    [JsonPropertyName("numLancto")] public int? NumLancto { get; set; }
    [JsonPropertyName("codProduto")] public string? CodProduto { get; set; }
    [JsonPropertyName("descricaoProduto")] public string? DescricaoProduto { get; set; }
    [JsonPropertyName("quantidade")] public decimal? Quantidade { get; set; }
    [JsonPropertyName("valUnitario")] public decimal? ValUnitario { get; set; }
    [JsonPropertyName("valDesconto")] public decimal? ValDesconto { get; set; }
    [JsonPropertyName("valTotal")] public decimal? ValTotal { get; set; }
    [JsonPropertyName("cancelado")] public string? Cancelado { get; set; }
    [JsonPropertyName("datHoraLancamento")] public string? DatHoraLancamento { get; set; }
}

public class GcomPagamento
{
    [JsonPropertyName("formaPgto")] public string? FormaPgto { get; set; }
    [JsonPropertyName("bandeira")] public string? Bandeira { get; set; }
    [JsonPropertyName("valor")] public decimal? Valor { get; set; }
}

#endregion
