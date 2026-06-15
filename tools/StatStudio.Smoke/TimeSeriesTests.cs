using StatStudio.Core.Statistics;

namespace StatStudio.Smoke;

internal static class TimeSeriesTests
{
    public static void Run()
    {
        Check.Section("Autocorrelation  {1,2,3,4,5}");
        var acf = TimeSeries.Autocorrelation(new double[] { 1, 2, 3, 4, 5 }, 2);
        Check.Close(acf.Acf[0], 1.0, "ACF lag 0");
        Check.Close(acf.Acf[1], 0.4, "ACF lag 1");
        Check.Close(acf.Acf[2], -0.1, "ACF lag 2");
        Check.Close(acf.Pacf[1], 0.4, "PACF lag 1");

        Check.Section("Linear trend  {1,2,3,4,5}");
        var tr = TimeSeries.LinearTrend(new double[] { 1, 2, 3, 4, 5 }, forecasts: 2);
        Check.Close(tr.Coefficients[0], 0.0, "intercept", 1e-6);
        Check.Close(tr.Coefficients[1], 1.0, "slope", 1e-6);
        Check.Close(tr.Forecasts[0], 6.0, "forecast t=6");
        Check.Close(tr.Forecasts[1], 7.0, "forecast t=7");

        Check.Section("Moving average (length 3, centered)");
        var ma = TimeSeries.MovingAverage(new double[] { 1, 2, 3, 4, 5 }, 3);
        Check.Close(ma.Fitted[1], 2.0, "MA at index 1");
        Check.Close(ma.Fitted[2], 3.0, "MA at index 2");
        Check.Close(ma.Fitted[3], 4.0, "MA at index 3");

        Check.Section("Single exponential smoothing  {2,4,6}, alpha=0.5");
        var ses = TimeSeries.SingleExp(new double[] { 2, 4, 6 }, 0.5, forecasts: 1);
        Check.Close(ses.Fitted[1], 2.0, "fitted[1] = level1");
        Check.Close(ses.Fitted[2], 3.0, "fitted[2] = level2");
        Check.Close(ses.Forecasts[0], 4.5, "forecast = level3");

        Check.Section("Double exponential smoothing (increasing series)");
        var des = TimeSeries.DoubleExp(new double[] { 1, 2, 3, 4, 5 }, 0.5, 0.5, forecasts: 2);
        Check.True(des.Forecasts[0] > 5 && des.Forecasts[1] > des.Forecasts[0], "forecasts continue upward");

        Check.Section("Classical decomposition (additive, period 4)");
        var y = new double[16];
        var pat = new double[] { 3, -1, -3, 1 };
        for (int t = 0; t < 16; t++) y[t] = 10 + pat[t % 4];
        var dec = TimeSeries.Decompose(y, 4, multiplicative: false);
        Check.Close(dec.SeasonalIndices[0], 3.0, "seasonal index phase 0", 0.3);
        Check.Close(dec.SeasonalIndices[2], -3.0, "seasonal index phase 2", 0.3);
        Check.True(Math.Abs(dec.SeasonalIndices.Sum()) < 1e-9, "additive indices sum to 0");

        Check.Section("Winters (multiplicative) forecasts");
        var w = TimeSeries.Winters(y, 4, 0.2, 0.1, 0.1, multiplicative: true, forecasts: 4);
        Check.Equal(w.Forecasts.Length, 4, "forecast count");
        Check.True(w.Forecasts.All(double.IsFinite), "forecasts finite");
    }
}
