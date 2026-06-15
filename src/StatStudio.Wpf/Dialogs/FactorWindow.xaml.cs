using System.Windows;
using System.Windows.Controls;

namespace StatStudio.Wpf.Dialogs;

public partial class FactorWindow : Window
{
    private readonly List<CheckBox> _boxes = new();
    public List<string> SelectedColumns { get; } = new();
    public int NumFactors { get; private set; } = 2;
    public bool Varimax => VarimaxBox.IsChecked == true;

    public FactorWindow(IEnumerable<string> columns)
    {
        InitializeComponent();
        foreach (var c in columns)
        {
            var cb = new CheckBox { Content = c, Margin = new Thickness(4, 3, 4, 3) };
            _boxes.Add(cb);
            List.Items.Add(cb);
        }
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        SelectedColumns.Clear();
        foreach (var cb in _boxes) if (cb.IsChecked == true) SelectedColumns.Add((string)cb.Content);
        if (SelectedColumns.Count < 2) { Warn("Select at least two variables."); return; }
        if (!TestOptions.ParseInt(FactorsBox.Text, out var m) || m < 1 || m > SelectedColumns.Count)
        { Warn($"Factors must be between 1 and {SelectedColumns.Count}."); return; }
        NumFactors = m;
        DialogResult = true;
    }

    private void Warn(string msg) => MessageBox.Show(msg, Title, MessageBoxButton.OK, MessageBoxImage.Information);
}
