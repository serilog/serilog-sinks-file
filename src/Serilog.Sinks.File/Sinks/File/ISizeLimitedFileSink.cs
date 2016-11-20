namespace Serilog.Sinks.File
{
    /// <summary>
    /// Supported by (file-based) sinks that has a limit that could be reached
    /// </summary>
    public interface ISizeLimitedFileSink
    {
        /// <summary>
        /// Gets a value indicating whether size limit reached.
        /// </summary>
        /// <value>
        ///   <c>true</c> if size limit reached; otherwise, <c>false</c>.
        /// </value>
        bool SizeLimitReached { get; }
    }
}