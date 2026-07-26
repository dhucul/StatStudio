using System.Windows;

namespace StatStudio.Wpf.Dialogs;

public partial class ArimaWindow : Window
{
    public string SeriesColumn => (string)SeriesCombo.SelectedItem;
    public int P { get; private set; } = 1;
    public int D { get; private set; }
    public int Q { get; private set; }
    public int Forecasts { get; private set; } = 6;
    public bool IncludeConstant => ConstBox.IsChecked == true;

    public ArimaWindow(IReadOnlyList<string> numeric)
    {
        InitializeComponent();
        foreach (var c in numeric) SeriesCombo.Items.Add(c);
        SeriesCombo.SelectedIndex = 0;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (SeriesCombo.SelectedItem is null) { Warn("Pick a series."); return; }
        if (!TestOptions.ParseInt(PBox.Text, out var p) || p < 0 || p > 5) { Warn("p must be 0–5."); return; }
        if (!TestOptions.ParseInt(DBox.Text, out var d) || d < 0 || d > 2) { Warn("d must be 0–2."); return; }
        if (!TestOptions.ParseInt(QBox.Text, out var q) || q < 0 || q > 5) { Warn("q must be 0–5."); return; }
        if (p == 0 && q == 0 && !IncludeConstant && d == 0) { Warn("Specify at least one term."); return; }
        if (!TestOptions.ParseInt(ForecastBox.Text, out var f) || f < 0) { Warn("Forecast count must be nonnegative."); return; }
        P = p; D = d; Q = q; Forecasts = f;
        DialogResult = true;
    }

    private void Warn(string m) => MessageBox.Show(m, Title, MessageBoxButton.OK, MessageBoxImage.Information);
}
