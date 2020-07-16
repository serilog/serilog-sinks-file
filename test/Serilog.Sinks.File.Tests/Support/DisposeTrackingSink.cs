using System;
using Serilog.Core;
using Serilog.Events;

namespace Serilog.Sinks.File.Tests.Support
{
    class DisposeTrackingSink : ILogEventSink, IDisposable
    {
        public bool IsDisposed { get; set; }

        public void Emit(LogEvent logEvent)
        {            
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }
}
