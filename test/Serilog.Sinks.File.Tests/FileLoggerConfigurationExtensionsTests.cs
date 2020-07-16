using System;
using System.Threading;
using Serilog.Sinks.File.Tests.Support;
using Serilog.Tests.Support;
using Xunit;
using System.IO;
using System.Text;

namespace Serilog.Sinks.File.Tests
{
    public class FileLoggerConfigurationExtensionsTests
    {
        static readonly string InvalidPath = new string(Path.GetInvalidPathChars());

        [Fact]
        public void WhenWritingCreationExceptionsAreSuppressed()
        {
            new LoggerConfiguration()
                .WriteTo.File(InvalidPath)
                .CreateLogger();
        }

        [Fact]
        public void WhenAuditingCreationExceptionsPropagate()
        {
            Assert.Throws<ArgumentException>(() =>
                new LoggerConfiguration()
                    .AuditTo.File(InvalidPath)
                    .CreateLogger());
        }

        [Fact]
        public void WhenWritingLoggingExceptionsAreSuppressed()
        {
            using (var tmp = TempFolder.ForCaller())
            using (var log = new LoggerConfiguration()
                .WriteTo.File(new ThrowingLogEventFormatter(), tmp.AllocateFilename())
                .CreateLogger())
            {
                log.Information("Hello");
            }
        }

        [Fact]
        public void WhenAuditingLoggingExceptionsPropagate()
        {
            using (var tmp = TempFolder.ForCaller())
            using (var log = new LoggerConfiguration()
                .AuditTo.File(new ThrowingLogEventFormatter(), tmp.AllocateFilename())
                .CreateLogger())
            {
                var ex = Assert.Throws<AggregateException>(() => log.Information("Hello"));
                Assert.IsType<NotImplementedException>(ex.GetBaseException());
            }
        }

        [Fact]
        public void WhenFlushingToDiskReportedFileSinkCanBeCreatedAndDisposed()
        {
            using (var tmp = TempFolder.ForCaller())
            using (var log = new LoggerConfiguration()
                .WriteTo.File(tmp.AllocateFilename(), flushToDiskInterval: TimeSpan.FromMilliseconds(500))
                .CreateLogger())
            {
                log.Information("Hello");
                Thread.Sleep(TimeSpan.FromSeconds(1));
            }
        }

        [Fact]
        public void WhenFlushingToDiskReportedSharedFileSinkCanBeCreatedAndDisposed()
        {
            using (var tmp = TempFolder.ForCaller())
            using (var log = new LoggerConfiguration()
                .WriteTo.File(tmp.AllocateFilename(), shared: true, flushToDiskInterval: TimeSpan.FromMilliseconds(500))
                .CreateLogger())
            {
                log.Information("Hello");
                Thread.Sleep(TimeSpan.FromSeconds(1));
            }
        }

        [Fact]
        public void BufferingIsNotAvailableWhenSharingEnabled()
        {
            Assert.Throws<ArgumentException>(() =>
                new LoggerConfiguration()
                    .WriteTo.File("logs", buffered: true, shared: true));
        }

        [Fact]
        public void HooksAreNotAvailableWhenSharingEnabled()
        {
            Assert.Throws<ArgumentException>(() =>
                new LoggerConfiguration()
                    .WriteTo.File("logs", shared: true, hooks: new GZipHooks()));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void SpecifiedEncodingIsPropagated(bool shared)
        {
            using (var tmp = TempFolder.ForCaller())
            {
                var filename = tmp.AllocateFilename("txt");

                using (var log = new LoggerConfiguration()
                    .WriteTo.File(filename, outputTemplate: "{Message}", encoding: Encoding.Unicode, shared: shared)
                    .CreateLogger())
                {
                    log.Information("ten chars.");
                }

                // Don't forget the two-byte BOM :-)
                Assert.Equal(22, System.IO.File.ReadAllBytes(filename).Length);
            }
        }
    }
}
