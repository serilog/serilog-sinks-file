using System.IO;
using System.Text;

namespace Serilog.Sinks.File.Tests.Support
{
    class FileHeaderWriter : FileLifecycleHooks
    {
        public string Header { get; }

        public FileHeaderWriter(string header)
        {
            Header = header;
        }

        public override Stream OnFileOpened(Stream underlyingStream, Encoding encoding)
        {
            if (underlyingStream.Length == 0)
            {
                var writer = new StreamWriter(underlyingStream, encoding);
                writer.WriteLine(Header);
                writer.Flush();
                underlyingStream.Flush();
            }

            return base.OnFileOpened(underlyingStream, encoding);
        }
    }
}
