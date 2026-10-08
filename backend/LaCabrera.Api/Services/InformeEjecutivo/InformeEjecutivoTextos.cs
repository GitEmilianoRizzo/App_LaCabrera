using static LaCabrera.Api.Services.InformeEjecutivo.Fmt;

namespace LaCabrera.Api.Services.InformeEjecutivo;

/// <summary>
/// Redacción automática del informe a partir de los números (reglas simples y explícitas,
/// sin IA): resumen, puntos clave, comparación con Palermo, conclusiones y alertas de control.
/// </summary>
internal static class InformeEjecutivoTextos
{
    /// <summary>Unidad de análisis para los puntos clave: un local o un país con varios locales.</summary>
    private sealed record Unidad(string Nombre, Metricas Base, Metricas Comp, int Locales)
    {
        public decimal Delta => Comp.VentasUsd - Base.VentasUsd;
    }

    public static void Generar(InformeEjecutivoModel m)
    {
        if (!m.Total.Base.TieneDatos || !m.Total.Comp.TieneDatos)
        {
            // Sin uno de los dos períodos no hay variaciones que analizar: se informa y se muestran solo los números.
            var (con, sin) = m.Total.Comp.TieneDatos ? (m.Comp, m.Base) : (m.Base, m.Comp);
            var total = m.Total.Comp.TieneDatos ? m.Total.Comp : m.Total.Base;
            m.Resumen = $"No hay ventas registradas en {sin.EnTexto} ({Fecha(sin.Desde)} al {Fecha(sin.Hasta)}), por lo que no se calculan variaciones. " +
                        (total.TieneDatos ? $"{Capitalizar(con.EnTexto)} suma <b>{Usd(total.VentasUsd)} de ventas netas</b> con {N(total.Cubiertos)} cubiertos." : "");
            m.ParaRevisar.Add($"<b>Período sin datos:</b> {sin.EnTexto} no tiene ventas cargadas en la plataforma. Conviene elegir otro período de comparación.");
            return;
        }

        m.Resumen = Resumen(m);
        m.PuntosClave = PuntosClave(m);
        m.FranquiciasVsPalermo = FranquiciasVsPalermo(m);
        m.Conclusiones = Conclusiones(m);
        m.ParaRevisar.AddRange(ParaRevisar(m));
    }

    private static string Resumen(InformeEjecutivoModel m)
    {
        var b = m.Total.Base;
        var c = m.Total.Comp;
        var vVentas = Var(b.VentasUsd, c.VentasUsd);
        var s = $"{m.Comp.Nombre} cerró con <b>{Usd(c.VentasUsd)} de ventas netas ({Pct(vVentas)} vs {m.Base.EnTexto})</b>, " +
                $"con cubiertos {Pct(Var(b.Cubiertos, c.Cubiertos))} y $/cubierto {Pct(Var(b.UsdPorCubierto, c.UsdPorCubierto))}.";

        if (m.Base.Dias != m.Comp.Dias)
        {
            var vDiaria = Var(b.VentasUsd / m.Base.Dias, c.VentasUsd / m.Comp.Dias);
            s += $" Ajustado por la cantidad de días ({m.Comp.Dias} vs {m.Base.Dias}), la venta diaria varía {Pct(vDiaria)}.";
        }

        var delta = c.VentasUsd - b.VentasUsd;
        if (Math.Abs(delta) < 1) return s;

        var umbral = Math.Abs(delta) * 0.05m;
        var aFavor = m.Locales.Where(l => Math.Sign(l.Comp.VentasUsd - l.Base.VentasUsd) == Math.Sign(delta)
                                          && Math.Abs(l.Comp.VentasUsd - l.Base.VentasUsd) >= umbral)
                              .OrderByDescending(l => Math.Abs(l.Comp.VentasUsd - l.Base.VentasUsd))
                              .Take(4).Select(l => Html(l.Nombre)).ToList();
        var enContra = m.Locales.Where(l => Math.Sign(l.Comp.VentasUsd - l.Base.VentasUsd) == -Math.Sign(delta)
                                            && Math.Abs(l.Comp.VentasUsd - l.Base.VentasUsd) >= umbral)
                                .OrderByDescending(l => Math.Abs(l.Comp.VentasUsd - l.Base.VentasUsd))
                                .Take(3).Select(l => Html(l.Nombre)).ToList();

        if (aFavor.Count > 0)
        {
            s += delta < 0
                ? $" La baja se concentra en <b>{Lista(aFavor)}</b>"
                : $" La suba se explica principalmente por <b>{Lista(aFavor)}</b>";
            s += enContra.Count > 0
                ? $", y está parcialmente compensada por <b>{Lista(enContra)}</b>."
                : ".";
        }
        return s;
    }

