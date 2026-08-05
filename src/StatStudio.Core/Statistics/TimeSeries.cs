namespace StatStudio.Core.Statistics;

public sealed record AccuracyMeasures(double Mape, double Mad, double Msd);

public sealed record TrendResult(
    string Type, double[] Coefficients, double[] Fitted, double[] Forecasts,
    AccuracyMeasures Accuracy, string Equation);

public sealed record SmoothingResult(
    string Method, IReadOnlyList<(string Name, double Value)> Parameters,
    double[] Fitted, double[] Forecasts, AccuracyMeasures Accuracy);

public sealed record DecompositionResult(
    bool Multiplicative, int Period, double[] Trend, double[] Seasonal,
    double[] SeasonalIndices, double[] Detrended);

public sealed record AcfResult(int N, double[] Acf, double[] Pacf);

/// <summary>Classical time-series analysis: trend, smoothing, decomposition, autocorrelation.</summary>
public static class TimeSeries
{
    // ---- autocorrelation ---------------------------------------------------

    public static AcfResult Autocorrelation(double[] y, int maxLag)
    {
        RequireSeries(y, 1);
        if (maxLag < 0) throw new ArgumentOutOfRangeException(nameof(maxLag));
        int n = y.Length;
        maxLag = Math.Min(maxLag, n - 1);
        double mean = y.Average();
        double denom = y.Sum(v => (v - mean) * (v - mean));

        var acf = new double[maxLag + 1];
        acf[0] = 1.0;
        for (int k = 1; k <= maxLag; k++)
        {
            double num = 0;
            for (int t = 0; t < n - k; t++) num += (y[t] - mean) * (y[t + k] - mean);
            acf[k] = denom > 0 ? num / denom : double.NaN;
        }

        // Partial autocorrelation via Durbin-Levinson. The recursion only ever reads row k-1, so
        // two rolling vectors replace the full (maxLag+1)^2 matrix — that matrix reached 3.2 GB
        // on a 20 000-point series and took the process out with an OutOfMemoryException.
        var pacf = new double[maxLag + 1];
        var previous = new double[maxLag + 1];
        var current = new double[maxLag + 1];
        if (maxLag >= 1) { pacf[1] = acf[1]; previous[1] = acf[1]; }
        for (int k = 2; k <= maxLag; k++)
        {
            double num = acf[k];
            for (int j = 1; j < k; j++) num -= previous[j] * acf[k - j];
            double den = 1.0;
            for (int j = 1; j < k; j++) den -= previous[j] * acf[j];
            double phikk = den != 0 ? num / den : 0;
            current[k] = phikk;
            for (int j = 1; j < k; j++) current[j] = previous[j] - phikk * previous[k - j];
            pacf[k] = phikk;
            (previous, current) = (current, previous);
        }
        return new AcfResult(n, acf, pacf);
    }

    // ---- trend -------------------------------------------------------------

    public static TrendResult LinearTrend(double[] y, int forecasts = 0)
    {
        RequireSeries(y, 3);
        RequireForecasts(forecasts);
        int n = y.Length;
        var t = Enumerable.Range(1, n).Select(i => (double)i).ToArray();
        var reg = Regression.SimpleLinear(t, y, "t", "Y");
        double b0 = reg.Coefficients[0], b1 = reg.Coefficients[1];
        var fc = Enumerable.Range(n + 1, forecasts).Select(i => b0 + b1 * i).ToArray();
        return new TrendResult("Linear", new[] { b0, b1 }, reg.Fitted, fc,
            Accuracy(y, reg.Fitted), $"Y = {Round(b0)} + {Round(b1)}·t");
    }

    public static TrendResult QuadraticTrend(double[] y, int forecasts = 0)
    {
        RequireSeries(y, 4);
        RequireForecasts(forecasts);
        int n = y.Length;
        var t = Enumerable.Range(1, n).Select(i => (double)i).ToArray();
        var reg = RegressionExtensions.Polynomial(t, y, 2, "t", "Y");
        var c = reg.Coefficients;
        var fc = Enumerable.Range(n + 1, forecasts).Select(i => c[0] + c[1] * i + c[2] * i * i).ToArray();
        return new TrendResult("Quadratic", c, reg.Fitted, fc, Accuracy(y, reg.Fitted),
            $"Y = {Round(c[0])} + {Round(c[1])}·t + {Round(c[2])}·t²");
    }

    // ---- moving average ----------------------------------------------------

