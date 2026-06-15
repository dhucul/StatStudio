using System.Windows;
using System.Windows.Controls;

namespace StatStudio.Wpf.Dialogs;

public partial class MixtureCreateWindow : Window
{
    public bool IsLattice => TypeCombo.SelectedIndex == 1;
    public int Components { get; private set; } = 3;
    public int Degree { get; private set; } = 2;
    public bool Randomize => RandomizeBox.IsChecked == true;

    public MixtureCreateWindow()
    {
        InitializeComponent();
        TypeCombo.Items.Add("Simplex Centroid");
        TypeCombo.Items.Add("Simplex Lattice");
        TypeCombo.SelectedIndex = 0;
        for (int q = 2; q <= 8; q++) ComponentsCombo.Items.Add(q);
        ComponentsCombo.SelectedItem = 3;
    }

    private void OnTypeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DegreeRow != null) DegreeRow.Visibility = IsLattice ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (ComponentsCombo.SelectedItem is null) { Warn("Pick the number of components."); return; }
        Components = (int)ComponentsCombo.SelectedItem;
        if (IsLattice)
        {
            if (!TestOptions.ParseInt(DegreeBox.Text, out var m) || m < 1 || m > 10) { Warn("Lattice degree must be 1–10."); return; }
            Degree = m;
        }
        DialogResult = true;
    }

    private void Warn(string msg) => MessageBox.Show(msg, Title, MessageBoxButton.OK, MessageBoxImage.Information);
}
