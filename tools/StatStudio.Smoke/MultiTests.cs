using StatStudio.Core.Statistics;

namespace StatStudio.Smoke;

internal static class MultiTests
{
    public static void Run()
    {
        Check.Section("PCA (two perfectly correlated variables)");
        var data = new[]
        {
            new double[] { 1, 2 }, new double[] { 2, 4 }, new double[] { 3, 6 },
            new double[] { 4, 8 }, new double[] { 5, 10 },
        };
        var pca = Pca.Compute(data, new[] { "x1", "x2" }, correlation: true);
        Check.Close(pca.Eigenvalues[0], 2.0, "first eigenvalue = 2", 1e-6);
        Check.Close(pca.Eigenvalues[1], 0.0, "second eigenvalue = 0", 1e-6);
        Check.Close(pca.Proportion[0], 1.0, "PC1 explains 100%", 1e-6);

        Check.Section("k-means (two clear clusters)");
        var pts = new[]
        {
            new double[] { 0, 0 }, new double[] { 0, 1 }, new double[] { 1, 0 },
            new double[] { 10, 10 }, new double[] { 10, 11 }, new double[] { 11, 10 },
        };
        var km = KMeans.Cluster(pts, 2, new[] { "x", "y" });
        Check.True(km.Assignments[0] == km.Assignments[1] && km.Assignments[1] == km.Assignments[2], "cluster A grouped");
        Check.True(km.Assignments[3] == km.Assignments[4] && km.Assignments[4] == km.Assignments[5], "cluster B grouped");
        Check.True(km.Assignments[0] != km.Assignments[3], "the two clusters differ");

        Check.Section("Fisher's exact test  [[3,1],[1,3]]");
        var fe = FishersExact.Test(3, 1, 1, 3);
        Check.Close(fe.PTwoSided, 0.485714, "two-sided p", 1e-3);
        Check.Close(fe.OddsRatio, 9.0, "odds ratio");

        Check.Section("Power & sample size");
        double nOne = Power.OneSampleTSampleSize(0.80, 0.5, 0.05, Alternative.TwoSided);
        Check.Close(nOne, 31.4, "1-sample n for d=0.5, power=0.80", 0.3);
        double back = Power.OneSampleTPower(nOne, 0.5, 0.05, Alternative.TwoSided);
        Check.Close(back, 0.80, "round-trip power", 5e-3);
        double nTwo = Power.TwoSampleTSampleSize(0.80, 0.5, 0.05, Alternative.TwoSided);
        Check.Close(nTwo, 62.8, "2-sample n per group ~ 2x", 0.6);
        Check.True(Power.OneProportionPower(1000, 0.5, 0.6, 0.05, Alternative.TwoSided) > 0.99, "large-n proportion power -> ~1");
    }
}
