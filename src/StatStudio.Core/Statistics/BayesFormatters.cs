using StatStudio.Core.Inference;

namespace StatStudio.Core.Statistics;

public static class BayesFormatters
{
    private static string Pct(double c) => $"{c * 100:0.#}%";

    public static string Proportion(BayesProportionResult r, string label)
    {
        var t = new TextTable("Posterior", "Mean", "Mode", "StDev", $"{Pct(r.Conf)} Credible Interval").LeftAlign(0);
        t.Add($"Beta({Fmt.N(r.PostA, 2)}, {Fmt.N(r.PostB, 2)})", Fmt.N(r.Mean, 4),
            double.IsNaN(r.Mode) ? "*" : Fmt.N(r.Mode, 4), Fmt.N(r.Sd, 4),
            $"({Fmt.N(r.CredLow, 4)}, {Fmt.N(r.CredHigh, 4)})");
        return $"Bayesian Analysis for One Proportion: {label}\n\n" +
               $"Prior: Beta({Fmt.G(r.PriorA)}, {Fmt.G(r.PriorB)})   Data: {r.X} of {r.N}\n\n" +
               t + "\n\n" +
               $"P(p > {Fmt.G(r.Threshold)}) = {Fmt.N(r.ProbAboveThreshold, 4)}";
    }

    public static string NormalMean(BayesNormalMeanResult r, string label)
    {
        var t = new TextTable("Posterior Mean", "Posterior SD", "Df", $"{Pct(r.Conf)} Credible Interval");
        t.Add(Fmt.N(r.PosteriorMean, 4), Fmt.N(r.PosteriorSd, 4),
            double.IsInfinity(r.Df) ? "∞" : Fmt.N(r.Df, 0),
            $"({Fmt.N(r.CredLow, 4)}, {Fmt.N(r.CredHigh, 4)})");
        return $"Bayesian Analysis for a Mean: {label}\n\n" +
               $"Method: {r.Method}\n\n" + t + "\n\n" +
               $"P(μ > {Fmt.G(r.Threshold)}) = {Fmt.N(r.ProbAboveThreshold, 4)}";
    }

    public static string Regression(BayesRegressionResult r)
    {
        var t = new TextTable("Term", "Post. Mean", "Post. SD", $"{Pct(r.Conf)} Credible Interval", "P(coef > 0)").LeftAlign(0);
        foreach (var term in r.Terms)
            t.Add(term.Name, Fmt.N(term.PosteriorMean, 4), Fmt.N(term.PosteriorSd, 4),
                $"({Fmt.N(term.CredLow, 4)}, {Fmt.N(term.CredHigh, 4)})", Fmt.N(term.ProbPositive, 3));
        return $"Bayesian Linear Regression: {r.Response} versus {string.Join(", ", r.Predictors)}\n" +
               "(reference prior p(β,σ²) ∝ 1/σ²; t posterior centered at OLS)\n\n" +
               t + $"\n\nResidual σ = {Fmt.N(r.Sigma, 4)}  (df = {Fmt.N(r.Df, 0)})";
    }
}
