using MathNet.Numerics.Distributions;
using StatStudio.Core.Data;

namespace StatStudio.Core.Statistics;

public sealed record CorrelationResult(
    IReadOnlyList<string> Names, double[,] R, double[,] P, int[,] N, bool Spearman);

public static class Correlation
{
    public static (double R, double P, int N) Pearson(double[] x, double[] y)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);
        if (x.Length != y.Length)
            throw new ArgumentException("Correlation inputs must be row-aligned and equal length.");
        StatGuard.Finite(x, nameof(x));
        StatGuard.Finite(y, nameof(y));
        int n = x.Length;
        if (n < 2) return (double.NaN, double.NaN, n);
        double mx = 0, my = 0;
        for (int i = 0; i < n; i++) { mx += x[i]; my += y[i]; }
        mx /= n; my /= n;
        double sxy = 0, sxx = 0, syy = 0;
        for (int i = 0; i < n; i++)
        {
            double dx = x[i] - mx, dy = y[i] - my;
            sxy += dx * dy; sxx += dx * dx; syy += dy * dy;
        }
        double r = (sxx > 0 && syy > 0) ? sxy / Math.Sqrt(sxx * syy) : double.NaN;
        return (r, PFromR(r, n), n);
    }

    public static (double R, double P, int N) Spearman(double[] x, double[] y)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);
        if (x.Length != y.Length)
            throw new ArgumentException("Correlation inputs must be row-aligned and equal length.");
        StatGuard.Finite(x, nameof(x));
        StatGuard.Finite(y, nameof(y));
        int n = x.Length;
        if (n < 2) return (double.NaN, double.NaN, n);
        var rx = Ranking.Average(x);
        var ry = Ranking.Average(y);
        var (r, _, _) = Pearson(rx, ry);
        return (r, PFromR(r, n), n);
    }

    /// <summary>Pairwise correlation matrix over the columns (complete pairs per cell).</summary>
    public static CorrelationResult Matrix(IReadOnlyList<DataColumn> cols, bool spearman)
    {
        int k = cols.Count;
        var R = new double[k, k];
        var P = new double[k, k];
        var N = new int[k, k];

        // Parse each column once. Going through Columns.Pairwise per cell re-ran double.TryParse
        // over both columns for every pair, so each column was re-parsed k-1 times.
        int rowCount = k == 0 ? 0 : cols.Max(c => c.Count);
        var parsed = new double[k][];
        for (int i = 0; i < k; i++)
        {
            var values = new double[rowCount];
            for (int r = 0; r < rowCount; r++)
                values[r] = !cols[i].IsMissing(r) && DataColumn.TryParse(cols[i][r], out var v)
                    ? v
                    : double.NaN;   // NaN marks "missing or unparseable"
            parsed[i] = values;
        }

        for (int i = 0; i < k; i++)
        {
            R[i, i] = 1; P[i, i] = double.NaN; N[i, i] = parsed[i].Count(double.IsFinite);
            for (int j = i + 1; j < k; j++)
            {
                var (xs, ys) = CompletePairs(parsed[i], parsed[j]);
                var (r, p, n) = spearman ? Spearman(xs, ys) : Pearson(xs, ys);
                R[i, j] = R[j, i] = r;
                P[i, j] = P[j, i] = p;
                N[i, j] = N[j, i] = n;
            }
        }
        return new CorrelationResult(cols.Select(c => c.Name).ToList(), R, P, N, spearman);
    }

    /// <summary>Row-aligned pairs from two pre-parsed columns, skipping rows missing in either.</summary>
    private static (double[] X, double[] Y) CompletePairs(double[] x, double[] y)
    {
        int n = Math.Min(x.Length, y.Length);
        var xs = new List<double>(n);
        var ys = new List<double>(n);
        for (int r = 0; r < n; r++)
        {
            if (!double.IsFinite(x[r]) || !double.IsFinite(y[r])) continue;
            xs.Add(x[r]);
            ys.Add(y[r]);
        }
        return (xs.ToArray(), ys.ToArray());
    }

    private static double PFromR(double r, int n)
    {
        if (n < 3 || double.IsNaN(r)) return double.NaN;
        if (Math.Abs(r) >= 1.0) return 0.0;
        double t = r * Math.Sqrt((n - 2) / (1 - r * r));
        return 2 * (1 - new StudentT(0, 1, n - 2).CumulativeDistribution(Math.Abs(t)));
    }
}
