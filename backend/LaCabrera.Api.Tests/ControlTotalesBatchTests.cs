using LaCabrera.Api.Models.DTOs;
using LaCabrera.Api.Services;
using Xunit;

namespace LaCabrera.Api.Tests;

public class ControlTotalesBatchTests
{
    // Sales Summary de Toast, Miami Sunny septiembre 2026
    private static readonly ControlTotals Declarado = new()
    {
        NetSalesAmount = 288260.26m,
        ServiceChargeAmount = 49346.28m,
        TaxAmount = 26835.62m,
        TipAmount = 8664.06m,
    };

    [Fact]
    public void Sin_diferencias_no_avisa()
    {
        var avisos = ControlTotalesBatch.Comparar(Declarado,
            new ControlTotalesBatch.Sumas(0, 288260.10m, 49346.11m, 26835.00m, 8664.06m), 0.5);

        Assert.Empty(avisos);
    }

    [Fact]
    public void Neto_sin_el_servicio_avisa_la_diferencia()
    {
        // Octubre con el parser combinado: el neto llegó 1,5% abajo y los impuestos sin el del servicio
        var avisos = ControlTotalesBatch.Comparar(Declarado,
            new ControlTotalesBatch.Sumas(0, 283877.18m, 49346.11m, 24000m, 8664.06m), 0.5);

        Assert.Equal(2, avisos.Count);
        Assert.All(avisos, a => Assert.Equal("CONTROL_TOTALS_MISMATCH", a.Code));
        Assert.Contains(avisos, a => a.Message.Contains("net_sales"));
        Assert.Contains(avisos, a => a.Message.Contains("tax"));
    }

    [Fact]
    public void Totales_no_declarados_no_se_comparan()
    {
        var avisos = ControlTotalesBatch.Comparar(new ControlTotals(),
            new ControlTotalesBatch.Sumas(100, 90, 10, 5, 1), 0.5);

        Assert.Empty(avisos);
    }
}
