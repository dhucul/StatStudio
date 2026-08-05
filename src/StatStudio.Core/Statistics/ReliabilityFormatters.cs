using StatStudio.Core.Inference;

namespace StatStudio.Core.Statistics;

public static class ReliabilityFormatters
{
    public static string DistributionFit(DistributionFit r, string name)
    {
        string ci = $"{Fmt.Pct(r.Conf)} CI";

        var par = new TextTable("Parameter", "Estimate", "Std Error", ci).LeftAlign(0);
        foreach (var p in r.Parameters)
            par.Add(p.Name, Fmt.N(p.Value, 5), Fmt.N(p.StandardError, 5), Interval(p.CiLow, p.CiHigh));

        var summary = new TextTable("Characteristic", "Estimate", "Std Error", ci).LeftAlign(0);
        foreach (var ch in r.Characteristics)
            summary.Add(ch.Name, Fmt.N(ch.Value), Fmt.N(ch.StandardError), Interval(ch.CiLow, ch.CiHigh));

        var pct = new TextTable("Percent", "Value", "Std Error", ci);
        foreach (var p in r.Percentiles)
            pct.Add(Fmt.N(p.Percent, 0), Fmt.N(p.Value), Fmt.N(p.StandardError), Interval(p.CiLow, p.CiHigh));

        // State the censoring split explicitly: the estimates mean something different when some
        // observations are survivors rather than failures.
        string counts = r.RightCensored > 0
            ? $"N = {r.N}   failures = {r.Failures}   right-censored = {r.RightCensored}\n" +
              "Censored observations contribute survival time only (maximum-likelihood fit)."
            : $"N = {r.N}   (no censoring — every observation is a failure)";

        return $"Distribution Analysis: {name}  ({r.Distribution})\n\n" +
               counts + "\n\n" +
               "Parameter Estimates (MLE)\n" + par + "\n" +
               "  Standard errors from the observed Fisher information; intervals are Wald\n" +
               "  (on the log scale for parameters constrained positive).\n\n" +
               "Characteristics of Distribution (delta method)\n" + summary + "\n\n" +
               "Table of Percentiles (delta method)\n" + pct;
    }

    private static string Interval(double low, double high) =>
        double.IsNaN(low) || double.IsNaN(high) ? "*" : $"({Fmt.N(low)}, {Fmt.N(high)})";

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
