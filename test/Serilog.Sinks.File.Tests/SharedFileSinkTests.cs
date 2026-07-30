using Serilog.Core;
using Xunit;
using Serilog.Formatting.Json;
using Serilog.Sinks.File.Tests.Support;
using System.Text;

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
    public void FileIsReWrittenAfterEventIfDeleted()
    {
        using var tmp = TempFolder.ForCaller();
        var nonexistent = tmp.AllocateFilename("txt");
        var evt = Some.LogEvent("Hello, world!");

        Emit();
        var lines = System.IO.File.ReadAllLines(nonexistent);
        Assert.Contains("Hello, world!", lines[0]);
        Assert.Single(lines);

        System.IO.File.Delete(nonexistent);
        Assert.False(System.IO.File.Exists(nonexistent));
        Assert.Throws<FileNotFoundException>(() => System.IO.File.ReadAllLines(nonexistent));

        Emit();
        lines = System.IO.File.ReadAllLines(nonexistent);
        Assert.True(System.IO.File.Exists(nonexistent));
        Assert.Contains("Hello, world!", lines[0]);
        Assert.Single(lines);

        void Emit()
        {
            using var sink = new SharedFileSink(nonexistent, new JsonFormatter(), null);
            sink.Emit(evt);
        }
    }

    [Fact]
    public void FileIsReWrittenAfterEventIfDeletedWithoutRecreatingSink()
    {
        using var tmp = TempFolder.ForCaller();
        var path = tmp.AllocateFilename("txt");

        using (var sink = new SharedFileSink(path, new JsonFormatter(), null))
        {
            sink.Emit(Some.LogEvent("First event"));

            System.IO.File.Delete(path);
            Assert.False(System.IO.File.Exists(path));

            sink.Emit(Some.LogEvent("Second event"));
            Assert.True(System.IO.File.Exists(path));
        }

        var lines = System.IO.File.ReadAllLines(path);
        Assert.Single(lines);
        Assert.Contains("Second event", lines[0]);
    }

    [Fact]
    public void FileIsReWrittenAfterEventIfDeletedWithoutRecreatingSinkWhenLimitIsSpecified()
    {
        using var tmp = TempFolder.ForCaller();
        var path = tmp.AllocateFilename("txt");

        using (var sink = new SharedFileSink(path, new JsonFormatter(), 4096))
        {
            sink.Emit(Some.LogEvent("First event"));

            System.IO.File.Delete(path);
            Assert.False(System.IO.File.Exists(path));

            sink.Emit(Some.LogEvent("Second event"));
            Assert.True(System.IO.File.Exists(path));
        }

        var lines = System.IO.File.ReadAllLines(path);
        Assert.Single(lines);
        Assert.Contains("Second event", lines[0]);
    }

    [Fact]
    public void EncodingIsPreservedAfterDeleteAndRecreateWithoutRecreatingSink()
    {
        using var tmp = TempFolder.ForCaller();
        var path = tmp.AllocateFilename("txt");
        var encoding = Encoding.Unicode;

        using (var sink = new SharedFileSink(path, new JsonFormatter(), null, encoding))
        {
            sink.Emit(Some.LogEvent("First event"));

            System.IO.File.Delete(path);
            Assert.False(System.IO.File.Exists(path));

            sink.Emit(Some.LogEvent("Second event ç"));
            Assert.True(System.IO.File.Exists(path));
        }

        var bytes = System.IO.File.ReadAllBytes(path);
        Assert.True(bytes.AsSpan().StartsWith(encoding.GetPreamble()));

        var text = encoding.GetString(bytes);
        Assert.Contains("Second event ç", text);
        Assert.DoesNotContain("First event", text);
    }

    [Fact]
    public void EncodingIsPreservedAfterDeleteAndRecreateWithRecreatingSink()
    {
        using var tmp = TempFolder.ForCaller();
        var path = tmp.AllocateFilename("txt");
        var encoding = Encoding.Unicode;

        void EmitWithNewSink(string message)
        {
            using var sink = new SharedFileSink(path, new JsonFormatter(), null, encoding);
            sink.Emit(Some.LogEvent(message));
        }

        EmitWithNewSink("First event");

        System.IO.File.Delete(path);
        Assert.False(System.IO.File.Exists(path));

        EmitWithNewSink("Second event ç");
        Assert.True(System.IO.File.Exists(path));

        var bytes = System.IO.File.ReadAllBytes(path);
        Assert.True(bytes.AsSpan().StartsWith(encoding.GetPreamble()));

        var text = encoding.GetString(bytes);
        Assert.Contains("Second event ç", text);
        Assert.DoesNotContain("First event", text);
    }

    [Fact]
    public void FileDeletionDuringWriteOperationIsHandledGracefully()
    {
        // Test that when a file is deleted between checking its existence and writing to it,
        // the sink gracefully reopens the file and retries the write operation.
        using var tmp = TempFolder.ForCaller();
        var path = tmp.AllocateFilename("txt");

        using (var sink = new SharedFileSink(path, new JsonFormatter(), null))
        {
            var evt1 = Some.LogEvent("Event 1");
            sink.Emit(evt1);
            Assert.True(System.IO.File.Exists(path));

            // Delete the file while the sink is still open
            System.IO.File.Delete(path);
            Assert.False(System.IO.File.Exists(path));

            // Emit another event - this should trigger ReopenOutputStream and succeed
            var evt2 = Some.LogEvent("Event 2");
            sink.Emit(evt2);
            Assert.True(System.IO.File.Exists(path));
        }

        // Verify the second event was written to the recreated file
        var lines = System.IO.File.ReadAllLines(path);
        Assert.Single(lines);
        Assert.Contains("Event 2", lines[0]);
    }

    [Fact]
    public void FileEncodingIsPreservedAfterDeletionAndRecreation()
    {
        // Test that encoding is preserved when a file is deleted and recreated during write operations.
        using var tmp = TempFolder.ForCaller();
        var path = tmp.AllocateFilename("txt");
        var encoding = Encoding.Unicode;

        using (var sink = new SharedFileSink(path, new JsonFormatter(), null, encoding))
        {
            var evt1 = Some.LogEvent("Event with special chars: é à ç");
            sink.Emit(evt1);
            sink.FlushToDisk();
            Assert.True(System.IO.File.Exists(path));

            // Delete the file
            System.IO.File.Delete(path);
            Assert.False(System.IO.File.Exists(path));

            // Emit another event with special characters - encoding should be preserved
            var evt2 = Some.LogEvent("Second event with ñ");
            sink.Emit(evt2);
            sink.FlushToDisk();
            Assert.True(System.IO.File.Exists(path));
        }

        // Read after sink is disposed
        var bytes = System.IO.File.ReadAllBytes(path);
        Assert.True(bytes.AsSpan().StartsWith(encoding.GetPreamble()), "Encoding preamble should be present after recreation");

        var text = encoding.GetString(bytes);
        Assert.Contains("Second event with ñ", text);
    }

    [Fact]
    public void MultipleDeletionAndRecreationCyclesAreHandled()
    {
        // Test that multiple file deletion/recreation cycles don't cause resource leaks.
        using var tmp = TempFolder.ForCaller();
        var path = tmp.AllocateFilename("txt");

        using (var sink = new SharedFileSink(path, new JsonFormatter(), null))
        {
            for (int i = 0; i < 5; i++)
            {
                var evt = Some.LogEvent($"Event {i + 1}");
                sink.Emit(evt);
                Assert.True(System.IO.File.Exists(path));

                // Delete and recreate cycle
                System.IO.File.Delete(path);
                Assert.False(System.IO.File.Exists(path));
            }

            // Final emit after multiple cycles
            var finalEvt = Some.LogEvent("Final event");
            sink.Emit(finalEvt);
            Assert.True(System.IO.File.Exists(path));
        }

        // Verify the final event was written
        var lines = System.IO.File.ReadAllLines(path);
        Assert.Single(lines);
        Assert.Contains("Final event", lines[0]);
    }
}
