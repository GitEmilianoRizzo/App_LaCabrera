namespace LaCabrera.Api.Services.InformeEjecutivo;

public class InformeEjecutivoSettings
{
    /// <summary>Locales propios que forman "Palermo" (referencia = índice 100).</summary>
    public string[] CodigosPalermo { get; set; } = { "PALERMO_NORTE", "PALERMO_SUR" };

    /// <summary>Prefijos de código de los locales de aeropuerto (otro formato, no comparable por $/cubierto).</summary>
    public string[] PrefijosAeropuerto { get; set; } = { "EZEIZA", "AEROPARQUE" };

    /// <summary>Ruta al ejecutable de Chromium/Chrome. Si está vacío se usa PUPPETEER_EXECUTABLE_PATH o rutas conocidas.</summary>
    public string? ChromiumPath { get; set; }
}

public class Periodo
{
    public DateTime Desde { get; init; }
    public DateTime Hasta { get; init; }
    public int Dias => (Hasta.Date - Desde.Date).Days + 1;
    public bool EsMesCompleto => Desde.Day == 1 && Hasta.Date == Desde.AddMonths(1).AddDays(-1).Date;

    /// <summary>"Septiembre 2026" o "01/09/2026 al 15/09/2026".</summary>
    public string Nombre { get; init; } = string.Empty;

    /// <summary>Etiqueta de columna: "Sep" / "Sep 25" o "P. inicial".</summary>
    public string Corto { get; init; } = string.Empty;

    /// <summary>Para texto corrido: "agosto" o "el período inicial".</summary>
    public string EnTexto { get; init; } = string.Empty;
}

public class Metricas
{
    public decimal VentasUsd { get; set; }
    public decimal VentasLocal { get; set; }
    public decimal BrutoLocal { get; set; }
    public decimal DescuentoLocal { get; set; }
    public decimal PropinaLocal { get; set; }
    public int Cubiertos { get; set; }
    public int Tickets { get; set; }
    public int Reembolsos { get; set; }
    public int DiasOperados { get; set; }
    public int DiasSinTipoCambio { get; set; }

    public bool TieneDatos => Tickets > 0 || VentasUsd != 0;
    public decimal? UsdPorCubierto => Cubiertos > 0 ? VentasUsd / Cubiertos : null;
    public decimal? LocalPorCubierto => Cubiertos > 0 ? VentasLocal / Cubiertos : null;
    public decimal? TicketPromedio => Tickets > 0 ? VentasUsd / Tickets : null;
    public decimal? CubiertosPorTicket => Tickets > 0 ? (decimal)Cubiertos / Tickets : null;
    public decimal? VentaPorDiaAbierto => DiasOperados > 0 ? VentasUsd / DiasOperados : null;
    public decimal? DescuentoPct => BrutoLocal > 0 ? DescuentoLocal / BrutoLocal : null;

    public static Metricas Sumar(IEnumerable<Metricas> items)
    {
        var m = new Metricas();
        foreach (var i in items)
        {
            m.VentasUsd += i.VentasUsd;
            m.VentasLocal += i.VentasLocal;
            m.BrutoLocal += i.BrutoLocal;
            m.DescuentoLocal += i.DescuentoLocal;
            m.PropinaLocal += i.PropinaLocal;
            m.Cubiertos += i.Cubiertos;
            m.Tickets += i.Tickets;
            m.Reembolsos += i.Reembolsos;
            m.DiasOperados = Math.Max(m.DiasOperados, i.DiasOperados);
            m.DiasSinTipoCambio += i.DiasSinTipoCambio;
        }
        return m;
    }
}

public class LocalInforme
{
    public int FranquiciaId { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string? Pais { get; init; }
    public string Moneda { get; init; } = "USD";
    public bool EsPalermo { get; init; }
    public bool EsAeropuerto { get; init; }
    public Metricas Base { get; init; } = new();
    public Metricas Comp { get; init; } = new();
}

public class GrupoInforme
{
    public string Nombre { get; init; } = string.Empty;
    public List<LocalInforme> Locales { get; init; } = new();
    public Metricas Base => Metricas.Sumar(Locales.Select(l => l.Base));
    public Metricas Comp => Metricas.Sumar(Locales.Select(l => l.Comp));
}

public class TipoCambioInforme
{
    public string Moneda { get; init; } = string.Empty;
    public decimal? PromedioBase { get; init; }
    public decimal? PromedioComp { get; init; }
    /// <summary>TC cargado manualmente o sin variación en todo el rango (p. ej. COP 4.200 fijo).</summary>
    public bool EsFijo { get; init; }
}

public class InformeEjecutivoModel
{
    public Periodo Base { get; init; } = new();
    public Periodo Comp { get; init; } = new();
    public DateTime GeneradoEl { get; init; }

    public List<LocalInforme> Locales { get; init; } = new();
    public GrupoInforme Total { get; init; } = new();
    public GrupoInforme Palermo { get; init; } = new();
    public GrupoInforme Franquicias { get; init; } = new();
    public GrupoInforme ServicioCompleto { get; init; } = new();
    public GrupoInforme Aeropuertos { get; init; } = new();
    /// <summary>Franquicias de servicio completo agrupadas por país.</summary>
    public List<GrupoInforme> Paises { get; init; } = new();
    public List<TipoCambioInforme> TiposCambio { get; init; } = new();

    // Textos generados (HTML simple con <b>)
    public string Resumen { get; set; } = string.Empty;
    public List<string> PuntosClave { get; set; } = new();
    public List<string> FranquiciasVsPalermo { get; set; } = new();
    public List<string> Conclusiones { get; set; } = new();
    public List<string> ParaRevisar { get; set; } = new();

    public decimal? IndiceUsdPorCubierto(Metricas m, bool comp)
    {
        var refUpc = (comp ? Palermo.Comp : Palermo.Base).UsdPorCubierto;
        return refUpc is > 0 && m.UsdPorCubierto is { } upc ? upc / refUpc.Value * 100 : null;
    }

    public decimal? IndiceTicket(Metricas m, bool comp)
    {
        var refTk = (comp ? Palermo.Comp : Palermo.Base).TicketPromedio;
        return refTk is > 0 && m.TicketPromedio is { } tk ? tk / refTk.Value * 100 : null;
    }
}
