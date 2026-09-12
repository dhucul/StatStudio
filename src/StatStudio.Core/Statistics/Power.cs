using MathNet.Numerics.Distributions;

namespace StatStudio.Core.Statistics;

/// <summary>Normal-approximation power, with signed alternatives and integer sample-size searches.</summary>
public static class Power
{
    private static double Critical(double alpha, Alternative alt)
    {
        StatGuard.Probability(alpha, nameof(alpha));
        if (!Enum.IsDefined(alt)) throw new ArgumentOutOfRangeException(nameof(alt));
        return Normal.InvCDF(0, 1, 1 - alpha / (alt == Alternative.TwoSided ? 2 : 1));
    }

    private static double RejectionProbability(double shift, double sd, double critical, Alternative alt) => alt switch
    {
        Alternative.Less => Normal.CDF(0, 1, (-critical - shift) / sd),
        Alternative.Greater => Normal.CDF(0, 1, (shift - critical) / sd),
        _ => Normal.CDF(0, 1, (-critical - shift) / sd) + Normal.CDF(0, 1, (shift - critical) / sd),
    };

    /// <param name="effect">Signed (mean - null mean) / sigma.</param>
    public static double OneSampleTPower(double n, double effect, double alpha, Alternative alt)
    {
        RequireSampleSize(n);
        RequireEffect(effect);
        return RejectionProbability(effect * Math.Sqrt(n), 1, Critical(alpha, alt), alt);
    }

    public static double TwoSampleTPower(double nPerGroup, double effect, double alpha, Alternative alt)
    {
        RequireSampleSize(nPerGroup);
        RequireEffect(effect);
        return RejectionProbability(effect * Math.Sqrt(nPerGroup / 2), 1, Critical(alpha, alt), alt);
    }

    public static double OneSampleTSampleSize(double power, double effect, double alpha, Alternative alt)
    {
        RequireEffect(effect);
        return SampleSize(power, effect, alt, n => OneSampleTPower(n, effect, alpha, alt));
    }

    public static double TwoSampleTSampleSize(double power, double effect, double alpha, Alternative alt)
    {
        RequireEffect(effect);
        return SampleSize(power, effect, alt, n => TwoSampleTPower(n, effect, alpha, alt));
    }

    public static double OneProportionPower(double n, double p0, double p1, double alpha, Alternative alt)
    {
        RequireSampleSize(n);
        RequireProportions(p0, p1);
        double se0 = Math.Sqrt(p0 * (1 - p0));
        return RejectionProbability((p1 - p0) * Math.Sqrt(n) / se0,
            Math.Sqrt(p1 * (1 - p1)) / se0, Critical(alpha, alt), alt);
    }

    public static double OneProportionSampleSize(double power, double p0, double p1, double alpha, Alternative alt)
    {
        RequireProportions(p0, p1);
        return SampleSize(power, p1 - p0, alt, n => OneProportionPower(n, p0, p1, alpha, alt));
    }

    private static double SampleSize(double target, double change, Alternative alt, Func<double, double> power)
    {
        StatGuard.Probability(target, nameof(target));
        if (power(2) >= target) return 2;
        if ((alt == Alternative.Less && change > 0) || (alt == Alternative.Greater && change < 0))
            throw new ArgumentException("The change is opposite to the selected alternative; increasing the sample size cannot achieve this power.");
        long lo = 2, hi = 4;
        while (power(hi) < target)
        {
            lo = hi;
            if (hi == int.MaxValue) throw new ArgumentException("Required sample size exceeds 2,147,483,647.");
            hi = Math.Min(int.MaxValue, hi * 2);
        }
        while (hi - lo > 1)
        {
            long mid = lo + (hi - lo) / 2;
            if (power(mid) >= target) hi = mid; else lo = mid;
        }
        return hi;
    }

    private static void RequireSampleSize(double n)
    {
        if (!double.IsFinite(n) || n < 2 || n != Math.Truncate(n))
            throw new ArgumentOutOfRangeException(nameof(n), "Sample size must be a whole number of at least 2.");
    }

    private static void RequireEffect(double effect)
    {
        if (!double.IsFinite(effect) || effect == 0)
            throw new ArgumentOutOfRangeException(nameof(effect), "Effect size must be finite and nonzero.");
    }

    private static void RequireProportions(double p0, double p1)
    {
        StatGuard.Probability(p0, nameof(p0));
        StatGuard.Probability(p1, nameof(p1));
        if (p0 == p1) throw new ArgumentException("The null and alternative proportions must differ.");
    }
}
