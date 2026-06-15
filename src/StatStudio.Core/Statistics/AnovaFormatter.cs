using StatStudio.Core.Inference;

namespace StatStudio.Core.Statistics;

public static class AnovaFormatter
{
    public static string OneWay(OneWayAnovaResult r, string factor, string response)
    {
        var anova = new TextTable("Source", "DF", "Adj SS", "Adj MS", "F-Value", "P-Value").LeftAlign(0);
        anova.Add(factor, r.DfFactor.ToString(), Fmt.N(r.SsFactor), Fmt.N(r.MsFactor), Fmt.N(r.F, 2), Fmt.P(r.P));
        anova.Add("Error", r.DfError.ToString(), Fmt.N(r.SsError), Fmt.N(r.MsError), "", "");
        anova.Add("Total", r.DfTotal.ToString(), Fmt.N(r.SsTotal), "", "", "");

        var model = new TextTable("S", "R-sq", "R-sq(adj)");
        model.Add(Fmt.N(r.PooledStDev), $"{r.RSquared * 100:0.00}%", $"{r.RSquaredAdj * 100:0.00}%");

        var means = new TextTable("Level", "N", "Mean", "StDev").LeftAlign(0);
        foreach (var g in r.Groups)
            means.Add(g.Name, g.N.ToString(), Fmt.N(g.Mean), Fmt.N(g.StDev));

        return $"One-way ANOVA: {response} versus {factor}\n\n" +
               "Analysis of Variance\n" + anova + "\n\n" +
               "Model Summary\n" + model + "\n\n" +
               "Means\n" + means;
    }
}
