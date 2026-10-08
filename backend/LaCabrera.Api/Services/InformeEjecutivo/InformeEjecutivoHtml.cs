using System.Reflection;
using System.Text;
using static LaCabrera.Api.Services.InformeEjecutivo.Fmt;

namespace LaCabrera.Api.Services.InformeEjecutivo;

/// <summary>
/// Arma el HTML del Informe Ejecutivo (5 páginas A4 apaisado, estética Pillow).
/// Logo, fuentes y CSS vienen embebidos en el ensamblado para no depender de internet.
/// </summary>
internal static class InformeEjecutivoHtml
{
    private const int TotalPaginas = 5;
    private const decimal EscalaIndice = 200m; // barras de índice: 0–200, línea de Palermo en 100

    private static readonly Lazy<string> Css = new(() => Recurso("informe.css")
        .Replace("__FONT_SANS__", DataUri("NotoSans.woff2", "font/woff2"))
        .Replace("__FONT_SANS_ITALIC__", DataUri("NotoSans-Italic.woff2", "font/woff2"))
        .Replace("__FONT_SERIF__", DataUri("NotoSerif-Bold.woff2", "font/woff2")));

    private static readonly Lazy<string> Logo = new(() => DataUri("logo_pillow.png", "image/png"));

    public static string Render(InformeEjecutivoModel m)
    {
        var compact = m.Locales.Count > 13 ? " compact" : "";
        var sb = new StringBuilder();
        sb.Append($"""
            <!doctype html><html lang="es"><head><meta charset="utf-8">
            <title>Resumen ejecutivo · {Html(m.Comp.Nombre)} vs {Html(m.Base.Nombre)} · Pillow</title>
            <style>{Css.Value}</style></head><body class="{compact.Trim()}">
            """);
        sb.Append(Pagina1(m));
        sb.Append(Pagina2(m));
        sb.Append(Pagina3(m));
        sb.Append(Pagina4(m));
        sb.Append(Pagina5(m));
        sb.Append("</body></html>");
        return sb.ToString();
    }

    // ------------------------------------------------------------------ comunes

    private static string Titulo(InformeEjecutivoModel m) => $"{m.Comp.Nombre} vs {m.Base.Nombre}";

    private static string Header(string eyebrow, string titulo, string subtitulo, bool grande = false) => $"""
        <header class="hdr {(grande ? "big" : "")}">
          <img class="logo" src="{Logo.Value}" alt="Pillow · Decisiones que descansan en datos">
          <div class="hdr-txt">
            <div class="eyebrow">{Html(eyebrow)}</div>
            <h1>{Html(titulo)}</h1>
            <div class="sub">{Html(subtitulo)}</div>
          </div>
        </header>
        <div class="lime-rule"></div>
        """;

    private static string Footer(InformeEjecutivoModel m, int pagina) => $"""
        <footer class="ftr">
          <span><b>Pillow</b> · Decisiones que descansan en datos</span>
          <span>Lic. Emiliano Rizzo · emiliano@pillow.com.ar · www.pillow.com.ar</span>
          <span><span class="gen">Generado {m.GeneradoEl:dd/MM/yyyy HH:mm} · </span><span class="pg">{pagina} / {TotalPaginas}</span></span>
        </footer>
        """;

    private static string Num(decimal? v, int dec = 0, string cls = "") => $"""<td class="num {cls}">{N(v, dec)}</td>""";

    private static string Delta(decimal? ratio, string cls = "") => $"""<td class="num {cls} {CssVar(ratio)}">{Pct(ratio)}</td>""";

    private static string Bullets(IEnumerable<string> items, string cls = "bul")
    {
        var lista = items.ToList();
        return lista.Count == 0
            ? """<p class="empty">Sin observaciones para el período.</p>"""
            : $"""<ul class="{cls}">{string.Concat(lista.Select(i => $"<li>{i}</li>"))}</ul>""";
    }

    private static string Pagina(string contenido) => $"""<section class="page">{contenido}</section>""";

    // ------------------------------------------------------------------ página 1

