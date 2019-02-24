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
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace Serilog.Sinks.File
{
    class PathRoller
    {
        readonly IRollingFilePathProvider _pathProvider;

        /// <summary>Constructor for legacy consumers.</summary>
        public PathRoller(string path, RollingInterval interval)
            : this( path, new DefaultRollingFilePathProvider( interval, path ) )
        {
        }

        public PathRoller(string path, IRollingFilePathProvider pathProvider)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));

            string pathDirectory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(pathDirectory))
                pathDirectory = Directory.GetCurrentDirectory();

            this.LogFileDirectory = Path.GetFullPath(pathDirectory);
        }

        public string LogFileDirectory { get; }

        public string DirectorySearchPattern => this._pathProvider.DirectorySearchPattern;

        public void GetLogFilePath(DateTime date, int? sequenceNumber, out string path)
        {
            path = this._pathProvider.GetRollingLogFilePath( date, sequenceNumber );
        }

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
