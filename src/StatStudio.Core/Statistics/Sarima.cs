using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Optimization;

namespace StatStudio.Core.Statistics;

public sealed record SarimaResult(
    int P, int D, int Q, int SeasonalP, int SeasonalD, int SeasonalQ, int Season, bool IncludeConstant,
    IReadOnlyList<ArimaTerm> Terms, double Sigma2, double LogLikelihood, double Aic,
    double[] Residuals, double[] Forecasts, double[] ForecastLower, double[] ForecastUpper, int N);

/// <summary>
/// Seasonal ARIMA — ARIMA(p,d,q)(P,D,Q)_s — by conditional least squares. The seasonal
/// and regular AR/MA polynomials are multiplied out; fitting is by Nelder-Mead.
/// </summary>
public static class Sarima
{
    public static SarimaResult Fit(double[] series, int p, int d, int q,
        int sp, int sd, int sq, int s, int forecasts = 0, bool includeConstant = true)
    {
        if (s < 1) throw new ArgumentException("Seasonal period must be ≥ 1.");
        if (p < 0 || q < 0 || sp < 0 || sq < 0 || d < 0 || sd < 0)
            throw new ArgumentException("Orders must be non-negative.");

        // Difference: regular d times, then seasonal D times — recording each stage for integration.
        var stages = new List<(int Lag, double[] Pre)>();
        var cur = series;
        for (int i = 0; i < d; i++) { stages.Add((1, cur)); cur = Diff(cur, 1); }
        for (int i = 0; i < sd; i++) { stages.Add((s, cur)); cur = Diff(cur, s); }
        double[] w = cur;
        int m = w.Length;

        int nc = includeConstant ? 1 : 0;
        int nParams = nc + p + sp + q + sq;
        int maxArLag = p + sp * s;
        if (m <= maxArLag + 2) throw new ArgumentException("Series too short for the specified SARIMA order.");

        double[] theta;
        if (nParams == 0) theta = Array.Empty<double>();
        else
        {
            var start = new double[nParams];
            if (includeConstant) start[0] = w.Average();
            if (nParams == nc) theta = start; // constant only -> CSS minimized at mean
            else
            {
                var obj = ObjectiveFunction.Value(v => Css(w, v.ToArray(), p, sp, q, sq, s, includeConstant, maxArLag));
                var solver = new NelderMeadSimplex(1e-10, 8000);
                theta = solver.FindMinimum(obj, Vector<double>.Build.DenseOfArray(start)).MinimizingPoint.ToArray();
            }
        }

        var resid = Residuals(w, theta, p, sp, q, sq, s, includeConstant, maxArLag);
        int nEff = m - maxArLag;
        double ss = 0;
        for (int t = maxArLag; t < m; t++) ss += resid[t] * resid[t];
        double sigma2 = ss / nEff;
        double logLik = -0.5 * nEff * (Math.Log(2 * Math.PI * sigma2) + 1);
        double aic = -2 * logLik + 2 * (nParams + 1);

        var se = StdErrors(w, theta, p, sp, q, sq, s, includeConstant, maxArLag, sigma2, nParams);
        var terms = new List<ArimaTerm>();
        var tdist = new StudentT(0, 1, Math.Max(1, nEff - nParams));
        int idx = 0;
        if (includeConstant) terms.Add(Term("Constant", theta, se, ref idx, tdist));
        for (int i = 1; i <= p; i++) terms.Add(Term($"AR({i})", theta, se, ref idx, tdist));
        for (int i = 1; i <= sp; i++) terms.Add(Term($"SAR({i})", theta, se, ref idx, tdist));
        for (int j = 1; j <= q; j++) terms.Add(Term($"MA({j})", theta, se, ref idx, tdist));
        for (int j = 1; j <= sq; j++) terms.Add(Term($"SMA({j})", theta, se, ref idx, tdist));

        var (fc, lo, hi) = Forecast(series, w, theta, p, sp, q, sq, s, includeConstant, maxArLag, resid, sigma2, stages, forecasts);
        return new SarimaResult(p, d, q, sp, sd, sq, s, includeConstant, terms, sigma2, logLik, aic,
            resid[maxArLag..], fc, lo, hi, series.Length);
    }

