namespace StatStudio.Core.Statistics;

/// <summary>Rank assignment with average ranks for ties (shared by the nonparametric tests).</summary>
public static class Ranking
{
    /// <summary>Average ranks (1-based) of the values; tied values share the mean of their ranks.</summary>
    public static double[] Average(IReadOnlyList<double> values)
    {
        int n = values.Count;
        var idx = Enumerable.Range(0, n).OrderBy(i => values[i]).ToArray();
        var ranks = new double[n];
        int i = 0;
        while (i < n)
        {
            int j = i;
            while (j + 1 < n && values[idx[j + 1]] == values[idx[i]]) j++;
            double avg = (i + j) / 2.0 + 1.0;   // mean of ranks (i+1)..(j+1)
            for (int k = i; k <= j; k++) ranks[idx[k]] = avg;
            i = j + 1;
        }
        return ranks;
    }

    /// <summary>Σ(t³ − t) over tie groups of sizes t — the standard tie-correction term.</summary>
    public static double TieCorrection(IReadOnlyList<double> values)
    {
        double sum = 0;
        foreach (var g in values.GroupBy(v => v))
        {
            double t = g.Count();
            sum += t * t * t - t;
        }
        return sum;
    }
}
