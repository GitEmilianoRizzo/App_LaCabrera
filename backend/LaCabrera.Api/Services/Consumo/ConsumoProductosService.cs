using LaCabrera.Api.Repositories;

namespace LaCabrera.Api.Services.Consumo;

// ---- Datos crudos (SQL) ----

public class ConsumoLineaCruda
{
    public int FranquiciaId { get; set; }
    public DateTime FechaNegocio { get; set; }
    public string Nombre { get; set; } = "";
    public string Categoria { get; set; } = "";
    public string Familia { get; set; } = "";
    public decimal Cantidad { get; set; }
    public decimal CantidadCobrada { get; set; }
    public decimal Importe { get; set; }
}

public class ConsumoDiaCrudo
{
    public int FranquiciaId { get; set; }
    public DateTime FechaNegocio { get; set; }
    public int Tickets { get; set; }
    public int Cubiertos { get; set; }
    public decimal VentaNeta { get; set; }
    public decimal? UnidadesPorUsd { get; set; }
    public decimal? ArsPorUsd { get; set; }
}

public class ConsumoFranquiciaCruda
{
    public int FranquiciaId { get; set; }
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string? Pais { get; set; }
    public string? Ciudad { get; set; }
    public string Moneda { get; set; } = "";
}

public record ConsumoDatosCrudos(
    IReadOnlyList<ConsumoLineaCruda> Lineas, IReadOnlyList<ConsumoDiaCrudo> Dias, IReadOnlyList<ConsumoFranquiciaCruda> Franquicias);

// ---- Respuesta (compacta: arrays con índices para que el rango de 2-3 meses viaje liviano) ----

public record ConsumoFranquiciaDto(
    int Id, string Codigo, string Nombre, string? Pais, string? Ciudad, string Moneda, bool SinNombres, string? Motivo);

public record ConsumoProductoDto(string Rubro, string Producto);

/// <summary>UsdTotal = venta de los tickets = UsdClasificado + UsdSinClasificar + UsdNoProducto + UsdAjuste.</summary>
public record ConsumoCoberturaDto(decimal UsdTotal, decimal UsdClasificado, decimal UsdSinClasificar, decimal UsdNoProducto, decimal UsdAjuste);

public record ConsumoProductosDto(
    string Desde,
    string Hasta,
    string? FechaMaximaDatos,
    IReadOnlyList<string> Fechas,
    IReadOnlyList<ConsumoFranquiciaDto> Franquicias,
    IReadOnlyList<ConsumoProductoDto> Productos,
    /// <summary>[fecha, franquicia, tickets, cubiertos, venta USD, venta ARS] (índices sobre Fechas y Franquicias).</summary>
    IReadOnlyList<decimal[]> Dias,
    /// <summary>[fecha, franquicia, producto, unidades cobradas, unidades equivalentes, kg, USD, ARS, cantidad POS].</summary>
    IReadOnlyList<decimal[]> Filas,
    ConsumoCoberturaDto Cobertura);

public interface IConsumoProductosService
{
    Task<ConsumoProductosDto> GetAsync(DateTime? desde, DateTime? hasta, int? franquiciaId);
}

public class ConsumoProductosService : IConsumoProductosService
{
    public const int DiasPorDefecto = 60;
    public const int DiasMaximos = 400;

    private readonly IConsumoProductosRepository _repo;

    public ConsumoProductosService(IConsumoProductosRepository repo) => _repo = repo;

    public async Task<ConsumoProductosDto> GetAsync(DateTime? desde, DateTime? hasta, int? franquiciaId)
    {
        var max = await _repo.GetFechaMaximaAsync();
        var h = (hasta ?? max ?? DateTime.Today).Date;
        var d = (desde ?? h.AddDays(-(DiasPorDefecto - 1))).Date;
        if (d > h) (d, h) = (h, d);
        if ((h - d).TotalDays >= DiasMaximos) d = h.AddDays(-(DiasMaximos - 1));

        var crudos = await _repo.GetDatosAsync(d, h, franquiciaId);
        return ConsumoProductosAgregador.Agregar(crudos, d, h, max);
    }
}

