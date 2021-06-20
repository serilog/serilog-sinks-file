using System;

namespace Serilog.Interval
{
    /// <summary>
    /// Minute Rolling Interval
    /// </summary>
    public class MinuteRollingInterval : RollingInterval
    {
        /// <summary>
        /// Format of rolling file name
        /// </summary>
        public override string Format => "yyyyMMddHHmm";

        /// <summary>
        /// Normalize time to current checkpoint
        /// </summary>
        public override DateTime? CurrentCheckpoint(DateTime instant) => Normalize(instant);

        /// <summary>
        /// Calculate next checkpoint from time
        /// </summary>
        public override DateTime? NextCheckpoint(DateTime instant) => Normalize(instant).AddMinutes(1);

        private static DateTime Normalize(DateTime instant) => new DateTime(instant.Year, instant.Month, instant.Day, instant.Hour, instant.Minute, 0, instant.Kind);
    }
}
