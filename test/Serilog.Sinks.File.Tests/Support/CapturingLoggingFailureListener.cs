using Serilog.Core;
using Serilog.Events;

namespace Serilog.Sinks.File.Tests.Support;

class CapturingLoggingFailureListener: ILoggingFailureListener
{
    public List<LogEvent> FailedEvents { get; } = [];

    public void OnLoggingFailed(object sender, LoggingFailureKind kind, string message, IReadOnlyCollection<LogEvent>? events, Exception? exception)
    {
        if (kind != LoggingFailureKind.Temporary && events != null)
        {
            FailedEvents.AddRange(events);
        }
    }
}
