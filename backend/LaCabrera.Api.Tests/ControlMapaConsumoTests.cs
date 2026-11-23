using LaCabrera.Api.Services.Consumo;

namespace LaCabrera.Api.Tests;

public class ControlMapaConsumoTests
{
    private static ControlFranquiciaDatos Datos(Action<ControlFranquiciaDatos>? ajustar = null)
    {
        var d = new ControlFranquiciaDatos
        {
            FranquiciaId = 1, Codigo = "NUEVA", Nombre = "Nueva", Pais = "Espana", Moneda = "EUR",
            PaisIso = "724", ZonaMapa = "Málaga", Latitud = 36.72m, Longitud = -4.42m, NodoActivo = true,
            DiasConVentas = 30, DiasConLineas = 30, DiasSinCotizacion = 0, Tickets = 1000, TicketsConCubiertos = 1000
        };
        ajustar?.Invoke(d);
        return d;
    }

    private static ControlLineaCruda L(string nombre, decimal importe, string familia = "") =>
        new() { FranquiciaId = 1, Nombre = nombre, Familia = familia, Importe = importe, Cantidad = 1 };

    private static readonly List<ControlLineaCruda> LineasBuenas = new()
    {
        L("OJO DE BIFE", 500), L("BIFE DE CHORIZO", 300), L("PAPAS FRITAS", 100), L("COCA COLA", 100)
    };

    private static string Estado(ControlFranquiciaDto r, string chequeo) => r.Chequeos.Single(c => c.Codigo == chequeo).Estado;

    [Fact]
    public void Franquicia_que_cumple_la_regla_da_ok()
    {
        var r = ReglaMapaConsumo.Evaluar(Datos(), LineasBuenas);
        Assert.Equal(ReglaMapaConsumo.Ok, r.Estado);
        Assert.Equal(100m, r.PctClasificado);
        Assert.Empty(r.SinClasificar);
    }

    [Fact]
    public void Sin_ubicacion_es_error_aunque_no_tenga_ventas()
    {
        var r = ReglaMapaConsumo.Evaluar(Datos(d => { d.Latitud = null; d.DiasConVentas = 0; d.Tickets = 0; }), new List<ControlLineaCruda>());
        Assert.Equal(ReglaMapaConsumo.Error, r.Estado);
        Assert.Equal(ReglaMapaConsumo.Error, Estado(r, "UBICACION"));
    }

    [Fact]
    public void Sin_ventas_y_con_ubicacion_queda_sin_datos()
    {
        var r = ReglaMapaConsumo.Evaluar(Datos(d => { d.DiasConVentas = 0; d.Tickets = 0; }), new List<ControlLineaCruda>());
        Assert.Equal(ReglaMapaConsumo.SinDatos, r.Estado);
    }

    [Fact]
    public void Dias_sin_cotizacion_es_error()
    {
        var r = ReglaMapaConsumo.Evaluar(Datos(d => { d.Moneda = "PHP"; d.DiasSinCotizacion = 5; }), LineasBuenas);
        Assert.Equal(ReglaMapaConsumo.Error, Estado(r, "COTIZACION"));
    }

    [Fact]
    public void Nombres_sin_clasificar_dan_atencion_y_se_listan()
    {
        var lineas = new List<ControlLineaCruda>(LineasBuenas) { L("XQZ 4471", 150), L("XQZ 4471", 50) };
        var r = ReglaMapaConsumo.Evaluar(Datos(), lineas);
        Assert.Equal(ReglaMapaConsumo.Atencion, Estado(r, "CLASIFICACION"));
        var n = Assert.Single(r.SinClasificar);
        Assert.Equal(("XQZ 4471", 200m), (n.Nombre, n.Importe));
        Assert.Equal(83.3m, r.PctClasificado);
    }

    [Fact]
    public void Pos_que_manda_codigos_es_error()
    {
        var lineas = new List<ControlLineaCruda> { L("10023", 800), L("10045", 700), L("OJO DE BIFE", 100) };
        var r = ReglaMapaConsumo.Evaluar(Datos(), lineas);
        Assert.Equal(ReglaMapaConsumo.Error, Estado(r, "CLASIFICACION"));
        Assert.Equal(ReglaMapaConsumo.Error, r.Estado);
    }

    [Fact]
    public void Dias_sin_detalle_y_sin_cubiertos_dan_atencion()
    {
        var r = ReglaMapaConsumo.Evaluar(Datos(d => { d.DiasConLineas = 20; d.TicketsConCubiertos = 100; }), LineasBuenas);
        Assert.Equal(ReglaMapaConsumo.Atencion, Estado(r, "DETALLE"));
        Assert.Equal(ReglaMapaConsumo.Atencion, Estado(r, "CUBIERTOS"));
        Assert.Equal(ReglaMapaConsumo.Atencion, r.Estado);
    }

    [Fact]
    public void Mensaje_de_activacion_sin_ubicacion_nombra_la_franquicia()
    {
        Assert.Contains("NUEVA", ReglaMapaConsumo.MensajeSinUbicacion("NUEVA"));
    }
}
