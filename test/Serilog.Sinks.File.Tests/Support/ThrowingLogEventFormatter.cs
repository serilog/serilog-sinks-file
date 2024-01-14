using Serilog.Events;
using Serilog.Formatting;

namespace Serilog.Tests.Support;

public class ThrowingLogEventFormatter : ITextFormatter
{
    public void Format(LogEvent logEvent, TextWriter output)
    {
        throw new NotImplementedException();
    }
}
