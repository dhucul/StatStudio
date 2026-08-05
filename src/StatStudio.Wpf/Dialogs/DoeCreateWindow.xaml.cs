using System.Windows;

namespace StatStudio.Wpf.Dialogs;

public partial class DoeCreateWindow : Window
{
    /// <summary>Largest design that stays workable in the worksheet grid.</summary>
    private const int MaxRuns = 10_000;

    public int Factors { get; private set; } = 3;
    public int Replicates { get; private set; } = 1;
    public int CenterPoints { get; private set; }
    public bool Randomize => RandomizeBox.IsChecked == true;

    public DoeCreateWindow() => InitializeComponent();

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (!TestOptions.ParseInt(FactorsBox.Text, out var k) || k < 2 || k > 7) { Warn("Factors must be 2–7."); return; }
        if (!TestOptions.ParseInt(RepBox.Text, out var rep) || rep < 1 || rep > 100) { Warn("Replicates must be 1–100."); return; }
        if (!TestOptions.ParseInt(CenterBox.Text, out var cp) || cp < 0 || cp > 100) { Warn("Center points must be 0–100."); return; }

        // Runs multiply out: 7 factors x 100 replicates is already 12 800 rows, and the total was
        // previously unbounded because replicates and center points had no ceiling.
        long runs = ((1L << k) + cp) * rep;
        if (runs > MaxRuns) { Warn($"That design needs {runs} runs, over the {MaxRuns} limit. Reduce factors or replicates."); return; }

        Factors = k; Replicates = rep; CenterPoints = cp;
        DialogResult = true;
    }

    private void Warn(string m) => MessageBox.Show(m, Title, MessageBoxButton.OK, MessageBoxImage.Information);
}
