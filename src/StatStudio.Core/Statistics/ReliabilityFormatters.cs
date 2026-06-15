using StatStudio.Core.Inference;

namespace StatStudio.Core.Statistics;

public static class ReliabilityFormatters
{
    public static string DistributionFit(DistributionFit r, string name)
    {
        var par = new TextTable("Parameter", "Estimate").LeftAlign(0);
        foreach (var (pn, pv) in r.Parameters) par.Add(pn, Fmt.N(pv, 5));

        var summary = new TextTable("Mean", "StDev", "Median");
        summary.Add(Fmt.N(r.Mean), Fmt.N(r.StDev), Fmt.N(r.Median));

        var pct = new TextTable("Percent", "Value");
        foreach (var (p, v) in r.Percentiles) pct.Add(Fmt.N(p, 0), Fmt.N(v));

        return $"Distribution Analysis: {name}  ({r.Distribution})\n\n" +
               $"N = {r.N}\n\n" +
               "Parameter Estimates (MLE)\n" + par + "\n\n" +
               "Characteristics of Distribution\n" + summary + "\n\n" +
               "Table of Percentiles\n" + pct;
    }

    public static string KaplanMeier(KaplanMeierResult r, string name)
    {
        var t = new TextTable("Time", "At Risk", "Failures", "Censored", "Survival");
        foreach (var row in r.Rows)
            t.Add(Fmt.G(row.Time), row.AtRisk.ToString(), row.Failures.ToString(),
                row.Censored.ToString(), Fmt.N(row.Survival, 4));
        string median = double.IsNaN(r.MedianSurvival) ? "not reached" : Fmt.G(r.MedianSurvival);
        return $"Kaplan-Meier Survival: {name}\n\n" +
               $"N = {r.N},  events = {r.Events},  median survival = {median}\n\n" +
               "Survival Table\n" + t;
    }
}
