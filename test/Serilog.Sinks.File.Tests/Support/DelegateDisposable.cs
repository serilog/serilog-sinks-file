namespace Serilog.Sinks.File.Tests.Support;

public class DelegateDisposable : IDisposable
{
    readonly Action _disposeAction;
    bool _disposed;

    public DelegateDisposable(Action disposeAction)
    {
        _disposeAction = disposeAction;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposeAction();
        _disposed = true;
    }
}
