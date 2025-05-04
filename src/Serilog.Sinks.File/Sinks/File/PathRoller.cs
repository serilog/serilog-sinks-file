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

    readonly string _directory;
    readonly string _filenamePrefix;
    readonly string _filenameSuffix;
    readonly Regex _filenameMatcher;
    readonly bool _keepPathStatic;
    readonly string? _customRollPattern;
    private Func<DateTime?, string>? _customFormatFunc;

    readonly RollingInterval _interval;
    readonly string _periodFormat;

    public PathRoller(string path, RollingInterval interval, bool keepPathStatic = false, Func<DateTime?,string>? customFormatFunc = null,
        string? customRollPattern = null)
    {
        if (path == null) throw new ArgumentNullException(nameof(path));
        _interval = interval;
        _keepPathStatic = keepPathStatic;
        _customRollPattern = customRollPattern;
        _customFormatFunc = customFormatFunc;

        if (_customRollPattern != null && _customFormatFunc != null)
        {
            ValidateCustomRollPattern(_customRollPattern);
            ValidateCustomFormatFuncMatchesPattern(customFormatFunc, _customRollPattern);
        }



        var pathDirectory = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(pathDirectory))
            pathDirectory = Directory.GetCurrentDirectory();

        _directory = Path.GetFullPath(pathDirectory);
        _filenamePrefix = Path.GetFileNameWithoutExtension(path);
        _filenameSuffix = Path.GetExtension(path);
        if (_customRollPattern == null)
        {
            _periodFormat = interval.GetFormat();
            _filenameMatcher = new Regex(
                "^" +
                Regex.Escape(_filenamePrefix) +
                "(?<" + PeriodMatchGroup + ">\\d{" + _periodFormat.Length + "})" +
                "(?<" + SequenceNumberMatchGroup + ">_[0-9]{3,}){0,1}" +
                Regex.Escape(_filenameSuffix) +
                "$",
                RegexOptions.Compiled);
        }
        else
        {
            _periodFormat = _customRollPattern;
            _filenameMatcher = new Regex(
            "^" +
                Regex.Escape(_filenamePrefix) +
                "(?<" + PeriodMatchGroup + ">" + _customRollPattern + ")" +
                "(?<" + SequenceNumberMatchGroup + ">_[0-9]{3,}){0,1}" +
                Regex.Escape(_filenameSuffix) +
                "$",
            RegexOptions.Compiled);
        }

        DirectorySearchPattern = $"{_filenamePrefix}*{_filenameSuffix}";
    }

    private void ValidateCustomFormatFuncMatchesPattern(Func<DateTime?, string>? customFormatFunc, string customRollPattern)
    {
        var temp = customFormatFunc?.Invoke(DateTime.Now);
        if (temp == null)
        {
            throw new ArgumentException("Custom format function did not return a value.", nameof(customFormatFunc));
        }

        if (!Regex.IsMatch(temp, customRollPattern))
        {
            throw new ArgumentException($"Custom format function does not match the custom roll pattern of {customRollPattern}.", nameof(customFormatFunc));
        }

    }

    public string LogFileDirectory => _directory;

    public string DirectorySearchPattern { get; }

    public void GetLogFilePath(DateTime date, int? sequenceNumber, out string path, out string? copyPath)
    {
        var currentCheckpoint = GetCurrentCheckpoint(date);

        var tok = GetToken(sequenceNumber, currentCheckpoint);

        if (_keepPathStatic)
        {
            path = Path.Combine(_directory, _filenamePrefix + _filenameSuffix);

            copyPath = Path.Combine(_directory, _filenamePrefix + tok + _filenameSuffix);
            return;
        }

        copyPath = null;
        GetLogFilePath(date, sequenceNumber, out path);
    }

    private string GetToken(int? sequenceNumber, DateTime? currentCheckpoint)
    {
        var tok = string.Empty;

        if (_customFormatFunc == null)
        {
            tok = currentCheckpoint?.ToString(_periodFormat, CultureInfo.InvariantCulture) ?? "";
        }
        else if( _customFormatFunc != null && sequenceNumber == null && currentCheckpoint != null)
        {
            tok = _customFormatFunc.Invoke(currentCheckpoint);
        }
        else if( _customFormatFunc != null && sequenceNumber != null && currentCheckpoint == null)
        {
            tok = _customFormatFunc.Invoke(DateTime.Now);
        }
        else if (_customFormatFunc != null && sequenceNumber == null && currentCheckpoint == null)
        {
            return string.Empty;
        }

        if (sequenceNumber == null) return tok;
        var path = Path.Combine(_directory, _filenamePrefix + tok + _filenameSuffix);
        if (System.IO.File.Exists(path))
        {
            tok += "_" + sequenceNumber.Value.ToString("000", CultureInfo.InvariantCulture);
        }

        return tok;
    }

    public void GetLogFilePath(DateTime date, int? sequenceNumber, out string path)
    {
        var currentCheckpoint = GetCurrentCheckpoint(date);

        var tok = GetToken(sequenceNumber, currentCheckpoint);

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

    private void ValidateCustomRollPattern(string pattern)
    {
        try
        {
            _ = new Regex(pattern);
        }
        catch (ArgumentException)
        {
            throw new ArgumentException("The custom roll pattern is not a valid regex pattern.", nameof(pattern));
        }
    }
}
