using System.Windows;

namespace StatStudio.Wpf.Dialogs;

public partial class DoeCreateWindow : Window
{
    public int Factors { get; private set; } = 3;
    public int Replicates { get; private set; } = 1;
    public int CenterPoints { get; private set; }
    public bool Randomize => RandomizeBox.IsChecked == true;

    public DoeCreateWindow() => InitializeComponent();

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (!TestOptions.ParseInt(FactorsBox.Text, out var k) || k < 2 || k > 7) { Warn("Factors must be 2–7."); return; }
        if (!TestOptions.ParseInt(RepBox.Text, out var rep) || rep < 1) { Warn("Replicates must be ≥ 1."); return; }
        if (!TestOptions.ParseInt(CenterBox.Text, out var cp) || cp < 0) { Warn("Center points must be ≥ 0."); return; }
        Factors = k; Replicates = rep; CenterPoints = cp;
        DialogResult = true;
    }

    private void Warn(string m) => MessageBox.Show(m, Title, MessageBoxButton.OK, MessageBoxImage.Information);
}
