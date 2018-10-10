using Serilog.Formatting;
using Serilog.Events;
using System.IO;

namespace Serilog.Tests.Support
{
    public class FixedOutputFormatter : ITextFormatter
    {
        string _substitutionText;

        public FixedOutputFormatter(string substitutionText)
        {
            _substitutionText = substitutionText;
        }

        public void Format(LogEvent logEvent, TextWriter output)
        {
            output.Write(_substitutionText);
        }
    }
}
