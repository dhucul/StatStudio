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
    }

    // ---- startup args / demo (used for screenshots and quick checks) --------

    private void ProcessArgs(string[] args)
    {
        for (int i = 1; i < args.Length; i++)
        {
            var a = args[i].ToLowerInvariant();
            switch (a)
            {
                case "--demo": var ws = BuildDemo(); RunDescriptives(ws, ws.NumericColumns().Select(c => c.Name));
                    DemoGraphs(ws); break;
                case "--shot-descriptives": BuildDemo(); var cw = CurrentWorksheet();
                    RunDescriptives(cw, cw.NumericColumns().Select(c => c.Name)); break;
                case "--shot-histogram": var w2 = BuildDemo(); ShowGraph("Histogram of Height", p => Plots.Histogram(p, "Height", w2.Find("Height")!.NumericValues())); break;
                case "--shot-boxplot": var w3 = BuildDemo(); ShowGraph("Boxplot", p => Plots.Boxplot(p,
                    new[] { ("Height", w3.Find("Height")!.NumericValues()), ("Weight", w3.Find("Weight")!.NumericValues()) })); break;
                case "--shot-scatter": var w4 = BuildDemo(); var (sx, sy) = Columns.Pairwise(w4.Find("Height")!, w4.Find("Weight")!);
                    ShowGraph("Scatterplot of Weight vs Height", p => Plots.Scatter(p, "Height", "Weight", sx, sy)); break;
                case "--shot-probplot": var w5 = BuildDemo(); ShowGraph("Probability Plot of Height", p => Plots.ProbabilityPlot(p, "Height", w5.Find("Height")!.NumericValues())); break;
                case "--shot-ttest": var w6 = BuildDemo(); var hv = w6.Find("Height")!.NumericValues();
                    OutputRaw(HypothesisFormatters.OneSampleT(HypothesisTests.OneSampleT(hv, 170, 0.95, Alternative.TwoSided), "Height", 170)); break;
                case "--shot-regression": var wr = BuildDemo();
                    var (rx, ry) = Columns.Pairwise(wr.Find("Height")!, wr.Find("Weight")!);
                    var rr = Regression.SimpleLinear(rx, ry, "Height", "Weight");
                    OutputRaw(RegressionFormatter.Format(rr));
                    ShowGraph("Fitted Line Plot of Weight vs Height",
                        p => Plots.FittedLine(p, "Height", "Weight", rx, ry, rr.Coefficients[0], rr.Coefficients[1])); break;
                case "--shot-imr": var wi = BuildDemo(); var iv = wi.Find("Height")!.NumericValues();
                    var (ic, mrc) = ControlCharts.IMR(iv);
                    OutputRaw(SpcFormatter.Pair("I-MR Chart", ic, mrc));
                    ShowGraph(ic.Title, p => Plots.ControlChart(p, ic)); break;
                case "--shot-capability": var wc = BuildDemo(); var cv = wc.Find("Height")!.NumericValues();
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
                        new[] { "Height", "Weight", "Group" }) { Owner = this }.Show();
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
                    var de = dr.Terms.Where(t => t.Name != "Constant")
                        .OrderByDescending(t => Math.Abs(double.IsNaN(t.T) ? t.Effect : t.T)).ToList();
                    ShowGraph("Pareto of Effects", p => Plots.LabeledBars(p, "Pareto of Effects", "Term", "|Standardized effect|",
                        de.Select(t => t.Name).ToList(), de.Select(t => Math.Abs(double.IsNaN(t.T) ? t.Effect : t.T)).ToList()));
                    break;
                case "--shot-pca": var wp = BuildDemo();
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
                case "--shot-tukey": var wtk = BuildAnovaDemo();
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
                case "--shot-correlation": var wco = BuildAnovaDemo();
                    OutputRaw(NonparametricFormatters.Correlation(Correlation.Matrix(
                        new[] { wco.Find("Method A")!, wco.Find("Method B")!, wco.Find("Method C")! }, false)));
                    OutputRaw(NonparametricFormatters.KruskalWallis(Nonparametric.KruskalWallis(new (string, double[])[]
                    {
                        ("Method A", wco.Find("Method A")!.NumericValues()),
                        ("Method B", wco.Find("Method B")!.NumericValues()),
                        ("Method C", wco.Find("Method C")!.NumericValues()),
                    }), "Yield", "Method")); break;
                case "--shot-anova": var wa = BuildAnovaDemo();
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
                try { LoadWorksheet(build()); Log($"Loaded sample dataset: {name}."); }
                catch (Exception ex) { ShowError("Sample data", ex); }
            };
            SampleDataMenu.Items.Add(item);
        }
    }

    private void NewWorksheet()
    {
        _table = WorksheetGrid.NewEmpty();
        Sheet.ItemsSource = _table.DefaultView;
        WorksheetHeader.Text = $"Worksheet: {_table.TableName}";
        RefreshNavigator();
        UpdateDims();
    }

    private void LoadWorksheet(CoreData.Worksheet ws)
    {
        _table = WorksheetGrid.ToDataTable(ws);
        for (int i = 0; i < 5; i++) _table.Rows.Add(_table.NewRow()); // room to type
        Sheet.ItemsSource = _table.DefaultView;
        WorksheetHeader.Text = $"Worksheet: {ws.Name}";
        RefreshNavigator();
        UpdateDims();
    }

    /// <summary>Current grid contents as a Core worksheet (commits any in-progress edit first).</summary>
    private CoreData.Worksheet CurrentWorksheet()
    {
        Sheet.CommitEdit(DataGridEditingUnit.Row, true);
        return WorksheetGrid.ToWorksheet(_table);
    }

    private void RefreshNavigator()
    {
        NavList.Items.Clear();
        var ws = WorksheetGrid.ToWorksheet(_table);
        NavList.Items.Add(ws.Name);
        foreach (var c in ws.Columns)
        {
            int n = c.Count - c.MissingCount();
            NavList.Items.Add($"   {c.Name}  ({(c.LooksNumeric() ? "num" : "text")}, n={n})");
        }
    }

    private void UpdateDims()
    {
        var ws = WorksheetGrid.ToWorksheet(_table);
        DimsText.Text = $"{ws.ColumnCount} cols × {ws.RowCount} rows";
    }

    // ---- session output ----------------------------------------------------

    internal void Log(string text)
    {
        Session.AppendText(text + Environment.NewLine);
        Session.ScrollToEnd();
    }

    /// <summary>Append a titled output block, Minitab-style.</summary>
    internal void Output(string title, string body)
    {
        Log("");
        Log(title);
        Log(new string('─', Math.Max(title.Length, 8)));
        Log(body.TrimEnd());
        Log("");
        StatusText.Text = title;
    }

    /// <summary>Append a pre-formatted block whose first line is its own title.</summary>
    internal void OutputRaw(string body)
    {
        Log("");
        Log(body.TrimEnd());
        Log("");
        var first = body.Split('\n').FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(first)) StatusText.Text = first.Trim();
    }

    private void ShowError(string title, Exception ex)
    {
        Log($"ERROR — {title}: {ex.Message}");
        MessageBox.Show(ex.Message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void NotYet(string feature) =>
        Log($"[{feature}] is not implemented yet — coming in a later phase.");

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
        Plots.ApplyDark(g.Plot);
        build(g.Plot);
        g.Render();
        g.Show();
    }

    // ---- File menu ---------------------------------------------------------

    private void OnNewWorksheet(object sender, RoutedEventArgs e) => NewWorksheet();

    private void OnOpenCsv(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Data files|*.csv;*.tsv;*.txt;*.xlsx|CSV/Text|*.csv;*.tsv;*.txt|Excel|*.xlsx|All files|*.*",
            Title = "Open data",
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var ws = dlg.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
                ? CoreData.WorksheetIo.ReadXlsx(dlg.FileName)
                : CoreData.WorksheetIo.ReadCsv(dlg.FileName);
            LoadWorksheet(ws);
            Log($"Opened '{System.IO.Path.GetFileName(dlg.FileName)}' — {ws.ColumnCount} columns, {ws.RowCount} rows.");
        }
        catch (Exception ex) { ShowError("Open failed", ex); }
    }

    private void OnSaveCsv(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV (comma)|*.csv|TSV (tab)|*.tsv|Excel|*.xlsx",
            FileName = _table.TableName + ".csv",
            Title = "Save worksheet",
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var ws = CurrentWorksheet();
            if (dlg.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                CoreData.WorksheetIo.WriteXlsx(ws, dlg.FileName);
            else
                CoreData.WorksheetIo.WriteCsv(ws, dlg.FileName);
            Log($"Saved worksheet to '{System.IO.Path.GetFileName(dlg.FileName)}'.");
        }
        catch (Exception ex) { ShowError("Save failed", ex); }
    }

    private void OnOpenProject(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "StatStudio project|*.ssproj|All files|*.*",
            Title = "Open project",
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            LoadWorksheet(CoreData.ProjectStore.Load(dlg.FileName));
            Log($"Opened project '{System.IO.Path.GetFileName(dlg.FileName)}'.");
        }
        catch (Exception ex) { ShowError("Open project failed", ex); }
    }

    private void OnSaveProject(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "StatStudio project|*.ssproj",
            FileName = _table.TableName + ".ssproj",
            Title = "Save project",
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            CoreData.ProjectStore.Save(CurrentWorksheet(), dlg.FileName);
            Log($"Saved project to '{System.IO.Path.GetFileName(dlg.FileName)}'.");
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
            "Variables (numeric):", numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;

        RunDescriptives(ws, dlg.SelectedColumns);
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
        foreach (var n in dlg.SelectedColumns)
        {
            var v = ws.Find(n)!.NumericValues();
            if (v.Length < 2) { Log($"{n}: need at least 2 values."); continue; }
            var r = HypothesisTests.OneSampleT(v, dlg.Mu0, dlg.Confidence, dlg.Alt);
            OutputRaw(HypothesisFormatters.OneSampleT(r, n, dlg.Mu0));
        }
    }

    private void OnTwoSampleT(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new TwoColumnWindow(numeric, "2-Sample t", showPooled: true) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var x1 = ws.Find(dlg.Column1)!.NumericValues();
        var x2 = ws.Find(dlg.Column2)!.NumericValues();
        if (x1.Length < 2 || x2.Length < 2) { Log("Each sample needs at least 2 values."); return; }
        var r = HypothesisTests.TwoSampleT(x1, x2, dlg.Pooled, dlg.Confidence, dlg.Alt);
        OutputRaw(HypothesisFormatters.TwoSampleT(r, dlg.Column1, dlg.Column2));
    }

    private void OnPairedT(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new TwoColumnWindow(numeric, "Paired t", showPooled: false) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var (x1, x2) = Columns.Pairwise(ws.Find(dlg.Column1)!, ws.Find(dlg.Column2)!);
        if (x1.Length < 2) { Log("Need at least 2 paired (row-matched) observations."); return; }
        var r = HypothesisTests.PairedT(x1, x2, dlg.Confidence, dlg.Alt);
        OutputRaw(HypothesisFormatters.PairedT(r, dlg.Column1, dlg.Column2));
    }

    private void OnOneProportion(object sender, RoutedEventArgs e)
    {
        var dlg = new ProportionWindow(two: false) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var r = HypothesisTests.OneProportion(dlg.Events1, dlg.Trials1, dlg.P0, dlg.Confidence, dlg.Alt);
        OutputRaw(HypothesisFormatters.OneProportion(r, "Sample", dlg.P0));
    }

    private void OnTwoProportions(object sender, RoutedEventArgs e)
    {
        var dlg = new ProportionWindow(two: true) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var r = HypothesisTests.TwoProportions(dlg.Events1, dlg.Trials1, dlg.Events2, dlg.Trials2, dlg.Confidence, dlg.Alt);
        OutputRaw(HypothesisFormatters.TwoProportions(r, "Sample 1", "Sample 2"));
    }

    private void OnChiSquareGof(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 1, out var numeric)) return;
        var dlg = new ColumnPickerWindow("Chi-Square Goodness-of-Fit",
            "Column(s) of observed counts (one test each):", numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        foreach (var n in dlg.SelectedColumns)
        {
            var obs = ws.Find(n)!.NumericValues();
            if (obs.Length < 2) { Log($"{n}: need at least 2 categories."); continue; }
            var r = HypothesisTests.ChiSquareGof(obs);
            var cats = Enumerable.Range(1, obs.Length).Select(i => i.ToString()).ToList();
            OutputRaw($"Goodness-of-Fit for {n}\n" + HypothesisFormatters.ChiSquareGof(r, cats));
        }
    }

    private void OnChiSquareAssoc(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new ColumnPickerWindow("Cross Tabulation & Chi-Square",
            "Columns forming the table (each column = a table column):", numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;

        var cols = dlg.SelectedColumns.Select(n => ws.Find(n)!.NumericValues()).ToList();
        int rows = cols.Min(c => c.Length);
        if (rows < 2) { Log("Need at least 2 rows of counts."); return; }
        var table = new double[rows, cols.Count];
        for (int i = 0; i < rows; i++)
            for (int j = 0; j < cols.Count; j++) table[i, j] = cols[j][i];

        var r = HypothesisTests.ChiSquareAssociation(table);
        var rowLabels = Enumerable.Range(1, rows).Select(i => $"R{i}").ToList();
        OutputRaw(HypothesisFormatters.Contingency(r, rowLabels, dlg.SelectedColumns));
    }

    private void OnOneWayAnova(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new ColumnPickerWindow("One-Way ANOVA",
            "Response columns (each column is a group):", numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var groups = dlg.SelectedColumns
            .Select(n => (n, ws.Find(n)!.NumericValues()))
            .Where(t => t.Item2.Length > 0).ToList();
        if (groups.Count < 2) { Log("Need at least 2 non-empty groups."); return; }
        var r = Anova.OneWay(groups);
        OutputRaw(AnovaFormatter.OneWay(r, "Factor", "Response"));
        try { OutputRaw(AdvancedFormatters.Tukey(AnovaExtensions.Tukey(groups))); }
        catch (Exception ex) { Log($"Tukey: {ex.Message}"); }
    }

    private void OnTwoWayAnova(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 1, out var numeric)) return;
        var all = ws.Columns.Select(c => c.Name).ToList();
        var dlg = new TwoWayAnovaWindow(numeric, all) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var (y, a, b) = Columns.Factorial(ws.Find(dlg.Response)!, ws.Find(dlg.FactorA)!, ws.Find(dlg.FactorB)!);
        if (y.Length < 4) { Log("Not enough complete rows."); return; }
        try { OutputRaw(AdvancedFormatters.TwoWayAnova(AnovaExtensions.TwoWay(y, a, b, dlg.FactorA, dlg.FactorB), dlg.Response)); }
        catch (Exception ex) { Log($"Two-way ANOVA: {ex.Message}"); }
    }

    private void OnEqualVariances(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new ColumnPickerWindow("Test for Equal Variances", "Group columns:", numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var groups = dlg.SelectedColumns.Select(n => (n, ws.Find(n)!.NumericValues()))
            .Where(t => t.Item2.Length > 1).ToList();
        if (groups.Count < 2) { Log("Need at least 2 groups with >1 value."); return; }
        try { OutputRaw(AdvancedFormatters.EqualVariances(VarianceTests.EqualVariances(groups), "Response", "Factor")); }
        catch (Exception ex) { Log($"Equal variances: {ex.Message}"); }
    }

    private void OnTwoVariances(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new TwoColumnWindow(numeric, "2 Variances (F-Test)", showPooled: false) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var x1 = ws.Find(dlg.Column1)!.NumericValues();
        var x2 = ws.Find(dlg.Column2)!.NumericValues();
        if (x1.Length < 2 || x2.Length < 2) { Log("Each sample needs at least 2 values."); return; }
        OutputRaw(AdvancedFormatters.FTest(VarianceTests.FTest(x1, x2, dlg.Confidence), dlg.Column1, dlg.Column2));
    }

    private void OnPolynomialRegression(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new PolynomialWindow(numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var (xs, ys) = Columns.Pairwise(ws.Find(dlg.XColumn)!, ws.Find(dlg.YColumn)!);
        if (xs.Length <= dlg.Degree + 1) { Log("Not enough points for that degree."); return; }
        try
        {
            var r = RegressionExtensions.Polynomial(xs, ys, dlg.Degree, dlg.XColumn, dlg.YColumn);
            OutputRaw(RegressionFormatter.Format(r));
            ShowGraph("Residuals vs Fitted", p => Plots.ResidualVsFitted(p, r.Fitted, r.Residuals));
        }
        catch (Exception ex) { Log($"Polynomial regression: {ex.Message}"); }
    }

    private void OnBestSubsets(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new RegressionWindow(numeric) { Owner = this, Title = "Best Subsets Regression" };
        if (dlg.ShowDialog() != true) return;
        var (y, x) = Columns.Design(ws.Find(dlg.Response)!, dlg.Predictors.Select(n => ws.Find(n)!).ToList());
        if (y.Length <= dlg.Predictors.Count + 1) { Log("Not enough complete rows."); return; }
        try { OutputRaw(AdvancedFormatters.BestSubsets(RegressionExtensions.BestSubsets(y, x, dlg.Predictors))); }
        catch (Exception ex) { Log($"Best subsets: {ex.Message}"); }
    }

    private void OnStepwise(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new RegressionWindow(numeric) { Owner = this, Title = "Stepwise Regression" };
        if (dlg.ShowDialog() != true) return;
        var (y, x) = Columns.Design(ws.Find(dlg.Response)!, dlg.Predictors.Select(n => ws.Find(n)!).ToList());
        if (y.Length <= dlg.Predictors.Count + 1) { Log("Not enough complete rows."); return; }
        try { OutputRaw(AdvancedFormatters.Stepwise(RegressionExtensions.Stepwise(y, x, dlg.Predictors))); }
        catch (Exception ex) { Log($"Stepwise: {ex.Message}"); }
    }

    private void OnLogistic(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new RegressionWindow(numeric) { Owner = this, Title = "Binary Logistic Regression" };
        if (dlg.ShowDialog() != true) return;
        var (y, x) = Columns.Design(ws.Find(dlg.Response)!, dlg.Predictors.Select(n => ws.Find(n)!).ToList());
        if (y.Length <= dlg.Predictors.Count + 1) { Log("Not enough complete rows."); return; }
        try { OutputRaw(AdvancedFormatters.Logistic(Logistic.Fit(y, x, dlg.Predictors, dlg.Response))); }
        catch (Exception ex) { Log($"Logistic regression: {ex.Message} (response must be coded 0/1)"); }
    }

    // ---- Multivariate & power ---------------------------------------------

    private void OnFishersExact(object sender, RoutedEventArgs e)
    {
        var dlg = new FisherWindow { Owner = this };
        if (dlg.ShowDialog() != true) return;
        OutputRaw(MultivariateFormatters.Fisher(FishersExact.Test(dlg.CellA, dlg.CellB, dlg.CellC, dlg.CellD)));
    }

    private void OnPca(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new ColumnPickerWindow("Principal Components", "Variables (2 or more):", numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var cols = dlg.SelectedColumns.Select(n => ws.Find(n)!).ToList();
        if (cols.Count < 2) { Log("Select at least two variables."); return; }
        var rows = Columns.Rows(cols);
        if (rows.Count < 2) { Log("Not enough complete rows."); return; }
        try
        {
            var r = Pca.Compute(rows.ToArray(), dlg.SelectedColumns, correlation: true);
            OutputRaw(MultivariateFormatters.Pca(r));
            ShowGraph("Scree Plot", p => Plots.Scree(p, r.Eigenvalues));
        }
        catch (Exception ex) { Log($"PCA: {ex.Message}"); }
    }

    private void OnKMeans(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 1, out var numeric)) return;
        var dlg = new KMeansWindow(numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var cols = dlg.SelectedColumns.Select(n => ws.Find(n)!).ToList();
        var rows = Columns.Rows(cols);
        if (rows.Count < dlg.K) { Log("Need at least k complete rows."); return; }
        try
        {
            var r = KMeans.Cluster(rows.ToArray(), dlg.K, dlg.SelectedColumns);
            OutputRaw(MultivariateFormatters.KMeans(r));
            if (dlg.SelectedColumns.Count == 2)
                ShowGraph("K-Means Clusters",
                    p => Plots.ClusterScatter(p, dlg.SelectedColumns[0], dlg.SelectedColumns[1], rows.ToArray(), r.Assignments, r.K));
        }
        catch (Exception ex) { Log($"K-Means: {ex.Message}"); }
    }

    // ---- Bayesian & mixed --------------------------------------------------

    private void OnBayesProportion(object sender, RoutedEventArgs e)
    {
        var dlg = new BayesProportionWindow { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var r = Bayes.Proportion(dlg.X, dlg.N, dlg.PriorA, dlg.PriorB, dlg.Confidence, dlg.Threshold);
        OutputRaw(BayesFormatters.Proportion(r, "Sample"));
    }

    private void OnBayesNormal(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 1, out var numeric)) return;
        var dlg = new BayesNormalWindow(numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var v = ws.Find(dlg.DataColumn)!.NumericValues();
        if (v.Length < 2) { Log("Need at least 2 values."); return; }
        var r = dlg.KnownVariance
            ? Bayes.NormalMeanKnownVar(v, dlg.PriorMean, dlg.PriorSd, dlg.KnownSigma, dlg.Confidence, dlg.Threshold)
            : Bayes.NormalMeanUnknownVar(v, dlg.Confidence, dlg.Threshold);
        OutputRaw(BayesFormatters.NormalMean(r, dlg.DataColumn));
    }

    private void OnBayesRegression(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new RegressionWindow(numeric) { Owner = this, Title = "Bayesian Linear Regression" };
        if (dlg.ShowDialog() != true) return;
        var (y, x) = Columns.Design(ws.Find(dlg.Response)!, dlg.Predictors.Select(n => ws.Find(n)!).ToList());
        if (y.Length <= dlg.Predictors.Count + 1) { Log("Not enough complete rows."); return; }
        try { OutputRaw(BayesFormatters.Regression(Bayes.LinearRegression(y, x, dlg.Predictors, dlg.Response))); }
        catch (Exception ex) { Log($"Bayesian regression: {ex.Message}"); }
    }

    private void OnOneWayRandom(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new ColumnPickerWindow("One-Way Random Effects",
            "Group columns (each column is a random-effect level):", numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var groups = dlg.SelectedColumns.Select(n => (n, ws.Find(n)!.NumericValues()))
            .Where(t => t.Item2.Length > 0).ToList();
        if (groups.Count < 2) { Log("Need at least 2 groups."); return; }
        try { OutputRaw(MixedFormatters.OneWayRandom(MixedModel.OneWayRandom(groups), "Response", "Group")); }
        catch (Exception ex) { Log($"Random-effects model: {ex.Message}"); }
    }

    private void OnFactorAnalysis(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new FactorWindow(numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var cols = dlg.SelectedColumns.Select(n => ws.Find(n)!).ToList();
        var rows = Columns.Rows(cols);
        if (rows.Count < 2) { Log("Not enough complete rows."); return; }
        try
        {
            var r = FactorAnalysis.Extract(rows.ToArray(), dlg.SelectedColumns, dlg.NumFactors, dlg.Varimax);
            OutputRaw(MultivariateFormatters.FactorAnalysis(r));
        }
        catch (Exception ex) { Log($"Factor analysis: {ex.Message}"); }
    }

    private void OnDistFit(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 1, out var numeric)) return;
        var dlg = new DistFitWindow(numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var t = ws.Find(dlg.TimesColumn)!.NumericValues();
        if (t.Length < 3) { Log("Need at least 3 observations."); return; }
        if (dlg.Distribution != "Normal" && t.Any(v => v <= 0))
        { Log($"{dlg.Distribution} requires all times > 0."); return; }
        try
        {
            var fit = dlg.Distribution switch
            {
                "Exponential" => Reliability.FitExponential(t),
                "Lognormal" => Reliability.FitLognormal(t),
                "Normal" => Reliability.FitNormal(t),
                _ => Reliability.FitWeibull(t),
            };
            OutputRaw(ReliabilityFormatters.DistributionFit(fit, dlg.TimesColumn));
            if (dlg.Distribution == "Weibull")
            {
                double beta = fit.Parameters[0].Value, eta = fit.Parameters[1].Value;
                ShowGraph($"Weibull Plot of {dlg.TimesColumn}", p => Plots.WeibullPlot(p, dlg.TimesColumn, t, beta, eta));
            }
            else ShowGraph($"Histogram of {dlg.TimesColumn}", p => Plots.Histogram(p, dlg.TimesColumn, t));
        }
        catch (Exception ex) { Log($"Distribution analysis: {ex.Message}"); }
    }

    private void OnKaplanMeier(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 1, out var numeric)) return;
        var dlg = new KaplanMeierWindow(numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;

        double[] times;
        bool[] censored;
        if (dlg.CensorColumn is null)
        {
            times = ws.Find(dlg.TimesColumn)!.NumericValues();
            censored = new bool[times.Length];
        }
        else
        {
            var (tv, cv) = Columns.Pairwise(ws.Find(dlg.TimesColumn)!, ws.Find(dlg.CensorColumn)!);
            times = tv;
            censored = cv.Select(v => v == 1).ToArray();
        }
        if (times.Length < 2) { Log("Need at least 2 observations."); return; }
        var km = Reliability.KaplanMeier(times, censored);
        OutputRaw(ReliabilityFormatters.KaplanMeier(km, dlg.TimesColumn));
        ShowGraph($"Kaplan-Meier Survival of {dlg.TimesColumn}",
            p => Plots.StepSurvival(p, dlg.TimesColumn, km.Rows.Select(r => r.Time).ToArray(),
                km.Rows.Select(r => r.Survival).ToArray()));
    }

    private void OnPowerSampleSize(object sender, RoutedEventArgs e)
    {
        var dlg = new PowerWindow { Owner = this };
        if (dlg.ShowDialog() != true) return;

        string test = dlg.TestIndex switch { 1 => "2-Sample t", 2 => "1 Proportion", _ => "1-Sample t" };
        string solveFor = dlg.SolveForPower ? "power" : "sample size";
        double n, power, effectVal;
        string effectDesc;

        if (dlg.TestIndex == 2)
        {
            effectDesc = $"p0 = {dlg.P0}, p1 = {dlg.P1}";
            if (dlg.SolveForPower) { n = dlg.N; power = Power.OneProportionPower(n, dlg.P0, dlg.P1, dlg.Alpha, dlg.Alt); }
            else { power = dlg.TargetPower; n = Power.OneProportionSampleSize(power, dlg.P0, dlg.P1, dlg.Alpha, dlg.Alt); }
        }
        else
        {
            effectVal = dlg.EffectSize;
            effectDesc = $"d = {effectVal}";
            bool two = dlg.TestIndex == 1;
            if (dlg.SolveForPower)
            {
                n = dlg.N;
                power = two ? Power.TwoSampleTPower(n, effectVal, dlg.Alpha, dlg.Alt)
                            : Power.OneSampleTPower(n, effectVal, dlg.Alpha, dlg.Alt);
            }
            else
            {
                power = dlg.TargetPower;
                n = two ? Power.TwoSampleTSampleSize(power, effectVal, dlg.Alpha, dlg.Alt)
                        : Power.OneSampleTSampleSize(power, effectVal, dlg.Alpha, dlg.Alt);
            }
        }
        OutputRaw(MultivariateFormatters.Power(test, solveFor, dlg.Alpha, dlg.Alt, effectDesc, n, power));
    }

    // ---- DOE & Gage R&R ----------------------------------------------------

    private void OnCreateFactorial(object sender, RoutedEventArgs e)
    {
        var dlg = new DoeCreateWindow { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var design = DoeDesign.FullFactorial(dlg.Factors, dlg.Replicates, dlg.CenterPoints, dlg.Randomize);
        var inv = System.Globalization.CultureInfo.InvariantCulture;

        var ws = new CoreData.Worksheet { Name = $"FactorialDesign_{dlg.Factors}f" };
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
        LoadWorksheet(ws);
        Log(DoeFormatters.Design(design));
    }

    private void OnCreateFractional(object sender, RoutedEventArgs e)
    {
        var dlg = new FractionalCreateWindow { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var design = DoeDesign.FractionalFactorial(dlg.Factors, dlg.Runs, dlg.Randomize);
        var inv = System.Globalization.CultureInfo.InvariantCulture;

        var ws = new CoreData.Worksheet { Name = $"FracFactorial_{dlg.Factors}f{dlg.Runs}r" };
        var so = ws.AddColumn("StdOrder");
        var ro = ws.AddColumn("RunOrder");
        var fcols = design.FactorNames.Select(fn => ws.AddColumn(fn)).ToList();
        foreach (var run in design.RunList)
        {
            so.Add(run.StdOrder.ToString());
            ro.Add(run.RunOrder.ToString());
            for (int j = 0; j < fcols.Count; j++) fcols[j].Add(run.Factors[j].ToString(inv));
        }
        LoadWorksheet(ws);
        Log(DoeFormatters.Fractional(design));
    }

    private void OnCalculator(object sender, RoutedEventArgs e)
    {
        var dlg = new CalculatorWindow { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var ws = CurrentWorksheet();
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        try
        {
            var result = CoreData.Calculator.Evaluate(dlg.Expression, ws);
            var col = ws.Find(dlg.TargetColumn) ?? ws.AddColumn(dlg.TargetColumn);
            col.Clear();
            foreach (var v in result) col.Add(double.IsNaN(v) ? null : v.ToString("0.##########", inv));
            LoadWorksheet(ws);
            Log($"Calculated '{dlg.TargetColumn}' = {dlg.Expression}  ({result.Length} rows).");
        }
        catch (Exception ex) { ShowError("Calculator", ex); }
    }

    private void OnCreateMixture(object sender, RoutedEventArgs e)
    {
        var dlg = new MixtureCreateWindow { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var design = dlg.IsLattice
            ? MixtureDesign.SimplexLattice(dlg.Components, dlg.Degree, dlg.Randomize)
            : MixtureDesign.SimplexCentroid(dlg.Components, dlg.Randomize);
        var inv = System.Globalization.CultureInfo.InvariantCulture;

        var ws = new CoreData.Worksheet { Name = $"Mixture_{dlg.Components}c" };
        var so = ws.AddColumn("StdOrder");
        var ro = ws.AddColumn("RunOrder");
        var pt = ws.AddColumn("PtType", CoreData.ColumnType.Text);
        var ccols = design.ComponentNames.Select(n => ws.AddColumn(n)).ToList();
        foreach (var run in design.RunList)
        {
            so.Add(run.StdOrder.ToString());
            ro.Add(run.RunOrder.ToString());
            pt.Add(run.PointType);
            for (int j = 0; j < ccols.Count; j++) ccols[j].Add(run.Components[j].ToString("0.#####", inv));
        }
        LoadWorksheet(ws);
        Log(DoeFormatters.Mixture(design));
    }

    private void OnAnalyzeMixture(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new RegressionWindow(numeric) { Owner = this, Title = "Analyze Mixture Design (response, then components)" };
        if (dlg.ShowDialog() != true) return;
        var (y, comps) = Columns.Design(ws.Find(dlg.Response)!, dlg.Predictors.Select(n => ws.Find(n)!).ToList());
        if (comps.Length < 2) { Log("Select at least two components."); return; }
        try
        {
            var r = MixtureAnalysis.Fit(y, comps, dlg.Predictors, quadratic: true);
            OutputRaw(DoeFormatters.MixtureModel(r));
        }
        catch (Exception ex) { Log($"Mixture analysis: {ex.Message}"); }
    }

    private void OnCreateRsm(object sender, RoutedEventArgs e)
    {
        var dlg = new RsmCreateWindow { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var design = dlg.IsBoxBehnken
            ? ResponseSurface.BoxBehnken(dlg.Factors, dlg.CenterPoints, dlg.Randomize)
            : ResponseSurface.CentralComposite(dlg.Factors, dlg.CenterPoints, dlg.FaceCentered, dlg.Randomize);
        var inv = System.Globalization.CultureInfo.InvariantCulture;

        var ws = new CoreData.Worksheet { Name = dlg.IsBoxBehnken ? $"BoxBehnken_{dlg.Factors}f" : $"CCD_{dlg.Factors}f" };
        var so = ws.AddColumn("StdOrder");
        var ro = ws.AddColumn("RunOrder");
        var pt = ws.AddColumn("PtType", CoreData.ColumnType.Text);
        var fcols = design.FactorNames.Select(fn => ws.AddColumn(fn)).ToList();
        foreach (var run in design.RunList)
        {
            so.Add(run.StdOrder.ToString());
            ro.Add(run.RunOrder.ToString());
            pt.Add(run.PointType);
            for (int j = 0; j < fcols.Count; j++) fcols[j].Add(run.Factors[j].ToString("0.#####", inv));
        }
        LoadWorksheet(ws);
        Log(DoeFormatters.Rsm(design));
    }

    private void OnAnalyzeRsm(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new RegressionWindow(numeric) { Owner = this, Title = "Analyze Response Surface (quadratic)" };
        if (dlg.ShowDialog() != true) return;
        var (y, x) = Columns.Design(ws.Find(dlg.Response)!, dlg.Predictors.Select(n => ws.Find(n)!).ToList());
        if (x.Length < 2) { Log("Select at least two factors."); return; }
        int terms = 1 + 2 * x.Length + x.Length * (x.Length - 1) / 2;
        if (y.Length <= terms) { Log($"Need more than {terms} complete runs for a quadratic model in {x.Length} factors."); return; }
        try
        {
            var r = ResponseSurface.Analyze(y, x, dlg.Predictors, dlg.Response);
            OutputRaw("Response Surface Regression (full quadratic model)\n\n" + RegressionFormatter.Format(r));
            ShowGraph("Residuals vs Fitted", p => Plots.ResidualVsFitted(p, r.Fitted, r.Residuals));
        }
        catch (Exception ex) { Log($"Response surface: {ex.Message}"); }
    }

    private void OnAnalyzeFactorial(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new RegressionWindow(numeric) { Owner = this, Title = "Analyze Factorial Design" };
        if (dlg.ShowDialog() != true) return;
        var (y, x) = Columns.Design(ws.Find(dlg.Response)!, dlg.Predictors.Select(n => ws.Find(n)!).ToList());
        if (y.Length < 4) { Log("Need at least 4 complete runs."); return; }
        try
        {
            var r = FactorialAnalysis.Analyze(y, x, dlg.Predictors, dlg.Response);
            OutputRaw(DoeFormatters.Factorial(r));
            var effects = r.Terms.Where(t => t.Name != "Constant")
                .OrderByDescending(t => Math.Abs(double.IsNaN(t.T) ? t.Effect : t.T)).ToList();
            string yl = r.DfError > 0 ? "|Standardized effect|" : "|Effect|";
            ShowGraph("Pareto of Effects", p => Plots.LabeledBars(p, "Pareto of Effects", "Term", yl,
                effects.Select(t => t.Name).ToList(),
                effects.Select(t => Math.Abs(double.IsNaN(t.T) ? t.Effect : t.T)).ToList()));
        }
        catch (Exception ex) { Log($"Factorial analysis: {ex.Message}"); }
    }

    private void OnGageRR(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 1, out var numeric)) return;
        var all = ws.Columns.Select(c => c.Name).ToList();
        var dlg = new TwoWayAnovaWindow(numeric, all) { Owner = this, Title = "Gage R&R (Crossed) — Response, Part, Operator" };
        if (dlg.ShowDialog() != true) return;
        var (y, part, op) = Columns.Factorial(ws.Find(dlg.Response)!, ws.Find(dlg.FactorA)!, ws.Find(dlg.FactorB)!);
        try
        {
            var g = GageRR.Analyze(y, part, op);
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
        return ws.Find(name)!.NumericValues();
    }

    private void OnTrendAnalysis(object sender, RoutedEventArgs e)
    {
        var v = OpenSeries("Trend Analysis", TsFields.TrendType | TsFields.Forecasts, out var dlg, out var name);
        if (v is null) return;
        if (v.Length < 3) { Log("Need at least 3 points."); return; }
        var r = dlg.Quadratic ? TimeSeries.QuadraticTrend(v, dlg.Forecasts) : TimeSeries.LinearTrend(v, dlg.Forecasts);
        OutputRaw(TimeSeriesFormatters.Trend(r, name, v.Length));
        ShowGraph($"Trend Analysis of {name}", p => Plots.TimeSeriesFit(p, name, v, r.Fitted, r.Forecasts));
    }

    private void OnMovingAverage(object sender, RoutedEventArgs e)
    {
        var v = OpenSeries("Moving Average", TsFields.Length | TsFields.Forecasts, out var dlg, out var name);
        if (v is null) return;
        if (v.Length <= dlg.Length) { Log("Series shorter than the MA length."); return; }
        var r = TimeSeries.MovingAverage(v, dlg.Length, dlg.Forecasts);
        OutputRaw(TimeSeriesFormatters.Smoothing(r, name, v.Length));
        ShowGraph($"Moving Average of {name}", p => Plots.TimeSeriesFit(p, name, v, r.Fitted, r.Forecasts));
    }

    private void OnSingleExp(object sender, RoutedEventArgs e)
    {
        var v = OpenSeries("Single Exponential Smoothing", TsFields.Alpha | TsFields.Forecasts, out var dlg, out var name);
        if (v is null) return;
        if (v.Length < 2) { Log("Need at least 2 points."); return; }
        var r = TimeSeries.SingleExp(v, dlg.Alpha, dlg.Forecasts);
        OutputRaw(TimeSeriesFormatters.Smoothing(r, name, v.Length));
        ShowGraph($"Single Exp Smoothing of {name}", p => Plots.TimeSeriesFit(p, name, v, r.Fitted, r.Forecasts));
    }

    private void OnDoubleExp(object sender, RoutedEventArgs e)
    {
        var v = OpenSeries("Double Exponential Smoothing", TsFields.Alpha | TsFields.Beta | TsFields.Forecasts, out var dlg, out var name);
        if (v is null) return;
        if (v.Length < 3) { Log("Need at least 3 points."); return; }
        var r = TimeSeries.DoubleExp(v, dlg.Alpha, dlg.Beta, dlg.Forecasts);
        OutputRaw(TimeSeriesFormatters.Smoothing(r, name, v.Length));
        ShowGraph($"Double Exp Smoothing of {name}", p => Plots.TimeSeriesFit(p, name, v, r.Fitted, r.Forecasts));
    }

    private void OnWinters(object sender, RoutedEventArgs e)
    {
        var v = OpenSeries("Winters' Method",
            TsFields.Period | TsFields.Alpha | TsFields.Beta | TsFields.Gamma | TsFields.Multiplicative | TsFields.Forecasts,
            out var dlg, out var name);
        if (v is null) return;
        try
        {
            var r = TimeSeries.Winters(v, dlg.Period, dlg.Alpha, dlg.Beta, dlg.Gamma, dlg.Multiplicative, dlg.Forecasts);
            OutputRaw(TimeSeriesFormatters.Smoothing(r, name, v.Length));
            ShowGraph($"Winters' Method of {name}", p => Plots.TimeSeriesFit(p, name, v, r.Fitted, r.Forecasts));
        }
        catch (Exception ex) { Log($"Winters: {ex.Message}"); }
    }

    private void OnDecomposition(object sender, RoutedEventArgs e)
    {
        var v = OpenSeries("Time Series Decomposition", TsFields.Period | TsFields.Multiplicative, out var dlg, out var name);
        if (v is null) return;
        if (v.Length < 2 * dlg.Period) { Log("Need at least two full seasons."); return; }
        var r = TimeSeries.Decompose(v, dlg.Period, dlg.Multiplicative);
        OutputRaw(TimeSeriesFormatters.Decomposition(r, name));
        ShowGraph($"Decomposition of {name} (trend)", p => Plots.TimeSeriesFit(p, name, v, r.Trend, Array.Empty<double>()));
    }

    private void OnArima(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 1, out var numeric)) return;
        var dlg = new ArimaWindow(numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var v = ws.Find(dlg.SeriesColumn)!.NumericValues();
        try
        {
            var r = Arima.Fit(v, dlg.P, dlg.D, dlg.Q, dlg.Forecasts, dlg.IncludeConstant);
            OutputRaw(TimeSeriesFormatters.Arima(r, dlg.SeriesColumn));
            if (r.Forecasts.Length > 0)
                ShowGraph($"ARIMA Forecast of {dlg.SeriesColumn}",
                    p => Plots.ForecastPlot(p, dlg.SeriesColumn, v, r.Forecasts, r.ForecastLower, r.ForecastUpper));
        }
        catch (Exception ex) { Log($"ARIMA: {ex.Message}"); }
    }

    private void OnSarima(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 1, out var numeric)) return;
        var dlg = new SarimaWindow(numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var v = ws.Find(dlg.SeriesColumn)!.NumericValues();
        try
        {
            var r = Sarima.Fit(v, dlg.P, dlg.D, dlg.Q, dlg.SP, dlg.SD, dlg.SQ, dlg.Season, dlg.Forecasts, dlg.IncludeConstant);
            OutputRaw(TimeSeriesFormatters.Sarima(r, dlg.SeriesColumn));
            if (r.Forecasts.Length > 0)
                ShowGraph($"SARIMA Forecast of {dlg.SeriesColumn}",
                    p => Plots.ForecastPlot(p, dlg.SeriesColumn, v, r.Forecasts, r.ForecastLower, r.ForecastUpper));
        }
        catch (Exception ex) { Log($"SARIMA: {ex.Message}"); }
    }

    private void OnAcf(object sender, RoutedEventArgs e) => RunAcf(false);
    private void OnPacf(object sender, RoutedEventArgs e) => RunAcf(true);

    private void RunAcf(bool partial)
    {
        var v = OpenSeries(partial ? "Partial Autocorrelation" : "Autocorrelation", TsFields.MaxLag, out var dlg, out var name);
        if (v is null) return;
        if (v.Length < 4) { Log("Need at least 4 points."); return; }
        var r = TimeSeries.Autocorrelation(v, dlg.MaxLag);
        OutputRaw(TimeSeriesFormatters.Acf(r, name, partial));
        var vals = partial ? r.Pacf : r.Acf;
        ShowGraph($"{(partial ? "PACF" : "ACF")} of {name}",
            p => Plots.Acf(p, $"{(partial ? "PACF" : "ACF")} of {name}", vals, r.N, partial ? "PACF" : "ACF"));
    }

    // ---- Nonparametrics ----------------------------------------------------

    private void OnMannWhitney(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new TwoColumnWindow(numeric, "Mann-Whitney", showPooled: false) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var x1 = ws.Find(dlg.Column1)!.NumericValues();
        var x2 = ws.Find(dlg.Column2)!.NumericValues();
        if (x1.Length < 1 || x2.Length < 1) { Log("Each sample needs data."); return; }
        var r = Nonparametric.MannWhitney(x1, x2, dlg.Alt);
        OutputRaw(NonparametricFormatters.MannWhitney(r, dlg.Column1, dlg.Column2));
    }

    private void OnWilcoxon(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 1, out var numeric)) return;
        var dlg = new OneSampleTWindow(numeric) { Owner = this, Title = "Wilcoxon Signed-Rank" };
        if (dlg.ShowDialog() != true) return;
        foreach (var n in dlg.SelectedColumns)
        {
            var v = ws.Find(n)!.NumericValues();
            if (v.Length < 2) { Log($"{n}: need at least 2 values."); continue; }
            OutputRaw(NonparametricFormatters.Wilcoxon(
                Nonparametric.WilcoxonSignedRank(v, dlg.Mu0, dlg.Alt), n, dlg.Mu0));
        }
    }

    private void OnKruskalWallis(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new ColumnPickerWindow("Kruskal-Wallis", "Response columns (each column is a group):", numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var groups = dlg.SelectedColumns.Select(n => (n, ws.Find(n)!.NumericValues()))
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
        foreach (var n in dlg.SelectedColumns)
        {
            var v = ws.Find(n)!.NumericValues();
            if (v.Length < 1) { Log($"{n}: no data."); continue; }
            OutputRaw(NonparametricFormatters.SignTest(
                Nonparametric.SignTest(v, dlg.Mu0, dlg.Alt), n, dlg.Mu0));
        }
    }

    private void OnRunsTest(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 1, out var numeric)) return;
        var dlg = new ColumnPickerWindow("Runs Test", "Columns to test for randomness:", numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        foreach (var n in dlg.SelectedColumns)
        {
            var v = ws.Find(n)!.NumericValues();
            if (v.Length < 3) { Log($"{n}: need at least 3 values."); continue; }
            OutputRaw(NonparametricFormatters.RunsTest(Nonparametric.RunsTest(v), n));
        }
    }

    // ---- Correlation & normality ------------------------------------------

    private void OnCorrelationPearson(object sender, RoutedEventArgs e) => Correlate(false);
    private void OnCorrelationSpearman(object sender, RoutedEventArgs e) => Correlate(true);

    private void Correlate(bool spearman)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new ColumnPickerWindow(spearman ? "Correlation (Spearman)" : "Correlation (Pearson)",
            "Variables (2 or more):", numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var cols = dlg.SelectedColumns.Select(n => ws.Find(n)!).ToList();
        if (cols.Count < 2) { Log("Select at least two variables."); return; }
        OutputRaw(NonparametricFormatters.Correlation(Correlation.Matrix(cols, spearman)));
    }

    private void OnNormalityTest(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 1, out var numeric)) return;
        var dlg = new ColumnPickerWindow("Normality Test", "Variables to test:", numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        foreach (var n in dlg.SelectedColumns)
        {
            var v = ws.Find(n)!.NumericValues();
            if (v.Length < 3) { Log($"{n}: need at least 3 values."); continue; }
            OutputRaw(NonparametricFormatters.AndersonDarling(Normality.AndersonDarling(v), n));
            ShowGraph($"Probability Plot of {n}", p => Plots.ProbabilityPlot(p, n, v));
        }
    }
    private void OnSimpleRegression(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new XyPickerWindow(numeric, "Simple Regression") { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var (xs, ys) = Columns.Pairwise(ws.Find(dlg.XColumn)!, ws.Find(dlg.YColumn)!);
        if (xs.Length < 3) { Log("Need at least 3 paired observations."); return; }
        try
        {
            var r = Regression.SimpleLinear(xs, ys, dlg.XColumn, dlg.YColumn);
            OutputRaw(RegressionFormatter.Format(r));
            ShowGraph($"Fitted Line Plot of {dlg.YColumn} vs {dlg.XColumn}",
                p => Plots.FittedLine(p, dlg.XColumn, dlg.YColumn, xs, ys, r.Coefficients[0], r.Coefficients[1]));
            ShowGraph("Residuals vs Fitted", p => Plots.ResidualVsFitted(p, r.Fitted, r.Residuals));
        }
        catch (Exception ex) { Log($"Regression: {ex.Message}"); }
    }

    private void OnMultipleRegression(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new RegressionWindow(numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var resp = ws.Find(dlg.Response)!;
        var preds = dlg.Predictors.Select(n => ws.Find(n)!).ToList();
        var (y, x) = Columns.Design(resp, preds);
        if (y.Length <= preds.Count + 1) { Log("Not enough complete rows for the number of predictors."); return; }
        try
        {
            var r = Regression.Fit(y, x, dlg.Predictors, dlg.Response);
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
            "Subgroup columns (each row across them is a subgroup):", numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var cols = dlg.SelectedColumns.Select(n => ws.Find(n)!).ToList();
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
        var dlg = new ColumnPickerWindow("I-MR Chart", "Column of individual measurements:", numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var v = ws.Find(dlg.SelectedColumns[0])!.NumericValues();
        if (v.Length < 2) { Log("Need at least 2 values."); return; }
        var (ind, mr) = ControlCharts.IMR(v);
        OutputRaw(SpcFormatter.Pair("I-MR Chart", ind, mr));
        ShowGraph(ind.Title, p => Plots.ControlChart(p, ind));
        ShowGraph(mr.Title, p => Plots.ControlChart(p, mr));
    }

    private int[] IntColumn(CoreData.Worksheet ws, string name) =>
        ws.Find(name)!.NumericValues().Select(v => (int)Math.Round(v)).ToArray();

    private void OnPChart(object sender, RoutedEventArgs e) => AttributeChart("P");
    private void OnUChart(object sender, RoutedEventArgs e) => AttributeChart("U");

    private void AttributeChart(string kind)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 2, out var numeric)) return;
        var dlg = new AttributeChartWindow(numeric, $"{kind} Chart", needSizesColumn: true, needConstantSize: false) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var counts = IntColumn(ws, dlg.CountsColumn);
        var sizes = IntColumn(ws, dlg.SizesColumn);
        int m = Math.Min(counts.Length, sizes.Length);
        if (m < 2) { Log("Need at least 2 rows."); return; }
        var chart = kind == "P"
            ? ControlCharts.PChart(counts.Take(m).ToArray(), sizes.Take(m).ToArray())
            : ControlCharts.UChart(counts.Take(m).ToArray(), sizes.Take(m).ToArray());
        OutputRaw(SpcFormatter.Chart(chart));
        ShowGraph(chart.Title, p => Plots.ControlChart(p, chart));
    }

    private void OnNpChart(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 1, out var numeric)) return;
        var dlg = new AttributeChartWindow(numeric, "NP Chart", needSizesColumn: false, needConstantSize: true) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var counts = IntColumn(ws, dlg.CountsColumn);
        if (counts.Length < 2) { Log("Need at least 2 rows."); return; }
        var chart = ControlCharts.NPChart(counts, dlg.ConstantSize);
        OutputRaw(SpcFormatter.Chart(chart));
        ShowGraph(chart.Title, p => Plots.ControlChart(p, chart));
    }

    private void OnCChart(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 1, out var numeric)) return;
        var dlg = new AttributeChartWindow(numeric, "C Chart", needSizesColumn: false, needConstantSize: false) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var counts = IntColumn(ws, dlg.CountsColumn);
        if (counts.Length < 2) { Log("Need at least 2 rows."); return; }
        var chart = ControlCharts.CChart(counts);
        OutputRaw(SpcFormatter.Chart(chart));
        ShowGraph(chart.Title, p => Plots.ControlChart(p, chart));
    }

    private void OnCapability(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 1, out var numeric)) return;
        var dlg = new CapabilityWindow(numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var v = ws.Find(dlg.DataColumn)!.NumericValues();
        if (v.Length < 2) { Log("Need at least 2 values."); return; }
        var cap = Capability.FromIndividuals(v, dlg.Lsl, dlg.Usl, dlg.Target);
        OutputRaw(SpcFormatter.Capability(cap));
        ShowGraph($"Process Capability of {dlg.DataColumn}",
            p => Plots.CapabilityHistogram(p, dlg.DataColumn, v, dlg.Lsl, dlg.Usl, dlg.Target));
    }

    // ---- Graph menu (filled in phase 1) ------------------------------------

    private void OnHistogram(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 1, out var numeric)) return;
        var dlg = new ColumnPickerWindow("Histogram", "Graph variables:", numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        int made = 0;
        foreach (var n in dlg.SelectedColumns)
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
        var series = dlg.SelectedColumns
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
        var (xs, ys) = Columns.Pairwise(ws.Find(dlg.XColumn)!, ws.Find(dlg.YColumn)!);
        if (xs.Length == 0) { Log("No paired (X, Y) rows to plot."); return; }
        ShowGraph($"Scatterplot of {dlg.YColumn} vs {dlg.XColumn}",
            p => Plots.Scatter(p, dlg.XColumn, dlg.YColumn, xs, ys));
        Log($"Scatterplot: {xs.Length} points.");
    }

    private void OnTimeSeries(object sender, RoutedEventArgs e)
    {
        var ws = CurrentWorksheet();
        if (!RequireNumeric(ws, 1, out var numeric)) return;
        var dlg = new ColumnPickerWindow("Time Series Plot", "Graph variables:", numeric) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        int made = 0;
        foreach (var n in dlg.SelectedColumns)
        {
            var v = ws.Find(n)!.NumericValues();
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
        int made = 0;
        foreach (var n in dlg.SelectedColumns)
        {
            var v = ws.Find(n)!.NumericValues();
            if (v.Length < 3) { Log($"{n}: need at least 3 values."); continue; }
            ShowGraph($"Probability Plot of {n}", p => Plots.ProbabilityPlot(p, n, v));
            made++;
        }
        Log($"Probability Plot: {made} graph(s) created.");
    }

    // ---- misc --------------------------------------------------------------

    private void OnNavDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) { }

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
