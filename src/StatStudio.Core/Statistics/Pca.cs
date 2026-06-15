using MathNet.Numerics.LinearAlgebra;

namespace StatStudio.Core.Statistics;

public sealed record PcaResult(
    IReadOnlyList<string> Variables, int N,
    double[] Eigenvalues, double[] Proportion, double[] Cumulative, double[,] Loadings);

public static class Pca
{
    /// <summary>
    /// Principal component analysis on the correlation matrix (default) or covariance matrix.
    /// <paramref name="data"/> is an n×p row-major matrix of complete observations.
    /// </summary>
    public static PcaResult Compute(double[][] data, IReadOnlyList<string> names, bool correlation = true)
    {
        int n = data.Length;
        int p = names.Count;
        if (n < 2 || p < 2) throw new ArgumentException("PCA needs at least 2 observations and 2 variables.");

        var means = new double[p];
        var sds = new double[p];
        for (int j = 0; j < p; j++)
        {
            for (int i = 0; i < n; i++) means[j] += data[i][j];
            means[j] /= n;
            double ss = 0;
            for (int i = 0; i < n; i++) ss += (data[i][j] - means[j]) * (data[i][j] - means[j]);
            sds[j] = Math.Sqrt(ss / (n - 1));
        }

        var m = Matrix<double>.Build.Dense(p, p);
        for (int a = 0; a < p; a++)
            for (int b = 0; b < p; b++)
            {
                double cov = 0;
                for (int i = 0; i < n; i++) cov += (data[i][a] - means[a]) * (data[i][b] - means[b]);
                cov /= n - 1;
                m[a, b] = correlation ? cov / (sds[a] * sds[b]) : cov;
            }

        var evd = m.Evd(Symmetricity.Symmetric);
        var rawVals = evd.EigenValues.Select(c => c.Real).ToArray();
        var order = Enumerable.Range(0, p).OrderByDescending(i => rawVals[i]).ToArray();

        double total = rawVals.Sum();
        var eig = new double[p];
        var prop = new double[p];
        var cum = new double[p];
        var loadings = new double[p, p];
        double running = 0;
        for (int c = 0; c < p; c++)
        {
            int src = order[c];
            eig[c] = rawVals[src];
            prop[c] = total > 0 ? eig[c] / total : double.NaN;
            running += prop[c];
            cum[c] = running;
            var vec = evd.EigenVectors.Column(src);
            for (int r = 0; r < p; r++) loadings[r, c] = vec[r];
        }
        return new PcaResult(names, n, eig, prop, cum, loadings);
    }
}
