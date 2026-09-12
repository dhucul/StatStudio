using ScottPlot;
using StatStudio.Core.Inference;
using StatStudio.Core.Statistics.Spc;

namespace StatStudio.Wpf.Graphs;

/// <summary>Builds the Phase-1 statistical graphs into a ScottPlot <see cref="Plot"/>.</summary>
internal static class Plots
{
    private static readonly Color Bg = Color.FromHex("#FFFFFF");
    private static readonly Color Fg = Color.FromHex("#1F1F1F");
    private static readonly Color GridLine = Color.FromHex("#E2E2E2");
    private static readonly Color Accent = Color.FromHex("#0A66C2");

    private static readonly Color[] Palette =
    {
        Color.FromHex("#2E9BD6"), Color.FromHex("#E0900F"), Color.FromHex("#5BA832"),
        Color.FromHex("#D64550"), Color.FromHex("#8A63D2"), Color.FromHex("#1FA88C"),
    };

    public static void ApplyTheme(Plot p)
    {
        p.FigureBackground.Color = Bg;
        p.DataBackground.Color = Bg;
        p.Axes.Color(Fg);
        p.Grid.MajorLineColor = GridLine;
        p.Legend.BackgroundColor = Color.FromHex("#FFFFFF");
        p.Legend.FontColor = Fg;
        p.Legend.OutlineColor = Color.FromHex("#C8C8C8");
    }

    public static void Histogram(Plot p, string name, double[] values)
    {
        int n = values.Length;
        int binCount = Math.Max(5, (int)Math.Ceiling(Math.Sqrt(n)));
        double min = values.Min(), max = values.Max();
        if (max <= min) max = min + 1;
        double width = (max - min) / binCount;

        var counts = new double[binCount];
        foreach (var v in values)
        {
            int b = (int)((v - min) / width);
            if (b >= binCount) b = binCount - 1;
            if (b < 0) b = 0;
            counts[b]++;
        }

        var bars = new List<Bar>(binCount);
        for (int i = 0; i < binCount; i++)
        {
            bars.Add(new Bar
            {
                Position = min + width * (i + 0.5),
                Value = counts[i],
                Size = width * 0.92,
                FillColor = Accent,
                LineWidth = 0,
            });
        }
        p.Add.Bars(bars);
        p.Title($"Histogram of {name}");
        p.XLabel(name);
        p.YLabel("Frequency");
        p.Axes.Margins(bottom: 0);
    }

    public static void Boxplot(Plot p, IReadOnlyList<(string Name, double[] Values)> series)
    {
        var boxes = new List<Box>();
        var ticks = new List<Tick>();
        for (int i = 0; i < series.Count; i++)
        {
            var sorted = Quantiles.Sorted(series[i].Values);
            if (sorted.Length == 0) continue;   // First/Last below throw on an empty series
            double q1 = Quantiles.Percentile(sorted, 25);
            double med = Quantiles.Percentile(sorted, 50);
            double q3 = Quantiles.Percentile(sorted, 75);
            double iqr = q3 - q1;
            double lo = q1 - 1.5 * iqr, hi = q3 + 1.5 * iqr;
            double wMin = sorted.First(v => v >= lo);
            double wMax = sorted.Last(v => v <= hi);
            var outliers = sorted.Where(v => v < lo || v > hi).ToArray();
            if (outliers.Length > 0)
            {
                var points = p.Add.ScatterPoints(Enumerable.Repeat((double)i, outliers.Length).ToArray(), outliers);
                points.Color = Palette[i % Palette.Length];
                points.MarkerSize = 6;
            }

            boxes.Add(new Box
            {
                Position = i,
                WhiskerMin = wMin,
                BoxMin = q1,
                BoxMiddle = med,
                BoxMax = q3,
                WhiskerMax = wMax,
                FillColor = Palette[i % Palette.Length].WithAlpha(0.6),
                LineColor = Fg,
            });
            ticks.Add(new Tick(i, series[i].Name));
        }

        p.Add.Boxes(boxes);
        p.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(ticks.ToArray());
        p.Title(series.Count == 1 ? $"Boxplot of {series[0].Name}" : "Boxplot");
        p.YLabel("Value");
    }

    public static void Scatter(Plot p, string xName, string yName, double[] xs, double[] ys)
    {
        var sp = p.Add.ScatterPoints(xs, ys);
        sp.Color = Accent;
        sp.MarkerSize = 7;
        p.Title($"Scatterplot of {yName} vs {xName}");
        p.XLabel(xName);
        p.YLabel(yName);
    }

