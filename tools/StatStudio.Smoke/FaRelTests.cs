using StatStudio.Core.Statistics;

namespace StatStudio.Smoke;

internal static class FaRelTests
{
    public static void Run()
    {
        Check.Section("Factor analysis — 1 factor on a correlated pair");
        var data = new[]
        {
            new double[] { 1, 2 }, new double[] { 2, 4 }, new double[] { 3, 6 },
            new double[] { 4, 8 }, new double[] { 5, 10 },
        };
        var fa = FactorAnalysis.Extract(data, new[] { "x1", "x2" }, 1);
        Check.Close(Math.Abs(fa.Loadings[0, 0]), 1.0, "|loading x1| = 1", 1e-6);
        Check.Close(Math.Abs(fa.Loadings[1, 0]), 1.0, "|loading x2| = 1", 1e-6);
        Check.Close(fa.Communalities[0], 1.0, "communality x1 = 1", 1e-6);
        Check.Close(fa.VarianceExplained[0], 2.0, "variance explained = 2", 1e-6);

        Check.Section("Factor analysis — varimax preserves communalities");
        var d3 = new double[8][];
        for (int i = 0; i < 8; i++)
            d3[i] = new double[] { i + 1, 2 * (i + 1), 9 - i + (i % 2) };
        var unrot = FactorAnalysis.Extract(d3, new[] { "a", "b", "c" }, 2, rotate: false);
        var rot = FactorAnalysis.Extract(d3, new[] { "a", "b", "c" }, 2, rotate: true);
        Check.Close(rot.Communalities.Sum(), unrot.Communalities.Sum(), "Σ communalities rotation-invariant", 1e-6);
        Check.Close(rot.VarianceExplained.Sum(), unrot.VarianceExplained.Sum(), "total variance preserved", 1e-6);

        Check.Section("Reliability — exponential / normal / lognormal MLE");
        var ex = Reliability.FitExponential(new double[] { 1, 2, 3, 4, 5 });
        Check.Close(ex.Mean, 3.0, "exponential mean = 3");
        Check.Close(ex.Median, 3 * Math.Log(2), "exponential median = mean·ln2", 1e-6);
        var no = Reliability.FitNormal(new double[] { 2, 4, 6, 8, 10 });
        Check.Close(no.Mean, 6.0, "normal mean = 6");
        Check.Close(no.StDev, Math.Sqrt(8), "normal sd (MLE) = sqrt(8)", 1e-6);
        var ln = Reliability.FitLognormal(new double[] { 1, Math.E, Math.E * Math.E });
        Check.Close(ln.Parameters.First(p => p.Name.Contains('μ')).Value, 1.0, "lognormal mu = 1", 1e-6);

        Check.Section("Reliability — Weibull MLE recovers β≈2, η≈100");
        int n = 30;
        var wt = new double[n];
        for (int i = 1; i <= n; i++) { double p = (i - 0.3) / (n + 0.4); wt[i - 1] = 100 * Math.Pow(-Math.Log(1 - p), 0.5); }
        var wb = Reliability.FitWeibull(wt);
        double beta = wb.Parameters.First(p => p.Name.Contains('β')).Value;
        double eta = wb.Parameters.First(p => p.Name.Contains('η')).Value;
        Check.Close(beta, 2.0, "Weibull shape ≈ 2", 0.15);
        Check.Close(eta, 100.0, "Weibull scale ≈ 100", 8.0);

        Check.Section("Kaplan-Meier survival");
        var km = Reliability.KaplanMeier(new double[] { 2, 3, 4, 5 }, new[] { false, false, false, false });
        Check.Close(km.Rows[0].Survival, 0.75, "S(2) = 0.75");
        Check.Close(km.Rows[1].Survival, 0.50, "S(3) = 0.50");
        Check.Close(km.MedianSurvival, 3.0, "median survival = 3");

        var kmc = Reliability.KaplanMeier(new double[] { 2, 3, 4, 5 }, new[] { false, true, false, false });
        Check.Close(kmc.Rows.First(r => r.Time == 4).Survival, 0.375, "censored: S(4) = 0.375", 1e-6);
    }
}
