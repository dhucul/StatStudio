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
        var wholeWindow = TimeSeries.MovingAverage(new double[] { 1, 2, 3 }, 3);
        Check.True(double.IsFinite(wholeWindow.Fitted[1]), "MA length may equal series length");

        Check.Section("Single exponential smoothing  {2,4,6}, alpha=0.5");
        var ses = TimeSeries.SingleExp(new double[] { 2, 4, 6 }, 0.5, forecasts: 1);
        Check.Close(ses.Fitted[1], 2.0, "fitted[1] = level1");
        Check.Close(ses.Fitted[2], 3.0, "fitted[2] = level2");
        Check.Close(ses.Forecasts[0], 4.5, "forecast = level3");
        var zeroActual = TimeSeries.SingleExp(new double[] { 10, 0, 10 }, 0.5);
        Check.Close(zeroActual.Accuracy.Mape, 50, "MAPE excludes zero actuals from its denominator");

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

        var integrated = new double[] { 0, 1, 3, 4, 7, 9, 12, 16, 17, 21, 24, 30 };
        var randomWalk = Arima.Fit(integrated, 0, 1, 0, forecasts: 4);
        var rwWidths = randomWalk.ForecastUpper.Zip(randomWalk.ForecastLower, (hi, lo) => hi - lo).ToArray();
        Check.True(rwWidths[3] > 1.9 * rwWidths[0], "ARIMA d=1 interval accumulates uncertainty");

        Check.Section("ARIMA(0,0,1) MA fit (reasonableness)");
        var rnd = new Random(1);
        var noisy = Enumerable.Range(0, 60).Select(_ => rnd.NextDouble() * 2 - 1).ToArray();
        var ma1 = Arima.Fit(noisy, 0, 0, 1);
        Check.True(double.IsFinite(ma1.Terms.First(t => t.Name == "MA(1)").Coef), "MA(1) coef finite");
        Check.True(ma1.Sigma2 > 0, "residual variance positive");

        Check.Section("SARIMA(0,0,0)(0,1,0)_4 — seasonal differencing repeats the season");
        var seas = new double[12];
        var pat4 = new double[] { 10, 20, 30, 40 };
        for (int t = 0; t < 12; t++) seas[t] = pat4[t % 4];
        var sar = Sarima.Fit(seas, 0, 0, 0, 0, 1, 0, 4, forecasts: 4, includeConstant: false);
        Check.Close(sar.Forecasts[0], 10, "forecast season 1", 1e-6);
        Check.Close(sar.Forecasts[1], 20, "forecast season 2", 1e-6);
        Check.Close(sar.Forecasts[2], 30, "forecast season 3", 1e-6);
        Check.Close(sar.Forecasts[3], 40, "forecast season 4", 1e-6);

        var seasonalWalk = new double[24];
        seasonalWalk[0] = 10; seasonalWalk[1] = 20; seasonalWalk[2] = 30; seasonalWalk[3] = 40;
        var seasonalChanges = new double[] { 1, 3, 2, 5, 4, 2, 6, 3, 5, 1, 4, 7, 2, 5, 3, 6, 4, 8, 2, 7 };
        for (int i = 4; i < seasonalWalk.Length; i++)
            seasonalWalk[i] = seasonalWalk[i - 4] + seasonalChanges[i - 4];
        var seasonalRandomWalk = Sarima.Fit(seasonalWalk, 0, 0, 0, 0, 1, 0, 4,
            forecasts: 8, includeConstant: false);
        var seasonalWidths = seasonalRandomWalk.ForecastUpper
            .Zip(seasonalRandomWalk.ForecastLower, (hi, lo) => hi - lo).ToArray();
        Check.True(seasonalWidths[4] > 1.3 * seasonalWidths[0], "SARIMA D=1 interval accumulates seasonal uncertainty");

        Check.Section("SARIMA(1,0,0)(0,0,0)_1 reduces to AR(1)");
        var sar2 = Sarima.Fit(line, 1, 0, 0, 0, 0, 0, 1);
        Check.Close(sar2.Terms.First(t => t.Name == "AR(1)").Coef, 1.0, "AR(1) ~ 1", 0.05);

        Check.Section("Time-series boundary contracts");
        Check.Throws<ArgumentOutOfRangeException>(
            () => TimeSeries.Autocorrelation(new double[] { 1, 2, 3 }, -1), "negative max lag rejected");
        Check.Throws<ArgumentOutOfRangeException>(
            () => TimeSeries.SingleExp(new double[] { 1, 2 }, 1.1), "invalid smoothing coefficient rejected");
        Check.Throws<ArgumentException>(
            () => TimeSeries.QuadraticTrend(new double[] { 1, 2, 3 }), "three-point quadratic rejected");
        Check.Throws<ArgumentOutOfRangeException>(
            () => Arima.Fit(line, 1, 0, 0, forecasts: -1), "negative ARIMA forecast count rejected");
    }
}
