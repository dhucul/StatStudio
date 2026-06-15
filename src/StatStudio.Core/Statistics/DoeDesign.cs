namespace StatStudio.Core.Statistics;

public sealed record DesignRun(int StdOrder, int RunOrder, int CenterPt, double[] Factors);

public sealed record FactorialDesign(
    int Factors, int Runs, int Replicates, int CenterPoints,
    IReadOnlyList<string> FactorNames, IReadOnlyList<DesignRun> RunList);

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
        if (replicates < 1) replicates = 1;

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

        var order = Enumerable.Range(0, runs.Count).ToList();
        if (randomize)
        {
            var rnd = new Random(seed);
            for (int i = order.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); (order[i], order[j]) = (order[j], order[i]); }
        }
        var final = new List<DesignRun>(runs.Count);
        for (int i = 0; i < order.Count; i++)
        {
            var src = runs[order[i]];
            final.Add(src with { RunOrder = i + 1 });
        }
        final = final.OrderBy(r => r.RunOrder).ToList();

        var names = Enumerable.Range(0, k).Select(i => ((char)('A' + i)).ToString()).ToList();
        return new FactorialDesign(k, runs.Count, replicates, centerPoints, names, final);
    }
}
