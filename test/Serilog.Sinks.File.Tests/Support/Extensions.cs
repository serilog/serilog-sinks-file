using Serilog.Events;

namespace Serilog.Sinks.File.Tests.Support
{
    public static class Extensions
    {
        public static object LiteralValue(this LogEventPropertyValue @this)
        {
            return ((ScalarValue)@this).Value;
        }
    }
}
