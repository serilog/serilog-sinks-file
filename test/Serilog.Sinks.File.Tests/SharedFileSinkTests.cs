using System.IO;
using Xunit;
using Serilog.Formatting.Json;
using Serilog.Sinks.File.Tests.Support;

#pragma warning disable 618

namespace Serilog.Sinks.File.Tests
{
    public class SharedFileSinkTests
    {
        [Fact]
        public void FileIsWrittenIfNonexistent()
        {
            using (var tmp = TempFolder.ForCaller())
            {
                var nonexistent = tmp.AllocateFilename("txt");
                var evt = Some.LogEvent("Hello, world!");

                using (var sink = new SharedFileSink(nonexistent, new JsonFormatter(), null))
                {
                    sink.Emit(evt);
                }

                var lines = System.IO.File.ReadAllLines(nonexistent);
                Assert.Contains("Hello, world!", lines[0]);
            }
        }

        [Fact]
        public void FileIsAppendedToWhenAlreadyCreated()
        {
            using (var tmp = TempFolder.ForCaller())
            {
                var path = tmp.AllocateFilename("txt");
                var evt = Some.LogEvent("Hello, world!");

                using (var sink = new SharedFileSink(path, new JsonFormatter(), null))
                {
                    sink.Emit(evt);
                }

                using (var sink = new SharedFileSink(path, new JsonFormatter(), null))
                {
                    sink.Emit(evt);
                }

                var lines = System.IO.File.ReadAllLines(path);
                Assert.Contains("Hello, world!", lines[0]);
                Assert.Contains("Hello, world!", lines[1]);
            }
        }

        [Fact]
        public void WhenLimitIsSpecifiedFileSizeIsRestricted()
        {
            const int maxBytes = 5000;
            const int eventsToLimit = 10;

            using (var tmp = TempFolder.ForCaller())
            {
                var path = tmp.AllocateFilename("txt");
                var evt = Some.LogEvent(new string('n', maxBytes / eventsToLimit));

                using (var sink = new SharedFileSink(path, new JsonFormatter(), maxBytes))
                {
                    for (var i = 0; i < eventsToLimit * 2; i++)
                    {
                        sink.Emit(evt);
                    }
                }

                var size = new FileInfo(path).Length;
                Assert.True(size > maxBytes);
                Assert.True(size < maxBytes * 2);
            }
        }

        [Fact]
        public void WhenLimitIsNotSpecifiedFileSizeIsNotRestricted()
        {
            const int maxBytes = 5000;
            const int eventsToLimit = 10;

            using (var tmp = TempFolder.ForCaller())
            {
                var path = tmp.AllocateFilename("txt");
                var evt = Some.LogEvent(new string('n', maxBytes / eventsToLimit));

                using (var sink = new SharedFileSink(path, new JsonFormatter(), null))
                {
                    for (var i = 0; i < eventsToLimit * 2; i++)
                    {
                        sink.Emit(evt);
                    }
                }

                var size = new FileInfo(path).Length;
                Assert.True(size > maxBytes * 2);
            }
        }
    }
}
