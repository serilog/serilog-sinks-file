using System.IO;
using System.Text;

namespace Serilog.Sinks.File.Tests.Support
{
    /// <inheritdoc />
    /// <summary>
    /// Demonstrates the use of <seealso cref="T:Serilog.FileLifecycleHooks" />, by capturing the log file path
    /// </summary>
    class CaptureFilePathHook : FileLifecycleHooks
    {
        public string? Path { get; private set; }

        public override Stream OnFileOpened(string path, Stream _, Encoding __)
        {
            Path = path;
            return base.OnFileOpened(path, _, __);
        }
    }
}
