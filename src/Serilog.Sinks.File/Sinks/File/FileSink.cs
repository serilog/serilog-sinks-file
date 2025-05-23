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

using System.Text;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Formatting;

namespace Serilog.Sinks.File;

/// <summary>
/// Write log events to a disk file.
/// </summary>
public sealed class FileSink : IFileSink, IDisposable, ISetLoggingFailureListener
{
    readonly TextWriter _output;
    readonly FileStream _underlyingStream;
    readonly ITextFormatter _textFormatter;
    readonly long? _fileSizeLimitBytes;
    readonly bool _buffered;
    readonly object _syncRoot = new();
    readonly WriteCountingStream? _countingStreamWrapper;

    ILoggingFailureListener _failureListener = SelfLog.FailureListener;

    /// <summary>Construct a <see cref="FileSink"/>.</summary>
    /// <param name="path">Path to the file.</param>
    /// <param name="textFormatter">Formatter used to convert log events to text.</param>
    /// <param name="fileSizeLimitBytes">The approximate maximum size, in bytes, to which a log file will be allowed to grow.
    /// For unrestricted growth, pass null. The default is 1 GB. To avoid writing partial events, the last event within the limit
    /// will be written in full even if it exceeds the limit.</param>
    /// <param name="encoding">Character encoding used to write the text file. The default is UTF-8 without BOM.</param>
    /// <param name="buffered">Indicates if flushing to the output file can be buffered or not. The default
    /// is false.</param>
    /// <returns>Configuration object allowing method chaining.</returns>
    /// <remarks>This constructor preserves compatibility with early versions of the public API. New code should not depend on this type.</remarks>
    /// <exception cref="ArgumentNullException">When <paramref name="textFormatter"/> is <code>null</code></exception>
    /// <exception cref="ArgumentException">When <paramref name="fileSizeLimitBytes"/> is <code>null</code> or less than <code>0</code></exception>
    /// <exception cref="ArgumentNullException">When <paramref name="path"/> is <code>null</code></exception>
    /// <exception cref="IOException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    /// <exception cref="NotSupportedException"></exception>
    /// <exception cref="PathTooLongException">When <paramref name="path"/> is too long</exception>
    /// <exception cref="UnauthorizedAccessException">The caller does not have the required permission to access the <paramref name="path"/></exception>
    /// <exception cref="ArgumentException">Invalid <paramref name="path"/></exception>
    [Obsolete("This type and constructor will be removed from the public API in a future version; use `WriteTo.File()` instead.")]
    public FileSink(string path, ITextFormatter textFormatter, long? fileSizeLimitBytes, Encoding? encoding = null, bool buffered = false)
        : this(path, textFormatter, fileSizeLimitBytes, encoding, buffered, null)
    {
    }

    // This overload should be used internally; the overload above maintains compatibility with the earlier public API.
    internal FileSink(
        string path,
        ITextFormatter textFormatter,
        long? fileSizeLimitBytes,
        Encoding? encoding,
        bool buffered,
        FileLifecycleHooks? hooks)
    {
        if (path == null) throw new ArgumentNullException(nameof(path));
        if (fileSizeLimitBytes is < 1) throw new ArgumentException("Invalid value provided; file size limit must be at least 1 byte, or null.");
        _textFormatter = textFormatter ?? throw new ArgumentNullException(nameof(textFormatter));
        _fileSizeLimitBytes = fileSizeLimitBytes;
        _buffered = buffered;

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        Stream outputStream = _underlyingStream = System.IO.File.Open(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
        outputStream.Seek(0, SeekOrigin.End);

        if (_fileSizeLimitBytes != null)
        {
            outputStream = _countingStreamWrapper = new WriteCountingStream(_underlyingStream);
        }

        // Parameter reassignment.
        encoding ??= new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        if (hooks != null)
        {
            try
            {
                outputStream = hooks.OnFileOpened(path, outputStream, encoding) ??
                               throw new InvalidOperationException($"The file lifecycle hook `{nameof(FileLifecycleHooks.OnFileOpened)}(...)` returned `null`.");
            }
            catch
            {
                outputStream.Dispose();
                throw;
            }
        }

        _output = new StreamWriter(outputStream, encoding);
    }

    bool IFileSink.EmitOrOverflow(LogEvent logEvent)
    {
        if (logEvent == null) throw new ArgumentNullException(nameof(logEvent));
        lock (_syncRoot)
        {
            if (_fileSizeLimitBytes != null)
            {
                if (_countingStreamWrapper!.CountedLength >= _fileSizeLimitBytes.Value)
                    return false;
            }

            _textFormatter.Format(logEvent, _output);
            if (!_buffered)
                _output.Flush();

            return true;
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

    void ISetLoggingFailureListener.SetFailureListener(ILoggingFailureListener failureListener)
    {
        _failureListener = failureListener ?? throw new ArgumentNullException(nameof(failureListener));
    }
}
