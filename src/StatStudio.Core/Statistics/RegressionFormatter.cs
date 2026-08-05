using StatStudio.Core.Inference;

namespace StatStudio.Core.Statistics;

public static class RegressionFormatter
{
    public static string Format(RegressionResult r)
    {
        var pred = string.Join(", ", r.Predictors);

        var eq = $"Regression Equation\n{r.Equation}\n\n";

        var coef = new TextTable("Term", "Coef", "SE Coef", "T-Value", "P-Value").LeftAlign(0);
        foreach (var t in r.Terms)
            coef.Add(t.Name, Fmt.N(t.Coef, 4), Fmt.N(t.SeCoef, 4), Fmt.N(t.T, 2), Fmt.P(t.P));

        var model = new TextTable("S", "R-sq", "R-sq(adj)");
        model.Add(Fmt.N(r.S, 4), Fmt.PctFixed(r.RSquared), Fmt.PctFixed(r.RSquaredAdj));

        var anova = new TextTable("Source", "DF", "Adj SS", "Adj MS", "F-Value", "P-Value").LeftAlign(0);
        anova.Add("Regression", r.DfRegression.ToString(), Fmt.N(r.SsRegression), Fmt.N(r.MsRegression), Fmt.N(r.F, 2), Fmt.P(r.P));
        anova.Add("Error", r.DfError.ToString(), Fmt.N(r.SsError), Fmt.N(r.MsError), "", "");
        anova.Add("Total", r.DfTotal.ToString(), Fmt.N(r.SsTotal), "", "", "");

        return $"Regression Analysis: {r.Response} versus {pred}\n\n" +
               eq +
               "Coefficients\n" + coef + "\n\n" +
               "Model Summary\n" + model + "\n\n" +
               "Analysis of Variance\n" + anova;
    }
}
