using System.Windows;

namespace StatStudio.Wpf.Dialogs;

public partial class CalculatorWindow : Window
{
    public string TargetColumn => TargetBox.Text.Trim();
    public string Expression => ExprBox.Text.Trim();

    public CalculatorWindow()
    {
        InitializeComponent();
        ExprBox.Focus();
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TargetColumn)) { Warn("Enter a destination column."); return; }
        if (string.IsNullOrWhiteSpace(Expression)) { Warn("Enter an expression."); return; }
        DialogResult = true;
    }

    private void Warn(string msg) => MessageBox.Show(msg, Title, MessageBoxButton.OK, MessageBoxImage.Information);
}
