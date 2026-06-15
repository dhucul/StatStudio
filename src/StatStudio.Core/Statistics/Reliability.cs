using MathNet.Numerics;
using MathNet.Numerics.Distributions;

namespace StatStudio.Core.Statistics;

public sealed record DistributionFit(
    string Distribution, IReadOnlyList<(string Name, double Value)> Parameters,
    double Mean, double StDev, double Median,
    IReadOnlyList<(double Percent, double Value)> Percentiles, int N);

public sealed record KmRow(double Time, int AtRisk, int Failures, int Censored, double Survival);

public sealed record KaplanMeierResult(IReadOnlyList<KmRow> Rows, double MedianSurvival, int N, int Events);

/// <summary>Reliability / survival analysis: parametric life-data MLE and Kaplan-Meier.</summary>
public static class Reliability
{
    private static readonly double[] Pcts = { 1, 5, 10, 50, 90, 95, 99 };

    public static DistributionFit FitExponential(double[] t)
    {
        double mean = t.Average();                       // scale = MTTF
        var pct = Pcts.Select(p => (p, -mean * Math.Log(1 - p / 100))).ToList();
        return new DistributionFit("Exponential",
            new[] { ("Mean (scale)", mean), ("Rate", 1 / mean) },
            mean, mean, mean * Math.Log(2), pct, t.Length);
    }

    public static DistributionFit FitWeibull(double[] t)
    {
        int n = t.Length;
        double meanLn = t.Average(x => Math.Log(x));
        double Shape(double b)
        {
            double sw = 0, swl = 0;
            foreach (var x in t) { double w = Math.Pow(x, b); sw += w; swl += w * Math.Log(x); }
            return swl / sw - 1.0 / b - meanLn;
        }
        double lo = 1e-3, hi = 100;
        for (int i = 0; i < 100; i++) { double mid = 0.5 * (lo + hi); if (Shape(mid) < 0) lo = mid; else hi = mid; }
        double beta = 0.5 * (lo + hi);
        double eta = Math.Pow(t.Sum(x => Math.Pow(x, beta)) / n, 1.0 / beta);

        double mean = eta * SpecialFunctions.Gamma(1 + 1.0 / beta);
        double var = eta * eta * (SpecialFunctions.Gamma(1 + 2.0 / beta) - Math.Pow(SpecialFunctions.Gamma(1 + 1.0 / beta), 2));
        var pct = Pcts.Select(p => (p, eta * Math.Pow(-Math.Log(1 - p / 100), 1.0 / beta))).ToList();
        return new DistributionFit("Weibull",
            new[] { ("Shape (β)", beta), ("Scale (η)", eta) },
            mean, Math.Sqrt(var), eta * Math.Pow(Math.Log(2), 1.0 / beta), pct, n);
    }

    public static DistributionFit FitLognormal(double[] t)
    {
        var logs = t.Select(x => Math.Log(x)).ToArray();
        double mu = logs.Average();
        double sigma = Math.Sqrt(logs.Average(v => (v - mu) * (v - mu)));
        double mean = Math.Exp(mu + sigma * sigma / 2);
        double sd = Math.Sqrt((Math.Exp(sigma * sigma) - 1) * Math.Exp(2 * mu + sigma * sigma));
        var pct = Pcts.Select(p => (p, Math.Exp(mu + sigma * Normal.InvCDF(0, 1, p / 100)))).ToList();
        return new DistributionFit("Lognormal",
            new[] { ("Location (μ)", mu), ("Scale (σ)", sigma) },
            mean, sd, Math.Exp(mu), pct, t.Length);
    }

    public static DistributionFit FitNormal(double[] t)
    {
        double mu = t.Average();
        double sigma = Math.Sqrt(t.Average(v => (v - mu) * (v - mu)));
        var pct = Pcts.Select(p => (p, mu + sigma * Normal.InvCDF(0, 1, p / 100))).ToList();
        return new DistributionFit("Normal",
            new[] { ("Mean (μ)", mu), ("StDev (σ)", sigma) },
            mu, sigma, mu, pct, t.Length);
    }

    /// <summary>Kaplan-Meier survival estimate. <paramref name="censored"/>[i] = true means right-censored.</summary>
    public static KaplanMeierResult KaplanMeier(double[] times, bool[] censored)
    {
        int n = times.Length;
        var order = Enumerable.Range(0, n).OrderBy(i => times[i]).ToArray();
        var distinct = times.Distinct().OrderBy(v => v).ToArray();

        var rows = new List<KmRow>();
        double s = 1.0;
        double median = double.NaN;
        int events = 0;
        foreach (var t in distinct)
        {
            int atRisk = times.Count(v => v >= t);
            int fails = 0, cens = 0;
            for (int i = 0; i < n; i++)
                if (times[i] == t) { if (censored[i]) cens++; else fails++; }
            events += fails;
            if (fails > 0) s *= 1.0 - (double)fails / atRisk;
            if (double.IsNaN(median) && s <= 0.5) median = t;
            rows.Add(new KmRow(t, atRisk, fails, cens, s));
        }
        return new KaplanMeierResult(rows, median, n, events);
    }
}
