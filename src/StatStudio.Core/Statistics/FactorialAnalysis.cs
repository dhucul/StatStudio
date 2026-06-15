using MathNet.Numerics.Distributions;

namespace StatStudio.Core.Statistics;

public sealed record FactorialTerm(string Name, double Effect, double Coef, double SeCoef, double T, double P);

public sealed record FactorialResult(
    string Response, IReadOnlyList<string> Factors, IReadOnlyList<FactorialTerm> Terms,
    double S, double RSquared, int N, int DfError);

/// <summary>
/// Analysis of a 2-level factorial design (orthogonal ±1 coding). Computes effects,
/// coefficients, and — when there is pure-error replication — t/p values.
/// </summary>
public static class FactorialAnalysis
{
    public static FactorialResult Analyze(double[] y, double[][] factors, IReadOnlyList<string> factorNames,
        string response = "Y")
    {
        int n = y.Length;
        int k = factors.Length;
        if (k < 1 || k > 7) throw new ArgumentException("Factorial analysis supports 1..7 factors.");

        // Code each factor to ±1 (center -> 0).
        var coded = new double[k][];
        for (int j = 0; j < k; j++)
        {
            double min = factors[j].Min(), max = factors[j].Max();
            double mid = (min + max) / 2, half = (max - min) / 2;
            coded[j] = factors[j].Select(v => half > 0 ? (v - mid) / half : 0).ToArray();
        }

        double yBar = y.Average();
        double ssTotal = y.Sum(v => (v - yBar) * (v - yBar));

        // Pure error from replicated factor-level combinations.
        var groups = new Dictionary<string, List<double>>();
        for (int i = 0; i < n; i++)
        {
            string key = string.Join(",", coded.Select(c => Math.Round(c[i], 6)));
            if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<double>();
            list.Add(y[i]);
        }
        double ssPe = groups.Values.Sum(g => { double m = g.Average(); return g.Sum(v => (v - m) * (v - m)); });
        int dfPe = n - groups.Count;
        double msErr = dfPe > 0 ? ssPe / dfPe : double.NaN;
        StudentT? tDist = dfPe > 0 ? new StudentT(0, 1, dfPe) : null;

        var terms = new List<FactorialTerm> { new("Constant", double.NaN, yBar, double.NaN, double.NaN, double.NaN) };
        double ssModel = 0;
        for (int mask = 1; mask < (1 << k); mask++)
        {
            var idx = Enumerable.Range(0, k).Where(b => (mask & (1 << b)) != 0).ToArray();
            var col = new double[n];
            for (int i = 0; i < n; i++) { double v = 1; foreach (var b in idx) v *= coded[b][i]; col[i] = v; }
            double dot = 0, ss = 0;
            for (int i = 0; i < n; i++) { dot += col[i] * y[i]; ss += col[i] * col[i]; }
            if (ss == 0) continue;
            double coef = dot / ss;
            double effect = 2 * coef;
            ssModel += coef * coef * ss;
            double se = tDist != null ? Math.Sqrt(msErr / ss) : double.NaN;
            double t = tDist != null && se > 0 ? coef / se : double.NaN;
            double p = tDist != null && !double.IsNaN(t) ? 2 * (1 - tDist.CumulativeDistribution(Math.Abs(t))) : double.NaN;
            terms.Add(new FactorialTerm(string.Concat(idx.Select(b => factorNames[b])), effect, coef, se, t, p));
        }

        // Order: constant, then by interaction order, then by appearance.
        var ordered = terms.OrderBy(t => t.Name == "Constant" ? -1 : t.Name.Length).ToList();
        double r2 = ssTotal > 0 ? ssModel / ssTotal : double.NaN;
        return new FactorialResult(response, factorNames, ordered, double.IsNaN(msErr) ? double.NaN : Math.Sqrt(msErr), r2, n, dfPe);
    }
}
