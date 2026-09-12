using System.Data;
using System.Windows;
using System.Windows.Controls;
using StatStudio.Core.Statistics;
using StatStudio.Core.Statistics.Spc;
using StatStudio.Wpf.Dialogs;
using StatStudio.Wpf.Graphs;
using CoreData = StatStudio.Core.Data;

namespace StatStudio.Wpf;

public partial class MainWindow : Window
{
    private DataTable _table = null!;
    private bool _dirty;

    public MainWindow()
    {
        InitializeComponent();
        Sheet.LoadingRow += (_, e) => e.Row.Header = (e.Row.GetIndex() + 1).ToString();
        PopulateSampleData();
        NewWorksheet();
        Log("StatStudio — ready.");
        Log("Open a CSV (File ▸ Open Data) or type data into the worksheet, then run an analysis from the Stat or Graph menu.");
        Log("");
        // Run startup args after the window is shown (owned graph windows require a shown owner).
        Loaded += (_, _) => ProcessArgs(Environment.GetCommandLineArgs());
        Closing += OnClosing;
    }

    // ---- startup args / demo (used for screenshots and quick checks) --------

    private void ProcessArgs(string[] args)
    {
        for (int i = 1; i < args.Length; i++)
        {
            var a = args[i].ToLowerInvariant();
            switch (a)
            {
                case "--demo":
                    var ws = BuildDemo(); RunDescriptives(ws, ws.NumericColumns().Select(c => c.Name));
                    DemoGraphs(ws); break;
                case "--shot-descriptives":
                    BuildDemo(); var cw = CurrentWorksheet();
                    RunDescriptives(cw, cw.NumericColumns().Select(c => c.Name)); break;
                case "--shot-histogram": var w2 = BuildDemo(); ShowGraph("Histogram of Height", p => Plots.Histogram(p, "Height", w2.Find("Height")!.NumericValues())); break;
                case "--shot-boxplot":
                    var w3 = BuildDemo(); ShowGraph("Boxplot", p => Plots.Boxplot(p,
                    new[] { ("Height", w3.Find("Height")!.NumericValues()), ("Weight", w3.Find("Weight")!.NumericValues()) })); break;
                case "--shot-scatter":
                    var w4 = BuildDemo(); var (sx, sy) = Columns.Pairwise(w4.Find("Height")!, w4.Find("Weight")!);
                    ShowGraph("Scatterplot of Weight vs Height", p => Plots.Scatter(p, "Height", "Weight", sx, sy)); break;
                case "--shot-probplot": var w5 = BuildDemo(); ShowGraph("Probability Plot of Height", p => Plots.ProbabilityPlot(p, "Height", w5.Find("Height")!.NumericValues())); break;
                case "--shot-ttest":
                    var w6 = BuildDemo(); var hv = w6.Find("Height")!.NumericValues();
                    OutputRaw(HypothesisFormatters.OneSampleT(HypothesisTests.OneSampleT(hv, 170, 0.95, Alternative.TwoSided), "Height", 170)); break;
                case "--shot-regression":
                    var wr = BuildDemo();
                    var (rx, ry) = Columns.Pairwise(wr.Find("Height")!, wr.Find("Weight")!);
                    var rr = Regression.SimpleLinear(rx, ry, "Height", "Weight");
                    OutputRaw(RegressionFormatter.Format(rr));
                    ShowGraph("Fitted Line Plot of Weight vs Height",
                        p => Plots.FittedLine(p, "Height", "Weight", rx, ry, rr.Coefficients[0], rr.Coefficients[1])); break;
                case "--shot-imr":
                    var wi = BuildDemo(); var iv = wi.Find("Height")!.NumericValues();
                    var (ic, mrc) = ControlCharts.IMR(iv);
                    OutputRaw(SpcFormatter.Pair("I-MR Chart", ic, mrc));
                    ShowGraph(ic.Title, p => Plots.ControlChart(p, ic)); break;
                case "--shot-capability":
                    var wc = BuildDemo(); var cv = wc.Find("Height")!.NumericValues();
                    var cap = Capability.FromIndividuals(cv, 150, 190, 170);
                    OutputRaw(SpcFormatter.Capability(cap));
                    ShowGraph("Process Capability of Height",
                        p => Plots.CapabilityHistogram(p, "Height", cv, 150, 190, 170)); break;
                case "--shot-sample":
                    LoadWorksheet(CoreData.SampleData.All.First(d => d.Name.StartsWith("Flower")).Build());
                    Log("Loaded sample dataset: Flower Measurements.");
                    break;
                case "--shot-dlg":
                    new Dialogs.ColumnPickerWindow("DialogProbe", "Variables (numeric):",
                        new[] { "Height", "Weight", "Group" })
                    { Owner = this }.Show();
                    break;
                case "--shot-bayes":
                    OutputRaw(BayesFormatters.Proportion(Bayes.Proportion(8, 10, 1, 1), "Sample"));
                    OutputRaw(MixedFormatters.OneWayRandom(MixedModel.OneWayRandom(new (string, double[])[]
                    {
                        ("G1", new double[] { 1, 2, 3 }),
                        ("G2", new double[] { 4, 5, 6 }),
                        ("G3", new double[] { 7, 8, 9 }),
                    }), "Yield", "Group"));
                    break;
                case "--shot-calc":
                    BuildDemo();
                    var cwx = CurrentWorksheet();
                    var calc = CoreData.Calculator.Evaluate("Height + Weight", cwx);
                    var newCol = cwx.AddColumn("Total");
                    foreach (var cval in calc) newCol.Add(cval.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
                    LoadWorksheet(cwx);
                    Log("Calculated 'Total' = Height + Weight.");
                    Log("Also: MEAN(Height) = " + CoreData.Calculator.Evaluate("MEAN(Height)", cwx)[0].ToString("0.###"));
                    break;
                case "--shot-reliability":
                    int rn = 30;
                    var wt = new double[rn];
                    for (int q = 1; q <= rn; q++) { double pr = (q - 0.3) / (rn + 0.4); wt[q - 1] = 100 * Math.Pow(-Math.Log(1 - pr), 0.5); }
                    var wfit = Reliability.FitWeibull(wt);
                    OutputRaw(ReliabilityFormatters.DistributionFit(wfit, "Life"));
                    ShowGraph("Weibull Plot of Life",
                        pp => Plots.WeibullPlot(pp, "Life", wt, wfit.Parameters[0].Value, wfit.Parameters[1].Value));
                    break;
                case "--shot-sarima":
                    var sser = Enumerable.Range(0, 48).Select(tt => 100 + 0.8 * tt
                        + 15 * Math.Sin(tt * 2 * Math.PI / 12) + 4 * Math.Sin(tt * 0.7)).ToArray();
                    var sr = Sarima.Fit(sser, 1, 1, 1, 0, 1, 1, 12, 12, includeConstant: false);
                    OutputRaw(TimeSeriesFormatters.Sarima(sr, "Monthly"));
                    ShowGraph("SARIMA Forecast of Monthly",
                        pp => Plots.ForecastPlot(pp, "Monthly", sser, sr.Forecasts, sr.ForecastLower, sr.ForecastUpper));
                    break;
                case "--shot-arima":
                    var aser = Enumerable.Range(0, 40).Select(tt => 50 + 1.5 * tt + 6 * Math.Sin(tt * Math.PI / 6)
                        + 3 * Math.Sin(tt * 0.9)).ToArray();
                    var ar = Arima.Fit(aser, 2, 1, 1, 8, includeConstant: true);
                    OutputRaw(TimeSeriesFormatters.Arima(ar, "Series"));
                    ShowGraph("ARIMA Forecast of Series",
                        pp => Plots.ForecastPlot(pp, "Series", aser, ar.Forecasts, ar.ForecastLower, ar.ForecastUpper));
                    break;
                case "--shot-gagerr":
                    var gm = new double[] { 10, 12, 11, 13, 20, 22, 21, 23 };
                    var gp = new[] { "P1", "P1", "P1", "P1", "P2", "P2", "P2", "P2" };
                    var go = new[] { "O1", "O1", "O2", "O2", "O1", "O1", "O2", "O2" };
                    var gr = GageRR.Analyze(gm, gp, go);
                    OutputRaw(DoeFormatters.GageRR(gr));
                    break;
                case "--shot-doe":
                    var dA = new double[] { -1, -1, 1, 1, -1, -1, 1, 1 };
                    var dB = new double[] { -1, 1, -1, 1, -1, 1, -1, 1 };
                    var dnz = new double[] { 0.2, -0.2, 0.1, -0.1, -0.2, 0.2, -0.1, 0.1 };
                    var dY = new double[8];
                    for (int q = 0; q < 8; q++) dY[q] = 10 + 3 * dA[q] + 2 * dB[q] + 1 * dA[q] * dB[q] + dnz[q];
                    var dr = FactorialAnalysis.Analyze(dY, new[] { dA, dB }, new[] { "A", "B" }, "Y");
                    OutputRaw(DoeFormatters.Factorial(dr));
                    var de = dr.Terms.Where(t => double.IsFinite(t.Effect))
                        .OrderByDescending(t => Math.Abs(double.IsNaN(t.T) ? t.Effect : t.T)).ToList();
                    ShowGraph("Pareto of Effects", p => Plots.LabeledBars(p, "Pareto of Effects", "Term", "|Standardized effect|",
                        de.Select(t => t.Name).ToList(), de.Select(t => Math.Abs(double.IsNaN(t.T) ? t.Effect : t.T)).ToList()));
                    break;
                case "--shot-pca":
                    var wp = BuildDemo();
                    var pcaCols = new[] { wp.Find("Height")!, wp.Find("Weight")! };
                    var pcaRows = Columns.Rows(pcaCols).ToArray();
                    var pcaR = Pca.Compute(pcaRows, new[] { "Height", "Weight" }, true);
                    OutputRaw(MultivariateFormatters.Pca(pcaR));
                    OutputRaw(MultivariateFormatters.Power("1-Sample t", "sample size", 0.05, Alternative.TwoSided,
                        "d = 0.5", Power.OneSampleTSampleSize(0.8, 0.5, 0.05, Alternative.TwoSided), 0.8));
                    OutputRaw(MultivariateFormatters.Fisher(FishersExact.Test(3, 1, 1, 3)));
                    ShowGraph("Scree Plot", p => Plots.Scree(p, pcaR.Eigenvalues)); break;
                case "--shot-trend":
                    var inv = System.Globalization.CultureInfo.InvariantCulture;
                    var sales = Enumerable.Range(0, 24).Select(t => 100 + 2.0 * t + 10 * Math.Sin(t * Math.PI / 6)).ToArray();
                    var wsS = new CoreData.Worksheet { Name = "TS" };
                    var scol = wsS.AddColumn("Sales");
                    foreach (var s in sales) scol.Add(s.ToString("F2", inv));
                    LoadWorksheet(wsS);
                    var tr = TimeSeries.LinearTrend(sales, 4);
                    OutputRaw(TimeSeriesFormatters.Trend(tr, "Sales", sales.Length));
                    OutputRaw(TimeSeriesFormatters.Acf(TimeSeries.Autocorrelation(sales, 12), "Sales", false));
                    ShowGraph("Trend Analysis of Sales", p => Plots.TimeSeriesFit(p, "Sales", sales, tr.Fitted, tr.Forecasts));
                    break;
                case "--shot-tukey":
                    var wtk = BuildAnovaDemo();
                    var tkGroups = new (string, double[])[]
                    {
                        ("Method A", wtk.Find("Method A")!.NumericValues()),
                        ("Method B", wtk.Find("Method B")!.NumericValues()),
                        ("Method C", wtk.Find("Method C")!.NumericValues()),
                    };
                    OutputRaw(AdvancedFormatters.Tukey(AnovaExtensions.Tukey(tkGroups))); break;
                case "--shot-logistic":
                    var lx = Enumerable.Repeat(0.0, 10).Concat(Enumerable.Repeat(1.0, 10)).ToArray();
                    var ly = new double[] { 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0 };
                    OutputRaw(AdvancedFormatters.Logistic(Logistic.Fit(ly, new[] { lx }, new[] { "x" }, "y"))); break;
                case "--shot-correlation":
                    var wco = BuildAnovaDemo();
                    OutputRaw(NonparametricFormatters.Correlation(Correlation.Matrix(
                        new[] { wco.Find("Method A")!, wco.Find("Method B")!, wco.Find("Method C")! }, false)));
                    OutputRaw(NonparametricFormatters.KruskalWallis(Nonparametric.KruskalWallis(new (string, double[])[]
                    {
                        ("Method A", wco.Find("Method A")!.NumericValues()),
                        ("Method B", wco.Find("Method B")!.NumericValues()),
                        ("Method C", wco.Find("Method C")!.NumericValues()),
                    }), "Yield", "Method")); break;
                case "--shot-anova":
                    var wa = BuildAnovaDemo();
                    OutputRaw(AnovaFormatter.OneWay(Anova.OneWay(new (string, double[])[]
                    {
                        ("Method A", wa.Find("Method A")!.NumericValues()),
                        ("Method B", wa.Find("Method B")!.NumericValues()),
                        ("Method C", wa.Find("Method C")!.NumericValues()),
                    }), "Method", "Yield")); break;
                case "--shot-menu":
                    StatMenu.IsSubmenuOpen = true;
                    if (StatMenu.Items.Count > 0 && StatMenu.Items[0] is MenuItem first) first.IsSubmenuOpen = true;
                    break;
                case "--data" when i + 1 < args.Length:
                    try { LoadWorksheet(CoreData.WorksheetIo.ReadCsv(args[++i])); }
                    catch (Exception ex) { Log("data load failed: " + ex.Message); }
                    break;
            }
        }
    }

    private CoreData.Worksheet BuildDemo()
    {
        var ws = new CoreData.Worksheet { Name = "Demo" };
        var height = ws.AddColumn("Height");
        var weight = ws.AddColumn("Weight");
        var group = ws.AddColumn("Group");
        var rnd = new Random(12345);
        for (int i = 0; i < 40; i++)
        {
            double h = 170 + NextGaussian(rnd) * 8;
            double w = 0.9 * h - 90 + NextGaussian(rnd) * 5;
            height.Add(h.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
            weight.Add(w.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
            group.Add(i % 2 == 0 ? "A" : "B");
        }
        LoadWorksheet(ws);
        return ws;
    }

    private CoreData.Worksheet BuildAnovaDemo()
    {
        var ws = new CoreData.Worksheet { Name = "Yields" };
        var a = ws.AddColumn("Method A");
        var b = ws.AddColumn("Method B");
        var c = ws.AddColumn("Method C");
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var rnd = new Random(7);
        for (int i = 0; i < 10; i++)
        {
            a.Add((20 + NextGaussian(rnd) * 2).ToString("F2", inv));
            b.Add((23 + NextGaussian(rnd) * 2).ToString("F2", inv));
            c.Add((21 + NextGaussian(rnd) * 2).ToString("F2", inv));
        }
        LoadWorksheet(ws);
        return ws;
    }

    private void DemoGraphs(CoreData.Worksheet ws)
    {
        var hv = ws.Find("Height")!.NumericValues();
        ShowGraph("Histogram of Height", p => Plots.Histogram(p, "Height", hv));
        ShowGraph("Boxplot", p => Plots.Boxplot(p,
            new[] { ("Height", hv), ("Weight", ws.Find("Weight")!.NumericValues()) }));
    }

    private static double NextGaussian(Random rnd)
    {
        double u1 = 1.0 - rnd.NextDouble();
        double u2 = 1.0 - rnd.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }

    // ---- worksheet plumbing -----------------------------------------------

    private void PopulateSampleData()
    {
        foreach (var ds in CoreData.SampleData.All)
        {
            var item = new MenuItem { Header = ds.Name, ToolTip = ds.Description };
            var build = ds.Build;
            var name = ds.Name;
            item.Click += (_, _) =>
            {
                if (!ConfirmDiscardChanges()) return;
                try { LoadWorksheet(build()); Log($"Loaded sample dataset: {name}."); }
                catch (Exception ex) { ShowError("Sample data", ex); }
            };
            SampleDataMenu.Items.Add(item);
        }
    }

    private void NewWorksheet()
    {
        SetTable(WorksheetGrid.NewEmpty(), markDirty: false);
    }

    private void LoadWorksheet(CoreData.Worksheet ws, bool markDirty = false)
    {
        var table = WorksheetGrid.ToDataTable(ws);
        for (int i = 0; i < 5; i++) table.Rows.Add(table.NewRow()); // room to type
        SetTable(table, markDirty);
    }

    private void SetTable(DataTable table, bool markDirty)
    {
        if (_table is not null)
        {
            _table.ColumnChanged -= OnTableChanged;
            _table.RowChanged -= OnTableRowChanged;
            _table.RowDeleted -= OnTableRowChanged;
        }

        _table = table;
        _table.ColumnChanged += OnTableChanged;
        _table.RowChanged += OnTableRowChanged;
        _table.RowDeleted += OnTableRowChanged;
        Sheet.ItemsSource = _table.DefaultView;
        _dirty = markDirty;
        UpdateWorksheetHeader();
        // ToWorksheet walks every cell of the grid; the navigator and the dimension readout used
        // to run it once each, converting the whole table twice on every load.
        var snapshot = WorksheetGrid.ToWorksheet(_table);
        RefreshNavigator(snapshot);
        UpdateDims(snapshot);
    }

    private void OnTableChanged(object? sender, DataColumnChangeEventArgs e) { MarkDirty(); QueueWorksheetRefresh(); }
    private void OnTableRowChanged(object? sender, DataRowChangeEventArgs e) { MarkDirty(); QueueWorksheetRefresh(); }
    private bool _refreshPending;

    private void QueueWorksheetRefresh()
    {
        if (_refreshPending || _closed) return;
        _refreshPending = true;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
        {
            _refreshPending = false;
            if (_closed) return;
            var snapshot = WorksheetGrid.ToWorksheet(_table);
            RefreshNavigator(snapshot);
            UpdateDims(snapshot);
        }));
    }

    private void MarkDirty()
    {
        if (_dirty) return;
        _dirty = true;
        UpdateWorksheetHeader();
    }

    private void MarkClean()
    {
        _dirty = false;
        UpdateWorksheetHeader();
    }

    private void UpdateWorksheetHeader() =>
        WorksheetHeader.Text = $"Worksheet: {_table.TableName}{(_dirty ? " *" : "")}";

    private bool ConfirmDiscardChanges()
    {
        Sheet.CommitEdit(DataGridEditingUnit.Row, true);
        if (!_dirty) return true;
        return MessageBox.Show(
            "The current worksheet has unsaved changes. Discard them?",
            "Unsaved changes",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!ConfirmDiscardChanges()) { e.Cancel = true; return; }
        // A model fit can outlive the window; signal it so its continuation stops before
        // touching torn-down controls.
        _closed = true;
        _work.Cancel();
    }

    private readonly BackgroundWork _work = new();
    private bool _closed;

    private void OnCancelWork(object sender, RoutedEventArgs e)
    {
        _work.Cancel();
        if (_work.IsRunning)
        {
            CancelWorkButton.IsEnabled = false;
            StatusText.Text = "Cancelling…";
        }
    }

    private async Task<T> RunWorkAsync<T>(Func<CancellationToken, T> calculate, bool discardCancelledResult = true)
    {
        var result = await _work.RunAsync(calculate, busy =>
        {
            if (_closed) return;
            MainMenu.IsEnabled = !busy;
            Sheet.IsEnabled = !busy;
            CancelWorkButton.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            CancelWorkButton.IsEnabled = busy;
            StatusText.Text = busy ? "Working…" : "Ready";
        }, discardCancelledResult);
        if (_closed) throw new OperationCanceledException();
        return result;
    }

    /// <summary>Current grid contents as a Core worksheet (commits any in-progress edit first).</summary>
    private CoreData.Worksheet CurrentWorksheet()
    {
        Sheet.CommitEdit(DataGridEditingUnit.Row, true);
        return WorksheetGrid.ToWorksheet(_table);
    }

    private void RefreshNavigator(CoreData.Worksheet ws)
    {
        NavList.Items.Clear();
        NavList.Items.Add(ws.Name);
        foreach (var c in ws.Columns)
        {
            int n = c.Count - c.MissingCount();
            NavList.Items.Add($"   {c.Name}  ({(c.Type == CoreData.ColumnType.Numeric && c.LooksNumeric() ? "num" : c.Type == CoreData.ColumnType.DateTime ? "date" : "text")}, n={n})");
        }
    }

    private void UpdateDims(CoreData.Worksheet ws) =>
        DimsText.Text = $"{ws.ColumnCount} cols × {ws.RowCount} rows";

    // ---- session output ----------------------------------------------------

    /// <summary>Session text is capped; past this many characters the oldest half is dropped.</summary>
    private const int SessionCharacterCap = 512_000;

    internal void Log(string text) => Append(text + Environment.NewLine);

    /// <summary>
    /// Single append point for the Session pane. ScrollToEnd forces a layout pass, so it runs once
    /// per block rather than once per line, and the buffer is bounded — an unbounded TextBox made
    /// every later append re-lay out megabytes of text.
    /// </summary>
    private void Append(string block)
    {
        if (_closed) return;
        Session.AppendText(block);
        if (Session.Text.Length > SessionCharacterCap)
            Session.Text = Session.Text[^(SessionCharacterCap / 2)..];
        Session.ScrollToEnd();
    }

    /// <summary>Append a titled output block, Minitab-style.</summary>
    internal void Output(string title, string body)
    {
        string nl = Environment.NewLine;
        Append($"{nl}{title}{nl}{new string('─', Math.Max(title.Length, 8))}{nl}{body.TrimEnd()}{nl}{nl}");
        StatusText.Text = title;
    }

    /// <summary>Append a pre-formatted block whose first line is its own title.</summary>
    internal void OutputRaw(string body)
    {
        string nl = Environment.NewLine;
        Append($"{nl}{body.TrimEnd()}{nl}{nl}");
        var first = body.Split('\n').FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(first)) StatusText.Text = first.Trim();
    }

