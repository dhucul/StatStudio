using System.Windows;
using System.Windows.Controls;

namespace StatStudio.Wpf.Dialogs;

[Flags]
public enum TsFields
{
    None = 0, TrendType = 1, Length = 2, Period = 4, Alpha = 8, Beta = 16,
    Gamma = 32, MaxLag = 64, Forecasts = 128, Multiplicative = 256,
}

public partial class TimeSeriesWindow : Window
{
    /// <summary>Upper bound on ACF/PACF lags; the PACF recursion is quadratic in this value.</summary>
    private const int MaxLagLimit = 500;

    public string SeriesColumn => (string)SeriesCombo.SelectedItem;
    public bool Quadratic => TrendCombo.SelectedIndex == 1;
    public int Length { get; private set; } = 3;
    public int Period { get; private set; } = 12;
    public double Alpha { get; private set; } = 0.2;
    public double Beta { get; private set; } = 0.1;
    public double Gamma { get; private set; } = 0.1;
    public int MaxLag { get; private set; } = 12;
    public int Forecasts { get; private set; } = 4;
    public bool Multiplicative => MultBox.IsChecked == true;

    public TimeSeriesWindow(IReadOnlyList<string> numeric, string title, TsFields fields)
    {
        InitializeComponent();
        Title = title;
        foreach (var c in numeric) SeriesCombo.Items.Add(c);
        SeriesCombo.SelectedIndex = 0;
        TrendCombo.Items.Add("Linear");
        TrendCombo.Items.Add("Quadratic");
        TrendCombo.SelectedIndex = 0;

        Show(TrendRow, fields.HasFlag(TsFields.TrendType));
        Show(LengthRow, fields.HasFlag(TsFields.Length));
        Show(PeriodRow, fields.HasFlag(TsFields.Period));
        Show(AlphaRow, fields.HasFlag(TsFields.Alpha));
        Show(BetaRow, fields.HasFlag(TsFields.Beta));
        Show(GammaRow, fields.HasFlag(TsFields.Gamma));
        Show(MaxLagRow, fields.HasFlag(TsFields.MaxLag));
        Show(ForecastRow, fields.HasFlag(TsFields.Forecasts));
        MultBox.Visibility = fields.HasFlag(TsFields.Multiplicative) ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void Show(UIElement el, bool on) => el.Visibility = on ? Visibility.Visible : Visibility.Collapsed;

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (SeriesCombo.SelectedItem is null) { Warn("Pick a series."); return; }
        if (LengthRow.Visibility == Visibility.Visible)
        {
            if (!TestOptions.ParseInt(LengthBox.Text, out var l) || l < 2) { Warn("MA length must be ≥ 2."); return; }
            Length = l;
        }
        if (PeriodRow.Visibility == Visibility.Visible)
        {
            if (!TestOptions.ParseInt(PeriodBox.Text, out var p) || p < 2) { Warn("Period must be ≥ 2."); return; }
            Period = p;
        }
        if (AlphaRow.Visibility == Visibility.Visible)
        {
            if (!TestOptions.ParseDouble(AlphaBox.Text, out var a) || a < 0 || a > 1) { Warn("Alpha must be between 0 and 1."); return; }
            Alpha = a;
        }
        if (BetaRow.Visibility == Visibility.Visible)
        {
            if (!TestOptions.ParseDouble(BetaBox.Text, out var b) || b < 0 || b > 1) { Warn("Beta must be between 0 and 1."); return; }
            Beta = b;
        }
        if (GammaRow.Visibility == Visibility.Visible)
        {
            if (!TestOptions.ParseDouble(GammaBox.Text, out var g) || g < 0 || g > 1) { Warn("Gamma must be between 0 and 1."); return; }
            Gamma = g;
        }
        if (MaxLagRow.Visibility == Visibility.Visible)
        {
            // The Durbin-Levinson recursion behind the PACF is O(maxLag^2) in time, so the lag
            // count needs a ceiling as well as a floor.
            if (!TestOptions.ParseInt(MaxLagBox.Text, out var ml) || ml < 0 || ml > MaxLagLimit)
            { Warn($"Maximum lag must be between 0 and {MaxLagLimit}."); return; }
            MaxLag = ml;
        }
        if (ForecastRow.Visibility == Visibility.Visible)
        {
            if (!TestOptions.ParseInt(ForecastBox.Text, out var f) || f < 0) { Warn("Forecast count must be nonnegative."); return; }
            Forecasts = f;
        }
        DialogResult = true;
    }

    private void Warn(string m) => MessageBox.Show(m, Title, MessageBoxButton.OK, MessageBoxImage.Information);
}
