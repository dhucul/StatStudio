using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearAlgebra;

namespace StatStudio.Core.Statistics;

public sealed record MixtureResult(
    string Response, IReadOnlyList<RegressionTerm> Terms,
    double S, double RSquared, double RSquaredAdj, int N, bool Quadratic);

/// <summary>Scheffé canonical mixture-model regression (no intercept, since components sum to 1).</summary>
public static class MixtureAnalysis
{
    public static MixtureResult Fit(double[] y, double[][] components, IReadOnlyList<string> names, bool quadratic)
    {
        StatGuard.Design(y, components, names);
        int n = y.Length;
        int q = components.Length;
        if (q < 2) throw new ArgumentException("Mixture analysis needs at least two components.");
        for (int r = 0; r < n; r++)
        {
            double sum = 0;
            for (int j = 0; j < q; j++)
            {
                if (components[j][r] < 0)
                    throw new ArgumentException($"Run {r + 1} contains a negative mixture component.");
                sum += components[j][r];
            }
            if (Math.Abs(sum - 1) > 1e-8)
                throw new ArgumentException($"Run {r + 1} components must sum to 1.");
        }

        var cols = new List<double[]>();
        var termNames = new List<string>();
        for (int j = 0; j < q; j++) { cols.Add(components[j]); termNames.Add(names[j]); }
        if (quadratic)
            for (int i = 0; i < q; i++)
                for (int j = i + 1; j < q; j++)
                {
                    var col = new double[n];
                    for (int r = 0; r < n; r++) col[r] = components[i][r] * components[j][r];
                    cols.Add(col);
                    termNames.Add($"{names[i]}*{names[j]}");
                }

        int p = cols.Count;
        // n == p leaves zero residual df, so every standard error and p-value comes back NaN.
        if (n <= p) throw new ArgumentException($"Need more than {p} runs for this mixture model.");

        var X = Matrix<double>.Build.Dense(n, p);
        for (int r = 0; r < n; r++) for (int c = 0; c < p; c++) X[r, c] = cols[c][r];
        var Y = Vector<double>.Build.DenseOfArray(y);

        var (beta, XtXinv) = RobustLinearAlgebra.LeastSquares(
            X, Y, "Cannot fit — the mixture design is singular for this model.");

        var fitted = X * beta;
        var resid = Y - fitted;
        double sse = resid.DotProduct(resid);
        double yBar = y.Average();
        double sst = y.Sum(v => (v - yBar) * (v - yBar));
        int dfErr = n - p;
        double mse = dfErr > 0 ? sse / dfErr : double.NaN;
        double s = Math.Sqrt(mse);
        double r2 = sst > 0 ? 1 - sse / sst : double.NaN;
        double r2adj = (sst > 0 && dfErr > 0) ? 1 - (sse / dfErr) / (sst / (n - 1)) : double.NaN;

        var tDist = dfErr > 0 ? new StudentT(0, 1, dfErr) : null;
        var coefs = beta.ToArray();
        var terms = new List<RegressionTerm>(p);
        for (int j = 0; j < p; j++)
        {
            double se = Math.Sqrt(Math.Max(0, mse * XtXinv[j, j]));
            double t = se > 0 ? coefs[j] / se : double.NaN;
            double pv = tDist != null && !double.IsNaN(t) ? 2 * (1 - tDist.CumulativeDistribution(Math.Abs(t))) : double.NaN;
            terms.Add(new RegressionTerm(termNames[j], coefs[j], se, t, pv));
        }
        return new MixtureResult("Y", terms, s, r2, r2adj, n, quadratic);
    }
}