    private void ShowError(string title, Exception ex)
    {
        if (_closed) return;
        if (ex is OperationCanceledException) { Log(title + ": cancelled."); return; }
        Log($"ERROR — {title}: {ex.Message}");
        MessageBox.Show(ex.Message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    // ---- analysis helpers --------------------------------------------------

    private List<string> NumericColumnNames(CoreData.Worksheet ws) =>
        ws.NumericColumns().Select(c => c.Name).ToList();

    private bool RequireNumeric(CoreData.Worksheet ws, int min, out List<string> names)
    {
        names = NumericColumnNames(ws);
        if (names.Count >= min) return true;
        Log(min == 1
            ? "No numeric columns to analyze. Enter or import numeric data first."
            : $"Need at least {min} numeric columns for this analysis.");
        return false;
    }

    private void ShowGraph(string title, Action<ScottPlot.Plot> build)
    {
        var g = new GraphWindow(title) { Owner = this };
        try
        {
            Plots.ApplyTheme(g.Plot);
            build(g.Plot);
            g.Render();
            g.Show();
        }
        catch (Exception ex)
        {
            // WPF registers a Window in Application.Windows at construction, so an un-shown one
            // leaks for the life of the process unless it is closed explicitly.
            g.Close();
            Log($"Graph '{title}' could not be drawn: {ex.Message}");
        }
    }

    // ---- File menu ---------------------------------------------------------

    private void OnNewWorksheet(object sender, RoutedEventArgs e)
    {
        if (ConfirmDiscardChanges()) NewWorksheet();
    }

    private async void OnOpenCsv(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Data files|*.csv;*.tsv;*.txt;*.xlsx|CSV/Text|*.csv;*.tsv;*.txt|Excel|*.xlsx|All files|*.*",
            Title = "Open data",
        };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.FileName };
        string fileName = selection.FileName;
        bool excel = fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase);
        var options = new ImportOptionsWindow(excel) { Owner = this };
        if (options.ShowDialog() != true) return;
        bool hasHeader = options.HasHeader, decodeGuards = options.DecodeFormulaGuards;
        // Ask before reading: a large workbook takes real time, and discarding it afterwards
        // means the user waited for a file that was never going to be loaded.
        if (!ConfirmDiscardChanges()) return;
        try
        {
            var ws = await RunWorkAsync(ct => excel
                ? CoreData.WorksheetIo.ReadXlsx(fileName, hasHeader, ct)
                : CoreData.WorksheetIo.ReadCsv(fileName, hasHeader, decodeGuards, ct));
            LoadWorksheet(ws);
            Log($"Opened '{System.IO.Path.GetFileName(selection.FileName)}' — {ws.ColumnCount} columns, {ws.RowCount} rows.");
        }
        catch (Exception ex) { ShowError("Open failed", ex); }
    }

    private async void OnSaveCsv(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV (comma)|*.csv|TSV (tab)|*.tsv|Excel|*.xlsx",
            FileName = _table.TableName + ".csv",
            Title = "Save worksheet",
        };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.FileName };
        try
        {
            var ws = CurrentWorksheet();
            string fileName = selection.FileName;
            await RunWorkAsync(ct =>
            {
                if (fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                    CoreData.WorksheetIo.WriteXlsx(ws, fileName, ct);
                else
                    CoreData.WorksheetIo.WriteCsv(ws, fileName,
                        fileName.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase) ? '\t' : ',', ct);
                return true;
            }, discardCancelledResult: false);
            MarkClean();
            Log($"Saved worksheet to '{System.IO.Path.GetFileName(selection.FileName)}'.");
        }
        catch (Exception ex) { ShowError("Save failed", ex); }
    }

    private async void OnOpenProject(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "StatStudio project|*.ssproj|All files|*.*",
            Title = "Open project",
        };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.FileName };
        if (!ConfirmDiscardChanges()) return;
        try
        {
            string fileName = selection.FileName;
            var ws = await RunWorkAsync(ct => CoreData.ProjectStore.Load(fileName, ct));
            LoadWorksheet(ws);
            Log($"Opened project '{System.IO.Path.GetFileName(selection.FileName)}'.");
        }
        catch (Exception ex) { ShowError("Open project failed", ex); }
    }

    private async void OnSaveProject(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "StatStudio project|*.ssproj",
            FileName = _table.TableName + ".ssproj",
            Title = "Save project",
        };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.FileName };
        try
        {
            var snapshot = CurrentWorksheet();
            string fileName = selection.FileName;
            await RunWorkAsync(ct => { CoreData.ProjectStore.Save(snapshot, fileName, ct); return true; }, discardCancelledResult: false);
            MarkClean();
            Log($"Saved project to '{System.IO.Path.GetFileName(selection.FileName)}'.");
        }
        catch (Exception ex) { ShowError("Save project failed", ex); }
    }

    private void OnExit(object sender, RoutedEventArgs e) => Close();

    // ---- Stat menu (filled in over phases 1-4) -----------------------------

    private void OnDescriptives(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 1, out var numeric)) return;
        var dlg = new ColumnPickerWindow("Display Descriptive Statistics",
            "Variables (numeric):", numeric)
        { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.SelectedColumns };

        RunDescriptives(ws, selection.SelectedColumns);
    }

    private void RunDescriptives(CoreData.Worksheet ws, IEnumerable<string> cols)
    {
        var stats = cols.Select(n => Descriptives.Compute(ws.Find(n)!)).ToList();
        Output("Descriptive Statistics", DescriptivesFormatter.Format(stats));
    }
    private void OnOneSampleT(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 1, out var numeric)) return;
        var dlg = new OneSampleTWindow(numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.Alt, dlg.Confidence, dlg.Mu0, dlg.SelectedColumns };
        foreach (var n in selection.SelectedColumns)
        {
            var v = ws.Find(n)!.NumericValues();
            if (v.Length < 2) { Log($"{n}: need at least 2 values."); continue; }
            var r = HypothesisTests.OneSampleT(v, selection.Mu0, selection.Confidence, selection.Alt);
            OutputRaw(HypothesisFormatters.OneSampleT(r, n, selection.Mu0));
        }
    }

    private void OnTwoSampleT(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new TwoColumnWindow(numeric, "2-Sample t", showPooled: true) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.Alt, dlg.Column1, dlg.Column2, dlg.Confidence, dlg.Pooled };
        var x1 = ws.Find(selection.Column1)!.NumericValues();
        var x2 = ws.Find(selection.Column2)!.NumericValues();
        if (x1.Length < 2 || x2.Length < 2) { Log("Each sample needs at least 2 values."); return; }
        try
        {
            var r = HypothesisTests.TwoSampleT(x1, x2, selection.Pooled, selection.Confidence, selection.Alt);
            OutputRaw(HypothesisFormatters.TwoSampleT(r, selection.Column1, selection.Column2));
        }
        catch (Exception ex) { Log($"2-Sample t: {ex.Message}"); }
    }

    private void OnPairedT(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new TwoColumnWindow(numeric, "Paired t", showPooled: false) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.Alt, dlg.Column1, dlg.Column2, dlg.Confidence };
        var (x1, x2) = Columns.Pairwise(ws.Find(selection.Column1)!, ws.Find(selection.Column2)!);
        if (x1.Length < 2) { Log("Need at least 2 paired (row-matched) observations."); return; }
        var r = HypothesisTests.PairedT(x1, x2, selection.Confidence, selection.Alt);
        OutputRaw(HypothesisFormatters.PairedT(r, selection.Column1, selection.Column2));
    }

    private void OnOneProportion(object sender, RoutedEventArgs e)
    {
        var dlg = new ProportionWindow(two: false) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.Alt, dlg.Confidence, dlg.Events1, dlg.P0, dlg.Trials1 };
        var r = HypothesisTests.OneProportion(selection.Events1, selection.Trials1, selection.P0, selection.Confidence, selection.Alt);
        OutputRaw(HypothesisFormatters.OneProportion(r, "Sample", selection.P0));
    }

    private void OnTwoProportions(object sender, RoutedEventArgs e)
    {
        var dlg = new ProportionWindow(two: true) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.Alt, dlg.Confidence, dlg.Events1, dlg.Events2, dlg.Trials1, dlg.Trials2 };
        var r = HypothesisTests.TwoProportions(selection.Events1, selection.Trials1, selection.Events2, selection.Trials2, selection.Confidence, selection.Alt);
        OutputRaw(HypothesisFormatters.TwoProportions(r, "Sample 1", "Sample 2"));
    }

    private void OnChiSquareGof(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 1, out var numeric)) return;
        var dlg = new ColumnPickerWindow("Chi-Square Goodness-of-Fit",
            "Column(s) of observed counts (one test each):", numeric)
        { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.SelectedColumns };
        foreach (var n in selection.SelectedColumns)
        {
            var obs = ws.Find(n)!.NumericValues();
            if (obs.Length < 2) { Log($"{n}: need at least 2 categories."); continue; }
            try
            {
                var r = HypothesisTests.ChiSquareGof(obs);
                var cats = Enumerable.Range(1, obs.Length).Select(i => i.ToString()).ToList();
                OutputRaw($"Goodness-of-Fit for {n}\n" + HypothesisFormatters.ChiSquareGof(r, cats));
            }
            catch (Exception ex) { Log($"{n}: {ex.Message}"); }
        }
    }

    private void OnChiSquareAssoc(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new ColumnPickerWindow("Cross Tabulation & Chi-Square",
            "Columns forming the table (each column = a table column):", numeric)
        { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.SelectedColumns };

        var selected = selection.SelectedColumns.Select(n => ws.Find(n)!).ToList();
        var completeRows = Columns.Rows(selected);
        int rows = completeRows.Count;
        if (rows < 2) { Log("Need at least 2 rows of counts."); return; }
        var table = new double[rows, selected.Count];
        for (int i = 0; i < rows; i++)
            for (int j = 0; j < selected.Count; j++) table[i, j] = completeRows[i][j];

        try
        {
            var r = HypothesisTests.ChiSquareAssociation(table);
            var rowLabels = Enumerable.Range(1, rows).Select(i => $"R{i}").ToList();
            OutputRaw(HypothesisFormatters.Contingency(r, rowLabels, selection.SelectedColumns));
        }
        catch (Exception ex) { Log($"Chi-square association: {ex.Message}"); }
    }

    private async void OnOneWayAnova(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new ColumnPickerWindow("One-Way ANOVA",
            "Response columns (each column is a group):", numeric)
        { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.SelectedColumns };
        var groups = selection.SelectedColumns
            .Select(n => (n, ws.Find(n)!.NumericValues()))
            .Where(t => t.Item2.Length > 0).ToList();
        if (groups.Count < 2) { Log("Need at least 2 non-empty groups."); return; }
        var r = await RunWorkAsync(ct => Anova.OneWay(groups));
        OutputRaw(AnovaFormatter.OneWay(r, "Factor", "Response"));
        try { OutputRaw(AdvancedFormatters.Tukey(await RunWorkAsync(ct => AnovaExtensions.Tukey(groups, cancellationToken: ct)))); }
        catch (Exception ex) { Log($"Tukey: {ex.Message}"); }
    }

    private async void OnTwoWayAnova(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 1, out var numeric)) return;
        var all = ws.Columns.Select(c => c.Name).ToList();
        var dlg = new TwoWayAnovaWindow(numeric, all) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.FactorA, dlg.FactorB, dlg.Response };
        var (y, a, b) = Columns.Factorial(ws.Find(selection.Response)!, ws.Find(selection.FactorA)!, ws.Find(selection.FactorB)!);
        if (y.Length < 4) { Log("Not enough complete rows."); return; }
        try { OutputRaw(AdvancedFormatters.TwoWayAnova(await RunWorkAsync(ct => AnovaExtensions.TwoWay(y, a, b, selection.FactorA, selection.FactorB)), selection.Response)); }
        catch (Exception ex) { Log($"Two-way ANOVA: {ex.Message}"); }
    }

    private async void OnEqualVariances(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new ColumnPickerWindow("Test for Equal Variances", "Group columns:", numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.SelectedColumns };
        var groups = selection.SelectedColumns.Select(n => (n, ws.Find(n)!.NumericValues()))
            .Where(t => t.Item2.Length > 1).ToList();
        if (groups.Count < 2) { Log("Need at least 2 groups with >1 value."); return; }
        try { OutputRaw(AdvancedFormatters.EqualVariances(await RunWorkAsync(ct => VarianceTests.EqualVariances(groups)), "Response", "Factor")); }
        catch (Exception ex) { Log($"Equal variances: {ex.Message}"); }
    }

    private async void OnTwoVariances(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new TwoColumnWindow(numeric, "2 Variances (F-Test)", showPooled: false, twoSidedOnly: true) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.Column1, dlg.Column2, dlg.Confidence };
        var x1 = ws.Find(selection.Column1)!.NumericValues();
        var x2 = ws.Find(selection.Column2)!.NumericValues();
        if (x1.Length < 2 || x2.Length < 2) { Log("Each sample needs at least 2 values."); return; }
        // Throws when either column is constant — an ordinary selection, not a programming error.
        try { OutputRaw(AdvancedFormatters.FTest(await RunWorkAsync(ct => VarianceTests.FTest(x1, x2, selection.Confidence)), selection.Column1, selection.Column2)); }
        catch (Exception ex) { Log($"2 Variances: {ex.Message}"); }
    }

    private async void OnPolynomialRegression(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new PolynomialWindow(numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.Degree, dlg.XColumn, dlg.YColumn };
        var (xs, ys) = Columns.Pairwise(ws.Find(selection.XColumn)!, ws.Find(selection.YColumn)!);
        if (xs.Length <= selection.Degree + 1) { Log("Not enough points for that degree."); return; }
        try
        {
            var r = await RunWorkAsync(ct => RegressionExtensions.Polynomial(xs, ys, selection.Degree, selection.XColumn, selection.YColumn, cancellationToken: ct));
            OutputRaw(RegressionFormatter.Format(r));
            ShowGraph("Residuals vs Fitted", p => Plots.ResidualVsFitted(p, r.Fitted, r.Residuals));
        }
        catch (Exception ex) { Log($"Polynomial regression: {ex.Message}"); }
    }

    private async void OnBestSubsets(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new RegressionWindow(numeric) { Owner = this, Title = "Best Subsets Regression" };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.Predictors, dlg.Response };
        var (y, x) = Columns.Design(ws.Find(selection.Response)!, selection.Predictors.Select(n => ws.Find(n)!).ToList());
        if (y.Length <= selection.Predictors.Count + 1) { Log("Not enough complete rows."); return; }
        try { OutputRaw(AdvancedFormatters.BestSubsets(await RunWorkAsync(ct => RegressionExtensions.BestSubsets(y, x, selection.Predictors, cancellationToken: ct)))); }
        catch (Exception ex) { Log($"Best subsets: {ex.Message}"); }
    }

    private async void OnStepwise(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new RegressionWindow(numeric) { Owner = this, Title = "Stepwise Regression" };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.Predictors, dlg.Response };
        var (y, x) = Columns.Design(ws.Find(selection.Response)!, selection.Predictors.Select(n => ws.Find(n)!).ToList());
        if (y.Length <= selection.Predictors.Count + 1) { Log("Not enough complete rows."); return; }
        try { OutputRaw(AdvancedFormatters.Stepwise(await RunWorkAsync(ct => RegressionExtensions.Stepwise(y, x, selection.Predictors, cancellationToken: ct)))); }
        catch (Exception ex) { Log($"Stepwise: {ex.Message}"); }
    }

    private async void OnLogistic(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new RegressionWindow(numeric) { Owner = this, Title = "Binary Logistic Regression" };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.Predictors, dlg.Response };
        var (y, x) = Columns.Design(ws.Find(selection.Response)!, selection.Predictors.Select(n => ws.Find(n)!).ToList());
        if (y.Length <= selection.Predictors.Count + 1) { Log("Not enough complete rows."); return; }
        try { OutputRaw(AdvancedFormatters.Logistic(await RunWorkAsync(ct => Logistic.Fit(y, x, selection.Predictors, selection.Response)))); }
        catch (Exception ex) { Log($"Logistic regression: {ex.Message} (response must be coded 0/1)"); }
    }

    // ---- Multivariate & power ---------------------------------------------

    private async void OnFishersExact(object sender, RoutedEventArgs e)
    {
        var dlg = new FisherWindow { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.CellA, dlg.CellB, dlg.CellC, dlg.CellD };
        // An all-zero table is accepted by the dialog but has no observations to test.
        try { OutputRaw(MultivariateFormatters.Fisher(await RunWorkAsync(ct => FishersExact.Test(selection.CellA, selection.CellB, selection.CellC, selection.CellD)))); }
        catch (Exception ex) { Log($"Fisher's exact test: {ex.Message}"); }
    }

    private async void OnPca(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new ColumnPickerWindow("Principal Components", "Variables (2 or more):", numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.SelectedColumns };
        var cols = selection.SelectedColumns.Select(n => ws.Find(n)!).ToList();
        if (cols.Count < 2) { Log("Select at least two variables."); return; }
        var rows = Columns.Rows(cols);
        if (rows.Count < 2) { Log("Not enough complete rows."); return; }
        try
        {
            var r = await RunWorkAsync(ct => Pca.Compute(rows.ToArray(), selection.SelectedColumns, correlation: true));
            OutputRaw(MultivariateFormatters.Pca(r));
            ShowGraph("Scree Plot", p => Plots.Scree(p, r.Eigenvalues));
        }
        catch (Exception ex) { Log($"PCA: {ex.Message}"); }
    }

    private async void OnKMeans(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 1, out var numeric)) return;
        var dlg = new KMeansWindow(numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.K, dlg.SelectedColumns };
        var cols = selection.SelectedColumns.Select(n => ws.Find(n)!).ToList();
        var rows = Columns.Rows(cols);
        if (rows.Count < selection.K) { Log("Need at least k complete rows."); return; }
        try
        {
            var r = await RunWorkAsync(ct => KMeans.Cluster(rows.ToArray(), selection.K, selection.SelectedColumns, cancellationToken: ct));
            OutputRaw(MultivariateFormatters.KMeans(r));
            if (selection.SelectedColumns.Count == 2)
                ShowGraph("K-Means Clusters",
                    p => Plots.ClusterScatter(p, selection.SelectedColumns[0], selection.SelectedColumns[1], rows.ToArray(), r.Assignments, r.K));
        }
        catch (Exception ex) { Log($"K-Means: {ex.Message}"); }
    }

    // ---- Bayesian & mixed --------------------------------------------------

    private async void OnBayesProportion(object sender, RoutedEventArgs e)
    {
        var dlg = new BayesProportionWindow { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.Confidence, dlg.N, dlg.PriorA, dlg.PriorB, dlg.Threshold, dlg.X };
        try
        {
            var r = await RunWorkAsync(ct => Bayes.Proportion(selection.X, selection.N, selection.PriorA, selection.PriorB, selection.Confidence, selection.Threshold));
            OutputRaw(BayesFormatters.Proportion(r, "Sample"));
        }
        catch (Exception ex) { Log($"Bayesian proportion: {ex.Message}"); }
    }

    private async void OnBayesNormal(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 1, out var numeric)) return;
        var dlg = new BayesNormalWindow(numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.Confidence, dlg.DataColumn, dlg.KnownSigma, dlg.KnownVariance, dlg.PriorMean, dlg.PriorSd, dlg.Threshold };
        var v = ws.Find(selection.DataColumn)!.NumericValues();
        if (v.Length < 2) { Log("Need at least 2 values."); return; }
        // The Jeffreys-prior path requires variation in the sample.
        try
        {
            var r = selection.KnownVariance
                ? await RunWorkAsync(ct => Bayes.NormalMeanKnownVar(v, selection.PriorMean, selection.PriorSd, selection.KnownSigma, selection.Confidence, selection.Threshold))
                : await RunWorkAsync(ct => Bayes.NormalMeanUnknownVar(v, selection.Confidence, selection.Threshold));
            OutputRaw(BayesFormatters.NormalMean(r, selection.DataColumn));
        }
        catch (Exception ex) { Log($"Bayesian normal mean: {ex.Message}"); }
    }

    private async void OnBayesRegression(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new RegressionWindow(numeric) { Owner = this, Title = "Bayesian Linear Regression" };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.Predictors, dlg.Response };
        var (y, x) = Columns.Design(ws.Find(selection.Response)!, selection.Predictors.Select(n => ws.Find(n)!).ToList());
        if (y.Length <= selection.Predictors.Count + 1) { Log("Not enough complete rows."); return; }
        try { OutputRaw(BayesFormatters.Regression(await RunWorkAsync(ct => Bayes.LinearRegression(y, x, selection.Predictors, selection.Response)))); }
        catch (Exception ex) { Log($"Bayesian regression: {ex.Message}"); }
    }

    private async void OnOneWayRandom(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new ColumnPickerWindow("One-Way Random Effects",
            "Group columns (each column is a random-effect level):", numeric)
        { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.SelectedColumns };
        var groups = selection.SelectedColumns.Select(n => (n, ws.Find(n)!.NumericValues()))
            .Where(t => t.Item2.Length > 0).ToList();
        if (groups.Count < 2) { Log("Need at least 2 groups."); return; }
        try { OutputRaw(MixedFormatters.OneWayRandom(await RunWorkAsync(ct => MixedModel.OneWayRandom(groups)), "Response", "Group")); }
        catch (Exception ex) { Log($"Random-effects model: {ex.Message}"); }
    }

    private async void OnFactorAnalysis(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new FactorWindow(numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.NumFactors, dlg.SelectedColumns, dlg.Varimax };
        var cols = selection.SelectedColumns.Select(n => ws.Find(n)!).ToList();
        var rows = Columns.Rows(cols);
        if (rows.Count < 2) { Log("Not enough complete rows."); return; }
        try
        {
            var r = await RunWorkAsync(ct => FactorAnalysis.Extract(rows.ToArray(), selection.SelectedColumns, selection.NumFactors, selection.Varimax));
            OutputRaw(MultivariateFormatters.FactorAnalysis(r));
        }
        catch (Exception ex) { Log($"Factor analysis: {ex.Message}"); }
    }

    private async void OnDistFit(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 1, out var numeric)) return;
        var dlg = new DistFitWindow(numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.CensorColumn, dlg.Distribution, dlg.TimesColumn };
        double[] t;
        bool[]? censored = null;
        if (selection.CensorColumn is null)
        {
            t = ws.Find(selection.TimesColumn)!.NumericValues();
        }
        else
        {
            // Row-aligned so a missing cell in either column drops the whole observation.
            var (times, flags) = Columns.Pairwise(ws.Find(selection.TimesColumn)!, ws.Find(selection.CensorColumn)!);
            if (flags.Any(v => v != 0 && v != 1))
            { Log("Censoring indicators must be exactly 0 (failure) or 1 (right-censored)."); return; }
            t = times;
            censored = flags.Select(v => v == 1).ToArray();
        }

        if (t.Length < 3) { Log("Need at least 3 observations."); return; }
        if (t.Any(v => v <= 0))
        { Log($"{selection.Distribution} requires all times > 0."); return; }
        try
        {
            var fit = selection.Distribution switch
            {
                "Exponential" => await RunWorkAsync(ct => Reliability.FitExponential(t, censored, cancellationToken: ct)),
                "Lognormal" => await RunWorkAsync(ct => Reliability.FitLognormal(t, censored, cancellationToken: ct)),
                "Normal" => await RunWorkAsync(ct => Reliability.FitNormal(t, censored, cancellationToken: ct)),
                _ => await RunWorkAsync(ct => Reliability.FitWeibull(t, censored, cancellationToken: ct)),
            };
            OutputRaw(ReliabilityFormatters.DistributionFit(fit, selection.TimesColumn));
            if (selection.Distribution == "Weibull")
            {
                double beta = fit.Parameters[0].Value, eta = fit.Parameters[1].Value;
                ShowGraph($"Weibull Plot of {selection.TimesColumn}",
                    p => Plots.WeibullPlot(p, selection.TimesColumn, t, beta, eta, censored));
            }
            else ShowGraph($"Histogram of {selection.TimesColumn}", p => Plots.Histogram(p, selection.TimesColumn, t));
        }
        catch (Exception ex) { Log($"Distribution analysis: {ex.Message}"); }
    }

    private async void OnKaplanMeier(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 1, out var numeric)) return;
        var dlg = new KaplanMeierWindow(numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.CensorColumn, dlg.TimesColumn };

        double[] times;
        bool[] censored;
        if (selection.CensorColumn is null)
        {
            times = ws.Find(selection.TimesColumn)!.NumericValues();
            censored = new bool[times.Length];
        }
        else
        {
            var (tv, cv) = Columns.Pairwise(ws.Find(selection.TimesColumn)!, ws.Find(selection.CensorColumn)!);
            times = tv;
            if (cv.Any(v => v != 0 && v != 1))
            {
                Log("Censoring indicators must be exactly 0 (event) or 1 (censored).");
                return;
            }
            censored = cv.Select(v => v == 1).ToArray();
        }
        if (times.Length < 2) { Log("Need at least 2 observations."); return; }
        if (times.Any(t => t < 0)) { Log("Survival times must be nonnegative."); return; }
        try
        {
            var km = await RunWorkAsync(ct => Reliability.KaplanMeier(times, censored, cancellationToken: ct));
            OutputRaw(ReliabilityFormatters.KaplanMeier(km, selection.TimesColumn));
            ShowGraph($"Kaplan-Meier Survival of {selection.TimesColumn}",
                p => Plots.StepSurvival(p, selection.TimesColumn, km.Rows.Select(r => r.Time).ToArray(),
                    km.Rows.Select(r => r.Survival).ToArray()));
        }
        catch (Exception ex) { Log($"Kaplan-Meier: {ex.Message}"); }
    }

    private void OnPowerSampleSize(object sender, RoutedEventArgs e)
    {
        var dlg = new PowerWindow { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.Alpha, dlg.Alt, dlg.EffectSize, dlg.N, dlg.P0, dlg.P1, dlg.SolveForPower, dlg.TargetPower, dlg.TestIndex };

        string test = selection.TestIndex switch { 1 => "2-Sample t", 2 => "1 Proportion", _ => "1-Sample t" };
        string solveFor = selection.SolveForPower ? "power" : "sample size";
        double n, power, effectVal;
        string effectDesc;

        if (selection.TestIndex == 2)
        {
            effectDesc = $"p0 = {selection.P0}, p1 = {selection.P1}";
            if (selection.SolveForPower) { n = selection.N; power = Power.OneProportionPower(n, selection.P0, selection.P1, selection.Alpha, selection.Alt); }
            else { power = selection.TargetPower; n = Power.OneProportionSampleSize(power, selection.P0, selection.P1, selection.Alpha, selection.Alt); }
        }
        else
        {
            effectVal = selection.EffectSize;
            effectDesc = $"d = {effectVal}";
            bool two = selection.TestIndex == 1;
            if (selection.SolveForPower)
            {
                n = selection.N;
                power = two ? Power.TwoSampleTPower(n, effectVal, selection.Alpha, selection.Alt)
                            : Power.OneSampleTPower(n, effectVal, selection.Alpha, selection.Alt);
            }
            else
            {
                power = selection.TargetPower;
                n = two ? Power.TwoSampleTSampleSize(power, effectVal, selection.Alpha, selection.Alt)
                        : Power.OneSampleTSampleSize(power, effectVal, selection.Alpha, selection.Alt);
            }
        }
        OutputRaw(MultivariateFormatters.Power(test, solveFor, selection.Alpha, selection.Alt, effectDesc, n, power));
    }

    // ---- DOE & Gage R&R ----------------------------------------------------

    private void OnCreateFactorial(object sender, RoutedEventArgs e)
    {
        var dlg = new DoeCreateWindow { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.CenterPoints, dlg.Factors, dlg.Randomize, dlg.Replicates };
        if (!ConfirmDiscardChanges()) return;
        var design = DoeDesign.FullFactorial(selection.Factors, selection.Replicates, selection.CenterPoints, selection.Randomize);
        var inv = System.Globalization.CultureInfo.InvariantCulture;

        var ws = new CoreData.Worksheet { Name = $"FactorialDesign_{selection.Factors}f" };
        var so = ws.AddColumn("StdOrder");
        var ro = ws.AddColumn("RunOrder");
        var cp = ws.AddColumn("CenterPt");
        var fcols = design.FactorNames.Select(fn => ws.AddColumn(fn)).ToList();
        foreach (var run in design.RunList)
        {
            so.Add(run.StdOrder.ToString());
            ro.Add(run.RunOrder.ToString());
            cp.Add(run.CenterPt.ToString());
            for (int j = 0; j < fcols.Count; j++) fcols[j].Add(run.Factors[j].ToString(inv));
        }
        LoadWorksheet(ws, markDirty: true);
        Log(DoeFormatters.Design(design));
    }

    private void OnCreateFractional(object sender, RoutedEventArgs e)
    {
        var dlg = new FractionalCreateWindow { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.Factors, dlg.Randomize, dlg.Runs };
        if (!ConfirmDiscardChanges()) return;
        var design = DoeDesign.FractionalFactorial(selection.Factors, selection.Runs, selection.Randomize);
        var inv = System.Globalization.CultureInfo.InvariantCulture;

        var ws = new CoreData.Worksheet { Name = $"FracFactorial_{selection.Factors}f{selection.Runs}r" };
        var so = ws.AddColumn("StdOrder");
        var ro = ws.AddColumn("RunOrder");
        var fcols = design.FactorNames.Select(fn => ws.AddColumn(fn)).ToList();
        foreach (var run in design.RunList)
        {
            so.Add(run.StdOrder.ToString());
            ro.Add(run.RunOrder.ToString());
            for (int j = 0; j < fcols.Count; j++) fcols[j].Add(run.Factors[j].ToString(inv));
        }
        LoadWorksheet(ws, markDirty: true);
        Log(DoeFormatters.Fractional(design));
    }

    private async void OnCalculator(object sender, RoutedEventArgs e)
    {
        var dlg = new CalculatorWindow { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.Expression, dlg.TargetColumn };
        var ws = CurrentWorksheet();
        try
        {
            var rowCount = await RunWorkAsync(ct => CoreData.Calculator.EvaluateIntoColumn(selection.Expression, ws, selection.TargetColumn, ct));
            LoadWorksheet(ws, markDirty: true);
            Log($"Calculated '{selection.TargetColumn}' = {selection.Expression}  ({rowCount} rows).");
        }
        catch (Exception ex) { ShowError("Calculator", ex); }
    }

    private void OnCreateMixture(object sender, RoutedEventArgs e)
    {
        var dlg = new MixtureCreateWindow { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.Components, dlg.Degree, dlg.IsLattice, dlg.Randomize };
        if (!ConfirmDiscardChanges()) return;
        var design = selection.IsLattice
            ? MixtureDesign.SimplexLattice(selection.Components, selection.Degree, selection.Randomize)
            : MixtureDesign.SimplexCentroid(selection.Components, selection.Randomize);

        var ws = new CoreData.Worksheet { Name = $"Mixture_{selection.Components}c" };
        var so = ws.AddColumn("StdOrder");
        var ro = ws.AddColumn("RunOrder");
        var pt = ws.AddColumn("PtType", CoreData.ColumnType.Text);
        var ccols = design.ComponentNames.Select(n => ws.AddColumn(n)).ToList();
        foreach (var run in design.RunList)
        {
            so.Add(run.StdOrder.ToString());
            ro.Add(run.RunOrder.ToString());
            pt.Add(run.PointType);
            for (int j = 0; j < ccols.Count; j++) ccols[j].AddNumber(run.Components[j]);
        }
        LoadWorksheet(ws, markDirty: true);
        Log(DoeFormatters.Mixture(design));
    }

    private async void OnAnalyzeMixture(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new RegressionWindow(numeric) { Owner = this, Title = "Analyze Mixture Design (response, then components)" };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.Predictors, dlg.Response };
        var (y, comps) = Columns.Design(ws.Find(selection.Response)!, selection.Predictors.Select(n => ws.Find(n)!).ToList());
        if (comps.Length < 2) { Log("Select at least two components."); return; }
        try
        {
            // The quadratic Scheffé model needs a run per cross-product term; a simplex-lattice
            // {q,1} design cannot support it, so fall back to the linear model rather than failing.
            int quadraticTerms = comps.Length + comps.Length * (comps.Length - 1) / 2;
            var r = await RunWorkAsync(ct => MixtureAnalysis.Fit(y, comps, selection.Predictors, quadratic: y.Length > quadraticTerms));
            OutputRaw(DoeFormatters.MixtureModel(r));
        }
        catch (Exception ex) { Log($"Mixture analysis: {ex.Message}"); }
    }

    private void OnCreateRsm(object sender, RoutedEventArgs e)
    {
        var dlg = new RsmCreateWindow { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.CenterPoints, dlg.FaceCentered, dlg.Factors, dlg.IsBoxBehnken, dlg.Randomize };
        if (!ConfirmDiscardChanges()) return;
        var design = selection.IsBoxBehnken
            ? ResponseSurface.BoxBehnken(selection.Factors, selection.CenterPoints, selection.Randomize)
            : ResponseSurface.CentralComposite(selection.Factors, selection.CenterPoints, selection.FaceCentered, selection.Randomize);

        var ws = new CoreData.Worksheet { Name = selection.IsBoxBehnken ? $"BoxBehnken_{selection.Factors}f" : $"CCD_{selection.Factors}f" };
        var so = ws.AddColumn("StdOrder");
        var ro = ws.AddColumn("RunOrder");
        var pt = ws.AddColumn("PtType", CoreData.ColumnType.Text);
        var fcols = design.FactorNames.Select(fn => ws.AddColumn(fn)).ToList();
        foreach (var run in design.RunList)
        {
            so.Add(run.StdOrder.ToString());
            ro.Add(run.RunOrder.ToString());
            pt.Add(run.PointType);
            for (int j = 0; j < fcols.Count; j++) fcols[j].AddNumber(run.Factors[j]);
        }
        LoadWorksheet(ws, markDirty: true);
        Log(DoeFormatters.Rsm(design));
    }

    private async void OnAnalyzeRsm(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new RegressionWindow(numeric) { Owner = this, Title = "Analyze Response Surface (quadratic)" };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.Predictors, dlg.Response };
        var (y, x) = Columns.Design(ws.Find(selection.Response)!, selection.Predictors.Select(n => ws.Find(n)!).ToList());
        if (x.Length < 2) { Log("Select at least two factors."); return; }
        int terms = 1 + 2 * x.Length + x.Length * (x.Length - 1) / 2;
        if (y.Length <= terms) { Log($"Need more than {terms} complete runs for a quadratic model in {x.Length} factors."); return; }
        try
        {
            var r = await RunWorkAsync(ct => ResponseSurface.Analyze(y, x, selection.Predictors, selection.Response));
            OutputRaw("Response Surface Regression (full quadratic model)\n\n" + RegressionFormatter.Format(r));
            ShowGraph("Residuals vs Fitted", p => Plots.ResidualVsFitted(p, r.Fitted, r.Residuals));
        }
        catch (Exception ex) { Log($"Response surface: {ex.Message}"); }
    }

    private async void OnAnalyzeFactorial(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new RegressionWindow(numeric) { Owner = this, Title = "Analyze Factorial Design" };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.Predictors, dlg.Response };
        var (y, x) = Columns.Design(ws.Find(selection.Response)!, selection.Predictors.Select(n => ws.Find(n)!).ToList());
        if (y.Length < 4) { Log("Need at least 4 complete runs."); return; }
        try
        {
            var r = await RunWorkAsync(ct => FactorialAnalysis.Analyze(y, x, selection.Predictors, selection.Response, cancellationToken: ct));
            OutputRaw(DoeFormatters.Factorial(r));
            var effects = r.Terms.Where(t => double.IsFinite(t.Effect))
                .OrderByDescending(t => Math.Abs(double.IsNaN(t.T) ? t.Effect : t.T)).ToList();
            string yl = r.DfError > 0 ? "|Standardized effect|" : "|Effect|";
            ShowGraph("Pareto of Effects", p => Plots.LabeledBars(p, "Pareto of Effects", "Term", yl,
                effects.Select(t => t.Name).ToList(),
                effects.Select(t => Math.Abs(double.IsNaN(t.T) ? t.Effect : t.T)).ToList()));
        }
        catch (Exception ex) { Log($"Factorial analysis: {ex.Message}"); }
    }

    private async void OnGageRR(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 1, out var numeric)) return;
        var all = ws.Columns.Select(c => c.Name).ToList();
        var dlg = new TwoWayAnovaWindow(numeric, all) { Owner = this, Title = "Gage R&R (Crossed) — Response, Part, Operator" };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.FactorA, dlg.FactorB, dlg.Response };
        var (y, part, op) = Columns.Factorial(ws.Find(selection.Response)!, ws.Find(selection.FactorA)!, ws.Find(selection.FactorB)!);
        try
        {
            var g = await RunWorkAsync(ct => GageRR.Analyze(y, part, op));
            OutputRaw(DoeFormatters.GageRR(g));
            var comps = g.Components.Where(c => c.Source.Trim() != "Total Variation").ToList();
            ShowGraph("Gage R&R Components", p => Plots.LabeledBars(p, "Gage R&R — % Study Var", "Source", "% Study Var",
                comps.Select(c => c.Source.Trim()).ToList(), comps.Select(c => c.PctStudyVar).ToList()));
        }
        catch (Exception ex) { Log($"Gage R&R: {ex.Message}"); }
    }

    // ---- Time series -------------------------------------------------------

    private double[]? OpenSeries(string title, TsFields fields, out TimeSeriesWindow dlg, out string name)
    {
        dlg = null!; name = "";
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 1, out var numeric)) return null;
        dlg = new TimeSeriesWindow(numeric, title, fields) { Owner = this };
        if (dlg.ShowDialog() != true) return null;
        name = dlg.SeriesColumn;
        try { return ws.Find(name)!.SeriesValues(); }
        catch (ArgumentException ex) { Log(ex.Message); return null; }
    }

    private async void OnTrendAnalysis(object sender, RoutedEventArgs e)
    {
        var v = OpenSeries("Trend Analysis", TsFields.TrendType | TsFields.Forecasts, out var dlg, out var name);
        if (v is null) return;
        var selection = new { dlg.Forecasts, dlg.Quadratic };
        int minimum = selection.Quadratic ? 4 : 3;
        if (v.Length < minimum) { Log($"Need at least {minimum} points."); return; }
        try
        {
            var r = selection.Quadratic ? await RunWorkAsync(ct => TimeSeries.QuadraticTrend(v, selection.Forecasts, cancellationToken: ct)) : await RunWorkAsync(ct => TimeSeries.LinearTrend(v, selection.Forecasts, cancellationToken: ct));
            OutputRaw(TimeSeriesFormatters.Trend(r, name, v.Length));
            ShowGraph($"Trend Analysis of {name}",
                p => Plots.TimeSeriesFit(p, name, v, r.Fitted, r.Forecasts, $"Trend Analysis of {name}"));
        }
        catch (Exception ex) { Log($"Trend analysis: {ex.Message}"); }
    }

    private async void OnMovingAverage(object sender, RoutedEventArgs e)
    {
        var v = OpenSeries("Moving Average", TsFields.Length | TsFields.Forecasts, out var dlg, out var name);
        if (v is null) return;
        var selection = new { dlg.Forecasts, dlg.Length };
        if (v.Length < selection.Length) { Log("Series shorter than the MA length."); return; }
        try
        {
            var r = await RunWorkAsync(ct => TimeSeries.MovingAverage(v, selection.Length, selection.Forecasts, cancellationToken: ct));
            OutputRaw(TimeSeriesFormatters.Smoothing(r, name, v.Length));
            ShowGraph($"Moving Average of {name}",
                p => Plots.TimeSeriesFit(p, name, v, r.Fitted, r.Forecasts, $"Moving Average of {name}"));
        }
        catch (Exception ex) { Log($"Moving average: {ex.Message}"); }
    }

    private async void OnSingleExp(object sender, RoutedEventArgs e)
    {
        var v = OpenSeries("Single Exponential Smoothing", TsFields.Alpha | TsFields.Forecasts, out var dlg, out var name);
        if (v is null) return;
        var selection = new { dlg.Alpha, dlg.Forecasts };
        if (v.Length < 2) { Log("Need at least 2 points."); return; }
        try
        {
            var r = await RunWorkAsync(ct => TimeSeries.SingleExp(v, selection.Alpha, selection.Forecasts, cancellationToken: ct));
            OutputRaw(TimeSeriesFormatters.Smoothing(r, name, v.Length));
            ShowGraph($"Single Exp Smoothing of {name}",
                p => Plots.TimeSeriesFit(p, name, v, r.Fitted, r.Forecasts, $"Single Exp Smoothing of {name}"));
        }
        catch (Exception ex) { Log($"Single exponential smoothing: {ex.Message}"); }
    }

    private async void OnDoubleExp(object sender, RoutedEventArgs e)
    {
        var v = OpenSeries("Double Exponential Smoothing", TsFields.Alpha | TsFields.Beta | TsFields.Forecasts, out var dlg, out var name);
        if (v is null) return;
        var selection = new { dlg.Alpha, dlg.Beta, dlg.Forecasts };
        if (v.Length < 3) { Log("Need at least 3 points."); return; }
        try
        {
            var r = await RunWorkAsync(ct => TimeSeries.DoubleExp(v, selection.Alpha, selection.Beta, selection.Forecasts, cancellationToken: ct));
            OutputRaw(TimeSeriesFormatters.Smoothing(r, name, v.Length));
            ShowGraph($"Double Exp Smoothing of {name}",
                p => Plots.TimeSeriesFit(p, name, v, r.Fitted, r.Forecasts, $"Double Exp Smoothing of {name}"));
        }
        catch (Exception ex) { Log($"Double exponential smoothing: {ex.Message}"); }
    }

    private async void OnWinters(object sender, RoutedEventArgs e)
    {
        var v = OpenSeries("Winters' Method",
            TsFields.Period | TsFields.Alpha | TsFields.Beta | TsFields.Gamma | TsFields.Multiplicative | TsFields.Forecasts,
            out var dlg, out var name);
        if (v is null) return;
        var selection = new { dlg.Alpha, dlg.Beta, dlg.Forecasts, dlg.Gamma, dlg.Multiplicative, dlg.Period };
        try
        {
            var r = await RunWorkAsync(ct => TimeSeries.Winters(v, selection.Period, selection.Alpha, selection.Beta, selection.Gamma, selection.Multiplicative, selection.Forecasts, cancellationToken: ct));
            OutputRaw(TimeSeriesFormatters.Smoothing(r, name, v.Length));
            ShowGraph($"Winters' Method of {name}",
                p => Plots.TimeSeriesFit(p, name, v, r.Fitted, r.Forecasts, $"Winters' Method of {name}"));
        }
        catch (Exception ex) { Log($"Winters: {ex.Message}"); }
    }

    private async void OnDecomposition(object sender, RoutedEventArgs e)
    {
        var v = OpenSeries("Time Series Decomposition", TsFields.Period | TsFields.Multiplicative, out var dlg, out var name);
        if (v is null) return;
        var selection = new { dlg.Multiplicative, dlg.Period };
        if (selection.Period > v.Length / 2) { Log("Need at least two full seasons."); return; }
        // Multiplicative decomposition rejects non-positive observations (OnWinters already guards this).
        try
        {
            var r = await RunWorkAsync(ct => TimeSeries.Decompose(v, selection.Period, selection.Multiplicative, cancellationToken: ct));
            OutputRaw(TimeSeriesFormatters.Decomposition(r, name));
            ShowGraph($"Decomposition of {name} (trend)",
                p => Plots.TimeSeriesFit(p, name, v, r.Trend, Array.Empty<double>(), $"Decomposition of {name} (trend)"));
        }
        catch (Exception ex) { Log($"Decomposition: {ex.Message}"); }
    }

    private async void OnArima(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 1, out var numeric)) return;
        var dlg = new ArimaWindow(numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.D, dlg.Forecasts, dlg.IncludeConstant, dlg.P, dlg.Q, dlg.SeriesColumn };
        double[] v;
        try { v = ws.Find(selection.SeriesColumn)!.SeriesValues(); }
        catch (ArgumentException ex) { Log(ex.Message); return; }
        StatusText.Text = $"Fitting ARIMA model for {selection.SeriesColumn}…";
        try
        {
            var r = await RunWorkAsync(ct => Arima.Fit(v, selection.P, selection.D, selection.Q, selection.Forecasts, selection.IncludeConstant, ct));

            // Formatting and plotting stay inside the try: this is an async void method, so an
            // exception after the await has no catch site and would terminate the process.
            OutputRaw(TimeSeriesFormatters.Arima(r, selection.SeriesColumn));
            if (r.Forecasts.Length > 0)
                ShowGraph($"ARIMA Forecast of {selection.SeriesColumn}",
                    p => Plots.ForecastPlot(p, selection.SeriesColumn, v, r.Forecasts, r.ForecastLower, r.ForecastUpper,
                        $"ARIMA Forecast of {selection.SeriesColumn}"));
        }
        catch (OperationCanceledException) { Log("ARIMA: cancelled."); }
        catch (Exception ex) { Log($"ARIMA: {ex.Message}"); }
    }

    private async void OnSarima(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 1, out var numeric)) return;
        var dlg = new SarimaWindow(numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.D, dlg.Forecasts, dlg.IncludeConstant, dlg.P, dlg.Q, dlg.SD, dlg.SP, dlg.SQ, dlg.Season, dlg.SeriesColumn };
        double[] v;
        try { v = ws.Find(selection.SeriesColumn)!.SeriesValues(); }
        catch (ArgumentException ex) { Log(ex.Message); return; }
        StatusText.Text = $"Fitting SARIMA model for {selection.SeriesColumn}…";
        try
        {
            var r = await RunWorkAsync(ct => Sarima.Fit(v, selection.P, selection.D, selection.Q, selection.SP, selection.SD, selection.SQ,
                selection.Season, selection.Forecasts, selection.IncludeConstant, ct));

            OutputRaw(TimeSeriesFormatters.Sarima(r, selection.SeriesColumn));
            if (r.Forecasts.Length > 0)
                ShowGraph($"SARIMA Forecast of {selection.SeriesColumn}",
                    p => Plots.ForecastPlot(p, selection.SeriesColumn, v, r.Forecasts, r.ForecastLower, r.ForecastUpper,
                        $"SARIMA Forecast of {selection.SeriesColumn}"));
        }
        catch (OperationCanceledException) { Log("SARIMA: cancelled."); }
        catch (Exception ex) { Log($"SARIMA: {ex.Message}"); }
    }

    private void OnAcf(object sender, RoutedEventArgs e) => RunAcf(false);
    private void OnPacf(object sender, RoutedEventArgs e) => RunAcf(true);

    private async void RunAcf(bool partial)
    {
        var v = OpenSeries(partial ? "Partial Autocorrelation" : "Autocorrelation", TsFields.MaxLag, out var dlg, out var name);
        if (v is null) return;
        var selection = new { dlg.MaxLag };
        if (v.Length < 4) { Log("Need at least 4 points."); return; }
        try
        {
            var r = await RunWorkAsync(ct => TimeSeries.Autocorrelation(v, selection.MaxLag, cancellationToken: ct));
            OutputRaw(TimeSeriesFormatters.Acf(r, name, partial));
            var vals = partial ? r.Pacf : r.Acf;
            ShowGraph($"{(partial ? "PACF" : "ACF")} of {name}",
                p => Plots.Acf(p, $"{(partial ? "PACF" : "ACF")} of {name}", vals, r.N, partial ? "PACF" : "ACF"));
        }
        catch (Exception ex) { Log($"{(partial ? "PACF" : "ACF")}: {ex.Message}"); }
    }

    // ---- Nonparametrics ----------------------------------------------------

    private void OnMannWhitney(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new TwoColumnWindow(numeric, "Mann-Whitney", showPooled: false) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.Alt, dlg.Column1, dlg.Column2 };
        var x1 = ws.Find(selection.Column1)!.NumericValues();
        var x2 = ws.Find(selection.Column2)!.NumericValues();
        if (x1.Length < 1 || x2.Length < 1) { Log("Each sample needs data."); return; }
        var r = Nonparametric.MannWhitney(x1, x2, selection.Alt);
        OutputRaw(NonparametricFormatters.MannWhitney(r, selection.Column1, selection.Column2));
    }

    private void OnWilcoxon(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 1, out var numeric)) return;
        var dlg = new OneSampleTWindow(numeric) { Owner = this, Title = "Wilcoxon Signed-Rank" };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.Alt, dlg.Mu0, dlg.SelectedColumns };
        foreach (var n in selection.SelectedColumns)
        {
            var v = ws.Find(n)!.NumericValues();
            if (v.Length < 2) { Log($"{n}: need at least 2 values."); continue; }
            OutputRaw(NonparametricFormatters.Wilcoxon(
                Nonparametric.WilcoxonSignedRank(v, selection.Mu0, selection.Alt), n, selection.Mu0));
        }
    }

    private void OnKruskalWallis(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new ColumnPickerWindow("Kruskal-Wallis", "Response columns (each column is a group):", numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.SelectedColumns };
        var groups = selection.SelectedColumns.Select(n => (n, ws.Find(n)!.NumericValues()))
            .Where(t => t.Item2.Length > 0).ToList();
        if (groups.Count < 2) { Log("Need at least 2 non-empty groups."); return; }
        OutputRaw(NonparametricFormatters.KruskalWallis(Nonparametric.KruskalWallis(groups), "Response", "Factor"));
    }

    private void OnSignTest(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 1, out var numeric)) return;
        var dlg = new OneSampleTWindow(numeric) { Owner = this, Title = "Sign Test for Median" };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.Alt, dlg.Mu0, dlg.SelectedColumns };
        foreach (var n in selection.SelectedColumns)
        {
            var v = ws.Find(n)!.NumericValues();
            if (v.Length < 1) { Log($"{n}: no data."); continue; }
            OutputRaw(NonparametricFormatters.SignTest(
                Nonparametric.SignTest(v, selection.Mu0, selection.Alt), n, selection.Mu0));
        }
    }

    private void OnRunsTest(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 1, out var numeric)) return;
        var dlg = new ColumnPickerWindow("Runs Test", "Columns to test for randomness:", numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.SelectedColumns };
        foreach (var n in selection.SelectedColumns)
        {
            var v = ws.Find(n)!.NumericValues();
            if (v.Length < 3) { Log($"{n}: need at least 3 values."); continue; }
            OutputRaw(NonparametricFormatters.RunsTest(Nonparametric.RunsTest(v), n));
        }
    }

    // ---- Correlation & normality ------------------------------------------

    private void OnCorrelationPearson(object sender, RoutedEventArgs e) => Correlate(false);
    private void OnCorrelationSpearman(object sender, RoutedEventArgs e) => Correlate(true);

    private async void Correlate(bool spearman)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new ColumnPickerWindow(spearman ? "Correlation (Spearman)" : "Correlation (Pearson)",
            "Variables (2 or more):", numeric)
        { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.SelectedColumns };
        var cols = selection.SelectedColumns.Select(n => ws.Find(n)!).ToList();
        if (cols.Count < 2) { Log("Select at least two variables."); return; }
        OutputRaw(NonparametricFormatters.Correlation(await RunWorkAsync(ct => Correlation.Matrix(cols, spearman))));
    }

    private async void OnNormalityTest(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 1, out var numeric)) return;
        var dlg = new ColumnPickerWindow("Normality Test", "Variables to test:", numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.SelectedColumns };
        foreach (var n in selection.SelectedColumns)
        {
            var v = ws.Find(n)!.NumericValues();
            if (v.Length < 3) { Log($"{n}: need at least 3 values."); continue; }
            // Anderson-Darling requires variation; a constant column must not end the loop.
            try
            {
                OutputRaw(NonparametricFormatters.AndersonDarling(await RunWorkAsync(ct => Normality.AndersonDarling(v)), n));
                ShowGraph($"Probability Plot of {n}", p => Plots.ProbabilityPlot(p, n, v));
            }
            catch (OperationCanceledException) { Log("Normality: cancelled."); return; }
            catch (Exception ex) { Log($"{n}: {ex.Message}"); }
        }
    }
    private async void OnSimpleRegression(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new XyPickerWindow(numeric, "Simple Regression") { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.XColumn, dlg.YColumn };
        var (xs, ys) = Columns.Pairwise(ws.Find(selection.XColumn)!, ws.Find(selection.YColumn)!);
        if (xs.Length < 3) { Log("Need at least 3 paired observations."); return; }
        try
        {
            var r = await RunWorkAsync(ct => Regression.SimpleLinear(xs, ys, selection.XColumn, selection.YColumn));
            OutputRaw(RegressionFormatter.Format(r));
            ShowGraph($"Fitted Line Plot of {selection.YColumn} vs {selection.XColumn}",
                p => Plots.FittedLine(p, selection.XColumn, selection.YColumn, xs, ys, r.Coefficients[0], r.Coefficients[1]));
            ShowGraph("Residuals vs Fitted", p => Plots.ResidualVsFitted(p, r.Fitted, r.Residuals));
        }
        catch (Exception ex) { Log($"Regression: {ex.Message}"); }
    }

    private async void OnMultipleRegression(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new RegressionWindow(numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.Predictors, dlg.Response };
        var resp = ws.Find(selection.Response)!;
        var preds = selection.Predictors.Select(n => ws.Find(n)!).ToList();
        var (y, x) = Columns.Design(resp, preds);
        if (y.Length <= preds.Count + 1) { Log("Not enough complete rows for the number of predictors."); return; }
        try
        {
            var r = await RunWorkAsync(ct => Regression.Fit(y, x, selection.Predictors, selection.Response));
            OutputRaw(RegressionFormatter.Format(r));
            ShowGraph("Residuals vs Fitted", p => Plots.ResidualVsFitted(p, r.Fitted, r.Residuals));
        }
        catch (Exception ex) { Log($"Regression: {ex.Message}"); }
    }
    private void OnXbarR(object sender, RoutedEventArgs e) => VariablesChart("Xbar-R", true);
    private void OnXbarS(object sender, RoutedEventArgs e) => VariablesChart("Xbar-S", false);

    private void VariablesChart(string kind, bool useRange)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new ColumnPickerWindow($"{kind} Chart",
            "Subgroup columns (each row across them is a subgroup):", numeric)
        { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.SelectedColumns };
        var cols = selection.SelectedColumns.Select(n => ws.Find(n)!).ToList();
        var subgroups = Columns.Rows(cols);
        try
        {
            var (mean, spread) = useRange ? ControlCharts.XbarR(subgroups) : ControlCharts.XbarS(subgroups);
            OutputRaw(SpcFormatter.Pair($"{kind} Chart", mean, spread));
            ShowGraph(mean.Title, p => Plots.ControlChart(p, mean));
            ShowGraph(spread.Title, p => Plots.ControlChart(p, spread));
        }
        catch (Exception ex) { Log($"{kind}: {ex.Message}"); }
    }

    private void OnImr(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 1, out var numeric)) return;
        var dlg = new ColumnPickerWindow("I-MR Chart", "Column of individual measurements:", numeric, singleSelection: true) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.SelectedColumns };
        double[] v;
        try { v = ws.Find(selection.SelectedColumns[0])!.SeriesValues(); }
        catch (ArgumentException ex) { Log(ex.Message); return; }
        if (v.Length < 2) { Log("Need at least 2 values."); return; }
        var (ind, mr) = ControlCharts.IMR(v);
        OutputRaw(SpcFormatter.Pair("I-MR Chart", ind, mr));
        ShowGraph(ind.Title, p => Plots.ControlChart(p, ind));
        ShowGraph(mr.Title, p => Plots.ControlChart(p, mr));
    }

    private static bool TryCounts(double[] values, bool positive, out int[] counts, out string error)
    {
        counts = Array.Empty<int>();
        error = "";
        if (values.Any(v => !double.IsFinite(v) || v != Math.Truncate(v) ||
                            v < (positive ? 1 : 0) || v > int.MaxValue))
        {
            error = positive
                ? "Sample sizes must be positive whole numbers."
                : "Counts must be nonnegative whole numbers.";
            return false;
        }
        counts = values.Select(v => (int)v).ToArray();
        return true;
    }

    private void OnPChart(object sender, RoutedEventArgs e) => AttributeChart("P");
    private void OnUChart(object sender, RoutedEventArgs e) => AttributeChart("U");

    private void AttributeChart(string kind)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new AttributeChartWindow(numeric, $"{kind} Chart", needSizesColumn: true, needConstantSize: false) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.CountsColumn, dlg.SizesColumn };
        var (countValues, sizeValues) = Columns.Pairwise(ws.Find(selection.CountsColumn)!, ws.Find(selection.SizesColumn)!);
        if (countValues.Length < 2) { Log("Need at least 2 complete rows."); return; }
        if (!TryCounts(countValues, positive: false, out var counts, out var error) ||
            !TryCounts(sizeValues, positive: true, out var sizes, out error))
        { Log(error); return; }
        try
        {
            var chart = kind == "P"
                ? ControlCharts.PChart(counts, sizes)
                : ControlCharts.UChart(counts, sizes);
            OutputRaw(SpcFormatter.Chart(chart));
            ShowGraph(chart.Title, p => Plots.ControlChart(p, chart));
        }
        catch (Exception ex) { Log($"{kind} chart: {ex.Message}"); }
    }

    private void OnNpChart(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 1, out var numeric)) return;
        var dlg = new AttributeChartWindow(numeric, "NP Chart", needSizesColumn: false, needConstantSize: true) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.ConstantSize, dlg.CountsColumn };
        if (!TryCounts(ws.Find(selection.CountsColumn)!.NumericValues(), positive: false, out var counts, out var error))
        { Log(error); return; }
        if (counts.Length < 2) { Log("Need at least 2 rows."); return; }
        try
        {
            var chart = ControlCharts.NPChart(counts, selection.ConstantSize);
            OutputRaw(SpcFormatter.Chart(chart));
            ShowGraph(chart.Title, p => Plots.ControlChart(p, chart));
        }
        catch (Exception ex) { Log($"NP chart: {ex.Message}"); }
    }

    private void OnCChart(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 1, out var numeric)) return;
        var dlg = new AttributeChartWindow(numeric, "C Chart", needSizesColumn: false, needConstantSize: false) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.CountsColumn };
        if (!TryCounts(ws.Find(selection.CountsColumn)!.NumericValues(), positive: false, out var counts, out var error))
        { Log(error); return; }
        if (counts.Length < 2) { Log("Need at least 2 rows."); return; }
        try
        {
            var chart = ControlCharts.CChart(counts);
            OutputRaw(SpcFormatter.Chart(chart));
            ShowGraph(chart.Title, p => Plots.ControlChart(p, chart));
        }
        catch (Exception ex) { Log($"C chart: {ex.Message}"); }
    }

    private void OnCapability(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 1, out var numeric)) return;
        var dlg = new CapabilityWindow(numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.DataColumn, dlg.Lsl, dlg.Target, dlg.Usl };
        double[] v;
        try { v = ws.Find(selection.DataColumn)!.SeriesValues(); }
        catch (ArgumentException ex) { Log(ex.Message); return; }
        if (v.Length < 2) { Log("Need at least 2 values."); return; }
        try
        {
            var cap = Capability.FromIndividuals(v, selection.Lsl, selection.Usl, selection.Target);
            OutputRaw(SpcFormatter.Capability(cap));
            ShowGraph($"Process Capability of {selection.DataColumn}",
                p => Plots.CapabilityHistogram(p, selection.DataColumn, v, selection.Lsl, selection.Usl, selection.Target));
        }
        catch (Exception ex) { Log($"Capability: {ex.Message}"); }
    }

    // ---- Graph menu (filled in phase 1) ------------------------------------

    private void OnHistogram(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 1, out var numeric)) return;
        var dlg = new ColumnPickerWindow("Histogram", "Graph variables:", numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.SelectedColumns };
        int made = 0;
        foreach (var n in selection.SelectedColumns)
        {
            var v = ws.Find(n)!.NumericValues();
            if (v.Length == 0) { Log($"{n}: no data."); continue; }
            ShowGraph($"Histogram of {n}", p => Plots.Histogram(p, n, v));
            made++;
        }
        Log($"Histogram: {made} graph(s) created.");
    }

    private void OnBoxplot(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 1, out var numeric)) return;
        var dlg = new ColumnPickerWindow("Boxplot", "Graph variables:", numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.SelectedColumns };
        var series = selection.SelectedColumns
            .Select(n => (Name: n, Values: ws.Find(n)!.NumericValues()))
            .Where(t => t.Values.Length > 0).ToList();
        if (series.Count == 0) { Log("No data to plot."); return; }
        ShowGraph("Boxplot", p => Plots.Boxplot(p, series));
        Log($"Boxplot: {series.Count} group(s).");
    }

    private void OnScatterplot(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new XyPickerWindow(numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.XColumn, dlg.YColumn };
        var (xs, ys) = Columns.Pairwise(ws.Find(selection.XColumn)!, ws.Find(selection.YColumn)!);
        if (xs.Length == 0) { Log("No paired (X, Y) rows to plot."); return; }
        ShowGraph($"Scatterplot of {selection.YColumn} vs {selection.XColumn}",
            p => Plots.Scatter(p, selection.XColumn, selection.YColumn, xs, ys));
        Log($"Scatterplot: {xs.Length} points.");
    }

    private void OnTimeSeries(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 1, out var numeric)) return;
        var dlg = new ColumnPickerWindow("Time Series Plot", "Graph variables:", numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.SelectedColumns };
        int made = 0;
        foreach (var n in selection.SelectedColumns)
        {
            double[] v;
            try { v = ws.Find(n)!.SeriesValues(); }
            catch (ArgumentException ex) { Log(ex.Message); continue; }
            if (v.Length == 0) { Log($"{n}: no data."); continue; }
            ShowGraph($"Time Series Plot of {n}", p => Plots.TimeSeries(p, n, v));
            made++;
        }
        Log($"Time Series: {made} graph(s) created.");
    }

    private void OnProbabilityPlot(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 1, out var numeric)) return;
        var dlg = new ColumnPickerWindow("Probability Plot", "Graph variables:", numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var selection = new { dlg.SelectedColumns };
        int made = 0;
        foreach (var n in selection.SelectedColumns)
        {
            var v = ws.Find(n)!.NumericValues();
            if (v.Length < 3) { Log($"{n}: need at least 3 values."); continue; }
            ShowGraph($"Probability Plot of {n}", p => Plots.ProbabilityPlot(p, n, v));
            made++;
        }
        Log($"Probability Plot: {made} graph(s) created.");
    }

    // ---- misc --------------------------------------------------------------

    private void OnGridAutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        var dc = _table.Columns[e.PropertyName];
        if (dc != null && !string.IsNullOrEmpty(dc.Caption)) e.Column.Header = dc.Caption;
    }

    private void OnAbout(object sender, RoutedEventArgs e) =>
        MessageBox.Show(
            "StatStudio\nA Minitab-style statistics workbench.\n\n.NET 10 • WPF • Math.NET Numerics • ScottPlot",
            "About StatStudio", MessageBoxButton.OK, MessageBoxImage.Information);
}
