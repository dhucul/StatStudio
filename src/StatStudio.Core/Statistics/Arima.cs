using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Optimization;

namespace StatStudio.Core.Statistics;

public sealed record ArimaTerm(string Name, double Coef, double Se, double T, double P);

public sealed record ArimaResult(
    int P, int D, int Q, bool IncludeConstant,
    IReadOnlyList<ArimaTerm> Terms, double Sigma2, double LogLikelihood, double Aic,
    double[] Residuals, double[] Forecasts, double[] ForecastLower, double[] ForecastUpper, int N);

/// <summary>
/// ARIMA(p,d,q) by conditional least squares. Pure AR (q=0) is solved exactly by OLS;
/// models with an MA term are fit by Nelder-Mead minimization of the conditional SS.
/// </summary>
public static class Arima
{
    public static ArimaResult Fit(double[] series, int p, int d, int q,
        int forecasts = 0, bool includeConstant = true)
    {
        if (p < 0 || q < 0 || d < 0 || p > 5 || q > 5 || d > 2)
            throw new ArgumentException("ARIMA orders out of range (p,q ≤ 5, d ≤ 2).");

        double[] w = series;
        for (int i = 0; i < d; i++) w = Diff(w);
        int m = w.Length;
        int nc = includeConstant ? 1 : 0;
        int nParams = nc + p + q;
        if (m <= nParams + 2) throw new ArgumentException("Series too short for the specified ARIMA order.");

        double[] theta;
        if (q == 0)
            theta = FitArOls(w, p, includeConstant);
        else
        {
            var start = new double[nParams];
            if (p > 0)
            {
                var ar = FitArOls(w, p, includeConstant);
                Array.Copy(ar, start, Math.Min(ar.Length, nParams));
            }
            else if (includeConstant) start[0] = w.Average();
            var obj = ObjectiveFunction.Value(v => Css(w, v.ToArray(), p, q, includeConstant));
            var solver = new NelderMeadSimplex(1e-10, 5000);
            var res = solver.FindMinimum(obj, Vector<double>.Build.DenseOfArray(start));
            theta = res.MinimizingPoint.ToArray();
        }

        var resid = Residuals(w, theta, p, q, includeConstant);
        int nEff = m - p;
        double ss = 0;
        for (int t = p; t < m; t++) ss += resid[t] * resid[t];
        double sigma2 = ss / nEff;
        double logLik = -0.5 * nEff * (Math.Log(2 * Math.PI * sigma2) + 1);
        double aic = -2 * logLik + 2 * (nParams + 1);

        var se = StdErrors(w, theta, p, q, includeConstant, sigma2, nParams);
        var terms = new List<ArimaTerm>();
        var tdist = new StudentT(0, 1, Math.Max(1, nEff - nParams));
        int idx = 0;
        if (includeConstant) terms.Add(MakeTerm("Constant", theta[idx], se[idx++], tdist));
        for (int i = 1; i <= p; i++) terms.Add(MakeTerm($"AR({i})", theta[idx], se[idx++], tdist));
        for (int j = 1; j <= q; j++) terms.Add(MakeTerm($"MA({j})", theta[idx], se[idx++], tdist));

        var (fc, lo, hi) = Forecast(series, w, theta, p, d, q, includeConstant, resid, sigma2, forecasts);
        return new ArimaResult(p, d, q, includeConstant, terms, sigma2, logLik, aic,
            resid[p..], fc, lo, hi, series.Length);
    }

    // ---- core recursions ---------------------------------------------------

    private static double[] Residuals(double[] w, double[] theta, int p, int q, bool c)
    {
        int m = w.Length, nc = c ? 1 : 0;
        var e = new double[m];
        for (int t = p; t < m; t++)
        {
            double pred = c ? theta[0] : 0;
            for (int i = 1; i <= p; i++) pred += theta[nc + i - 1] * w[t - i];
            for (int j = 1; j <= q; j++) pred += theta[nc + p + j - 1] * (t - j >= p ? e[t - j] : 0);
            e[t] = w[t] - pred;
        }
        return e;
    }

    private static double Css(double[] w, double[] theta, int p, int q, bool c)
    {
        var e = Residuals(w, theta, p, q, c);
        double ss = 0;
        for (int t = p; t < w.Length; t++) ss += e[t] * e[t];
        return ss;
    }

