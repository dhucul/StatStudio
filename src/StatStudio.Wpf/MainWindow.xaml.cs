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
