using MathNet.Numerics;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Optimization;

namespace StatStudio.Core.Statistics;

/// <summary>A fitted parameter with its standard error and Wald confidence interval.</summary>
public sealed record ParameterEstimate(
    string Name, double Value, double StandardError, double CiLow, double CiHigh);

/// <summary>A distribution percentile with its delta-method standard error and interval.</summary>
public sealed record PercentileEstimate(
    double Percent, double Value, double StandardError, double CiLow, double CiHigh);

/// <summary>
/// A summary property of the fitted distribution (mean, standard deviation, median) with its
/// delta-method standard error and interval. These are functions of the fitted parameters, so
/// they carry the parameters' uncertainty.
/// </summary>
public sealed record DistributionCharacteristic(
    string Name, double Value, double StandardError, double CiLow, double CiHigh);

public sealed record DistributionFit(
    string Distribution, IReadOnlyList<ParameterEstimate> Parameters,
    DistributionCharacteristic Mean, DistributionCharacteristic StDev, DistributionCharacteristic Median,
    IReadOnlyList<PercentileEstimate> Percentiles, int N,
    int Failures, int RightCensored, double Conf)
{
    /// <summary>Mean, standard deviation and median in display order.</summary>
    public IReadOnlyList<DistributionCharacteristic> Characteristics => new[] { Mean, StDev, Median };
}

public sealed record KmRow(double Time, int AtRisk, int Failures, int Censored, double Survival);

public sealed record KaplanMeierResult(IReadOnlyList<KmRow> Rows, double MedianSurvival, int N, int Events);

/// <summary>Reliability / survival analysis: parametric life-data MLE and Kaplan-Meier.</summary>
public static class Reliability
{
    private static readonly double[] Pcts = { 1, 5, 10, 50, 90, 95, 99 };

    /// <summary>
    /// Exponential MLE. With right-censoring the estimator is the total time on test divided by
    /// the number of failures — censored units contribute their run time but not a failure.
    /// </summary>
    public static DistributionFit FitExponential(double[] t, bool[]? censored = null, double conf = 0.95, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var c = RequireLifeData(t, censored, positive: true, minimumFailures: 1);
        StatGuard.Probability(conf, nameof(conf));
        double mean = t.Sum() / c.Failures;

        var theta = new[] { mean };
        var (se, low, high, cov) = Uncertainty(v => ExponentialLogLikelihood(v, t, c), theta,
            new[] { true }, conf);

        // Rate = 1/mean, so its interval is the reciprocal of the mean's, with the ends swapped.
        var parameters = new List<ParameterEstimate>
        {
            new("Mean (scale)", mean, se[0], low[0], high[0]),
            new("Rate", 1 / mean, se[0] / (mean * mean), 1 / high[0], 1 / low[0]),
        };
        var pct = Pcts.Select(p => Percentile(p,
            v => -v[0] * Math.Log(1 - p / 100), theta, cov, positiveSupport: true, conf)).ToList();

        // For the exponential the mean and standard deviation are both the scale itself, so these
        // intervals coincide with the parameter's by construction.
        var meanC = Characteristic("Mean", v => v[0], theta, cov, true, conf);
        var sdC = Characteristic("StDev", v => v[0], theta, cov, true, conf);
        var medianC = Characteristic("Median", v => v[0] * Math.Log(2), theta, cov, true, conf);

        return new DistributionFit("Exponential", parameters,
            meanC, sdC, medianC, pct, t.Length, c.Failures, c.Censored, conf);
    }

