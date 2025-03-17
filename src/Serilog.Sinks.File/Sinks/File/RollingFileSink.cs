// Copyright 2013-2017 Serilog Contributors
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
using System.Text;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Formatting;

namespace Serilog.Sinks.File;

sealed class RollingFileSink : ILogEventSink, IFlushableFileSink, IDisposable, ISetLoggingFailureListener
{
    readonly PathRoller _roller;
    readonly ITextFormatter _textFormatter;
    readonly long? _fileSizeLimitBytes;
    readonly int? _retainedFileCountLimit;
    readonly TimeSpan? _retainedFileTimeLimit;
    readonly Encoding? _encoding;
    readonly bool _buffered;
    readonly bool _shared;
    readonly bool _rollOnFileSizeLimit;
    readonly bool _keepPathAsStaticFile;
    readonly FileLifecycleHooks? _hooks;
    readonly Func<DateTime?, string>? _customFormatFunc;

    ILoggingFailureListener _failureListener = SelfLog.FailureListener;

    readonly object _syncRoot = new();
    bool _isDisposed;
    DateTime? _nextCheckpoint;
    IFileSink? _currentFile;
    int? _currentFileSequence;

    public RollingFileSink(string path,
                          ITextFormatter textFormatter,
                          long? fileSizeLimitBytes,
                          int? retainedFileCountLimit,
                          Encoding? encoding,
                          bool buffered,
                          bool shared,
                          RollingInterval rollingInterval,
                          bool rollOnFileSizeLimit,
                          FileLifecycleHooks? hooks,
                          TimeSpan? retainedFileTimeLimit,
                          bool keepPathAsStaticFile,
                          Func<DateTime?, string>? customFormatFunc,
                          string? customRollPattern)
    {
        if (path == null) throw new ArgumentNullException(nameof(path));
        if (fileSizeLimitBytes is < 1) throw new ArgumentException("Invalid value provided; file size limit must be at least 1 byte, or null.");
        if (retainedFileCountLimit is < 1) throw new ArgumentException("Zero or negative value provided; retained file count limit must be at least 1.");
        if (retainedFileTimeLimit.HasValue && retainedFileTimeLimit < TimeSpan.Zero) throw new ArgumentException("Negative value provided; retained file time limit must be non-negative.", nameof(retainedFileTimeLimit));
        if(customFormatFunc != null && customRollPattern == null) throw new ArgumentException("When Supplying a Custom Format Function, a Custom Roll Pattern must also be supplied.");

        _roller = new PathRoller(path, rollingInterval, keepPathAsStaticFile, customFormatFunc, customRollPattern);
        _textFormatter = textFormatter;
        _fileSizeLimitBytes = fileSizeLimitBytes;
        _retainedFileCountLimit = retainedFileCountLimit;
        _retainedFileTimeLimit = retainedFileTimeLimit;
        _encoding = encoding;
        _buffered = buffered;
        _shared = shared;
        _rollOnFileSizeLimit = rollOnFileSizeLimit;
        _hooks = hooks;
        _keepPathAsStaticFile = keepPathAsStaticFile;
        _customFormatFunc = customFormatFunc;
    }

    public void Emit(LogEvent logEvent)
    {
        if (logEvent == null) throw new ArgumentNullException(nameof(logEvent));

        bool failed;
        lock (_syncRoot)
        {
            if (_isDisposed) throw new ObjectDisposedException("The log file has been disposed.");

            var now = Clock.DateTimeNow;
            AlignCurrentFileTo(now);

            while (_currentFile?.EmitOrOverflow(logEvent) == false && _rollOnFileSizeLimit)
            {
                AlignCurrentFileTo(now, nextSequence: true);
            }

            failed = _currentFile == null;
        }

        if (failed)
        {
            // Support fallback chains without the overhead of throwing an exception.
            _failureListener.OnLoggingFailed(
                this,
                LoggingFailureKind.Permanent,
                "the target file could not be opened or created",
                [logEvent],
                exception: null);
        }
    }

    void AlignCurrentFileTo(DateTime now, bool nextSequence = false)
    {
        if (_currentFile == null && !_nextCheckpoint.HasValue)
        {
            OpenFile(now);
        }
        else if (nextSequence || (_nextCheckpoint.HasValue && now >= _nextCheckpoint.Value))
        {
            int? minSequence = null;
            if (nextSequence)
            {
                if (_currentFileSequence == null)
                    minSequence = 1;
                else
                    minSequence = _currentFileSequence.Value + 1;
            }

            CloseFile();
            OpenFile(now, minSequence);
        }
    }

