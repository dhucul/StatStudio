using System.Windows;

namespace StatStudio.Wpf.Dialogs;

public partial class PolynomialWindow : Window
{
    public string YColumn => (string)YCombo.SelectedItem;
    public string XColumn => (string)XCombo.SelectedItem;
    public int Degree { get; private set; } = 2;

    public PolynomialWindow(IReadOnlyList<string> numeric)
    {
        InitializeComponent();
        foreach (var c in numeric) { YCombo.Items.Add(c); XCombo.Items.Add(c); }
        YCombo.SelectedIndex = 0;
        XCombo.SelectedIndex = numeric.Count > 1 ? 1 : 0;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (YCombo.SelectedItem is null || XCombo.SelectedItem is null) { Warn("Pick both variables."); return; }
        if (!TestOptions.ParseInt(DegreeBox.Text, out var d) || d < 1 || d > 6) { Warn("Degree must be 1–6."); return; }
        Degree = d;
        DialogResult = true;
    }

    private void Warn(string m) => MessageBox.Show(m, Title, MessageBoxButton.OK, MessageBoxImage.Information);
}
