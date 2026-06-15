using StatStudio.Core.Inference;

namespace StatStudio.Core.Statistics;

/// <summary>Formats descriptive statistics as Minitab-style Session tables.</summary>
public static class DescriptivesFormatter
{
    public static string Format(IEnumerable<DescriptiveStats> stats)
    {
        var list = stats.ToList();

        var t1 = new TextTable("Variable", "N", "N*", "Mean", "SE Mean", "StDev",
                               "Minimum", "Q1", "Median", "Q3", "Maximum").LeftAlign(0);
        foreach (var s in list)
            t1.Add(s.Variable, s.N.ToString(), s.NMissing.ToString(),
                   Fmt.N(s.Mean), Fmt.N(s.SeMean), Fmt.N(s.StDev),
                   Fmt.N(s.Minimum), Fmt.N(s.Q1), Fmt.N(s.Median), Fmt.N(s.Q3), Fmt.N(s.Maximum));

        var t2 = new TextTable("Variable", "Range", "IQR", "Variance",
                               "Skewness", "Kurtosis", "Sum").LeftAlign(0);
        foreach (var s in list)
            t2.Add(s.Variable, Fmt.N(s.Range), Fmt.N(s.Iqr), Fmt.N(s.Variance),
                   Fmt.N(s.Skewness, 2), Fmt.N(s.Kurtosis, 2), Fmt.N(s.Sum));

        return t1 + "\n\n" + t2;
    }
}
