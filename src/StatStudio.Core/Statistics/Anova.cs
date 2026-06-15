using MathNet.Numerics.Distributions;

namespace StatStudio.Core.Statistics;

public sealed record GroupStats(string Name, int N, double Mean, double StDev);

public sealed record OneWayAnovaResult(
    IReadOnlyList<GroupStats> Groups,
    double SsFactor, double SsError, double SsTotal,
    int DfFactor, int DfError, int DfTotal,
    double MsFactor, double MsError,
    double F, double P,
    double PooledStDev, double RSquared, double RSquaredAdj);

public static class Anova
{
    /// <summary>One-way (single-factor) ANOVA across the given groups.</summary>
    public static OneWayAnovaResult OneWay(IReadOnlyList<(string Name, double[] Values)> groups)
    {
        var used = groups.Where(g => g.Values.Length > 0).ToList();
        int k = used.Count;
        if (k < 2) throw new ArgumentException("One-way ANOVA needs at least two non-empty groups.");

        int nTotal = used.Sum(g => g.Values.Length);
        double grand = used.SelectMany(g => g.Values).Average();

        double ssFactor = 0, ssError = 0;
        var groupStats = new List<GroupStats>(k);
        foreach (var g in used)
        {
            int n = g.Values.Length;
            double mean = g.Values.Average();
            ssFactor += n * (mean - grand) * (mean - grand);

            double ss = 0;
            foreach (var v in g.Values) ss += (v - mean) * (v - mean);
            ssError += ss;
            double sd = n > 1 ? Math.Sqrt(ss / (n - 1)) : 0;
            groupStats.Add(new GroupStats(g.Name, n, mean, sd));
        }

        double ssTotal = ssFactor + ssError;
        int dfFactor = k - 1, dfError = nTotal - k, dfTotal = nTotal - 1;
        double msFactor = ssFactor / dfFactor;
        double msError = dfError > 0 ? ssError / dfError : double.NaN;
        double f = msError > 0 ? msFactor / msError : double.NaN;
        double p = (dfError > 0 && !double.IsNaN(f))
            ? 1 - new FisherSnedecor(dfFactor, dfError).CumulativeDistribution(f)
            : double.NaN;

        double pooledSd = dfError > 0 ? Math.Sqrt(msError) : double.NaN;
        double r2 = ssTotal > 0 ? ssFactor / ssTotal : double.NaN;
        double r2adj = (ssTotal > 0 && dfTotal > 0 && dfError > 0)
            ? 1 - (ssError / dfError) / (ssTotal / dfTotal)
            : double.NaN;

        return new OneWayAnovaResult(groupStats, ssFactor, ssError, ssTotal,
            dfFactor, dfError, dfTotal, msFactor, msError, f, p, pooledSd, r2, r2adj);
    }
}
