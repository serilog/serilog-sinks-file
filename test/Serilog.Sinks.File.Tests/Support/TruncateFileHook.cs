using System.Text;

namespace Serilog.Sinks.File.Tests.Support;

/// <inheritdoc />
/// <summary>
/// Demonstrates the use of <seealso cref="T:Serilog.FileLifecycleHooks" />, by emptying the file before it's written to
/// </summary>
public class TruncateFileHook : FileLifecycleHooks
{
    public override Stream OnFileOpened(Stream underlyingStream, Encoding encoding)
    {
        underlyingStream.SetLength(0);
        return base.OnFileOpened(underlyingStream, encoding);
    }
}
