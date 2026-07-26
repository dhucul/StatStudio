using System.Windows;

namespace StatStudio.Wpf.Dialogs;

public partial class TwoWayAnovaWindow : Window
{
    public string Response => (string)ResponseCombo.SelectedItem;
    public string FactorA => (string)FactorACombo.SelectedItem;
    public string FactorB => (string)FactorBCombo.SelectedItem;

    public TwoWayAnovaWindow(IReadOnlyList<string> numeric, IReadOnlyList<string> allColumns)
    {
        InitializeComponent();
        foreach (var c in numeric) ResponseCombo.Items.Add(c);
        foreach (var c in allColumns) { FactorACombo.Items.Add(c); FactorBCombo.Items.Add(c); }
        ResponseCombo.SelectedIndex = 0;
        int firstFactor = allColumns.ToList().FindIndex(c => c != Response);
        FactorACombo.SelectedIndex = Math.Max(0, firstFactor);
        int secondFactor = allColumns.ToList().FindIndex(c => c != Response && c != FactorA);
        FactorBCombo.SelectedIndex = Math.Max(0, secondFactor);
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (ResponseCombo.SelectedItem is null || FactorACombo.SelectedItem is null || FactorBCombo.SelectedItem is null)
        {
            MessageBox.Show("Pick a response and two factors.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (FactorA == FactorB || Response == FactorA || Response == FactorB)
        {
            MessageBox.Show("Choose three different columns for the response and factors.", Title,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
    }
}