    void OpenFile(DateTime now, int? minSequence = null)
    {
        var currentCheckpoint = _roller.GetCurrentCheckpoint(now);

        _nextCheckpoint = _roller.GetNextCheckpoint(now);

        try
        {
            var existingFiles = Enumerable.Empty<string>();
            try
            {
                if (Directory.Exists(_roller.LogFileDirectory))
                {
                    // ReSharper disable once ConvertClosureToMethodGroup
                    existingFiles = Directory.GetFiles(_roller.LogFileDirectory, _roller.DirectorySearchPattern)
                        .Select(f => Path.GetFileName(f));
                }
            }
            catch (DirectoryNotFoundException)
            {
            }

            var latestForThisCheckpoint = _roller
                .SelectMatches(existingFiles)
                .Where(m => m.DateTime == currentCheckpoint)
#if ENUMERABLE_MAXBY
            .MaxBy(m => m.SequenceNumber);
#else
                .OrderByDescending(m => m.SequenceNumber)
                .FirstOrDefault();
#endif

            var sequence = latestForThisCheckpoint?.SequenceNumber;
            if (minSequence != null)
            {
                if (sequence == null || sequence.Value < minSequence.Value)
                    sequence = minSequence;
            }

            if (sequence != null)
            {
                _roller.GetLogFilePath(now, sequence, out var p, out var c);
                switch (_keepPathAsStaticFile)
                {
                    // var path = Path.Combine(_roller.LogFileDirectory, $"{_roller.PathRollerPrefix}{_customFormatFunc?.Invoke()}_{sequence.Value.ToString("000", CultureInfo.InvariantCulture)}{_roller.DirectorySearchPattern}");
                    case true when System.IO.File.Exists(c):
                        sequence++;
                        break;
                    case false when _customFormatFunc != null && !System.IO.File.Exists(p):
                        sequence = 1;
                        break;
                }
            }

            const int maxAttempts = 3;
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                string path;
                if (_keepPathAsStaticFile)
                {
                    _roller.GetLogFilePath(now, sequence, out path, out var copyPath);
                    if (copyPath != null && System.IO.File.Exists(path))
                    {
                        System.IO.File.Copy(path, copyPath);
                    }
                }
                else
                {
                    _roller.GetLogFilePath(now, sequence, out path);
                }

                try
                {
                    _currentFile = _shared
                        ?
#pragma warning disable 618
                        new SharedFileSink(path, _textFormatter, _fileSizeLimitBytes, _encoding)
                        :
#pragma warning restore 618
                        new FileSink(path, _textFormatter, _fileSizeLimitBytes, _encoding, _buffered, _hooks, _keepPathAsStaticFile);

                    _currentFileSequence = sequence;

                    if (_currentFile is ISetLoggingFailureListener setLoggingFailureListener)
                    {
                        setLoggingFailureListener.SetFailureListener(_failureListener);
                    }
                }
                catch (IOException ex)
                {
                    if (IOErrors.IsLockedFile(ex))
                    {
                        _failureListener.OnLoggingFailed(
                            this,
                            LoggingFailureKind.Temporary,
                            $"file target {path} was locked, attempting to open next in sequence (attempt {attempt + 1})",
                            events: null,
                            exception: null);
                        sequence = (sequence ?? 0) + 1;
                        continue;
                    }

                    throw;
                }

                ApplyRetentionPolicy(path, now);
                return;
            }
        }
        finally
        {
            if (_currentFile == null)
            {
                // We only try periodically because repeated failures
                // to open log files REALLY slow an app down.
                // If the next checkpoint would be earlier, keep it!
                var retryCheckpoint = now.AddMinutes(30);
                if (_nextCheckpoint == null || retryCheckpoint < _nextCheckpoint)
                {
                    _nextCheckpoint = retryCheckpoint;
                }
            }
        }
    }

    void ApplyRetentionPolicy(string currentFilePath, DateTime now)
    {
        if (_retainedFileCountLimit == null && _retainedFileTimeLimit == null) return;

        var currentFileName = Path.GetFileName(currentFilePath);

        // We consider the current file to exist, even if nothing's been written yet,
        // because files are only opened on response to an event being processed.
        // ReSharper disable once ConvertClosureToMethodGroup
        var potentialMatches = Directory.GetFiles(_roller.LogFileDirectory, _roller.DirectorySearchPattern)
            .Select(f => Path.GetFileName(f))
            .Union([currentFileName]);

        var newestFirst = _roller
            .SelectMatches(potentialMatches)
            .OrderByDescending(m => m.DateTime)
            .ThenByDescending(m => m.SequenceNumber);

        var toRemove = newestFirst
            .Where(n => StringComparer.OrdinalIgnoreCase.Compare(currentFileName, n.Filename) != 0)
            .SkipWhile((f, i) => ShouldRetainFile(f, i, now))
            .Select(x => x.Filename)
            .ToList();

        foreach (var obsolete in toRemove)
        {
            var fullPath = Path.Combine(_roller.LogFileDirectory, obsolete);
            try
            {
                _hooks?.OnFileDeleting(fullPath);
                System.IO.File.Delete(fullPath);
            }
            catch (Exception ex)
            {
                _failureListener.OnLoggingFailed(
                    this,
                    LoggingFailureKind.Temporary,
                    $"error while processing obsolete log file {fullPath}",
                    events: null,
                    ex);
            }
        }
    }

    bool ShouldRetainFile(RollingLogFile file, int index, DateTime now)
    {
        if (_retainedFileCountLimit.HasValue && index >= _retainedFileCountLimit.Value - 1)
            return false;

        if (_retainedFileTimeLimit.HasValue && file.DateTime.HasValue &&
            file.DateTime.Value < now.Subtract(_retainedFileTimeLimit.Value))
        {
            return false;
        }

        return true;
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_currentFile == null) return;
            CloseFile();
            _isDisposed = true;
        }
    }

    void CloseFile()
    {
        if (_currentFile != null)
        {
            (_currentFile as IDisposable)?.Dispose();
            _currentFile = null;
        }

        _nextCheckpoint = null;
    }

    public void FlushToDisk()
    {
        lock (_syncRoot)
        {
            _currentFile?.FlushToDisk();
        }
    }

    public void SetFailureListener(ILoggingFailureListener failureListener)
    {
        _failureListener = failureListener ?? throw new ArgumentNullException(nameof(failureListener));
    }
}
