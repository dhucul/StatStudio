using StatStudio.Core.Statistics;

namespace StatStudio.Smoke;

internal static class AdvancedTests
{
    public static void Run()
    {
        Check.Section("Studentized range (Tukey q)");
        Check.Close(StudentizedRange.InverseCDF(0.95, 3, 10), 3.877, "q(0.95, k=3, df=10)", 5e-3);
        Check.Close(StudentizedRange.CDF(3.877, 3, 10), 0.95, "ptukey(3.877, 3, 10)", 3e-3);
        Check.Close(StudentizedRange.InverseCDF(0.95, 3, 5000), 3.314, "q(0.95, k=3, df=inf)", 1e-2);

        Check.Section("Two-way ANOVA (balanced 2x2, n=2)");
        var resp = new double[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var fa = new[] { "a1", "a1", "a1", "a1", "a2", "a2", "a2", "a2" };
        var fb = new[] { "b1", "b1", "b2", "b2", "b1", "b1", "b2", "b2" };
        var tw = AnovaExtensions.TwoWay(resp, fa, fb, "A", "B");
        Check.Close(tw.SsA, 32, "SS A");
        Check.Close(tw.SsB, 8, "SS B");
        Check.Close(tw.SsAB, 0, "SS interaction");
        Check.Close(tw.SsError, 2, "SS error");
        Check.Equal(tw.DfError, 4, "df error");
        Check.Close(tw.FA, 64, "F for A");
        Check.Close(tw.FB, 16, "F for B");

        Check.Section("Tukey HSD (3 separated groups)");
        var tukey = AnovaExtensions.Tukey(new (string, double[])[]
        {
            ("G1", new double[] { 1, 2, 3 }),
            ("G2", new double[] { 4, 5, 6 }),
            ("G3", new double[] { 7, 8, 9 }),
        });
        Check.Equal(tukey.Comparisons.Count, 3, "3 pairwise comparisons");
        Check.True(tukey.Comparisons.All(c => c.P < 0.05), "all pairs significant");

        Check.Section("F-test for two variances");
        var f = VarianceTests.FTest(new double[] { 0, 2, 4, 6, 8 }, new double[] { 3, 4, 5 });
        Check.Close(f.F, 10.0, "F = var1/var2");
        Check.Equal(f.Df1, 4, "df1");
        Check.Equal(f.Df2, 2, "df2");

        Check.Section("Equal-variance tests (Bartlett)");
        var eqEqual = VarianceTests.EqualVariances(new (string, double[])[]
        {
            ("g1", new double[] { 1, 2, 3, 4, 5 }),
            ("g2", new double[] { 2, 3, 4, 5, 6 }),
            ("g3", new double[] { 3, 4, 5, 6, 7 }),
        });
        Check.True(eqEqual.BartlettP > 0.99, "equal variances -> Bartlett p ~ 1");
        var eqDiff = VarianceTests.EqualVariances(new (string, double[])[]
        {
            ("g1", new double[] { 1, 2, 3, 4, 5 }),
            ("g2", new double[] { 10, 20, 30, 40, 50 }),
            ("g3", new double[] { 1, 1, 2, 2, 3 }),
        });
        Check.True(eqDiff.BartlettP < 0.05, "unequal variances -> Bartlett p < 0.05");

        Check.Section("Binary logistic regression (saturated 2x2, OR=16)");
        var lx = Enumerable.Repeat(0.0, 10).Concat(Enumerable.Repeat(1.0, 10)).ToArray();
        var ly = new double[] { 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0 };
        var logit = Logistic.Fit(ly, new[] { lx }, new[] { "x" }, "y");
        Check.True(logit.Converged, "IRLS converged");
        Check.Close(logit.Terms[0].Coef, -1.386294, "intercept = ln(0.25)", 1e-3);
        Check.Close(logit.Terms[1].Coef, 2.772589, "slope = ln(16)", 1e-3);
        Check.Close(logit.Terms[1].OddsRatio, 16.0, "odds ratio", 1e-2);

        Check.Section("Polynomial regression (y = x^2)");
        var poly = RegressionExtensions.Polynomial(new double[] { 1, 2, 3, 4, 5 }, new double[] { 1, 4, 9, 16, 25 }, 2);
        Check.Close(poly.RSquared, 1.0, "R-squared = 1", 1e-6);
        Check.Close(poly.Terms[2].Coef, 1.0, "x^2 coefficient", 1e-6);

        Check.Section("Best subsets (x1 relevant, x2 noise)");
        var bs = RegressionExtensions.BestSubsets(
            new double[] { 3, 5, 7, 9, 11 },
            new[] { new double[] { 1, 2, 3, 4, 5 }, new double[] { 5, 3, 8, 1, 9 } },
            new[] { "x1", "x2" });
        Check.True(bs.Models[0].Predictors.Contains("x1"), "best model includes x1");
        Check.Close(bs.Models[0].RSquared, 1.0, "best model R-squared = 1", 1e-6);
    }
}
