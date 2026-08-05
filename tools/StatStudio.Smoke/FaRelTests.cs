using StatStudio.Core.Statistics;

namespace StatStudio.Smoke;

internal static class FaRelTests
{
    /// <summary>
    /// Right-censored maximum likelihood. Before this existed, censored units were fitted as if
    /// they had failed at their censoring time, biasing every estimate toward shorter life.
    /// </summary>
    private static void CensoredLifeData()
    {
        Check.Section("Reliability — right-censored MLE");

        // Weibull(shape 2, scale 1000) observed through a study that stops at 900 h. Generated from
        // exact quantiles so the target parameters are recoverable without simulation noise.
        const int n = 400;
        const double trueShape = 2.0, trueScale = 1000.0, cutoff = 900.0;
        var times = new double[n];
        var censored = new bool[n];
        for (int i = 1; i <= n; i++)
        {
            double life = trueScale * Math.Pow(-Math.Log(1 - (i - 0.5) / n), 1.0 / trueShape);
            censored[i - 1] = life > cutoff;
            times[i - 1] = censored[i - 1] ? cutoff : life;
        }
        int censoredCount = censored.Count(c => c);
        Check.True(censoredCount > n / 5, $"test fixture is meaningfully censored ({censoredCount}/{n})");

        var handled = Reliability.FitWeibull(times, censored);
        Check.Close(handled.Parameters[0].Value, trueShape, "censored Weibull recovers the shape", 0.05);
        Check.Close(handled.Parameters[1].Value, trueScale, "censored Weibull recovers the scale", 0.05);
        Check.Equal(handled.Failures, n - censoredCount, "failure count reported");
        Check.Equal(handled.RightCensored, censoredCount, "censored count reported");

        // The whole point: ignoring the flags must be visibly worse.
        var ignored = Reliability.FitWeibull(times);
        Check.True(Math.Abs(ignored.Parameters[1].Value - trueScale) >
                   Math.Abs(handled.Parameters[1].Value - trueScale) * 5,
            "ignoring censoring is far more biased than handling it");
        Check.True(ignored.Mean.Value < handled.Mean.Value, "ignoring censoring under-states mean life");

        // Exponential has a closed form: total time on test over the number of failures.
        var expTimes = new double[] { 10, 20, 30, 40, 50 };
        var expCens = new[] { false, false, true, false, true };
        var exponential = Reliability.FitExponential(expTimes, expCens);
        Check.Close(exponential.Mean.Value, 150.0 / 3.0, "exponential MLE = total time on test / failures");

        // Location-scale fits fall back to the closed form when nothing is censored, and switch to
        // the numerical censored likelihood otherwise.
        var plain = new double[] { 8, 9, 10, 11, 12, 13, 14 };
        var noneCensored = new bool[plain.Length];
        Check.Close(Reliability.FitNormal(plain, noneCensored).Parameters[0].Value,
            Reliability.FitNormal(plain).Parameters[0].Value, "no-censoring normal path is unchanged", 1e-12);
        var normalCens = new[] { false, false, false, false, false, true, true };
        var censoredNormal = Reliability.FitNormal(plain, normalCens);
        Check.True(censoredNormal.Parameters[0].Value > Reliability.FitNormal(plain).Parameters[0].Value,
            "censored normal mean exceeds the naive mean");
        Check.True(Reliability.FitLognormal(plain, normalCens).Mean.Value > Reliability.FitLognormal(plain).Mean.Value,
            "censored lognormal mean exceeds the naive mean");

        // A likelihood with no failures has no maximum.
        Check.Throws<ArgumentException>(
            () => Reliability.FitWeibull(new double[] { 1, 2, 3 }, new[] { true, true, true }),
            "all-censored sample rejected");
        Check.Throws<ArgumentException>(
            () => Reliability.FitWeibull(new double[] { 1, 2, 3 }, new[] { true, true }),
            "censor-flag length mismatch rejected");
    }

