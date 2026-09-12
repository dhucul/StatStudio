using System.Data;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using StatStudio.Core.Data;
using StatStudio.Core.Statistics;
using StatStudio.Wpf;
using StatStudio.Wpf.Dialogs;
using StatStudio.Wpf.Graphs;
using Xunit;

namespace StatStudio.Wpf.Tests;

[Collection("WPF")]
public sealed class WorkflowTests
{
    [Fact]
    public Task DialogFitOptionsAreWorkerSafe() => OnSta(async () =>
    {
        var arima = new ArimaWindow(new[] { "Y" });
        var sarima = new SarimaWindow(new[] { "Y" });
        try
        {
            Assert.True(await Task.Run(() => arima.IncludeConstant));
            Assert.False(await Task.Run(() => sarima.IncludeConstant));
        }
        finally { arima.Close(); sarima.Close(); }
    });

    [Fact]
    public Task RepeatedFitsFailureAndCancellationReleaseTheirState() => OnSta(async () =>
    {
        var work = new BackgroundWork();
        var states = new List<bool>();
        var y = new double[] { 1, 3, 2, 5, 4, 6, 8, 7, 9, 10 };
        for (int i = 0; i < 2; i++)
            Assert.Equal(2, (await work.RunAsync(ct => Arima.Fit(y, 0, 1, 0, 2, false, ct), states.Add)).Forecasts.Length);
        Assert.False(work.IsRunning);
        work.Cancel(); // completed sources must not remain reachable
        await Assert.ThrowsAsync<ArgumentException>(() => work.RunAsync<int>(_ => throw new ArgumentException(), states.Add));
        Assert.False(work.IsRunning);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var task = work.RunAsync(ct => { started.SetResult(); ct.WaitHandle.WaitOne(); ct.ThrowIfCancellationRequested(); return 1; }, states.Add);
        await started.Task;
        Assert.True(work.IsRunning);
        await Assert.ThrowsAsync<InvalidOperationException>(() => work.RunAsync(_ => 0, states.Add));
        work.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.False(work.IsRunning);
        Assert.Equal(42, await work.RunAsync(_ => 42, states.Add));
        work.Cancel();
        Assert.Equal(new[] { true, false, true, false, true, false, true, false, true, false }, states);
    });

    [Fact]
    public Task FailedBusySetupAlsoReleasesTheOperation() => OnSta(async () =>
    {
        var work = new BackgroundWork();
        bool restored = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => work.RunAsync(_ => 1, busy =>
        {
            if (busy) throw new InvalidOperationException("setup failed");
            restored = true;
        }));
        Assert.True(restored); Assert.False(work.IsRunning);
        Assert.Equal(2, await work.RunAsync(_ => 2, _ => { }));
    });

    [Fact]
    public Task CommittedSaveIsNotReportedAsCancelledByALateClick() => OnSta(async () =>
    {
        var work = new BackgroundWork();
        var committed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var finish = new ManualResetEventSlim();
        var task = work.RunAsync(_ => { committed.SetResult(); finish.Wait(); return 42; }, _ => { }, discardCancelledResult: false);
        await committed.Task;
        work.Cancel();
        finish.Set();
        Assert.Equal(42, await task);
        Assert.False(work.IsRunning);
    });

    [Fact]
    public Task MainWindowRestoresControlsAndDoesNotPublishAfterClosing() => OnSta(async () =>
    {
        var window = new MainWindow();
        var run = typeof(MainWindow).GetMethod("RunWorkAsync", BindingFlags.NonPublic | BindingFlags.Instance)!.MakeGenericMethod(typeof(int));
        Task<int> Run(Func<CancellationToken, int> action) => (Task<int>)run.Invoke(window, new object[] { action, true })!;
        Assert.Equal(1, await Run(_ => 1));
        await Assert.ThrowsAsync<ArgumentException>(() => Run(_ => throw new ArgumentException()));
        Assert.True(window.MainMenu.IsEnabled); Assert.True(window.Sheet.IsEnabled);
        Assert.Equal(Visibility.Collapsed, window.CancelWorkButton.Visibility);
        Assert.False(window.Sheet.CanUserSortColumns);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = Run(ct => { started.SetResult(); ct.WaitHandle.WaitOne(); ct.ThrowIfCancellationRequested(); return 99; });
        await started.Task;
        Assert.False(window.Sheet.IsEnabled);
        window.Close();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    });

    [Fact]
    public Task GridRoundTripPreservesDeclaredTypes() => OnSta(() =>
    {
        var ws = new Worksheet();
        ws.AddColumn("ID", ColumnType.Text).Add("00123");
        ws.AddColumn("When", ColumnType.DateTime).Add("2026-01-01");
        ws.AddColumn("X").AddNumber(1e-12);
        var table = WorksheetGrid.ToDataTable(ws);
        var back = WorksheetGrid.ToWorksheet(table);
        Assert.Equal(ColumnType.Text, back[0].Type); Assert.Equal("00123", back[0][0]);
        Assert.Equal(ColumnType.DateTime, back[1].Type);
        Assert.Equal(1e-12, back[2].NumericValues()[0]);
        Assert.Equal("X", Assert.Single(back.NumericColumns()).Name);
        return Task.CompletedTask;
    });

    [Fact]
    public Task WorksheetEditsRefreshDerivedCounts() => OnSta(async () =>
    {
        var window = new MainWindow();
        try
        {
            var table = ((DataView)window.Sheet.ItemsSource).Table!;
            table.Rows[0][0] = "7";
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.Contains("1 rows", window.DimsText.Text);
            Assert.Contains("n=1", window.NavList.Items[1].ToString());
        }
        finally
        {
            typeof(MainWindow).GetField("_dirty", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, false);
            window.Close();
        }
    });

    [Fact]
    public Task BoxplotsRetainOutlierMarkers() => OnSta(() =>
    {
        var plot = new ScottPlot.Plot();
        Plots.Boxplot(plot, new[] { ("X", Enumerable.Range(1, 20).Select(i => (double)i).Append(1000).ToArray()) });
        Assert.Single(plot.GetPlottables().OfType<ScottPlot.Plottables.Scatter>());
        plot.Dispose();
        return Task.CompletedTask;
    });

    private static Task OnSta(Func<Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            dispatcher.BeginInvoke(new Action(async () =>
            {
                try { await action(); completion.TrySetResult(); }
                catch (Exception ex) { completion.TrySetException(ex); }
                finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
            }));
            Dispatcher.Run();
        })
        { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }
}

[CollectionDefinition("WPF", DisableParallelization = true)]
public sealed class WpfCollection { }
