using MathNet.Numerics.Distributions;

namespace StatStudio.Core.Statistics;

public enum Alternative { TwoSided, Less, Greater }

public sealed record OneSampleTResult(
    int N, double Mean, double StDev, double SeMean, double Mu0,
    double T, double Df, double P, double CiLow, double CiHigh, double Conf, Alternative Alt);

public sealed record TwoSampleTResult(
    bool Pooled,
    int N1, double Mean1, double StDev1, int N2, double Mean2, double StDev2,
    double Difference, double SeDiff, double T, double Df, double P,
    double CiLow, double CiHigh, double Conf, Alternative Alt);

public sealed record PairedTResult(
    int N, double Mean1, double Sd1, double Mean2, double Sd2,
    double MeanDiff, double SdDiff, double SeDiff, double T, double Df, double P,
    double CiLow, double CiHigh, double Conf, Alternative Alt);

public sealed record OnePropResult(
    int X, int N, double PHat, double P0, double Z, double P,
    double CiLow, double CiHigh, double Conf, Alternative Alt);

public sealed record TwoPropResult(
    int X1, int N1, double P1, int X2, int N2, double P2,
    double Difference, double Z, double P, double CiLow, double CiHigh, double Conf, Alternative Alt);

public sealed record ChiSquareGofResult(double ChiSq, int Df, double P,
    double[] Observed, double[] Expected);

public sealed record ContingencyResult(double ChiSq, int Df, double P,
    double[,] Observed, double[,] Expected, double[] RowTotals, double[] ColTotals, double Total);

/// <summary>Classical hypothesis tests. p-values and critical values use Math.NET distributions.</summary>
public static class HypothesisTests
{
    // ---- t-tests -----------------------------------------------------------

    public static OneSampleTResult OneSampleT(double[] x, double mu0 = 0,
        double conf = 0.95, Alternative alt = Alternative.TwoSided)
    {
        int n = x.Length;
        double mean = Mean(x), sd = StdDev(x), se = sd / Math.Sqrt(n);
        double df = n - 1;
        double t = se > 0 ? (mean - mu0) / se : double.NaN;
        double p = PFromT(t, df, alt);
        var (lo, hi) = TInterval(mean, se, df, conf, alt);
        return new OneSampleTResult(n, mean, sd, se, mu0, t, df, p, lo, hi, conf, alt);
    }

    public static TwoSampleTResult TwoSampleT(double[] x1, double[] x2, bool pooled = false,
        double conf = 0.95, Alternative alt = Alternative.TwoSided)
    {
        int n1 = x1.Length, n2 = x2.Length;
        double m1 = Mean(x1), m2 = Mean(x2), s1 = StdDev(x1), s2 = StdDev(x2);
        double v1 = s1 * s1, v2 = s2 * s2;
        double diff = m1 - m2;

        double se, df;
        if (pooled)
        {
            double sp2 = ((n1 - 1) * v1 + (n2 - 1) * v2) / (n1 + n2 - 2);
            se = Math.Sqrt(sp2 * (1.0 / n1 + 1.0 / n2));
            df = n1 + n2 - 2;
        }
        else
        {
            se = Math.Sqrt(v1 / n1 + v2 / n2);
            double num = Math.Pow(v1 / n1 + v2 / n2, 2);
            double den = Math.Pow(v1 / n1, 2) / (n1 - 1) + Math.Pow(v2 / n2, 2) / (n2 - 1);
            df = num / den;
        }

        double t = se > 0 ? diff / se : double.NaN;
        double p = PFromT(t, df, alt);
        var (lo, hi) = TInterval(diff, se, df, conf, alt);
        return new TwoSampleTResult(pooled, n1, m1, s1, n2, m2, s2, diff, se, t, df, p, lo, hi, conf, alt);
    }

    public static PairedTResult PairedT(double[] x1, double[] x2,
        double conf = 0.95, Alternative alt = Alternative.TwoSided)
    {
        if (x1.Length != x2.Length)
            throw new ArgumentException("Paired t requires equal-length, row-matched samples.");
        int n = x1.Length;
        var d = new double[n];
        for (int i = 0; i < n; i++) d[i] = x1[i] - x2[i];

        double md = Mean(d), sdd = StdDev(d), se = sdd / Math.Sqrt(n);
        double df = n - 1;
        double t = se > 0 ? md / se : double.NaN;
        double p = PFromT(t, df, alt);
        var (lo, hi) = TInterval(md, se, df, conf, alt);
        return new PairedTResult(n, Mean(x1), StdDev(x1), Mean(x2), StdDev(x2),
            md, sdd, se, t, df, p, lo, hi, conf, alt);
    }

    // ---- proportions (normal approximation) --------------------------------

    public static OnePropResult OneProportion(int x, int n, double p0 = 0.5,
        double conf = 0.95, Alternative alt = Alternative.TwoSided)
    {
        double phat = (double)x / n;
        double seTest = Math.Sqrt(p0 * (1 - p0) / n);
        double z = seTest > 0 ? (phat - p0) / seTest : double.NaN;
        double p = PFromZ(z, alt);

        // Wald CI on the observed proportion.
        double seCi = Math.Sqrt(phat * (1 - phat) / n);
        var (lo, hi) = ZInterval(phat, seCi, conf, alt, 0, 1);
        return new OnePropResult(x, n, phat, p0, z, p, lo, hi, conf, alt);
    }

