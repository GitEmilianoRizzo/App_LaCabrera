using LaCabrera.Api.Services.Consumo;

namespace LaCabrera.Api.Tests;

public class ClasificadorProductosTests
{
    [Theory]
    [InlineData("OJO DE BIFE 400GR", "CARNES VACUNAS", "Ojo de bife / rib eye")]
    [InlineData("Ribeye 14 oz", "CARNES VACUNAS", "Ojo de bife / rib eye")]
    [InlineData("BIFE ANCHO", "CARNES VACUNAS", "Ojo de bife / rib eye")]
    [InlineData("ENTRA±A", "CARNES VACUNAS", "Entraña / skirt")]
    [InlineData("Skirt Steak", "CARNES VACUNAS", "Entraña / skirt")]
    [InlineData("NEW YORK STRIP", "CARNES VACUNAS", "Bife de chorizo / NY strip")]
    [InlineData("Tomahawk 1 KG", "CARNES VACUNAS", "Cortes con hueso (T-bone, tomahawk, chuletón)")]
    [InlineData("PICANHA", "CARNES VACUNAS", "Picanha")]
    [InlineData("Provoleta con tocino", "ENTRADAS", "Provoleta")]
    [InlineData("Empanada de carne x4", "ENTRADAS", "Empanadas")]
    [InlineData("Copa Malbec", "VINO", "Vino por copa")]
    [InlineData("BT CATENA MALBEC", "VINO", "Vino por botella")]
    [InlineData("Pulpo a la parrilla", "PESCADOS Y MARISCOS", "Pulpo")]
    [InlineData("Langostinos al ajillo", "PESCADOS Y MARISCOS", "Langostinos / camarones / langosta")]
    [InlineData("Papas fritas", "GUARNICIONES", "Papas fritas")]
    [InlineData("Sandwich de ojo de bife", "SÁNDWICHES Y HAMBURGUESAS", "Sándwich de ojo de bife")]
    [InlineData("Cerveza Stella Artois", "CERVEZA", "Cerveza")]
    [InlineData("Chardonnay Catena", "VINO", "Vino por botella")]
    [InlineData("Pollito de campo", "POLLO", "Pollo")]
    [InlineData("Mojito", "COCTELERÍA Y DESTILADOS", "Cócteles clásicos")]
    [InlineData("A PUNTO", "NO PRODUCTO", "Modificadores e instrucciones de cocina")]
    [InlineData("Gorjeta", "NO PRODUCTO", "Propina / servicio")]
    [InlineData("Calentitos", "CORTESÍAS (SIN CARGO)", "Cortesías de la casa")]
    public void Lleva_cada_nombre_del_POS_a_su_producto_canonico(string nombre, string rubro, string producto)
    {
        var c = ClasificadorProductos.Clasificar(nombre);
        Assert.Equal(rubro, c.Rubro);
        Assert.Equal(producto, c.Producto);
    }

    [Fact]
    public void Falsos_positivos_corregidos_en_el_analisis()
    {
        Assert.NotEqual("CERVEZA", ClasificadorProductos.Clasificar("Ensalada americana").Rubro);   // CANA ⊂ AMERICANA
        Assert.Equal("ENSALADAS", ClasificadorProductos.Clasificar("Ensalada de tomate rojo").Rubro); // OJO ⊂ ROJO
        Assert.NotEqual("NO PRODUCTO", ClasificadorProductos.Clasificar("Cortesía").Rubro);          // CORTE ⊂ CORTESIA
        Assert.Equal("Cortesías de la casa", ClasificadorProductos.Clasificar("Cortesia").Producto);
    }

    [Fact]
    public void Combo_cuenta_el_plato_principal_y_guarda_atributos()
    {
        var c = ClasificadorProductos.Clasificar("1/2 Ojo de bife Wagyu + papas fritas");
        Assert.Equal("Ojo de bife / rib eye", c.Producto);
        Assert.True(c.EsCombo);
        Assert.True(c.EsMediaPorcion);
        Assert.Equal("WAGYU", c.Premium);
    }

