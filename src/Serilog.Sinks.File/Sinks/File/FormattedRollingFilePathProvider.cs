using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace Serilog.Sinks.File
{
    /// <summary>Implements <see cref="IRollingFilePathProvider"/> around a Custom .NET DateTime format string which should (but is not required to) contain the file-name extension as an enquoted literal. Any sequence numbers are inserted before the file-name extension with a leading underscore '_' character.</summary>
    class FormattedRollingFilePathProvider : IRollingFilePathProvider
    {
        readonly string logDirectory;
        readonly string filePathFormat;

        static readonly Regex _sequenceSuffixRegex = new Regex( @"_([0-9]{3,})$", RegexOptions.Compiled ); // Matches "_000", "_001", "_999", "_1000", "_999999", but not "_", "_0", "_a", "_99", etc. Requiring 3 digits avoids matching "_dd", "_mm" in a file-name.

        public FormattedRollingFilePathProvider( string logDirectoryAbsolutePath, RollingInterval interval, string filePathFormat )
        {
            this.Interval = interval;

            if( String.IsNullOrWhiteSpace( logDirectoryAbsolutePath ) ) throw new ArgumentNullException( nameof(logDirectoryAbsolutePath) );
            if( !Path.IsPathRooted( logDirectoryAbsolutePath ) ) throw new ArgumentException( message: "Path must be absolute.", paramName: nameof(logDirectoryAbsolutePath) );

            this.logDirectory   = PathUtility.EnsureTrailingDirectorySeparator( logDirectoryAbsolutePath );
            this.filePathFormat = filePathFormat  ?? throw new ArgumentNullException( nameof(filePathFormat) );

            // Test the format before using it:
            // Also use the rendered string to get any prefix and file-name extensions for generating a glob pattern.
            ValidateFilePathFormat( interval, filePathFormat, out string exampleFormatted );

            this.DirectorySearchPattern = CreateDirectorySearchPattern( exampleFormatted );
        }

        private static void ValidateFilePathFormat(RollingInterval interval, string filePathFormat, out string exampleFormatted)
        {
            const string DefaultMessage = "The rolling file name format is invalid. ";

            // Using `DateTime.MaxValue` to get an example formatted value. This is better than `DateTime.Now` because it's constant and deterministic, and because all the components are at their maximum (e.g. Hour == 23) it means it tests for the improper use of 'h' (instead of 'hh') and if 'tt' is used, for example.

            DateTime pointInTime = interval.GetCurrentCheckpoint( DateTime.MaxValue ) ?? DateTime.MinValue;
            DateTime parsed;
            string formatted;

            try
            {
                formatted = pointInTime.ToString( filePathFormat, CultureInfo.InvariantCulture );
                parsed    = DateTime.ParseExact( formatted, filePathFormat, CultureInfo.InvariantCulture );
            }
            catch( ArgumentException argEx )
            {
                throw new ArgumentException( DefaultMessage + "See the inner ArgumentException for details.", argEx );
            }
            catch( FormatException formatEx )
            {
                throw new ArgumentException( DefaultMessage + "See the inner FormatException for details.", formatEx );
            }

            if( parsed != pointInTime )
            {
                throw new ArgumentException( DefaultMessage + "The format does not round-trip DateTime values correctly. Does the file path format have sufficient specifiers for the selected interval? (e.g. did you specify " + nameof(RollingInterval) + "." + nameof(RollingInterval.Hour) + " but forget to include an 'HH' specifier in the file path format?)" );
            }

            // Also do an early check for invalid file-name characters, e.g. ':'. Note that '/' and '\' are allowed - though if a user on Linux uses "'Log'-yyyy/MM/dd/'.log'" as a format string it might not be the effect they want...
            if( formatted.IndexOfAny( Path.GetInvalidPathChars() ) > -1 || formatted.IndexOf(':') >= 2 ) // ':' isn't included in `Path.GetInvalidPathChars()` on Windows for some reason.
            {
                throw new ArgumentException( DefaultMessage + "The format generates file-names that contain illegal characters, such as ':' or '/'." );
            }

            exampleFormatted = formatted;
        }

        private static string CreateDirectorySearchPattern(string formatted)
        {
            // If the generated file-name extension does not contain any digits then we can assume it's a static textual extension.
            // This will break if the file-name extension contains some alphabetic DateTime format specifier, of course.

            string globPrefix = null;
            string globSuffix = Path.GetExtension( formatted );

            {
                // NOTE: This breaks if there are no file-name extensions and the user is using dots to separate-out file extensions, erk!
                string exampleFileName = Path.GetFileNameWithoutExtension( formatted ); // Remove any formatted directory names that are applied before the filename format, e.g. `logDirectoryAbsolutePath + "yyyy-MM'\Log: 'yyyy-MM-dd HH-mm'.log'` --> "Log: 2019-02-22 23:59.log"

                Int32 firstNonLetterCharIdx = -1;
                for( Int32 i = 0; i < exampleFileName.Length; i++ )
                {
                    if( !( Char.IsLetter( exampleFileName[i] ) || Char.IsWhiteSpace( exampleFileName[i] ) || exampleFileName[i] == '-' || exampleFileName[i] == '_' ) )
                    {
                        firstNonLetterCharIdx = i;
                        break;
                    }
                }

                if( firstNonLetterCharIdx > -1 && firstNonLetterCharIdx < exampleFileName.Length - 1 )
                {
                    globPrefix = formatted.Substring( 0, firstNonLetterCharIdx );
                }
            }

            return globPrefix + "*" + globSuffix;
        }

        public RollingInterval Interval { get; }

        public bool SupportsSubdirectories => true;

        public string DirectorySearchPattern { get; }

        public string GetRollingLogFilePath( DateTime instant, Int32? sequenceNumber )
        {
            // Get period-start for the given point-in-time instant based on the interval:
            // e.g. if `instant == 2019-02-22 23:59:59` and `interval == Month`, then use `2019-02-01 00:00:00`.
            // This is to ensure that if the format string "yyyy-MM-dd HH:mm'.log" is used with a Monthly interval, for example, the dd, HH, and mm components will be normalized.

            String filePath;
            DateTime periodStart = this.Interval.GetCurrentCheckpoint( instant ) ?? DateTime.MinValue; // NOTE: `GetCurrentCheckpoint == null` when Infinite is used with a max file-size limit. While it would be nice to assume the file name format does not contain any DateTime format specifiers, use DateTime.MinValue just in case.

            filePath = periodStart.ToString( this.filePathFormat, CultureInfo.InvariantCulture );

            if( sequenceNumber != null )
            {
                // Insert the sequence number immediately before the extension.
                filePath = PathUtility.GetFilePathWithoutExtension( filePath ) + "_" + sequenceNumber.Value.ToString( "000", CultureInfo.InvariantCulture ) + Path.GetExtension( filePath );
            }

            return Path.Combine( this.logDirectory, filePath );
        }

        public Boolean MatchRollingLogFilePath( FileInfo file, out DateTime? periodStart, out Int32? sequenceNumber )
        {
            if( file == null ) throw new ArgumentNullException(nameof(file));

            // Remove the logDirectory prefix:
            if( !file.FullName.StartsWith( this.logDirectory, StringComparison.OrdinalIgnoreCase ) )
            {
                periodStart = null;
                sequenceNumber = null;
                return false;
            }

            string logDirectoryRelativeFilePath = file.FullName.Substring( startIndex: this.logDirectory.Length ); // `this.logDirectory` always has a trailing slash.

            // Don't use `Path.GetFileNameWithoutExtension( fileName );`, we want something like `Path.GetFullPathToFileWithoutExtension( fileName );`
            string pathWithoutExtension = PathUtility.GetFilePathWithoutExtension( logDirectoryRelativeFilePath );

            // If there is a sequence suffix, trim it so that `DateTime::TryParseExact` will still work:
            GetSequenceNumber( pathWithoutExtension, file.Extension, out string pathWithoutSequenceSuffix, out sequenceNumber );

            if( DateTime.TryParseExact( pathWithoutSequenceSuffix, this.filePathFormat, CultureInfo.InvariantCulture, style: DateTimeStyles.None, result: out DateTime periodStartValue ) )
            {
                periodStart = periodStartValue;
                return true;
            }
            else
            {
                periodStart = null;
            }

            return false;
        }

        static void GetSequenceNumber( string pathWithoutExtension, string ext, out string pathWithoutSequenceSuffix, out Int32? sequenceNumber )
        {
            // e.g. If fileNameFormat is "yyyy-MM\'Errors-'yyyy-MM-dd'.log'" then a possible file-name is "C:\logfiles\2019-02\Errors-2019-02-22_001.log", note the "_001" sequence-number inserted right before the extension.
            Match sequenceSuffixPatternMatch = _sequenceSuffixRegex.Match( pathWithoutExtension ); // The _sequenceSuffixRegex pattern has the '$' anchor so it will only match suffixes.
            if( sequenceSuffixPatternMatch.Success && sequenceSuffixPatternMatch.Groups.Count == 2 )
            {
                string wholeMatch     = sequenceSuffixPatternMatch.Groups[0].Value;
                string sequenceDigits = sequenceSuffixPatternMatch.Groups[1].Value;

                sequenceNumber = Int32.Parse( sequenceDigits, NumberStyles.Integer, CultureInfo.InvariantCulture );

                pathWithoutSequenceSuffix = pathWithoutExtension.Substring( 0, pathWithoutExtension.Length - wholeMatch.Length ) + ext;
            }
            else
            {
                sequenceNumber = null;

                pathWithoutSequenceSuffix = pathWithoutExtension + ext;
            }
        }
    }
}
