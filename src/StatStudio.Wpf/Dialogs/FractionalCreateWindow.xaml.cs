using System.Windows;
using System.Windows.Controls;
using StatStudio.Core.Statistics;

namespace StatStudio.Wpf.Dialogs;

public partial class FractionalCreateWindow : Window
{
    public int Factors { get; private set; }
    public int Runs { get; private set; }
    public bool Randomize => RandomizeBox.IsChecked == true;

    public FractionalCreateWindow()
    {
        InitializeComponent();
        for (int k = 3; k <= 7; k++) FactorsCombo.Items.Add(k);
        FactorsCombo.SelectedIndex = 1; // k = 4
    }

    private void OnFactorsChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RunsCombo is null || FactorsCombo.SelectedItem is null) return;
        int k = (int)FactorsCombo.SelectedItem;
        RunsCombo.Items.Clear();
        foreach (var r in DoeDesign.AvailableFractions(k)) RunsCombo.Items.Add(r);
        if (RunsCombo.Items.Count > 0) RunsCombo.SelectedIndex = 0;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (FactorsCombo.SelectedItem is null || RunsCombo.SelectedItem is null)
        {
            MessageBox.Show("Pick factors and a run count.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        Factors = (int)FactorsCombo.SelectedItem;
        Runs = (int)RunsCombo.SelectedItem;
        DialogResult = true;
    }
}
