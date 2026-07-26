using System.Windows;

namespace StatStudio.Wpf.Dialogs;

public partial class KaplanMeierWindow : Window
{
    public const string None = "(none)";

    public string TimesColumn => (string)TimesCombo.SelectedItem;
    public string? CensorColumn => (string)CensorCombo.SelectedItem == None ? null : (string)CensorCombo.SelectedItem;

    public KaplanMeierWindow(IReadOnlyList<string> numeric)
    {
        InitializeComponent();
        foreach (var c in numeric) TimesCombo.Items.Add(c);
        TimesCombo.SelectedIndex = 0;
        CensorCombo.Items.Add(None);
        foreach (var c in numeric) CensorCombo.Items.Add(c);
        CensorCombo.SelectedIndex = 0;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (TimesCombo.SelectedItem is null) { MessageBox.Show("Pick a time column.", Title); return; }
        if (CensorColumn == TimesColumn)
        {
            MessageBox.Show("Choose a different censoring-indicator column.", Title,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
    }
}