    private static string Pagina1(InformeEjecutivoModel m)
    {
        var b = m.Total.Base;
        var c = m.Total.Comp;
        string Kpi(string label, decimal? vb, decimal? vc, int dec) => $"""
            <div class="kpi"><div class="kpi-l">{label}</div><div class="kpi-v">{N(vc, dec)}</div>
            <div class="kpi-d"><span class="{CssVar(Var(vb, vc))}">{Pct(Var(vb, vc))}</span> vs {Html(m.Base.Corto.ToLowerInvariant())} ({N(vb, dec)})</div></div>
            """;

        var kpis = Kpi("Ventas netas USD", b.VentasUsd, c.VentasUsd, 0)
                 + Kpi("Cubiertos", b.Cubiertos, c.Cubiertos, 0)
                 + Kpi("USD / cubierto", b.UsdPorCubierto, c.UsdPorCubierto, 1)
                 + Kpi("Venta promedio diaria", b.VentasUsd / m.Base.Dias, c.VentasUsd / m.Comp.Dias, 0);

        var pb = m.Palermo.Base; var pc = m.Palermo.Comp;
        var fb = m.Franquicias.Base; var fc = m.Franquicias.Comp;
        var sb = m.ServicioCompleto.Base; var sc = m.ServicioCompleto.Comp;
        string Fila(string label, string celdas) => $"""<tr><td class="lbl">{label}</td>{celdas}</tr>""";
        string Trio(decimal? vb, decimal? vc, int dec, string sep = "") => Num(vb, dec, sep) + Num(vc, dec) + Delta(Var(vb, vc));
        var sharePb = b.VentasUsd > 0 ? pb.VentasUsd / b.VentasUsd : (decimal?)null;
        var sharePc = c.VentasUsd > 0 ? pc.VentasUsd / c.VentasUsd : (decimal?)null;

        var filas = Fila("Ventas USD", Trio(pb.VentasUsd, pc.VentasUsd, 0) + Trio(fb.VentasUsd, fc.VentasUsd, 0, "sep"))
                  + Fila("Cubiertos", Trio(pb.Cubiertos, pc.Cubiertos, 0) + Trio(fb.Cubiertos, fc.Cubiertos, 0, "sep"))
                  + Fila("$/cubierto", Trio(pb.UsdPorCubierto, pc.UsdPorCubierto, 1) + Trio(fb.UsdPorCubierto, fc.UsdPorCubierto, 1, "sep"))
                  + Fila("$/cub. franq. sin aeropuertos", $"""<td class="num">{Guion}</td><td class="num">{Guion}</td><td class="num">{Guion}</td>""" + Trio(sb.UsdPorCubierto, sc.UsdPorCubierto, 1, "sep"))
                  + Fila("Participación en ventas",
                        $"""<td class="num">{Share(sharePb)}</td><td class="num">{Share(sharePc)}</td><td></td>""" +
                        $"""<td class="num sep">{Share(1 - sharePb)}</td><td class="num">{Share(1 - sharePc)}</td><td></td>""");

        var bc = Html(m.Base.Corto); var cc = Html(m.Comp.Corto);
        return Pagina($"""
            {Header("Resumen ejecutivo", Titulo(m), "Ventas, cubiertos y $/cubierto — total, por local y franquicias vs Palermo · Ventas netas en USD (sin impuestos ni propinas)", grande: true)}
            <main>
              <div class="kpis">{kpis}</div>
              <p class="lead">{m.Resumen}</p>
              <h2>Puntos clave</h2>
              {Bullets(m.PuntosClave)}
              <h2>Franquicias vs Palermo</h2>
              <div class="split">
                <table class="t">
                  <thead><tr><th class="lbl">Indicador</th><th>Palermo {bc}</th><th>Palermo {cc}</th><th>Var.</th>
                    <th class="sep">Franq. {bc}</th><th>Franq. {cc}</th><th>Var.</th></tr></thead>
                  <tbody>{filas}</tbody>
                </table>
                {Bullets(m.FranquiciasVsPalermo, "bul side")}
              </div>
            </main>
            {Footer(m, 1)}
            """);
    }

    // ------------------------------------------------------------------ página 2

