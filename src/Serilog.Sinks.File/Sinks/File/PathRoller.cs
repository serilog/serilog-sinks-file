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

using System.Globalization;
using System.Text.RegularExpressions;

namespace Serilog.Sinks.File;

sealed class PathRoller
{
    const string PeriodMatchGroup = "period";
    const string SequenceNumberMatchGroup = "sequence";
    const string IntervalPathPatternMatcher = @"{interval}";
    const string SequenceNumberPathPatternMatcher = @"{sequence_number}";


    readonly string _directory;
    readonly string _filenamePrefix;
    readonly string _filenameSuffix;
    readonly Regex _filenameMatcher;

    readonly RollingInterval _interval;
    readonly string _periodFormat;
    readonly string _originalPath;

    public PathRoller(string path, RollingInterval interval)
    {
        _interval = interval;
        _originalPath = path ?? throw new ArgumentNullException(nameof(path));
        _periodFormat = interval.GetFormat();

        var pathDirectory = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(pathDirectory))
            pathDirectory = Directory.GetCurrentDirectory();

        _directory = Path.GetFullPath(pathDirectory);
        _filenamePrefix = Path.GetFileNameWithoutExtension(path);
        _filenameSuffix = Path.GetExtension(path);
        _filenameMatcher = new Regex(
            "^" +
            Regex.Escape(_filenamePrefix) +
            "(?<" + PeriodMatchGroup + ">\\d{" + _periodFormat.Length + "})" +
            "(?<" + SequenceNumberMatchGroup + ">_[0-9]{3,}){0,1}" +
            Regex.Escape(_filenameSuffix) +
            "$",
            RegexOptions.Compiled);

        DirectorySearchPattern = $"{_filenamePrefix}*{_filenameSuffix}";
    }

    public string LogFileDirectory => _directory;

    public string DirectorySearchPattern { get; }

    public void GetLogFilePath(DateTime date, int? sequenceNumber, out string path)
    {
        var currentCheckpoint = GetCurrentCheckpoint(date);

        var tok = currentCheckpoint?.ToString(_periodFormat, CultureInfo.InvariantCulture) ?? "";
        var sequenceNumberFormatted = sequenceNumber.HasValue
            ? sequenceNumber.Value.ToString("000", CultureInfo.InvariantCulture)
            : null;
        if (sequenceNumberFormatted != null)
            tok += "_" + sequenceNumberFormatted;

        if (TryGetPatternMatch(_originalPath, out var pattern))
        {
            path = GetPathForPattern(tok, sequenceNumberFormatted ?? "");
            return;
        }

        path = Path.Combine(_directory, _filenamePrefix + tok + _filenameSuffix);
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

    public bool TryGetPatternMatch(string path, out PathPatternType? patternType)
    {
        patternType = null;
        if (path.Contains(IntervalPathPatternMatcher) && path.Contains(SequenceNumberPathPatternMatcher))
        {
            patternType = PathPatternType.Both;
            return true;
        }

        if (path.Contains(IntervalPathPatternMatcher))
        {
            patternType = PathPatternType.Interval;
            return true;
        }

        if (path.Contains(SequenceNumberPathPatternMatcher))
        {
            patternType = PathPatternType.SequenceNumber;
            return true;
        }

        return false;
    }

    private string GetPathForPattern(string intervalToken,
        string sequenceNumber)
    {
        var newPrefix = _filenamePrefix.Replace(IntervalPathPatternMatcher, intervalToken).Replace(
            SequenceNumberPathPatternMatcher,
            sequenceNumber);

        return Path.Combine(_directory, newPrefix + _filenameSuffix);
    }
}
