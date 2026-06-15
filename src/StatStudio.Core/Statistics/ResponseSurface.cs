namespace StatStudio.Core.Statistics;

public sealed record RsmRun(int StdOrder, int RunOrder, string PointType, double[] Factors);

public sealed record RsmDesign(
    string Type, int Factors, int Runs, double Alpha,
    IReadOnlyList<string> FactorNames, IReadOnlyList<RsmRun> RunList);

public static class ResponseSurface
{
    /// <summary>Central composite design: factorial cube + axial (±α) + center points.</summary>
    public static RsmDesign CentralComposite(int k, int centerPoints = 4, bool faceCentered = false,
        bool randomize = true, int seed = 12345)
    {
        if (k < 2 || k > 5) throw new ArgumentException("CCD supports 2..5 factors.");
        int cube = 1 << k;
        double alpha = faceCentered ? 1.0 : Math.Pow(cube, 0.25);

        var runs = new List<RsmRun>();
        int std = 0;
        for (int mask = 0; mask < cube; mask++)
        {
            var f = new double[k];
            for (int j = 0; j < k; j++) f[j] = ((mask >> j) & 1) == 0 ? -1.0 : 1.0;
            runs.Add(new RsmRun(++std, 0, "Cube", f));
        }
        for (int j = 0; j < k; j++)
        {
            var fp = new double[k]; fp[j] = alpha; runs.Add(new RsmRun(++std, 0, "Axial", fp));
            var fm = new double[k]; fm[j] = -alpha; runs.Add(new RsmRun(++std, 0, "Axial", fm));
        }
        for (int c = 0; c < centerPoints; c++) runs.Add(new RsmRun(++std, 0, "Center", new double[k]));

        return Finalize("Central Composite", k, alpha, runs, randomize, seed);
    }

    /// <summary>Box-Behnken design: all factor-pairs at ±1 (others 0) + center points (k = 3..5).</summary>
    public static RsmDesign BoxBehnken(int k, int centerPoints = 3, bool randomize = true, int seed = 12345)
    {
        if (k < 3 || k > 5) throw new ArgumentException("Box-Behnken supports 3..5 factors.");
        var runs = new List<RsmRun>();
        int std = 0;
        for (int i = 0; i < k; i++)
            for (int j = i + 1; j < k; j++)
                foreach (var si in new[] { -1.0, 1.0 })
                    foreach (var sj in new[] { -1.0, 1.0 })
                    {
                        var f = new double[k]; f[i] = si; f[j] = sj;
                        runs.Add(new RsmRun(++std, 0, "Edge", f));
                    }
        for (int c = 0; c < centerPoints; c++) runs.Add(new RsmRun(++std, 0, "Center", new double[k]));

        return Finalize("Box-Behnken", k, 1.0, runs, randomize, seed);
    }

    /// <summary>Fits the full second-order (quadratic) response-surface model by OLS.</summary>
    public static RegressionResult Analyze(double[] y, double[][] factors, IReadOnlyList<string> names,
        string response = "Y")
    {
        var (preds, termNames) = QuadraticTerms(factors, names);
        return Regression.Fit(y, preds, termNames, response);
    }

    /// <summary>Linear, square, and two-way interaction columns for the quadratic model.</summary>
    public static (double[][] Predictors, string[] Names) QuadraticTerms(double[][] factors, IReadOnlyList<string> names)
    {
        int k = factors.Length, n = factors[0].Length;
        var preds = new List<double[]>();
        var nm = new List<string>();
        for (int j = 0; j < k; j++) { preds.Add(factors[j]); nm.Add(names[j]); }
        for (int j = 0; j < k; j++) { preds.Add(factors[j].Select(v => v * v).ToArray()); nm.Add($"{names[j]}*{names[j]}"); }
        for (int i = 0; i < k; i++)
            for (int j = i + 1; j < k; j++)
            {
                var col = new double[n];
                for (int r = 0; r < n; r++) col[r] = factors[i][r] * factors[j][r];
                preds.Add(col); nm.Add($"{names[i]}*{names[j]}");
            }
        return (preds.ToArray(), nm.ToArray());
    }

    private static RsmDesign Finalize(string type, int k, double alpha, List<RsmRun> runs, bool randomize, int seed)
    {
        var order = Enumerable.Range(0, runs.Count).ToList();
        if (randomize)
        {
            var rnd = new Random(seed);
            for (int i = order.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); (order[i], order[j]) = (order[j], order[i]); }
        }
        var final = new List<RsmRun>(runs.Count);
        for (int i = 0; i < order.Count; i++) final.Add(runs[order[i]] with { RunOrder = i + 1 });
        final = final.OrderBy(r => r.RunOrder).ToList();
        var fnames = Enumerable.Range(0, k).Select(i => ((char)('A' + i)).ToString()).ToList();
        return new RsmDesign(type, k, runs.Count, alpha, fnames, final);
    }
}