    private static List<string> PuntosClave(InformeEjecutivoModel m)
    {
        // Países con varios locales de servicio completo se analizan juntos; el resto, por local.
        var unidades = new List<Unidad>();
        foreach (var pais in m.Paises)
        {
            if (pais.Locales.Count > 1)
                unidades.Add(new Unidad(pais.Nombre, pais.Base, pais.Comp, pais.Locales.Count));
            else
                unidades.AddRange(pais.Locales.Select(l => new Unidad(l.Nombre, l.Base, l.Comp, 1)));
        }
        unidades.AddRange(m.Palermo.Locales.Concat(m.Aeropuertos.Locales).Select(l => new Unidad(l.Nombre, l.Base, l.Comp, 1)));

        var total = Math.Abs(m.Total.Comp.VentasUsd - m.Total.Base.VentasUsd);
        var puntos = new List<string>();

        foreach (var u in unidades.Where(u => u.Base.TieneDatos || u.Comp.TieneDatos)
                                  .OrderByDescending(u => Math.Abs(u.Delta)).Take(5))
        {
            if (!u.Base.TieneDatos)
            {
                puntos.Add($"<b>{Html(u.Nombre)}:</b> sin ventas en {m.Base.EnTexto}; en {m.Comp.EnTexto} suma {Usd(u.Comp.VentasUsd)}.");
                continue;
            }
            if (!u.Comp.TieneDatos)
            {
                puntos.Add($"<b>{Html(u.Nombre)}:</b> sin ventas registradas en {m.Comp.EnTexto} ({Usd(u.Base.VentasUsd)} en {m.Base.EnTexto}). Conviene verificar la integración.");
                continue;
            }

            var vVentas = Var(u.Base.VentasUsd, u.Comp.VentasUsd);
            var vCub = Var(u.Base.Cubiertos, u.Comp.Cubiertos);
            var vUpc = Var(u.Base.UsdPorCubierto, u.Comp.UsdPorCubierto);
            var titulo = u.Locales > 1 ? $"{Html(u.Nombre)} {Pct(vVentas)} en conjunto" : $"{Html(u.Nombre)} {Pct(vVentas)}";
            var s = $"<b>{titulo} ({Usd(u.Delta, signo: true)})</b>";
            if (total > 0 && Math.Abs(u.Delta) >= total * 0.6m && Math.Sign(u.Delta) == Math.Sign(m.Total.Comp.VentasUsd - m.Total.Base.VentasUsd))
                s += " explica la mayor parte de la variación del total.";
            else
                s += ".";

            if (vCub is not null && vUpc is not null)
            {
                var volumen = Math.Abs(vCub.Value) >= Math.Abs(vUpc.Value);
                s += volumen
                    ? $" Cubiertos {Pct(vCub)} y $/cubierto {Pct(vUpc)}: es un tema de volumen más que de precio."
                    : $" $/cubierto {Pct(vUpc)} con cubiertos {Pct(vCub)}: lo mueve el gasto por cubierto (precio o mix).";
            }

            if (u.Locales == 1 && u.Base.DiasOperados != u.Comp.DiasOperados)
            {
                var vDia = Var(u.Base.VentaPorDiaAbierto, u.Comp.VentaPorDiaAbierto);
                s += $" Operó {u.Comp.DiasOperados} días vs {u.Base.DiasOperados}; por día abierto la variación es {Pct(vDia)}.";
            }
            puntos.Add(s);
        }
        return puntos;
    }

