using System;
using System.IO;
using Serilog;
using Serilog.Debugging;

namespace Sample
{
    public class Program
    {
        public static void Main(string[] args)
        {
            SelfLog.Enable(Console.Out);

            var sw = System.Diagnostics.Stopwatch.StartNew();

            // create a log'name'.txt file every minute
            Log.Logger = new LoggerConfiguration()
                .WriteTo.File("log.txt", rollingInterval: RollingInterval.Minute, compressionType: CompressionType.GZip)
                .CreateLogger();

            // two minute loop to create two log files
            // need to determine when new file is created so compress before new file is seeded
            var end = DateTime.UtcNow.AddMinutes(2);
            while(DateTime.UtcNow < end)
            {
                Log.Information("Hello, file logger!");
            }

            Log.CloseAndFlush();

            sw.Stop();

            Console.WriteLine($"Elapsed: {sw.ElapsedMilliseconds} ms");
            Console.WriteLine($"Size: {new FileInfo("log.txt").Length}");
            Console.WriteLine($"Path: {new FileInfo("log.txt").DirectoryName}");

            Console.WriteLine("Press any key to delete the temporary log file...");
            Console.ReadKey(true);

            File.Delete("log.txt");
        }
    }
}
