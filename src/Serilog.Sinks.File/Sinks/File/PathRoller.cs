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
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Serilog.Sinks.File
{
    class PathRoller
    {
        const string PeriodMatchGroup = "period";
        const string SequenceNumberMatchGroup = "sequence";

        readonly string _directory;
        readonly string _filenameFormat;

        readonly Regex _filenameMatcher;

        readonly RollingInterval _interval;
        private readonly List<PathTokenizer.Token> _tokenized;
        readonly string _periodFormat;


        public PathRoller(string path, RollingInterval interval)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            _interval = interval;

            _tokenized = PathTokenizer.Tokenize(path);

            StringBuilder formatBuilder = new StringBuilder();

            var dateFound = false;
            var seqFound = false;

            _periodFormat = interval.GetFormat();

            var i = _tokenized.FindIndex(x => x.Type == PathTokenizer.Token.TokenType.Parameter);
            if (i < 0)
                i = _tokenized.Count - 1;

            var lastSeparator = _tokenized.FindLastIndex(i, x => x.Type == PathTokenizer.Token.TokenType.DirectorySeparator);

            string? pathDirectory = null;
            if (lastSeparator > 0)
                pathDirectory = Path.Combine(_tokenized.Take(lastSeparator).Where(x => x.Type == PathTokenizer.Token.TokenType.Text).Select(x => x.ToString()).ToArray());

            if (string.IsNullOrEmpty(pathDirectory))
                pathDirectory = Directory.GetCurrentDirectory();

            foreach (var t in _tokenized.Skip(lastSeparator+1))
            {
                if (t.Type == PathTokenizer.Token.TokenType.Extension)
                    break;

                if (t.Type == PathTokenizer.Token.TokenType.Parameter)
                {
                    if (t.Value.ToLower() == "date")
                    {
                        dateFound = true;
                        formatBuilder.Append("{0}");
                        if (t.Argument != null && t.Argument != string.Empty)
                            _periodFormat = t.Argument;
                    }
                    else if (t.Value.ToLower() == "seq")
                    {
                        seqFound = true;
                        formatBuilder.Append("{1}");
                    }
                    else
                        formatBuilder.Append(t.ToString()); 
                }
                else
                    formatBuilder.Append(t.ToString()); 
            }

            if (!dateFound) formatBuilder.Append("{0}");
            if (!seqFound) formatBuilder.Append("{1}");

            var ext = _tokenized.FirstOrDefault(x => x.Type == PathTokenizer.Token.TokenType.Extension);

            if (ext != null)
                formatBuilder.Append(ext.ToString());
            _filenameFormat = formatBuilder.ToString();

            _directory = Path.GetFullPath(pathDirectory);

            _filenameMatcher = new Regex(string.Format(_filenameFormat.Replace("\\", "\\\\"),
                "(?<" + PeriodMatchGroup + ">.{" + _periodFormat.Length + "})",
                "(?<" + SequenceNumberMatchGroup + ">_[0-9]{3,}){0,1}") + "$",
                RegexOptions.Compiled);

            //_filenameMatcher = new Regex(
            //    "^" +
            //    Regex.Escape(_filenamePrefix) +
            //    "(?<" + PeriodMatchGroup + ">\\d{" + _periodFormat.Length + "})" +
            //    "(?<" + SequenceNumberMatchGroup + ">_[0-9]{3,}){0,1}" +
            //    Regex.Escape(_filenameSuffix) +
            //    "$",
            //    RegexOptions.Compiled);

            DirectorySearchPattern = string.Format(_filenameFormat, "*", "*").Replace("**", "*");
        }

        public string LogFileDirectory => _directory;

        public string DirectorySearchPattern { get; }

        public void GetLogFilePath(DateTime date, int? sequenceNumber, out string path)
        {
            var currentCheckpoint = GetCurrentCheckpoint(date);

            var tok = currentCheckpoint?.ToString(_periodFormat, CultureInfo.InvariantCulture) ?? "";
            string seq = string.Empty;
            if (sequenceNumber != null)
                seq = "_" + sequenceNumber.Value.ToString("000", CultureInfo.InvariantCulture);

            path = Path.Combine(_directory, string.Format(_filenameFormat, tok, seq));
        }

        public IEnumerable<RollingLogFile> SelectMatches(IEnumerable<string> filenames)
        {
            foreach (var filename in filenames)
            {
                var match = _filenameMatcher.Match(filename);
                if (!match.Success)
                    continue;

                int? inc = null;
                var incGroup = match.Groups[SequenceNumberMatchGroup];
                if (incGroup.Captures.Count != 0)
                {
                    var incPart = incGroup.Captures[0].Value.Substring(1);
                    inc = int.Parse(incPart, CultureInfo.InvariantCulture);
                }

                DateTime? period = null;
                var periodGroup = match.Groups[PeriodMatchGroup];
                if (periodGroup.Captures.Count != 0)
                {
                    var dateTimePart = periodGroup.Captures[0].Value;
                    if (DateTime.TryParseExact(
                        dateTimePart,
                        _periodFormat,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var dateTime))
                    {
                        period = dateTime;
                    }
                }

                yield return new RollingLogFile(filename, period, inc);
            }
        }

        public DateTime? GetCurrentCheckpoint(DateTime instant) => _interval.GetCurrentCheckpoint(instant);

        public DateTime? GetNextCheckpoint(DateTime instant) => _interval.GetNextCheckpoint(instant);
    }
}