    private static List<string> FranquiciasVsPalermo(InformeEjecutivoModel m)
    {
        var r = new List<string>();
        var pal = m.Palermo;
        var fr = m.Franquicias;
        if (!pal.Comp.TieneDatos || !fr.Comp.TieneDatos) return r;

        var share = m.Total.Comp.VentasUsd > 0 ? pal.Comp.VentasUsd / m.Total.Comp.VentasUsd : (decimal?)null;
        r.Add($"<b>Palermo {Pct(Var(pal.Base.VentasUsd, pal.Comp.VentasUsd))} y franquicias {Pct(Var(fr.Base.VentasUsd, fr.Comp.VentasUsd))}.</b> " +
              $"Palermo representa el {Share(share)} de la venta total.");

        var sc = m.ServicioCompleto.Comp;
        if (sc.UsdPorCubierto is { } upcSc && pal.Comp.UsdPorCubierto is { } upcPal)
        {
            var rangos = m.Paises
                .Where(p => p.Comp.UsdPorCubierto is not null)
                .Select(p =>
                {
                    var valores = p.Locales.Select(l => l.Comp.UsdPorCubierto).Where(v => v is not null).Select(v => v!.Value).ToList();
                    var nombre = p.Locales.Count > 1 ? InformeEjecutivoService.NombrePais(p.Locales[0].Pais) : p.Nombre;
                    return valores.Count > 1 && Math.Round(valores.Min()) != Math.Round(valores.Max())
                        ? $"{Html(nombre)} {N(valores.Min())}–{N(valores.Max())}"
                        : $"{Html(nombre)} {N(p.Comp.UsdPorCubierto)}";
                }).ToList();

            var dif = upcSc / upcPal - 1;
            r.Add($"<b>Sin aeropuertos, las franquicias facturan USD {N(upcSc)} por cubierto vs USD {N(upcPal)} de Palermo ({Pct(dif, 0)}).</b>" +
                  (rangos.Count > 0 ? $" Por país: {Lista(rangos)}." : ""));
        }

        var detalle = pal.Locales.Where(l => l.Comp.TieneDatos || l.Base.TieneDatos).Select(l =>
        {
            var s = $"{Html(l.Nombre)} {Pct(Var(l.Base.VentasUsd, l.Comp.VentasUsd))}";
            if (l.Base.DiasOperados != l.Comp.DiasOperados)
                s += $" ({l.Comp.DiasOperados} días operados vs {l.Base.DiasOperados})";
            return s;
        }).ToList();
        if (detalle.Count > 0)
            r.Add($"<b>Dentro de Palermo:</b> {Lista(detalle)}; $/cubierto {Pct(Var(pal.Base.UsdPorCubierto, pal.Comp.UsdPorCubierto))}.");

        return r;
    }