/// <summary>
/// Clasifica las líneas en productos canónicos y las agrega por día, franquicia y producto.
/// Criterios (los mismos del análisis para la Central):
/// - Venta: las líneas se escalan al neto del ticket de cada franquicia y día (sin impuestos ni servicio), así el total
///   coincide con el Dashboard aunque el POS mande las líneas con impuesto (Colombia). Se pasa a USD con la cotización del día.
/// - Unidades: solo las líneas cobradas; lo que va sin cargo (incluido en menús, cortesías, instrucciones) no suma.
///   Unidades equivalentes: media porción = 0,5 y empanadas por cantidad (x2, x4). Kg: solo carnes con gramaje en el nombre.
/// - Tickets y cubiertos: los mismos de Alertas (vw_VentasResumenDiario: tickets no anulados y SUM(CantidadCubiertos)).
/// - Cantidad POS: SUM(Cantidad) de las líneas no anuladas, sin filtrar nada; cruza con el detalle de los tickets.
/// - Auditable: no se descarta nada. Lo que no se reconoce queda en "SIN CLASIFICAR", propinas/ajustes/modificadores en
///   "NO PRODUCTO" y la diferencia entre las líneas y el neto de los tickets (redondeos, días sin detalle, factor fuera de
///   rango) en "AJUSTE A TICKETS". Por franquicia y día, la suma de las filas en USD y ARS es igual a la venta del día.
/// </summary>
public static class ConsumoProductosAgregador
{
    public const decimal UmbralSinClasificar = 0.5m;
    public const string RubroAjuste = "AJUSTE A TICKETS";
    public const string ProductoAjuste = "Diferencia entre líneas y neto de tickets";
    public const string ProductoSinDetalle = "Tickets sin detalle de productos";
    private const decimal FactorMinimo = 0.5m, FactorMaximo = 1.5m;