    public static SmoothingResult MovingAverage(double[] y, int length, int forecasts = 0)
    {
        RequireSeries(y, 1);
        if (length < 1 || length > y.Length)
            throw new ArgumentOutOfRangeException(nameof(length), "Length must be between 1 and the series length.");
        RequireForecasts(forecasts);
        int n = y.Length;
        var fitted = new double[n];
        Array.Fill(fitted, double.NaN);
        bool center = length % 2 == 1;
        int half = length / 2;
        for (int i = 0; i < n; i++)
        {
            int lo = center ? i - half : i - length + 1;
            int hi = center ? i + half : i;
            if (lo < 0 || hi >= n) continue;
            double sum = 0;
            for (int j = lo; j <= hi; j++) sum += y[j];
            fitted[i] = sum / length;
        }
        double last = y.Skip(Math.Max(0, n - length)).Average();
        var fc = Enumerable.Repeat(last, forecasts).ToArray();
        return new SmoothingResult("Moving Average", new[] { ("Length", (double)length) },
            fitted, fc, Accuracy(y, fitted));
    }

    // ---- exponential smoothing --------------------------------------------

    public static SmoothingResult SingleExp(double[] y, double alpha, int forecasts = 0)
    {
        RequireSeries(y, 1);
        StatGuard.UnitInterval(alpha, nameof(alpha));
        RequireForecasts(forecasts);
        int n = y.Length;
        var fitted = new double[n];
        Array.Fill(fitted, double.NaN);
        double level = y[0];
        for (int t = 1; t < n; t++)
        {
            fitted[t] = level;                      // one-step-ahead forecast
            level = alpha * y[t] + (1 - alpha) * level;
        }
        var fc = Enumerable.Repeat(level, forecasts).ToArray();
        return new SmoothingResult("Single Exponential Smoothing", new[] { ("Alpha", alpha) },
            fitted, fc, Accuracy(y, fitted));
    }

    public static SmoothingResult DoubleExp(double[] y, double alpha, double beta, int forecasts = 0)
    {
        RequireSeries(y, 2);
        StatGuard.UnitInterval(alpha, nameof(alpha));
        StatGuard.UnitInterval(beta, nameof(beta));
        RequireForecasts(forecasts);
        int n = y.Length;
        var fitted = new double[n];
        Array.Fill(fitted, double.NaN);
        double level = y[0];
        double trend = n > 1 ? y[1] - y[0] : 0;
        for (int t = 1; t < n; t++)
        {
            fitted[t] = level + trend;
            double prev = level;
            level = alpha * y[t] + (1 - alpha) * (level + trend);
            trend = beta * (level - prev) + (1 - beta) * trend;
        }
        var fc = Enumerable.Range(1, forecasts).Select(h => level + h * trend).ToArray();
        return new SmoothingResult("Double Exponential Smoothing (Holt)",
            new[] { ("Alpha", alpha), ("Gamma (trend)", beta) }, fitted, fc, Accuracy(y, fitted));
    }

    public static SmoothingResult Winters(double[] y, int period, double alpha, double beta, double gamma,
        bool multiplicative, int forecasts = 0)
    {
        RequireSeries(y, 1);
        if (period < 2) throw new ArgumentOutOfRangeException(nameof(period));
        StatGuard.UnitInterval(alpha, nameof(alpha));
        StatGuard.UnitInterval(beta, nameof(beta));
        StatGuard.UnitInterval(gamma, nameof(gamma));
        RequireForecasts(forecasts);
        if (multiplicative && y.Any(v => v <= 0))
            throw new ArgumentException("Multiplicative Winters requires positive observations.", nameof(y));
        int n = y.Length;
        if (n < 2 * period) throw new ArgumentException("Winters needs at least two full seasons of data.");

        double level = y.Take(period).Average();
        double level2 = y.Skip(period).Take(period).Average();
        double trend = (level2 - level) / period;
        var seasonal = new double[n + forecasts + period];
        for (int i = 0; i < period; i++)
            seasonal[i] = multiplicative ? (level != 0 ? y[i] / level : 1) : y[i] - level;

        var fitted = new double[n];
        Array.Fill(fitted, double.NaN);
        for (int t = period; t < n; t++)
        {
            double s = seasonal[t - period];
            fitted[t] = multiplicative ? (level + trend) * s : level + trend + s;
            double prev = level;
            if (multiplicative)
            {
                level = alpha * (s != 0 ? y[t] / s : y[t]) + (1 - alpha) * (level + trend);
                seasonal[t] = gamma * (level != 0 ? y[t] / level : 1) + (1 - gamma) * s;
            }
            else
            {
                level = alpha * (y[t] - s) + (1 - alpha) * (level + trend);
                seasonal[t] = gamma * (y[t] - level) + (1 - gamma) * s;
            }
            trend = beta * (level - prev) + (1 - beta) * trend;
        }

        var fc = new double[forecasts];
        for (int h = 1; h <= forecasts; h++)
        {
            double s = seasonal[n - period + ((h - 1) % period)];
            fc[h - 1] = multiplicative ? (level + h * trend) * s : level + h * trend + s;
        }
        return new SmoothingResult($"Winters ({(multiplicative ? "multiplicative" : "additive")})",
            new[] { ("Alpha", alpha), ("Beta (trend)", beta), ("Gamma (seasonal)", gamma) },
            fitted, fc, Accuracy(y, fitted));
    }