    private static string Pagina2(InformeEjecutivoModel m)
    {
        string Fila(string nombre, Metricas b, Metricas c, string cls = "") =>
            $"""<tr class="{cls}"><td class="lbl">{Html(nombre)}</td>""" +
            Num(b.VentasUsd) + Num(c.VentasUsd) + Delta(Var(b.VentasUsd, c.VentasUsd)) +
            Num(b.Cubiertos, 0, "sep") + Num(c.Cubiertos) + Delta(Var(b.Cubiertos, c.Cubiertos)) +
            Num(b.UsdPorCubierto, 1, "sep") + Num(c.UsdPorCubierto, 1) + Delta(Var(b.UsdPorCubierto, c.UsdPorCubierto)) +
            "</tr>";

        var filas = new StringBuilder(Fila("Total", m.Total.Base, m.Total.Comp, "total"));
        foreach (var (grupo, locales) in new[] { ("Palermo", m.Palermo.Locales), ("Franquicias", m.Franquicias.Locales) })
        {
            if (locales.Count == 0) continue;
            filas.Append($"""<tr class="grp"><td colspan="10">{grupo}</td></tr>""");
            foreach (var l in locales) filas.Append(Fila(l.Nombre, l.Base, l.Comp));
        }

        var bc = Html(m.Base.Corto); var cc = Html(m.Comp.Corto);
        return Pagina($"""
            {Header("Resumen ejecutivo · " + Titulo(m), "Indicadores por local", $"Ventas netas en USD, cubiertos y USD por cubierto · {m.Base.Nombre} vs {m.Comp.Nombre}")}
            <main>
              <table class="t wide p2t">
                <thead>
                  <tr class="super"><th></th><th colspan="3">Ventas USD</th><th colspan="3" class="sep">Cubiertos</th><th colspan="3" class="sep">USD / cubierto</th></tr>
                  <tr><th class="lbl">Local</th><th>{bc}</th><th>{cc}</th><th>Var.</th><th class="sep">{bc}</th><th>{cc}</th><th>Var.</th>
                    <th class="sep">{bc}</th><th>{cc}</th><th>Var.</th></tr>
                </thead>
                <tbody>{filas}</tbody>
              </table>
              <div class="note"><b>Criterios:</b> Ventas = neto USD (sin impuestos ni propinas), convertido con el tipo de cambio de cada día.
              Reembolsos descontados en ventas y cubiertos; tickets anulados excluidos.
              Palermo = {Html(Lista(m.Palermo.Locales.Select(l => l.Nombre).ToList()))}; franquicias = los {m.Franquicias.Locales.Count} locales restantes.
              {Html(m.Base.Nombre)}: {m.Base.Dias} días; {Html(m.Comp.Nombre)}: {m.Comp.Dias} días.<br>
              <b>Fuente:</b> transacciones registradas en la plataforma del {Fecha(m.Base.Desde)} al {Fecha(m.Base.Hasta)} y del {Fecha(m.Comp.Desde)} al {Fecha(m.Comp.Hasta)}.</div>
            </main>
            {Footer(m, 2)}
            """);
    }

    // ------------------------------------------------------------------ página 3

