using LaCabrera.Api.Services;

namespace LaCabrera.Api.Tests;

public class VinsonImportesTests
{
    private static VinsonProduct P(string nombre, decimal cantidad, decimal brutoUnitario, decimal netoUnitario, decimal? descuento = null) =>
        new() { Name = nombre, Quantity = cantidad, GrossAmount = brutoUnitario, NetAmount = netoUnitario, DiscountAmount = descuento };

    private static VinsonTransaccion Cabecera(decimal? montoFinal, decimal? montoNeto, int estado = 3) =>
        new() { IdTransaccion = 1, Estado = estado, MontoFinal = montoFinal, MontoNeto = montoNeto };

    private static List<VinsonLineaCalculada> Lineas(params VinsonProduct[] productos) =>
        productos.Select(VinsonImportes.CalcularLinea).ToList();

    // EZE, ticket 213529 del 14/08/2026, tal como lo informa Vinson (importes unitarios)
    private static readonly VinsonProduct[] Ticket213529 =
    {
        P("A Punto", 22, 0m, 0m),
        P("Agua C/Gas", 2, 5000m, 4132.23m),
        P("Agua S/Gas", 13, 5000m, 4132.23m),
        P("Empanada x2", 2, 12000m, 9917.36m),
        P("Empanadas x4", 6, 30000m, 24793.39m),
        P("Gaseosa Lata Pepsi", 8, 6000m, 4958.68m),
        P("Mensaje Cocina", 4, 0m, 0m),
        P("Pepsi Lata", 2, 0m, 0m),
        P("Pepsi Light Lata", 4, 0m, 0m),
        P("Pomelo Lata", 2, 0m, 0m),
        P("Sand Ojo Bife C/Prov", 22, 33000m, 27272.73m),
        P("Sand Veggie", 1, 24000m, 19834.71m),
    };

    [Fact]
    public void Linea_con_cantidad_mayor_a_uno_usa_importe_extendido()
    {
        var linea = VinsonImportes.CalcularLinea(P("Sand Ojo Bife C/Prov", 22, 33000m, 27272.73m));

        Assert.Equal(22m, linea.Cantidad);
        Assert.Equal(33000m, linea.PrecioUnitario);
        Assert.Equal(726000m, linea.ImporteBruto);
        Assert.Equal(600000.06m, linea.ImporteNeto);
        Assert.Equal(125999.94m, linea.ImporteImpuesto);
    }

    [Fact]
    public void Ticket_213529_queda_igual_que_el_POS()
    {
        var lineas = Lineas(Ticket213529);
        var ticket = VinsonImportes.CalcularTicket(Cabecera(1077000m, 890082.64m), null, lineas, TratamientoTicketNegativo.MarcarDevolucion);

        Assert.Equal(1077000m, lineas.Sum(l => l.ImporteBruto));
        Assert.Equal("CABECERA", ticket.OrigenImportes);
        Assert.Equal("CLOSED", ticket.Estado);
        Assert.False(ticket.EstaAnulado);
        Assert.Equal(1077000m, ticket.ImporteBruto);
        Assert.Equal(890082.64m, ticket.ImporteNeto);
        Assert.Equal(186917.36m, ticket.ImporteImpuesto);
    }

    [Fact]
    public void ImporteNetoSinImpuesto_descuenta_el_IVA_una_sola_vez()
    {
        // montoNeto de Vinson ya es sin IVA: no hay que volver a restar el impuesto
        var ticket = VinsonImportes.CalcularTicket(Cabecera(1077000m, 890082.64m), null, Lineas(Ticket213529), TratamientoTicketNegativo.MarcarDevolucion);

        Assert.Equal(890082.64m, ticket.ImporteNetoSinImpuesto);
        Assert.Equal(ticket.ImporteBruto, ticket.ImporteNetoSinImpuesto + ticket.ImporteImpuesto);
        Assert.Equal(1077000m / 1.21m, ticket.ImporteNetoSinImpuesto, 0);
    }

    [Fact]
    public void Descuento_unitario_se_multiplica_por_cantidad_y_se_acota_al_neto()
    {
        // 2 unidades con 100% de descuento (consumo interno): el neto queda en 0
        var interno = VinsonImportes.CalcularLinea(P("Infusion + Tostadas", 2, 14000m, 11570.25m, -11570.25m));
        Assert.Equal(28000m, interno.ImporteBruto);
        Assert.Equal(23140.50m, interno.ImporteDescuento);
        Assert.Equal(0m, interno.ImporteNeto);
        Assert.Equal(0m, interno.ImporteImpuesto);

        // Descuento mayor que el producto: no deja la línea en negativo
        var excedido = VinsonImportes.CalcularLinea(P("Te", 1, 3900m, 3223.14m, -50000m));
        Assert.Equal(3223.14m, excedido.ImporteDescuento);
        Assert.Equal(0m, excedido.ImporteNeto);
    }