    // ---- decomposition -----------------------------------------------------

    public static DecompositionResult Decompose(double[] y, int period, bool multiplicative)
    {
        RequireSeries(y, 1);
        if (period < 2) throw new ArgumentOutOfRangeException(nameof(period));
        if (y.Length < 2 * period)
            throw new ArgumentException("Decomposition needs at least two full seasons.", nameof(y));
        if (multiplicative && y.Any(v => v <= 0))
            throw new ArgumentException("Multiplicative decomposition requires positive observations.", nameof(y));
        int n = y.Length;
        var trend = new double[n];
        Array.Fill(trend, double.NaN);
        int half = period / 2;
        for (int t = 0; t < n; t++)
        {
            if (t - half < 0 || t + half >= n) continue;
            double sum;
            if (period % 2 == 0)
            {
                sum = 0.5 * y[t - half] + 0.5 * y[t + half];
                for (int j = -half + 1; j <= half - 1; j++) sum += y[t + j];
                trend[t] = sum / period;
            }
            else
            {
                sum = 0;
                for (int j = -half; j <= half; j++) sum += y[t + j];
                trend[t] = sum / period;
            }
        }

        var detrended = new double[n];
        for (int t = 0; t < n; t++)
            detrended[t] = double.IsNaN(trend[t]) ? double.NaN
                : (multiplicative ? y[t] / trend[t] : y[t] - trend[t]);

        var idx = new double[period];
        for (int s = 0; s < period; s++)
        {
            var vals = new List<double>();
            for (int t = s; t < n; t += period) if (!double.IsNaN(detrended[t])) vals.Add(detrended[t]);
            idx[s] = vals.Count > 0 ? vals.Average() : (multiplicative ? 1 : 0);
        }
        // Normalize: multiplicative indices average 1; additive sum 0.
        if (multiplicative) { double m = idx.Average(); if (m != 0) for (int s = 0; s < period; s++) idx[s] /= m; }
        else { double m = idx.Average(); for (int s = 0; s < period; s++) idx[s] -= m; }

        var seasonal = new double[n];
        for (int t = 0; t < n; t++) seasonal[t] = idx[t % period];
        return new DecompositionResult(multiplicative, period, trend, seasonal, idx, detrended);
    }

    // ---- helpers -----------------------------------------------------------

    private static AccuracyMeasures Accuracy(double[] actual, double[] fitted)
    {
        double mape = 0, mad = 0, msd = 0;
        int count = 0, mapeCount = 0;
        for (int i = 0; i < actual.Length; i++)
        {
            if (double.IsNaN(fitted[i])) continue;
            double e = actual[i] - fitted[i];
            mad += Math.Abs(e);
            msd += e * e;
            if (actual[i] != 0) { mape += Math.Abs(e / actual[i]); mapeCount++; }
            count++;
        }
        if (count == 0) return new AccuracyMeasures(double.NaN, double.NaN, double.NaN);
        return new AccuracyMeasures(mapeCount == 0 ? double.NaN : 100 * mape / mapeCount,
            mad / count, msd / count);
    }

    private static void RequireSeries(double[] y, int minimumLength)
    {
        ArgumentNullException.ThrowIfNull(y);
        if (y.Length < minimumLength)
            throw new ArgumentException($"Series needs at least {minimumLength} observation(s).", nameof(y));
        StatGuard.Finite(y, nameof(y));
    }

    private static void RequireForecasts(int forecasts)
    {
        if (forecasts < 0) throw new ArgumentOutOfRangeException(nameof(forecasts));
    }

    private static string Round(double v) => v.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
}
