using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearAlgebra;

namespace StatStudio.Core.Statistics;

public sealed record LogisticTerm(string Name, double Coef, double Se, double Z, double P, double OddsRatio);

public sealed record LogisticResult(
    string Response, IReadOnlyList<string> Predictors, IReadOnlyList<LogisticTerm> Terms,
    double Deviance, double NullDeviance, int N, int Iterations, bool Converged, double[] Coefficients);

public static class Logistic
{
    /// <summary>Binary logistic regression fit by iteratively reweighted least squares (Newton-Raphson).</summary>
    public static LogisticResult Fit(double[] y, double[][] predictors, IReadOnlyList<string> predictorNames,
        string response = "Y")
    {
        StatGuard.Design(y, predictors, predictorNames);
        int n = y.Length;
        int k = predictors.Length;
        int p = k + 1;
        if (n <= p) throw new ArgumentException($"Need more than {p} observations.");
        if (y.Any(v => v != 0 && v != 1)) throw new ArgumentException("Response must be binary (0/1).");
        if (!y.Contains(0) || !y.Contains(1))
            throw new ArgumentException("Response must contain both outcome classes.");

        var X = Matrix<double>.Build.Dense(n, p);
        for (int i = 0; i < n; i++) { X[i, 0] = 1; for (int j = 0; j < k; j++) X[i, j + 1] = predictors[j][i]; }
        var Y = Vector<double>.Build.DenseOfArray(y);

        var beta = Vector<double>.Build.Dense(p, 0.0);
        Matrix<double>? xtwxInv = null;
        bool converged = false;
        int iter = 0;
        var Xt = X.Transpose();                             // loop-invariant
        var weighted = Matrix<double>.Build.Dense(n, p);    // reused scratch for W·X
        for (; iter < 50; iter++)
        {
            var eta = X * beta;
            var mu = eta.Map(Sigmoid);
            var w = mu.Map(m => Math.Max(m * (1 - m), 1e-9));
            // Scale the rows of X by w rather than forming the n x n diagonal W: materialising it
            // cost n^2 doubles per iteration (800 MB at n = 10 000, allocated up to 50 times).
            for (int i = 0; i < n; i++)
            {
                double wi = w[i];
                for (int j = 0; j < p; j++) weighted[i, j] = X[i, j] * wi;
            }
            var xtwx = Xt * weighted;
            var grad = Xt * (Y - mu);
            var solved = RobustLinearAlgebra.SolveSquare(
                xtwx, grad, "Cannot fit logistic regression — the weighted design matrix is singular.");
            var delta = solved.Solution;
            xtwxInv = solved.Inverse;
            beta += delta;
            if (delta.AbsoluteMaximum() < 1e-10) { converged = true; iter++; break; }
        }

        var muFinal = (X * beta).Map(Sigmoid);
        double dev = -2 * Deviance(y, muFinal.ToArray());
        double pBar = y.Average();
        double nullDev = -2 * y.Sum(yi => yi * SafeLog(pBar) + (1 - yi) * SafeLog(1 - pBar));

        var names = new List<string> { "Constant" };
        names.AddRange(predictorNames);
        var coefs = beta.ToArray();
        var terms = new List<LogisticTerm>(p);
        for (int j = 0; j < p; j++)
        {
            double se = Math.Sqrt(Math.Max(0, xtwxInv![j, j]));
            double z = se > 0 ? coefs[j] / se : double.NaN;
            double pv = 2 * (1 - Normal.CDF(0, 1, Math.Abs(z)));
            double or = j == 0 ? double.NaN : Math.Exp(coefs[j]);
            terms.Add(new LogisticTerm(names[j], coefs[j], se, z, pv, or));
        }

        return new LogisticResult(response, predictorNames, terms, dev, nullDev, n, iter, converged, coefs);
    }

    private static double Sigmoid(double z) => 1.0 / (1.0 + Math.Exp(-z));

    private static double Deviance(double[] y, double[] mu)
    {
        double s = 0;
        for (int i = 0; i < y.Length; i++) s += y[i] * SafeLog(mu[i]) + (1 - y[i]) * SafeLog(1 - mu[i]);
        return s;
    }

    private static double SafeLog(double p) => Math.Log(Math.Min(1 - 1e-15, Math.Max(1e-15, p)));
}
