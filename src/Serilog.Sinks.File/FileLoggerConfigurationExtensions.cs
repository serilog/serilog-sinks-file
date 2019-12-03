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

using System;
using System.ComponentModel;
using System.Text;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display;
using Serilog.Formatting.Json;
using Serilog.Sinks.File;

// ReSharper disable RedundantArgumentDefaultValue, MethodOverloadWithOptionalParameter

namespace Serilog
{
    /// <summary>Extends <see cref="LoggerConfiguration"/> with methods to add file sinks.</summary>
    public static partial class FileLoggerConfigurationExtensions
    {
        const int DefaultRetainedFileCountLimit = 31; // A long month of logs
        const long DefaultFileSizeLimitBytes = 1L * 1024 * 1024 * 1024;
        const string DefaultOutputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}";

        static LoggerConfiguration ConfigureFile(
            this Func<ILogEventSink, LogEventLevel, LoggingLevelSwitch, LoggerConfiguration> addSink,
            ITextFormatter formatter,
            string path,
            LogEventLevel restrictedToMinimumLevel,
            long? fileSizeLimitBytes,
            LoggingLevelSwitch levelSwitch,
            bool buffered,
            bool propagateExceptions,
            bool shared,
            TimeSpan? flushToDiskInterval,
            Encoding encoding,
            RollingInterval rollingInterval,
            bool rollOnFileSizeLimit,
            int? retainedFileCountLimit,
            FileLifecycleHooks hooks)
        {
            if (addSink == null) throw new ArgumentNullException(nameof(addSink));
            if (formatter == null) throw new ArgumentNullException(nameof(formatter));
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (fileSizeLimitBytes.HasValue && fileSizeLimitBytes < 0) throw new ArgumentException("Negative value provided; file size limit must be non-negative.", nameof(fileSizeLimitBytes));
            if (retainedFileCountLimit.HasValue && retainedFileCountLimit < 1) throw new ArgumentException("At least one file must be retained.", nameof(retainedFileCountLimit));
            if (shared && buffered) throw new ArgumentException("Buffered writes are not available when file sharing is enabled.", nameof(buffered));
            if (shared && hooks != null) throw new ArgumentException("File lifecycle hooks are not currently supported for shared log files.", nameof(hooks));

            ILogEventSink sink;

            if (rollOnFileSizeLimit || rollingInterval != RollingInterval.Infinite)
            {
                sink = new RollingFileSink(path, formatter, fileSizeLimitBytes, retainedFileCountLimit, encoding, buffered, shared, rollingInterval, rollOnFileSizeLimit, hooks, propagateExceptions);
            }
            else
            {
                try
                {
                    if (shared)
                    {
#pragma warning disable 618
                        sink = new SharedFileSink(path, formatter, fileSizeLimitBytes, encoding);
#pragma warning restore 618
                    }
                    else
                    {
                        sink = new FileSink(path, formatter, fileSizeLimitBytes, encoding, buffered, hooks);
                    }
                }
                catch (Exception ex)
                {
                    SelfLog.WriteLine("Unable to open file sink for {0}: {1}", path, ex);

                    if (propagateExceptions)
                        throw;

                    return addSink(new NullSink(), LevelAlias.Maximum, null);
                }
            }

            if (flushToDiskInterval.HasValue)
            {
#pragma warning disable 618
                sink = new PeriodicFlushToDiskSink(sink, flushToDiskInterval.Value);
#pragma warning restore 618
            }

            return addSink(sink, restrictedToMinimumLevel, levelSwitch);
        }
    }
}
