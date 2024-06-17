using System.Text;
using Serilog.Sinks.File.Tests.Support;
using Xunit;

namespace Serilog.Sinks.File.Tests;

public class WriteCountingStreamTests
{
    [Fact]
    public void CountedLengthIsResetToStreamLengthIfNewSizeIsSmaller()
    {
        // If we counted 10 bytes written and SetLength was called with a smaller length (e.g. 5)
        // we adjust the counter to the new byte count of the file to reflect reality

        using var tmp = TempFolder.ForCaller();
        var path = tmp.AllocateFilename("txt");

        long streamLengthAfterSetLength;
        long countedLengthAfterSetLength;

        using (var fileStream = System.IO.File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read))
        using (var countingStream = new WriteCountingStream(fileStream))
        using (var writer = new StreamWriter(countingStream, new UTF8Encoding(false)))
        {
            writer.WriteLine("Hello, world!");
            writer.Flush();

            countingStream.SetLength(5);
            streamLengthAfterSetLength = countingStream.Length;
            countedLengthAfterSetLength = countingStream.CountedLength;
        }

        Assert.Equal(5, streamLengthAfterSetLength);
        Assert.Equal(5, countedLengthAfterSetLength);

        var lines = System.IO.File.ReadAllLines(path);

        Assert.Single(lines);
        Assert.Equal("Hello", lines[0]);
    }

    [Fact]
    public void CountedLengthRemainsTheSameIfNewSizeIsLarger()
    {
        // If we counted 10 bytes written and SetLength was called with a larger length (e.g. 100)
        // we leave the counter intact because our position on the stream remains the same... The
        // file just grew larger in size

        using var tmp = TempFolder.ForCaller();
        var path = tmp.AllocateFilename("txt");

        long streamLengthBeforeSetLength;
        long streamLengthAfterSetLength;
        long countedLengthAfterSetLength;

        using (var fileStream = System.IO.File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read))
        using (var countingStream = new WriteCountingStream(fileStream))
        using (var writer = new StreamWriter(countingStream, new UTF8Encoding(false)))
        {
            writer.WriteLine("Hello, world!");
            writer.Flush();

            streamLengthBeforeSetLength = countingStream.CountedLength;
            countingStream.SetLength(100);
            streamLengthAfterSetLength = countingStream.Length;
            countedLengthAfterSetLength = countingStream.CountedLength;
        }

        Assert.Equal(100, streamLengthAfterSetLength);
        Assert.Equal(streamLengthBeforeSetLength, countedLengthAfterSetLength);

        var lines = System.IO.File.ReadAllLines(path);

        Assert.Equal(2, lines.Length);
        Assert.Equal("Hello, world!", lines[0]);
    }
}
