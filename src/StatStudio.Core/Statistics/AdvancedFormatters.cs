using StatStudio.Core.Inference;

namespace StatStudio.Core.Statistics;

public static class AdvancedFormatters
{
    public static string TwoWayAnova(TwoWayAnovaResult r, string response)
    {
        var t = new TextTable("Source", "DF", "Adj SS", "Adj MS", "F-Value", "P-Value").LeftAlign(0);
        t.Add(r.FactorA, r.DfA.ToString(), Fmt.N(r.SsA), Fmt.N(r.MsA), Fmt.N(r.FA, 2), Fmt.P(r.PA));
        t.Add(r.FactorB, r.DfB.ToString(), Fmt.N(r.SsB), Fmt.N(r.MsB), Fmt.N(r.FB, 2), Fmt.P(r.PB));
        t.Add($"{r.FactorA}*{r.FactorB}", r.DfAB.ToString(), Fmt.N(r.SsAB), Fmt.N(r.MsAB), Fmt.N(r.FAB, 2), Fmt.P(r.PAB));
        t.Add("Error", r.DfError.ToString(), Fmt.N(r.SsError), Fmt.N(r.MsError), "", "");
        t.Add("Total", r.DfTotal.ToString(), Fmt.N(r.SsTotal), "", "", "");
        var model = new TextTable("S", "R-sq").Row(Fmt.N(r.S), $"{r.RSquared * 100:0.00}%");
        return $"Two-way ANOVA: {response} versus {r.FactorA}, {r.FactorB}\n\n" +
               "Analysis of Variance\n" + t + "\n\n" + "Model Summary\n" + model;
    }

    public static string Tukey(TukeyResult r)
    {
        var t = new TextTable("Comparison", "Difference", "SE", "T-Value", "Adj P", $"{r.Conf * 100:0.#}% CI").LeftAlign(0);
        foreach (var c in r.Comparisons)
            t.Add($"{c.GroupA} - {c.GroupB}", Fmt.N(c.Difference), Fmt.N(c.Se),
                Fmt.N(c.Q / Math.Sqrt(2), 2), Fmt.P(c.P), $"({Fmt.N(c.CiLow)}, {Fmt.N(c.CiHigh)})");
        return $"Tukey Pairwise Comparisons ({r.Conf * 100:0.#}% simultaneous confidence)\n" +
               $"Critical q = {Fmt.N(r.QCritical, 3)}\n\n" + t;
    }

    public static string FTest(FTestResult r, string n1, string n2)
    {
        var t = new TextTable("Sample", "N", "StDev", "Variance").LeftAlign(0);
        t.Add(n1, r.N1.ToString(), Fmt.N(r.StDev1), Fmt.N(r.StDev1 * r.StDev1));
        t.Add(n2, r.N2.ToString(), Fmt.N(r.StDev2), Fmt.N(r.StDev2 * r.StDev2));
        var test = new TextTable("F-Value", "DF1", "DF2", "P-Value");
        test.Add(Fmt.N(r.F, 3), r.Df1.ToString(), r.Df2.ToString(), Fmt.P(r.P));
        return $"Test for Two Variances: {n1} vs {n2}\n\n" + t + "\n\n" +
               $"{r.Conf * 100:0.#}% CI for variance ratio: ({Fmt.N(r.RatioCiLow)}, {Fmt.N(r.RatioCiHigh)})\n\n" +
               "F-Test (normal)\n" + test;
    }

    public static string EqualVariances(EqualVarianceResult r, string response, string factor)
    {
        var g = new TextTable("Level", "N", "StDev").LeftAlign(0);
        foreach (var s in r.Groups) g.Add(s.Name, s.N.ToString(), Fmt.N(s.StDev));
        var tests = new TextTable("Method", "Test Statistic", "DF", "P-Value").LeftAlign(0);
        tests.Add("Bartlett", Fmt.N(r.Bartlett, 2), r.BartlettDf.ToString(), Fmt.P(r.BartlettP));
        tests.Add("Levene", Fmt.N(r.Levene, 2), $"{r.LeveneDf1}, {r.LeveneDf2}", Fmt.P(r.LeveneP));
        return $"Test for Equal Variances: {response} versus {factor}\n\n" + g + "\n\n" + tests;
    }

    public static string Logistic(LogisticResult r)
    {
        var coef = new TextTable("Term", "Coef", "SE Coef", "Z-Value", "P-Value", "Odds Ratio").LeftAlign(0);
        foreach (var t in r.Terms)
            coef.Add(t.Name, Fmt.N(t.Coef, 4), Fmt.N(t.Se, 4), Fmt.N(t.Z, 2), Fmt.P(t.P),
                double.IsNaN(t.OddsRatio) ? "" : Fmt.N(t.OddsRatio, 4));
        double r2 = r.NullDeviance > 0 ? 1 - r.Deviance / r.NullDeviance : double.NaN;
        var summary = new TextTable("Deviance", "Null Deviance", "Pseudo R-sq (McFadden)", "N");
        summary.Add(Fmt.N(r.Deviance, 2), Fmt.N(r.NullDeviance, 2), $"{r2 * 100:0.00}%", r.N.ToString());
        return $"Binary Logistic Regression: {r.Response} versus {string.Join(", ", r.Predictors)}\n\n" +
               "Coefficients\n" + coef + "\n\n" + "Model Summary\n" + summary +
               (r.Converged ? "" : "\n\n[warning] IRLS did not fully converge.");
    }

    public static string BestSubsets(BestSubsetsResult r, int top = 12)
    {
        var t = new TextTable("Vars", "R-sq", "R-sq(adj)", "Mallows Cp", "S", "Predictors").LeftAlign(5);
        foreach (var m in r.Models.Take(top))
            t.Add(m.NumPredictors.ToString(), $"{m.RSquared * 100:0.00}", $"{m.RSquaredAdj * 100:0.00}",
                Fmt.N(m.MallowsCp, 1), Fmt.N(m.S, 4), string.Join(", ", m.Predictors));
        return "Best Subsets Regression (ranked by adjusted R-sq)\n\n" + t;
    }

    public static string Stepwise(StepwiseResult r)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("Stepwise Regression\n\n");
        foreach (var s in r.Steps) sb.Append("  ").Append(s).Append('\n');
        sb.Append('\n').Append(RegressionFormatter.Format(r.Final));
        return sb.ToString();
    }

    private static TextTable Row(this TextTable t, params string[] cells) { t.Add(cells); return t; }
}
