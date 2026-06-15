using StatStudio.Core.Statistics;

namespace StatStudio.Smoke;

internal static class HypoTests
{
    public static void Run()
    {
        var A = new double[] { 1, 2, 3, 4, 5 };
        var B = new double[] { 2, 4, 6, 8, 10 };

        Check.Section("One-sample t  {2,4,4,4,5,5,7,9}, mu0=4");
        var t1 = HypothesisTests.OneSampleT(new double[] { 2, 4, 4, 4, 5, 5, 7, 9 }, 4.0);
        Check.Close(t1.T, 1.322876, "t");
        Check.Close(t1.Df, 7, "df");
        Check.Close(t1.P, 0.227418, "p-value", 2e-3);
        Check.Close(t1.CiLow, 3.21221, "95% CI low", 1e-3);
        Check.Close(t1.CiHigh, 6.78779, "95% CI high", 1e-3);

        Check.Section("Two-sample t (pooled)  A vs B");
        var tp = HypothesisTests.TwoSampleT(A, B, pooled: true);
        Check.Close(tp.T, -1.897367, "t");
        Check.Close(tp.Df, 8, "df");
        Check.Close(tp.P, 0.094356, "p-value", 2e-3);

        Check.Section("Two-sample t (Welch)  A vs B");
        var tw = HypothesisTests.TwoSampleT(A, B, pooled: false);
        Check.Close(tw.T, -1.897367, "t");
        Check.Close(tw.Df, 5.882353, "df");
        Check.Close(tw.P, 0.107348, "p-value", 3e-3);

        Check.Section("Paired t  A vs B");
        var tpr = HypothesisTests.PairedT(A, B);
        Check.Close(tpr.T, -4.242641, "t");
        Check.Close(tpr.Df, 4, "df");
        Check.Close(tpr.MeanDiff, -3.0, "mean difference");
        Check.Close(tpr.P, 0.013222, "p-value", 2e-3);

        Check.Section("Chi-square goodness-of-fit  {10,20,30,40}");
        var g = HypothesisTests.ChiSquareGof(new double[] { 10, 20, 30, 40 });
        Check.Close(g.ChiSq, 20.0, "chi-square");
        Check.Equal(g.Df, 3, "df");
        Check.True(g.P < 0.001, "p-value < 0.001");

        Check.Section("Chi-square association  2x2 [[10,20],[30,40]]");
        var c = HypothesisTests.ChiSquareAssociation(new double[,] { { 10, 20 }, { 30, 40 } });
        Check.Close(c.ChiSq, 0.793651, "chi-square (no Yates)");
        Check.Equal(c.Df, 1, "df");
        Check.Close(c.P, 0.372978, "p-value", 2e-3);
        Check.Close(c.Expected[0, 0], 12.0, "expected[0,0]");

        Check.Section("One-way ANOVA  {1,2,3},{4,5,6},{7,8,9}");
        var a = Anova.OneWay(new (string, double[])[]
        {
            ("G1", new double[] { 1, 2, 3 }),
            ("G2", new double[] { 4, 5, 6 }),
            ("G3", new double[] { 7, 8, 9 }),
        });
        Check.Close(a.SsFactor, 54.0, "SS factor");
        Check.Close(a.SsError, 6.0, "SS error");
        Check.Close(a.SsTotal, 60.0, "SS total");
        Check.Equal(a.DfFactor, 2, "df factor");
        Check.Equal(a.DfError, 6, "df error");
        Check.Close(a.MsFactor, 27.0, "MS factor");
        Check.Close(a.MsError, 1.0, "MS error");
        Check.Close(a.F, 27.0, "F");
        Check.True(a.P < 0.005, "p-value < 0.005");
        Check.Close(a.PooledStDev, 1.0, "pooled StDev");
        Check.Close(a.RSquared, 0.90, "R-squared");
    }
}
