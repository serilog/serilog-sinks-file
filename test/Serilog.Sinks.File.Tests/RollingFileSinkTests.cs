using System.IO.Compression;
using Xunit;
using Serilog.Events;
using Serilog.Sinks.File.Tests.Support;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Debugging;
using Xunit.Abstractions;

namespace Serilog.Sinks.File.Tests;

public class RollingFileSinkTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public RollingFileSinkTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

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
    public void WhenRetentionTimeIsSetOldFilesAreDeleted()
    {
        LogEvent e1 = Some.InformationEvent(DateTime.Today.AddDays(-5)),
            e2 = Some.InformationEvent(e1.Timestamp.AddDays(2)),
            e3 = Some.InformationEvent(e2.Timestamp.AddDays(5));

        TestRollingEventSequence(
            (pf, wt) => wt.File(pf, retainedFileTimeLimit: TimeSpan.FromDays(1), rollingInterval: RollingInterval.Day),
            new[] {e1, e2, e3},
            files =>
            {
                Assert.Equal(3, files.Count);
                Assert.True(!System.IO.File.Exists(files[0]));
                Assert.True(!System.IO.File.Exists(files[1]));
                Assert.True(System.IO.File.Exists(files[2]));
            });
    }

    [Fact]
    public void WhenRetentionCountAndTimeIsSetOldFilesAreDeletedByTime()
    {
        LogEvent e1 = Some.InformationEvent(DateTime.Today.AddDays(-5)),
            e2 = Some.InformationEvent(e1.Timestamp.AddDays(2)),
            e3 = Some.InformationEvent(e2.Timestamp.AddDays(5));

        TestRollingEventSequence(
            (pf, wt) => wt.File(pf, retainedFileCountLimit: 2, retainedFileTimeLimit: TimeSpan.FromDays(1), rollingInterval: RollingInterval.Day),
            new[] {e1, e2, e3},
            files =>
            {
                Assert.Equal(3, files.Count);
                Assert.True(!System.IO.File.Exists(files[0]));
                Assert.True(!System.IO.File.Exists(files[1]));
                Assert.True(System.IO.File.Exists(files[2]));
            });
    }

    [Fact]
    public void WhenRetentionCountAndTimeIsSetOldFilesAreDeletedByCount()
    {
        LogEvent e1 = Some.InformationEvent(DateTime.Today.AddDays(-5)),
            e2 = Some.InformationEvent(e1.Timestamp.AddDays(2)),
            e3 = Some.InformationEvent(e2.Timestamp.AddDays(5));

        TestRollingEventSequence(
            (pf, wt) => wt.File(pf, retainedFileCountLimit: 2, retainedFileTimeLimit: TimeSpan.FromDays(10), rollingInterval: RollingInterval.Day),
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
                Assert.False(System.IO.File.Exists(files[0]));
                Assert.True(System.IO.File.Exists(files[1]));
                Assert.True(System.IO.File.Exists(files[2]));
                Assert.True(System.IO.File.Exists(ArchiveOldLogsHook.AddTopDirectory(files[0], archiveDirectory)));
            });
    }

    [Fact]
    public void WhenFirstOpeningFailedWithLockRetryDelayedUntilNextCheckpoint()
    {
        var fileName = Some.String() + ".txt";
        using var temp = new TempFolder();
        using var log = new LoggerConfiguration()
            .WriteTo.File(Path.Combine(temp.Path, fileName), rollOnFileSizeLimit: true, fileSizeLimitBytes: 1, rollingInterval: RollingInterval.Minute, hooks: new FailOpeningHook(true, 2, 3, 4))
            .CreateLogger();
        LogEvent e1 = Some.InformationEvent(new DateTime(2012, 10, 28)),
            e2 = Some.InformationEvent(e1.Timestamp.AddSeconds(1)),
            e3 = Some.InformationEvent(e1.Timestamp.AddMinutes(5)),
            e4 = Some.InformationEvent(e1.Timestamp.AddMinutes(31));
        LogEvent[] logEvents = new[] { e1, e2, e3, e4 };
        
        foreach (var logEvent in logEvents)
        {
            Clock.SetTestDateTimeNow(logEvent.Timestamp.DateTime);
            log.Write(logEvent);
        }

        var files = Directory.GetFiles(temp.Path)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var pattern = "yyyyMMddHHmm";

        Assert.Equal(6, files.Length);
        // Successful write of e1:
        Assert.True(files[0].EndsWith(ExpectedFileName(fileName, e1.Timestamp, pattern)), files[0]);
        // Failing writes for e2, will be dropped and logged to SelfLog:
        Assert.True(files[1].EndsWith("_001.txt"), files[1]);
        Assert.True(files[2].EndsWith("_002.txt"), files[2]);
        Assert.True(files[3].EndsWith("_003.txt"), files[3]);
        // Successful write of e3:
        Assert.True(files[4].EndsWith(ExpectedFileName(fileName, e3.Timestamp, pattern)), files[4]);
        // Successful write of e4:
        Assert.True(files[5].EndsWith(ExpectedFileName(fileName, e4.Timestamp, pattern)), files[5]);
    }

    [Fact]
    public void WhenFirstOpeningFailedWithLockRetryDelayed30Minutes()
    {
        var fileName = Some.String() + ".txt";
        using var temp = new TempFolder();
        using var log = new LoggerConfiguration()
            .WriteTo.File(Path.Combine(temp.Path, fileName), rollOnFileSizeLimit: true, fileSizeLimitBytes: 1, rollingInterval: RollingInterval.Hour, hooks: new FailOpeningHook(true, 2, 3, 4))
            .CreateLogger();
        LogEvent e1 = Some.InformationEvent(new DateTime(2012, 10, 28)),
            e2 = Some.InformationEvent(e1.Timestamp.AddSeconds(1)),
            e3 = Some.InformationEvent(e1.Timestamp.AddMinutes(5)),
            e4 = Some.InformationEvent(e1.Timestamp.AddMinutes(31));
        LogEvent[] logEvents = new[] { e1, e2, e3, e4 };
        
        SelfLog.Enable(_testOutputHelper.WriteLine);
        foreach (var logEvent in logEvents)
        {
            Clock.SetTestDateTimeNow(logEvent.Timestamp.DateTime);
            log.Write(logEvent);
        }

        var files = Directory.GetFiles(temp.Path)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var pattern = "yyyyMMddHH";

        Assert.Equal(4, files.Length);
        // Successful write of e1:
        Assert.True(files[0].EndsWith(ExpectedFileName(fileName, e1.Timestamp, pattern)), files[0]);
        // Failing writes for e2, will be dropped and logged to SelfLog; on lock it will try it three times:
        Assert.True(files[1].EndsWith("_001.txt"), files[1]);
        Assert.True(files[2].EndsWith("_002.txt"), files[2]);
        /* e3 will be dropped and logged to SelfLog without new file as it's in the 30 minutes cooldown and roller only starts on next hour! */
        // Successful write of e4, the third file will be retried after failing initially:
        Assert.True(files[3].EndsWith("_003.txt"), files[3]);
    }

    [Fact]
    public void WhenFirstOpeningFailedWithoutLockRetryDelayed30Minutes()
    {
        var fileName = Some.String() + ".txt";
        using var temp = new TempFolder();
        using var log = new LoggerConfiguration()
            .WriteTo.File(Path.Combine(temp.Path, fileName), rollOnFileSizeLimit: true, fileSizeLimitBytes: 1, rollingInterval: RollingInterval.Hour, hooks: new FailOpeningHook(false, 2))
            .CreateLogger();
        LogEvent e1 = Some.InformationEvent(new DateTime(2012, 10, 28)),
            e2 = Some.InformationEvent(e1.Timestamp.AddSeconds(1)),
            e3 = Some.InformationEvent(e1.Timestamp.AddMinutes(5)),
            e4 = Some.InformationEvent(e1.Timestamp.AddMinutes(31));
        LogEvent[] logEvents = new[] { e1, e2, e3, e4 };
        
        SelfLog.Enable(_testOutputHelper.WriteLine);
        foreach (var logEvent in logEvents)
        {
            Clock.SetTestDateTimeNow(logEvent.Timestamp.DateTime);
            log.Write(logEvent);
        }

        var files = Directory.GetFiles(temp.Path)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var pattern = "yyyyMMddHH";

        Assert.Equal(2, files.Length);
        // Successful write of e1:
        Assert.True(files[0].EndsWith(ExpectedFileName(fileName, e1.Timestamp, pattern)), files[0]);
        /* Failing writes for e2, will be dropped and logged to SelfLog; on non-lock it will try it once */
        /* e3 will be dropped and logged to SelfLog without new file as it's in the 30 minutes cooldown and roller only starts on next hour! */
        // Successful write of e4, the file will be retried after failing initially:
        Assert.True(files[1].EndsWith("_001.txt"), files[1]);
    }

    [Fact]
    public void WhenSizeLimitIsBreachedNewFilesCreated()
    {
        var fileName = Some.String() + ".txt";
        using var temp = new TempFolder();
        using var log = new LoggerConfiguration()
            .WriteTo.File(Path.Combine(temp.Path, fileName), rollOnFileSizeLimit: true, fileSizeLimitBytes: 1)
            .CreateLogger();
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

    [Fact]
    public void WhenStreamWrapperSpecifiedIsUsedForRolledFiles()
    {
        var gzipWrapper = new GZipHooks();
        var fileName = Some.String() + ".txt";

        using var temp = new TempFolder();
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
            using var textStream = new MemoryStream();
            using (var fs = System.IO.File.OpenRead(files[i]))
            using (var decompressStream = new GZipStream(fs, CompressionMode.Decompress))
            {
                decompressStream.CopyTo(textStream);
            }

            textStream.Position = 0;
            var lines = textStream.ReadAllLines();

            Assert.Single(lines);
            Assert.EndsWith(logEvents[i].MessageTemplate.Text, lines[0]);
        }
    }

    [Fact]
    public void IfTheLogFolderDoesNotExistItWillBeCreated()
    {
        var fileName = Some.String() + "-{Date}.txt";
        var temp = Some.TempFolderPath();
        var folder = Path.Combine(temp, Guid.NewGuid().ToString());
        var pathFormat = Path.Combine(folder, fileName);

        Logger? log = null;

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
            log?.Dispose();
            Directory.Delete(temp, true);
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
        Action<IList<string>>? verifyWritten = null)
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

                var expected = ExpectedFileName(pathFormat, @event.Timestamp, "yyyyMMdd");
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

    static string ExpectedFileName(string fileName, DateTimeOffset timestamp, string pattern)
    {
        return fileName.Replace(".txt", timestamp.ToString(pattern) + ".txt");
    }
}
