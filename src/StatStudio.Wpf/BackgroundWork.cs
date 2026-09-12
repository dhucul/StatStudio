namespace StatStudio.Wpf;

/// <summary>Owns one operation's cancellation source from creation through disposal.</summary>
internal sealed class BackgroundWork
{
    private CancellationTokenSource? _current;
    public bool IsRunning => _current != null;
    public void Cancel() => _current?.Cancel();

    // Called on the UI thread; await resumes there before clearing shared state.
    public async Task<T> RunAsync<T>(Func<CancellationToken, T> action, Action<bool> busy, bool discardCancelledResult = true)
    {
        if (IsRunning) throw new InvalidOperationException("Another operation is still running.");
        using var source = new CancellationTokenSource();
        _current = source;
        try
        {
            busy(true);
            var result = await Task.Run(() => action(source.Token), source.Token);
            if (discardCancelledResult) source.Token.ThrowIfCancellationRequested();
            return result;
        }
        finally
        {
            _current = null;
            busy(false);
        }
    }
}
