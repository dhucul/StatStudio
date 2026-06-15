using System.Windows;

namespace StatStudio.Wpf.Dialogs;

public partial class FisherWindow : Window
{
    public int CellA { get; private set; }
    public int CellB { get; private set; }
    public int CellC { get; private set; }
    public int CellD { get; private set; }

    public FisherWindow() => InitializeComponent();

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (!TestOptions.ParseInt(A.Text, out var a) || !TestOptions.ParseInt(B.Text, out var b) ||
            !TestOptions.ParseInt(C.Text, out var c) || !TestOptions.ParseInt(D.Text, out var d) ||
            a < 0 || b < 0 || c < 0 || d < 0)
        {
            MessageBox.Show("Enter four non-negative integers.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        CellA = a; CellB = b; CellC = c; CellD = d;
        DialogResult = true;
    }
}
