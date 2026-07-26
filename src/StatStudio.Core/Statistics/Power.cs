using MathNet.Numerics.Distributions;

namespace StatStudio.Core.Statistics;

/// <summary>
/// Power and sample-size calculations (normal approximation, which is exact for the
/// z-test and a close approximation for the corresponding t-tests).
/// </summary>
public static class Power
{
    private static double Zc(double alpha, Alternative alt)
    {
        StatGuard.Probability(alpha, nameof(alpha));
        return alt == Alternative.TwoSided
            ? Normal.InvCDF(0, 1, 1 - alpha / 2)
            : Normal.InvCDF(0, 1, 1 - alpha);
    }

    // ---- one-sample t (effect = |mean - mu0| / sigma) ----------------------

    public static double OneSampleTPower(double n, double effect, double alpha, Alternative alt)
    {
        RequireSampleSize(n, nameof(n));
        RequireEffect(effect);
        double ncp = Math.Abs(effect) * Math.Sqrt(n);
        double zc = Zc(alpha, alt);
        double power = Normal.CDF(0, 1, ncp - zc);
        if (alt == Alternative.TwoSided) power += Normal.CDF(0, 1, -ncp - zc);
        return power;
    }

    public static double OneSampleTSampleSize(double power, double effect, double alpha, Alternative alt)
    {
        StatGuard.Probability(power, nameof(power));
        RequireEffect(effect);
        double zc = Zc(alpha, alt), zb = Normal.InvCDF(0, 1, power);
        return Math.Pow((zc + zb) / Math.Abs(effect), 2);
    }

    // ---- two-sample t (effect = |mu1 - mu2| / sigma, equal n per group) -----

    public static double TwoSampleTPower(double nPerGroup, double effect, double alpha, Alternative alt)
    {
        RequireSampleSize(nPerGroup, nameof(nPerGroup));
        RequireEffect(effect);
        double ncp = Math.Abs(effect) * Math.Sqrt(nPerGroup / 2.0);
        double zc = Zc(alpha, alt);
        double power = Normal.CDF(0, 1, ncp - zc);
        if (alt == Alternative.TwoSided) power += Normal.CDF(0, 1, -ncp - zc);
        return power;
    }

    public static double TwoSampleTSampleSize(double power, double effect, double alpha, Alternative alt)
    {
        StatGuard.Probability(power, nameof(power));
        RequireEffect(effect);
        double zc = Zc(alpha, alt), zb = Normal.InvCDF(0, 1, power);
        return 2 * Math.Pow((zc + zb) / Math.Abs(effect), 2);
    }

    // ---- one proportion ----------------------------------------------------

    public static double OneProportionPower(double n, double p0, double p1, double alpha, Alternative alt)
    {
        RequireSampleSize(n, nameof(n));
        RequireDistinctProportions(p0, p1);
        double zc = Zc(alpha, alt);
        double se0 = Math.Sqrt(p0 * (1 - p0)), se1 = Math.Sqrt(p1 * (1 - p1));
        return Normal.CDF(0, 1, (Math.Abs(p1 - p0) * Math.Sqrt(n) - zc * se0) / se1);
    }

    public static double OneProportionSampleSize(double power, double p0, double p1, double alpha, Alternative alt)
    {
        StatGuard.Probability(power, nameof(power));
        RequireDistinctProportions(p0, p1);
        double zc = Zc(alpha, alt), zb = Normal.InvCDF(0, 1, power);
        double num = zc * Math.Sqrt(p0 * (1 - p0)) + zb * Math.Sqrt(p1 * (1 - p1));
        return Math.Pow(num / (p1 - p0), 2);
    }

    private static void RequireSampleSize(double n, string paramName)
    {
        if (!double.IsFinite(n) || n <= 0)
            throw new ArgumentOutOfRangeException(paramName, "Sample size must be positive and finite.");
    }

    private static void RequireEffect(double effect)
    {
        if (!double.IsFinite(effect) || effect == 0)
            throw new ArgumentOutOfRangeException(nameof(effect), "Effect size must be finite and nonzero.");
    }

    private static void RequireDistinctProportions(double p0, double p1)
    {
        StatGuard.Probability(p0, nameof(p0));
        StatGuard.Probability(p1, nameof(p1));
        if (p0 == p1)
            throw new ArgumentException("The null and alternative proportions must differ.");
    }
}
