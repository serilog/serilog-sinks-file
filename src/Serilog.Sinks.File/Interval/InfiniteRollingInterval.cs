using System;

namespace Serilog.Interval
{
    /// <summary>
    /// Infinite Rolling Interval
    /// </summary>
    public class InfiniteRollingInterval : RollingInterval
    {
        /// <summary>
        /// Format of rolling file name
        /// </summary>
        public override string Format => string.Empty;

        /// <summary>
        /// Normalize time to current checkpoint
        /// </summary>
        public override DateTime? CurrentCheckpoint(DateTime instant) => null;

        /// <summary>
        /// Calculate next checkpoint from time
        /// </summary>
        public override DateTime? NextCheckpoint(DateTime instant) => null;
    }
}