    [Theory]
    [InlineData("OJO DE BIFE 400GR", 400)]
    [InlineData("RIBEYE 14 OZ", 397)]
    [InlineData("TOMAHAWK 1 KG", 1000)]
    [InlineData("ENTRAÑA", null)]
    public void Gramos_salen_del_nombre(string nombre, int? gramos)
        => Assert.Equal(gramos, ClasificadorProductos.Clasificar(nombre).Gramos);

    [Fact]
    public void Respaldo_por_categoria_cuando_el_nombre_no_matchea()
    {
        Assert.Equal("VINO", ClasificadorProductos.Clasificar("Zzz 2019", "WINE").Rubro);
        Assert.Equal("SIN CLASIFICAR", ClasificadorProductos.Clasificar("10234").Rubro);
    }
}

public class ConsumoProductosAgregadorTests
{
    private static readonly DateTime D1 = new(2026, 10, 1), D2 = new(2026, 10, 2);

    private static ConsumoLineaCruda L(int fr, DateTime f, string nombre, decimal cant, decimal importe, decimal? cobrada = null) =>
        new() { FranquiciaId = fr, FechaNegocio = f, Nombre = nombre, Cantidad = cant, CantidadCobrada = cobrada ?? cant, Importe = importe };

    private static ConsumoDiaCrudo Dia(int fr, DateTime f, decimal neto, decimal tc, int cub = 10) =>
        new() { FranquiciaId = fr, FechaNegocio = f, Tickets = 5, Cubiertos = cub, VentaNeta = neto, UnidadesPorUsd = tc, ArsPorUsd = 1000 };

    private static readonly ConsumoFranquiciaCruda[] Frs =
    {
        new() { FranquiciaId = 1, Codigo = "BOGOTA", Pais = "Colombia", Ciudad = "Bogota", Moneda = "COP" },
        new() { FranquiciaId = 2, Codigo = "PALERMO", Pais = "Argentina", Ciudad = "Buenos Aires", Moneda = "ARS" },
    };

    [Fact]
    public void Escala_las_lineas_al_neto_del_ticket_y_convierte_a_usd_y_ars()
    {
        // Colombia: las líneas traen impuesto (1.190.000) y el ticket neto es 1.000.000 → factor 0,84.
        var crudos = new ConsumoDatosCrudos(
            new[] { L(1, D1, "OJO DE BIFE 400GR", 2, 714_000), L(1, D1, "Papas fritas", 1, 476_000) },
            new[] { Dia(1, D1, 1_000_000, 4_000) }, Frs);

        var r = ConsumoProductosAgregador.Agregar(crudos, D1, D2, D2);

        Assert.Equal(new[] { "2026-10-01", "2026-10-02" }, r.Fechas);
        var ojo = r.Productos.ToList().FindIndex(p => p.Producto == "Ojo de bife / rib eye");
        var fila = r.Filas.Single(f => f[2] == ojo);
        Assert.Equal(2, fila[3]);          // unidades
        Assert.Equal(0.8m, fila[5]);       // kg = 2 × 400 g
        Assert.Equal(150m, fila[6]);       // 714.000 × 0,8403 / 4.000
        Assert.Equal(150_000m, fila[7]);   // ARS
        Assert.Equal(250m, r.Filas.Sum(f => f[6])); // total = neto del ticket en USD
        var bogota = r.Franquicias.ToList().FindIndex(f => f.Codigo == "BOGOTA");
        Assert.Equal(new decimal[] { 0, bogota, 5, 10, 250, 250_000 }, r.Dias.Single());
    }

