using StatStudio.Core.Statistics;

namespace StatStudio.Smoke;

internal static class BayesMixedTests
{
    public static void Run()
    {
        Check.Section("Bayesian proportion (Beta-Binomial)");
        var bp = Bayes.Proportion(8, 10, priorA: 1, priorB: 1);
        Check.Close(bp.PostA, 9, "posterior a = 1 + 8");
        Check.Close(bp.PostB, 3, "posterior b = 1 + 2");
        Check.Close(bp.Mean, 0.75, "posterior mean = 9/12");
        Check.Close(bp.Mode, 0.8, "posterior mode = 8/10");
        Check.True(bp.CredLow > 0 && bp.CredHigh < 1 && bp.CredLow < bp.Mean && bp.Mean < bp.CredHigh, "credible interval brackets the mean");

        Check.Section("Bayesian normal mean — known variance");
        var nk = Bayes.NormalMeanKnownVar(new double[] { 8, 10, 10, 12 }, priorMean: 0, priorSd: 1, knownSigma: 2);
        Check.Close(nk.PosteriorMean, 5.0, "posterior mean (prior+data precision)");
        Check.Close(nk.PosteriorSd, Math.Sqrt(0.5), "posterior sd = sqrt(0.5)", 1e-6);

        Check.Section("Bayesian normal mean — unknown variance (Jeffreys)");
        var nu = Bayes.NormalMeanUnknownVar(new double[] { 2, 4, 4, 4, 5, 5, 7, 9 });
        Check.Close(nu.PosteriorMean, 5.0, "posterior mean = x-bar");
        Check.Close(nu.CredLow, 3.21221, "95% credible low", 1e-3);
        Check.Close(nu.CredHigh, 6.78779, "95% credible high", 1e-3);

        Check.Section("Bayesian linear regression (reference prior)");
        var br = Bayes.LinearRegression(
            new double[] { 2, 4, 5, 4, 5 },
            new[] { new double[] { 1, 2, 3, 4, 5 } },
            new[] { "x" });
        var slope = br.Terms.First(t => t.Name == "x");
        Check.Close(slope.PosteriorMean, 0.6, "slope posterior mean = OLS");
        Check.True(slope.ProbPositive > 0.9, "P(slope > 0) > 0.9");

        Check.Section("Mixed model — one-way random effects");
        var mm = MixedModel.OneWayRandom(new (string, double[])[]
        {
            ("G1", new double[] { 1, 2, 3 }),
            ("G2", new double[] { 4, 5, 6 }),
            ("G3", new double[] { 7, 8, 9 }),
        });
        Check.Close(mm.VarWithin, 1.0, "within-group variance");
        Check.Close(mm.VarBetween, 26.0 / 3.0, "between-group variance");
        Check.Close(mm.Icc, (26.0 / 3.0) / (26.0 / 3.0 + 1), "intraclass correlation", 1e-6);
        Check.Close(mm.N0, 3.0, "n0 (balanced) = group size");
        Check.Close(mm.GrandMean, 5.0, "grand mean");
    }
}
