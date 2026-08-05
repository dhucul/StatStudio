using StatStudio.Core.Statistics;
using StatStudio.Core.Statistics.Spc;

namespace StatStudio.Smoke;

internal static class EdgeTests
{
    /// <summary>Inputs that previously crashed or returned silently wrong numbers.</summary>
    private static void RegressionsForFixedDefects()
    {
        Check.Section("Regressions — previously crashing or wrong");

        // Welch's df was 0/0 for two constant samples, and the NaN reached StudentT's constructor.
        var constantA = new double[] { 5, 5, 5, 5 };
        var constantB = new double[] { 7, 7, 7, 7 };
        var welch = HypothesisTests.TwoSampleT(constantA, constantB, pooled: false);
        Check.True(double.IsNaN(welch.T), "Welch t on two constant samples is NaN, not a throw");
        Check.Close(welch.Difference, -2, "Welch difference still reported");

        // Math.Pow overflowed for lifetimes in the thousands, collapsing beta to the 1e-3 bracket floor.
        int n = 40;
        var largeScale = new double[n];
        for (int i = 1; i <= n; i++)
            largeScale[i - 1] = 20_000 * Math.Pow(-Math.Log(1 - (i - 0.3) / (n + 0.4)), 1.0 / 2.5);
        var weibull = Reliability.FitWeibull(largeScale);
        Check.Close(weibull.Parameters[0].Value, 2.5, "Weibull shape recovered at scale 20 000", 0.15);
        Check.Close(weibull.Parameters[1].Value, 20_000, "Weibull scale recovered at scale 20 000", 0.15);

        // Terms were ordered by the length of the concatenated factor names, so a 2-way
        // interaction sorted ahead of a main effect whose name happened to be longer.
        var fa = new double[] { -1, -1, 1, 1 };
        var fb = new double[] { -1, 1, -1, 1 };
        var response = new double[4];
        for (int i = 0; i < 4; i++) response[i] = 10 + 3 * fa[i] + 2 * fb[i] + fa[i] * fb[i];
        var longNames = FactorialAnalysis.Analyze(response, new[] { fa, fb }, new[] { "A", "Concentration" }, "Y");
        Check.Equal(string.Join(", ", longNames.Terms.Select(t => t.Name)),
            "Constant, A, Concentration, A*Concentration", "main effects precede interactions regardless of name length");

        // A regular fractional design used to be rejected outright; aliased effects collapse to one term.
        var frac = DoeDesign.FractionalFactorial(3, 4, randomize: false);
        var fx = Enumerable.Range(0, 3)
            .Select(j => frac.RunList.Select(r => r.Factors[j]).ToArray()).ToArray();
        var fy = fx[0].Select((_, i) => 10 + 2 * fx[0][i] + 3 * fx[1][i]).ToArray();
        var fracFit = FactorialAnalysis.Analyze(fy, fx, new[] { "A", "B", "C" }, "Y");
        Check.Close(fracFit.Terms.First(t => t.Name == "A").Effect, 4, "fractional design analysed: effect A");
        Check.Close(fracFit.Terms.First(t => t.Name == "B").Effect, 6, "fractional design analysed: effect B");
        Check.True(fracFit.Aliases.Count > 0, "fractional design reports its alias structure");

        // A constant column left every value on the median, so n was 0 and the variance divided by zero.
        var runs = Nonparametric.RunsTest(new double[] { 4, 4, 4, 4, 4 });
        Check.True(double.IsNaN(runs.Z), "runs test on a constant column yields NaN, not a divide-by-zero");

        // The PACF allocated a (maxLag+1)^2 matrix; this size used to be ~200 MB.
        var series = Enumerable.Range(0, 5_000).Select(i => Math.Sin(i * 0.05) + i * 0.001).ToArray();
        var acf = TimeSeries.Autocorrelation(series, 400);
        Check.Equal(acf.Acf.Length, 401, "ACF returns every requested lag");
        Check.True(acf.Pacf.Skip(1).All(double.IsFinite), "PACF finite across 400 lags on a 5 000-point series");

        // Spread is undefined for a single observation; reporting 0 asserted perfect precision.
        var single = Descriptives.Compute("One", new double[] { 42 });
        Check.True(double.IsNaN(single.StDev), "single-observation StDev is undefined, not zero");

        CultureIndependentOutput();
    }

    /// <summary>
    /// Session output must read identically on every machine. Interpolated formats such as
    /// $"{x*100:0.00}%" use the *current* culture, so percentages used to render as "87,50%"
    /// on a European locale while every Fmt-formatted number beside them stayed invariant.
    /// </summary>
    private static void CultureIndependentOutput()
    {
        var original = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");

            var x = new double[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            var y = new double[] { 2.1, 3.9, 6.2, 7.8, 10.1, 12.2, 13.8, 16.1 };
            string regression = RegressionFormatter.Format(Regression.SimpleLinear(x, y, "X", "Y"));
            Check.True(!regression.Contains(',') || !System.Text.RegularExpressions.Regex.IsMatch(regression, @"\d,\d"),
                "regression output has no comma decimal separators under de-DE");

            var groups = new (string, double[])[] { ("A", new double[] { 1, 2, 3 }), ("B", new double[] { 4, 5, 6 }) };
            string anova = AnovaFormatter.OneWay(Anova.OneWay(groups), "Factor", "Y");
            Check.True(!System.Text.RegularExpressions.Regex.IsMatch(anova, @"\d,\d"),
                "ANOVA output has no comma decimal separators under de-DE");

            string tTest = HypothesisFormatters.OneSampleT(
                HypothesisTests.OneSampleT(x, 4, 0.975), "X", 4);
            Check.True(tTest.Contains("97.5%"), "confidence percentage renders invariantly (97.5%, not 97,5%)");
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = original;
        }
    }

    public static void Run()
    {
        RegressionsForFixedDefects();

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