    public static void TimeSeries(Plot p, string name, double[] values)
    {
        var xs = Enumerable.Range(1, values.Length).Select(i => (double)i).ToArray();
        var line = p.Add.Scatter(xs, values);
        line.Color = Accent;
        line.MarkerSize = 5;
        line.LineWidth = 2;
        p.Title($"Time Series Plot of {name}");
        p.XLabel("Index");
        p.YLabel(name);
    }

    public static void FittedLine(Plot p, string xName, string yName, double[] xs, double[] ys,
        double intercept, double slope)
    {
        var sp = p.Add.ScatterPoints(xs, ys);
        sp.Color = Accent;
        sp.MarkerSize = 7;

        double x0 = xs.Min(), x1 = xs.Max();
        var line = p.Add.Scatter(new[] { x0, x1 }, new[] { intercept + slope * x0, intercept + slope * x1 });
        line.Color = Color.FromHex("#E0900F");
        line.MarkerSize = 0;
        line.LineWidth = 2;

        p.Title($"Fitted Line Plot: {yName} vs {xName}");
        p.XLabel(xName);
        p.YLabel(yName);
    }

    public static void ResidualVsFitted(Plot p, double[] fitted, double[] residuals)
    {
        var sp = p.Add.ScatterPoints(fitted, residuals);
        sp.Color = Accent;
        sp.MarkerSize = 6;

        var zero = p.Add.HorizontalLine(0);
        zero.Color = Color.FromHex("#888888");
        zero.LineWidth = 1;

        p.Title("Residuals vs Fitted Values");
        p.XLabel("Fitted value");
        p.YLabel("Residual");
    }

    public static void ControlChart(Plot p, SpcChart chart)
    {
        int k = chart.Values.Length;
        var xs = Enumerable.Range(1, k).Select(i => (double)i).ToArray();

        var line = p.Add.Scatter(xs, chart.Values);
        line.Color = Accent;
        line.MarkerSize = 6;
        line.LineWidth = 1.5f;

        // Center line and control limits (per-point limits draw as stepped scatter).
        var cl = p.Add.HorizontalLine(chart.Center);
        cl.Color = Color.FromHex("#1E9E4A");
        cl.LineWidth = 1.5f;

        var ucl = p.Add.Scatter(xs, chart.Ucl);
        ucl.Color = Color.FromHex("#D64550"); ucl.MarkerSize = 0; ucl.LineWidth = 1.5f; ucl.LinePattern = LinePattern.Dashed;
        var lcl = p.Add.Scatter(xs, chart.Lcl);
        lcl.Color = Color.FromHex("#D64550"); lcl.MarkerSize = 0; lcl.LineWidth = 1.5f; lcl.LinePattern = LinePattern.Dashed;

        // Highlight out-of-control points in red.
        var oocX = new List<double>();
        var oocY = new List<double>();
        for (int i = 0; i < k; i++)
            if (chart.OutOfControl[i]) { oocX.Add(xs[i]); oocY.Add(chart.Values[i]); }
        if (oocX.Count > 0)
        {
            var bad = p.Add.ScatterPoints(oocX.ToArray(), oocY.ToArray());
            bad.Color = Color.FromHex("#D64550");
            bad.MarkerSize = 10;
        }

        p.Title(chart.Title);
        p.XLabel("Sample");
        p.YLabel(chart.YLabel);
    }

    /// <param name="title">
    /// Chart title. Defaults to a generic time-series caption; callers that are plotting a
    /// specific method (trend, moving average, Winters, decomposition) pass their own so the
    /// chart no longer disagrees with the window it sits in.
    /// </param>
    public static void TimeSeriesFit(Plot p, string name, double[] actual, double[] fitted, double[] forecasts,
        string? title = null)
    {
        int n = actual.Length;
        var xs = Enumerable.Range(1, n).Select(i => (double)i).ToArray();
        var act = p.Add.Scatter(xs, actual);
        act.Color = Accent; act.MarkerSize = 5; act.LineWidth = 1.5f; act.LegendText = "Actual";

        var fx = new List<double>(); var fy = new List<double>();
        for (int i = 0; i < n; i++) if (!double.IsNaN(fitted[i])) { fx.Add(xs[i]); fy.Add(fitted[i]); }
        if (fx.Count > 0)
        {
            var fit = p.Add.Scatter(fx.ToArray(), fy.ToArray());
            fit.Color = Color.FromHex("#E0900F"); fit.MarkerSize = 0; fit.LineWidth = 2; fit.LegendText = "Fitted";
        }
        if (forecasts.Length > 0)
        {
            var fxs = Enumerable.Range(n + 1, forecasts.Length).Select(i => (double)i).ToArray();
            var fcl = p.Add.Scatter(fxs, forecasts);
            fcl.Color = Color.FromHex("#5BA832"); fcl.MarkerSize = 5; fcl.LineWidth = 2;
            fcl.LinePattern = LinePattern.Dashed; fcl.LegendText = "Forecast";
        }
        p.ShowLegend();
        p.Title(title ?? $"Time Series Plot of {name}");
        p.XLabel("Period"); p.YLabel(name);
    }

