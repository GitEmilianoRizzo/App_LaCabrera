using System.Net;
using LaCabrera.Api.Services.ExchangeRate;
using Microsoft.Extensions.Logging.Abstractions;

namespace LaCabrera.Api.Tests;

public class BcraExchangeRateProviderTests
{
    // Respuesta del BCRA del 09/10/2026 (recortada a las monedas que usamos)
    private const string Respuesta = """
    {"status":200,"results":{"fecha":"2026-10-09","detalle":[
      {"codigoMoneda":"USD","descripcion":"DOLAR E.E.U.U.","tipoPase":0.0,"tipoCotizacion":1517.0},
      {"codigoMoneda":"BRL","descripcion":"REAL","tipoPase":0.200497,"tipoCotizacion":304.154303},
      {"codigoMoneda":"CLP","descripcion":"PESO CHILENO","tipoPase":0.001022,"tipoCotizacion":1.550855},
      {"codigoMoneda":"COP","descripcion":"COLOMBIANOS","tipoPase":0.000313,"tipoCotizacion":0.475563},
      {"codigoMoneda":"ARS","descripcion":"PESO","tipoPase":0.000659,"tipoCotizacion":0.0}
    ]}}
    """;

    private class RespuestaFija : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Respuesta) });
    }

    [Fact]
    public async Task Convierte_real_peso_chileno_y_colombiano_cruzando_por_pesos_argentinos()
    {
        var proveedor = new BcraExchangeRateProvider(new HttpClient(new RespuestaFija()), NullLogger<BcraExchangeRateProvider>.Instance);

        var tasas = (await proveedor.GetRatesAsync(new DateTime(2026, 10, 9), new[] { "BRL", "CLP", "COP", "ARS", "USD" }))
            .ToDictionary(t => t.CodigoMoneda);

        Assert.All(tasas.Values, t => Assert.True(t.Success));
        Assert.Equal(4.99m, Math.Round(tasas["BRL"].UnidadesPorUsd, 2));
        Assert.Equal(978.17m, Math.Round(tasas["CLP"].UnidadesPorUsd, 2));
        Assert.Equal(3189.90m, Math.Round(tasas["COP"].UnidadesPorUsd, 2));
        Assert.Equal(1517m, tasas["ARS"].UnidadesPorUsd);
        Assert.Equal(1m, tasas["USD"].UnidadesPorUsd);
    }
}
