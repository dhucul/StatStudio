using StatStudio.Core.Inference;

namespace StatStudio.Core.Statistics;

/// <summary>Formats hypothesis-test results as Minitab-style Session text.</summary>
public static class HypothesisFormatters
{
    public static string AltSymbol(Alternative a) => a switch
    {
        Alternative.Less => "<",
        Alternative.Greater => ">",
        _ => "≠", // not-equal
    };

    private static string Pct(double conf) => $"{conf * 100:0.#}%";

    private static string Ci(double lo, double hi)
    {
        string l = double.IsNegativeInfinity(lo) ? "-inf" : Fmt.N(lo);
        string h = double.IsPositiveInfinity(hi) ? "inf" : Fmt.N(hi);
        return $"({l}, {h})";
    }

    public static string OneSampleT(OneSampleTResult r, string variable, double mu0)
    {
        var stats = new TextTable("N", "Mean", "StDev", "SE Mean", $"{Pct(r.Conf)} CI for μ");
        stats.Add(r.N.ToString(), Fmt.N(r.Mean), Fmt.N(r.StDev), Fmt.N(r.SeMean), Ci(r.CiLow, r.CiHigh));

        var test = new TextTable("T-Value", "DF", "P-Value");
        test.Add(Fmt.N(r.T, 2), r.Df.ToString("0"), Fmt.P(r.P));

        return $"One-Sample T: {variable}\n\n" +
               "Descriptive Statistics\n" + stats + "\n\n" +
               $"Null hypothesis         H0: μ = {Fmt.G(mu0)}\n" +
               $"Alternative hypothesis  H1: μ {AltSymbol(r.Alt)} {Fmt.G(mu0)}\n\n" +
               "Test\n" + test;
    }

    public static string TwoSampleT(TwoSampleTResult r, string name1, string name2)
    {
        var stats = new TextTable("Sample", "N", "Mean", "StDev", "SE Mean").LeftAlign(0);
        stats.Add(name1, r.N1.ToString(), Fmt.N(r.Mean1), Fmt.N(r.StDev1), Fmt.N(r.StDev1 / Math.Sqrt(r.N1)));
        stats.Add(name2, r.N2.ToString(), Fmt.N(r.Mean2), Fmt.N(r.StDev2), Fmt.N(r.StDev2 / Math.Sqrt(r.N2)));

        var test = new TextTable("Difference", "SE", "T-Value", "DF", "P-Value");
        test.Add(Fmt.N(r.Difference), Fmt.N(r.SeDiff), Fmt.N(r.T, 2), Fmt.N(r.Df, 2), Fmt.P(r.P));

        string method = r.Pooled ? "Equal variances assumed (pooled)" : "Welch (unequal variances)";
        return $"Two-Sample T: {name1} vs {name2}\n\n" +
               "Descriptive Statistics\n" + stats + "\n\n" +
               $"Estimation for Difference ({Pct(r.Conf)} CI)\n" +
               $"  μ({name1}) - μ({name2}): {Ci(r.CiLow, r.CiHigh)}\n\n" +
               $"Method: {method}\n" +
               $"Alternative hypothesis  H1: difference {AltSymbol(r.Alt)} 0\n\n" +
               "Test\n" + test;
    }

    public static string PairedT(PairedTResult r, string name1, string name2)
    {
        var stats = new TextTable("Sample", "N", "Mean", "StDev", "SE Mean").LeftAlign(0);
        stats.Add(name1, r.N.ToString(), Fmt.N(r.Mean1), Fmt.N(r.Sd1), Fmt.N(r.Sd1 / Math.Sqrt(r.N)));
        stats.Add(name2, r.N.ToString(), Fmt.N(r.Mean2), Fmt.N(r.Sd2), Fmt.N(r.Sd2 / Math.Sqrt(r.N)));

        var diff = new TextTable("N", "Mean", "StDev", "SE Mean", $"{Pct(r.Conf)} CI for μ_diff");
        diff.Add(r.N.ToString(), Fmt.N(r.MeanDiff), Fmt.N(r.SdDiff), Fmt.N(r.SeDiff), Ci(r.CiLow, r.CiHigh));

        var test = new TextTable("T-Value", "DF", "P-Value");
        test.Add(Fmt.N(r.T, 2), r.Df.ToString("0"), Fmt.P(r.P));

        return $"Paired T: {name1} - {name2}\n\n" +
               "Descriptive Statistics\n" + stats + "\n\n" +
               "Estimation for Paired Difference\n" + diff + "\n\n" +
               $"Alternative hypothesis  H1: μ_diff {AltSymbol(r.Alt)} 0\n\n" +
               "Test\n" + test;
    }

