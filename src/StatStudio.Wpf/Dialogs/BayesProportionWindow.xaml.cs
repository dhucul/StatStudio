using System.Windows;

namespace StatStudio.Wpf.Dialogs;

public partial class BayesProportionWindow : Window
{
    public int X { get; private set; }
    public int N { get; private set; }
    public double PriorA { get; private set; } = 1;
    public double PriorB { get; private set; } = 1;
    public double Threshold { get; private set; } = 0.5;
    public double Confidence { get; private set; } = 0.95;

    public BayesProportionWindow() => InitializeComponent();

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (!TestOptions.ParseInt(XBox.Text, out var x) || !TestOptions.ParseInt(NBox.Text, out var n) || n <= 0 || x < 0 || x > n)
        { Warn("Need 0 ≤ events ≤ trials, trials > 0."); return; }
        if (!TestOptions.ParseDouble(ABox.Text, out var a) || a <= 0 || !TestOptions.ParseDouble(BBox.Text, out var b) || b <= 0)
        { Warn("Prior a and b must be positive."); return; }
        if (!TestOptions.ParseDouble(ThreshBox.Text, out var th) || th <= 0 || th >= 1) { Warn("Threshold must be in (0,1)."); return; }
        X = x; N = n; PriorA = a; PriorB = b; Threshold = th;
        if (!TestOptions.TryParseConf(ConfBox.Text, out var confidence))
        { Warn("Confidence must be between 0 and 1 (or between 0 and 100 as a percent)."); return; }
        Confidence = confidence;
        DialogResult = true;
    }

    private void Warn(string m) => MessageBox.Show(m, Title, MessageBoxButton.OK, MessageBoxImage.Information);
}