    public static ConsumoProductosDto Agregar(ConsumoDatosCrudos crudos, DateTime desde, DateTime hasta, DateTime? fechaMaxima)
    {
        var fechas = new List<string>();
        var idxFecha = new Dictionary<DateTime, int>();
        for (var f = desde.Date; f <= hasta.Date; f = f.AddDays(1))
        {
            idxFecha[f] = fechas.Count;
            fechas.Add(f.ToString("yyyy-MM-dd"));
        }

        var dias = crudos.Dias.ToDictionary(x => (x.FranquiciaId, x.FechaNegocio.Date));

        // Factor por franquicia y día: neto de tickets / suma de líneas.
        var sumaLineas = crudos.Lineas.GroupBy(l => (l.FranquiciaId, l.FechaNegocio.Date)).ToDictionary(g => g.Key, g => g.Sum(l => l.Importe));
        decimal Factor((int, DateTime) k)
        {
            if (!dias.TryGetValue(k, out var dia) || !sumaLineas.TryGetValue(k, out var s) || s <= 0 || dia.VentaNeta <= 0) return 1m;
            var f = dia.VentaNeta / s;
            return f is >= FactorMinimo and <= FactorMaximo ? f : 1m;
        }

        var cache = new Dictionary<(string, string, string), ProductoClasificado>();
        var productos = new List<ConsumoProductoDto>();
        var idxProducto = new Dictionary<(string, string), int>();
        int Producto(string rubro, string producto)
        {
            if (idxProducto.TryGetValue((rubro, producto), out var i)) return i;
            idxProducto[(rubro, producto)] = i = productos.Count;
            productos.Add(new ConsumoProductoDto(rubro, producto));
            return i;
        }
        var acum = new Dictionary<(int Fecha, int Fr, int Prod), decimal[]>(); // unidades, eq, kg, usd, ars, cantidad POS
        var usdPorFr = new Dictionary<int, (decimal Total, decimal SinClasificar)>();

        foreach (var l in crudos.Lineas)
        {
            var clave = (l.FranquiciaId, l.FechaNegocio.Date);
            if (!idxFecha.TryGetValue(clave.Item2, out var fi)) continue;
            dias.TryGetValue(clave, out var dia);
            var tc = dia?.UnidadesPorUsd is > 0 ? dia.UnidadesPorUsd.Value : (decimal?)null;
            var k = (l.Nombre, l.Categoria, l.Familia);
            if (!cache.TryGetValue(k, out var c)) cache[k] = c = ClasificadorProductos.Clasificar(l.Nombre, l.Categoria, l.Familia);

            var pi = Producto(c.Rubro, c.Producto);
            var a = acum.TryGetValue((fi, l.FranquiciaId, pi), out var x) ? x : acum[(fi, l.FranquiciaId, pi)] = new decimal[6];
            a[5] += l.Cantidad; // la cantidad se cuenta aunque el día no tenga cotización
            if (tc is null) continue; // sin cotización no hay USD (el día tampoco suma venta USD): no se inventa

            var usd = l.Importe * Factor(clave) / tc.Value;
            var (tot, sc) = usdPorFr.GetValueOrDefault(l.FranquiciaId);
            usdPorFr[l.FranquiciaId] = (tot + usd, sc + (c.Rubro == ClasificadorProductos.RubroSinClasificar ? usd : 0));

            if (c.Rubro != ClasificadorProductos.RubroNoProducto)
            {
                var cobradas = l.Importe > 0 ? l.CantidadCobrada : 0;
                a[0] += cobradas;
                a[1] += cobradas * (c.UnidadesPorItem ?? (c.EsMediaPorcion ? 0.5m : 1m));
                if (c.Gramos is int g && c.Rubro == "CARNES VACUNAS") a[2] += cobradas * g / 1000m;
            }
            a[3] += usd;
            if (dia?.ArsPorUsd is > 0) a[4] += usd * dia.ArsPorUsd.Value;
        }

        // POS que mandan códigos en vez de nombres (p. ej. AyresIT): se incluyen igual, con aviso; su venta queda en SIN CLASIFICAR.
        var sinNombres = usdPorFr.Where(kv => kv.Value.Total > 0 && kv.Value.SinClasificar / kv.Value.Total > UmbralSinClasificar)
            .Select(kv => kv.Key).ToHashSet();

        var franquicias = crudos.Franquicias.OrderBy(f => f.Pais).ThenBy(f => f.Codigo).ToList();
        var idxFr = franquicias.Select((f, i) => (f.FranquiciaId, i)).ToDictionary(t => t.FranquiciaId, t => t.i);

        // Filas redondeadas; después, por franquicia y día, el ajuste lleva la suma exacta a la venta del día.
        var filas = acum.Where(kv => idxFr.ContainsKey(kv.Key.Fr))
            .Select(kv => new[]
            {
                kv.Key.Fecha, kv.Key.Fr, kv.Key.Prod,
                Math.Round(kv.Value[0], 2), Math.Round(kv.Value[1], 2), Math.Round(kv.Value[2], 3),
                Math.Round(kv.Value[3], 2), Math.Round(kv.Value[4], 0), Math.Round(kv.Value[5], 2),
            })
            .ToList();
        var sumaFilas = filas.GroupBy(r => ((int)r[0], (int)r[1])).ToDictionary(g => g.Key, g => (Usd: g.Sum(r => r[6]), Ars: g.Sum(r => r[7])));

        var diasDto = new List<decimal[]>();
        foreach (var x in crudos.Dias.Where(x => idxFecha.ContainsKey(x.FechaNegocio.Date) && idxFr.ContainsKey(x.FranquiciaId)))
        {
            var fi = idxFecha[x.FechaNegocio.Date];
            var cub = x.Cubiertos;
            var usd = x.UnidadesPorUsd is > 0 ? Math.Round(x.VentaNeta / x.UnidadesPorUsd.Value, 2) : 0;
            var ars = x.ArsPorUsd is > 0 ? Math.Round(x.VentaNeta / x.UnidadesPorUsd!.Value * x.ArsPorUsd.Value, 0) : 0;
            diasDto.Add(new[] { fi, idxFr[x.FranquiciaId], x.Tickets, cub, usd, ars });

            // Día con tickets pero sin líneas (el POS no mandó el detalle): toda la venta va a "sin detalle", a la vista.
            var conLineas = sumaFilas.TryGetValue((fi, x.FranquiciaId), out var sf);
            var (su, sa) = sf;
            if (usd - su != 0 || ars - sa != 0)
                filas.Add(new[] { fi, x.FranquiciaId, Producto(RubroAjuste, conLineas ? ProductoAjuste : ProductoSinDetalle), 0, 0, 0, usd - su, ars - sa, 0 });
        }
        diasDto.Sort((a, b) => a[0] != b[0] ? a[0].CompareTo(b[0]) : a[1].CompareTo(b[1]));

        var filasDto = filas
            .Select(r => { var c = (decimal[])r.Clone(); c[1] = idxFr[(int)r[1]]; return c; })
            .OrderBy(r => r[0]).ThenBy(r => r[1]).ThenBy(r => r[2])
            .ToList();

        var frDto = franquicias.Select(f => new ConsumoFranquiciaDto(
            f.FranquiciaId, f.Codigo, f.Nombre, f.Pais, f.Ciudad, f.Moneda, sinNombres.Contains(f.FranquiciaId),
            sinNombres.Contains(f.FranquiciaId) ? "El POS informa códigos en lugar de nombres de producto: su venta queda en Sin clasificar" : null)).ToList();

        decimal Suma(Func<string, bool> rubro) => filasDto.Where(r => rubro(productos[(int)r[2]].Rubro)).Sum(r => r[6]);
        var usdSinClasificar = Suma(r => r == ClasificadorProductos.RubroSinClasificar);
        var usdNoProducto = Suma(r => r == ClasificadorProductos.RubroNoProducto);
        var usdAjuste = Suma(r => r == RubroAjuste);
        var usdTotal = diasDto.Sum(d => d[4]);
        return new ConsumoProductosDto(
            desde.ToString("yyyy-MM-dd"), hasta.ToString("yyyy-MM-dd"), fechaMaxima?.ToString("yyyy-MM-dd"),
            fechas, frDto, productos, diasDto, filasDto,
            new ConsumoCoberturaDto(usdTotal, usdTotal - usdSinClasificar - usdNoProducto - usdAjuste, usdSinClasificar, usdNoProducto, usdAjuste));
    }
}
