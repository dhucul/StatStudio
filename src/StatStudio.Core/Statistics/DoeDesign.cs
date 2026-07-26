using System.Numerics;

namespace StatStudio.Core.Statistics;

public sealed record DesignRun(int StdOrder, int RunOrder, int CenterPt, double[] Factors);

public sealed record FactorialDesign(
    int Factors, int Runs, int Replicates, int CenterPoints,
    IReadOnlyList<string> FactorNames, IReadOnlyList<DesignRun> RunList);

public sealed record FractionalDesign(
    int Factors, int Runs, int Resolution, string DefiningRelation,
    IReadOnlyList<string> Generators, IReadOnlyList<string> FactorNames, IReadOnlyList<DesignRun> RunList);

public static class DoeDesign
{
    /// <summary>
    /// Full 2^k factorial design in coded units (±1), with optional replicates,
    /// center points (per replicate), and randomized run order.
    /// </summary>
    public static FactorialDesign FullFactorial(int k, int replicates = 1, int centerPoints = 0,
        bool randomize = true, int seed = 12345)
    {
        if (k < 2 || k > 7) throw new ArgumentException("Number of factors must be 2..7.");
        if (replicates < 1) throw new ArgumentOutOfRangeException(nameof(replicates));
        if (centerPoints < 0) throw new ArgumentOutOfRangeException(nameof(centerPoints));

        int baseRuns = 1 << k;
        var corner = new List<double[]>(baseRuns);
        for (int mask = 0; mask < baseRuns; mask++)
        {
            var f = new double[k];
            for (int j = 0; j < k; j++) f[j] = ((mask >> j) & 1) == 0 ? -1.0 : 1.0;
            corner.Add(f);
        }

        var runs = new List<DesignRun>();
        int std = 0;
        for (int rep = 0; rep < replicates; rep++)
        {
            foreach (var f in corner) runs.Add(new DesignRun(++std, 0, 0, (double[])f.Clone()));
            for (int c = 0; c < centerPoints; c++) runs.Add(new DesignRun(++std, 0, 1, new double[k]));
        }

        var final = Finalize(runs, randomize, seed);
        return new FactorialDesign(k, runs.Count, replicates, centerPoints, FactorNames(k), final);
    }

    // Standard minimum-aberration generators for 2^(k-p) designs, keyed by (k, runs).
    private static readonly Dictionary<(int K, int Runs), string[]> Catalog = new()
    {
        [(3, 4)] = new[] { "C=AB" },
        [(4, 8)] = new[] { "D=ABC" },
        [(5, 16)] = new[] { "E=ABCD" },
        [(5, 8)] = new[] { "D=AB", "E=AC" },
        [(6, 32)] = new[] { "F=ABCDE" },
        [(6, 16)] = new[] { "E=ABC", "F=BCD" },
        [(6, 8)] = new[] { "D=AB", "E=AC", "F=BC" },
        [(7, 64)] = new[] { "G=ABCDEF" },
        [(7, 32)] = new[] { "F=ABCD", "G=ABDE" },
        [(7, 16)] = new[] { "E=ABC", "F=BCD", "G=ACD" },
        [(7, 8)] = new[] { "D=AB", "E=AC", "F=BC", "G=ABC" },
    };

    public static IEnumerable<int> AvailableFractions(int k) =>
        Catalog.Keys.Where(key => key.K == k).Select(key => key.Runs).OrderBy(r => r);

    /// <summary>2^(k-p) fractional factorial via standard generators, with resolution and defining relation.</summary>
    public static FractionalDesign FractionalFactorial(int k, int runs, bool randomize = true, int seed = 12345)
    {
        if (!Catalog.TryGetValue((k, runs), out var gens))
            throw new ArgumentException($"No standard fractional design for {k} factors in {runs} runs.");

        int b = (int)Math.Round(Math.Log2(runs));
        var baseRuns = new List<double[]>(runs);
        for (int mask = 0; mask < runs; mask++)
        {
            var f = new double[k];
            for (int j = 0; j < b; j++) f[j] = ((mask >> j) & 1) == 0 ? -1.0 : 1.0;
            baseRuns.Add(f);
        }

        foreach (var g in gens)
        {
            int eq = g.IndexOf('=');
            int xi = g[0] - 'A';
            string src = g[(eq + 1)..];
            foreach (var f in baseRuns)
            {
                double v = 1;
                foreach (var c in src) v *= f[c - 'A'];
                f[xi] = v;
            }
        }

        var runsList = new List<DesignRun>(runs);
        int std = 0;
        foreach (var f in baseRuns) runsList.Add(new DesignRun(++std, 0, 0, (double[])f.Clone()));
        var final = Finalize(runsList, randomize, seed);

        var (relation, resolution) = DefiningRelation(gens);
        return new FractionalDesign(k, runs, resolution, relation, gens, FactorNames(k), final);
    }

    // ---- helpers -----------------------------------------------------------

    private static List<DesignRun> Finalize(List<DesignRun> runs, bool randomize, int seed)
    {
        var order = Enumerable.Range(0, runs.Count).ToList();
        if (randomize)
        {
            var rnd = new Random(seed);
            for (int i = order.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); (order[i], order[j]) = (order[j], order[i]); }
        }
        var final = new List<DesignRun>(runs.Count);
        for (int i = 0; i < order.Count; i++) final.Add(runs[order[i]] with { RunOrder = i + 1 });
        return final.OrderBy(r => r.RunOrder).ToList();
    }

    private static (string Relation, int Resolution) DefiningRelation(string[] gens)
    {
        var words = new List<int>();
        foreach (var g in gens)
        {
            int eq = g.IndexOf('=');
            int w = 1 << (g[0] - 'A');
            foreach (var c in g[(eq + 1)..]) w ^= 1 << (c - 'A');
            words.Add(w);
        }
        int p = words.Count;
        var group = new HashSet<int>();
        for (int subset = 1; subset < (1 << p); subset++)
        {
            int combo = 0;
            for (int i = 0; i < p; i++) if ((subset & (1 << i)) != 0) combo ^= words[i];
            group.Add(combo);
        }
        int resolution = group.Min(w => BitOperations.PopCount((uint)w));
        var ordered = group.OrderBy(w => BitOperations.PopCount((uint)w)).Select(WordToString);
        return ("I = " + string.Join(" = ", ordered), resolution);
    }

    private static string WordToString(int mask)
    {
        var sb = new System.Text.StringBuilder();
        for (int b = 0; b < 26; b++) if ((mask & (1 << b)) != 0) sb.Append((char)('A' + b));
        return sb.ToString();
    }

    private static List<string> FactorNames(int k) =>
        Enumerable.Range(0, k).Select(i => ((char)('A' + i)).ToString()).ToList();
}
