﻿// Copyright 2013-2019 Serilog Contributors
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

#if ATOMIC_APPEND

using System.Security.AccessControl;
using System.Text;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Formatting;

namespace Serilog.Sinks.File;

/// <summary>
/// Write log events to a disk file.
/// </summary>
[Obsolete("This type will be removed from the public API in a future version; use `WriteTo.File(shared: true)` instead.")]
public sealed class SharedFileSink : IFileSink, IDisposable, ISetLoggingFailureListener
{
    readonly MemoryStream _writeBuffer;
    readonly string _path;
    readonly TextWriter _output;
    readonly ITextFormatter _textFormatter;
    readonly long? _fileSizeLimitBytes;
    readonly object _syncRoot = new();

    ILoggingFailureListener _failureListener = SelfLog.FailureListener;

    // The stream is reopened with a larger buffer if atomic writes beyond the current buffer size are needed.
    FileStream _fileOutput;
    int _fileStreamBufferLength = DefaultFileStreamBufferLength;

    const int DefaultFileStreamBufferLength = 4096;

    /// <summary>Construct a <see cref="FileSink"/>.</summary>
    /// <param name="path">Path to the file.</param>
    /// <param name="textFormatter">Formatter used to convert log events to text.</param>
    /// <param name="fileSizeLimitBytes">The approximate maximum size, in bytes, to which a log file will be allowed to grow.
    /// For unrestricted growth, pass null. The default is 1 GB. To avoid writing partial events, the last event within the limit
    /// will be written in full even if it exceeds the limit.</param>
    /// <param name="encoding">Character encoding used to write the text file. The default is UTF-8 without BOM.</param>
    /// <returns>Configuration object allowing method chaining.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="path"/> is <code>null</code></exception>
    /// <exception cref="ArgumentNullException">When <paramref name="textFormatter"/> is <code>null</code></exception>
    /// <exception cref="IOException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    /// <exception cref="NotSupportedException"></exception>
    /// <exception cref="PathTooLongException">When <paramref name="path"/> is too long</exception>
    /// <exception cref="UnauthorizedAccessException">The caller does not have the required permission to access the <paramref name="path"/></exception>
    /// <exception cref="ArgumentException">Invalid <paramref name="path"/></exception>
    public SharedFileSink(string path, ITextFormatter textFormatter, long? fileSizeLimitBytes, Encoding? encoding = null)
    {
        if (fileSizeLimitBytes.HasValue && fileSizeLimitBytes < 1)
            throw new ArgumentException("Invalid value provided; file size limit must be at least 1 byte, or null.");

        _path = path ?? throw new ArgumentNullException(nameof(path));
        _textFormatter = textFormatter ?? throw new ArgumentNullException(nameof(textFormatter));
        _fileSizeLimitBytes = fileSizeLimitBytes;

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // FileSystemRights.AppendData sets the Win32 FILE_APPEND_DATA flag. On Linux this is O_APPEND, but that API is not yet
        // exposed by .NET Core.
        _fileOutput = new FileStream(
            path,
            FileMode.Append,
            FileSystemRights.AppendData,
            FileShare.ReadWrite,
            _fileStreamBufferLength,
            FileOptions.None);

        _writeBuffer = new MemoryStream();
        _output = new StreamWriter(_writeBuffer,
            encoding ?? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    bool IFileSink.EmitOrOverflow(LogEvent logEvent)
    {
        if (logEvent == null) throw new ArgumentNullException(nameof(logEvent));

        lock (_syncRoot)
        {
            try
            {
                _textFormatter.Format(logEvent, _output);
                _output.Flush();
                var bytes = _writeBuffer.GetBuffer();
                var length = (int) _writeBuffer.Length;
                if (length > _fileStreamBufferLength)
                {
                    var oldOutput = _fileOutput;

                    _fileOutput = new FileStream(
                        _path,
                        FileMode.Append,
                        FileSystemRights.AppendData,
                        FileShare.ReadWrite,
                        length,
                        FileOptions.None);
                    _fileStreamBufferLength = length;

                    oldOutput.Dispose();
                }

                if (_fileSizeLimitBytes != null)
                {
                    try
                    {
                        if (_fileOutput.Length >= _fileSizeLimitBytes.Value)
                            return false;
                    }
                    catch (FileNotFoundException) { } // Cheaper and more reliable than checking existence
                }

                _fileOutput.Write(bytes, 0, length);
                _fileOutput.Flush();
                return true;
            }
            catch
            {
                // Make sure there's no leftover cruft in there.
                _output.Flush();
                throw;
            }
            finally
            {
                _writeBuffer.Position = 0;
                _writeBuffer.SetLength(0);
            }
        }
    }

    /// <summary>
    /// Emit the provided log event to the sink.
    /// </summary>
    /// <param name="logEvent">The log event to write.</param>
    /// <exception cref="ArgumentNullException">When <paramref name="logEvent"/> is <code>null</code></exception>
    public void Emit(LogEvent logEvent)
    {
        if (!((IFileSink)this).EmitOrOverflow(logEvent))
        {
            // Support fallback chains without the overhead of throwing an exception.
            _failureListener.OnLoggingFailed(
                this,
                LoggingFailureKind.Permanent,
                "the log file size limit has been reached and no rolling behavior was specified",
                [logEvent],
                exception: null);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_syncRoot)
        {
            _fileOutput.Dispose();
        }
    }

    /// <inheritdoc />
    public void FlushToDisk()
    {
        lock (_syncRoot)
        {
            _output.Flush();
            _fileOutput.Flush(true);
        }
    }

    void ISetLoggingFailureListener.SetFailureListener(ILoggingFailureListener failureListener)
    {
        _failureListener = failureListener ?? throw new ArgumentNullException(nameof(failureListener));
    }
}

#endif
