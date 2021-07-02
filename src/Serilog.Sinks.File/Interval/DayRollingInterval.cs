using System;

namespace Serilog.Interval
{
    /// <summary>
    /// Day Rolling Interval
    /// </summary>
    public class DayRollingInterval : RollingInterval
    {
        /// <summary>
        /// Format of rolling file name
        /// </summary>
        public override string Format => "yyyyMMdd";

        /// <summary>
        /// Normalize time to current checkpoint
        /// </summary>
        public override DateTime? CurrentCheckpoint(DateTime instant) => Normalize(instant);

        /// <summary>
        /// Calculate next checkpoint from time
        /// </summary>
        public override DateTime? NextCheckpoint(DateTime instant) => Normalize(instant).AddDays(1);

        private static DateTime Normalize(DateTime instant) => new DateTime(instant.Year, instant.Month, instant.Day, 0, 0, 0, instant.Kind);
    }
}
