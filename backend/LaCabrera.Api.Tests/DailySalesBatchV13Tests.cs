using System.Text.Json;
using LaCabrera.Api.Models.DTOs;
using LaCabrera.Api.Services;
using LaCabrera.Api.Validators;
using Xunit;

namespace LaCabrera.Api.Tests;

/// <summary>
/// Contrato daily-batch v1.3 (T-380, Manila). Usa el ejemplo que se le manda al proveedor
/// (docs/integration/examples/manila_v1.3_full_day_example.json) y verifica que v1.0-v1.2 no cambian.
/// </summary>
public class DailySalesBatchV13Tests
{
    private static readonly DailySalesBatchRequestValidator Validador = new();

    private static DailySalesBatchRequest Ejemplo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "docs", "integration", "examples", "manila_v1.3_full_day_example.json")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var json = File.ReadAllText(Path.Combine(dir!.FullName, "docs", "integration", "examples", "manila_v1.3_full_day_example.json"));
        return JsonSerializer.Deserialize<DailySalesBatchRequest>(json)!;
    }

    private static TicketDto Ticket(DailySalesBatchRequest b, string id) => b.Tickets.Single(t => t.TicketId == id);

    [Fact]
    public void El_ejemplo_completo_es_valido_y_lee_los_campos_nuevos()
    {
        var batch = Ejemplo();

        var resultado = Validador.Validate(batch);

        Assert.True(resultado.IsValid, string.Join(" | ", resultado.Errors.Select(e => e.ErrorMessage)));
        Assert.Equal(5, batch.Tickets.Count);
        var devolucion = Ticket(batch, "POS01-000131");
        Assert.Equal("POS01-000124", devolucion.OriginalTicketId);
        Assert.Equal(1200.00m, Ticket(batch, "POS02-000057").TaxBreakdown!.VatExemptSales);
        Assert.Equal("GCASH-REF-PLACEHOLDER", Ticket(batch, "POS01-000124").PaymentMethods![1].Reference);
    }

    [Fact]
    public void El_ejemplo_no_genera_avisos_de_consistencia()
    {
        var batch = Ejemplo();

        var avisos = batch.Tickets.SelectMany(DailySalesBatchV13.Consistencia).ToList();

        Assert.Empty(avisos);
    }

    [Fact]
    public void Negativo_sin_REFUNDED_se_rechaza_con_codigo_propio()
    {
        var batch = Ejemplo();
        var venta = Ticket(batch, "POS01-000123");
        venta.Amounts.GrossAmount = -1920;
        venta.Items[0].Quantity = -1;

        var resultado = Validador.Validate(batch);

        Assert.False(resultado.IsValid);
        Assert.All(resultado.Errors, e => Assert.Equal("NEGATIVE_AMOUNT_NOT_REFUNDED", e.ErrorCode));
        Assert.Contains(resultado.Errors, e => e.ErrorMessage.Contains("POS01-000123"));
        Assert.Contains(resultado.Errors, e => e.ErrorMessage.Contains("negative quantity"));
    }

    [Fact]
    public void REFUNDED_sin_original_ticket_id_o_en_positivo_se_rechaza()
    {
        var batch = Ejemplo();
        var devolucion = Ticket(batch, "POS01-000131");
        devolucion.OriginalTicketId = null;
        devolucion.Amounts.NetAmount = 2240;

        var resultado = Validador.Validate(batch);

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, e => e.ErrorCode == "INVALID_REFUND" && e.ErrorMessage.Contains("original_ticket_id"));
        Assert.Contains(resultado.Errors, e => e.ErrorCode == "INVALID_REFUND" && e.ErrorMessage.Contains("net_amount must be <= 0"));
    }

    [Fact]
    public void Una_sola_fecha_de_negocio_y_ticket_id_unico_por_batch()
    {
        var batch = Ejemplo();
        Ticket(batch, "POS01-000123").BusinessDate = "2026-11-18";
        batch.Tickets[2].TicketId = "POS01-000124";

        var resultado = Validador.Validate(batch);

        Assert.Contains(resultado.Errors, e => e.ErrorCode == "BUSINESS_DATE_MISMATCH");
        Assert.Contains(resultado.Errors, e => e.ErrorCode == "DUPLICATE_TICKET_ID" && e.ErrorMessage.Contains("POS01-000124"));
    }

    [Fact]
    public void v13_exige_amounts_include_tax()
    {
        var batch = Ejemplo();
        batch.BatchHeader.AmountsIncludeTax = null;

        var resultado = Validador.Validate(batch);

        Assert.Contains(resultado.Errors, e => e.ErrorCode == "MISSING_AMOUNTS_INCLUDE_TAX");
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("1.2")]
    public void Versiones_anteriores_siguen_rechazando_negativos_y_aceptan_lo_de_siempre(string version)
    {
        var batch = Ejemplo();
        batch.SchemaVersion = version;
        batch.BatchHeader.AmountsIncludeTax = null;           // no es obligatorio antes de 1.3
        Ticket(batch, "POS01-000123").BusinessDate = "2026-11-18"; // tampoco la fecha única

        var resultado = Validador.Validate(batch);

        // Solo falla la devolución negativa, con el mensaje y código de siempre
        Assert.False(resultado.IsValid);
        Assert.All(resultado.Errors, e => Assert.DoesNotMatch("^[A-Z_]+$", e.ErrorCode));
        Assert.Contains(resultado.Errors, e => e.ErrorMessage == "ticket.amounts.gross_amount debe ser >= 0");
        Assert.Contains(resultado.Errors, e => e.ErrorMessage == "item.quantity debe ser >= 0");

        batch.Tickets.RemoveAll(t => t.Status == "REFUNDED");
        Assert.True(Validador.Validate(batch).IsValid);
    }

    [Fact]
    public void REFUNDED_no_queda_anulado_y_VOIDED_si()
    {
        Assert.False(DailySalesBatchV13.EstaAnulado("REFUNDED"));
        Assert.False(DailySalesBatchV13.EstaAnulado("CLOSED"));
        Assert.True(DailySalesBatchV13.EstaAnulado("VOIDED"));
        Assert.True(DailySalesBatchV13.EstaAnulado("CANCELLED"));
    }

    [Fact]
    public void Service_charge_se_suma_al_neto_interno_para_que_el_dashboard_no_lo_reste_dos_veces()
    {
        // net_amount_excl_tax 1.714,29 + service charge 171,43: el dashboard hace (neto interno − servicio)
        var interno = DailySalesBatchV13.NetoSinImpuestoInterno(1714.29m, 171.43m);

        Assert.Equal(1885.72m, interno);
        Assert.Equal(1714.29m, interno - 171.43m);
        Assert.Equal(1714.29m, DailySalesBatchV13.NetoSinImpuestoInterno(1714.29m, null));
        Assert.Null(DailySalesBatchV13.NetoSinImpuestoInterno(null, 171.43m));
    }

    [Fact]
    public void Neto_sin_impuesto_usa_el_informado_y_deriva_con_IVA_cero()
    {
        Assert.Equal(1500m, DailySalesBatchV13.NetoSinImpuesto(true, 1680m, 1500m, 180m));   // informado: no se descuenta dos veces
        Assert.Equal(1500m, DailySalesBatchV13.NetoSinImpuesto(true, 1680m, null, 180m));
        Assert.Equal(1200m, DailySalesBatchV13.NetoSinImpuesto(true, 1200m, null, 0m));     // exento
        Assert.Equal(1680m, DailySalesBatchV13.NetoSinImpuesto(false, 1680m, null, 180m));
        Assert.Null(DailySalesBatchV13.NetoSinImpuesto(true, 1680m, null, null));
    }

    [Fact]
    public void Avisa_linea_suma_de_lineas_y_pagos_que_no_cierran()
    {
        var batch = Ejemplo();
        var venta = Ticket(batch, "POS01-000124");
        venta.Items[2].GrossAmount = 2000;           // qty × precio = 2240
        venta.PaymentMethods![1].Amount = 1500;      // pagos 4500 contra 4594

        var avisos = DailySalesBatchV13.Consistencia(venta);

        Assert.Contains(avisos, a => a.Code == "LINE_AMOUNT_MISMATCH" && a.LineId == "3" && a.Message.Contains("2240.00"));
        Assert.Contains(avisos, a => a.Code == "TICKET_LINES_MISMATCH" && a.Message.Contains("gross_amount"));
        Assert.Contains(avisos, a => a.Code == "PAYMENTS_MISMATCH" && a.Message.Contains("4500.00"));
    }

    [Fact]
    public void Ticket_anulado_sin_pagos_no_avisa_pagos()
    {
        var batch = Ejemplo();

        var avisos = DailySalesBatchV13.Consistencia(Ticket(batch, "POS01-000125"));

        Assert.DoesNotContain(avisos, a => a.Code == "PAYMENTS_MISMATCH");
    }
}
