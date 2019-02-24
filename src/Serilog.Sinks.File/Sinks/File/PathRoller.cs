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
        readonly IRollingFilePathProvider _pathProvider;

        /// <summary>Constructor for legacy consumers.</summary>
        public PathRoller(string path, RollingInterval interval)
            : this( path, new DefaultRollingFilePathProvider( interval, Path.GetFullPath( path ) ) )
        {
        }

        public PathRoller(string path, IRollingFilePathProvider pathProvider)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            this._pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));

            string logFileDirectory = Path.GetDirectoryName(path);
            if( string.IsNullOrEmpty( logFileDirectory ) ) logFileDirectory = Directory.GetCurrentDirectory();

            this.LogFileDirectory = Path.GetFullPath(logFileDirectory);
        }

        public string LogFileDirectory { get; }

        public string DirectorySearchPattern => this._pathProvider.DirectorySearchPattern;

        public void GetLogFilePath(DateTime date, int? sequenceNumber, out string path)
        {
            path = this._pathProvider.GetRollingLogFilePath( date, sequenceNumber );
        }

        /// <summary>Filters <paramref name="files"/> to only those files that match the current log file name format, then converts them into <see cref="RollingLogFile"/> instances.</summary>
        public IEnumerable<RollingLogFile> SelectMatches(IEnumerable<FileInfo> files)
        {
            foreach (FileInfo file in files)
            {
                if( this._pathProvider.MatchRollingLogFilePath( file, out DateTime? periodStart, out Int32? sequenceNumber ) )
                {
                    yield return new RollingLogFile( file, periodStart, sequenceNumber );
                }
            }
        }

        public DateTime? GetCurrentCheckpoint(DateTime instant) => this._pathProvider.Interval.GetCurrentCheckpoint(instant);

        public DateTime? GetNextCheckpoint   (DateTime instant) => this._pathProvider.Interval.GetNextCheckpoint(instant);
    }
}
