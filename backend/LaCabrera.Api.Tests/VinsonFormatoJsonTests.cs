using System.Text.Json;
using LaCabrera.Api.Services;

namespace LaCabrera.Api.Tests;

/// <summary>
/// La respuesta de Vinson cambió canceledItem de texto a booleano (oct-2026) y rompió la sincronización.
/// Se aceptan los dos formatos.
/// </summary>
public class VinsonFormatoJsonTests
{
    private static readonly JsonSerializerOptions Opciones = new() { PropertyNameCaseInsensitive = true };

    private static VinsonProduct Producto(string canceledItemJson) =>
        JsonSerializer.Deserialize<List<VinsonSalesFullInfo>>(
            $$"""[{"transactionId": 1, "products": [{"name": "Bife", "quantity": 1, "grossAmount": 100, "netAmount": 82.64, "canceledItem": {{canceledItemJson}}}]}]""",
            Opciones)![0].Products![0];

    [Theory]
    [InlineData("false", false)]
    [InlineData("true", true)]
    [InlineData("\"False\"", false)]
    [InlineData("\"True\"", true)]
    [InlineData("null", false)]
    public void CanceledItem_se_lee_como_texto_o_booleano(string json, bool anulado)
    {
        var linea = VinsonImportes.CalcularLinea(Producto(json));
        Assert.Equal(anulado, linea.EstaAnulado);
    }

    [Fact]
    public void Medio_de_pago_sale_de_payments_en_el_formato_nuevo()
    {
        var json = """[{"transactionId": 1, "payments": [{"paymentMethodId": 3, "paymentMethod": "Efectivo", "amount": 1000.0}, {"paymentMethodId": 7, "paymentMethod": "Mercado Pago QR", "amount": 25000.0}], "products": []}]""";
        var detalle = JsonSerializer.Deserialize<List<VinsonSalesFullInfo>>(json, Opciones)![0];
        Assert.Equal("Mercado Pago QR", detalle.MedioPagoPrincipal);
    }

    [Fact]
    public void Medio_de_pago_del_formato_viejo_sigue_funcionando()
    {
        var json = """[{"transactionId": 1, "metodoPago": "Efectivo", "products": []}]""";
        var detalle = JsonSerializer.Deserialize<List<VinsonSalesFullInfo>>(json, Opciones)![0];
        Assert.Equal("Efectivo", detalle.MedioPagoPrincipal);
    }
}
