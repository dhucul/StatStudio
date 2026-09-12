using StatStudio.Core.Inference;

namespace StatStudio.Core.Statistics;

public static class NonparametricFormatters
{
    private static string Alt(Alternative a) => HypothesisFormatters.AltSymbol(a);

    public static string MannWhitney(MannWhitneyResult r, string n1, string n2)
    {
        var t = new TextTable("Sample", "N").LeftAlign(0);
        t.Add(n1, r.N1.ToString());
        t.Add(n2, r.N2.ToString());
        var test = new TextTable("W (rank sum 1)", "U", "Z", "P-Value");
        test.Add(Fmt.N(r.W1, 1), Fmt.N(r.U, 1), Fmt.N(r.Z, 2), Fmt.P(r.P));
        return $"Mann-Whitney (Wilcoxon rank-sum): {n1} vs {n2}\n\n" + t + "\n\n" +
               $"Estimate for median difference: {Fmt.N(r.MedianDiffEstimate)}\n" +
               $"Alternative hypothesis  H1: median difference {Alt(r.Alt)} 0\n\n" + test;
    }

    public static string Wilcoxon(WilcoxonResult r, string name, double mu0)
    {
        var test = new TextTable("N", "W+", "W-", "Z", "P-Value");
        test.Add(r.N.ToString(), Fmt.N(r.WPlus, 1), Fmt.N(r.WMinus, 1), Fmt.N(r.Z, 2), Fmt.P(r.P));
        return $"Wilcoxon Signed-Rank: {name}\n\n" +
               $"Sample median: {Fmt.N(r.Median)}\n" +
               $"Null hypothesis         H0: median = {Fmt.G(mu0)}\n" +
               $"Alternative hypothesis  H1: median {Alt(r.Alt)} {Fmt.G(mu0)}\n\n" + test;
    }

    public static string KruskalWallis(KruskalWallisResult r, string response, string factor)
    {
        var t = new TextTable("Group", "N", "Mean Rank").LeftAlign(0);
        foreach (var g in r.Groups) t.Add(g.Name, g.N.ToString(), Fmt.N(g.MeanRank, 2));
        var test = new TextTable("H", "DF", "P-Value");
        test.Add(Fmt.N(r.H, 2), r.Df.ToString(), Fmt.P(r.P));
        return $"Kruskal-Wallis: {response} versus {factor}\n\n" + t + "\n\n" + test;
    }

    public static string SignTest(SignTestResult r, string name, double median0)
    {
        var t = new TextTable("N", "Below", "Equal", "Above", "P-Value");
        t.Add(r.N.ToString(), r.Below.ToString(), r.Equal.ToString(), r.Above.ToString(), Fmt.P(r.P));
        return $"Sign Test for Median: {name}\n\n" +
               $"Sample median: {Fmt.N(r.Median)}\n" +
               $"Null hypothesis         H0: median = {Fmt.G(median0)}\n" +
               $"Alternative hypothesis  H1: median {Alt(r.Alt)} {Fmt.G(median0)}\n\n" + t;
    }

    public static string RunsTest(RunsTestResult r, string name)
    {
        var t = new TextTable("N", "Above", "Below", "Runs", "Expected", "Z", "P-Value");
        t.Add(r.N.ToString(), r.NAbove.ToString(), r.NBelow.ToString(), r.Runs.ToString(),
            Fmt.N(r.Expected, 2), Fmt.N(r.Z, 2), Fmt.P(r.P));
        return $"Runs Test for Randomness: {name}\n\n" + t;
    }

    public static string Correlation(CorrelationResult r)
    {
        int k = r.Names.Count;
        string method = r.Spearman ? "Spearman rank" : "Pearson";
        var sb = new System.Text.StringBuilder();
        sb.Append($"{method} Correlation (cell contents: correlation; p-value)\n\n");

        var headers = new List<string> { "" };
        for (int j = 0; j < k - 1; j++) headers.Add(r.Names[j]);
        var t = new TextTable(headers.ToArray()).LeftAlign(0);
        for (int i = 1; i < k; i++)
        {
            var cells = new List<string> { r.Names[i] };
            for (int j = 0; j < i; j++)
                cells.Add($"{Fmt.N(r.R[i, j], 3)}; {Fmt.P(r.P[i, j])}");
            t.Add(cells.ToArray());
        }
        sb.Append(t);
        return sb.ToString();
    }

    public static string AndersonDarling(AndersonDarlingResult r, string name)
    {
        var t = new TextTable("N", "Mean", "StDev", "AD", "P-Value");
        t.Add(r.N.ToString(), Fmt.N(r.Mean), Fmt.N(r.StDev), Fmt.N(r.ASquared, 3), Fmt.P(r.P));
        // An undefined p-value is not evidence of normality, so it must not fall through to the
        // "fail to reject" branch (NaN compares false against every threshold).
        string verdict = !double.IsFinite(r.P) || r.P < 0 || r.P > 1 ? "p-value undefined: the test is inconclusive."
            : r.P < 0.05 ? "p < 0.05: reject normality."
            : "p >= 0.05: fail to reject normality.";
        return $"Anderson-Darling Normality Test: {name}\n\n" + t + "\n\n" + verdict;
    }
}
