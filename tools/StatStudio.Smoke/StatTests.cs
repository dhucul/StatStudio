using StatStudio.Core.Inference;
using StatStudio.Core.Statistics;

namespace StatStudio.Smoke;

internal static class StatTests
{
    public static void Run()
    {
        Check.Section("Quantiles (Minitab / R-6 method)");
        // 1..10: Q1=2.75, Median=5.5, Q3=8.25
        var ten = Quantiles.Sorted(Enumerable.Range(1, 10).Select(i => (double)i));
        Check.Close(Quantiles.Percentile(ten, 25), 2.75, "Q1 of 1..10");
        Check.Close(Quantiles.Percentile(ten, 50), 5.50, "Median of 1..10");
        Check.Close(Quantiles.Percentile(ten, 75), 8.25, "Q3 of 1..10");

        Check.Section("Descriptive statistics  {2,4,4,4,5,5,7,9}");
        var d = Descriptives.Compute("C1", new double[] { 2, 4, 4, 4, 5, 5, 7, 9 });
        Check.Equal(d.N, 8, "N");
        Check.Close(d.Mean, 5.0, "Mean");
        Check.Close(d.SeMean, 0.755929, "SE Mean");
        Check.Close(d.StDev, 2.138090, "StDev");
        Check.Close(d.Variance, 4.571429, "Variance");
        Check.Close(d.Minimum, 2.0, "Minimum");
        Check.Close(d.Q1, 4.0, "Q1");
        Check.Close(d.Median, 4.5, "Median");
        Check.Close(d.Q3, 6.5, "Q3");
        Check.Close(d.Maximum, 9.0, "Maximum");
        Check.Close(d.Range, 7.0, "Range");
        Check.Close(d.Iqr, 2.5, "IQR");
        Check.Close(d.Skewness, 0.537139, "Skewness", 1e-3);
        Check.Close(d.Kurtosis, -0.870613, "Kurtosis", 1e-3);
        Check.Close(d.Sum, 40.0, "Sum");
    }
}
