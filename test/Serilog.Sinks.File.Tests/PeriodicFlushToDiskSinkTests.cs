using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.File.Tests.Support;
using Xunit;

#pragma warning disable 618 // PeriodicFlushToDiskSink is marked obsolete; we still test it directly.

namespace Serilog.Sinks.File.Tests;

public class PeriodicFlushToDiskSinkTests
{
    [Fact]
    public void FlushToDisk_WhenFlushableThrowsObjectDisposed_SuppressesFailureReport()
    {
        var flushable = new StubFlushable(() => new ObjectDisposedException("FileStream"));
        var listener = new RecordingFailureListener();
        using var sink = new PeriodicFlushToDiskSink(flushable, Timeout.InfiniteTimeSpan);
        ((ISetLoggingFailureListener)sink).SetFailureListener(listener);

        sink.Emit(Some.InformationEvent());
        sink.FlushToDisk(flushable);

        Assert.Empty(listener.Failures);
        Assert.Equal(1, flushable.FlushCount);
    }

    [Fact]
    public void FlushToDisk_WhenFlushableThrowsIOException_ReportsAsTemporaryFailure()
    {
        var ioException = new IOException("disk full");
        var flushable = new StubFlushable(() => ioException);
        var listener = new RecordingFailureListener();
        using var sink = new PeriodicFlushToDiskSink(flushable, Timeout.InfiniteTimeSpan);
        ((ISetLoggingFailureListener)sink).SetFailureListener(listener);

        sink.Emit(Some.InformationEvent());
        sink.FlushToDisk(flushable);

        var failure = Assert.Single(listener.Failures);
        Assert.Equal(LoggingFailureKind.Temporary, failure.Kind);
        Assert.Same(ioException, failure.Exception);
    }

    [Fact]
    public void FlushToDisk_WhenNoEventEmitted_DoesNotInvokeUnderlyingFlush()
    {
        var flushable = new StubFlushable();
        var listener = new RecordingFailureListener();
        using var sink = new PeriodicFlushToDiskSink(flushable, Timeout.InfiniteTimeSpan);
        ((ISetLoggingFailureListener)sink).SetFailureListener(listener);

        sink.FlushToDisk(flushable);

        Assert.Equal(0, flushable.FlushCount);
        Assert.Empty(listener.Failures);
    }

    sealed class StubFlushable : ILogEventSink, IFlushableFileSink
    {
        readonly Func<Exception?>? _onFlush;

        public StubFlushable(Func<Exception?>? onFlush = null)
        {
            _onFlush = onFlush;
        }

        public int FlushCount;

        public void Emit(LogEvent logEvent)
        {
        }

        public void FlushToDisk()
        {
            FlushCount++;
            var exception = _onFlush?.Invoke();
            if (exception != null)
            {
                throw exception;
            }
        }
    }

    sealed class RecordingFailureListener : ILoggingFailureListener
    {
        public List<(LoggingFailureKind Kind, string Message, Exception? Exception)> Failures { get; } = [];

        public void OnLoggingFailed(object sender, LoggingFailureKind kind, string message, IReadOnlyCollection<LogEvent>? events, Exception? exception)
        {
            Failures.Add((kind, message, exception));
        }
    }
}
