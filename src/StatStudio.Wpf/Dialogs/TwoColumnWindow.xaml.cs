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

    public TwoColumnWindow(IReadOnlyList<string> columns, string title, bool showPooled)
    {
        InitializeComponent();
        Title = title;
        foreach (var c in columns) { Combo1.Items.Add(c); Combo2.Items.Add(c); }
        Combo1.SelectedIndex = 0;
        Combo2.SelectedIndex = columns.Count > 1 ? 1 : 0;
        PooledBox.Visibility = showPooled ? Visibility.Visible : Visibility.Collapsed;
        AltCombo.ItemsSource = TestOptions.AltLabels;
        AltCombo.SelectedIndex = 0;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (Combo1.SelectedItem is null || Combo2.SelectedItem is null)
        {
            MessageBox.Show("Pick both columns.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        Alt = TestOptions.ParseAlt(AltCombo.SelectedIndex);
        Confidence = TestOptions.ParseConf(ConfBox.Text);
        DialogResult = true;
    }
}
