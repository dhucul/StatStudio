using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearAlgebra;

namespace StatStudio.Core.Statistics;

public sealed record RegressionTerm(string Name, double Coef, double SeCoef, double T, double P);

public sealed record RegressionResult(
    string Response,
    IReadOnlyList<string> Predictors,
    IReadOnlyList<RegressionTerm> Terms,
    double S, double RSquared, double RSquaredAdj,
    double SsRegression, double SsError, double SsTotal,
    int DfRegression, int DfError, int DfTotal,
    double MsRegression, double MsError, double F, double P,
    double[] Coefficients, double[] Fitted, double[] Residuals,
    int N, string Equation);

public static class Regression
{
    public static RegressionResult SimpleLinear(double[] x, double[] y,
        string predictor = "X", string response = "Y") =>
        Fit(y, new[] { x }, new[] { predictor }, response);

    /// <summary>OLS multiple linear regression with full inferential output.</summary>
    public static RegressionResult Fit(double[] y, double[][] predictors,
        IReadOnlyList<string> predictorNames, string response = "Y")
    {
        int n = y.Length;
        int k = predictors.Length;          // number of predictors
        int p = k + 1;                      // params incl. intercept
        if (n <= p) throw new ArgumentException($"Need more than {p} observations for {k} predictor(s).");

        var X = Matrix<double>.Build.Dense(n, p);
        for (int i = 0; i < n; i++)
        {
            X[i, 0] = 1.0;
            for (int j = 0; j < k; j++) X[i, j + 1] = predictors[j][i];
        }
        var Y = Vector<double>.Build.DenseOfArray(y);

        var Xt = X.Transpose();
        var XtXinv = (Xt * X).Inverse();
        var beta = XtXinv * (Xt * Y);
        if (beta.Any(b => double.IsNaN(b) || double.IsInfinity(b)))
            throw new ArgumentException(
                "Cannot fit the model — the predictors are constant or collinear (singular design matrix).");
        var fittedV = X * beta;
        var residV = Y - fittedV;

        double sse = residV.DotProduct(residV);
        double yBar = y.Average();
        double sst = y.Sum(v => (v - yBar) * (v - yBar));
        double ssr = sst - sse;

        int dfReg = k, dfErr = n - p, dfTot = n - 1;
        double msr = dfReg > 0 ? ssr / dfReg : double.NaN;
        double mse = dfErr > 0 ? sse / dfErr : double.NaN;
        double s = Math.Sqrt(mse);
        double f = mse > 0 ? msr / mse : double.NaN;
        double pF = (dfReg > 0 && dfErr > 0 && !double.IsNaN(f))
            ? 1 - new FisherSnedecor(dfReg, dfErr).CumulativeDistribution(f)
            : double.NaN;

        double r2 = sst > 0 ? ssr / sst : double.NaN;
        double r2adj = (sst > 0 && dfErr > 0) ? 1 - (sse / dfErr) / (sst / dfTot) : double.NaN;

        var tDist = dfErr > 0 ? new StudentT(0, 1, dfErr) : null;
        var terms = new List<RegressionTerm>(p);
        var names = new List<string> { "Constant" };
        names.AddRange(predictorNames);
        var coefs = beta.ToArray();
        for (int j = 0; j < p; j++)
        {
            double se = Math.Sqrt(Math.Max(0, mse * XtXinv[j, j]));
            double t = se > 0 ? coefs[j] / se : double.NaN;
            double pv = (tDist != null && !double.IsNaN(t))
                ? 2 * (1 - tDist.CumulativeDistribution(Math.Abs(t))) : double.NaN;
            terms.Add(new RegressionTerm(names[j], coefs[j], se, t, pv));
        }

        return new RegressionResult(response, predictorNames, terms, s, r2, r2adj,
            ssr, sse, sst, dfReg, dfErr, dfTot, msr, mse, f, pF,
            coefs, fittedV.ToArray(), residV.ToArray(), n, BuildEquation(response, predictorNames, coefs));
    }

    private static string BuildEquation(string response, IReadOnlyList<string> predictors, double[] coef)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(response).Append(" = ").Append(Round(coef[0]));
        for (int j = 0; j < predictors.Count; j++)
        {
            double c = coef[j + 1];
            sb.Append(c >= 0 ? " + " : " - ").Append(Round(Math.Abs(c))).Append(' ').Append(predictors[j]);
        }
        return sb.ToString();
    }

    private static string Round(double v) =>
        v.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
}
