using System;
using System.Collections.Generic;
using System.IO;

namespace Serilog.Sinks.File
{
    /// <summary>Interface of types that provide rolling log-file filename information.</summary>
    public interface IRollingFilePathProvider
    {
        /// <summary>The configured interval period.</summary>
        RollingInterval Interval { get; }

        /// <summary>Returns <c>true</c> if this implementation will produce file names that exist in subdirectories of the log directory.</summary>
        bool SupportsSubdirectories { get; }

        /// <summary>Returns a Windows-compatible glob pattern for matching file-names in a directory. May return "*" if the implementation could not determine a more specific glob pattern. Non-log files may match the glob pattern. The value will not include any directory path names components (so if the user wants files in a directory-per-interval, it would only match the expected filename WITHIN that interval directory).</summary>
        string DirectorySearchPattern { get; }

        /// <summary>Gets a path to a file (either absolute, or relative to the current file log directory) for the specified interval and point-in-time.</summary>
        string GetRollingLogFilePath(DateTime instant, Int32? sequenceNumber);

        /// <summary>Given a file-name (FileInfo) to a log file, returns <c>true</c> if the file matches the file-name pattern for that interval, and also returns the interval's period start encoded in the filename and the sequence number, if any.</summary>
        bool MatchRollingLogFilePath(FileInfo file, out DateTime? periodStart, out int? sequenceNumber);
    }

    internal static class PathUtility
    {
        public static string GetFilePathWithoutExtension(string filePath)
        {
            string ext = Path.GetExtension( filePath );
            if( String.IsNullOrEmpty( ext ) ) return filePath;

            return filePath.Substring( 0, filePath.Length - ext.Length );
        }

        public static string EnsureTrailingDirectorySeparator(string directoryPath)
        {
            if( String.IsNullOrWhiteSpace( directoryPath ) ) throw new ArgumentNullException(nameof(directoryPath));

            directoryPath = directoryPath.Trim();
            Char last = directoryPath[ directoryPath.Length - 1 ];

            if( last == Path.DirectorySeparatorChar || last == Path.AltDirectorySeparatorChar ) return directoryPath;

            directoryPath = directoryPath + Path.DirectorySeparatorChar;

            return directoryPath;
        }

        public static bool CaseInsensitiveFileSystem => true;
        // HACK TODO: This is surprisingly difficult.
        // There are a few possibilities:
        // * On Windows 10 in a WSL context, we can check for `FILE_FLAG_POSIX_SEMANTICS` on a given directory (as NTFS now supports per-directory case-sensitivity...)O
        // * On other platforms, we can test case-sensitivity by creating a file with a random (unused) name like "ABCDEFG.txt" and then testing if "abcdefg.TXT" exists.
        // * Alternatively, perhaps allow users to specify a flag during configuration to declare the filesystem case-sensitive?
    }

    internal class FileInfoComparer : IEqualityComparer<FileInfo>
    {
        public static FileInfoComparer Instance { get; } = new FileInfoComparer();

        public Boolean Equals( FileInfo x, FileInfo y )
        {
            if( x == null && y == null ) return true;
            if( x == null || y == null ) return false;

            if( PathUtility.CaseInsensitiveFileSystem )
            {
                return String.Equals( x.FullName, y.FullName, StringComparison.OrdinalIgnoreCase );
            }
            else
            {
                return String.Equals( x.FullName, y.FullName, StringComparison.Ordinal );
            }
        }

        public Int32 GetHashCode( FileInfo obj )
        {
            return obj.FullName.GetHashCode();
        }
    }
}
