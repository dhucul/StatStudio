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

        Check.Section("ARIMA(1,0,0) on 1..10 (exact AR via OLS)");
        var line = Enumerable.Range(1, 10).Select(i => (double)i).ToArray();
        var ar1 = Arima.Fit(line, 1, 0, 0, forecasts: 3);
        ArimaTerm AT(ArimaResult r, string n) => r.Terms.First(t => t.Name == n);
        Check.Close(AT(ar1, "AR(1)").Coef, 1.0, "AR(1) coef = 1", 1e-6);
        Check.Close(AT(ar1, "Constant").Coef, 1.0, "constant = 1", 1e-6);
        Check.Close(ar1.Forecasts[0], 11.0, "forecast t=11", 1e-4);

        Check.Section("ARIMA(2,0,0) on Fibonacci (φ1=φ2=1)");
        var fib = new double[] { 1, 1, 2, 3, 5, 8, 13, 21, 34, 55 };
        var ar2 = Arima.Fit(fib, 2, 0, 0);
        Check.Close(AT(ar2, "AR(1)").Coef, 1.0, "AR(1) = 1", 1e-6);
        Check.Close(AT(ar2, "AR(2)").Coef, 1.0, "AR(2) = 1", 1e-6);

        Check.Section("ARIMA(0,1,0) on 1..10 (difference -> drift)");
        var ima = Arima.Fit(line, 0, 1, 0, forecasts: 3);
        Check.Close(ima.Forecasts[0], 11.0, "forecast t=11", 1e-4);
        Check.Close(ima.Forecasts[2], 13.0, "forecast t=13", 1e-4);

        Check.Section("ARIMA(0,0,1) MA fit (reasonableness)");
        var rnd = new Random(1);
        var noisy = Enumerable.Range(0, 60).Select(_ => rnd.NextDouble() * 2 - 1).ToArray();
        var ma1 = Arima.Fit(noisy, 0, 0, 1);
        Check.True(double.IsFinite(ma1.Terms.First(t => t.Name == "MA(1)").Coef), "MA(1) coef finite");
        Check.True(ma1.Sigma2 > 0, "residual variance positive");
    }
}
