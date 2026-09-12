using System.Windows;
using StatStudio.Core.Statistics;

namespace StatStudio.Wpf.Dialogs;

public partial class TwoColumnWindow : Window
{
    public string Column1 => (string)Combo1.SelectedItem;
    public string Column2 => (string)Combo2.SelectedItem;
    public bool Pooled => PooledBox.IsChecked == true;
    public double Confidence { get; private set; } = 0.95;
    public Alternative Alt { get; private set; } = Alternative.TwoSided;

    public TwoColumnWindow(IReadOnlyList<string> columns, string title, bool showPooled, bool twoSidedOnly = false)
    {
        InitializeComponent();
        Title = title;
        foreach (var c in columns) { Combo1.Items.Add(c); Combo2.Items.Add(c); }
        Combo1.SelectedIndex = 0;
        Combo2.SelectedIndex = columns.Count > 1 ? 1 : 0;
        PooledBox.Visibility = showPooled ? Visibility.Visible : Visibility.Collapsed;
        AltCombo.ItemsSource = TestOptions.AltLabels;
        AltCombo.SelectedIndex = 0;
        AltCombo.IsEnabled = !twoSidedOnly;
        if (twoSidedOnly) AltCombo.ToolTip = "This test currently supports the two-sided alternative.";
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (Combo1.SelectedItem is null || Combo2.SelectedItem is null)
        {
            MessageBox.Show("Pick both columns.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (Column1 == Column2)
        {
            MessageBox.Show("Choose two different columns.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (!TestOptions.TryParseConf(ConfBox.Text, out var confidence))
        {
            MessageBox.Show("Confidence must be between 0 and 1 (or between 0 and 100 as a percent).", Title,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        Alt = TestOptions.ParseAlt(AltCombo.SelectedIndex);
        Confidence = confidence;
        DialogResult = true;
    }
}
