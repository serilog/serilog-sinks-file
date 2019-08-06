using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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
        public void WhenRetentionCountAndArchivingHookIsSetOldFilesAreCopiedAndOriginalDeleted()
        {
            const string archiveDirectory = "OldLogs";
            LogEvent e1 = Some.InformationEvent(),
                    e2 = Some.InformationEvent(e1.Timestamp.AddDays(1)),
                    e3 = Some.InformationEvent(e2.Timestamp.AddDays(5));

            TestRollingEventSequence(
                (pf, wt) => wt.File(pf, retainedFileCountLimit: 2, rollingInterval: RollingInterval.Day, hooks: new ArchiveOldLogsHook(archiveDirectory)),
                new[] {e1, e2, e3},
                files =>
                {
                    Assert.Equal(3, files.Count);
                    Assert.True(!System.IO.File.Exists(files[0]));
                    Assert.True(System.IO.File.Exists(files[1]));
                    Assert.True(System.IO.File.Exists(files[2]));

                    Assert.True(System.IO.File.Exists(ArchiveOldLogsHook.AddTopDirectory(files[0], archiveDirectory)));
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
        public void WhenStreamWrapperSpecifiedIsUsedForRolledFiles()
        {
            var gzipWrapper = new GZipHooks();
            var fileName = Some.String() + ".txt";

            using (var temp = new TempFolder())
            {
                string[] files;
                var logEvents = new[]
                {
                    Some.InformationEvent(),
                    Some.InformationEvent(),
                    Some.InformationEvent()
                };

                using (var log = new LoggerConfiguration()
                    .WriteTo.File(Path.Combine(temp.Path, fileName), rollOnFileSizeLimit: true, fileSizeLimitBytes: 1, hooks: gzipWrapper)
                    .CreateLogger())
                {

                    foreach (var logEvent in logEvents)
                    {
                        log.Write(logEvent);
                    }

                    files = Directory.GetFiles(temp.Path)
                        .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                    Assert.Equal(3, files.Length);
                    Assert.True(files[0].EndsWith(fileName), files[0]);
                    Assert.True(files[1].EndsWith("_001.txt"), files[1]);
                    Assert.True(files[2].EndsWith("_002.txt"), files[2]);
                }

                // Ensure the data was written through the wrapping GZipStream, by decompressing and comparing against
                // what we wrote
                for (var i = 0; i < files.Length; i++)
                {
                    using (var textStream = new MemoryStream())
                    {
                        using (var fs = System.IO.File.OpenRead(files[i]))
                        using (var decompressStream = new GZipStream(fs, CompressionMode.Decompress))
                        {
                            decompressStream.CopyTo(textStream);
                        }

                        textStream.Position = 0;
                        var lines = textStream.ReadAllLines();

                        Assert.Equal(1, lines.Count);
                        Assert.True(lines[0].EndsWith(logEvents[i].MessageTemplate.Text));
                    }
                }
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