    private static string Pagina3(InformeEjecutivoModel m)
    {
        string Card(string tag, string titulo, string color, IEnumerable<(string k, string v)> filas) =>
            $"""<div class="gc {color}"><div class="gc-h"><span class="gc-tag">{Html(tag)}</span>{Html(titulo)}</div><div class="gc-b">""" +
            string.Concat(filas.Select(f => $"""<div class="gc-r"><span>{f.k}</span><b>{f.v}</b></div>""")) +
            "</div></div>";

        string Idx(decimal? idx) => idx is null ? "" :
            $""" <span class="idx {(Math.Round(idx.Value) >= 100 ? "pos" : "neg")}">índice {N(idx)}</span>""";

        var p = m.Palermo.Comp;
        var s = m.ServicioCompleto.Comp;
        var a = m.Aeropuertos.Comp;
        var cards =
            Card("Base 100", m.Palermo.Nombre, "c-navy", new[]
            {
                ("Ventas USD", N(p.VentasUsd)), ("Cubiertos", N(p.Cubiertos)), ("USD / cubierto", N(p.UsdPorCubierto, 1)),
                ("Ticket promedio USD", N(p.TicketPromedio, 1)), ("Cubiertos por ticket", N(p.CubiertosPorTicket, 2)),
            }) +
            Card($"{m.ServicioCompleto.Locales.Count} locales", "Franquicias servicio completo", "c-lime", new[]
            {
                ("Ventas USD", N(s.VentasUsd)), ("Cubiertos", N(s.Cubiertos)),
                ("USD / cubierto", N(s.UsdPorCubierto, 1) + Idx(m.IndiceUsdPorCubierto(s, true))),
                ("Ticket promedio USD", N(s.TicketPromedio, 1) + Idx(m.IndiceTicket(s, true))),
                ("Cubiertos por ticket", N(s.CubiertosPorTicket, 2)),
            }) +
            Card(Lista(m.Aeropuertos.Locales.Select(l => l.Nombre).ToList()), "Aeropuertos", "c-slate", new[]
            {
                ("Ventas USD", N(a.VentasUsd)), ("Cubiertos", N(a.Cubiertos)),
                ("USD / cubierto", N(a.UsdPorCubierto, 1) + Idx(m.IndiceUsdPorCubierto(a, true))),
                ("Ticket promedio USD", N(a.TicketPromedio, 1)), ("Formato", "no comparable"),
            });

        return Pagina($"""
            {Header("Resumen ejecutivo · Franquicias vs Palermo", "Comparativo en USD",
                $"{m.Comp.Nombre} (con variación vs {m.Base.Nombre}) · Ventas netas en USD, sin impuestos ni propinas · Palermo = base 100")}
            <main>
              <div class="gcards">{cards}</div>
              <div class="cols2">
                <div><h2>Conclusiones</h2>{Bullets(m.Conclusiones)}</div>
                <div class="review"><h2>Para revisar en el control de franquicias</h2>{Bullets(m.ParaRevisar)}</div>
              </div>
              <div class="note"><b>Criterios:</b> Ventas = neto USD (sin impuestos ni propinas), convertido con el tipo de cambio de cada día.
              Índice = indicador del local / indicador de Palermo × 100. Franquicias servicio completo = todas menos aeropuertos.
              Venta/día abierto = ventas / días con operación. Desc. % = descuentos / venta bruta.</div>
            </main>
            {Footer(m, 3)}
            """);
    }

    // ------------------------------------------------------------------ página 4

