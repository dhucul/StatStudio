using System.Windows;
using System.Windows.Controls;
using StatStudio.Core.Statistics;

namespace StatStudio.Wpf.Dialogs;

public partial class PowerWindow : Window
{
    public int TestIndex => TestCombo.SelectedIndex;          // 0=1-sample t, 1=2-sample t, 2=1 proportion
    public bool SolveForPower => SolveCombo.SelectedIndex == 0;
    public double Alpha { get; private set; } = 0.05;
    public Alternative Alt { get; private set; } = Alternative.TwoSided;
    public double EffectSize { get; private set; }
    public double P0 { get; private set; }
    public double P1 { get; private set; }
    public double N { get; private set; }
    public double TargetPower { get; private set; }

    public PowerWindow()
    {
        InitializeComponent();
        TestCombo.Items.Add("1-Sample t");
        TestCombo.Items.Add("2-Sample t");
        TestCombo.Items.Add("1 Proportion");
        TestCombo.SelectedIndex = 0;
        SolveCombo.Items.Add("Power (enter sample size)");
        SolveCombo.Items.Add("Sample size (enter power)");
        SolveCombo.SelectedIndex = 1;
        AltCombo.ItemsSource = TestOptions.AltLabels;
        AltCombo.SelectedIndex = 0;
    }

    private void OnTestChanged(object sender, SelectionChangedEventArgs e) => UpdateVisibility();
    private void OnSolveChanged(object sender, SelectionChangedEventArgs e) => UpdateVisibility();

    private void UpdateVisibility()
    {
        if (EffectRow is null) return; // during init
        bool prop = TestCombo.SelectedIndex == 2;
        EffectRow.Visibility = prop ? Visibility.Collapsed : Visibility.Visible;
        P0Row.Visibility = prop ? Visibility.Visible : Visibility.Collapsed;
        P1Row.Visibility = prop ? Visibility.Visible : Visibility.Collapsed;
        NRow.Visibility = SolveForPower ? Visibility.Visible : Visibility.Collapsed;
        PowerRow.Visibility = SolveForPower ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        Alpha = TestOptions.ParseDouble(AlphaBox.Text, out var av) && av > 0 && av < 1 ? av : 0.05;
        Alt = TestOptions.ParseAlt(AltCombo.SelectedIndex);

        if (TestCombo.SelectedIndex == 2)
        {
            if (!TestOptions.ParseDouble(P0Box.Text, out var p0) || !TestOptions.ParseDouble(P1Box.Text, out var p1)
                || p0 <= 0 || p0 >= 1 || p1 <= 0 || p1 >= 1 || p0 == p1)
            { Warn("Enter p0 and p1 in (0,1), p0 ≠ p1."); return; }
            P0 = p0; P1 = p1;
        }
        else
        {
            if (!TestOptions.ParseDouble(EffectBox.Text, out var d) || d == 0) { Warn("Enter a non-zero effect size."); return; }
            EffectSize = d;
        }

        if (SolveForPower)
        {
            if (!TestOptions.ParseDouble(NBox.Text, out var n) || n < 2) { Warn("Enter a sample size ≥ 2."); return; }
            N = n;
        }
        else
        {
            if (!TestOptions.ParseDouble(PowerBox.Text, out var pw) || pw <= 0 || pw >= 1) { Warn("Enter a target power in (0,1)."); return; }
            TargetPower = pw;
        }
        DialogResult = true;
    }

    private void Warn(string m) => MessageBox.Show(m, Title, MessageBoxButton.OK, MessageBoxImage.Information);
}
