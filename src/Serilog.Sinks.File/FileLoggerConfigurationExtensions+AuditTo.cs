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
    public static partial class FileLoggerConfigurationExtensions
    {
        /// <summary>
        /// Write log events to the specified file.
        /// </summary>
        /// <param name="sinkConfiguration">Logger sink configuration.</param>
        /// <param name="path">Path to the file.</param>
        /// <param name="restrictedToMinimumLevel">The minimum level for
        /// events passed through the sink. Ignored when <paramref name="levelSwitch"/> is specified.</param>
        /// <param name="levelSwitch">A switch allowing the pass-through minimum level
        /// to be changed at runtime.</param>
        /// <param name="formatProvider">Supplies culture-specific formatting information, or null.</param>
        /// <param name="outputTemplate">A message template describing the format used to write to the sink.
        /// the default is "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}".</param>
        /// <returns>Configuration object allowing method chaining.</returns>
        /// <remarks>The file will be written using the UTF-8 character set.</remarks>
        [Obsolete("New code should not be compiled against this obsolete overload"), EditorBrowsable(EditorBrowsableState.Never)]
        public static LoggerConfiguration File(
            this LoggerAuditSinkConfiguration sinkConfiguration,
            string path,
            LogEventLevel restrictedToMinimumLevel,
            string outputTemplate,
            IFormatProvider formatProvider,
            LoggingLevelSwitch levelSwitch)
        {
            return File(sinkConfiguration, path, restrictedToMinimumLevel, outputTemplate, formatProvider, levelSwitch, null, null);
        }

        /// <summary>
        /// Write log events to the specified file.
        /// </summary>
        /// <param name="sinkConfiguration">Logger sink configuration.</param>
        /// <param name="formatter">A formatter, such as <see cref="JsonFormatter"/>, to convert the log events into
        /// text for the file. If control of regular text formatting is required, use the other
        /// overload of <see cref="File(LoggerAuditSinkConfiguration, string, LogEventLevel, string, IFormatProvider, LoggingLevelSwitch)"/>
        /// and specify the outputTemplate parameter instead.
        /// </param>
        /// <param name="path">Path to the file.</param>
        /// <param name="restrictedToMinimumLevel">The minimum level for
        /// events passed through the sink. Ignored when <paramref name="levelSwitch"/> is specified.</param>
        /// <param name="levelSwitch">A switch allowing the pass-through minimum level
        /// to be changed at runtime.</param>
        /// <returns>Configuration object allowing method chaining.</returns>
        /// <remarks>The file will be written using the UTF-8 character set.</remarks>
        [Obsolete("New code should not be compiled against this obsolete overload"), EditorBrowsable(EditorBrowsableState.Never)]
        public static LoggerConfiguration File(
            this LoggerAuditSinkConfiguration sinkConfiguration,
            ITextFormatter formatter,
            string path,
            LogEventLevel restrictedToMinimumLevel,
            LoggingLevelSwitch levelSwitch)
        {
            return File(sinkConfiguration, formatter, path, restrictedToMinimumLevel, levelSwitch, null, null);
        }

        /// <summary>
        /// Write audit log events to the specified file.
        /// </summary>
        /// <param name="sinkConfiguration">Logger sink configuration.</param>
        /// <param name="path">Path to the file.</param>
        /// <param name="restrictedToMinimumLevel">The minimum level for
        /// events passed through the sink. Ignored when <paramref name="levelSwitch"/> is specified.</param>
        /// <param name="levelSwitch">A switch allowing the pass-through minimum level
        /// to be changed at runtime.</param>
        /// <param name="formatProvider">Supplies culture-specific formatting information, or null.</param>
        /// <param name="outputTemplate">A message template describing the format used to write to the sink.
        /// the default is "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}".</param>
        /// <param name="encoding">Character encoding used to write the text file. The default is UTF-8 without BOM.</param>
        /// <param name="hooks">Optionally enables hooking into log file lifecycle events.</param>
        /// <returns>Configuration object allowing method chaining.</returns>
        public static LoggerConfiguration File(
            this LoggerAuditSinkConfiguration sinkConfiguration,
            string path,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            string outputTemplate = DefaultOutputTemplate,
            IFormatProvider formatProvider = null,
            LoggingLevelSwitch levelSwitch = null,
            Encoding encoding = null,
            FileLifecycleHooks hooks = null)
        {
            if (sinkConfiguration == null) throw new ArgumentNullException(nameof(sinkConfiguration));
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (outputTemplate == null) throw new ArgumentNullException(nameof(outputTemplate));

            var formatter = new MessageTemplateTextFormatter(outputTemplate, formatProvider);
            return File(sinkConfiguration, formatter, path, restrictedToMinimumLevel, levelSwitch, encoding, hooks);
        }

        /// <summary>
        /// Write audit log events to the specified file.
        /// </summary>
        /// <param name="sinkConfiguration">Logger sink configuration.</param>
        /// <param name="formatter">A formatter, such as <see cref="JsonFormatter"/>, to convert the log events into
        /// text for the file. If control of regular text formatting is required, use the other
        /// overload of <see cref="File(LoggerAuditSinkConfiguration, string, LogEventLevel, string, IFormatProvider, LoggingLevelSwitch, Encoding, FileLifecycleHooks)"/>
        /// and specify the outputTemplate parameter instead.
        /// </param>
        /// <param name="path">Path to the file.</param>
        /// <param name="restrictedToMinimumLevel">The minimum level for
        /// events passed through the sink. Ignored when <paramref name="levelSwitch"/> is specified.</param>
        /// <param name="levelSwitch">A switch allowing the pass-through minimum level
        /// to be changed at runtime.</param>
        /// <param name="encoding">Character encoding used to write the text file. The default is UTF-8 without BOM.</param>
        /// <param name="hooks">Optionally enables hooking into log file lifecycle events.</param>
        /// <returns>Configuration object allowing method chaining.</returns>
        public static LoggerConfiguration File(
            this LoggerAuditSinkConfiguration sinkConfiguration,
            ITextFormatter formatter,
            string path,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            LoggingLevelSwitch levelSwitch = null,
            Encoding encoding = null,
            FileLifecycleHooks hooks = null)
        {
            if (sinkConfiguration == null) throw new ArgumentNullException(nameof(sinkConfiguration));
            if (formatter == null) throw new ArgumentNullException(nameof(formatter));
            if (path == null) throw new ArgumentNullException(nameof(path));

            return ConfigureFile(sinkConfiguration.Sink, formatter, path, restrictedToMinimumLevel, null, levelSwitch, false, true,
                false, null, encoding, RollingInterval.Infinite, false, null, hooks);
        }
    }
}