    /// <summary>
    /// Standard errors come from a numerical Hessian, so they are pinned against the closed-form
    /// results that exist for these distributions rather than against previous output.
    /// </summary>
    private static void StandardErrorsAndIntervals()
    {
        Check.Section("Reliability — standard errors & confidence intervals");

        // Exponential: Var(mean) = mean^2 / failures, exactly — censored or not.
        const int ne = 500;
        var expData = Enumerable.Range(1, ne).Select(i => -300.0 * Math.Log(1 - (i - 0.5) / ne)).ToArray();
        var exponential = Reliability.FitExponential(expData);
        Check.Close(exponential.Parameters[0].StandardError,
            exponential.Parameters[0].Value / Math.Sqrt(ne), "exponential SE = mean / sqrt(failures)", 1e-3);

        var partlyCensored = expData.Select(v => v > 400).ToArray();
        var expCensored = Reliability.FitExponential(expData, partlyCensored);
        Check.Close(expCensored.Parameters[0].StandardError,
            expCensored.Parameters[0].Value / Math.Sqrt(expCensored.Failures),
            "censored exponential SE uses the failure count", 1e-3);

        // Normal: SE(mu) = sigma/sqrt(n), SE(sigma) = sigma/sqrt(2n).
        const int ng = 800;
        var normalData = Enumerable.Range(1, ng)
            .Select(i => 500 + 40 * MathNet.Numerics.Distributions.Normal.InvCDF(0, 1, (i - 0.5) / ng)).ToArray();
        var normal = Reliability.FitNormal(normalData);
        double sigma = normal.Parameters[1].Value;
        Check.Close(normal.Parameters[0].StandardError, sigma / Math.Sqrt(ng), "normal SE(mu) = sigma/sqrt(n)", 1e-3);
        Check.Close(normal.Parameters[1].StandardError, sigma / Math.Sqrt(2.0 * ng), "normal SE(sigma) = sigma/sqrt(2n)", 1e-3);

        // Weibull large-sample: Var(beta) = 0.6079 beta^2/n, Var(eta) = 1.1087 eta^2/(n beta^2).
        const int nw = 2000;
        var weibullData = Enumerable.Range(1, nw)
            .Select(i => 1000 * Math.Pow(-Math.Log(1 - (i - 0.5) / nw), 0.5)).ToArray();
        var weibull = Reliability.FitWeibull(weibullData);
        double beta = weibull.Parameters[0].Value, eta = weibull.Parameters[1].Value;
        Check.Close(weibull.Parameters[0].StandardError,
            Math.Sqrt(0.6079 * beta * beta / nw), "Weibull SE(shape) matches the asymptotic form", 0.02);
        Check.Close(weibull.Parameters[1].StandardError,
            Math.Sqrt(1.1087 * eta * eta / (nw * beta * beta)), "Weibull SE(scale) matches the asymptotic form", 0.02);

        // Intervals must bracket the estimate, and a positive parameter can never get a negative bound.
        foreach (var p in weibull.Parameters)
        {
            Check.True(p.CiLow < p.Value && p.Value < p.CiHigh, $"Weibull {p.Name} interval brackets the estimate");
            Check.True(p.CiLow > 0, $"Weibull {p.Name} lower bound stays positive");
        }

        // Percentiles carry delta-method uncertainty, and a wider interval at 99% than at 95%.
        var p10 = weibull.Percentiles.First(p => p.Percent == 10);
        Check.True(p10.StandardError > 0 && p10.CiLow < p10.Value && p10.Value < p10.CiHigh,
            "10th percentile has a bracketing delta-method interval");
        var wide = Reliability.FitWeibull(weibullData, null, 0.99);
        Check.True(wide.Parameters[0].CiHigh - wide.Parameters[0].CiLow >
                   weibull.Parameters[0].CiHigh - weibull.Parameters[0].CiLow,
            "a 99% interval is wider than a 95% interval");
        Check.Close(wide.Conf, 0.99, "requested confidence level is reported");

        // Censoring costs information, so the same data must yield a larger standard error.
        var censorFlags = weibullData.Select(v => v > 900).ToArray();
        var censoredFit = Reliability.FitWeibull(weibullData, censorFlags);
        Check.True(censoredFit.Parameters[1].StandardError > weibull.Parameters[1].StandardError,
            "censoring widens the scale standard error");

        Check.Throws<ArgumentOutOfRangeException>(
            () => Reliability.FitWeibull(weibullData, null, 1.5), "invalid confidence level rejected");

        // ---- distribution characteristics ----------------------------------
        // These are functions of the parameters, so several reduce to an identity that must hold
        // exactly if the delta-method plumbing is right.

        // Normal: mean IS mu and StDev IS sigma, so both must reproduce the parameter intervals.
        Check.Close(normal.Mean.StandardError, normal.Parameters[0].StandardError,
            "normal mean SE equals SE(mu)", 1e-9);
        Check.Close(normal.Mean.CiLow, normal.Parameters[0].CiLow, "normal mean CI equals mu's", 1e-9);
        Check.Close(normal.StDev.StandardError, normal.Parameters[1].StandardError,
            "normal StDev SE equals SE(sigma)", 1e-9);
        Check.Close(normal.StDev.CiHigh, normal.Parameters[1].CiHigh, "normal StDev CI equals sigma's", 1e-9);
        Check.Close(normal.Median.Value, normal.Mean.Value, "normal median equals its mean", 1e-9);

        // Exponential: mean IS the scale; the median is that scale times ln 2, so the SE scales too.
        Check.Close(exponential.Mean.StandardError, exponential.Parameters[0].StandardError,
            "exponential mean SE equals the scale's", 1e-9);
        Check.Close(exponential.Median.StandardError, exponential.Mean.StandardError * Math.Log(2),
            "exponential median SE scales by ln 2", 1e-6);

        // Weibull: the median is by definition the 50th percentile, computed by a separate path.
        var p50 = weibull.Percentiles.First(p => p.Percent == 50);
        Check.Close(weibull.Median.Value, p50.Value, "Weibull median equals the 50th percentile", 1e-9);
        Check.Close(weibull.Median.StandardError, p50.StandardError,
            "Weibull median SE equals the 50th percentile's", 1e-6);

        // Every characteristic of a positive-support distribution must bracket and stay positive.
        foreach (var ch in weibull.Characteristics)
        {
            Check.True(ch.CiLow < ch.Value && ch.Value < ch.CiHigh, $"Weibull {ch.Name} interval brackets it");
            Check.True(ch.CiLow > 0, $"Weibull {ch.Name} lower bound stays positive");
        }
        foreach (var ch in Reliability.FitLognormal(weibullData).Characteristics)
            Check.True(ch.StandardError > 0 && ch.CiLow < ch.Value && ch.Value < ch.CiHigh,
                $"lognormal {ch.Name} has a bracketing interval");

        // Censoring must widen the mean interval, as it does the parameters'.
        Check.True(censoredFit.Mean.StandardError > weibull.Mean.StandardError,
            "censoring widens the mean standard error");
    }

