using MathNet.Numerics.Distributions;

namespace StatStudio.Core.Statistics;

public sealed record TwoWayAnovaResult(
    string FactorA, string FactorB,
    double SsA, double SsB, double SsAB, double SsError, double SsTotal,
    int DfA, int DfB, int DfAB, int DfError, int DfTotal,
    double MsA, double MsB, double MsAB, double MsError,
    double FA, double FB, double FAB, double PA, double PB, double PAB,
    double S, double RSquared);

public sealed record TukeyComparison(
    string GroupA, string GroupB, double Difference, double Se, double Q, double P, double CiLow, double CiHigh);

public sealed record TukeyResult(double Conf, double QCritical, IReadOnlyList<TukeyComparison> Comparisons);

public static class AnovaExtensions
{
    /// <summary>Balanced two-way ANOVA with interaction (equal replicates per A×B cell).</summary>
    public static TwoWayAnovaResult TwoWay(double[] response, string[] factorA, string[] factorB,
        string nameA = "A", string nameB = "B")
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(factorA);
        ArgumentNullException.ThrowIfNull(factorB);
        if (response.Length == 0 || factorA.Length != response.Length || factorB.Length != response.Length)
            throw new ArgumentException("Response and factor arrays must be nonempty and have equal lengths.");
        StatGuard.Finite(response, nameof(response));
        if (factorA.Any(string.IsNullOrWhiteSpace) || factorB.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Factor labels cannot be empty.");

        int N = response.Length;
        var aLevels = factorA.Distinct().OrderBy(s => s).ToList();
        var bLevels = factorB.Distinct().OrderBy(s => s).ToList();
        int a = aLevels.Count, b = bLevels.Count;
        if (a < 2 || b < 2)
            throw new ArgumentException("Each factor needs at least two levels.");
        var aIndex = aLevels.Select((value, index) => (value, index)).ToDictionary(x => x.value, x => x.index);
        var bIndex = bLevels.Select((value, index) => (value, index)).ToDictionary(x => x.value, x => x.index);

        var cell = new Dictionary<(int, int), List<double>>();
        for (int i = 0; i < a; i++) for (int j = 0; j < b; j++) cell[(i, j)] = new List<double>();
        for (int r = 0; r < N; r++)
            cell[(aIndex[factorA[r]], bIndex[factorB[r]])].Add(response[r]);

        int n = cell[(0, 0)].Count;
        if (cell.Values.Any(c => c.Count != n) || n < 2)
            throw new ArgumentException("Two-way ANOVA requires a balanced design with at least 2 observations per cell.");

        double grand = response.Average();
        double[] meanA = Enumerable.Range(0, a).Select(i => Enumerable.Range(0, b).SelectMany(j => cell[(i, j)]).Average()).ToArray();
        double[] meanB = Enumerable.Range(0, b).Select(j => Enumerable.Range(0, a).SelectMany(i => cell[(i, j)]).Average()).ToArray();

        double ssA = b * n * meanA.Sum(m => (m - grand) * (m - grand));
        double ssB = a * n * meanB.Sum(m => (m - grand) * (m - grand));
        double ssAB = 0, ssError = 0;
        for (int i = 0; i < a; i++)
            for (int j = 0; j < b; j++)
            {
                double cm = cell[(i, j)].Average();
                ssAB += n * Math.Pow(cm - meanA[i] - meanB[j] + grand, 2);
                foreach (var v in cell[(i, j)]) ssError += (v - cm) * (v - cm);
            }
        double ssTotal = response.Sum(v => (v - grand) * (v - grand));

        int dfA = a - 1, dfB = b - 1, dfAB = (a - 1) * (b - 1), dfError = a * b * (n - 1), dfTotal = N - 1;
        double msA = ssA / dfA, msB = ssB / dfB, msAB = dfAB > 0 ? ssAB / dfAB : double.NaN;
        double msError = dfError > 0 ? ssError / dfError : double.NaN;

        double FA = msA / msError, FB = msB / msError, FAB = msAB / msError;
        double pA = PF(FA, dfA, dfError), pB = PF(FB, dfB, dfError), pAB = PF(FAB, dfAB, dfError);

        return new TwoWayAnovaResult(nameA, nameB, ssA, ssB, ssAB, ssError, ssTotal,
            dfA, dfB, dfAB, dfError, dfTotal, msA, msB, msAB, msError,
            FA, FB, FAB, pA, pB, pAB, Math.Sqrt(msError), ssTotal > 0 ? 1 - ssError / ssTotal : double.NaN);
    }

    /// <summary>Tukey HSD (Tukey-Kramer) all-pairwise comparisons for a one-way layout.</summary>
    public static TukeyResult Tukey(IReadOnlyList<(string Name, double[] Values)> groups, double conf = 0.95)
    {
        var anova = Anova.OneWay(groups);
        int k = anova.Groups.Count;
        double mse = anova.MsError;
        int dfError = anova.DfError;
        double qCrit = StudentizedRange.InverseCDF(conf, k, dfError);

        var comps = new List<TukeyComparison>();
        for (int i = 0; i < k; i++)
            for (int j = i + 1; j < k; j++)
            {
                var gi = anova.Groups[i];
                var gj = anova.Groups[j];
                double diff = gi.Mean - gj.Mean;
                double se = Math.Sqrt(mse * 0.5 * (1.0 / gi.N + 1.0 / gj.N));
                double q = se > 0 ? Math.Abs(diff) / se : double.NaN;
                double p = 1 - StudentizedRange.CDF(q, k, dfError);
                comps.Add(new TukeyComparison(gi.Name, gj.Name, diff, se, q, p,
                    diff - qCrit * se, diff + qCrit * se));
            }
        return new TukeyResult(conf, qCrit, comps);
    }

    private static double PF(double f, int d1, int d2)
    {
        if (double.IsNaN(f) || d1 <= 0 || d2 <= 0) return double.NaN;
        return 1 - new FisherSnedecor(d1, d2).CumulativeDistribution(f);
    }
}
