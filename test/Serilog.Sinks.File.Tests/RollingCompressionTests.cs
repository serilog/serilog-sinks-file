using System.Linq;
using Xunit;
using Serilog.Sinks.File.Tests.Support;
using System.IO;

namespace Serilog.Sinks.File.Tests
{
    public class RollingCompressionTests
    {
        [Fact]
        public void GZipCompressionCorrect()
        {
            var fileName = Some.String() + "log.txt";
            var temp = Some.TempFolderPath();
            var pathFormat = Path.Combine(temp, fileName);

            using (var log = new LoggerConfiguration()
                       .WriteTo.File(pathFormat,
                       rollOnFileSizeLimit: true, fileSizeLimitBytes: 1,
                       compressionType: CompressionType.GZip)
                       .CreateLogger())
            {

                log.Information("test");
                log.Information("test");

                string compressedFile = Directory.EnumerateFiles(temp).Where(name => name.Contains("-GZip")).First();

                using (FileStream compressedStream = new FileStream(compressedFile, FileMode.Open))
                {

                    byte[] compressedBytes = new byte[2];

                    compressedStream.Read(compressedBytes, 0, compressedBytes.Length);

                    Assert.Equal(compressedBytes[0], 0x1f);
                    Assert.Equal(compressedBytes[1], 0x8b);
                }

                log.Dispose();

                foreach (var loggedFile in Directory.GetFiles(temp))
                {
                    System.IO.File.Delete(loggedFile);
                }
                Directory.Delete(temp);
            }

        }

        [Fact]
        public void ZipCompressionCorrect()
        {

            var fileName = Some.String() + "log.txt";
            var temp = Some.TempFolderPath();
            var pathFormat = Path.Combine(temp, fileName);

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

                log.Dispose();

                foreach (var loggedFile in Directory.GetFiles(temp))
                {
                    System.IO.File.Delete(loggedFile);
                }
                Directory.Delete(temp);
            }
        }
    }
}