    /// <param name="title">Chart title; defaults to the ARIMA caption. SARIMA callers pass their own.</param>
    public static void ForecastPlot(Plot p, string name, double[] actual, double[] forecasts, double[] lower, double[] upper,
        string? title = null)
    {
        int n = actual.Length;
        var xs = Enumerable.Range(1, n).Select(i => (double)i).ToArray();
        var act = p.Add.Scatter(xs, actual);
        act.Color = Accent; act.MarkerSize = 4; act.LineWidth = 1.5f; act.LegendText = "Actual";

        if (forecasts.Length > 0)
        {
            var fxs = Enumerable.Range(n + 1, forecasts.Length).Select(i => (double)i).ToArray();
            var band = p.Add.Scatter(fxs, upper);
            band.Color = Color.FromHex("#D64550"); band.MarkerSize = 0; band.LineWidth = 1; band.LinePattern = LinePattern.Dotted; band.LegendText = "95% CI";
            var bandLo = p.Add.Scatter(fxs, lower);
            bandLo.Color = Color.FromHex("#D64550"); bandLo.MarkerSize = 0; bandLo.LineWidth = 1; bandLo.LinePattern = LinePattern.Dotted;
            var fc = p.Add.Scatter(fxs, forecasts);
            fc.Color = Color.FromHex("#5BA832"); fc.MarkerSize = 5; fc.LineWidth = 2; fc.LinePattern = LinePattern.Dashed; fc.LegendText = "Forecast";
        }
        p.ShowLegend();
        p.Title(title ?? $"ARIMA Forecast of {name}");
        p.XLabel("Period"); p.YLabel(name);
    }

    public static void Acf(Plot p, string title, double[] values, int n, string yLabel)
    {
        var bars = new List<Bar>();
        for (int k = 1; k < values.Length; k++)
            bars.Add(new Bar { Position = k, Value = values[k], Size = 0.25, FillColor = Accent, LineWidth = 0 });
        p.Add.Bars(bars);

        double bound = 1.96 / Math.Sqrt(n);
        foreach (var b in new[] { bound, -bound })
        {
            var ln = p.Add.HorizontalLine(b);
            ln.Color = Color.FromHex("#D64550"); ln.LineWidth = 1; ln.LinePattern = LinePattern.Dashed;
        }
        var zero = p.Add.HorizontalLine(0); zero.Color = Fg; zero.LineWidth = 1;
        p.Title(title); p.XLabel("Lag"); p.YLabel(yLabel);
    }

    public static void LabeledBars(Plot p, string title, string xLabel, string yLabel,
        IReadOnlyList<string> labels, IReadOnlyList<double> values)
    {
        var bars = new List<Bar>();
        var ticks = new List<Tick>();
        for (int i = 0; i < values.Count; i++)
        {
            bars.Add(new Bar { Position = i, Value = values[i], Size = 0.7, FillColor = Accent, LineWidth = 0 });
            ticks.Add(new Tick(i, labels[i]));
        }
        p.Add.Bars(bars);
        p.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(ticks.ToArray());
        p.Title(title);
        p.XLabel(xLabel);
        p.YLabel(yLabel);
        p.Axes.Margins(bottom: 0);
    }

    /// <param name="censored">
    /// Right-censoring flags aligned to <paramref name="times"/>. Censored units are not plotted —
    /// they never failed — but they still shift the plotting positions of the failures that follow
    /// them, via Johnson's rank adjustment. Without that the points would spread as though nothing
    /// had been censored and would no longer straddle the fitted line.
    /// </param>
    public static void WeibullPlot(Plot p, string name, double[] times, double beta, double eta,
        bool[]? censored = null)
    {
        int n = times.Length;
        var flags = censored ?? new bool[n];
        var order = Enumerable.Range(0, n).OrderBy(i => times[i]).ToArray();

        var xs = new List<double>(n);
        var ys = new List<double>(n);
        double adjustedRank = 0;
        for (int k = 0; k < n; k++)
        {
            int i = order[k];
            if (flags[i]) continue;                       // survivor: no failure to plot
            int remaining = n - k;                        // this unit plus everything after it
            adjustedRank += (n + 1 - adjustedRank) / (remaining + 1);
            double prob = (adjustedRank - 0.3) / (n + 0.4);   // median rank
            xs.Add(Math.Log(times[i]));
            ys.Add(Math.Log(-Math.Log(1 - prob)));
        }
        if (xs.Count == 0) return;

        var sp = p.Add.ScatterPoints(xs.ToArray(), ys.ToArray());
        sp.Color = Accent; sp.MarkerSize = 6;
        if (flags.Any(f => f)) sp.LegendText = $"Failures ({xs.Count} of {n})";

        double x0 = xs[0], x1 = xs[^1];
        var line = p.Add.Scatter(new[] { x0, x1 }, new[] { beta * (x0 - Math.Log(eta)), beta * (x1 - Math.Log(eta)) });
        line.Color = Color.FromHex("#E0900F"); line.MarkerSize = 0; line.LineWidth = 2;
        if (flags.Any(f => f)) { line.LegendText = "MLE fit"; p.ShowLegend(); }

        p.Title($"Weibull Probability Plot of {name}");
        p.XLabel("ln(time)"); p.YLabel("ln(-ln(1 - p))");
    }

