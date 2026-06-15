using System.Windows;

namespace StatStudio.Wpf.Dialogs;

public partial class XyPickerWindow : Window
{
    public string YColumn => (string)YCombo.SelectedItem;
    public string XColumn => (string)XCombo.SelectedItem;

    public XyPickerWindow(IReadOnlyList<string> columns, string title = "Scatterplot")
    {
        InitializeComponent();
        Title = title;
        foreach (var c in columns)
        {
            YCombo.Items.Add(c);
            XCombo.Items.Add(c);
        }
        YCombo.SelectedIndex = 0;
        XCombo.SelectedIndex = columns.Count > 1 ? 1 : 0;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (YCombo.SelectedItem is null || XCombo.SelectedItem is null)
        {
            MessageBox.Show("Pick both variables.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
    }
}
