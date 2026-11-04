using LaCabrera.Api.Models.DTOs;

namespace LaCabrera.Api.Services;

/// <summary>
/// Compara lo que suman los tickets de un batch contra los totales de control que declara el origen
/// (por ejemplo el Sales Summary de Toast). Una diferencia no rechaza el batch: queda como advertencia
/// para que no vuelva a entrar en silencio una carga que no cierra (caso Miami, octubre 2026).
/// </summary>
public static class ControlTotalesBatch
{
    public record Sumas(decimal Bruto, decimal Neto, decimal Servicio, decimal Impuesto, decimal Propina);

    public static List<BatchWarning> Comparar(ControlTotals control, Sumas sumas, double toleranciaPorcentaje)
    {
        var avisos = new List<BatchWarning>();

        void Comparar(string campo, decimal? declarado, decimal calculado)
        {
            if (declarado is not { } d || d == 0) return;
            var tolerancia = Math.Abs(d) * (decimal)(toleranciaPorcentaje / 100);
            if (Math.Abs(calculado - d) > tolerancia)
            {
                avisos.Add(new BatchWarning
                {
                    Code = "CONTROL_TOTALS_MISMATCH",
                    Message = $"Diferencia en {campo}: declarado {d:0.00}, suma de tickets {calculado:0.00} ({(calculado - d) / d:+0.0%;-0.0%})"
                });
            }
        }

        Comparar("gross_sales", control.GrossSalesAmount, sumas.Bruto);
        Comparar("net_sales", control.NetSalesAmount, sumas.Neto);
        Comparar("service_charge", control.ServiceChargeAmount, sumas.Servicio);
        Comparar("tax", control.TaxAmount, sumas.Impuesto);
        Comparar("tips", control.TipAmount, sumas.Propina);
        return avisos;
    }
}
