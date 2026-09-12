using System.Collections.Concurrent;
using MathNet.Numerics.Distributions;

namespace StatStudio.Core.Statistics;

/// <summary>
/// The studentized range distribution (Tukey's q), by numerical integration.
/// Used for Tukey HSD post-hoc comparisons.
/// </summary>
/// <remarks>
/// This is the hottest numeric path in the app: <see cref="AnovaExtensions.Tukey"/> calls
/// <see cref="CDF"/> once per pairwise comparison plus once per bisection step of
/// <see cref="InverseCDF"/>, and every call runs an integral nested inside another integral.
/// The inner grid is fixed, so the standard-normal density and CDF at each node are computed
/// once for the process rather than on every evaluation.
/// </remarks>
public static class StudentizedRange
{
    private const double InnerLo = -8.0, InnerHi = 8.0;
    private const int InnerSteps = 240;

    private static readonly double[] InnerZ = BuildInnerGrid();
    private static readonly double[] InnerPdf = InnerZ.Select(z => Normal.PDF(0, 1, z)).ToArray();
    private static readonly double[] InnerCdf = InnerZ.Select(z => Normal.CDF(0, 1, z)).ToArray();

    /// <summary>Chi-squared helpers keyed by df; both are pure functions of df and are reused across calls.</summary>
    private static readonly ConcurrentDictionary<double, (ChiSquared Chi, double SMax)> ChiCache = new();

    private static double[] BuildInnerGrid()
    {
        double h = (InnerHi - InnerLo) / InnerSteps;
        var z = new double[InnerSteps + 1];
        for (int i = 0; i <= InnerSteps; i++) z[i] = InnerLo + i * h;
        return z;
    }

    /// <summary>P(Q ≤ q) for k groups and df error degrees of freedom.</summary>
    public static double CDF(double q, int k, double df)
    {
        if (q <= 0) return 0;
        if (k < 2) return double.NaN;
        if (double.IsInfinity(df) || df > 5000) return RangeCdf(q, k);

        // Integrate F_range(q·s)·f_S(s) ds, where S = sqrt(χ²_df / df).
        var (chi, sMax) = ChiCache.GetOrAdd(df, d =>
        {
            var c = new ChiSquared(d);
            return (c, Math.Sqrt(c.InverseCumulativeDistribution(0.99999) / d));
        });
        return Simpson(1e-6, sMax, 160, s => RangeCdf(q * s, k) * chi.Density(df * s * s) * 2 * df * s);
    }

    public static double InverseCDF(double p, int k, double df, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        double lo = 0, hi = 100;
        // 30 halvings of [0, 100] resolve q to ~1e-7 — far finer than anything reported, and
        // 18 fewer full nested integrations than the 48 this used to run.
        for (int it = 0; it < 30; it++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            double mid = 0.5 * (lo + hi);
            if (CDF(mid, k, df) < p) lo = mid; else hi = mid;
        }
        return 0.5 * (lo + hi);
    }

    // CDF of the range of k i.i.d. standard normals at width w.
    private static double RangeCdf(double w, int k)
    {
        if (w <= 0) return 0;
        double h = (InnerHi - InnerLo) / InnerSteps;
        double sum = 0;
        for (int i = 0; i <= InnerSteps; i++)
        {
            double inner = InnerCdf[i] - Normal.CDF(0, 1, InnerZ[i] - w);
            double f = inner <= 0 ? 0 : k * InnerPdf[i] * Math.Pow(inner, k - 1);
            int weight = (i == 0 || i == InnerSteps) ? 1 : (i % 2 == 0 ? 2 : 4);
            sum += weight * f;
        }
        return sum * h / 3.0;
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
