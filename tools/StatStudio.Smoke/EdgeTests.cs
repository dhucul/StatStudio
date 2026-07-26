using StatStudio.Core.Statistics;
using StatStudio.Core.Statistics.Spc;

namespace StatStudio.Smoke;

internal static class EdgeTests
{
    public static void Run()
    {
        Check.Section("Validated public boundaries");

        Check.Throws<ArgumentException>(() => Regression.Fit(
            new double[] { 1, 2, 3 },
            new[] { new double[] { 1, 2 } },
            new[] { "X" }), "ragged regression design rejected");
        Check.Throws<ArgumentException>(() => Regression.Fit(
            new double[] { 1, 2, 3, 4 },
            new[]
            {
                new double[] { 1, 2, 3, 4 },
                new double[] { 2, 4, 6, 8 },
            },
            new[] { "X1", "X2" }), "singular regression design rejected");
        Check.Throws<ArgumentException>(() => Logistic.Fit(
            new double[] { 1, 1, 1, 1 },
            new[] { new double[] { 1, 2, 3, 4 } },
            new[] { "X" }), "single-class logistic response rejected");

        Check.Throws<ArgumentException>(() => Pca.Compute(
            new[]
            {
                new double[] { 1, 4 },
                new double[] { 1, 5 },
                new double[] { 1, 6 },
            },
            new[] { "Constant", "Variable" }, correlation: true), "constant PCA variable rejected");
        Check.Throws<ArgumentException>(
            () => Correlation.Pearson(new double[] { 1, 2 }, new double[] { 1 }),
            "mismatched correlation inputs rejected");
        Check.Throws<ArgumentException>(() => KMeans.Cluster(
            new[]
            {
                new double[] { 0 },
                new double[] { 0 },
                new double[] { 1 },
            },
            3, new[] { "X" }), "k-means requires k distinct observations");

        Check.Throws<ArgumentException>(
            () => VarianceTests.FTest(new double[] { 1, 1, 1 }, new double[] { 1, 2, 3 }),
            "zero variance F-test sample rejected");
        Check.Throws<ArgumentException>(() => VarianceTests.EqualVariances(
            new (string, double[])[]
            {
                ("A", new double[] { 1, 1, 1 }),
                ("B", new double[] { 1, 2, 3 }),
            }), "zero variance Bartlett group rejected");
        Check.Throws<ArgumentException>(
            () => Capability.FromIndividuals(new double[] { 1, 2, 3 }, 5, 1),
            "reversed capability limits rejected");

        Check.Throws<ArgumentException>(() => FactorialAnalysis.Analyze(
            new double[] { 1, 2, 3, 4 },
            new[] { new double[] { -1, 1, 2, -1 } },
            new[] { "A" }), "non-two-level factorial rejected");
        Check.Throws<ArgumentException>(() => MixtureAnalysis.Fit(
            new double[] { 1, 2, 3 },
            new[]
            {
                new double[] { 0.5, 0.2, 0.8 },
                new double[] { 0.4, 0.8, 0.2 },
            },
            new[] { "A", "B" }, quadratic: false), "mixture rows must sum to one");
        Check.Throws<ArgumentOutOfRangeException>(
            () => DoeDesign.FullFactorial(2, replicates: 0), "zero DOE replicates rejected");
        Check.Throws<ArgumentOutOfRangeException>(
            () => ResponseSurface.CentralComposite(2, centerPoints: -1), "negative RSM center points rejected");
        Check.Throws<ArgumentException>(() => RegressionExtensions.Stepwise(
            new double[] { 1, 2, 3 }, Array.Empty<double[]>(), Array.Empty<string>()),
            "empty stepwise predictor set rejected");

        Check.Throws<ArgumentOutOfRangeException>(
            () => HypothesisTests.OneProportion(2, 1), "invalid event count rejected");
        Check.Throws<ArgumentOutOfRangeException>(
            () => HypothesisTests.OneSampleT(new double[] { 1, 2 }, conf: 2),
            "invalid confidence rejected");
        Check.Throws<ArgumentException>(
            () => HypothesisTests.ChiSquareGof(new double[] { 2, -1 }),
            "negative goodness-of-fit count rejected");
        Check.Throws<ArgumentException>(
            () => HypothesisTests.ChiSquareAssociation(new double[,] { { 0, 0 }, { 0, 0 } }),
            "empty contingency table rejected");

        Check.Throws<ArgumentOutOfRangeException>(
            () => Bayes.Proportion(2, 1), "invalid Bayesian event count rejected");
        Check.Throws<ArgumentOutOfRangeException>(
            () => Bayes.Proportion(0, 0), "zero Bayesian trial count rejected");
        Check.Throws<ArgumentOutOfRangeException>(
            () => Bayes.NormalMeanKnownVar(new double[] { 1, 2 }, 0, 1, 0),
            "nonpositive known sigma rejected");
        Check.Throws<ArgumentException>(
            () => FishersExact.Test(0, 0, 0, 0), "empty Fisher table rejected");
        Check.Throws<ArgumentOutOfRangeException>(
            () => Power.OneSampleTSampleSize(1, 0.5, 0.05, Alternative.TwoSided),
            "invalid target power rejected");
        Check.Throws<ArgumentException>(
            () => Normality.AndersonDarling(new double[] { 1, 1, 1 }),
            "constant normality sample rejected");

        Check.Throws<ArgumentOutOfRangeException>(
            () => Reliability.FitWeibull(new double[] { 1, 0, 2 }),
            "nonpositive Weibull lifetime rejected");
        Check.Throws<ArgumentOutOfRangeException>(
            () => Reliability.FitNormal(new double[] { 1, -1, 2 }),
            "negative normal lifetime rejected");
        Check.Throws<ArgumentException>(
            () => Reliability.KaplanMeier(new double[] { 1, 2 }, new[] { false }),
            "Kaplan-Meier dimension mismatch rejected");
        Check.Throws<ArgumentOutOfRangeException>(
            () => Reliability.KaplanMeier(new double[] { -1, 2 }, new[] { false, true }),
            "negative survival time rejected");
        Check.Throws<ArgumentException>(() => AnovaExtensions.TwoWay(
            new double[] { 1, 2 },
            new[] { "A" },
            new[] { "X", "Y" }), "two-way ANOVA dimension mismatch rejected");
    }
}
