using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;
using Serilog.Events;
using Serilog.Sinks.File.Tests.Support;
using Serilog.Configuration;

namespace Serilog.Sinks.File.Tests
{
    public class RollingFileSinkTests
    {
        [Fact]
        public void LogEventsAreEmittedToTheFileNamedAccordingToTheEventTimestamp()
        {
            TestRollingEventSequence(Some.InformationEvent());
        }

        [Fact]
        public void EventsAreWrittenWhenSharingIsEnabled()
        {
            TestRollingEventSequence(
                (pf, wt) => wt.File(pf, shared: true, rollingInterval: RollingInterval.Day),
                new[] { Some.InformationEvent() });
        }

        [Fact]
        public void EventsAreWrittenWhenBufferingIsEnabled()
        {
            TestRollingEventSequence(
                (pf, wt) => wt.File(pf, buffered: true, rollingInterval: RollingInterval.Day),
                new[] { Some.InformationEvent() });
        }

        [Fact]
        public void EventsAreWrittenWhenDiskFlushingIsEnabled()
        {
            // Doesn't test flushing, but ensures we haven't broken basic logging
            TestRollingEventSequence(
                (pf, wt) => wt.File(pf, flushToDiskInterval: TimeSpan.FromMilliseconds(50), rollingInterval: RollingInterval.Day),
                new[] { Some.InformationEvent() });
        }

        [Fact]
        public void WhenTheDateChangesTheCorrectFileIsWritten()
        {
            var e1 = Some.InformationEvent();
            var e2 = Some.InformationEvent(e1.Timestamp.AddDays(1));
            TestRollingEventSequence(e1, e2);
        }

        [Fact]
        public void WhenRetentionCountIsSetOldFilesAreDeleted()
        {
            LogEvent e1 = Some.InformationEvent(),
                e2 = Some.InformationEvent(e1.Timestamp.AddDays(1)),
                e3 = Some.InformationEvent(e2.Timestamp.AddDays(5));

            TestRollingEventSequence(
                (pf, wt) => wt.File(pf, retainedFileCountLimit: 2, rollingInterval: RollingInterval.Day),
                new[] {e1, e2, e3},
                files =>
                {
                    Assert.Equal(3, files.Count);
                    Assert.True(!System.IO.File.Exists(files[0]));
                    Assert.True(System.IO.File.Exists(files[1]));
                    Assert.True(System.IO.File.Exists(files[2]));
                });
        }

        [Fact]
        public void WhenSizeLimitIsBreachedNewFilesCreated()
        {
            var fileName = Some.String() + ".txt";
            using (var temp = new TempFolder())
            using (var log = new LoggerConfiguration()
                .WriteTo.File(Path.Combine(temp.Path, fileName), rollOnFileSizeLimit: true, fileSizeLimitBytes: 1)
                .CreateLogger())
            {
                LogEvent e1 = Some.InformationEvent(),
                    e2 = Some.InformationEvent(e1.Timestamp),
                    e3 = Some.InformationEvent(e1.Timestamp);

                log.Write(e1); log.Write(e2); log.Write(e3);

                var files = Directory.GetFiles(temp.Path)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                Assert.Equal(3, files.Length);
                Assert.True(files[0].EndsWith(fileName), files[0]);
                Assert.True(files[1].EndsWith("_001.txt"), files[1]);
                Assert.True(files[2].EndsWith("_002.txt"), files[2]);
            }
        }

        [Fact]
        public void IfTheLogFolderDoesNotExistItWillBeCreated()
        {
            var fileName = Some.String() + "-{Date}.txt";
            var temp = Some.TempFolderPath();
            var folder = Path.Combine(temp, Guid.NewGuid().ToString());
            var pathFormat = Path.Combine(folder, fileName);

            ILogger log = null;
             
            try
            {
                log = new LoggerConfiguration()
                    .WriteTo.File(pathFormat, retainedFileCountLimit: 3, rollingInterval: RollingInterval.Day)
                    .CreateLogger();

                log.Write(Some.InformationEvent());

                Assert.True(Directory.Exists(folder));
            }
            finally
            {
                var disposable = (IDisposable)log;
                if (disposable != null) disposable.Dispose();
                Directory.Delete(temp, true);
            }
        }

