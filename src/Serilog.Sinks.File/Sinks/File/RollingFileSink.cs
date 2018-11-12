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
        readonly CompressionType _compressionType;    

        readonly object _syncRoot = new object();
        bool _isDisposed;
        DateTime? _nextCheckpoint;
        IFileSink _currentFile;
        int? _currentFileSequence;     

        public RollingFileSink(string path,
                              ITextFormatter textFormatter,
                              long? fileSizeLimitBytes,
                              int? retainedFileCountLimit,
                              Encoding encoding,
                              bool buffered,
                              bool shared,
                              RollingInterval rollingInterval,
                              bool rollOnFileSizeLimit,
                              CompressionType compressionType
                              )
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (fileSizeLimitBytes.HasValue && fileSizeLimitBytes < 0) throw new ArgumentException("Negative value provided; file size limit must be non-negative");
            if (retainedFileCountLimit.HasValue && retainedFileCountLimit < 1) throw new ArgumentException("Zero or negative value provided; retained file count limit must be at least 1");

            _roller = new PathRoller(path, rollingInterval);
            _compressionType = compressionType;
            _textFormatter = textFormatter;
            _fileSizeLimitBytes = fileSizeLimitBytes;
            _retainedFileCountLimit = retainedFileCountLimit;
            _encoding = encoding;
            _buffered = buffered;
            _shared = shared;
            _rollOnFileSizeLimit = rollOnFileSizeLimit;
        }

        public void Compress(string logFile, string logDirectory, CompressionType compressionType)
        {     
            var readDirectory = Path.Combine(logDirectory, "new_dir");

            switch (compressionType)
            {
                case CompressionType.Zip:
                    ZipCompress(logFile, logDirectory, readDirectory);
                    break;
                case CompressionType.GZip:
                    GZipCompress(logFile, logDirectory);
                    break;
                case CompressionType.None:
                    throw new Exception("Compression type entered incorrectly or not supported.\n");
            }
        }

        public void ZipCompress(string prevLog, string logDirectory, string readDirectory)
        {
            Directory.CreateDirectory(readDirectory);
            System.IO.File.Move(
                Path.Combine(logDirectory, prevLog), Path.Combine(readDirectory, prevLog)
                );

            var zipName = prevLog.Remove(prevLog.Length - 4);
            var zip_path = Path.Combine(logDirectory, $"{zipName}-Zip.zip");
            System.IO.Compression.ZipFile.CreateFromDirectory(readDirectory, zip_path);

            Directory.Delete(readDirectory, true);
        }

        public void GZipCompress(string prevLog, string logDirectory)
        {
            var logPath = Path.Combine(logDirectory, prevLog);
            byte[] byteArray;
            using (FileStream prevLogStream = new FileStream(logPath, FileMode.Open))
            {
                byteArray = new byte[prevLogStream.Length];
                prevLogStream.Read(byteArray, 0, (int)prevLogStream.Length);
            }

            var logName = prevLog.Remove(prevLog.Length - 4);
            var GZipPath = Path.Combine(logDirectory, $"{logName}-GZip.gz");

            using (FileStream outFile = new FileStream(GZipPath, FileMode.Create))
            using (System.IO.Compression.GZipStream gzipStream = new System.IO.Compression.GZipStream(outFile, System.IO.Compression.CompressionMode.Compress, false))
            {
                gzipStream.Write(byteArray, 0, byteArray.Length);
            }

            System.IO.File.Delete(logPath);
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

                var prevLog = Directory.GetFiles(_roller.LogFileDirectory, _roller.DirectorySearchPattern)
                                         .Select(Path.GetFileName).LastOrDefault();
                var logDirectory = _roller.LogFileDirectory;          

                CloseFile();

                if (_compressionType != CompressionType.None)
                {
                    Compress(prevLog, logDirectory, _compressionType);

                    var directoryFiles = Directory.GetFiles(_roller.LogFileDirectory, _roller.DirectorySearchPattern);

                    foreach (var directoryFile in directoryFiles)
                    {
                        var directoryFileName = Path.GetFileName(directoryFile);

                        if (!(directoryFileName.Contains("-GZip")))
                        {
                            Compress(directoryFileName, logDirectory, _compressionType);
                        }
                    }

                }

                OpenFile(now, minSequence);
            }
        }

        void OpenFile(DateTime now, int? minSequence = null)
        {
            var currentCheckpoint = _roller.GetCurrentCheckpoint(now);

            _nextCheckpoint = _roller.GetNextCheckpoint(now) ?? now.AddMinutes(30);

            var existingFiles = Enumerable.Empty<string>();
            try
            {
                existingFiles = Directory.GetFiles(_roller.LogFileDirectory, _roller.DirectorySearchPattern)
                                         .Select(Path.GetFileName);
            }
            catch (DirectoryNotFoundException) { }

            var latestForThisCheckpoint = _roller
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
                _roller.GetLogFilePath(now, sequence, out var path);

                try
                {
                    _currentFile = _shared ?
                        (IFileSink)new SharedFileSink(path, _textFormatter, _fileSizeLimitBytes, _encoding) :
                        new FileSink(path, _textFormatter, _fileSizeLimitBytes, _encoding, _buffered);
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

            var currentFileName = Path.GetFileName(currentFilePath);

            // We consider the current file to exist, even if nothing's been written yet,
            // because files are only opened on response to an event being processed.
            var potentialMatches = Directory.GetFiles(_roller.LogFileDirectory, _roller.DirectorySearchPattern)
                .Select(Path.GetFileName)
                .Union(new [] { currentFileName });

            var newestFirst = _roller
                .SelectMatches(potentialMatches)
                .OrderByDescending(m => m.DateTime)
                .ThenByDescending(m => m.SequenceNumber)
                .Select(m => m.Filename);

            var toRemove = newestFirst
                .Where(n => StringComparer.OrdinalIgnoreCase.Compare(currentFileName, n) != 0)
                .Skip(_retainedFileCountLimit.Value - 1)
                .ToList();

            foreach (var obsolete in toRemove)
            {
                var fullPath = Path.Combine(_roller.LogFileDirectory, obsolete);
                try
                {
                    System.IO.File.Delete(fullPath);
                }
                catch (Exception ex)
                {
                    SelfLog.WriteLine("Error {0} while removing obsolete log file {1}", ex, fullPath);
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
