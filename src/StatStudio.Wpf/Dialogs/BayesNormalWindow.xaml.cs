using System.Windows;

namespace StatStudio.Wpf.Dialogs;

public partial class BayesNormalWindow : Window
{
    public string DataColumn => (string)DataCombo.SelectedItem;
    public bool KnownVariance => KnownBox.IsChecked == true;
    public double PriorMean { get; private set; }
    public double PriorSd { get; private set; } = 10;
    public double KnownSigma { get; private set; } = 1;
    public double Threshold { get; private set; }
    public double Confidence { get; private set; } = 0.95;

    public BayesNormalWindow(IReadOnlyList<string> numeric)
    {
        InitializeComponent();
        foreach (var c in numeric) DataCombo.Items.Add(c);
        DataCombo.SelectedIndex = 0;
        UpdateRows();
    }

    private void OnKnownChanged(object sender, RoutedEventArgs e) => UpdateRows();

    private void UpdateRows()
    {
        var vis = KnownVariance ? Visibility.Visible : Visibility.Collapsed;
        if (PriorMeanRow != null) PriorMeanRow.Visibility = vis;
        if (PriorSdRow != null) PriorSdRow.Visibility = vis;
        if (SigmaRow != null) SigmaRow.Visibility = vis;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (DataCombo.SelectedItem is null) { Warn("Pick a data column."); return; }
        if (KnownVariance)
        {
            if (!TestOptions.ParseDouble(PriorMeanBox.Text, out var pm)) { Warn("Enter a prior mean."); return; }
            if (!TestOptions.ParseDouble(PriorSdBox.Text, out var ps) || ps <= 0) { Warn("Prior SD must be > 0."); return; }
            if (!TestOptions.ParseDouble(SigmaBox.Text, out var sg) || sg <= 0) { Warn("Known σ must be > 0."); return; }
            PriorMean = pm; PriorSd = ps; KnownSigma = sg;
        }
        if (!TestOptions.ParseDouble(ThreshBox.Text, out var th)) { Warn("Enter a finite threshold."); return; }
        if (!TestOptions.TryParseConf(ConfBox.Text, out var confidence))
        { Warn("Confidence must be between 0 and 1 (or between 0 and 100 as a percent)."); return; }
        Threshold = th;
        Confidence = confidence;
        DialogResult = true;
    }

    private void Warn(string m) => MessageBox.Show(m, Title, MessageBoxButton.OK, MessageBoxImage.Information);
}
