namespace StatStudio.Core.Inference;

/// <summary>Percentiles using Minitab's method (= R type 6, the (n+1) method).</summary>
public static class Quantiles
{
    /// <summary>
    /// The <paramref name="p"/>-th percentile (0..100) of an ascending-sorted sample.
    /// Position L = p/100·(n+1); linear interpolation between order statistics, clamped
    /// to the min/max. This matches Minitab's Q1/Median/Q3.
    /// </summary>
    public static double Percentile(IReadOnlyList<double> sortedAsc, double p)
    {
        int n = sortedAsc.Count;
        if (n == 0) return double.NaN;
        if (n == 1) return sortedAsc[0];

        double rank = p / 100.0 * (n + 1);
        if (rank <= 1) return sortedAsc[0];
        if (rank >= n) return sortedAsc[n - 1];

        int k = (int)Math.Floor(rank);   // 1-based lower order statistic
        double frac = rank - k;
        return sortedAsc[k - 1] + frac * (sortedAsc[k] - sortedAsc[k - 1]);
    }

    public static double Median(IReadOnlyList<double> sortedAsc) => Percentile(sortedAsc, 50);

    /// <summary>Sorts a copy ascending and returns it (input untouched).</summary>
    public static double[] Sorted(IEnumerable<double> values)
    {
        var a = values.ToArray();
        Array.Sort(a);
        return a;
    }
}