    // ---- combined polynomials ---------------------------------------------

    // Returns AR coefficients a_k (positive convention: w_t = c + Σ a_k w_{t-k} + …).
    private static double[] ArCoefs(double[] theta, int p, int sp, int s, bool c)
    {
        int nc = c ? 1 : 0;
        var phi = new double[p + 1]; phi[0] = 1;
        for (int i = 1; i <= p; i++) phi[i] = -theta[nc + i - 1];
        var bigPhi = new double[sp * s + 1]; bigPhi[0] = 1;
        for (int j = 1; j <= sp; j++) bigPhi[j * s] = -theta[nc + p + j - 1];
        var A = Convolve(phi, bigPhi);
        var a = new double[A.Length - 1];
        for (int k = 1; k < A.Length; k++) a[k - 1] = -A[k];
        return a;
    }

    // Returns MA coefficients m_k (positive convention).
    private static double[] MaCoefs(double[] theta, int p, int sp, int q, int sq, int s, bool c)
    {
        int nc = c ? 1 : 0;
        var th = new double[q + 1]; th[0] = 1;
        for (int j = 1; j <= q; j++) th[j] = theta[nc + p + sp + j - 1];
        var bigTh = new double[sq * s + 1]; bigTh[0] = 1;
        for (int j = 1; j <= sq; j++) bigTh[j * s] = theta[nc + p + sp + q + j - 1];
        var M = Convolve(th, bigTh);
        var mm = new double[M.Length - 1];
        for (int k = 1; k < M.Length; k++) mm[k - 1] = M[k];
        return mm;
    }

    private static double[] Residuals(double[] w, double[] theta, int p, int sp, int q, int sq, int s, bool c, int maxArLag)
    {
        int m = w.Length;
        double cc = c ? theta[0] : 0;
        var a = ArCoefs(theta, p, sp, s, c);
        var mm = MaCoefs(theta, p, sp, q, sq, s, c);
        var e = new double[m];
        for (int t = maxArLag; t < m; t++)
        {
            double pred = cc;
            for (int k = 1; k <= a.Length; k++) pred += a[k - 1] * w[t - k];
            for (int k = 1; k <= mm.Length; k++) pred += mm[k - 1] * (t - k >= maxArLag ? e[t - k] : 0);
            e[t] = w[t] - pred;
        }
        return e;
    }

    private static double Css(double[] w, double[] theta, int p, int sp, int q, int sq, int s, bool c, int maxArLag)
    {
        var e = Residuals(w, theta, p, sp, q, sq, s, c, maxArLag);
        double ss = 0;
        for (int t = maxArLag; t < w.Length; t++) ss += e[t] * e[t];
        return ss;
    }

    private static double[] StdErrors(double[] w, double[] theta, int p, int sp, int q, int sq, int s,
        bool c, int maxArLag, double sigma2, int nParams)
    {
        if (nParams == 0) return Array.Empty<double>();
        try
        {
            int m = w.Length, rows = m - maxArLag;
            var J = Matrix<double>.Build.Dense(rows, nParams);
            double h = 1e-5;
            for (int k = 0; k < nParams; k++)
            {
                var up = (double[])theta.Clone(); up[k] += h;
                var dn = (double[])theta.Clone(); dn[k] -= h;
                var eu = Residuals(w, up, p, sp, q, sq, s, c, maxArLag);
                var ed = Residuals(w, dn, p, sp, q, sq, s, c, maxArLag);
                for (int t = maxArLag; t < m; t++) J[t - maxArLag, k] = (eu[t] - ed[t]) / (2 * h);
            }
            var cov = (J.TransposeThisAndMultiply(J)).Inverse() * sigma2;
            var se = new double[nParams];
            for (int k = 0; k < nParams; k++) se[k] = Math.Sqrt(Math.Max(0, cov[k, k]));
            return se;
        }
        catch { return Enumerable.Repeat(double.NaN, nParams).ToArray(); }
    }

