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

        // A blank box means "not specified"; unparseable text is a mistake and must be reported,
        // not silently downgraded to a one-sided analysis.
        if (!TryOptional(LslBox.Text, out var lsl)) { Warn("LSL must be a number, or left blank."); return; }
        if (!TryOptional(UslBox.Text, out var usl)) { Warn("USL must be a number, or left blank."); return; }
        if (!TryOptional(TargetBox.Text, out var target)) { Warn("Target must be a number, or left blank."); return; }
        Lsl = lsl; Usl = usl; Target = target;

        if (Lsl is null && Usl is null) { Warn("Enter at least one spec limit (LSL or USL)."); return; }
        if (Lsl is not null && Usl is not null && Lsl >= Usl) { Warn("LSL must be less than USL."); return; }
        if (Target is not null &&
            ((Lsl is not null && Target < Lsl) || (Usl is not null && Target > Usl)))
        { Warn("Target must fall within the specification limits."); return; }
        DialogResult = true;
    }

    /// <summary>Blank → null (absent); a number → that value; anything else → false (invalid).</summary>
    private static bool TryOptional(string? text, out double? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text)) return true;
        if (!TestOptions.ParseDouble(text, out var v)) return false;
        value = v;
        return true;
    }

    private void Warn(string msg) =>
        MessageBox.Show(msg, Title, MessageBoxButton.OK, MessageBoxImage.Information);
}
