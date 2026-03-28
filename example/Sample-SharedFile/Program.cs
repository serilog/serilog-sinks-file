using Serilog;
using Serilog.Debugging;

SelfLog.Enable(Console.Out);

var sw = System.Diagnostics.Stopwatch.StartNew();

Console.WriteLine("Running with shared=true...");
sw.Restart();
var sharedLogger = new LoggerConfiguration()
    .WriteTo.File("log-shared.txt", shared: true)
    .CreateLogger();

for (var i = 0; i < 1000000; ++i)
{
    sharedLogger.Information("Hello, file logger!");
}

sharedLogger.Dispose();

sw.Stop();
Console.WriteLine($"Elapsed: {sw.ElapsedMilliseconds} ms");
Console.WriteLine($"Size: {new FileInfo("log-shared.txt").Length}");

Console.WriteLine("Press any key to delete the temporary log file...");
Console.ReadKey(true);
File.Delete("log-shared.txt");