    private static List<string> Conclusiones(InformeEjecutivoModel m)
    {
        var r = new List<string>();
        var pal = m.Palermo.Comp;
        var sc = m.ServicioCompleto;

        var idxSc = m.IndiceUsdPorCubierto(sc.Comp, comp: true);
        var idxScBase = m.IndiceUsdPorCubierto(sc.Base, comp: false);
        if (idxSc is { } i)
        {
            var mas = i >= 100 ? "más" : "menos";
            var s = $"<b>Las franquicias de servicio completo facturan {Pct(Math.Abs(i / 100 - 1), 0, signo: false)} {mas} por cubierto que Palermo</b> " +
                    $"(USD {N(sc.Comp.UsdPorCubierto, 1)} vs {N(pal.UsdPorCubierto, 1)}; índice {N(i)})";
            if (idxScBase is { } ib)
            {
                var cambio = Math.Round(i) - Math.Round(ib);
                s += cambio == 0 ? $", igual que en {m.Base.EnTexto}." :
                     (Math.Abs(i - 100) > Math.Abs(ib - 100) ? ", y la brecha se amplió" : ", y la brecha se redujo") +
                     $" respecto de {m.Base.EnTexto} (índice {N(ib)}).";
            }
            else s += ".";
            r.Add(s);
        }

        var ranking = m.Locales.Where(l => !l.EsPalermo && !l.EsAeropuerto)
            .Select(l => (l, idx: m.IndiceUsdPorCubierto(l.Comp, true)))
            .Where(x => x.idx is not null).OrderByDescending(x => x.idx).ToList();
        if (ranking.Count > 0)
        {
            var top = ranking[0];
            var paises = m.Paises.Where(p => p.Locales.Count > 1)
                .Select(p => $"{Html(p.Nombre)} {N(m.IndiceUsdPorCubierto(p.Comp, true))}").ToList();
            r.Add($"<b>Por cubierto, {Html(top.l.Nombre)} encabeza el ranking</b> (índice {N(top.idx)})." +
                  (paises.Count > 0 ? $" Índice por país: {Lista(paises)}." : ""));
        }

        var porDia = m.Locales.Where(l => l.Comp.VentaPorDiaAbierto is not null)
            .OrderByDescending(l => l.Comp.VentaPorDiaAbierto).ToList();
        if (porDia.Count > 1)
        {
            var a = porDia[0];
            var b = porDia[1];
            var s = $"<b>{Html(a.Nombre)} es el local que más vende por día abierto:</b> USD {N(a.Comp.VentaPorDiaAbierto)}, " +
                    $"{N(a.Comp.VentaPorDiaAbierto / b.Comp.VentaPorDiaAbierto, 1)} veces {Html(b.Nombre)} (USD {N(b.Comp.VentaPorDiaAbierto)}).";
            if (m.Total.Comp.VentasUsd > 0 && m.Total.Comp.Cubiertos > 0 && pal.TieneDatos)
                s += $" Palermo concentra el {Share(pal.VentasUsd / m.Total.Comp.VentasUsd)} de la venta con el " +
                     $"{Share((decimal)pal.Cubiertos / m.Total.Comp.Cubiertos)} de los cubiertos.";
            r.Add(s);
        }

        if (pal.CubiertosPorTicket is { } cptPal && sc.Comp.CubiertosPorTicket is { } cptSc && pal.TicketPromedio is { } tkPal && sc.Comp.TicketPromedio is { } tkSc)
        {
            var mesas = cptPal >= cptSc ? "Palermo tiene mesas más grandes" : "Las franquicias tienen mesas más grandes";
            r.Add($"<b>{mesas}</b> ({N(cptPal, 2)} cubiertos por ticket en Palermo vs {N(cptSc, 2)} en servicio completo). " +
                  $"El ticket promedio de las franquicias es {Pct(Math.Abs(tkSc / tkPal - 1), 0, signo: false)} {(tkSc >= tkPal ? "mayor" : "menor")} " +
                  $"(USD {N(tkSc, 1)} vs {N(tkPal, 1)}).");
        }

        var aero = m.Aeropuertos;
        if (aero.Comp.TieneDatos)
        {
            var unCubPorTicket = aero.Locales.Where(l => l.Comp.CubiertosPorTicket is { } c && Math.Abs(c - 1) < 0.01m).Select(l => Html(l.Nombre)).ToList();
            r.Add($"<b>Los aeropuertos no son comparables por $/cubierto</b> (índice {N(m.IndiceUsdPorCubierto(aero.Comp, true))}): son otro formato" +
                  (unCubPorTicket.Count > 0 ? $", y {Lista(unCubPorTicket)} carga 1 cubierto por ticket" : "") +
                  $". Sus ventas variaron {Pct(Var(aero.Base.VentasUsd, aero.Comp.VentasUsd))} en el período.");
        }
        return r;
    }

