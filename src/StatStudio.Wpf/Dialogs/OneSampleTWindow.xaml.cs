using System.Windows;
using System.Windows.Controls;
using StatStudio.Core.Statistics;

namespace StatStudio.Wpf.Dialogs;

public partial class OneSampleTWindow : Window
{
    private readonly List<CheckBox> _boxes = new();

    public List<string> SelectedColumns { get; } = new();
    public double Mu0 { get; private set; }
    public double Confidence { get; private set; } = 0.95;
    public Alternative Alt { get; private set; } = Alternative.TwoSided;

    public OneSampleTWindow(IEnumerable<string> columns)
    {
        InitializeComponent();
        foreach (var c in columns)
        {
            var cb = new CheckBox { Content = c, Margin = new Thickness(4, 3, 4, 3) };
            _boxes.Add(cb);
            List.Items.Add(cb);
        }
        AltCombo.ItemsSource = TestOptions.AltLabels;
        AltCombo.SelectedIndex = 0;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        SelectedColumns.Clear();
        foreach (var cb in _boxes) if (cb.IsChecked == true) SelectedColumns.Add((string)cb.Content);
        if (SelectedColumns.Count == 0)
        {
            MessageBox.Show("Select at least one variable.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (!TestOptions.ParseDouble(MeanBox.Text, out var mu))
        {
            MessageBox.Show("Enter a numeric hypothesized mean.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        Mu0 = mu;
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
