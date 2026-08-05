namespace StatStudio.Core.Statistics;

public sealed record MixtureRun(int StdOrder, int RunOrder, string PointType, double[] Components);

public sealed record MixtureDesignResult(
    string Type, int Components, int Runs,
    IReadOnlyList<string> ComponentNames, IReadOnlyList<MixtureRun> RunList);

public static class MixtureDesign
{
    /// <summary>Simplex-lattice {q, m}: components take values 0, 1/m, …, 1 summing to 1.</summary>
    /// <param name="seed">Omit for a fresh run order; pass a value to reproduce a design.</param>
    public static MixtureDesignResult SimplexLattice(int q, int m, bool randomize = true, int? seed = null)
    {
        if (q < 2 || q > 8) throw new ArgumentException("Components must be 2..8.");
        if (m < 1 || m > 10) throw new ArgumentException("Lattice degree must be 1..10.");

        // C(m+q-1, q-1) grows fast: {8, 10} alone is 19 448 runs. Reject the unusable sizes up
        // front instead of building a worksheet nobody can run.
        long runCount = Binomial(m + q - 1, q - 1);
        if (runCount > MaxLatticeRuns)
            throw new ArgumentException(
                $"A {{{q}, {m}}} simplex lattice needs {runCount} runs, over the {MaxLatticeRuns} limit. " +
                "Reduce the number of components or the lattice degree.");

        var runs = new List<MixtureRun>();
        int std = 0;
        foreach (var comp in Compositions(q, m))
        {
            var pts = comp.Select(a => (double)a / m).ToArray();
            runs.Add(new MixtureRun(++std, 0, PointType(pts), pts));
        }
        return Finalize("Simplex Lattice", q, runs, randomize, seed);
    }

    /// <summary>Largest simplex-lattice design that is still practical to run and edit.</summary>
    private const int MaxLatticeRuns = 1_000;

    private static long Binomial(int n, int k)
    {
        if (k < 0 || k > n) return 0;
        k = Math.Min(k, n - k);
        long result = 1;
        for (int i = 1; i <= k; i++) result = result * (n - k + i) / i;
        return result;
    }

    /// <summary>Simplex-centroid: the centroid of every non-empty subset of components.</summary>
    /// <param name="seed">Omit for a fresh run order; pass a value to reproduce a design.</param>
    public static MixtureDesignResult SimplexCentroid(int q, bool randomize = true, int? seed = null)
    {
        if (q < 2 || q > 8) throw new ArgumentException("Components must be 2..8.");
        var runs = new List<MixtureRun>();
        int std = 0;
        for (int mask = 1; mask < (1 << q); mask++)
        {
            int count = System.Numerics.BitOperations.PopCount((uint)mask);
            var pts = new double[q];
            for (int j = 0; j < q; j++) if ((mask & (1 << j)) != 0) pts[j] = 1.0 / count;
            runs.Add(new MixtureRun(++std, 0, PointType(pts), pts));
        }
        return Finalize("Simplex Centroid", q, runs, randomize, seed);
    }

    // ---- helpers -----------------------------------------------------------

    private static IEnumerable<int[]> Compositions(int q, int m)
    {
        var current = new int[q];
        return Recurse(0, m);

        IEnumerable<int[]> Recurse(int pos, int remaining)
        {
            if (pos == q - 1) { current[pos] = remaining; yield return (int[])current.Clone(); yield break; }
            for (int v = 0; v <= remaining; v++)
            {
                current[pos] = v;
                foreach (var r in Recurse(pos + 1, remaining - v)) yield return r;
            }
        }
    }

    private static string PointType(double[] pts)
    {
        int nonzero = pts.Count(v => v > 0);
        return nonzero == 1 ? "Pure" : nonzero == pts.Length ? "Centroid" : "Blend";
    }

    private static MixtureDesignResult Finalize(string type, int q, List<MixtureRun> runs, bool randomize, int? seed)
    {
        var order = Enumerable.Range(0, runs.Count).ToList();
        if (randomize)
        {
            var rnd = seed is null ? Random.Shared : new Random(seed.Value);
            for (int i = order.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); (order[i], order[j]) = (order[j], order[i]); }
        }
        var final = new List<MixtureRun>(runs.Count);
        for (int i = 0; i < order.Count; i++) final.Add(runs[order[i]] with { RunOrder = i + 1 });
        var names = Enumerable.Range(0, q).Select(i => ((char)('A' + i)).ToString()).ToList();
        return new MixtureDesignResult(type, q, runs.Count, names, final);
    }
}
