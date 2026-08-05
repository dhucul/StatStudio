using System.Windows;

namespace StatStudio.Wpf.Dialogs;

public partial class DistFitWindow : Window
{
    public const string None = "(none)";

    public string TimesColumn => (string)TimesCombo.SelectedItem;
    public string Distribution => (string)DistCombo.SelectedItem;

    /// <summary>Column of 0/1 right-censoring flags, or null when every row is a failure.</summary>
    public string? CensorColumn =>
        (string)CensorCombo.SelectedItem == None ? null : (string)CensorCombo.SelectedItem;

    public DistFitWindow(IReadOnlyList<string> numeric)
    {
        InitializeComponent();
        foreach (var c in numeric) TimesCombo.Items.Add(c);
        TimesCombo.SelectedIndex = 0;
        CensorCombo.Items.Add(None);
        foreach (var c in numeric) CensorCombo.Items.Add(c);
        CensorCombo.SelectedIndex = 0;
        foreach (var d in new[] { "Weibull", "Exponential", "Lognormal", "Normal" }) DistCombo.Items.Add(d);
        DistCombo.SelectedIndex = 0;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (TimesCombo.SelectedItem is null) { Warn("Pick a failure-times column."); return; }
        if (CensorColumn == TimesColumn) { Warn("Choose a different censoring-indicator column."); return; }
        DialogResult = true;
    }

    private void Warn(string msg) => MessageBox.Show(msg, Title, MessageBoxButton.OK, MessageBoxImage.Information);
}
