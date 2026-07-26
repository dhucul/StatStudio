using System.Windows;

namespace StatStudio.Wpf.Dialogs;

public partial class CapabilityWindow : Window
{
    public string DataColumn => (string)DataCombo.SelectedItem;
    public double? Lsl { get; private set; }
    public double? Usl { get; private set; }
    public double? Target { get; private set; }

    public CapabilityWindow(IReadOnlyList<string> columns)
    {
        InitializeComponent();
        foreach (var c in columns) DataCombo.Items.Add(c);
        DataCombo.SelectedIndex = 0;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (DataCombo.SelectedItem is null) { Warn("Pick a data column."); return; }
        Lsl = TestOptions.ParseDouble(LslBox.Text, out var lo) ? lo : null;
        Usl = TestOptions.ParseDouble(UslBox.Text, out var hi) ? hi : null;
        Target = TestOptions.ParseDouble(TargetBox.Text, out var t) ? t : null;
        if (Lsl is null && Usl is null) { Warn("Enter at least one spec limit (LSL or USL)."); return; }
        if (Lsl is not null && Usl is not null && Lsl >= Usl) { Warn("LSL must be less than USL."); return; }
        if (Target is not null &&
            ((Lsl is not null && Target < Lsl) || (Usl is not null && Target > Usl)))
        { Warn("Target must fall within the specification limits."); return; }
        DialogResult = true;
    }

    private void Warn(string msg) =>
        MessageBox.Show(msg, Title, MessageBoxButton.OK, MessageBoxImage.Information);
}