    private static string Pagina4(InformeEjecutivoModel m)
    {
        var totalComp = m.Total.Comp.VentasUsd;
        string FilaGrupo(GrupoInforme g, string cls)
        {
            var b = g.Base; var c = g.Comp;
            return $"""<tr class="{cls}"><td class="lbl">{Html(g.Nombre)}</td>""" +
                   Num(c.VentasUsd) + Delta(Var(b.VentasUsd, c.VentasUsd)) +
                   Num(c.Cubiertos, 0, "sep") + Delta(Var(b.Cubiertos, c.Cubiertos)) +
                   Num(c.UsdPorCubierto, 1, "sep") + Delta(Var(b.UsdPorCubierto, c.UsdPorCubierto)) +
                   Num(m.IndiceUsdPorCubierto(c, true), 0, "sep b") + Num(m.IndiceUsdPorCubierto(b, false)) +
                   Num(c.TicketPromedio, 1, "sep") + Num(m.IndiceTicket(c, true), 0, "b") +
                   $"""<td class="num sep">{Share(totalComp > 0 ? c.VentasUsd / totalComp : null)}</td></tr>""";
        }

        var grupos = new StringBuilder(FilaGrupo(m.Palermo, "base"));
        grupos.Append(FilaGrupo(m.ServicioCompleto, ""));
        foreach (var p in m.Paises) grupos.Append(FilaGrupo(p, "sub"));
        if (m.Aeropuertos.Locales.Count > 0) grupos.Append(FilaGrupo(m.Aeropuertos, ""));
        grupos.Append(FilaGrupo(m.Franquicias, "total"));

        var detalle = new StringBuilder();
        foreach (var l in m.Locales.OrderByDescending(l => l.EsPalermo).ThenByDescending(l => m.IndiceUsdPorCubierto(l.Comp, true) ?? -1))
        {
            var b = l.Base; var c = l.Comp;
            var idx = m.IndiceUsdPorCubierto(c, true);
            var ancho = idx is null ? 0 : Math.Min(100, idx.Value / EscalaIndice * 100);
            detalle.Append($"""<tr><td class="lbl">{Html(l.Nombre)}</td>""")
                .Append(Num(c.VentasUsd)).Append(Delta(Var(b.VentasUsd, c.VentasUsd)))
                .Append(Num(c.Cubiertos, 0, "sep"))
                .Append(Num(c.UsdPorCubierto, 1, "sep")).Append(Delta(Var(b.UsdPorCubierto, c.UsdPorCubierto)))
                .Append(Num(idx, 0, "sep b"))
                .Append($"""<td class="barcell"><div class="track"><div class="bar {(l.EsPalermo ? "pal" : "fr")}" style="width:{ancho.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}%"></div><div class="ref"></div></div></td>""")
                .Append(Num(c.TicketPromedio, 1, "sep")).Append(Num(c.CubiertosPorTicket, 2))
                .Append(Num(c.VentaPorDiaAbierto, 0, "sep")).Append($"""<td class="num">{c.DiasOperados}</td>""")
                .Append($"""<td class="num sep">{Share(c.DescuentoPct)}</td></tr>""");
        }

        return Pagina($"""
            {Header("Resumen ejecutivo · " + m.Comp.Nombre, "Comparativo por grupo y por local", $"Ventas netas en USD · Variaciones vs {m.Base.Nombre} · Índice $/cubierto: Palermo = 100")}
            <main>
              <h2>Comparativo por grupo</h2>
              <table class="t wide dense">
                <thead><tr><th class="lbl">Grupo</th><th>Ventas USD</th><th>Var. vs {Html(m.Base.Corto.ToLowerInvariant())}</th><th class="sep">Cubiertos</th><th>Var.</th>
                  <th class="sep">USD/cub</th><th>Var.</th><th class="sep">Índice $/cub</th><th>Índice {Html(m.Base.Corto.ToLowerInvariant())}</th>
                  <th class="sep">Ticket prom.</th><th>Índice ticket</th><th class="sep">% venta total</th></tr></thead>
                <tbody>{grupos}</tbody>
              </table>
              <h2>Detalle por local</h2>
              <table class="t wide dense">
                <thead><tr><th class="lbl">Local</th><th>Ventas USD</th><th>Var. vs {Html(m.Base.Corto.ToLowerInvariant())}</th><th class="sep">Cubiertos</th>
                  <th class="sep">USD/cub</th><th>Var.</th><th class="sep">Índice $/cub</th><th class="barh">vs Palermo = 100</th>
                  <th class="sep">Ticket prom.</th><th>Cub./ticket</th><th class="sep">Venta/día abierto</th><th>Días</th><th class="sep">Desc. %</th></tr></thead>
                <tbody>{detalle}</tbody>
              </table>
              <div class="legend"><span><i class="sw pal"></i>Palermo</span><span><i class="sw fr"></i>Franquicias</span>
                <span><i class="dash"></i>línea punteada = Palermo (100)</span></div>
            </main>
            {Footer(m, 4)}
            """);
    }

    // ------------------------------------------------------------------ página 5

