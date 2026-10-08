using System.Globalization;

namespace LaCabrera.Api.Services.InformeEjecutivo;

/// <summary>Formato numérico rioplatense (1.835.175 / 34,3 / −4,7%) independiente de la cultura del servidor.</summary>
internal static class Fmt
{
    public const string Menos = "−";
    public const string Guion = "—";

    private static readonly NumberFormatInfo Nfi = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = ".",
        NumberGroupSizes = new[] { 3 },
        NegativeSign = Menos,
    };

    private static readonly string[] Meses =
    {
        "enero", "febrero", "marzo", "abril", "mayo", "junio",
        "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre"
    };

    public static string N(decimal? v, int dec = 0) =>
        v is null ? Guion : Math.Round(v.Value, dec, MidpointRounding.AwayFromZero).ToString("N" + dec, Nfi);

    /// <summary>Variación relativa b/a − 1 (null si no hay base).</summary>
    public static decimal? Var(decimal? a, decimal? b) =>
        a is > 0 && b is not null ? b.Value / a.Value - 1 : null;

    /// <summary>"+3,4%" / "−25,6%" a partir de un ratio (0,034).</summary>
    public static string Pct(decimal? ratio, int dec = 1, bool signo = true)
    {
        if (ratio is null) return Guion;
        var v = Math.Round(ratio.Value * 100, dec, MidpointRounding.AwayFromZero);
        var s = Math.Abs(v).ToString("N" + dec, Nfi) + "%";
        if (!signo) return (v < 0 ? Menos : "") + s;
        return v > 0 ? "+" + s : v < 0 ? Menos + s : s;
    }

    /// <summary>Porcentaje sin signo forzado (participaciones: "28,2%").</summary>
    public static string Share(decimal? ratio, int dec = 1) => Pct(ratio, dec, signo: false);

    /// <summary>"USD 1,84 M" / "USD 105 mil" / "USD 950".</summary>
    public static string Usd(decimal v, bool signo = false)
    {
        var abs = Math.Abs(v);
        var txt = abs >= 1_000_000 ? $"{N(abs / 1_000_000, 2)} M"
                : abs >= 1_000 ? $"{N(abs / 1_000)} mil"
                : N(abs);
        var pre = v < 0 ? Menos : signo && v > 0 ? "+" : "";
        return $"{pre}USD {txt}";
    }

    public static string CssVar(decimal? ratio) =>
        ratio is null ? "" : Math.Round(ratio.Value * 1000) > 0 ? "pos" : Math.Round(ratio.Value * 1000) < 0 ? "neg" : "";

    public static string Mes(int mes) => Meses[mes - 1];

    public static string Capitalizar(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];

    public static string Fecha(DateTime d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    /// <summary>"A, B y C".</summary>
    public static string Lista(IReadOnlyList<string> items) => items.Count switch
    {
        0 => "",
        1 => items[0],
        _ => string.Join(", ", items.Take(items.Count - 1)) + " y " + items[^1],
    };

    public static string Html(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");
}
