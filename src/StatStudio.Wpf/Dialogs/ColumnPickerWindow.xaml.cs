using System.Windows;
using System.Windows.Controls;

namespace StatStudio.Wpf.Dialogs;

public partial class ColumnPickerWindow : Window
{
    private readonly List<CheckBox> _boxes = new();
    private readonly bool _singleSelection;

    public List<string> SelectedColumns { get; } = new();

    public ColumnPickerWindow(string title, string prompt, IEnumerable<string> columns,
        IEnumerable<string>? preselect = null, bool singleSelection = false)
    {
        InitializeComponent();
        Title = title;
        Prompt.Text = prompt;
        _singleSelection = singleSelection;

        var pre = new HashSet<string>(preselect ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        foreach (var c in columns)
        {
            var cb = new CheckBox { Content = c, Margin = new Thickness(4, 3, 4, 3), IsChecked = pre.Contains(c) };
            _boxes.Add(cb);
            List.Items.Add(cb);
        }
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        SelectedColumns.Clear();
        foreach (var cb in _boxes)
            if (cb.IsChecked == true) SelectedColumns.Add((string)cb.Content);

        if (SelectedColumns.Count == 0 || (_singleSelection && SelectedColumns.Count != 1))
        {
            MessageBox.Show(_singleSelection ? "Select exactly one column." : "Select at least one column.", Title,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
    }
}
