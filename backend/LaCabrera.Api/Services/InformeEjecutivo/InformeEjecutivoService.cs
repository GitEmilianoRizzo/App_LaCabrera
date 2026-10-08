using LaCabrera.Api.Repositories;
using Microsoft.Extensions.Options;
using static LaCabrera.Api.Services.InformeEjecutivo.Fmt;

namespace LaCabrera.Api.Services.InformeEjecutivo;

public interface IInformeEjecutivoService
{
    Task<InformeEjecutivoModel> CalcularAsync(DateTime baseDesde, DateTime baseHasta, DateTime compDesde, DateTime compHasta);
    Task<byte[]> GenerarPdfAsync(DateTime baseDesde, DateTime baseHasta, DateTime compDesde, DateTime compHasta, CancellationToken ct = default);
}

public class InformeEjecutivoService : IInformeEjecutivoService
{
    private static readonly Dictionary<string, string> NombresPais = new(StringComparer.OrdinalIgnoreCase)
    {
        ["USA"] = "EE.UU.", ["United States"] = "EE.UU.", ["Estados Unidos"] = "EE.UU.",
        ["Spain"] = "España", ["Espana"] = "España",
        ["Brazil"] = "Brasil", ["Mexico"] = "México", ["Peru"] = "Perú",
    };

    private readonly IInformeEjecutivoRepository _repository;
    private readonly IPdfRenderer _pdf;
    private readonly InformeEjecutivoSettings _settings;
    private readonly ILogger<InformeEjecutivoService> _logger;

