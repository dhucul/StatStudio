using System.Windows;
using System.Windows.Controls;

namespace StatStudio.Wpf.Dialogs;

public partial class RsmCreateWindow : Window
{
    public bool IsBoxBehnken => TypeCombo.SelectedIndex == 1;
    public int Factors { get; private set; }
    public int CenterPoints { get; private set; } = 4;
    public bool FaceCentered => FaceBox.IsChecked == true;
    public bool Randomize => RandomizeBox.IsChecked == true;

    public RsmCreateWindow()
    {
        InitializeComponent();
        TypeCombo.Items.Add("Central Composite (CCD)");
        TypeCombo.Items.Add("Box-Behnken");
        TypeCombo.SelectedIndex = 0;
    }

    private void OnTypeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FactorsCombo is null) return;
        int prev = FactorsCombo.SelectedItem is int v ? v : 3;
        FactorsCombo.Items.Clear();
        int min = IsBoxBehnken ? 3 : 2;
        for (int k = min; k <= 5; k++) FactorsCombo.Items.Add(k);
        FactorsCombo.SelectedItem = FactorsCombo.Items.Contains(prev) ? prev : FactorsCombo.Items[0];
        FaceBox.Visibility = IsBoxBehnken ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (FactorsCombo.SelectedItem is null) { Warn("Pick the number of factors."); return; }
        if (!TestOptions.ParseInt(CenterBox.Text, out var cp) || cp < 0) { Warn("Center points must be ≥ 0."); return; }
        Factors = (int)FactorsCombo.SelectedItem;
        CenterPoints = cp;
        DialogResult = true;
    }

    private void Warn(string m) => MessageBox.Show(m, Title, MessageBoxButton.OK, MessageBoxImage.Information);
}
