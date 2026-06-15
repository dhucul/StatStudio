using StatStudio.Core.Inference;

namespace StatStudio.Core.Statistics;

public static class TimeSeriesFormatters
{
    private static string Accuracy(AccuracyMeasures a)
    {
        var t = new TextTable("MAPE", "MAD", "MSD");
        t.Add(Fmt.N(a.Mape, 3), Fmt.N(a.Mad, 3), Fmt.N(a.Msd, 3));
        return "Accuracy Measures\n" + t;
    }

    private static string Forecasts(double[] fc, int startPeriod)
    {
        if (fc.Length == 0) return "";
        var t = new TextTable("Period", "Forecast");
        for (int i = 0; i < fc.Length; i++) t.Add((startPeriod + i).ToString(), Fmt.N(fc[i]));
        return "\n\nForecasts\n" + t;
    }

    public static string Trend(TrendResult r, string name, int n)
    {
        return $"Trend Analysis: {name}\n\n" +
               $"Model: {r.Type} trend\n{r.Equation}\n\n" +
               Accuracy(r.Accuracy) + Forecasts(r.Forecasts, n + 1);
    }

    public static string Smoothing(SmoothingResult r, string name, int n)
    {
        var p = new TextTable(r.Parameters.Select(x => x.Name).ToArray());
        p.Add(r.Parameters.Select(x => Fmt.N(x.Value, 3)).ToArray());
        return $"{r.Method}: {name}\n\n" +
               "Smoothing Parameters\n" + p + "\n\n" +
               Accuracy(r.Accuracy) + Forecasts(r.Forecasts, n + 1);
    }

    public static string Decomposition(DecompositionResult r, string name)
    {
        var t = new TextTable("Season", "Index");
        for (int s = 0; s < r.SeasonalIndices.Length; s++) t.Add((s + 1).ToString(), Fmt.N(r.SeasonalIndices[s], 4));
        return $"Time Series Decomposition: {name}\n\n" +
               $"Model: {(r.Multiplicative ? "multiplicative" : "additive")}, period {r.Period}\n\n" +
               "Seasonal Indices\n" + t;
    }

    public static string Acf(AcfResult r, string name, bool partial)
    {
        var vals = partial ? r.Pacf : r.Acf;
        double bound = 1.96 / Math.Sqrt(r.N);
        var t = new TextTable("Lag", partial ? "PACF" : "ACF", "Signif?").LeftAlign(2);
        for (int k = 1; k < vals.Length; k++)
            t.Add(k.ToString(), Fmt.N(vals[k], 4), Math.Abs(vals[k]) > bound ? "*" : "");
        return $"{(partial ? "Partial Autocorrelation" : "Autocorrelation")}: {name}\n\n" +
               $"(95% significance bound ≈ ±{Fmt.N(bound, 4)}; * marks |value| beyond it)\n\n" + t;
    }
}