        [Fact]
        public void AssemblyVersionIsFixedAt200()
        {
            var assembly = typeof(FileLoggerConfigurationExtensions).GetTypeInfo().Assembly;
            Assert.Equal("2.0.0.0", assembly.GetName().Version.ToString(4));
        }

        [Fact]
        public void GZipCompressionCorrect()
        {
            // Zip compress a file and check it's magic byte at the start


            // use try-finally to delete the folder that is created and filled in try
            // log files into a directory, check each file in directory is gzip zip

            var fileName = Some.String() + "log.txt";
            var temp = Some.TempFolderPath();
            var pathFormat = Path.Combine(temp, fileName);

            ILogger log = null;


            log = new LoggerConfiguration()
                   .WriteTo.File(pathFormat,
                   rollOnFileSizeLimit: true, fileSizeLimitBytes: 4,
                   compressionType: CompressionType.GZip)
                   .CreateLogger();

            while (Directory.GetFiles(temp).Length < 2)
            {
                log.Information("test if compresses on roll.");
            }

            Log.CloseAndFlush();

            var compressedFile =
                Directory.EnumerateFiles(temp)
                .Where(name => name.Contains("-GZip")).First();

            using (FileStream compressedStream = new FileStream(compressedFile, FileMode.Open))
            {

                byte[] compressedBytes = new byte[2];

                compressedStream.Read(compressedBytes, 0, compressedBytes.Length);

                Assert.Equal(compressedBytes[0], 0x1f);

                Assert.Equal(compressedBytes[1], 0x8b);

            }

        }

        [Fact]
        public void ZipCompressionCorrect()
        {

            var fileName = Some.String() + "log.txt";
            var temp = Some.TempFolderPath();
            var pathFormat = Path.Combine(temp, fileName);

           // ILogger log = null;

            using (var log = new LoggerConfiguration()
                       .WriteTo.File(pathFormat,
                       rollOnFileSizeLimit: true, fileSizeLimitBytes: 1,
                       compressionType: CompressionType.Zip)
                       .CreateLogger())
                {

                log.Information("test");
                log.Information("test");

                string compressedFile = Directory.EnumerateFiles(temp).Where(name => name.Contains("-Zip")).First();

                using (FileStream compressedStream = new FileStream(compressedFile, FileMode.Open))
                {

                    byte[] compressedBytes = new byte[2];

                    compressedStream.Read(compressedBytes, 0, compressedBytes.Length);

                    Assert.Equal(compressedBytes[0], 0x50);
                    Assert.Equal(compressedBytes[1], 0x4B);
                }

                foreach (var loggedFile in Directory.GetFiles(temp))
                {
                    System.IO.File.Delete(loggedFile);
                }
                Directory.Delete(temp);
            }
        }

        static void TestRollingEventSequence(params LogEvent[] events)
        {
            TestRollingEventSequence(
                (pf, wt) => wt.File(pf, retainedFileCountLimit: null, rollingInterval: RollingInterval.Day),
                events);
        }

        static void TestRollingEventSequence(
            Action<string, LoggerSinkConfiguration> configureFile,
            IEnumerable<LogEvent> events,
            Action<IList<string>> verifyWritten = null)
        {
            var fileName = Some.String() + "-.txt";
            var folder = Some.TempFolderPath();
            var pathFormat = Path.Combine(folder, fileName);

            var config = new LoggerConfiguration();
            configureFile(pathFormat, config.WriteTo);
            var log = config.CreateLogger();

            var verified = new List<string>();

            try
            {
                foreach (var @event in events)
                {
                    Clock.SetTestDateTimeNow(@event.Timestamp.DateTime);
                    log.Write(@event);

                    var expected = pathFormat.Replace(".txt", @event.Timestamp.ToString("yyyyMMdd") + ".txt");
                    Assert.True(System.IO.File.Exists(expected));

                    verified.Add(expected);
                }
            }
            finally
            {
                log.Dispose();
                verifyWritten?.Invoke(verified);
                Directory.Delete(folder, true);
            }
        }
    }
}
