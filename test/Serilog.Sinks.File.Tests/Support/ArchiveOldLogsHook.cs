using System;
using System.IO;
using System.Text;

namespace Serilog.Sinks.File.Tests.Support
{
    internal class ArchiveOldLogsHook : FileLifecycleHooks
    {
        private readonly string _relativeArchiveDir;

        public ArchiveOldLogsHook(string relativeArchiveDir)
        {
            _relativeArchiveDir = relativeArchiveDir;
        }

        public override void OnFileDeleting(string path)
        {
            base.OnFileDeleting(path);
            var newFile = AddTopDirectory(path, _relativeArchiveDir, true);
            System.IO.File.Copy(path, newFile, false);
        }

        public static string AddTopDirectory(string path, string directoryToAdd, bool createOnNonExist = false)
        {
            string file = Path.GetFileName(path);
            string directory = Path.Combine(Path.GetDirectoryName(path) ?? throw new InvalidOperationException(), directoryToAdd);

            if (createOnNonExist && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            return Path.Combine(directory, file);
        }
    }
}
