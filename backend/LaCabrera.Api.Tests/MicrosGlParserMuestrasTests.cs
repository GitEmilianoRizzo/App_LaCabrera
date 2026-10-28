using System.Globalization;
using LaCabrera.Api.Services;
using LaCabrera.Api.Validators;
using Xunit.Abstractions;

namespace LaCabrera.Api.Tests;

/// <summary>
/// Prueba el parser contra archivos reales de MICROS. Los archivos no están en el repo (son datos de
/// terceros): se indican con la variable MICROS_MUESTRAS (carpeta con GL_*.txt). Sin la variable, se saltea.
/// </summary>
public class MicrosGlParserMuestrasTests
{
    private readonly ITestOutputHelper _out;
    public MicrosGlParserMuestrasTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void Archivos_reales_cuadran_contra_los_totales_del_dia_y_pasan_el_validador()
    {
        var carpeta = Environment.GetEnvironmentVariable("MICROS_MUESTRAS");
        if (string.IsNullOrEmpty(carpeta) || !Directory.Exists(carpeta)) return;

        foreach (var archivo in Directory.GetFiles(carpeta, "GL_*.txt", SearchOption.AllDirectories).Where(f => !f.Contains("GL_PIE_")))
        {
            var contenido = File.ReadAllText(archivo);
            var r = MicrosGlParser.Parsear(contenido, "TEST", "America/Santiago", "CLP");
            var b = r.Batch;

            var sum = contenido.Split('\n').Select(l => l.TrimEnd('\r').Split('|')).Where(c => c[0] == "SUM").ToList();
            decimal S(int i) => sum.Sum(c => decimal.Parse(c[i], CultureInfo.InvariantCulture));

            var validacion = new DailySalesBatchRequestValidator().Validate(b);
            var lineasVendidas = b.Tickets.SelectMany(t => t.Items).Where(i => i.IsVoided != true).ToList();

            _out.WriteLine($"{r.CodigoTienda} {r.FechaNegocio:dd/MM}: {b.Tickets.Count} tickets ({b.Tickets.Count(t => t.Status == "OPEN")} abiertos), {b.Tickets.Sum(t => t.Items.Count)} líneas ({b.Tickets.SelectMany(t => t.Items).Count(i => i.IsVoided == true)} anuladas)");
            _out.WriteLine($"   bruto {b.BatchHeader.ControlTotals.GrossSalesAmount:N0} vs SUM[24] {S(24):N0} | neto {b.BatchHeader.ControlTotals.NetSalesAmount:N0} vs SUM[3] {S(3):N0} | desc {b.BatchHeader.ControlTotals.DiscountAmount:N0} vs SUM[4] {-S(4):N0} | propina {b.BatchHeader.ControlTotals.TipAmount:N0} vs SUM[5] {S(5):N0}");
            _out.WriteLine($"   líneas vendidas: bruto {lineasVendidas.Sum(i => i.GrossAmount):N0}, neto {lineasVendidas.Sum(i => i.NetAmount):N0} | validador: {(validacion.IsValid ? "OK" : string.Join("; ", validacion.Errors.Take(3)))} | avisos: {string.Join(" / ", r.Avisos.Take(3))}");

            Assert.True(validacion.IsValid, $"{r.CodigoTienda}: {string.Join("; ", validacion.Errors.Take(3))}");
            Assert.Equal(S(5), b.BatchHeader.ControlTotals.TipAmount);
            // Los descuentos de cheques sin cabecera (CHDR) no se cargan: el parser los avisa
            var cheques = contenido.Split('\n').Select(l => l.Split('|')).Where(c => c[0] == "CHDR").Select(c => c[3]).ToHashSet();
            var huerfanos = -contenido.Split('\n').Select(l => l.TrimEnd('\r').Split('|'))
                .Where(c => c[0] == "CDSC" && !cheques.Contains(c[1])).Sum(c => decimal.Parse(c[63], CultureInfo.InvariantCulture));
            Assert.Equal(-S(4) - huerfanos, b.BatchHeader.ControlTotals.DiscountAmount);
            Assert.Equal(b.BatchHeader.ControlTotals.GrossSalesAmount, lineasVendidas.Sum(i => i.GrossAmount));
        }
    }
}
