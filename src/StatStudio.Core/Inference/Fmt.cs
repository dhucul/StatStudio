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

    /// <summary>
    /// A 0..1 fraction as a percentage with trailing zeros trimmed (0.95 → "95%", 0.975 → "97.5%").
    /// </summary>
    public static string Pct(double fraction, int dp = 1) =>
        double.IsNaN(fraction)
            ? "*"
            : (fraction * 100).ToString("0." + new string('#', Math.Max(1, dp)), Inv) + "%";

    /// <summary>A 0..1 fraction as a percentage with fixed decimals (0.875 → "87.50%").</summary>
    public static string PctFixed(double fraction, int dp = 2) =>
        double.IsNaN(fraction) ? "*" : (fraction * 100).ToString("F" + dp, Inv) + "%";
}