    public static string OneProportion(OnePropResult r, string label, double p0)
    {
        var stats = new TextTable("N", "Event", "Sample p", $"{Pct(r.Conf)} CI");
        stats.Add(r.N.ToString(), r.X.ToString(), Fmt.N(r.PHat, 4), Ci(r.CiLow, r.CiHigh));

        var test = new TextTable("Z-Value", "P-Value");
        test.Add(Fmt.N(r.Z, 2), Fmt.P(r.P));

        return $"Test and CI for One Proportion: {label}\n\n" +
               "Descriptive Statistics\n" + stats + "\n\n" +
               $"Null hypothesis         H0: p = {Fmt.G(p0)}\n" +
               $"Alternative hypothesis  H1: p {AltSymbol(r.Alt)} {Fmt.G(p0)}\n" +
               "Method: Normal approximation\n\n" +
               "Test\n" + test;
    }

    public static string TwoProportions(TwoPropResult r, string l1, string l2)
    {
        var stats = new TextTable("Sample", "N", "Event", "Sample p").LeftAlign(0);
        stats.Add(l1, r.N1.ToString(), r.X1.ToString(), Fmt.N(r.P1, 4));
        stats.Add(l2, r.N2.ToString(), r.X2.ToString(), Fmt.N(r.P2, 4));

        var test = new TextTable("Difference", "Z-Value", "P-Value");
        test.Add(Fmt.N(r.Difference, 4), Fmt.N(r.Z, 2), Fmt.P(r.P));

        return $"Test and CI for Two Proportions: {l1}, {l2}\n\n" +
               "Descriptive Statistics\n" + stats + "\n\n" +
               $"Estimation for Difference ({Pct(r.Conf)} CI): {Ci(r.CiLow, r.CiHigh)}\n" +
               $"Alternative hypothesis  H1: p1 - p2 {AltSymbol(r.Alt)} 0\n" +
               "Method: Normal approximation\n\n" +
               "Test\n" + test;
    }

    public static string ChiSquareGof(ChiSquareGofResult r, IReadOnlyList<string> categories)
    {
        var t = new TextTable("Category", "Observed", "Expected", "Contribution").LeftAlign(0);
        for (int i = 0; i < r.Observed.Length; i++)
        {
            double contrib = (r.Observed[i] - r.Expected[i]) * (r.Observed[i] - r.Expected[i]) / r.Expected[i];
            t.Add(i < categories.Count ? categories[i] : (i + 1).ToString(),
                Fmt.G(r.Observed[i]), Fmt.N(r.Expected[i]), Fmt.N(contrib));
        }
        var test = new TextTable("Chi-Square", "DF", "P-Value");
        test.Add(Fmt.N(r.ChiSq), r.Df.ToString(), Fmt.P(r.P));

        return "Chi-Square Goodness-of-Fit Test\n\n" + t + "\n\n" + test;
    }

    public static string Contingency(ContingencyResult r, IReadOnlyList<string> rowLabels,
        IReadOnlyList<string> colLabels)
    {
        int rows = r.Observed.GetLength(0), cols = r.Observed.GetLength(1);
        var headers = new List<string> { "" };
        for (int j = 0; j < cols; j++) headers.Add(j < colLabels.Count ? colLabels[j] : $"C{j + 1}");
        headers.Add("Total");

        var t = new TextTable(headers.ToArray()).LeftAlign(0);
        for (int i = 0; i < rows; i++)
        {
            var cells = new List<string> { i < rowLabels.Count ? rowLabels[i] : $"R{i + 1}" };
            for (int j = 0; j < cols; j++)
                cells.Add($"{Fmt.G(r.Observed[i, j])} ({Fmt.N(r.Expected[i, j], 1)})");
            cells.Add(Fmt.G(r.RowTotals[i]));
            t.Add(cells.ToArray());
        }
        var totalRow = new List<string> { "Total" };
        for (int j = 0; j < cols; j++) totalRow.Add(Fmt.G(r.ColTotals[j]));
        totalRow.Add(Fmt.G(r.Total));
        t.Add(totalRow.ToArray());

        var test = new TextTable("Chi-Square", "DF", "P-Value");
        test.Add(Fmt.N(r.ChiSq), r.Df.ToString(), Fmt.P(r.P));

        return "Chi-Square Test for Association\n" +
               "(cell contents: observed (expected))\n\n" + t + "\n\n" +
               "Pearson Chi-Square\n" + test;
    }
}