    public InformeEjecutivoService(
        IInformeEjecutivoRepository repository,
        IPdfRenderer pdf,
        IOptions<InformeEjecutivoSettings> settings,
        ILogger<InformeEjecutivoService> logger)
    {
        _repository = repository;
        _pdf = pdf;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<byte[]> GenerarPdfAsync(DateTime baseDesde, DateTime baseHasta, DateTime compDesde, DateTime compHasta, CancellationToken ct = default)
    {
        var model = await CalcularAsync(baseDesde, baseHasta, compDesde, compHasta);
        var html = InformeEjecutivoHtml.Render(model);
        return await _pdf.RenderAsync(html, ct);
    }

    public async Task<InformeEjecutivoModel> CalcularAsync(DateTime baseDesde, DateTime baseHasta, DateTime compDesde, DateTime compHasta)
    {
        var (pBase, pComp) = CrearPeriodos(baseDesde.Date, baseHasta.Date, compDesde.Date, compHasta.Date);

        var filasBase = await _repository.GetVentasDiariasAsync(pBase.Desde, pBase.Hasta);
        var filasComp = await _repository.GetVentasDiariasAsync(pComp.Desde, pComp.Hasta);

        var info = filasBase.Concat(filasComp)
            .GroupBy(r => r.FranquiciaId)
            .ToDictionary(g => g.Key, g => g.First());

        var locales = info.Values
            .Select(r => new LocalInforme
            {
                FranquiciaId = r.FranquiciaId,
                Codigo = r.Codigo,
                Nombre = r.Nombre,
                Pais = r.Pais,
                Moneda = r.Moneda,
                EsPalermo = _settings.CodigosPalermo.Contains(r.Codigo, StringComparer.OrdinalIgnoreCase),
                EsAeropuerto = _settings.PrefijosAeropuerto.Any(p => r.Codigo.StartsWith(p, StringComparison.OrdinalIgnoreCase)),
                Base = Acumular(filasBase.Where(f => f.FranquiciaId == r.FranquiciaId)),
                Comp = Acumular(filasComp.Where(f => f.FranquiciaId == r.FranquiciaId)),
            })
            .OrderByDescending(l => l.EsPalermo)
            .ThenBy(l => l.EsPalermo ? "" : NombrePais(l.Pais))
            .ThenBy(l => l.Nombre)
            .ToList();

        var franquicias = locales.Where(l => !l.EsPalermo).ToList();
        var servicioCompleto = franquicias.Where(l => !l.EsAeropuerto).ToList();

        var model = new InformeEjecutivoModel
        {
            Base = pBase,
            Comp = pComp,
            GeneradoEl = DateTime.Now,
            Locales = locales,
            Total = new GrupoInforme { Nombre = "Total", Locales = locales },
            Palermo = new GrupoInforme { Nombre = "Palermo (N+S)", Locales = locales.Where(l => l.EsPalermo).ToList() },
            Franquicias = new GrupoInforme { Nombre = "Total franquicias", Locales = franquicias },
            ServicioCompleto = new GrupoInforme { Nombre = "Franquicias servicio completo", Locales = servicioCompleto },
            Aeropuertos = new GrupoInforme
            {
                Nombre = "Aeropuertos (" + Lista(franquicias.Where(l => l.EsAeropuerto).Select(l => l.Nombre).ToList()) + ")",
                Locales = franquicias.Where(l => l.EsAeropuerto).ToList()
            },
            Paises = servicioCompleto
                .GroupBy(l => NombrePais(l.Pais))
                .Select(g => new GrupoInforme
                {
                    Nombre = g.Count() > 1 ? $"{g.Key} ({g.Count()} locales)" : g.First().Nombre,
                    Locales = g.ToList()
                })
                .OrderByDescending(g => g.Comp.VentasUsd)
                .ToList(),
            TiposCambio = CalcularTiposCambio(filasBase, filasComp),
        };

        try
        {
            InformeEjecutivoTextos.Generar(model);
        }
        catch (Exception ex)
        {
            // Los textos son un agregado: si algo falla, el informe sale igual con los números.
            _logger.LogError(ex, "Error generando textos del informe ejecutivo");
        }

        var sinTc = locales.Where(l => l.Base.DiasSinTipoCambio + l.Comp.DiasSinTipoCambio > 0).ToList();
        if (sinTc.Count > 0)
        {
            _logger.LogWarning("Informe ejecutivo: días sin tipo de cambio en {Locales}", string.Join(", ", sinTc.Select(l => l.Codigo)));
            model.ParaRevisar.Add(
                $"<b>Días sin tipo de cambio:</b> {Html(Lista(sinTc.Select(l => l.Nombre).ToList()))} tienen ventas en fechas sin cotización cargada; " +
                "esas ventas no se incluyen en los totales en USD. Conviene completar los tipos de cambio y regenerar el informe.");
        }

        return model;
    }

    internal static string NombrePais(string? pais) =>
        string.IsNullOrWhiteSpace(pais) ? "Otros" : NombresPais.TryGetValue(pais, out var n) ? n : pais;

    private static Metricas Acumular(IEnumerable<InformeVentaDiaRow> filas)
    {
        var m = new Metricas();
        foreach (var f in filas)
        {
            m.VentasLocal += f.NetoLocal;
            m.BrutoLocal += f.BrutoLocal;
            m.DescuentoLocal += f.DescuentoLocal;
            m.PropinaLocal += f.PropinaLocal;
            m.Cubiertos += f.Cubiertos;
            m.Tickets += f.Tickets;
            m.Reembolsos += f.Reembolsos;
            if (f.Tickets > 0) m.DiasOperados++;

            if (f.UnidadesPorUsd is > 0 && f.UnidadesPorUsd is { } tc)
                m.VentasUsd += f.NetoLocal / tc;
            else
                m.DiasSinTipoCambio++;
        }
        return m;
    }

    private static List<TipoCambioInforme> CalcularTiposCambio(IReadOnlyList<InformeVentaDiaRow> filasBase, IReadOnlyList<InformeVentaDiaRow> filasComp)
    {
        static Dictionary<string, List<InformeVentaDiaRow>> PorMoneda(IEnumerable<InformeVentaDiaRow> filas) =>
            filas.Where(f => f.Moneda != "USD" && f.UnidadesPorUsd is > 0)
                 .GroupBy(f => f.Moneda)
                 // Un valor por día con ventas (promedio simple de las cotizaciones diarias)
                 .ToDictionary(g => g.Key, g => g.GroupBy(f => f.FechaNegocio).Select(d => d.First()).ToList());

        var b = PorMoneda(filasBase);
        var c = PorMoneda(filasComp);

        return b.Keys.Union(c.Keys).OrderBy(k => k).Select(moneda =>
        {
            b.TryGetValue(moneda, out var fb);
            c.TryGetValue(moneda, out var fc);
            var todas = (fb ?? new()).Concat(fc ?? new()).ToList();
            return new TipoCambioInforme
            {
                Moneda = moneda,
                PromedioBase = fb?.Count > 0 ? fb.Average(f => f.UnidadesPorUsd!.Value) : null,
                PromedioComp = fc?.Count > 0 ? fc.Average(f => f.UnidadesPorUsd!.Value) : null,
                EsFijo = todas.Count > 1 && (
                    todas.Any(f => string.Equals(f.TipoTasa, "MANUAL", StringComparison.OrdinalIgnoreCase))
                    || todas.Select(f => f.UnidadesPorUsd).Distinct().Count() == 1),
            };
        }).ToList();
    }

    private static (Periodo, Periodo) CrearPeriodos(DateTime bDesde, DateTime bHasta, DateTime cDesde, DateTime cHasta)
    {
        static bool MesCompleto(DateTime d, DateTime h) => d.Day == 1 && h == d.AddMonths(1).AddDays(-1);

        var ambosMeses = MesCompleto(bDesde, bHasta) && MesCompleto(cDesde, cHasta);
        var mismoAnio = bDesde.Year == cDesde.Year;

        Periodo Crear(DateTime d, DateTime h, string fallbackCorto, string fallbackTexto)
        {
            if (MesCompleto(d, h))
            {
                var mes = Mes(d.Month);
                return new Periodo
                {
                    Desde = d,
                    Hasta = h,
                    Nombre = $"{Capitalizar(mes)} {d.Year}",
                    Corto = ambosMeses
                        ? Capitalizar(mes[..3]) + (mismoAnio ? "" : $" {d:yy}")
                        : fallbackCorto,
                    EnTexto = mismoAnio ? mes : $"{mes} {d.Year}",
                };
            }
            return new Periodo
            {
                Desde = d,
                Hasta = h,
                Nombre = d.Year == h.Year ? $"{d:dd'/'MM} al {Fecha(h)}" : $"{Fecha(d)} al {Fecha(h)}",
                Corto = fallbackCorto,
                EnTexto = fallbackTexto,
            };
        }

        return (Crear(bDesde, bHasta, "P. inicial", "el período inicial"),
                Crear(cDesde, cHasta, "P. final", "el período final"));
    }
}