    private static (double[] Fc, double[] Lo, double[] Hi) Forecast(double[] series, double[] w, double[] theta,
        int p, int sp, int q, int sq, int s, bool c, int maxArLag, double[] resid, double sigma2,
        List<(int Lag, double[] Pre)> stages, int h)
    {
        if (h <= 0) return (Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>());
        int m = w.Length;
        double cc = c ? theta[0] : 0;
        var a = ArCoefs(theta, p, sp, s, c);
        var mm = MaCoefs(theta, p, sp, q, sq, s, c);

        var wExt = new double[m + h]; Array.Copy(w, wExt, m);
        var eExt = new double[m + h]; Array.Copy(resid, eExt, m);
        for (int t = m; t < m + h; t++)
        {
            double pred = cc;
            for (int k = 1; k <= a.Length; k++) pred += a[k - 1] * wExt[t - k];
            for (int k = 1; k <= mm.Length; k++) pred += mm[k - 1] * (t - k < m ? eExt[t - k] : 0);
            wExt[t] = pred; eExt[t] = 0;
        }
        var fc = new double[h];
        for (int i = 0; i < h; i++) fc[i] = wExt[m + i];

        // Integrate back through each differencing stage in reverse.
        for (int st = stages.Count - 1; st >= 0; st--)
        {
            int lag = stages[st].Lag;
            var pre = stages[st].Pre;
            var ext = new List<double>(pre);
            int p0 = pre.Length;
            var integ = new double[h];
            for (int step = 0; step < h; step++)
            {
                double prev = ext[p0 + step - lag];
                double val = fc[step] + prev;
                integ[step] = val; ext.Add(val);
            }
            fc = integ;
        }

        // psi-weight CI on the differenced scale (approximate at the original scale).
        var psi = Psi(a, mm, h);
        var lo = new double[h]; var hi = new double[h];
        double cumVar = 0;
        for (int step = 0; step < h; step++)
        {
            cumVar += psi[step] * psi[step];
            double half = 1.959964 * Math.Sqrt(sigma2 * cumVar);
            lo[step] = fc[step] - half; hi[step] = fc[step] + half;
        }
        return (fc, lo, hi);
    }

    private static double[] Psi(double[] a, double[] mm, int h)
    {
        var psi = new double[h]; psi[0] = 1;
        for (int j = 1; j < h; j++)
        {
            double v = j <= mm.Length ? mm[j - 1] : 0;
            for (int i = 1; i <= a.Length && i <= j; i++) v += a[i - 1] * psi[j - i];
            psi[j] = v;
        }
        return psi;
    }

    // ---- helpers -----------------------------------------------------------

    private static double[] Diff(double[] x, int lag)
    {
        var r = new double[x.Length - lag];
        for (int i = 0; i < r.Length; i++) r[i] = x[i + lag] - x[i];
        return r;
    }

    private static double[] Convolve(double[] a, double[] b)
    {
        var r = new double[a.Length + b.Length - 1];
        for (int i = 0; i < a.Length; i++)
            for (int j = 0; j < b.Length; j++) r[i + j] += a[i] * b[j];
        return r;
    }

    private static ArimaTerm Term(string name, double[] theta, double[] se, ref int idx, StudentT t)
    {
        double coef = theta[idx];
        double s = idx < se.Length ? se[idx] : double.NaN;
        idx++;
        double tv = s > 0 ? coef / s : double.NaN;
        double pv = !double.IsNaN(tv) ? 2 * (1 - t.CumulativeDistribution(Math.Abs(tv))) : double.NaN;
        return new ArimaTerm(name, coef, s, tv, pv);
    }
}
