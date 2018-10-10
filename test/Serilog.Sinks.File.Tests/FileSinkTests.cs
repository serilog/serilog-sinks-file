using System.IO;
using Xunit;
using Serilog.Formatting.Json;
using Serilog.Sinks.File.Tests.Support;
using Serilog.Tests.Support;
using System.Text;

#pragma warning disable 618

namespace Serilog.Sinks.File.Tests
{
    public class FileSinkTests
    {
        [Fact]
        public void FileIsWrittenIfNonexistent()
        {
            using (var tmp = TempFolder.ForCaller())
            {
                var nonexistent = tmp.AllocateFilename("txt");
                var evt = Some.LogEvent("Hello, world!");

                using (var sink = new FileSink(nonexistent, new JsonFormatter(), null))
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

                using (var sink = new FileSink(path, new JsonFormatter(), null))
                {
                    sink.Emit(evt);
                }

                using (var sink = new FileSink(path, new JsonFormatter(), null))
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

                using (var sink = new FileSink(path, new JsonFormatter(), maxBytes))
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

                using (var sink = new FileSink(path, new JsonFormatter(), null))
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


        [Fact]
        public void WhenLimitIsSpecifiedAndEncodingHasPreambleDataIsCorrectlyAppendedToFileSink()
        {
            long? maxBytes = 5000;
            var encoding = Encoding.UTF8;

            Assert.True(encoding.GetPreamble().Length > 0);
            WriteTwoEventsAndCheckOutputFileLength(maxBytes, encoding);
        }

        [Fact]
        public void WhenLimitIsNotSpecifiedAndEncodingHasPreambleDataIsCorrectlyAppendedToFileSink()
        {
            var encoding = Encoding.UTF8;

            Assert.True(encoding.GetPreamble().Length > 0);
            WriteTwoEventsAndCheckOutputFileLength(null, encoding);
        }

        [Fact]
        public void WhenLimitIsSpecifiedAndEncodingHasNoPreambleDataIsCorrectlyAppendedToFileSink()
        {
            long? maxBytes = 5000;
            var encoding = new UTF8Encoding(false);

            Assert.Equal(0, encoding.GetPreamble().Length);
            WriteTwoEventsAndCheckOutputFileLength(maxBytes, encoding);
        }

        [Fact]
        public void WhenLimitIsNotSpecifiedAndEncodingHasNoPreambleDataIsCorrectlyAppendedToFileSink()
        {
            var encoding = new UTF8Encoding(false);

            Assert.Equal(0, encoding.GetPreamble().Length);
            WriteTwoEventsAndCheckOutputFileLength(null, encoding);
        }

        static void WriteTwoEventsAndCheckOutputFileLength(long? maxBytes, Encoding encoding)
        {
            using (var tmp = TempFolder.ForCaller())
            {
                var path = tmp.AllocateFilename("txt");
                var evt = Some.LogEvent("Irrelevant as it will be replaced by the formatter");
                var actualEventOutput = "x";
                var formatter = new FixedOutputFormatter(actualEventOutput);
                var eventOuputLength = encoding.GetByteCount(actualEventOutput);

                using (var sink = new FileSink(path, formatter, maxBytes, encoding: encoding))
                {
                    sink.Emit(evt);
                }
                var size = new FileInfo(path).Length;
                Assert.Equal(encoding.GetPreamble().Length + eventOuputLength, size);

                //write a second event to the same file
                using (var sink = new FileSink(path, formatter, maxBytes, encoding: encoding))
                {
                    sink.Emit(evt);
                }

                size = new FileInfo(path).Length;
                Assert.Equal(encoding.GetPreamble().Length + eventOuputLength * 2, size);
            }
        }
    }
}