    [Fact]
    public void Lineas_sin_cargo_no_suman_unidades_y_media_porcion_vale_medio()
    {
        var crudos = new ConsumoDatosCrudos(
            new[] { L(1, D1, "1/2 ENTRAÑA", 2, 100, cobrada: 2), L(1, D1, "ENTRAÑA", 3, 0, cobrada: 0) },
            new[] { Dia(1, D1, 100, 1) }, Frs);

        var fila = ConsumoProductosAgregador.Agregar(crudos, D1, D1, D1).Filas.Single();

        Assert.Equal(2, fila[3]);
        Assert.Equal(1, fila[4]);
    }

    [Fact]
    public void Franquicia_con_codigos_en_vez_de_nombres_queda_en_sin_clasificar_y_avisada()
    {
        var crudos = new ConsumoDatosCrudos(
            new[] { L(1, D1, "PICANHA", 1, 100), L(2, D1, "10234", 4, 900), L(2, D1, "OJO DE BIFE", 1, 100) },
            new[] { Dia(1, D1, 100, 1), Dia(2, D1, 1000, 1) }, Frs);

        var r = ConsumoProductosAgregador.Agregar(crudos, D1, D1, D1);

        var palermo = r.Franquicias.ToList().FindIndex(f => f.Codigo == "PALERMO");
        Assert.True(r.Franquicias[palermo].SinNombres);
        var sinClasificar = r.Productos.ToList().FindIndex(p => p.Rubro == "SIN CLASIFICAR");
        Assert.Equal(900m, r.Filas.Where(f => f[1] == palermo && f[2] == sinClasificar).Sum(f => f[6]));
        Assert.Equal(900m, r.Cobertura.UsdSinClasificar);
        Assert.Equal(1100m, r.Cobertura.UsdTotal);
    }

    [Fact]
    public void Todo_suma_la_venta_de_los_tickets_por_franquicia_y_dia()
    {
        // Propina (no producto), un nombre desconocido, un día sin líneas y un factor fuera de rango: todo queda a la vista.
        var crudos = new ConsumoDatosCrudos(
            new[] { L(1, D1, "PROPINA", 1, 10), L(1, D1, "ENTRAÑA", 1, 50), L(1, D1, "XYZ 123", 1, 40), L(1, D2, "ENTRAÑA", 1, 10) },
            new[] { Dia(1, D1, 100.004m, 3), Dia(1, D2, 300, 3), Dia(2, D2, 50, 1) }, Frs);

        var r = ConsumoProductosAgregador.Agregar(crudos, D1, D2, D2);

        foreach (var d in r.Dias)
        {
            var filas = r.Filas.Where(f => f[0] == d[0] && f[1] == d[1]).ToList();
            Assert.Equal(d[4], filas.Sum(f => f[6]));
            Assert.Equal(d[5], filas.Sum(f => f[7]));
        }
        var c = r.Cobertura;
        Assert.Equal(c.UsdTotal, c.UsdClasificado + c.UsdSinClasificar + c.UsdNoProducto + c.UsdAjuste);
        Assert.True(c.UsdNoProducto > 0);
        Assert.True(c.UsdAjuste > 0); // D2: líneas 10 vs tickets 300 → factor fuera de rango; Palermo D2 sin líneas
    }

    [Fact]
    public void Cantidad_pos_suma_todas_las_lineas_y_cubiertos_son_los_de_alertas()
    {
        var crudos = new ConsumoDatosCrudos(
            new[] { L(1, D1, "EXPERIENCIA LA CABRERA", 7, 0), L(1, D1, "PICANHA", 3, 300), L(1, D1, "Calentitos", 2, 0), L(1, D1, "QQQ", 1, 5) },
            new[] { Dia(1, D1, 305, 1, cub: 0) }, Frs);

        var r = ConsumoProductosAgregador.Agregar(crudos, D1, D1, D1);

        Assert.Equal(0, r.Dias.Single()[3]);          // sin inventar cubiertos: lo mismo que Alertas
        Assert.Equal(13m, r.Filas.Sum(f => f[8]));    // 7 + 3 + 2 + 1
        Assert.Equal(4m, r.Filas.Sum(f => f[3]));     // cobradas: picanha 3 + QQQ 1
    }
}
