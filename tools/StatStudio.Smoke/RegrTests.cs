using StatStudio.Core.Statistics;

namespace StatStudio.Smoke;

internal static class RegrTests
{
    public static void Run()
    {
        Check.Section("Simple regression  x={1..5}, y={2,4,5,4,5}");
        var r = Regression.SimpleLinear(
            new double[] { 1, 2, 3, 4, 5 },
            new double[] { 2, 4, 5, 4, 5 }, "x", "y");
        var intercept = r.Terms[0];
        var slope = r.Terms[1];
        Check.Close(intercept.Coef, 2.2, "intercept");
        Check.Close(slope.Coef, 0.6, "slope");
        Check.Close(r.RSquared, 0.6, "R-squared");
        Check.Close(r.S, 0.894427, "S (residual SE)");
        Check.Close(slope.SeCoef, 0.282843, "slope SE");
        Check.Close(slope.T, 2.121320, "slope t");
        Check.Close(r.F, 4.5, "F");
        Check.Equal(r.DfError, 3, "df error");

        Check.Section("Perfect line  y = 2x + 1");
        var perfect = Regression.SimpleLinear(
            new double[] { 1, 2, 3, 4, 5 },
            new double[] { 3, 5, 7, 9, 11 });
        Check.Close(perfect.Terms[0].Coef, 1.0, "intercept");
        Check.Close(perfect.Terms[1].Coef, 2.0, "slope");
        Check.Close(perfect.RSquared, 1.0, "R-squared = 1");

        Check.Section("Multiple regression  y = 1 + 2*x1 + 3*x2 (exact)");
        var m = Regression.Fit(
            new double[] { 9, 8, 19, 18, 26 },
            new[]
            {
                new double[] { 1, 2, 3, 4, 5 },
                new double[] { 2, 1, 4, 3, 5 },
            },
            new[] { "x1", "x2" }, "y");
        Check.Close(m.Terms[0].Coef, 1.0, "constant", 1e-3);
        Check.Close(m.Terms[1].Coef, 2.0, "x1 coef", 1e-3);
        Check.Close(m.Terms[2].Coef, 3.0, "x2 coef", 1e-3);
        Check.Close(m.RSquared, 1.0, "R-squared = 1", 1e-6);

        Check.Section("Regression rejects a singular design");
        bool threw = false;
        try { Regression.Fit(new double[] { 1, 2, 3, 4 }, new[] { new double[] { 5, 5, 5, 5 } }, new[] { "const" }, "y"); }
        catch (ArgumentException) { threw = true; }
        Check.True(threw, "constant predictor throws instead of returning NaN");
    }
}