    /// <summary>
    /// Weibull MLE, supporting right-censoring. Censored units enter the Σtᶜ terms (they survived
    /// that long) but not the failure count or the Σln t term, which is exactly what separates the
    /// censored likelihood from the complete-data one.
    /// </summary>
    public static DistributionFit FitWeibull(double[] t, bool[]? censored = null, double conf = 0.95, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var c = RequireLifeData(t, censored, positive: true, minimumFailures: 2);
        StatGuard.Probability(conf, nameof(conf));
        int n = t.Length;
        int r = c.Failures;

        // Work on lifetimes divided by their geometric mean. Raw data in the thousands overflows
        // Math.Pow(x, b) to +Infinity at large trial shapes, which makes the likelihood derivative
        // NaN; the bisection then never takes its lower branch and silently returns beta ~ 0.001.
        // Rescaling puts every value near 1, and eta is un-scaled at the end.
        double geometricMean = Math.Exp(t.Average(x => Math.Log(x)));
        var z = t.Select(x => x / geometricMean).ToArray();

        // Mean of ln(z) over the FAILURES only. With no censoring this is 0 by construction and
        // the equation collapses to the classic complete-data form.
        double meanLnFailures = 0;
        for (int i = 0; i < n; i++) if (!c.IsCensored[i]) meanLnFailures += Math.Log(z[i]);
        meanLnFailures /= r;

        double Shape(double b)
        {
            cancellationToken.ThrowIfCancellationRequested();
            double sw = 0, swl = 0;
            for (int i = 0; i < n; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                double w = Math.Pow(z[i], b);   // every unit contributes, censored or not
                sw += w;
                swl += w * Math.Log(z[i]);
            }
            return swl / sw - 1.0 / b - meanLnFailures;
        }

        double lo = 1e-3, hi = 100;
        double fLo = Shape(lo), fHi = Shape(hi);
        if (!double.IsFinite(fLo) || !double.IsFinite(fHi) || fLo > 0 || fHi < 0)
            throw new ArgumentException(
                "The Weibull shape parameter could not be bracketed for this data.", nameof(t));

        for (int i = 0; i < 100; i++) { double mid = 0.5 * (lo + hi); if (Shape(mid) < 0) lo = mid; else hi = mid; }
        double beta = 0.5 * (lo + hi);
        // Scale is normalised by the failure count r, not n — the censored-data MLE.
        double eta = geometricMean * Math.Pow(z.Sum(x => Math.Pow(x, beta)) / r, 1.0 / beta);

        var theta = new[] { beta, eta };
        var (se, low, high, cov) = Uncertainty(v => WeibullLogLikelihood(v, t, c), theta,
            new[] { true, true }, conf);
        var parameters = new List<ParameterEstimate>
        {
            new("Shape (β)", beta, se[0], low[0], high[0]),
            new("Scale (η)", eta, se[1], low[1], high[1]),
        };
        var pct = Pcts.Select(p => Percentile(p,
            v => v[1] * Math.Pow(-Math.Log(1 - p / 100), 1.0 / v[0]), theta, cov,
            positiveSupport: true, conf)).ToList();

        // Mean = η·Γ(1+1/β), SD = η·√(Γ(1+2/β) − Γ(1+1/β)²), median = η·(ln 2)^(1/β) — all
        // functions of both parameters, so both sources of uncertainty propagate.
        var meanC = Characteristic("Mean",
            v => v[1] * SpecialFunctions.Gamma(1 + 1.0 / v[0]), theta, cov, true, conf);
        var sdC = Characteristic("StDev",
            v => v[1] * Math.Sqrt(Math.Max(0, SpecialFunctions.Gamma(1 + 2.0 / v[0])
                                              - Math.Pow(SpecialFunctions.Gamma(1 + 1.0 / v[0]), 2))),
            theta, cov, true, conf);
        var medianC = Characteristic("Median",
            v => v[1] * Math.Pow(Math.Log(2), 1.0 / v[0]), theta, cov, true, conf);

        return new DistributionFit("Weibull", parameters,
            meanC, sdC, medianC, pct, n, r, c.Censored, conf);
    }

