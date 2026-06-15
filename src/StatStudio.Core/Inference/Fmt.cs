using System.Globalization;

namespace StatStudio.Core.Inference;

/// <summary>Consistent, culture-invariant number formatting for Session output.</summary>
public static class Fmt
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Fixed decimals (default 3); NaN renders as Minitab's "*".</summary>
    public static string N(double v, int dp = 3) =>
        double.IsNaN(v) ? "*" : v.ToString("F" + dp, Inv);

    /// <summary>General compact form, up to 5 decimals, trailing zeros trimmed.</summary>
    public static string G(double v) =>
        double.IsNaN(v) ? "*" : v.ToString("0.#####", Inv);

    /// <summary>p-value: shows "0.000" when &lt; 0.0005 (Minitab convention).</summary>
    public static string P(double p)
    {
        if (double.IsNaN(p)) return "*";
        if (p < 0.0005) return "0.000";
        return p.ToString("F3", Inv);
    }

    public static string Int(double v) =>
        double.IsNaN(v) ? "*" : Math.Round(v).ToString(Inv);
}
