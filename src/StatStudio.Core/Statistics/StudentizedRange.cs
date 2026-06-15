using MathNet.Numerics.Distributions;

namespace StatStudio.Core.Statistics;

/// <summary>
/// The studentized range distribution (Tukey's q), by numerical integration.
/// Used for Tukey HSD post-hoc comparisons.
/// </summary>
public static class StudentizedRange
{
    /// <summary>P(Q ≤ q) for k groups and df error degrees of freedom.</summary>
    public static double CDF(double q, int k, double df)
    {
        if (q <= 0) return 0;
        if (k < 2) return double.NaN;
        if (double.IsInfinity(df) || df > 5000) return RangeCdf(q, k);

        // Integrate F_range(q·s)·f_S(s) ds, where S = sqrt(χ²_df / df).
        var chi = new ChiSquared(df);
        double sMax = Math.Sqrt(chi.InverseCumulativeDistribution(0.99999) / df);
        return Simpson(1e-6, sMax, 160, s => RangeCdf(q * s, k) * chi.Density(df * s * s) * 2 * df * s);
    }

    public static double InverseCDF(double p, int k, double df)
    {
        double lo = 0, hi = 100;
        for (int it = 0; it < 48; it++)
        {
            double mid = 0.5 * (lo + hi);
            if (CDF(mid, k, df) < p) lo = mid; else hi = mid;
        }
        return 0.5 * (lo + hi);
    }

    // CDF of the range of k i.i.d. standard normals at width w.
    private static double RangeCdf(double w, int k)
    {
        if (w <= 0) return 0;
        return Simpson(-8.0, 8.0, 240, z =>
        {
            double inner = Normal.CDF(0, 1, z) - Normal.CDF(0, 1, z - w);
            if (inner <= 0) return 0;
            return k * Normal.PDF(0, 1, z) * Math.Pow(inner, k - 1);
        });
    }

    private static double Simpson(double a, double b, int n, Func<double, double> f)
    {
        if (n % 2 == 1) n++;
        double h = (b - a) / n;
        double sum = f(a) + f(b);
        for (int i = 1; i < n; i++) sum += (i % 2 == 0 ? 2 : 4) * f(a + i * h);
        return sum * h / 3.0;
    }
}
