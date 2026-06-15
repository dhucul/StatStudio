using System.Windows;
using System.Windows.Controls;

namespace StatStudio.Wpf.Dialogs;

public partial class KMeansWindow : Window
{
    private readonly List<CheckBox> _boxes = new();
    public List<string> SelectedColumns { get; } = new();
    public int K { get; private set; } = 3;

    public KMeansWindow(IEnumerable<string> columns)
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
        if (SelectedColumns.Count < 1) { Warn("Select at least one variable."); return; }
        if (!TestOptions.ParseInt(KBox.Text, out var k) || k < 2) { Warn("k must be ≥ 2."); return; }
        K = k;
        DialogResult = true;
    }

    private void Warn(string m) => MessageBox.Show(m, Title, MessageBoxButton.OK, MessageBoxImage.Information);
}
