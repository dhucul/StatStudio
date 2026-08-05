using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace StatStudio.Wpf;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Without this, any exception escaping a UI event handler terminates the process and
        // takes the unsaved worksheet with it. An analysis that fails should report and continue.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) => TryLog(args.ExceptionObject);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            TryLog(args.Exception);
            args.SetObserved();
        };
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        TryLog(e.Exception);
        MessageBox.Show(
            $"{e.Exception.Message}\n\nThe worksheet is unchanged — you can keep working or save it.",
            "StatStudio — unexpected error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void TryLog(object? error)
    {
        try
        {
            string path = Path.Combine(Path.GetTempPath(), "statstudio-errors.log");
            File.AppendAllText(path, $"{DateTime.Now:u}  {error}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Diagnostics must never become the failure themselves.
        }
    }
}
