namespace StatStudio.Core.Statistics.Spc;

public sealed record CapabilityResult(
    int N, double Mean, double SigmaWithin, double SigmaOverall,
    double? Lsl, double? Usl, double? Target,
    double Cp, double Cpk, double Pp, double Ppk, double Cpm);

public static class Capability
{
    /// <summary>
    /// Normal capability from individual measurements. Within-subgroup spread uses the
    /// average moving range (σ̂ = MR̄/d2); overall spread uses the sample standard deviation.
    /// </summary>
    public static CapabilityResult FromIndividuals(double[] values, double? lsl, double? usl, double? target = null)
    {
        int n = values.Length;
        double mean = values.Average();
        double sigmaOverall = StdDev(values);

        double mrBar = 0;
        for (int i = 1; i < n; i++) mrBar += Math.Abs(values[i] - values[i - 1]);
        mrBar /= Math.Max(1, n - 1);
        double sigmaWithin = mrBar / SpcConstants.D2(2);

        double cp = Index(lsl, usl, sigmaWithin);
        double cpk = Cpk(mean, lsl, usl, sigmaWithin);
        double pp = Index(lsl, usl, sigmaOverall);
        double ppk = Cpk(mean, lsl, usl, sigmaOverall);
        double cpm = Cpm(mean, lsl, usl, target, sigmaOverall);

        return new CapabilityResult(n, mean, sigmaWithin, sigmaOverall, lsl, usl, target,
            cp, cpk, pp, ppk, cpm);
    }

    private static double Index(double? lsl, double? usl, double sigma)
    {
        if (sigma <= 0 || lsl is null || usl is null) return double.NaN;
        return (usl.Value - lsl.Value) / (6 * sigma);
    }

    private static double Cpk(double mean, double? lsl, double? usl, double sigma)
    {
        if (sigma <= 0) return double.NaN;
        double? upper = usl is null ? null : (usl.Value - mean) / (3 * sigma);
        double? lower = lsl is null ? null : (mean - lsl.Value) / (3 * sigma);
        if (upper is null && lower is null) return double.NaN;
        if (upper is null) return lower!.Value;
        if (lower is null) return upper.Value;
        return Math.Min(upper.Value, lower.Value);
    }

    private static double Cpm(double mean, double? lsl, double? usl, double? target, double sigmaOverall)
    {
        if (target is null || lsl is null || usl is null || sigmaOverall < 0) return double.NaN;
        double denom = Math.Sqrt(sigmaOverall * sigmaOverall + (mean - target.Value) * (mean - target.Value));
        return denom <= 0 ? double.NaN : (usl.Value - lsl.Value) / (6 * denom);
    }

    private static double StdDev(double[] x)
    {
        int n = x.Length;
        if (n < 2) return 0;
        double m = x.Average(), ss = 0;
        foreach (var v in x) ss += (v - m) * (v - m);
        return Math.Sqrt(ss / (n - 1));
    }
}
