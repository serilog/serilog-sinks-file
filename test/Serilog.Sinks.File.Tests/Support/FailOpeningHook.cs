using System.Text;

namespace Serilog.Sinks.File.Tests.Support;

/// <inheritdoc />
/// <summary>
/// Demonstrates the use of <seealso cref="T:Serilog.FileLifecycleHooks" />, by failing to open for the given amount of times.
/// </summary>
class FailOpeningHook : FileLifecycleHooks
{
    readonly bool _asFileLocked;
    readonly int[] _failingInstances;

    public int TimesOpened { get; private set; }

    public FailOpeningHook(bool asFileLocked, params int[] failingInstances)
    {
        _asFileLocked = asFileLocked;
        _failingInstances = failingInstances;
    }

    public override Stream OnFileOpened(string path, Stream _, Encoding __)
    {
        TimesOpened++;
        if (_failingInstances.Contains(TimesOpened))
        {
            var message = $"We failed on try {TimesOpened}, the file was locked: {_asFileLocked}";
            
            throw _asFileLocked
                ? new IOException(message)
                : new Exception(message);
        }

        return base.OnFileOpened(path, _, __);
    }
}
