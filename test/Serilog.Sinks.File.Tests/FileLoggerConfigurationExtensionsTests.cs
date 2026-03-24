using Serilog.Sinks.File.Tests.Support;
using Serilog.Tests.Support;
using Xunit;
using System.Text;

namespace Serilog.Sinks.File.Tests;

public class FileLoggerConfigurationExtensionsTests
{
    static readonly string InvalidPath = new(Path.GetInvalidPathChars());

    [Fact]
    public void WhenWritingCreationExceptionsAreSuppressed()
    {
        new LoggerConfiguration()
            .WriteTo.File(InvalidPath)
            .CreateLogger();
    }

    [Fact]
    public void WhenWritingCreationExceptionsAreReported()
    {
        var listener = new CapturingLoggingFailureListener();

        var logger = new LoggerConfiguration()
            .WriteTo.Fallible(wt => wt.File(InvalidPath), listener)
            .CreateLogger();

        logger.Information("Hello");

        Assert.Single(listener.FailedEvents);
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
        using var tmp = TempFolder.ForCaller();
        using var log = new LoggerConfiguration()
            .WriteTo.File(new ThrowingLogEventFormatter(), tmp.AllocateFilename())
            .CreateLogger();
        log.Information("Hello");
    }

    [Fact]
    public void WhenAuditingLoggingExceptionsPropagate()
    {
        using var tmp = TempFolder.ForCaller();
        using var log = new LoggerConfiguration()
            .AuditTo.File(new ThrowingLogEventFormatter(), tmp.AllocateFilename())
            .CreateLogger();
        var ex = Assert.Throws<AggregateException>(() => log.Information("Hello"));
        Assert.IsType<NotImplementedException>(ex.GetBaseException());
    }

    [Fact]
    public void WhenFlushingToDiskReportedFileSinkCanBeCreatedAndDisposed()
    {
        using var tmp = TempFolder.ForCaller();
        using var log = new LoggerConfiguration()
            .WriteTo.File(tmp.AllocateFilename(), flushToDiskInterval: TimeSpan.FromMilliseconds(500))
            .CreateLogger();
        log.Information("Hello");
        Thread.Sleep(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void WhenFlushingToDiskReportedSharedFileSinkCanBeCreatedAndDisposed()
    {
        using var tmp = TempFolder.ForCaller();
        using var log = new LoggerConfiguration()
            .WriteTo.File(tmp.AllocateFilename(), shared: true, flushToDiskInterval: TimeSpan.FromMilliseconds(500))
            .CreateLogger();
        log.Information("Hello");
        Thread.Sleep(TimeSpan.FromSeconds(1));
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
        using var tmp = TempFolder.ForCaller();
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

    [Theory]
    [InlineData("yyyy-MM-dd_HH-mm-ss")]
    [InlineData("dd-MM-yyyy_HH-mm-ss")]
    [InlineData("yyyy_MM_dd-HH_mm_ss")]
    [InlineData("dd_MM_yyyy_HH_mm_ss")]
    [InlineData("yyyy-MM-dd-HH-mm-ss")]
    [InlineData("dd-MM-yyyy_HHmmss")]
    [InlineData("ddMMyyyy_HH-mm-ss")]
    [InlineData("yyyyMMddHHmmss")]
    public void SetValidDateTimeFormatWorksSuccessfully(string dateTimeStr)
    {
        string filename = "logging-.txt";

        LoggerConfiguration logConfig = new LoggerConfiguration().
            WriteTo.File(filename, outputTemplate: "{Message}", encoding: Encoding.Unicode, fileSizeLimitBytes: 1024,
                rollOnFileSizeLimit: true, dateTimeFormatFileName: dateTimeStr);

        Assert.NotNull(logConfig);
    }

    [Theory]
    [InlineData(false, 1024, RollingInterval.Infinite, "yyyyMMddHHmmss")]
    [InlineData(true, 1024, RollingInterval.Infinite, "yyyyMMddHHmmss")]
    public void SetValidConfigurationWorksSuccessfully(bool rollOnFileSizeLimit, long fileSizeLimit, RollingInterval rollingInterval, string dateTimeStr)
    {
        string filename = "logging-.txt";

        LoggerConfiguration logConfig = new LoggerConfiguration().
            WriteTo.File(filename, outputTemplate: "{Message}", encoding: Encoding.Unicode, fileSizeLimitBytes: fileSizeLimit,
                rollOnFileSizeLimit: rollOnFileSizeLimit, dateTimeFormatFileName: dateTimeStr, rollingInterval: rollingInterval);

        Assert.NotNull(logConfig);
    }

    [Theory]
    [InlineData("yy-MM-dd_HH-mm-ss")]
    [InlineData("dd-MM-yyyy_HH-mm-s")]
    [InlineData("-yyyy_MM_dd-HH_mm_ss")]
    [InlineData("dd_MM_yyyy_HH_mm_ss_")]
    [InlineData("yyyy-MM-dd-HH-mm-s")]
    [InlineData("dd-MM-yyyy_Hmmss")]
    [InlineData("ddMMyyyy+HH-mm-ss")]
    [InlineData("-yyyyMMddHHmmss-")]
    public void SetInvalidDateTimeFormatThrowException(string dateTimeStr)
    {
        using var tmp = TempFolder.ForCaller();
        var filename = tmp.AllocateFilename("txt");

        Assert.Throws<ArgumentException>(() =>
            new LoggerConfiguration().WriteTo.File(filename, outputTemplate: "{Message}", encoding: Encoding.Unicode,
                fileSizeLimitBytes: 1024, rollOnFileSizeLimit: true, dateTimeFormatFileName: dateTimeStr));
    }

    [Theory]
    [InlineData(false, 0, RollingInterval.Infinite, "yyyyMMddHHmmss")]
    [InlineData(true, 1024, RollingInterval.Hour, "yyyyMMddHHmmss")]
    public void SetInvalidConfigurationWorksThrowException(bool rollOnFileSizeLimit, long fileSizeLimit, RollingInterval rollingInterval, string dateTimeStr)
    {
        string filename = "logging-.txt";

        // tiny hack, because the InlineData could not have a nullable value.
        long? tmpFileSizeLimit = null;
        if (fileSizeLimit > 0)
            tmpFileSizeLimit = fileSizeLimit;

        Assert.Throws<ArgumentException>(() =>
            new LoggerConfiguration().
                WriteTo.File(filename, outputTemplate: "{Message}", encoding: Encoding.Unicode, fileSizeLimitBytes: tmpFileSizeLimit,
                    rollOnFileSizeLimit: rollOnFileSizeLimit, dateTimeFormatFileName: dateTimeStr, rollingInterval: rollingInterval));
    }
}
