using System.Windows;

namespace StatStudio.Wpf.Dialogs;

public partial class DistFitWindow : Window
{
    public string TimesColumn => (string)TimesCombo.SelectedItem;
    public string Distribution => (string)DistCombo.SelectedItem;

    public DistFitWindow(IReadOnlyList<string> numeric)
    {
        InitializeComponent();
        foreach (var c in numeric) TimesCombo.Items.Add(c);
        TimesCombo.SelectedIndex = 0;
        foreach (var d in new[] { "Weibull", "Exponential", "Lognormal", "Normal" }) DistCombo.Items.Add(d);
        DistCombo.SelectedIndex = 0;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (TimesCombo.SelectedItem is null) { MessageBox.Show("Pick a column.", Title); return; }
        DialogResult = true;
    }
}
