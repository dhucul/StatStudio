using MathNet.Numerics.Distributions;

namespace StatStudio.Core.Statistics;

public sealed record FactorialTerm(string Name, double Effect, double Coef, double SeCoef, double T, double P);

public sealed record FactorialResult(
    string Response, IReadOnlyList<string> Factors, IReadOnlyList<FactorialTerm> Terms,
    double S, double RSquared, int N, int DfError,
    IReadOnlyList<string> Aliases);

/// <summary>
/// Analysis of a 2-level factorial design (orthogonal ±1 coding). Computes effects,
/// coefficients, and — when there is pure-error replication — t/p values.
/// </summary>
public static class FactorialAnalysis
{
    public static FactorialResult Analyze(double[] y, double[][] factors, IReadOnlyList<string> factorNames,
        string response = "Y", CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StatGuard.Design(y, factors, factorNames);
        int n = y.Length;
        int k = factors.Length;
        if (k < 1 || k > 7) throw new ArgumentException("Factorial analysis supports 1..7 factors.");
        if (n < 2) throw new ArgumentException("Factorial analysis needs at least 2 runs.");

        // Code each factor to ±1 (center -> 0).
        var coded = new double[k][];
        for (int j = 0; j < k; j++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            double min = factors[j].Min(), max = factors[j].Max();
            if (min == max) throw new ArgumentException($"Factor '{factorNames[j]}' has only one level.");
            double mid = (min + max) / 2, half = (max - min) / 2;
            coded[j] = factors[j].Select(v => half > 0 ? (v - mid) / half : 0).ToArray();
            if (coded[j].Any(v => !Near(v, -1) && !Near(v, 0) && !Near(v, 1)))
                throw new ArgumentException($"Factor '{factorNames[j]}' is not two-level with optional center points.");
            coded[j] = coded[j].Select(v => Near(v, 0) ? 0.0 : Math.Sign(v)).ToArray();
        }

        var cornerGroups = new Dictionary<string, int>();
        for (int i = 0; i < n; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool corner = coded.All(c => Near(Math.Abs(c[i]), 1));
            bool center = coded.All(c => Near(c[i], 0));
            if (!corner && !center)
                throw new ArgumentException("Runs must be factorial corners or all-factor center points.");
            if (corner)
            {
                string key = string.Join(",", coded.Select(c => c[i] > 0 ? "1" : "-1"));
                cornerGroups[key] = cornerGroups.GetValueOrDefault(key) + 1;
            }
        }
        // A full design has all 2^k corners; a regular 2^(k-p) fraction has a power-of-two subset of
        // them. Requiring the full set rejected every fractional design DoeDesign can generate.
        int distinctCorners = cornerGroups.Count;
        bool isRegularDesign = distinctCorners >= 2 && (distinctCorners & (distinctCorners - 1)) == 0;
        if (!isRegularDesign || distinctCorners > (1 << k) || cornerGroups.Values.Distinct().Count() != 1)
            throw new ArgumentException(
                "Factorial runs must form a balanced full or regular fractional (2^(k-p)) design.");

        var corners = Enumerable.Range(0, n).Where(i => coded.All(c => c[i] != 0)).ToArray();
        var centers = Enumerable.Range(0, n).Where(i => coded.All(c => c[i] == 0)).ToArray();
        var masksAtCorners = corners.Select(i => Enumerable.Range(0, k)
            .Where(j => coded[j][i] > 0).Aggregate(0, (mask, j) => mask | (1 << j))).Distinct().ToArray();
        // A regular fraction is an affine subspace over GF(2), not an arbitrary
        // power-of-two subset of corners. Translate it to zero and check closure.
        var translated = masksAtCorners.Select(mask => mask ^ masksAtCorners[0]).ToHashSet();
        if (translated.Any(a => translated.Any(b => !translated.Contains(a ^ b))))
            throw new ArgumentException("The corner runs do not form a regular fractional factorial design.");

        double yBar = y.Average();
        double ssTotal = y.Sum(v => (v - yBar) * (v - yBar));

        // Pure error from replicated factor-level combinations.
        var groups = new Dictionary<string, List<double>>();
        for (int i = 0; i < n; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string key = string.Join(",", coded.Select(c => Math.Round(c[i], 6)));
            if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<double>();
            list.Add(y[i]);
        }
        double ssPe = groups.Values.Sum(g => { double m = g.Average(); return g.Sum(v => (v - m) * (v - m)); });
        int dfPe = n - groups.Count;
        double msErr = dfPe > 0 ? ssPe / dfPe : double.NaN;
        StudentT? tDist = dfPe > 0 ? new StudentT(0, 1, dfPe) : null;

        double cornerMean = corners.Average(i => y[i]);
        var terms = new List<FactorialTerm> { new("Constant", double.NaN, cornerMean,
            dfPe > 0 ? Math.Sqrt(msErr / corners.Length) : double.NaN, double.NaN, double.NaN) };
        var aliases = new List<string>();         // effects a fractional design cannot separate
        var seenContrasts = new Dictionary<string, (string Name, int Sign)>(StringComparer.Ordinal)
        {
            [new string('+', corners.Length)] = ("Constant", 1),
        };

        // Walk masks by interaction order so that, in a fractional design, each alias group is
        // represented by its lowest-order effect (A rather than BCD) — the usual convention.
        var masks = Enumerable.Range(1, (1 << k) - 1)
            .OrderBy(m => System.Numerics.BitOperations.PopCount((uint)m))
            .ThenBy(m => m);

        foreach (int mask in masks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var idx = Enumerable.Range(0, k).Where(b => (mask & (1 << b)) != 0).ToArray();
            var col = new double[n];
            for (int i = 0; i < n; i++) { double v = 1; foreach (var b in idx) v *= coded[b][i]; col[i] = v; }

            string label = string.Join("*", idx.Select(b => factorNames[b]));

            // Contrasts that agree up to sign represent one estimable effect.
            // The constant contrast is already registered, including in a negative fraction.
            int sign = Math.Sign(col[corners[0]]);
            string signature = string.Concat(corners.Select(i => sign * col[i] > 0 ? '+' : '-'));
            if (seenContrasts.TryGetValue(signature, out var alias))
            {
                aliases.Add($"{alias.Name} = {(alias.Sign == sign ? "" : "-")}{label}");
                continue;
            }
            seenContrasts[signature] = (label, sign);

            double dot = 0, ss = 0;
            for (int i = 0; i < n; i++) { dot += col[i] * y[i]; ss += col[i] * col[i]; }
            if (ss == 0) continue;
            double coef = dot / ss;
            double effect = 2 * coef;
            double se = tDist != null ? Math.Sqrt(msErr / ss) : double.NaN;
            double t = tDist != null && se > 0 ? coef / se : double.NaN;
            double p = tDist != null && !double.IsNaN(t) ? 2 * (1 - tDist.CumulativeDistribution(Math.Abs(t))) : double.NaN;
            terms.Add(new FactorialTerm(label, effect, coef, se, t, p));
        }

        if (centers.Length > 0)
        {
            double shift = centers.Average(i => y[i]) - cornerMean;
            double se = dfPe > 0 ? Math.Sqrt(msErr * (1.0 / centers.Length + 1.0 / corners.Length)) : double.NaN;
            double t = se > 0 ? shift / se : double.NaN;
            double p = tDist != null && !double.IsNaN(t) ? 2 * (1 - tDist.CumulativeDistribution(Math.Abs(t))) : double.NaN;
            terms.Add(new FactorialTerm("Center point", double.NaN, shift, se, t, p));
        }
        // All distinct estimable contrasts (and a center shift when present) fit
        // the group means exactly. Remaining variation is the replicated pure error.
        double r2 = ssTotal > 0 ? 1 - ssPe / ssTotal : double.NaN;
        return new FactorialResult(response, factorNames, terms,
            double.IsNaN(msErr) ? double.NaN : Math.Sqrt(msErr), r2, n, dfPe, aliases);
    }

    private static bool Near(double value, double expected) => Math.Abs(value - expected) <= 1e-8;
}
