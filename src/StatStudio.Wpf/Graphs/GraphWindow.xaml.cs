using System.Windows;

namespace StatStudio.Wpf.Graphs;

public partial class GraphWindow : Window
{
    public GraphWindow(string title)
    {
        InitializeComponent();
        Title = title;
    }

    public ScottPlot.Plot Plot => PlotView.Plot;

    public void Render() => PlotView.Refresh();
}