    public static void StepSurvival(Plot p, string name, double[] times, double[] survival)
    {
        var xs = new List<double> { 0 };
        var ys = new List<double> { 1 };
        double prev = 1;
        for (int i = 0; i < times.Length; i++)
        {
            xs.Add(times[i]); ys.Add(prev);
            xs.Add(times[i]); ys.Add(survival[i]);
            prev = survival[i];
        }
        var line = p.Add.Scatter(xs.ToArray(), ys.ToArray());
        line.Color = Accent; line.MarkerSize = 0; line.LineWidth = 2;
        p.Title($"Kaplan-Meier Survival of {name}");
        p.XLabel("Time"); p.YLabel("Survival probability");
        p.Axes.SetLimitsY(0, 1.05);
    }

    public static void Scree(Plot p, double[] eigenvalues)
    {
        var xs = Enumerable.Range(1, eigenvalues.Length).Select(i => (double)i).ToArray();
        var line = p.Add.Scatter(xs, eigenvalues);
        line.Color = Accent; line.MarkerSize = 7; line.LineWidth = 2;
        var one = p.Add.HorizontalLine(1.0);
        one.Color = Color.FromHex("#D64550"); one.LineWidth = 1; one.LinePattern = LinePattern.Dashed;
        p.Title("Scree Plot");
        p.XLabel("Component number"); p.YLabel("Eigenvalue");
    }

    public static void ClusterScatter(Plot p, string xName, string yName, double[][] data, int[] assign, int k)
    {
        for (int c = 0; c < k; c++)
        {
            var xs = new List<double>(); var ys = new List<double>();
            for (int i = 0; i < data.Length; i++) if (assign[i] == c) { xs.Add(data[i][0]); ys.Add(data[i][1]); }
            if (xs.Count == 0) continue;
            var sp = p.Add.ScatterPoints(xs.ToArray(), ys.ToArray());
            sp.Color = Palette[c % Palette.Length]; sp.MarkerSize = 8; sp.LegendText = $"Cluster {c + 1}";
        }
        p.ShowLegend();
        p.Title("K-Means Clusters");
        p.XLabel(xName); p.YLabel(yName);
    }

    public static void CapabilityHistogram(Plot p, string name, double[] values, double? lsl, double? usl, double? target)
    {
        Histogram(p, name, values);
        p.Title($"Process Capability of {name}");
        void Spec(double? v, string hex)
        {
            if (v is null) return;
            var ln = p.Add.VerticalLine(v.Value);
            ln.Color = Color.FromHex(hex);
            ln.LineWidth = 2;
        }
        Spec(lsl, "#D64550");
        Spec(usl, "#D64550");
        Spec(target, "#1E9E4A");
    }

    public static void ProbabilityPlot(Plot p, string name, double[] values)
    {
        var (sorted, scores) = NormalScores.Compute(values);
        var sp = p.Add.ScatterPoints(sorted, scores);
        sp.Color = Accent;
        sp.MarkerSize = 6;

        // Reference line: z = (x − mean)/sd.
        double mean = sorted.Average();
        double sd = Math.Sqrt(sorted.Sum(v => (v - mean) * (v - mean)) / Math.Max(1, sorted.Length - 1));
        if (sd > 0)
        {
            double x0 = sorted[0], x1 = sorted[^1];
            var fit = p.Add.Scatter(new[] { x0, x1 }, new[] { (x0 - mean) / sd, (x1 - mean) / sd });
            fit.Color = Color.FromHex("#E0900F");
            fit.MarkerSize = 0;
            fit.LineWidth = 2;
        }

        p.Title($"Probability Plot of {name} (Normal)");
        p.XLabel(name);
        p.YLabel("Normal score");
    }
}
