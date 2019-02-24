using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace Serilog.Sinks.File
{
    /// <summary>Implements <see cref="IRollingFilePathProvider"/> with the default behaviour for Serilog Rolling-files as of version 4.0.0. This implementation uses hard-coded, fixed-length date formats that are appended to the configured file-name before the file extension.</summary>
    class DefaultRollingFilePathProvider : IRollingFilePathProvider
    {
        const string PeriodMatchGroup         = "period";
        const string SequenceNumberMatchGroup = "sequence";

        readonly string fileNamePrefix; // todo: rename filePathPrefix
        readonly string fileNameSuffix;
        readonly string periodFormat;
        readonly Regex  fileNameRegex;

        readonly string filePathFormat;

        /// <param name="logFilePath">Path to the log file. The RollingInterval and sequence number (if applicable) will be inserted before the file-name extension.</param>
        /// <param name="interval">Interval from which to generate file names.</param>
        public DefaultRollingFilePathProvider(RollingInterval interval, string logFilePath)
        {
            this.Interval       = interval;

            this.fileNamePrefix = PathUtility.GetFilePathWithoutExtension( logFilePath );
            this.fileNameSuffix = Path.GetExtension( logFilePath );
            this.periodFormat   = RollingIntervalExtensions.GetFormat( interval );

            // NOTE: This technique using Regex only works reliably if periodFormat will always generate output of the same length as periodFormat itself.
            // So "yyyy-MM-dd" is okay (as 'yyyy' is always 4, 'MM' and 'dd' are always 2)
            // But "yyyyy MMMM h" isn't, because 'MMMM' could be "May" or "August" and 'h' could be "1" or "12".

            // TODO: Consider validating `periodFormat` by throwing ArgumentException or FormatException if it contains any variable-length DateTime specifiers?

            // e.g. "^fileNamePrefix(?<period>\d{8})(?<sequence>_([0-9]){3,}){0,1}fileNameSuffix$" would match "filename20190222_001fileNameSuffix"
            String pattern = "^" + Regex.Escape( fileNamePrefix ) + "(?<" + PeriodMatchGroup + ">\\d{" + periodFormat.Length + "})" + "(?<" + SequenceNumberMatchGroup + ">_[0-9]{3,}){0,1}" +  Regex.Escape( fileNameSuffix ) + "$";

            this.fileNameRegex = new Regex( pattern, RegexOptions.Compiled );

            this.filePathFormat = "{0}{1:" + periodFormat + "}{2:'_'000}{3}"; // e.g. "{0}{1:yyyyMMdd}{2:'_'000}{3}" to render as "C:\logs\File20190222_001.log".

            this.DirectorySearchPattern = this.fileNamePrefix + "*" + this.fileNameSuffix;
        }

        public RollingInterval Interval { get; }

        public string DirectorySearchPattern { get; }

        public String GetRollingLogFilePath( DateTime instant, Int32? sequenceNumber )
        {
            DateTime? periodStart = this.Interval.GetCurrentCheckpoint( instant );

            return String.Format( CultureInfo.InvariantCulture, this.filePathFormat, this.fileNamePrefix, periodStart, sequenceNumber, this.fileNameSuffix );
        }

        public Boolean MatchRollingLogFilePath( FileInfo file, out DateTime? periodStart, out Int32? sequenceNumber )
        {
            periodStart    = null;
            sequenceNumber = null;

            Match match = this.fileNameRegex.Match( file.FullName );
            if( !match.Success )
            {
                return false;
            }

            Group periodGrp = match.Groups[ PeriodMatchGroup ];
            if( periodGrp.Success )
            {
                String periodText = periodGrp.Captures[0].Value;

                if( DateTime.TryParseExact( periodText, this.periodFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime periodStartValue ) )
                {
                    periodStart = periodStartValue;
                }
                else
                {
                    // This should never happen.
                }
            }

            Group sequenceGrp = match.Groups[ SequenceNumberMatchGroup ];
            if( sequenceGrp.Success )
            {
                String sequenceText = sequenceGrp.Captures[0].Value.Substring( startIndex: 1 );
                if( Int32.TryParse( sequenceText, NumberStyles.Integer, CultureInfo.InvariantCulture, out Int32 sequenceNumberValue ) )
                {
                    sequenceNumber = sequenceNumberValue;
                }
                else
                {
                    // This should never happen.
                }
            }

            return true;
        }
    }

    
}
