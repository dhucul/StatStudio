using System.Windows;

namespace StatStudio.Wpf.Dialogs;

public partial class SarimaWindow : Window
{
    public string SeriesColumn => (string)SeriesCombo.SelectedItem;
    public int P { get; private set; }
    public int D { get; private set; }
    public int Q { get; private set; }
    public int SP { get; private set; }
    public int SD { get; private set; }
    public int SQ { get; private set; }
    public int Season { get; private set; } = 12;
    public int Forecasts { get; private set; } = 12;
    public bool IncludeConstant { get; private set; }

    public SarimaWindow(IReadOnlyList<string> numeric)
    {
        InitializeComponent();
        foreach (var c in numeric) SeriesCombo.Items.Add(c);
        SeriesCombo.SelectedIndex = 0;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (SeriesCombo.SelectedItem is null) { Warn("Pick a series."); return; }
        if (!Ord(PBox.Text, 5, out var p) || !Ord(DBox.Text, 2, out var d) || !Ord(QBox.Text, 5, out var q) ||
            !Ord(SPBox.Text, 3, out var sp) || !Ord(SDBox.Text, 2, out var sd) || !Ord(SQBox.Text, 3, out var sq))
        { Warn("Orders must be small non-negative integers (p,q≤5; P,Q≤3; d,D≤2)."); return; }
        if (!TestOptions.ParseInt(SBox.Text, out var s) || s < 1 || s > 10000) { Warn("Seasonal period must be between 1 and 10000."); return; }
        // At s = 1 the seasonal polynomials collapse onto the regular ones, leaving the optimiser
        // on a flat ridge of equivalent parameterisations.
        if (s == 1 && (sp > 0 || sd > 0 || sq > 0))
        { Warn("A seasonal period of 1 has no seasonal structure — use a period ≥ 2, or set P, D and Q to 0."); return; }
        if (!TestOptions.ParseInt(ForecastBox.Text, out var f) || f < 0 || f > 10000) { Warn("Forecast count must be between 0 and 10000."); return; }
        P = p; D = d; Q = q; SP = sp; SD = sd; SQ = sq; Season = s; Forecasts = f;
        IncludeConstant = ConstBox.IsChecked == true;
        DialogResult = true;
    }

    private static bool Ord(string text, int max, out int v) =>
        TestOptions.ParseInt(text, out v) && v >= 0 && v <= max;

    private void Warn(string m) => MessageBox.Show(m, Title, MessageBoxButton.OK, MessageBoxImage.Information);
}