    public static DistributionFit FitLognormal(double[] t, bool[]? censored = null, double conf = 0.95, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var c = RequireLifeData(t, censored, positive: true, minimumFailures: 2);
        StatGuard.Probability(conf, nameof(conf));
        var logs = t.Select(x => Math.Log(x)).ToArray();
        var (mu, sigma) = GaussianMle(logs, c, cancellationToken);

        var theta = new[] { mu, sigma };
        // μ is a log-scale location and may be any sign, so its interval stays symmetric.
        var (se, low, high, cov) = Uncertainty(v => GaussianLogLikelihood(v, logs, c), theta,
            new[] { false, true }, conf);
        var parameters = new List<ParameterEstimate>
        {
            new("Location (μ)", mu, se[0], low[0], high[0]),
            new("Scale (σ)", sigma, se[1], low[1], high[1]),
        };
        var pct = Pcts.Select(p => Percentile(p,
            v => Math.Exp(v[0] + v[1] * Normal.InvCDF(0, 1, p / 100)), theta, cov,
            positiveSupport: true, conf)).ToList();

        var meanC = Characteristic("Mean",
            v => Math.Exp(v[0] + v[1] * v[1] / 2), theta, cov, true, conf);
        var sdC = Characteristic("StDev",
            v => Math.Sqrt((Math.Exp(v[1] * v[1]) - 1) * Math.Exp(2 * v[0] + v[1] * v[1])),
            theta, cov, true, conf);
        var medianC = Characteristic("Median", v => Math.Exp(v[0]), theta, cov, true, conf);

        return new DistributionFit("Lognormal", parameters,
            meanC, sdC, medianC, pct, t.Length, c.Failures, c.Censored, conf);
    }

    public static DistributionFit FitNormal(double[] t, bool[]? censored = null, double conf = 0.95, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var c = RequireLifeData(t, censored, positive: true, minimumFailures: 2);
        StatGuard.Probability(conf, nameof(conf));
        var (mu, sigma) = GaussianMle(t, c, cancellationToken);

        var theta = new[] { mu, sigma };
        var (se, low, high, cov) = Uncertainty(v => GaussianLogLikelihood(v, t, c), theta,
            new[] { false, true }, conf);
        var parameters = new List<ParameterEstimate>
        {
            new("Mean (μ)", mu, se[0], low[0], high[0]),
            new("StDev (σ)", sigma, se[1], low[1], high[1]),
        };
        // A normal percentile is not constrained positive, so its interval stays symmetric too.
        var pct = Pcts.Select(p => Percentile(p,
            v => v[0] + v[1] * Normal.InvCDF(0, 1, p / 100), theta, cov,
            positiveSupport: false, conf)).ToList();

        // A normal mean and median are unrestricted in sign, so their intervals stay symmetric;
        // the standard deviation is positive and takes the log-scale interval.
        var meanC = Characteristic("Mean", v => v[0], theta, cov, false, conf);
        var sdC = Characteristic("StDev", v => v[1], theta, cov, true, conf);
        var medianC = Characteristic("Median", v => v[0], theta, cov, false, conf);

        return new DistributionFit("Normal", parameters,
            meanC, sdC, medianC, pct, t.Length, c.Failures, c.Censored, conf);
    }

