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

using Serilog.Events;
using Serilog.Formatting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Serilog.Sinks.File
{
    /// <summary>
    /// Write log events to a disk file.
    /// </summary>
    [Obsolete("This type will be removed from the public API in a future version; use `WriteTo.File()` instead.")]
    public sealed class FileSink : IFileSink, IDisposable
    {
        readonly TextWriter _output;
        readonly FileStream _underlyingStream;
        readonly ITextFormatter _textFormatter;
        readonly long? _fileSizeLimitBytes;
        readonly bool _buffered;
        readonly object _syncRoot = new object();
        readonly WriteCountingStream _countingStreamWrapper;
        readonly Func<IEnumerable<LogEvent>> _logFileHeaders;
        bool _appendHeaderLogs = false;

        /// <summary>Construct a <see cref="FileSink"/>.</summary>
        /// <param name="path">Path to the file.</param>
        /// <param name="textFormatter">Formatter used to convert log events to text.</param>
        /// <param name="fileSizeLimitBytes">The approximate maximum size, in bytes, to which a log file will be allowed to grow.
        /// For unrestricted growth, pass null. The default is 1 GB. To avoid writing partial events, the last event within the limit
        /// will be written in full even if it exceeds the limit.</param>
        /// <param name="encoding">Character encoding used to write the text file. The default is UTF-8 without BOM.</param>
        /// <param name="buffered">Indicates if flushing to the output file can be buffered or not. The default
        /// is false.</param>
        /// <param name="logFileHeaders">This action calls when a new log file created. It enables you to write any header text to the log file.</param>
        /// <returns>Configuration object allowing method chaining.</returns>
        /// <remarks>The file will be written using the UTF-8 character set.</remarks>
        /// <exception cref="IOException"></exception>
        public FileSink(string path, ITextFormatter textFormatter, long? fileSizeLimitBytes, Encoding encoding = null, bool buffered = false, Func<IEnumerable<LogEvent>> logFileHeaders = null)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (textFormatter == null) throw new ArgumentNullException(nameof(textFormatter));
            if (fileSizeLimitBytes.HasValue && fileSizeLimitBytes < 0) throw new ArgumentException("Negative value provided; file size limit must be non-negative.");

            _textFormatter = textFormatter;
            _fileSizeLimitBytes = fileSizeLimitBytes;
            _buffered = buffered;
            _logFileHeaders = logFileHeaders;

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (logFileHeaders != null && FileNotFoundOrEmpty(path))
                _appendHeaderLogs = true;

            Stream outputStream = _underlyingStream = System.IO.File.Open(path, FileMode.Append, FileAccess.Write, FileShare.Read);
            if (_fileSizeLimitBytes != null)
            {
                outputStream = _countingStreamWrapper = new WriteCountingStream(_underlyingStream);
            }

            _output = new StreamWriter(outputStream, encoding ?? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        bool FileNotFoundOrEmpty(string path)
        {
            var fileInfo = new FileInfo(path);

            return !fileInfo.Exists || fileInfo.Length == 0;
        }

        bool IFileSink.EmitOrOverflow(LogEvent logEvent)
        {
            if (logEvent == null) throw new ArgumentNullException(nameof(logEvent));
            lock (_syncRoot)
            {
                if (_fileSizeLimitBytes != null)
                {
                    if (_countingStreamWrapper.CountedLength >= _fileSizeLimitBytes.Value)
                        return false;
                }

                if (_appendHeaderLogs)
                {
                    var logFileHeaderCollection = _logFileHeaders.Invoke();

                    foreach (var item in logFileHeaderCollection)
                        AppendToOutput(item);

                    _appendHeaderLogs = false;
                }

                AppendToOutput(logEvent);

                return true;
            }
        }

        private void AppendToOutput(LogEvent logEvent)
        {
            _textFormatter.Format(logEvent, _output);
            if (!_buffered)
                _output.Flush();
        }

        /// <summary>
        /// Emit the provided log event to the sink.
        /// </summary>
        /// <param name="logEvent">The log event to write.</param>
        public void Emit(LogEvent logEvent)
        {
            ((IFileSink)this).EmitOrOverflow(logEvent);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            lock (_syncRoot)
            {
                _output.Dispose();
            }
        }

        /// <inheritdoc />
        public void FlushToDisk()
        {
            lock (_syncRoot)
            {
                _output.Flush();
                _underlyingStream.Flush(true);
            }
        }
    }
}
