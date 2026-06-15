using StatStudio.Core.Statistics;

namespace StatStudio.Smoke;

internal static class NonparTests
{
    public static void Run()
    {
        Check.Section("Mann-Whitney  A={1,2,3,4} B={5,6,7,8}");
        var mw = Nonparametric.MannWhitney(new double[] { 1, 2, 3, 4 }, new double[] { 5, 6, 7, 8 });
        Check.Close(mw.W1, 10, "W1 (rank sum of group 1)");
        Check.Close(mw.U, 0, "U statistic");
        Check.Close(mw.Z, -2.309401, "z (normal approx)", 1e-3);
        Check.Close(mw.P, 0.020921, "p-value", 2e-3);

        Check.Section("Wilcoxon signed-rank  {1,-2,3,4,-5}");
        var wsr = Nonparametric.WilcoxonSignedRank(new double[] { 1, -2, 3, 4, -5 });
        Check.Close(wsr.WPlus, 8, "W+");
        Check.Close(wsr.WMinus, 7, "W-");

        Check.Section("Kruskal-Wallis  {1,2,3},{4,5,6},{7,8,9}");
        var kw = Nonparametric.KruskalWallis(new (string, double[])[]
        {
            ("G1", new double[] { 1, 2, 3 }),
            ("G2", new double[] { 4, 5, 6 }),
            ("G3", new double[] { 7, 8, 9 }),
        });
        Check.Close(kw.H, 7.2, "H statistic");
        Check.Equal(kw.Df, 2, "df");
        Check.Close(kw.P, 0.027324, "p-value", 2e-3);

        Check.Section("Sign test  {1..7}, median0=4");
        var st = Nonparametric.SignTest(new double[] { 1, 2, 3, 4, 5, 6, 7 }, 4);
        Check.Equal(st.Below, 3, "below");
        Check.Equal(st.Above, 3, "above");
        Check.Equal(st.Equal, 1, "equal (dropped)");
        Check.Close(st.P, 1.0, "p-value");

        Check.Section("Runs test  {1,2,3,4,5,6}");
        var rt = Nonparametric.RunsTest(new double[] { 1, 2, 3, 4, 5, 6 });
        Check.Equal(rt.Runs, 2, "runs");
        Check.Close(rt.Expected, 4.0, "expected runs");
        Check.Equal(rt.NAbove, 3, "n above");

        Check.Section("Correlation");
        var (r, p, _) = Correlation.Pearson(new double[] { 1, 2, 3, 4, 5 }, new double[] { 2, 4, 5, 4, 5 });
        Check.Close(r, 0.774597, "Pearson r");
        Check.Close(p, 0.124166, "Pearson p", 2e-3);
        var (rho, _, _) = Correlation.Spearman(new double[] { 1, 2, 3, 4, 5 }, new double[] { 1, 4, 9, 16, 25 });
        Check.Close(rho, 1.0, "Spearman rho (monotonic)");

        Check.Section("Anderson-Darling normality (reasonableness)");
        var nd = Normality.AndersonDarling(new double[] { -2, -1.5, -1, -0.5, 0, 0, 0.5, 1, 1.5, 2 });
        var sk = Normality.AndersonDarling(new double[] { 1, 1, 1, 1, 1, 2, 2, 3, 8, 20 });
        Check.True(double.IsFinite(nd.ASquared) && double.IsFinite(sk.ASquared), "A² finite for both");
        Check.True(sk.ASquared > nd.ASquared, "skewed sample has larger A² than symmetric");
        Check.True(sk.P < nd.P, "skewed sample has smaller p than symmetric");
    }
}
