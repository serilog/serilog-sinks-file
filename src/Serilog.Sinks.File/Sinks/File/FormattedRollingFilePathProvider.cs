using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace Serilog.Sinks.File
{
    /// <summary>Implements <see cref="IRollingFilePathProvider"/> around a Custom .NET DateTime format string which should (but is not required to) contain the file-name extension as an enquoted literal. Any sequence numbers are inserted before the file-name extension with a leading underscore '_' character.</summary>
    class FormattedRollingFilePathProvider : IRollingFilePathProvider
    {
        readonly string filePathFormat;

        static readonly Regex _sequenceSuffixRegex = new Regex( @"_([0-9]{3,})$", RegexOptions.Compiled ); // Matches "_000", "_001", "_999", "_1000", "_999999", but not "_", "_0", "_a", "_99", etc. Requiring 3 digits avoids matching "_dd", "_mm" in a file-name.

        public FormattedRollingFilePathProvider( RollingInterval interval, string filePathFormat )
        {
            const string DefaultMessage = "The rolling file name format is invalid. ";

            this.Interval = interval;

            this.filePathFormat = this.filePathFormat ?? throw new ArgumentNullException( nameof( FormattedRollingFilePathProvider.filePathFormat) );

            if( !Path.IsPathRooted( filePathFormat ) ) throw new ArgumentException( message: "Path format must be absolute.", paramName: nameof(filePathFormat) );

            // Test the format before using it:
            // Also use the rendered string to get any prefix and file-name extensions for generating a glob pattern.
            try
            {
                string formatted = DateTime.MaxValue.ToString( this.filePathFormat, CultureInfo.InvariantCulture );
                DateTime parsed = DateTime.ParseExact( formatted, this.filePathFormat, CultureInfo.InvariantCulture );
                if( DateTime.MaxValue != parsed )
                {
                    throw new ArgumentException( DefaultMessage + "The format does not round-trip DateTime values correctly." );
                }

                // Also do an early check for invalid file-name characters, e.g. ':' or '/', but do allow "\" in the case user wants to split logs between directories.
                if( formatted.IndexOfAny( anyOf: new[] { ':', '/' } ) > -1 )
                {
                    throw new ArgumentException( DefaultMessage + "The format generates file-names that contain illegal characters, such as ':' or '/'. not round-trip DateTime values correctly." );
                }

                // If the generated file-name extension does not contain any digits then we can assume it's a static textual extension.
                // This will break if the file-name extension contains some alphabetic DateTime format specifier, of course.

                string globPrefix = null;
                string globSuffix = Path.GetExtension( formatted );

                {
                    string exampleFileName = Path.GetFileNameWithoutExtension( formatted );

                    Int32 firstNonLetterCharIdx = -1;
                    for( Int32 i = 0; i < exampleFileName.Length; i++ )
                    {
                        if( !Char.IsLetter( exampleFileName[i] ) )
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

                if( !String.IsNullOrEmpty( globPrefix ) || !String.IsNullOrEmpty( globSuffix ) )
                {
                    this.DirectorySearchPattern = globPrefix + "*" + globSuffix;
                }
            }
            catch( ArgumentException argEx )
            {
                throw new ArgumentException( DefaultMessage + "See the inner ArgumentException for details.", argEx );
            }
            catch( FormatException formatEx )
            {
                throw new ArgumentException( DefaultMessage + "See the inner FormatException for details.", formatEx );
            }
        }

        public RollingInterval Interval { get; }

        public string DirectorySearchPattern { get; }

        public string GetRollingLogFilePath( DateTime instant, Int32? sequenceNumber )
        {
            // Get period-start for the given point-in-time instant based on the interval:
            // e.g. if `instant == 2019-02-22 23:59:59` and `interval == Month`, then use `2019-02-01 00:00:00`.
            // This is to ensure that if the format string "yyyy-MM-dd HH:mm'.log" is used with a Monthly interval, for example, the dd, HH, and mm components will be normalized.

            String filePath;
            DateTime? periodStart = this.Interval.GetCurrentCheckpoint( instant );
            if( periodStart == null || this.Interval == RollingInterval.Infinite )
            {
                // i.e. Interval == Infinite. This should never happen (as it would use non-rolling File sinks).
                filePath = DateTime.MinValue.ToString( this.filePathFormat, CultureInfo.InvariantCulture );
            }
            else
            {
                filePath = periodStart.Value.ToString( this.filePathFormat, CultureInfo.InvariantCulture );
            }

            if( sequenceNumber != null )
            {
                // Insert the sequence number immediately before the extension.
                filePath = PathUtility.GetFilePathWithoutExtension( filePath ) + "_" + sequenceNumber.Value.ToString( "000", CultureInfo.InvariantCulture ) + Path.GetExtension( filePath );
            }

            return filePath;
        }

        public Boolean MatchRollingLogFilePath( FileInfo file, out DateTime? periodStart, out Int32? sequenceNumber )
        {
            if( file == null ) throw new ArgumentNullException(nameof(file));

            // If there is a sequence suffix, trim it so that `DateTime::TryParseExact` will still work:
            GetSequenceNumber( file, out string pathWithoutSequenceSuffix, out sequenceNumber );

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

        static void GetSequenceNumber( FileInfo file, out string pathWithoutSequenceSuffix, out Int32? sequenceNumber )
        {
            // e.g. If fileNameFormat is "yyyy-MM\'Errors-'yyyy-MM-dd'.log'" then a possible file-name is "C:\logfiles\2019-02\Errors-2019-02-22_001.log", note the "_001" sequence-number inserted right before the extension.

            // Don't use `Path.GetFileNameWithoutExtension( fileName );`, we want something like `Path.GetFullPathToFileWithoutExtension( fileName );`
            string pathWithoutExtension = PathUtility.GetFilePathWithoutExtension( file.FullName );

            Match sequenceSuffixPatternMatch = _sequenceSuffixRegex.Match( pathWithoutExtension ); // The _sequenceSuffixRegex pattern has the '$' anchor so it will only match suffixes.
            if( sequenceSuffixPatternMatch.Success && sequenceSuffixPatternMatch.Groups.Count == 2 )
            {
                string wholeMatch     = sequenceSuffixPatternMatch.Groups[0].Value;
                string sequenceDigits = sequenceSuffixPatternMatch.Groups[1].Value;

                sequenceNumber = Int32.Parse( sequenceDigits, NumberStyles.Integer, CultureInfo.InvariantCulture );

                pathWithoutSequenceSuffix = pathWithoutExtension.Substring( 0, pathWithoutExtension.Length - wholeMatch.Length ) + file.Extension;
            }
            else
            {
                sequenceNumber = null;

                pathWithoutSequenceSuffix = file.FullName;
            }
        }
    }
}