    private static string Pagina5(InformeEjecutivoModel m)
    {
        string Fila(string nombre, string moneda, Metricas b, Metricas c, string cls = "") =>
            $"""<tr class="{cls}"><td class="lbl">{Html(nombre)}</td><td class="cur">{moneda}</td>""" +
            Num(b.VentasLocal) + Num(c.VentasLocal) + Delta(Var(b.VentasLocal, c.VentasLocal)) +
            Num(b.LocalPorCubierto, moneda is "USD" or "EUR" ? 2 : 0, "sep") + Num(c.LocalPorCubierto, moneda is "USD" or "EUR" ? 2 : 0) +
            Delta(Var(b.LocalPorCubierto, c.LocalPorCubierto)) +
            $"""<td class="num sep ref-usd {CssVar(Var(b.VentasUsd, c.VentasUsd))}">{Pct(Var(b.VentasUsd, c.VentasUsd))}</td></tr>""";

        var filas = new StringBuilder();
        foreach (var grupo in m.Locales.GroupBy(l => l.Moneda).OrderBy(g => g.Key == "USD").ThenBy(g => g.Key))
        {
            var locales = grupo.ToList();
            var paises = Lista(locales.Select(l => InformeEjecutivoService.NombrePais(l.Pais)).Distinct().ToList());
            filas.Append($"""<tr class="grp"><td colspan="10">{Html(paises)} — {grupo.Key}</td></tr>""");

            var palermo = locales.Where(l => l.EsPalermo).ToList();
            foreach (var l in palermo) filas.Append(Fila(l.Nombre, grupo.Key, l.Base, l.Comp));
            if (palermo.Count > 1 && locales.Count > palermo.Count)
                filas.Append(Fila("Subtotal Palermo", grupo.Key, Metricas.Sumar(palermo.Select(l => l.Base)), Metricas.Sumar(palermo.Select(l => l.Comp)), "subtot"));
            foreach (var l in locales.Where(l => !l.EsPalermo)) filas.Append(Fila(l.Nombre, grupo.Key, l.Base, l.Comp));
            if (locales.Count > 1)
                filas.Append(Fila($"Total {paises}", grupo.Key, Metricas.Sumar(locales.Select(l => l.Base)), Metricas.Sumar(locales.Select(l => l.Comp)), "total"));
        }

        var fx = new StringBuilder();
        foreach (var tc in m.TiposCambio)
        {
            var v = Var(tc.PromedioBase, tc.PromedioComp);
            var dec = tc.PromedioComp is < 10 ? 3 : 0;
            var nota = v is null ? "" : Math.Round(v.Value, 3) == 0 ? "sin cambio"
                : $"{(v > 0 ? "se depreció" : "se apreció")} {Pct(Math.Abs(v.Value), 1, signo: false)} vs USD{(tc.EsFijo ? " · TC fijo" : "")}";
            fx.Append($"""<div class="fx-i"><b>{tc.Moneda}</b> {N(tc.PromedioBase, dec)} a {N(tc.PromedioComp, dec)}<span>{nota}</span></div>""");
        }

        var bc = Html(m.Base.Corto); var cc = Html(m.Comp.Corto);
        return Pagina($"""
            {Header("Resumen ejecutivo · " + Titulo(m), "Ventas en moneda local", "Ventas netas (sin impuestos ni propinas) en la moneda de cada país · No se suman entre monedas distintas")}
            <main>
              <p class="lead small p5">La última columna repite la variación en USD como referencia: la diferencia con la variación en moneda local muestra el efecto del tipo de cambio.</p>
              <table class="t wide dense p5t">
                <thead>
                  <tr class="super"><th></th><th></th><th colspan="3">Ventas moneda local</th><th colspan="3" class="sep">$ / cubierto</th><th class="sep">Referencia</th></tr>
                  <tr><th class="lbl">Local</th><th class="cur">Moneda</th><th>{bc}</th><th>{cc}</th><th>Var.</th>
                    <th class="sep">{bc}</th><th>{cc}</th><th>Var.</th><th class="sep">Var. ventas USD</th></tr></thead>
                <tbody>{filas}</tbody>
              </table>
              {(fx.Length == 0 ? "" : $"""
              <div class="fx">
                <div class="fx-t">Tipo de cambio promedio<br><span>moneda local por USD · {bc} a {cc}</span></div>
                {fx}
              </div>
              """)}
            </main>
            {Footer(m, 5)}
            """);
    }

    // ------------------------------------------------------------------ recursos

    private static Stream AbrirRecurso(string nombre) =>
        Assembly.GetExecutingAssembly().GetManifestResourceStream("InformeEjecutivo." + nombre)
        ?? throw new InvalidOperationException($"Recurso embebido no encontrado: InformeEjecutivo.{nombre}");

    private static string Recurso(string nombre)
    {
        using var reader = new StreamReader(AbrirRecurso(nombre));
        return reader.ReadToEnd();
    }

    private static string DataUri(string nombre, string mime)
    {
        using var stream = AbrirRecurso(nombre);
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return $"data:{mime};base64,{Convert.ToBase64String(ms.ToArray())}";
    }
}
