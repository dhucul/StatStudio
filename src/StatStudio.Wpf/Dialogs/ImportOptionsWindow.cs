using System.Windows;
using System.Windows.Controls;

namespace StatStudio.Wpf.Dialogs;

internal sealed class ImportOptionsWindow : Window
{
    private readonly CheckBox _header = new() { Content = "First row contains column names", IsChecked = true, Margin = new Thickness(0, 8, 0, 8) };
    private readonly CheckBox _guards = new() { Content = "This CSV was exported by StatStudio", Margin = new Thickness(0, 0, 0, 8) };
    public bool HasHeader => _header.IsChecked == true;
    public bool DecodeFormulaGuards => _guards.IsChecked == true;

    public ImportOptionsWindow(bool excel)
    {
        Title = "Import data";
        Width = 410;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new Thickness(18) };
        panel.Children.Add(new TextBlock { Text = "Choose how to read the file. Clear the first option if row 1 is an observation.", TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(_header);
        if (!excel)
        {
            _guards.ToolTip = "Restore the apostrophes added by StatStudio's CSV export. Leave this off for other files to preserve their literal text.";
            panel.Children.Add(_guards);
        }
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var ok = new Button { Content = "Import", IsDefault = true, MinWidth = 80, Margin = new Thickness(4) };
        ok.Click += (_, _) => DialogResult = true;
        buttons.Children.Add(ok);
        buttons.Children.Add(new Button { Content = "Cancel", IsCancel = true, MinWidth = 80, Margin = new Thickness(4) });
        panel.Children.Add(buttons);
        Content = panel;
    }
}
