using StatStudio.Core.Inference;

namespace StatStudio.Core.Statistics;

public static class MultivariateFormatters
{
    public static string Pca(PcaResult r)
    {
        int p = r.Variables.Count;
        var eig = new TextTable(new[] { "" }.Concat(Enumerable.Range(1, p).Select(i => $"PC{i}")).ToArray()).LeftAlign(0);
        eig.Add(new[] { "Eigenvalue" }.Concat(r.Eigenvalues.Select(v => Fmt.N(v, 4))).ToArray());
        eig.Add(new[] { "Proportion" }.Concat(r.Proportion.Select(v => Fmt.N(v, 4))).ToArray());
        eig.Add(new[] { "Cumulative" }.Concat(r.Cumulative.Select(v => Fmt.N(v, 4))).ToArray());

        var load = new TextTable(new[] { "Variable" }.Concat(Enumerable.Range(1, p).Select(i => $"PC{i}")).ToArray()).LeftAlign(0);
        for (int v = 0; v < p; v++)
            load.Add(new[] { r.Variables[v] }.Concat(Enumerable.Range(0, p).Select(c => Fmt.N(r.Loadings[v, c], 4))).ToArray());

        return $"Principal Component Analysis: {string.Join(", ", r.Variables)}\n\n" +
               "Eigenanalysis of the Correlation Matrix\n" + eig + "\n\n" +
               "Eigenvectors (loadings)\n" + load;
    }

    public static string KMeans(KMeansResult r)
    {
        var sizes = new TextTable("Cluster", "N", "Within SS").LeftAlign(0);
        for (int c = 0; c < r.K; c++) sizes.Add((c + 1).ToString(), r.Sizes[c].ToString(), Fmt.N(r.WithinSS[c], 3));

        var cent = new TextTable(new[] { "Cluster" }.Concat(r.Variables).ToArray()).LeftAlign(0);
        for (int c = 0; c < r.K; c++)
            cent.Add(new[] { (c + 1).ToString() }.Concat(r.Centroids[c].Select(v => Fmt.N(v, 3))).ToArray());

        return $"K-Means Cluster Analysis: {string.Join(", ", r.Variables)}\n\n" +
               $"Number of clusters: {r.K}   (converged in {r.Iterations} iterations)\n\n" +
               "Cluster Summary\n" + sizes + $"\n  Total within-cluster SS = {Fmt.N(r.TotalWithinSS, 3)}\n\n" +
               "Cluster Centroids\n" + cent;
    }

    public static string Fisher(FisherResult r)
    {
        var t = new TextTable("", "Col 1", "Col 2").LeftAlign(0);
        t.Add("Row 1", r.A.ToString(), r.B.ToString());
        t.Add("Row 2", r.C.ToString(), r.D.ToString());
        var test = new TextTable("Two-sided P", "Less P", "Greater P", "Odds Ratio");
        test.Add(Fmt.P(r.PTwoSided), Fmt.P(r.PLess), Fmt.P(r.PGreater),
            double.IsInfinity(r.OddsRatio) ? "inf" : Fmt.N(r.OddsRatio, 4));
        return "Fisher's Exact Test (2×2)\n\n" + t + "\n\n" + test;
    }

    public static string Power(string test, string solveFor, double alpha, Alternative alt,
        string effectDescription, double sampleSize, double power)
    {
        var t = new TextTable("Quantity", "Value").LeftAlign(0);
        t.Add("Test", test);
        t.Add("Alternative", alt.ToString());
        t.Add("Alpha", Fmt.N(alpha, 3));
        t.Add("Effect", effectDescription);
        t.Add("Sample size", sampleSize >= 100000 ? "*" : Fmt.N(sampleSize, 2));
        t.Add("Power", Fmt.N(power, 4));
        return $"Power and Sample Size — {test}\n(solving for {solveFor}; normal approximation)\n\n" + t;
    }
}
