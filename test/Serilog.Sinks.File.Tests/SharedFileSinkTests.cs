using Serilog.Core;
using Xunit;
using Serilog.Formatting.Json;
using Serilog.Sinks.File.Tests.Support;

#pragma warning disable 618

namespace Serilog.Sinks.File.Tests;

public class SharedFileSinkTests
{
    [Fact]
    public void FileIsWrittenIfNonexistent()
    {
        using var tmp = TempFolder.ForCaller();
        var nonexistent = tmp.AllocateFilename("txt");
        var evt = Some.LogEvent("Hello, world!");

        using (var sink = new SharedFileSink(nonexistent, new JsonFormatter(), null))
        {
            sink.Emit(evt);
        }

        var lines = System.IO.File.ReadAllLines(nonexistent);
        Assert.Contains("Hello, world!", lines[0]);
    }

    [Fact]
    public void FileIsAppendedToWhenAlreadyCreated()
    {
        using var tmp = TempFolder.ForCaller();
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

    [Fact]
    public void WhenLimitIsSpecifiedFileSizeIsRestricted()
    {
        const int maxBytes = 5000;
        const int eventsToLimit = 10;

        using var tmp = TempFolder.ForCaller();
        var path = tmp.AllocateFilename("txt");
        var evt = Some.LogEvent(new string('n', maxBytes / eventsToLimit));

        var listener = new CapturingLoggingFailureListener();
        using (var sink = new SharedFileSink(path, new JsonFormatter(), maxBytes))
        {
            ((ISetLoggingFailureListener)sink).SetFailureListener(listener);
            for (var i = 0; i < eventsToLimit * 2; i++)
            {
                sink.Emit(evt);
            }
        }

        var size = new FileInfo(path).Length;
        Assert.True(size > maxBytes);
        Assert.True(size < maxBytes * 2);
        Assert.NotEmpty(listener.FailedEvents);
    }

    [Fact]
    public void WhenLimitIsNotSpecifiedFileSizeIsNotRestricted()
    {
        const int maxBytes = 5000;
        const int eventsToLimit = 10;

        using var tmp = TempFolder.ForCaller();
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

    [Fact]
    public void FileIsNotLockedAfterDisposal()
    {
        using var tmp = TempFolder.ForCaller();
        var path = tmp.AllocateFilename("txt");
        var evt = Some.LogEvent("Hello, world!");

        using (var sink = new SharedFileSink(path, new JsonFormatter(), null))
        {
            sink.Emit(evt);
        }

        // Ensure the file is not locked after the sink is disposed
        var exceptionThrown = false;
        try
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite))
            {
            }
        }
        catch (IOException)
        {
            exceptionThrown = true;
        }

        Assert.False(exceptionThrown, "File should not be locked after sink disposal.");
    }

    [Fact]
    public void FileIsLockedByOneUserAndAnotherUserTriesToWrite()
    {
        using var tmp = TempFolder.ForCaller();
        var path = tmp.AllocateFilename("txt");
        var evt = Some.LogEvent("Hello, world!");

        // Lock the file by one user
        using (var stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
        {
            using (var writer = new StreamWriter(stream))
            {
                writer.WriteLine("Initial content");
                writer.Flush();

                // Try to write to the locked file by another user
                var exceptionThrown = false;
                try
                {
                    using (var sink = new SharedFileSink(path, new JsonFormatter(), null))
                    {
                        sink.Emit(evt);
                    }
                }
                catch (IOException)
                {
                    exceptionThrown = true;
                }

                Assert.True(exceptionThrown, "IOException should be thrown when trying to write to a locked file.");
            }
        }

        // Verify the file content
        var lines = System.IO.File.ReadAllLines(path);
        Assert.Contains("Initial content", lines);
        Assert.DoesNotContain("Hello, world!", lines);
    }

    [Fact]
    public async Task FileIsNotLockedDuringAsyncOperations()
    {
        using var tmp = TempFolder.ForCaller();
        var path = tmp.AllocateFilename("txt");
        var evt = Some.LogEvent("Hello, world!");

        using (var sink = new SharedFileSink(path, new JsonFormatter(), null))
        {
            await Task.Run(() => sink.Emit(evt));
        }

        // Ensure the file is not locked after async operations
        var exceptionThrown = false;
        try
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite))
            {
                stream.ReadAllLines();
            }
        }
        catch (IOException)
        {
            exceptionThrown = true;
        }

        Assert.False(exceptionThrown, "File should not be locked after async operations.");
    }
}
