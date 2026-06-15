using StatStudio.Core.Data;
using StatStudio.Core.Inference;

namespace StatStudio.Core.Statistics;

/// <summary>Per-variable descriptive statistics, computed with Minitab's formulas.</summary>
public sealed record DescriptiveStats(
    string Variable,
    int N,
    int NMissing,
    double Mean,
    double SeMean,
    double StDev,
    double Variance,
    double Minimum,
    double Q1,
    double Median,
    double Q3,
    double Maximum,
    double Range,
    double Iqr,
    double Skewness,
    double Kurtosis,
    double Sum);

public static class Descriptives
{
    public static DescriptiveStats Compute(DataColumn col) =>
        Compute(col.Name, col.NumericValues(), col.MissingCount());

    public static DescriptiveStats Compute(string variable, double[] values, int nMissing = 0)
    {
        int n = values.Length;
        if (n == 0)
            return new DescriptiveStats(variable, 0, nMissing,
                double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN,
                double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN,
                double.NaN, 0);

        double sum = 0;
        foreach (var v in values) sum += v;
        double mean = sum / n;

        double ss = 0;
        foreach (var v in values) ss += (v - mean) * (v - mean);
        double variance = n > 1 ? ss / (n - 1) : 0.0;
        double sd = Math.Sqrt(variance);
        double seMean = n > 0 ? sd / Math.Sqrt(n) : double.NaN;

        var sorted = Quantiles.Sorted(values);
        double min = sorted[0], max = sorted[n - 1];
        double q1 = Quantiles.Percentile(sorted, 25);
        double median = Quantiles.Percentile(sorted, 50);
        double q3 = Quantiles.Percentile(sorted, 75);

        // Minitab skewness/kurtosis: (1/n)·Σ z^3  and  (1/n)·Σ z^4 − 3, with z = (x−mean)/s.
        double skew = double.NaN, kurt = double.NaN;
        if (n > 1 && sd > 0)
        {
            double s3 = 0, s4 = 0;
            foreach (var v in values)
            {
                double z = (v - mean) / sd;
                double z2 = z * z;
                s3 += z2 * z;
                s4 += z2 * z2;
            }
            skew = s3 / n;
            kurt = s4 / n - 3.0;
        }

        return new DescriptiveStats(variable, n, nMissing, mean, seMean, sd, variance,
            min, q1, median, q3, max, max - min, q3 - q1, skew, kurt, sum);
    }
}
