namespace StatStudio.Core.Statistics;

public sealed record MixtureRun(int StdOrder, int RunOrder, string PointType, double[] Components);

public sealed record MixtureDesignResult(
    string Type, int Components, int Runs,
    IReadOnlyList<string> ComponentNames, IReadOnlyList<MixtureRun> RunList);

public static class MixtureDesign
{
    /// <summary>Simplex-lattice {q, m}: components take values 0, 1/m, …, 1 summing to 1.</summary>
    public static MixtureDesignResult SimplexLattice(int q, int m, bool randomize = true, int seed = 12345)
    {
        if (q < 2 || q > 8) throw new ArgumentException("Components must be 2..8.");
        if (m < 1 || m > 10) throw new ArgumentException("Lattice degree must be 1..10.");

        var runs = new List<MixtureRun>();
        int std = 0;
        foreach (var comp in Compositions(q, m))
        {
            var pts = comp.Select(a => (double)a / m).ToArray();
            runs.Add(new MixtureRun(++std, 0, PointType(pts), pts));
        }
        return Finalize("Simplex Lattice", q, runs, randomize, seed);
    }

    /// <summary>Simplex-centroid: the centroid of every non-empty subset of components.</summary>
    public static MixtureDesignResult SimplexCentroid(int q, bool randomize = true, int seed = 12345)
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

    private static MixtureDesignResult Finalize(string type, int q, List<MixtureRun> runs, bool randomize, int seed)
    {
        var order = Enumerable.Range(0, runs.Count).ToList();
        if (randomize)
        {
            var rnd = new Random(seed);
            for (int i = order.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); (order[i], order[j]) = (order[j], order[i]); }
        }
        var final = new List<MixtureRun>(runs.Count);
        for (int i = 0; i < order.Count; i++) final.Add(runs[order[i]] with { RunOrder = i + 1 });
        final = final.OrderBy(r => r.RunOrder).ToList();
        var names = Enumerable.Range(0, q).Select(i => ((char)('A' + i)).ToString()).ToList();
        return new MixtureDesignResult(type, q, runs.Count, names, final);
    }
}
