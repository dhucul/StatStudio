using MathNet.Numerics.Distributions;

namespace StatStudio.Core.Statistics;

public sealed record FTestResult(
    int N1, double StDev1, int N2, double StDev2, double F, int Df1, int Df2, double P,
    double RatioCiLow, double RatioCiHigh, double Conf);

public sealed record EqualVarianceResult(
    IReadOnlyList<(string Name, int N, double StDev)> Groups,
    double Bartlett, int BartlettDf, double BartlettP,
    double Levene, int LeveneDf1, int LeveneDf2, double LeveneP);

public static class VarianceTests
{
    /// <summary>F-test for the ratio of two variances (two-sided), with a CI for σ1²/σ2².</summary>
    public static FTestResult FTest(double[] x1, double[] x2, double conf = 0.95)
    {
        RequireSample(x1, nameof(x1));
        RequireSample(x2, nameof(x2));
        StatGuard.Probability(conf, nameof(conf));

        int n1 = x1.Length, n2 = x2.Length;
        double v1 = Variance(x1), v2 = Variance(x2);
        if (v1 <= 0 || v2 <= 0)
            throw new ArgumentException("Both samples must contain variation.");
        double f = v1 / v2;
        int df1 = n1 - 1, df2 = n2 - 1;
        var dist = new FisherSnedecor(df1, df2);
        double cdf = dist.CumulativeDistribution(f);
        double p = 2 * Math.Min(cdf, 1 - cdf);

        double alpha = 1 - conf;
        double fLo = new FisherSnedecor(df1, df2).InverseCumulativeDistribution(alpha / 2);
        double fHi = new FisherSnedecor(df1, df2).InverseCumulativeDistribution(1 - alpha / 2);
        // CI for σ1²/σ2²: (F/F_{1-α/2}, F/F_{α/2})
        return new FTestResult(n1, Math.Sqrt(v1), n2, Math.Sqrt(v2), f, df1, df2,
            Math.Min(1, p), f / fHi, f / fLo, conf);
    }

    /// <summary>Test for equal variances across k groups: Bartlett (normal) and Levene/Brown-Forsythe (robust).</summary>
    public static EqualVarianceResult EqualVariances(IReadOnlyList<(string Name, double[] Values)> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);
        foreach (var group in groups)
            RequireSample(group.Values, nameof(groups));
        var used = groups.ToList();
        int k = used.Count;
        if (k < 2) throw new ArgumentException("Need at least two groups with >1 observation.");

        int N = used.Sum(g => g.Values.Length);
        var stats = used.Select(g => (g.Name, g.Values.Length, Math.Sqrt(Variance(g.Values)))).ToList();

        // Bartlett
        if (used.Any(g => Variance(g.Values) <= 0))
            throw new ArgumentException("Every group must contain variation.", nameof(groups));
        double pooledVar = used.Sum(g => (g.Values.Length - 1) * Variance(g.Values)) / (N - k);
        double sumLn = used.Sum(g => (g.Values.Length - 1) * Math.Log(Variance(g.Values)));
        double bNum = (N - k) * Math.Log(pooledVar) - sumLn;
        double c = 1 + (used.Sum(g => 1.0 / (g.Values.Length - 1)) - 1.0 / (N - k)) / (3.0 * (k - 1));
        double bartlett = bNum / c;
        int bartlettDf = k - 1;
        double bartlettP = 1 - new ChiSquared(bartlettDf).CumulativeDistribution(bartlett);

        // Levene (Brown-Forsythe): one-way ANOVA on |x − group median|
        var z = used.Select(g =>
        {
            double med = Median(g.Values);
            return (g.Name, g.Values.Select(v => Math.Abs(v - med)).ToArray());
        }).ToList();
        var la = Anova.OneWay(z);
        return new EqualVarianceResult(stats, bartlett, bartlettDf, bartlettP,
            la.F, la.DfFactor, la.DfError, la.P);
    }

    private static double Variance(double[] x)
    {
        int n = x.Length;
        if (n < 2) return double.NaN;
        double m = x.Average(), ss = 0;
        foreach (var v in x) ss += (v - m) * (v - m);
        return ss / (n - 1);
    }

    private static void RequireSample(double[] values, string paramName)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length < 2)
            throw new ArgumentException("At least two observations are required.", paramName);
        StatGuard.Finite(values, paramName);
    }

    private static double Median(double[] x)
    {
        var s = x.OrderBy(v => v).ToArray();
        int n = s.Length;
        return n % 2 == 1 ? s[n / 2] : (s[n / 2 - 1] + s[n / 2]) / 2.0;
    }
}