    public static void Run()
    {
        CensoredLifeData();
        StandardErrorsAndIntervals();

        Check.Section("Factor analysis — 1 factor on a correlated pair");
        var data = new[]
        {
            new double[] { 1, 2 }, new double[] { 2, 4 }, new double[] { 3, 6 },
            new double[] { 4, 8 }, new double[] { 5, 10 },
        };
        var fa = FactorAnalysis.Extract(data, new[] { "x1", "x2" }, 1);
        Check.Close(Math.Abs(fa.Loadings[0, 0]), 1.0, "|loading x1| = 1", 1e-6);
        Check.Close(Math.Abs(fa.Loadings[1, 0]), 1.0, "|loading x2| = 1", 1e-6);
        Check.Close(fa.Communalities[0], 1.0, "communality x1 = 1", 1e-6);
        Check.Close(fa.VarianceExplained[0], 2.0, "variance explained = 2", 1e-6);

        Check.Section("Factor analysis — varimax preserves communalities");
        var d3 = new double[8][];
        for (int i = 0; i < 8; i++)
            d3[i] = new double[] { i + 1, 2 * (i + 1), 9 - i + (i % 2) };
        var unrot = FactorAnalysis.Extract(d3, new[] { "a", "b", "c" }, 2, rotate: false);
        var rot = FactorAnalysis.Extract(d3, new[] { "a", "b", "c" }, 2, rotate: true);
        Check.Close(rot.Communalities.Sum(), unrot.Communalities.Sum(), "Σ communalities rotation-invariant", 1e-6);
        Check.Close(rot.VarianceExplained.Sum(), unrot.VarianceExplained.Sum(), "total variance preserved", 1e-6);

        Check.Section("Reliability — exponential / normal / lognormal MLE");
        var ex = Reliability.FitExponential(new double[] { 1, 2, 3, 4, 5 });
        Check.Close(ex.Mean.Value, 3.0, "exponential mean = 3");
        Check.Close(ex.Median.Value, 3 * Math.Log(2), "exponential median = mean·ln2", 1e-6);
        var no = Reliability.FitNormal(new double[] { 2, 4, 6, 8, 10 });
        Check.Close(no.Mean.Value, 6.0, "normal mean = 6");
        Check.Close(no.StDev.Value, Math.Sqrt(8), "normal sd (MLE) = sqrt(8)", 1e-6);
        var ln = Reliability.FitLognormal(new double[] { 1, Math.E, Math.E * Math.E });
        Check.Close(ln.Parameters.First(p => p.Name.Contains('μ')).Value, 1.0, "lognormal mu = 1", 1e-6);

        Check.Section("Reliability — Weibull MLE recovers β≈2, η≈100");
        int n = 30;
        var wt = new double[n];
        for (int i = 1; i <= n; i++) { double p = (i - 0.3) / (n + 0.4); wt[i - 1] = 100 * Math.Pow(-Math.Log(1 - p), 0.5); }
        var wb = Reliability.FitWeibull(wt);
        double beta = wb.Parameters.First(p => p.Name.Contains('β')).Value;
        double eta = wb.Parameters.First(p => p.Name.Contains('η')).Value;
        Check.Close(beta, 2.0, "Weibull shape ≈ 2", 0.15);
        Check.Close(eta, 100.0, "Weibull scale ≈ 100", 8.0);

        Check.Section("Kaplan-Meier survival");
        var km = Reliability.KaplanMeier(new double[] { 2, 3, 4, 5 }, new[] { false, false, false, false });
        Check.Close(km.Rows[0].Survival, 0.75, "S(2) = 0.75");
        Check.Close(km.Rows[1].Survival, 0.50, "S(3) = 0.50");
        Check.Close(km.MedianSurvival, 3.0, "median survival = 3");

        var kmc = Reliability.KaplanMeier(new double[] { 2, 3, 4, 5 }, new[] { false, true, false, false });
        Check.Close(kmc.Rows.First(r => r.Time == 4).Survival, 0.375, "censored: S(4) = 0.375", 1e-6);
    }
}
