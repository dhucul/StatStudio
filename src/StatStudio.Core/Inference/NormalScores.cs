using MathNet.Numerics.Distributions;

namespace StatStudio.Core.Inference;

/// <summary>Normal-probability-plot helpers (sorted data paired with normal scores).</summary>
public static class NormalScores
{
    /// <summary>
    /// Sorted values paired with their normal scores using the median-rank plotting
    /// position p_i = (i − 0.375)/(n + 0.25), z_i = Φ⁻¹(p_i) — Minitab's default.
    /// </summary>
    public static (double[] Sorted, double[] Scores) Compute(IEnumerable<double> values)
    {
        var sorted = Quantiles.Sorted(values);
        int n = sorted.Length;
        var z = new double[n];
        for (int i = 0; i < n; i++)
        {
            double p = (i + 1 - 0.375) / (n + 0.25);
            z[i] = Normal.InvCDF(0.0, 1.0, p);
        }
        return (sorted, z);
    }
}
