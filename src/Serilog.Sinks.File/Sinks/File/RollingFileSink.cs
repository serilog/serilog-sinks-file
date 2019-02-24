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

#pragma warning disable 618

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Formatting;

namespace Serilog.Sinks.File
{
    sealed class RollingFileSink : ILogEventSink, IFlushableFileSink, IDisposable
    {
        readonly PathRoller _roller;
        readonly ITextFormatter _textFormatter;
        readonly long? _fileSizeLimitBytes;
        readonly int? _retainedFileCountLimit;
        readonly Encoding _encoding;
        readonly bool _buffered;
        readonly bool _shared;
        readonly bool _rollOnFileSizeLimit;

        readonly object _syncRoot = new object();
        bool _isDisposed;
        DateTime? _nextCheckpoint;
        IFileSink _currentFile;
        int? _currentFileSequence;

        public RollingFileSink(PathRoller roller,
                              ITextFormatter textFormatter,
                              long? fileSizeLimitBytes,
                              int? retainedFileCountLimit,
                              Encoding encoding,
                              bool buffered,
                              bool shared,
                              bool rollOnFileSizeLimit)
        {
            if (roller == null) throw new ArgumentNullException(nameof(roller));
            if (fileSizeLimitBytes.HasValue && fileSizeLimitBytes < 0) throw new ArgumentException("Negative value provided; file size limit must be non-negative");
            if (retainedFileCountLimit.HasValue && retainedFileCountLimit < 1) throw new ArgumentException("Zero or negative value provided; retained file count limit must be at least 1");

            _roller = roller;
            _textFormatter = textFormatter;
            _fileSizeLimitBytes = fileSizeLimitBytes;
            _retainedFileCountLimit = retainedFileCountLimit;
            _encoding = encoding;
            _buffered = buffered;
            _shared = shared;
            _rollOnFileSizeLimit = rollOnFileSizeLimit;
        }

        public void Emit(LogEvent logEvent)
        {
            if (logEvent == null) throw new ArgumentNullException(nameof(logEvent));

            lock (_syncRoot)
            {
                if (_isDisposed) throw new ObjectDisposedException("The log file has been disposed.");

                var now = Clock.DateTimeNow;
                AlignCurrentFileTo(now);

                while (_currentFile?.EmitOrOverflow(logEvent) == false && _rollOnFileSizeLimit)
                {
                    AlignCurrentFileTo(now, nextSequence: true);
                }
            }
        }

        void AlignCurrentFileTo(DateTime now, bool nextSequence = false)
        {
            if (!_nextCheckpoint.HasValue)
            {
                OpenFile(now);
            }
            else if (nextSequence || now >= _nextCheckpoint.Value)
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

            // We only try periodically because repeated failures
            // to open log files REALLY slow an app down.
            _nextCheckpoint = _roller.GetNextCheckpoint(now) ?? now.AddMinutes(30);

            IEnumerable<FileInfo> existingFiles = Enumerable.Empty<FileInfo>();
            try
            {
                existingFiles = new DirectoryInfo( _roller.LogFileDirectory )
                    .GetFiles( _roller.DirectorySearchPattern );
                                       
            }
            catch (DirectoryNotFoundException) { }

            RollingLogFile latestForThisCheckpoint = _roller
                .SelectMatches(existingFiles)
                .Where(m => m.DateTime == currentCheckpoint)
                .OrderByDescending(m => m.SequenceNumber)
                .FirstOrDefault();

            var sequence = latestForThisCheckpoint?.SequenceNumber;
            if (minSequence != null)
            {
                if (sequence == null || sequence.Value < minSequence.Value)
                    sequence = minSequence;
            }

            const int maxAttempts = 3;
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                _roller.GetLogFilePath(now, sequence, out string path);

                try
                {
                    _currentFile = _shared ?
                        (IFileSink)new SharedFileSink(path, _textFormatter, _fileSizeLimitBytes, _encoding) :
                                   new FileSink      (path, _textFormatter, _fileSizeLimitBytes, _encoding, _buffered);

                    _currentFileSequence = sequence;
                }
                catch (IOException ex)
                {
                    if (IOErrors.IsLockedFile(ex))
                    {
                        SelfLog.WriteLine("File target {0} was locked, attempting to open next in sequence (attempt {1})", path, attempt + 1);
                        sequence = (sequence ?? 0) + 1;
                        continue;
                    }

                    throw;
                }

                ApplyRetentionPolicy(path);
                return;
            }
        }

        void ApplyRetentionPolicy(string currentFilePath)
        {
            if (_retainedFileCountLimit == null) return;

            FileInfo currentFile = new FileInfo( currentFilePath );

            // We consider the current file to exist, even if nothing's been written yet,
            // because files are only opened on response to an event being processed.

            // 1. Get files in the directory (and subdirectories) that match the current DirectorySearchPattern (which would select a superset of actual log files), also add `currentFilePath` too:
            // e.g. "\logs\log-20190222.log" and "\logs\log-2019-not-a-logfile-0222.log"
            IEnumerable<FileInfo> potentialMatches = new DirectoryInfo( _roller.LogFileDirectory )
                .GetFiles(_roller.DirectorySearchPattern, SearchOption.AllDirectories)
                .Union(new [] { currentFile }, comparer: FileInfoComparer.Instance);

            // 2. For each matched file, filter out to files that exactly match the current IRollingFilePathProvider's format, then put in descending chronological order.
            // e.g. "\logs\log-20190222.log"
            IEnumerable<FileInfo> newestFirst = _roller
                .SelectMatches(potentialMatches)
                .OrderByDescending(m => m.DateTime)
                .ThenByDescending(m => m.SequenceNumber)
                .Select(m => m.File);

            // 3. Delete all files after the retained file limit, *excluding* the file for `currentFile`.
            IEnumerable<FileInfo> toRemove = newestFirst
                .Where(n => !FileInfoComparer.Instance.Equals(currentFile, n))
                .Skip(_retainedFileCountLimit.Value - 1)
                .ToList();

            foreach (FileInfo obsoleteLogFile in toRemove)
            {
                try
                {
                   obsoleteLogFile.Delete();
                }
                catch (Exception ex)
                {
                    SelfLog.WriteLine("Error {0} while removing obsolete log file {1}", ex, obsoleteLogFile.FullName);
                }
            }
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
    }
}
