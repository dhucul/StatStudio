using MathNet.Numerics.Distributions;

namespace StatStudio.Core.Statistics;

public sealed record MannWhitneyResult(
    int N1, int N2, double W1, double U, double MedianDiffEstimate,
    double Z, double P, Alternative Alt);

public sealed record WilcoxonResult(int N, double WPlus, double WMinus, double Z, double P, double Median, Alternative Alt);

public sealed record KruskalWallisResult(
    IReadOnlyList<(string Name, int N, double MeanRank)> Groups,
    double H, int Df, double P);

public sealed record SignTestResult(int N, int Below, int Equal, int Above, double Median, double P, Alternative Alt);

public sealed record RunsTestResult(int N, int NAbove, int NBelow, int Runs, double Expected, double Z, double P);

/// <summary>Distribution-free (rank-based) hypothesis tests, with normal-approximation p-values.</summary>
public static class Nonparametric
{
    /// <summary>Mann-Whitney U (Wilcoxon rank-sum) for two independent samples; tie-corrected normal approx.</summary>
    public static MannWhitneyResult MannWhitney(double[] x1, double[] x2, Alternative alt = Alternative.TwoSided)
    {
        int n1 = x1.Length, n2 = x2.Length, n = n1 + n2;
        var all = x1.Concat(x2).ToArray();
        var ranks = Ranking.Average(all);
        double w1 = 0;
        for (int i = 0; i < n1; i++) w1 += ranks[i];

        // Every product below is widened before multiplying: n1*(n1+1) and n1*n2 are int
        // arithmetic that wraps negative past ~46 340 observations.
        double u1 = w1 - (double)n1 * (n1 + 1) / 2.0;
        double u = Math.Min(u1, (double)n1 * n2 - u1);

        double muU = (double)n1 * n2 / 2.0;
        double tie = Ranking.TieCorrection(all);
        double varU = ((double)n1 * n2 / 12.0) * ((n + 1) - tie / ((double)n * (n - 1)));
        double sd = Math.Sqrt(varU);
        double z = sd > 0 ? (u1 - muU) / sd : double.NaN;
        double p = PFromZ(z, alt);

        double medianDiff = Median(x1) - Median(x2);
        return new MannWhitneyResult(n1, n2, w1, u, medianDiff, z, p, alt);
    }

    /// <summary>Wilcoxon signed-rank test on differences from <paramref name="mu0"/> (one-sample or paired).</summary>
    public static WilcoxonResult WilcoxonSignedRank(double[] x, double mu0 = 0, Alternative alt = Alternative.TwoSided)
    {
        var diffs = x.Select(v => v - mu0).Where(d => d != 0).ToArray();
        int n = diffs.Length;
        var absDiffs = diffs.Select(Math.Abs).ToArray();
        var absRanks = Ranking.Average(absDiffs);

        double wPlus = 0, wMinus = 0;
        for (int i = 0; i < n; i++)
        {
            if (diffs[i] > 0) wPlus += absRanks[i];
            else wMinus += absRanks[i];
        }

        double mu = (double)n * (n + 1) / 4.0;
        double tie = Ranking.TieCorrection(absDiffs);
        double varW = (double)n * (n + 1) * (2.0 * n + 1) / 24.0 - tie / 48.0;
        double sd = Math.Sqrt(varW);
        double z = sd > 0 ? (wPlus - mu) / sd : double.NaN;
        double p = PFromZ(z, alt);
        return new WilcoxonResult(n, wPlus, wMinus, z, p, Median(x), alt);
    }

    /// <summary>Kruskal-Wallis H test for k independent groups; tie-corrected, chi-square p-value.</summary>
    public static KruskalWallisResult KruskalWallis(IReadOnlyList<(string Name, double[] Values)> groups)
    {
        var used = groups.Where(g => g.Values.Length > 0).ToList();
        int k = used.Count;
        if (k < 2) throw new ArgumentException("Kruskal-Wallis needs at least two groups.");

        var all = used.SelectMany(g => g.Values).ToArray();
        int n = all.Length;
        var ranks = Ranking.Average(all);

        var groupStats = new List<(string, int, double)>();
        double h = 0;
        int offset = 0;
        foreach (var g in used)
        {
            int gn = g.Values.Length;
            double rsum = 0;
            for (int i = 0; i < gn; i++) rsum += ranks[offset + i];
            offset += gn;
            h += rsum * rsum / gn;
            groupStats.Add((g.Name, gn, rsum / gn));
        }
        h = 12.0 / ((double)n * (n + 1)) * h - 3.0 * (n + 1);   // n*(n+1) overflows int past ~46 340

        double tie = Ranking.TieCorrection(all);
        double c = 1 - tie / ((double)n * n * n - n);
        if (c > 0) h /= c;

        int df = k - 1;
        double p = 1 - new ChiSquared(df).CumulativeDistribution(h);
        return new KruskalWallisResult(groupStats, h, df, p);
    }

    /// <summary>Sign test for the median against <paramref name="median0"/> (exact binomial).</summary>
    public static SignTestResult SignTest(double[] x, double median0 = 0, Alternative alt = Alternative.TwoSided)
    {
        int below = x.Count(v => v < median0);
        int above = x.Count(v => v > median0);
        int equal = x.Length - below - above;
        int n = below + above;

        var bin = new Binomial(0.5, n);
        double p = alt switch
        {
            Alternative.Less => bin.CumulativeDistribution(above),                 // few above
            Alternative.Greater => 1 - (above > 0 ? bin.CumulativeDistribution(above - 1) : 0),
            _ => TwoSidedBinomial(Math.Min(below, above), n),
        };
        return new SignTestResult(n, below, equal, above, Median(x), Math.Min(1.0, p), alt);
    }

    /// <summary>Wald-Wolfowitz runs test for randomness about the median; normal approximation.</summary>
    public static RunsTestResult RunsTest(double[] x)
    {
        double med = Median(x);
        var signs = x.Where(v => v != med).Select(v => v > med).ToArray();
        int n = signs.Length;
        int nA = signs.Count(s => s), nB = n - nA;

        int runs = n == 0 ? 0 : 1;
        for (int i = 1; i < n; i++) if (signs[i] != signs[i - 1]) runs++;

        // A constant column leaves every value on the median, so n can be 0 or 1 and both
        // expressions below divide by zero. Report the degenerate case as undefined instead.
        double expected = n > 0 ? 2.0 * nA * nB / n + 1 : double.NaN;
        double var = n > 1 ? 2.0 * nA * nB * (2.0 * nA * nB - n) / ((double)n * n * (n - 1)) : double.NaN;
        double sd = Math.Sqrt(var);
        double z = sd > 0 ? (runs - expected) / sd : double.NaN;
        double p = PFromZ(z, Alternative.TwoSided);
        return new RunsTestResult(n, nA, nB, runs, expected, z, p);
    }

    // ---- helpers -----------------------------------------------------------

    private static double TwoSidedBinomial(int kMin, int n)
    {
        var bin = new Binomial(0.5, n);
        return Math.Min(1.0, 2 * bin.CumulativeDistribution(kMin));
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

    private static double Median(double[] x)
    {
        var s = x.OrderBy(v => v).ToArray();
        int n = s.Length;
        if (n == 0) return double.NaN;
        return n % 2 == 1 ? s[n / 2] : (s[n / 2 - 1] + s[n / 2]) / 2.0;
    }
}
