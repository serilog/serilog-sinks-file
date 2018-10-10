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

            Log.Logger = new LoggerConfiguration()
                .WriteTo.File("log.txt")
                .CreateLogger();

            for (var i = 0; i < 1000000; ++i)
            {
                Log.Information("Hello, file logger!");
            }

            Log.CloseAndFlush();

            sw.Stop();

            Console.WriteLine($"Elapsed: {sw.ElapsedMilliseconds} ms");
            Console.WriteLine($"Size: {new FileInfo("log.txt").Length}");

            Console.WriteLine("Press any key to delete the temporary log file...");
            Console.ReadKey(true);

            File.Delete("log.txt");
        }
    }
}
