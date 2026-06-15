using System.Windows;
using System.Windows.Controls;

namespace StatStudio.Wpf.Dialogs;

public partial class RegressionWindow : Window
{
    private readonly List<CheckBox> _boxes = new();

    public string Response => (string)ResponseCombo.SelectedItem;
    public List<string> Predictors { get; } = new();

    public RegressionWindow(IReadOnlyList<string> columns)
    {
        InitializeComponent();
        foreach (var c in columns)
        {
            ResponseCombo.Items.Add(c);
            var cb = new CheckBox { Content = c, Margin = new Thickness(4, 3, 4, 3) };
            _boxes.Add(cb);
            List.Items.Add(cb);
        }
        ResponseCombo.SelectedIndex = 0;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        Predictors.Clear();
        foreach (var cb in _boxes)
            if (cb.IsChecked == true && (string)cb.Content != Response)
                Predictors.Add((string)cb.Content);

        if (Predictors.Count == 0)
        {
            MessageBox.Show("Select at least one predictor (other than the response).",
                Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
    }
}
