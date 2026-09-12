using MathNet.Numerics.Distributions;

namespace StatStudio.Core.Statistics;

public sealed record AndersonDarlingResult(int N, double Mean, double StDev, double ASquared, double P);

/// <summary>Normality testing.</summary>
public static class Normality
{
    /// <summary>
    /// Anderson-Darling test for normality (mean &amp; variance estimated from the data).
    /// Returns the A² statistic and the standard small-sample-adjusted p-value.
    /// </summary>
    public static AndersonDarlingResult AndersonDarling(double[] x)
    {
        ArgumentNullException.ThrowIfNull(x);
        int n = x.Length;
        if (n < 3)
            throw new ArgumentException("At least three observations are required.", nameof(x));
        StatGuard.Finite(x, nameof(x));

        var s = x.OrderBy(v => v).ToArray();
        double mean = s.Average();
        double sd = Math.Sqrt(s.Sum(v => (v - mean) * (v - mean)) / (n - 1));
        if (sd <= 0)
            throw new ArgumentException("The sample must contain variation.", nameof(x));

        double a2 = 0;
        for (int i = 0; i < n; i++)
        {
            double fi = Clamp(Normal.CDF(0, 1, (s[i] - mean) / sd));
            double fni = Clamp(Normal.CDF(0, 1, (s[n - 1 - i] - mean) / sd));
            a2 += (2.0 * (i + 1) - 1) * (Math.Log(fi) + Math.Log(1 - fni));
        }
        a2 = -n - a2 / n;

        double aStar = a2 * (1 + 0.75 / n + 2.25 / ((double)n * n));
        return new AndersonDarlingResult(n, mean, sd, a2, PValue(aStar));
    }

    private static double Clamp(double f) => Math.Min(1 - 1e-12, Math.Max(1e-12, f));

    // Standard Anderson-Darling p-value (normal, parameters estimated) — D'Agostino & Stephens.
    private static double PValue(double a)
    {
        if (!double.IsFinite(a) || a < 0) return double.NaN;
        // The fitted quadratic tail is not valid indefinitely: its slope eventually
        // reverses. At A* >= 100 the decreasing tail is already below 1e-160.
        if (a >= 100) return 0;
        if (a >= 0.6) return Math.Exp(1.2937 - 5.709 * a + 0.0186 * a * a);
        if (a > 0.34) return Math.Exp(0.9177 - 4.279 * a - 1.38 * a * a);
        if (a > 0.2) return 1 - Math.Exp(-8.318 + 42.796 * a - 59.938 * a * a);
        return 1 - Math.Exp(-13.436 + 101.14 * a - 223.73 * a * a);
    }
}