    // Ticket EZE 213201: todas las líneas negativas, el POS lo informa en 0
    private static readonly VinsonProduct[] Devolucion =
    {
        P("Bife Chorizo 400 Gr", 1, -48000m, -48000m),
        P("Cafe C/ Leche", 2, 0m, 0m),
        P("Pollo + Guarn", 1, -42000m, -42000m),
    };

    [Fact]
    public void Ticket_negativo_MarcarDevolucion_queda_REFUNDED_anulado_y_en_cero()
    {
        var lineas = Lineas(Devolucion);
        var ticket = VinsonImportes.CalcularTicket(Cabecera(0m, 0m), null, lineas, TratamientoTicketNegativo.MarcarDevolucion);

        Assert.True(ticket.EsDevolucion);
        Assert.Equal("REFUNDED", ticket.Estado);
        Assert.True(ticket.EstaAnulado);
        Assert.Equal(0m, ticket.ImporteBruto);
        Assert.Equal(0m, ticket.ImporteNeto);
    }

    [Fact]
    public void Ticket_negativo_ImporteCero_queda_cerrado_en_cero()
    {
        var ticket = VinsonImportes.CalcularTicket(Cabecera(0m, 0m), null, Lineas(Devolucion), TratamientoTicketNegativo.ImporteCero);

        Assert.True(ticket.EsDevolucion);
        Assert.Equal("CLOSED", ticket.Estado);
        Assert.False(ticket.EstaAnulado);
        Assert.Equal(0m, ticket.ImporteNeto);
    }

    [Fact]
    public void Ticket_negativo_Restar_mantiene_el_comportamiento_anterior()
    {
        var ticket = VinsonImportes.CalcularTicket(Cabecera(0m, 0m), null, Lineas(Devolucion), TratamientoTicketNegativo.Restar);

        Assert.Equal("CLOSED", ticket.Estado);
        Assert.Equal("LINEAS", ticket.OrigenImportes);
        Assert.Equal(-90000m, ticket.ImporteBruto);
        Assert.Equal(-90000m, ticket.ImporteNeto);
    }

    [Fact]
    public void Ticket_normal_no_se_considera_devolucion()
    {
        Assert.False(VinsonImportes.EsDevolucion(Lineas(Ticket213529)));
        Assert.False(VinsonImportes.EsDevolucion(new List<VinsonLineaCalculada>()));
        Assert.False(VinsonImportes.EsDevolucion(Lineas(P("Cocido", 1, 0m, 0m))));
    }

    [Fact]
    public void Sin_cabecera_usa_el_detalle_y_despues_las_lineas()
    {
        var lineas = Lineas(P("Sand Veggie", 2, 24000m, 19834.71m));

        var conDetalle = VinsonImportes.CalcularTicket(Cabecera(null, null),
            new VinsonSalesFullInfo { TotalAmount = 48000m, NetAmount = 39669.42m }, lineas, TratamientoTicketNegativo.MarcarDevolucion);
        Assert.Equal("DETALLE", conDetalle.OrigenImportes);
        Assert.Equal(39669.42m, conDetalle.ImporteNeto);

        var soloLineas = VinsonImportes.CalcularTicket(Cabecera(null, null), null, lineas, TratamientoTicketNegativo.MarcarDevolucion);
        Assert.Equal("LINEAS", soloLineas.OrigenImportes);
        Assert.Equal(48000m, soloLineas.ImporteBruto);
        Assert.Equal(39669.42m, soloLineas.ImporteNeto);
    }

    [Theory]
    [InlineData(0, "OPEN")]
    [InlineData(3, "CLOSED")]
    [InlineData(5, "VOIDED")]
    public void Estado_se_mapea_desde_el_codigo_de_Vinson(int codigo, string esperado)
    {
        Assert.Equal(esperado, VinsonImportes.MapearEstado(codigo));
        var ticket = VinsonImportes.CalcularTicket(Cabecera(100m, 82.64m, codigo), null, new List<VinsonLineaCalculada>(), TratamientoTicketNegativo.MarcarDevolucion);
        Assert.Equal(codigo == 5, ticket.EstaAnulado);
    }
}