    /// <summary>
    /// Location/scale MLE for a normal sample with right-censoring, on whatever scale the caller
    /// supplies (raw values for the normal fit, logs for the lognormal).
    /// </summary>
    /// <remarks>
    /// Uncensored data has the familiar closed form, so that path is taken directly. With censoring
    /// the log-likelihood
    ///     Σ_failures [ln φ((x−μ)/σ) − ln σ]  +  Σ_censored ln(1 − Φ((x−μ)/σ))
    /// has no closed-form maximiser, so it is minimised numerically. σ is optimised as ln σ to keep
    /// it positive without a constraint, and the complete-data estimates seed the search.
    /// </remarks>
    private static (double Mu, double Sigma) GaussianMle(double[] x, Censoring c, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        double mu0 = x.Average();
        double sigma0 = Math.Sqrt(x.Average(v => (v - mu0) * (v - mu0)));
        if (sigma0 <= 0)
            throw new ArgumentException("The sample must contain variation.", nameof(x));
        if (c.Censored == 0) return (mu0, sigma0);

        double NegativeLogLikelihood(double mu, double logSigma)
        {
            double sigma = Math.Exp(logSigma);
            double total = 0;
            for (int i = 0; i < x.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                double zi = (x[i] - mu) / sigma;
                if (c.IsCensored[i])
                {
                    // Survival above the censoring time. Clamped so a far-tail unit cannot
                    // produce log(0) = -infinity and stall the simplex.
                    double survival = Math.Max(1e-300, 1 - Normal.CDF(0, 1, zi));
                    total -= Math.Log(survival);
                }
                else
                {
                    total += 0.5 * zi * zi + logSigma;   // constants dropped; they do not move the optimum
                }
            }
            return double.IsFinite(total) ? total : double.MaxValue;
        }

        try
        {
            var objective = ObjectiveFunction.Value(v => NegativeLogLikelihood(v[0], v[1]));
            var solver = new NelderMeadSimplex(1e-10, 2000);
            var start = Vector<double>.Build.DenseOfArray(new[] { mu0, Math.Log(sigma0) });
            var best = solver.FindMinimum(objective, start).MinimizingPoint;
            double mu = best[0], sigma = Math.Exp(best[1]);
            if (!double.IsFinite(mu) || !double.IsFinite(sigma) || sigma <= 0)
                throw new ArgumentException("The censored likelihood did not converge to a usable fit.", nameof(x));
            return (mu, sigma);
        }
        catch (MaximumIterationsException)
        {
            throw new ArgumentException(
                "The censored maximum-likelihood fit did not converge for this data.", nameof(x));
        }
    }