    public static TwoPropResult TwoProportions(int x1, int n1, int x2, int n2,
        double conf = 0.95, Alternative alt = Alternative.TwoSided)
    {
        double p1 = (double)x1 / n1, p2 = (double)x2 / n2, diff = p1 - p2;
        double pPool = (double)(x1 + x2) / (n1 + n2);
        double seTest = Math.Sqrt(pPool * (1 - pPool) * (1.0 / n1 + 1.0 / n2));
        double z = seTest > 0 ? diff / seTest : double.NaN;
        double p = PFromZ(z, alt);

        double seCi = Math.Sqrt(p1 * (1 - p1) / n1 + p2 * (1 - p2) / n2);
        var (lo, hi) = ZInterval(diff, seCi, conf, alt, -1, 1);
        return new TwoPropResult(x1, n1, p1, x2, n2, p2, diff, z, p, lo, hi, conf, alt);
    }

    // ---- chi-square --------------------------------------------------------

    public static ChiSquareGofResult ChiSquareGof(double[] observed, double[]? expected = null)
    {
        int k = observed.Length;
        double total = observed.Sum();
        var exp = expected ?? Enumerable.Repeat(total / k, k).ToArray();
        double chi = 0;
        for (int i = 0; i < k; i++) chi += (observed[i] - exp[i]) * (observed[i] - exp[i]) / exp[i];
        int df = k - 1;
        double p = ChiUpperTail(chi, df);
        return new ChiSquareGofResult(chi, df, p, observed, exp);
    }

    public static ContingencyResult ChiSquareAssociation(double[,] table)
    {
        int rows = table.GetLength(0), cols = table.GetLength(1);
        var rowT = new double[rows];
        var colT = new double[cols];
        double total = 0;
        for (int i = 0; i < rows; i++)
            for (int j = 0; j < cols; j++) { rowT[i] += table[i, j]; colT[j] += table[i, j]; total += table[i, j]; }

        var exp = new double[rows, cols];
        double chi = 0;
        for (int i = 0; i < rows; i++)
            for (int j = 0; j < cols; j++)
            {
                exp[i, j] = rowT[i] * colT[j] / total;
                double diff = table[i, j] - exp[i, j];
                if (exp[i, j] > 0) chi += diff * diff / exp[i, j];
            }
        int df = (rows - 1) * (cols - 1);
        double p = ChiUpperTail(chi, df);
        return new ContingencyResult(chi, df, p, table, exp, rowT, colT, total);
    }

    // ---- distribution helpers ---------------------------------------------

    private static double PFromT(double t, double df, Alternative alt)
    {
        if (double.IsNaN(t)) return double.NaN;
        var d = new StudentT(0, 1, df);
        return alt switch
        {
            Alternative.Less => d.CumulativeDistribution(t),
            Alternative.Greater => 1 - d.CumulativeDistribution(t),
            _ => 2 * (1 - d.CumulativeDistribution(Math.Abs(t))),
        };
    }

    private static double PFromZ(double z, Alternative alt)
    {
        if (double.IsNaN(z)) return double.NaN;
        return alt switch
        {
            Alternative.Less => Normal.CDF(0, 1, z),
            Alternative.Greater => 1 - Normal.CDF(0, 1, z),
            _ => 2 * (1 - Normal.CDF(0, 1, Math.Abs(z))),
        };
    }

    private static (double, double) TInterval(double est, double se, double df, double conf, Alternative alt)
    {
        var d = new StudentT(0, 1, df);
        if (alt == Alternative.TwoSided)
        {
            double tc = d.InverseCumulativeDistribution(1 - (1 - conf) / 2);
            return (est - tc * se, est + tc * se);
        }
        double t1 = d.InverseCumulativeDistribution(conf);
        return alt == Alternative.Less
            ? (double.NegativeInfinity, est + t1 * se)
            : (est - t1 * se, double.PositiveInfinity);
    }

    private static (double, double) ZInterval(double est, double se, double conf, Alternative alt,
        double clampLo, double clampHi)
    {
        double Clamp(double v) => Math.Max(clampLo, Math.Min(clampHi, v));
        if (alt == Alternative.TwoSided)
        {
            double zc = Normal.InvCDF(0, 1, 1 - (1 - conf) / 2);
            return (Clamp(est - zc * se), Clamp(est + zc * se));
        }
        double z1 = Normal.InvCDF(0, 1, conf);
        return alt == Alternative.Less
            ? (clampLo, Clamp(est + z1 * se))
            : (Clamp(est - z1 * se), clampHi);
    }

    private static double ChiUpperTail(double x, int df) =>
        df <= 0 ? double.NaN : 1 - new ChiSquared(df).CumulativeDistribution(x);

    private static double Mean(double[] x)
    {
        double s = 0;
        foreach (var v in x) s += v;
        return s / x.Length;
    }

    private static double StdDev(double[] x)
    {
        int n = x.Length;
        if (n < 2) return 0;
        double m = Mean(x), ss = 0;
        foreach (var v in x) ss += (v - m) * (v - m);
        return Math.Sqrt(ss / (n - 1));
    }
}