    private static double[] FitArOls(double[] w, int p, bool c)
    {
        int m = w.Length, nc = c ? 1 : 0, cols = nc + p;
        int rows = m - p;
        var X = Matrix<double>.Build.Dense(rows, cols);
        var Y = Vector<double>.Build.Dense(rows);
        for (int t = p; t < m; t++)
        {
            int r = t - p;
            int col = 0;
            if (c) X[r, col++] = 1;
            for (int i = 1; i <= p; i++) X[r, col++] = w[t - i];
            Y[r] = w[t];
        }
        var beta = (X.TransposeThisAndMultiply(X)).Inverse() * (X.TransposeThisAndMultiply(Y));
        return beta.ToArray();
    }

    private static double[] StdErrors(double[] w, double[] theta, int p, int q, bool c, double sigma2, int nParams)
    {
        try
        {
            int m = w.Length, rows = m - p;
            var J = Matrix<double>.Build.Dense(rows, nParams);
            double h = 1e-5;
            for (int k = 0; k < nParams; k++)
            {
                var up = (double[])theta.Clone(); up[k] += h;
                var dn = (double[])theta.Clone(); dn[k] -= h;
                var eu = Residuals(w, up, p, q, c);
                var ed = Residuals(w, dn, p, q, c);
                for (int t = p; t < m; t++) J[t - p, k] = (eu[t] - ed[t]) / (2 * h);
            }
            var cov = (J.TransposeThisAndMultiply(J)).Inverse() * sigma2;
            var se = new double[nParams];
            for (int k = 0; k < nParams; k++) se[k] = Math.Sqrt(Math.Max(0, cov[k, k]));
            return se;
        }
        catch
        {
            return Enumerable.Repeat(double.NaN, nParams).ToArray();
        }
    }

    // ---- forecasting -------------------------------------------------------

    private static (double[] Fc, double[] Lo, double[] Hi) Forecast(double[] series, double[] w, double[] theta,
        int p, int d, int q, bool c, double[] resid, double sigma2, int h)
    {
        if (h <= 0) return (Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>());
        int m = w.Length, nc = c ? 1 : 0;

        var wExt = new double[m + h];
        Array.Copy(w, wExt, m);
        var eExt = new double[m + h];
        Array.Copy(resid, eExt, m);
        for (int t = m; t < m + h; t++)
        {
            double pred = c ? theta[0] : 0;
            for (int i = 1; i <= p; i++) pred += theta[nc + i - 1] * wExt[t - i];
            for (int j = 1; j <= q; j++) pred += theta[nc + p + j - 1] * (t - j < m ? eExt[t - j] : 0);
            wExt[t] = pred;
            eExt[t] = 0;
        }
        var fcW = new double[h];
        for (int i = 0; i < h; i++) fcW[i] = wExt[m + i];

        // Integrate back to the original scale using the last value of each difference level.
        var lastVal = new double[d];
        var cur = series;
        for (int l = 0; l < d; l++) { lastVal[l] = cur[^1]; cur = Diff(cur); }

        var fc = new double[h];
        for (int step = 0; step < h; step++)
        {
            double v = fcW[step];
            for (int l = d - 1; l >= 0; l--) { v = lastVal[l] + v; lastVal[l] = v; }
            fc[step] = v;
        }

        // Forecast-error variance from psi-weights of the (differenced) ARMA model.
        var psi = PsiWeights(theta, p, q, c, h);
        var lo = new double[h];
        var hi = new double[h];
        double cumVar = 0;
        for (int step = 0; step < h; step++)
        {
            cumVar += psi[step] * psi[step];
            double half = 1.959964 * Math.Sqrt(sigma2 * cumVar);
            lo[step] = fc[step] - half;
            hi[step] = fc[step] + half;
        }
        return (fc, lo, hi);
    }

    private static double[] PsiWeights(double[] theta, int p, int q, bool c, int h)
    {
        int nc = c ? 1 : 0;
        var psi = new double[h];
        psi[0] = 1;
        for (int j = 1; j < h; j++)
        {
            double v = j <= q ? theta[nc + p + j - 1] : 0;
            for (int i = 1; i <= p && i <= j; i++) v += theta[nc + i - 1] * psi[j - i];
            psi[j] = v;
        }
        return psi;
    }

    // ---- helpers -----------------------------------------------------------

    private static double[] Diff(double[] x)
    {
        var r = new double[x.Length - 1];
        for (int i = 0; i < r.Length; i++) r[i] = x[i + 1] - x[i];
        return r;
    }

    private static ArimaTerm MakeTerm(string name, double coef, double se, StudentT t)
    {
        double tv = se > 0 ? coef / se : double.NaN;
        double pv = !double.IsNaN(tv) ? 2 * (1 - t.CumulativeDistribution(Math.Abs(tv))) : double.NaN;
        return new ArimaTerm(name, coef, se, tv, pv);
    }
}