    /// <summary>Kaplan-Meier survival estimate. <paramref name="censored"/>[i] = true means right-censored.</summary>
    public static KaplanMeierResult KaplanMeier(double[] times, bool[] censored, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(times);
        ArgumentNullException.ThrowIfNull(censored);
        if (times.Length == 0)
            throw new ArgumentException("At least one survival time is required.", nameof(times));
        if (times.Length != censored.Length)
            throw new ArgumentException("Survival times and censoring indicators must have equal lengths.");
        StatGuard.Finite(times, nameof(times));
        if (times.Any(t => t < 0))
            throw new ArgumentOutOfRangeException(nameof(times), "Survival times must be nonnegative.");

        int n = times.Length;
        var observations = times.Zip(censored)
            .OrderBy(pair => pair.First)
            .ToArray();

        var rows = new List<KmRow>();
        double s = 1.0;
        double median = double.NaN;
        int events = 0;
        int atRisk = n;
        int index = 0;
        while (index < observations.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            double time = observations[index].First;
            int fails = 0, cens = 0;
            while (index < observations.Length && observations[index].First == time)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (observations[index].Second) cens++;
                else fails++;
                index++;
            }

            events += fails;
            if (fails > 0) s *= 1.0 - (double)fails / atRisk;
            if (double.IsNaN(median) && s <= 0.5) median = time;
            rows.Add(new KmRow(time, atRisk, fails, cens, s));
            atRisk -= fails + cens;
        }
        return new KaplanMeierResult(rows, median, n, events);
    }

    // ---- uncertainty -------------------------------------------------------

    /// <summary>
    /// Standard errors and Wald intervals from the observed Fisher information — the negated
    /// Hessian of the log-likelihood at the MLE, inverted to a covariance matrix.
    /// </summary>
    /// <remarks>
    /// The Hessian is taken numerically by central differences rather than derived per
    /// distribution: one tested code path covers all four fits, censored or not, and it stays
    /// correct if a likelihood is ever changed. Parameters constrained positive (shape, scale, σ)
    /// get their interval on the log scale — θ·exp(±z·SE/θ) — which cannot produce a negative
    /// bound and has better small-sample coverage than a symmetric interval.
    /// </remarks>
    private static (double[] Se, double[] Low, double[] High, Matrix<double>? Covariance) Uncertainty(
        Func<double[], double> logLikelihood, double[] theta, bool[] positive, double conf)
    {
        int p = theta.Length;
        var se = new double[p];
        var low = new double[p];
        var high = new double[p];
        Array.Fill(se, double.NaN);
        Array.Fill(low, double.NaN);
        Array.Fill(high, double.NaN);

        Matrix<double>? covariance = null;
        try
        {
            var information = -Hessian(logLikelihood, theta);
            var cov = information.Inverse();
            if (cov.Enumerate().All(double.IsFinite))
            {
                covariance = cov;
                double z = Normal.InvCDF(0, 1, 1 - (1 - conf) / 2);
                for (int i = 0; i < p; i++)
                {
                    double variance = cov[i, i];
                    if (!double.IsFinite(variance) || variance < 0) continue;
                    se[i] = Math.Sqrt(variance);
                    if (positive[i] && theta[i] > 0)
                    {
                        double factor = Math.Exp(z * se[i] / theta[i]);
                        low[i] = theta[i] / factor;
                        high[i] = theta[i] * factor;
                    }
                    else
                    {
                        low[i] = theta[i] - z * se[i];
                        high[i] = theta[i] + z * se[i];
                    }
                }
            }
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException)
        {
            // A singular or indefinite information matrix means the data cannot pin the
            // parameters down; report "*" rather than a fabricated interval.
        }
        return (se, low, high, covariance);
    }

    /// <summary>
    /// Delta method for any smooth function of the fitted parameters: Var(g(θ)) = ∇g' Σ ∇g.
    /// The gradient is numerical, so percentiles and distribution characteristics alike need no
    /// hand-derived derivatives.
    /// </summary>
    private static (double Value, double Se, double Low, double High) DeltaMethod(
        Func<double[], double> g, double[] theta, Matrix<double>? covariance,
        bool positiveSupport, double conf)
    {
        double value = g(theta);
        if (covariance is null || !double.IsFinite(value))
            return (value, double.NaN, double.NaN, double.NaN);

        int p = theta.Length;
        var gradient = Vector<double>.Build.Dense(p);
        for (int i = 0; i < p; i++)
        {
            double step = Step(theta[i]);
            var up = (double[])theta.Clone(); up[i] += step;
            var down = (double[])theta.Clone(); down[i] -= step;
            gradient[i] = (g(up) - g(down)) / (2 * step);
        }

        double variance = gradient * (covariance * gradient);
        if (!double.IsFinite(variance) || variance < 0)
            return (value, double.NaN, double.NaN, double.NaN);

        double se = Math.Sqrt(variance);
        double z = Normal.InvCDF(0, 1, 1 - (1 - conf) / 2);
        if (positiveSupport && value > 0)
        {
            double factor = Math.Exp(z * se / value);
            return (value, se, value / factor, value * factor);
        }
        return (value, se, value - z * se, value + z * se);
    }

    private static PercentileEstimate Percentile(
        double percent, Func<double[], double> quantile, double[] theta,
        Matrix<double>? covariance, bool positiveSupport, double conf)
    {
        var (value, se, low, high) = DeltaMethod(quantile, theta, covariance, positiveSupport, conf);
        return new PercentileEstimate(percent, value, se, low, high);
    }

    private static DistributionCharacteristic Characteristic(
        string name, Func<double[], double> g, double[] theta,
        Matrix<double>? covariance, bool positiveSupport, double conf)
    {
        var (value, se, low, high) = DeltaMethod(g, theta, covariance, positiveSupport, conf);
        return new DistributionCharacteristic(name, value, se, low, high);
    }

    /// <summary>Relative step for central differences, floored so parameters near zero still move.</summary>
    private static double Step(double value) => Math.Max(Math.Abs(value), 1.0) * 1e-4;

    private static Matrix<double> Hessian(Func<double[], double> f, double[] theta)
    {
        int p = theta.Length;
        var h = theta.Select(Step).ToArray();
        var hessian = Matrix<double>.Build.Dense(p, p);
        double centre = f(theta);

        for (int i = 0; i < p; i++)
            for (int j = i; j < p; j++)
            {
                double value;
                if (i == j)
                {
                    var up = (double[])theta.Clone(); up[i] += h[i];
                    var down = (double[])theta.Clone(); down[i] -= h[i];
                    value = (f(up) - 2 * centre + f(down)) / (h[i] * h[i]);
                }
                else
                {
                    var pp = (double[])theta.Clone(); pp[i] += h[i]; pp[j] += h[j];
                    var pm = (double[])theta.Clone(); pm[i] += h[i]; pm[j] -= h[j];
                    var mp = (double[])theta.Clone(); mp[i] -= h[i]; mp[j] += h[j];
                    var mm = (double[])theta.Clone(); mm[i] -= h[i]; mm[j] -= h[j];
                    value = (f(pp) - f(pm) - f(mp) + f(mm)) / (4 * h[i] * h[j]);
                }
                hessian[i, j] = value;
                hessian[j, i] = value;
            }
        return hessian;
    }

    // ---- log-likelihoods (additive constants dropped; they vanish under differentiation) ----

    private static double WeibullLogLikelihood(double[] theta, double[] t, Censoring c)
    {
        double beta = theta[0], eta = theta[1];
        if (beta <= 0 || eta <= 0) return double.NegativeInfinity;
        double total = 0;
        for (int i = 0; i < t.Length; i++)
        {
            if (!c.IsCensored[i])
                total += Math.Log(beta) - beta * Math.Log(eta) + (beta - 1) * Math.Log(t[i]);
            total -= Math.Pow(t[i] / eta, beta);
        }
        return total;
    }

    private static double ExponentialLogLikelihood(double[] theta, double[] t, Censoring c)
    {
        double mean = theta[0];
        if (mean <= 0) return double.NegativeInfinity;
        double total = 0;
        for (int i = 0; i < t.Length; i++)
        {
            if (!c.IsCensored[i]) total -= Math.Log(mean);
            total -= t[i] / mean;
        }
        return total;
    }

    /// <summary>Normal log-likelihood on the supplied scale (raw values, or logs for lognormal).</summary>
    private static double GaussianLogLikelihood(double[] theta, double[] x, Censoring c)
    {
        double mu = theta[0], sigma = theta[1];
        if (sigma <= 0) return double.NegativeInfinity;
        double total = 0;
        for (int i = 0; i < x.Length; i++)
        {
            double z = (x[i] - mu) / sigma;
            if (c.IsCensored[i])
                total += Math.Log(Math.Max(1e-300, 1 - Normal.CDF(0, 1, z)));
            else
                total += -Math.Log(sigma) - 0.5 * z * z;
        }
        return total;
    }

    /// <summary>Validated censoring layout: which observations are censored, and the two counts.</summary>
    private readonly record struct Censoring(bool[] IsCensored, int Failures, int Censored);

    private static Censoring RequireLifeData(double[] values, bool[]? censored, bool positive, int minimumFailures)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length == 0)
            throw new ArgumentException("At least one lifetime observation is required.", nameof(values));
        StatGuard.Finite(values, nameof(values));
        if (positive && values.Any(v => v <= 0))
            throw new ArgumentOutOfRangeException(nameof(values), "This distribution requires strictly positive lifetimes.");

        var flags = censored ?? new bool[values.Length];
        if (flags.Length != values.Length)
            throw new ArgumentException("Lifetimes and censoring indicators must have equal lengths.", nameof(censored));

        int censoredCount = flags.Count(f => f);
        int failures = values.Length - censoredCount;
        // Censored units bound a lifetime from below but never pin one down; the likelihood has no
        // maximum without failures to locate it.
        if (failures < minimumFailures)
            throw new ArgumentException(
                $"At least {minimumFailures} uncensored failure(s) are required to fit this distribution " +
                $"({failures} of {values.Length} observations are failures).", nameof(censored));

        return new Censoring(flags, failures, censoredCount);
    }
}
