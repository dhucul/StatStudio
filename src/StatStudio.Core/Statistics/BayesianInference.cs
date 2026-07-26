using MathNet.Numerics.Distributions;

namespace StatStudio.Core.Statistics;

public sealed record BayesProportionResult(
    double PriorA, double PriorB, int X, int N, double PostA, double PostB,
    double Mean, double Mode, double Sd, double CredLow, double CredHigh, double Conf,
    double ProbAboveThreshold, double Threshold);

public sealed record BayesNormalMeanResult(
    string Method, double PosteriorMean, double PosteriorSd, double Df,
    double CredLow, double CredHigh, double Conf, double ProbAboveThreshold, double Threshold);

public sealed record BayesRegressionTerm(
    string Name, double PosteriorMean, double PosteriorSd, double CredLow, double CredHigh, double ProbPositive);

public sealed record BayesRegressionResult(
    string Response, IReadOnlyList<string> Predictors, IReadOnlyList<BayesRegressionTerm> Terms,
    double Sigma, double Df, double Conf);

/// <summary>Closed-form (conjugate / reference-prior) Bayesian inference.</summary>
public static class Bayes
{
    /// <summary>Beta-Binomial: prior Beta(a,b) + x successes in n → posterior Beta(a+x, b+n−x).</summary>
    public static BayesProportionResult Proportion(int x, int n, double priorA = 1, double priorB = 1,
        double conf = 0.95, double threshold = 0.5)
    {
        if (n <= 0 || x < 0 || x > n)
            throw new ArgumentOutOfRangeException(nameof(x), "Successes must be between zero and a positive number of trials.");
        if (!double.IsFinite(priorA) || priorA <= 0)
            throw new ArgumentOutOfRangeException(nameof(priorA), "The beta prior parameters must be positive and finite.");
        if (!double.IsFinite(priorB) || priorB <= 0)
            throw new ArgumentOutOfRangeException(nameof(priorB), "The beta prior parameters must be positive and finite.");
        StatGuard.Probability(conf, nameof(conf));
        StatGuard.UnitInterval(threshold, nameof(threshold));

        double pa = priorA + x, pb = priorB + (n - x);
        var beta = new Beta(pa, pb);
        double mean = pa / (pa + pb);
        double mode = (pa > 1 && pb > 1) ? (pa - 1) / (pa + pb - 2) : double.NaN;
        double sd = Math.Sqrt(pa * pb / ((pa + pb) * (pa + pb) * (pa + pb + 1)));
        double alpha = 1 - conf;
        double lo = beta.InverseCumulativeDistribution(alpha / 2);
        double hi = beta.InverseCumulativeDistribution(1 - alpha / 2);
        double pgt = 1 - beta.CumulativeDistribution(threshold);
        return new BayesProportionResult(priorA, priorB, x, n, pa, pb, mean, mode, sd, lo, hi, conf, pgt, threshold);
    }

    /// <summary>Normal mean with known variance: Normal(μ0, τ0²) prior → Normal posterior.</summary>
    public static BayesNormalMeanResult NormalMeanKnownVar(double[] data, double priorMean, double priorSd,
        double knownSigma, double conf = 0.95, double threshold = 0)
    {
        RequireNormalData(data, 1);
        if (!double.IsFinite(priorMean))
            throw new ArgumentOutOfRangeException(nameof(priorMean), "The prior mean must be finite.");
        if (!double.IsFinite(priorSd) || priorSd <= 0)
            throw new ArgumentOutOfRangeException(nameof(priorSd), "The prior standard deviation must be positive and finite.");
        if (!double.IsFinite(knownSigma) || knownSigma <= 0)
            throw new ArgumentOutOfRangeException(nameof(knownSigma), "The known standard deviation must be positive and finite.");
        StatGuard.Probability(conf, nameof(conf));
        if (!double.IsFinite(threshold))
            throw new ArgumentOutOfRangeException(nameof(threshold), "The threshold must be finite.");

        int n = data.Length;
        double xbar = data.Average();
        double priorPrec = 1 / (priorSd * priorSd);
        double dataPrec = n / (knownSigma * knownSigma);
        double postVar = 1 / (priorPrec + dataPrec);
        double postMean = (priorMean * priorPrec + xbar * dataPrec) * postVar;
        double postSd = Math.Sqrt(postVar);
        double z = Normal.InvCDF(0, 1, 1 - (1 - conf) / 2);
        double pgt = 1 - Normal.CDF(postMean, postSd, threshold);
        return new BayesNormalMeanResult("Normal mean (known σ)", postMean, postSd, double.PositiveInfinity,
            postMean - z * postSd, postMean + z * postSd, conf, pgt, threshold);
    }

    /// <summary>Normal mean with unknown variance, Jeffreys prior → Student-t posterior for μ.</summary>
    public static BayesNormalMeanResult NormalMeanUnknownVar(double[] data, double conf = 0.95, double threshold = 0)
    {
        RequireNormalData(data, 2);
        StatGuard.Probability(conf, nameof(conf));
        if (!double.IsFinite(threshold))
            throw new ArgumentOutOfRangeException(nameof(threshold), "The threshold must be finite.");

        int n = data.Length;
        double xbar = data.Average();
        double s = Math.Sqrt(data.Sum(v => (v - xbar) * (v - xbar)) / (n - 1));
        if (s <= 0)
            throw new ArgumentException("The sample must contain variation.", nameof(data));
        double scale = s / Math.Sqrt(n);
        double df = n - 1;
        var t = new StudentT(0, 1, df);
        double tc = t.InverseCumulativeDistribution(1 - (1 - conf) / 2);
        double pgt = scale > 0 ? 1 - t.CumulativeDistribution((threshold - xbar) / scale) : double.NaN;
        return new BayesNormalMeanResult("Normal mean (Jeffreys prior)", xbar, scale, df,
            xbar - tc * scale, xbar + tc * scale, conf, pgt, threshold);
    }

    /// <summary>Bayesian linear regression with the reference prior p(β,σ²)∝1/σ² → t posterior centered at OLS.</summary>
    public static BayesRegressionResult LinearRegression(double[] y, double[][] predictors,
        IReadOnlyList<string> predictorNames, string response = "Y", double conf = 0.95)
    {
        StatGuard.Probability(conf, nameof(conf));
        var reg = Regression.Fit(y, predictors, predictorNames, response);
        if (reg.DfError <= 0)
            throw new ArgumentException("Bayesian regression requires positive residual degrees of freedom.", nameof(y));
        var t = new StudentT(0, 1, reg.DfError);
        double tc = t.InverseCumulativeDistribution(1 - (1 - conf) / 2);

        var terms = new List<BayesRegressionTerm>();
        foreach (var term in reg.Terms)
        {
            double se = term.SeCoef;
            double probPos = se > 0 ? t.CumulativeDistribution(term.Coef / se) : double.NaN;
            terms.Add(new BayesRegressionTerm(term.Name, term.Coef, se,
                term.Coef - tc * se, term.Coef + tc * se, probPos));
        }
        return new BayesRegressionResult(response, predictorNames, terms, reg.S, reg.DfError, conf);
    }

    private static void RequireNormalData(double[] data, int minimum)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length < minimum)
            throw new ArgumentException($"At least {minimum} observation(s) are required.", nameof(data));
        StatGuard.Finite(data, nameof(data));
    }
}
