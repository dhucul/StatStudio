using System.Windows;
using StatStudio.Core.Statistics;

namespace StatStudio.Wpf.Dialogs;

public partial class ProportionWindow : Window
{
    private readonly bool _two;

    public int Events1 { get; private set; }
    public int Trials1 { get; private set; }
    public int Events2 { get; private set; }
    public int Trials2 { get; private set; }
    public double P0 { get; private set; } = 0.5;
    public double Confidence { get; private set; } = 0.95;
    public Alternative Alt { get; private set; } = Alternative.TwoSided;

    public ProportionWindow(bool two)
    {
        InitializeComponent();
        _two = two;
        Title = two ? "2 Proportions" : "1 Proportion";
        AltCombo.ItemsSource = TestOptions.AltLabels;
        AltCombo.SelectedIndex = 0;

        if (two)
        {
            P0Label.Visibility = Visibility.Collapsed;
            P0Box.Visibility = Visibility.Collapsed;
        }
        else
        {
            Lbl2.Visibility = Visibility.Collapsed;
            Lbl2b.Visibility = Visibility.Collapsed;
            X2.Visibility = Visibility.Collapsed;
            N2.Visibility = Visibility.Collapsed;
        }
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (!TestOptions.ParseInt(X1.Text, out var x1) || !TestOptions.ParseInt(N1.Text, out var n1) || n1 <= 0 || x1 < 0 || x1 > n1)
        {
            MessageBox.Show("Sample 1 needs 0 ≤ events ≤ trials, trials > 0.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        Events1 = x1; Trials1 = n1;

        if (_two)
        {
            if (!TestOptions.ParseInt(X2.Text, out var x2) || !TestOptions.ParseInt(N2.Text, out var n2) || n2 <= 0 || x2 < 0 || x2 > n2)
            {
                MessageBox.Show("Sample 2 needs 0 ≤ events ≤ trials, trials > 0.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            Events2 = x2; Trials2 = n2;
        }
        else
        {
            if (!TestOptions.ParseDouble(P0Box.Text, out var p0) || p0 <= 0 || p0 >= 1)
            {
                MessageBox.Show("Hypothesized p must be between 0 and 1.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            P0 = p0;
        }

        Alt = TestOptions.ParseAlt(AltCombo.SelectedIndex);
        if (!TestOptions.TryParseConf(ConfBox.Text, out var confidence))
        {
            MessageBox.Show("Confidence must be between 0 and 1 (or between 0 and 100 as a percent).", Title,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        Confidence = confidence;
        DialogResult = true;
    }
}