    private static List<string> ParaRevisar(InformeEjecutivoModel m)
    {
        var r = new List<string>();

        foreach (var tc in m.TiposCambio.Where(t => t.EsFijo))
        {
            var locales = m.Locales.Where(l => l.Moneda == tc.Moneda).Select(l => Html(l.Nombre)).ToList();
            r.Add($"<b>Tipo de cambio {tc.Moneda}:</b> {Lista(locales)} usa{(locales.Count > 1 ? "n" : "")} un TC fijo de " +
                  $"{N(tc.PromedioComp ?? tc.PromedioBase, (tc.PromedioComp ?? tc.PromedioBase) < 10 ? 3 : 0)} {tc.Moneda}/USD. Las ventas en USD (y cualquier regalía calculada en USD) dependen de ese valor; " +
                  "conviene confirmar si es el que fija el contrato o si corresponde el TC de mercado del período.");
        }

        var desc = m.Locales
            .Where(l => l.Comp.DescuentoPct is not null && l.Base.DescuentoPct is not null
                        && Math.Abs(l.Comp.DescuentoPct.Value - l.Base.DescuentoPct.Value) >= 0.01m)
            .OrderByDescending(l => Math.Abs(l.Comp.DescuentoPct!.Value - l.Base.DescuentoPct!.Value))
            .Take(4)
            .Select(l => $"{Html(l.Nombre)} pasa de {Share(l.Base.DescuentoPct)} a {Share(l.Comp.DescuentoPct)}")
            .ToList();
        if (desc.Count > 0)
            r.Add($"<b>Cambios en descuentos (% del bruto):</b> {Lista(desc)}. Si la regalía es sobre ventas netas, los descuentos reducen la base.");

        var sinDescNiProp = m.Locales
            .Where(l => l.Comp.Tickets > 0 && l.Comp.DescuentoLocal == 0 && l.Comp.PropinaLocal == 0)
            .Select(l => Html(l.Nombre)).ToList();
        if (sinDescNiProp.Count > 0 && sinDescNiProp.Count < m.Locales.Count(l => l.Comp.Tickets > 0))
            r.Add($"<b>Sin descuentos ni propinas registrados:</b> {Lista(sinDescNiProp)}. Puede ser una diferencia en cómo se carga la " +
                  "información en el sistema; conviene validarlo antes de comparar márgenes.");

        foreach (var l in m.Locales.Where(l => l.Comp.Reembolsos > 0 || l.Base.Reembolsos > 0))
            r.Add($"<b>Reembolsos en {Html(l.Nombre)}:</b> {l.Comp.Reembolsos} tickets en {m.Comp.EnTexto} ({l.Base.Reembolsos} en {m.Base.EnTexto}). " +
                  "Ya están descontados de ventas y cubiertos.");

        var pocosDias = m.Locales
            .Where(l => l.Comp.TieneDatos && l.Comp.DiasOperados < m.Comp.Dias - 2)
            .Select(l => $"{Html(l.Nombre)} ({l.Comp.DiasOperados} de {m.Comp.Dias})").ToList();
        if (pocosDias.Count > 0)
            r.Add($"<b>Días sin operación en {m.Comp.EnTexto}:</b> {Lista(pocosDias)}. Conviene confirmar si fueron cierres reales o días sin datos.");

        var sinDatos = m.Locales.Where(l => l.Base.TieneDatos != l.Comp.TieneDatos)
            .Select(l => $"{Html(l.Nombre)} (sin datos en {(l.Comp.TieneDatos ? m.Base.EnTexto : m.Comp.EnTexto)})").ToList();
        if (sinDatos.Count > 0)
            r.Add($"<b>Locales con datos en un solo período:</b> {Lista(sinDatos)}. Las variaciones del total incluyen ese efecto.");

        return r;
    }
}
