using System.Windows;

namespace StatStudio.Wpf.Dialogs;

public partial class AttributeChartWindow : Window
{
    private readonly bool _needSizes;
    private readonly bool _needConst;

    public string CountsColumn => (string)CountsCombo.SelectedItem;
    public string SizesColumn => (string)SizesCombo.SelectedItem;
    public int ConstantSize { get; private set; }

    public AttributeChartWindow(IReadOnlyList<string> columns, string title,
        bool needSizesColumn, bool needConstantSize)
    {
        InitializeComponent();
        Title = title;
        _needSizes = needSizesColumn;
        _needConst = needConstantSize;

        foreach (var c in columns) { CountsCombo.Items.Add(c); SizesCombo.Items.Add(c); }
        CountsCombo.SelectedIndex = 0;
        SizesCombo.SelectedIndex = columns.Count > 1 ? 1 : 0;

        if (!needSizesColumn) { SizesLabel.Visibility = Visibility.Collapsed; SizesCombo.Visibility = Visibility.Collapsed; }
        if (!needConstantSize) { ConstLabel.Visibility = Visibility.Collapsed; ConstBox.Visibility = Visibility.Collapsed; }
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (CountsCombo.SelectedItem is null) { Warn("Pick a counts column."); return; }
        if (_needSizes && SizesCombo.SelectedItem is null) { Warn("Pick a sample-sizes column."); return; }
        if (_needConst)
        {
            if (!TestOptions.ParseInt(ConstBox.Text, out var n) || n <= 0) { Warn("Enter a positive sample size."); return; }
            ConstantSize = n;
        }
        DialogResult = true;
    }

    private void Warn(string msg) =>
        MessageBox.Show(msg, Title, MessageBoxButton.OK, MessageBoxImage.Information);
}
