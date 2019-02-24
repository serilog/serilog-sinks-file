// Copyright 2013-2016 Serilog Contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.IO;

namespace Serilog.Sinks.File
{
    class PathRoller
    {
        readonly IRollingFilePathProvider pathProvider;

        public static PathRoller CreateForFormattedPath( string path, RollingInterval interval )
        {
            IRollingFilePathProvider pathProvider = new FormattedRollingFilePathProvider( interval, Path.GetFullPath( path ) );

            return new PathRoller( path, pathProvider );
        }

        public static PathRoller CreateForLegacyPath( string path, RollingInterval interval )
        {
            IRollingFilePathProvider pathProvider = new DefaultRollingFilePathProvider( interval, Path.GetFullPath( path ) );

            return new PathRoller( path, pathProvider );
        }

        private PathRoller(string path, IRollingFilePathProvider pathProvider)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            this.pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));

            string logFileDirectory = Path.GetDirectoryName(path);
            if( string.IsNullOrEmpty( logFileDirectory ) ) logFileDirectory = Directory.GetCurrentDirectory();

            this.LogFileDirectory = Path.GetFullPath(logFileDirectory);
        }

        public string LogFileDirectory { get; }

        public string DirectorySearchPattern => this.pathProvider.DirectorySearchPattern;

        public void GetLogFilePath(DateTime date, int? sequenceNumber, out string path)
        {
            // The IRollingFilePathProvider will include the log directory path in the output file-name, so this method doesn't need to prefix `this.LogFileDirectory`.
            path = this.pathProvider.GetRollingLogFilePath( date, sequenceNumber );
        }

        /// <summary>Filters <paramref name="files"/> to only those files that match the current log file name format, then converts them into <see cref="RollingLogFile"/> instances.</summary>
        public IEnumerable<RollingLogFile> SelectMatches(IEnumerable<FileInfo> files)
        {
            foreach (FileInfo file in files)
            {
                if( this.pathProvider.MatchRollingLogFilePath( file, out DateTime? periodStart, out Int32? sequenceNumber ) )
                {
                    yield return new RollingLogFile( file, periodStart, sequenceNumber );
                }
            }
        }

        public DateTime? GetCurrentCheckpoint(DateTime instant) => this.pathProvider.Interval.GetCurrentCheckpoint(instant);

        public DateTime? GetNextCheckpoint   (DateTime instant) => this.pathProvider.Interval.GetNextCheckpoint(instant);
    }
}
