namespace Serilog.Sinks;

/// <summary>
/// Specifies the pattern in log file path (if any)
/// </summary>
public enum PathPatternType
{
    /// <summary>
    /// The path has a interval pattern in it
    /// </summary>
    Interval,
    /// <summary>
    /// The path has a sequence number pattern in it
    /// </summary>
    SequenceNumber,
    /// <summary>
    /// The path matches both of the patterns
    /// </summary>
    Both
}
