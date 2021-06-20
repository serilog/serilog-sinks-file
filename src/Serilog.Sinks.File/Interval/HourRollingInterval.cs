using System;

namespace Serilog.Interval
{
    /// <summary>
    /// Hour Rolling Interval
    /// </summary>
    public class HourRollingInterval : RollingInterval
    {
        /// <summary>
        /// Format of rolling file name
        /// </summary>
        public override string Format => "yyyyMMddHH";

        /// <summary>
        /// Normalize time to current checkpoint
        /// </summary>
        public override DateTime? CurrentCheckpoint(DateTime instant) => Normalize(instant);

        /// <summary>
        /// Calculate next checkpoint from time
        /// </summary>
        public override DateTime? NextCheckpoint(DateTime instant) => Normalize(instant).AddHours(1);

        private static DateTime Normalize(DateTime instant) => new DateTime(instant.Year, instant.Month, instant.Day, instant.Hour, 0, 0, instant.Kind);
    }
}
