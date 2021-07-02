using System;

namespace Serilog.Interval
{
    /// <summary>
    /// Month Rolling Interval
    /// </summary>
    public class MonthRollingInterval : RollingInterval
    {
        /// <summary>
        /// Format of rolling file name
        /// </summary>
        public override string Format => "yyyyMM";

        /// <summary>
        /// Normalize time to current checkpoint
        /// </summary>
        public override DateTime? CurrentCheckpoint(DateTime instant) => Normalize(instant);

        /// <summary>
        /// Calculate next checkpoint from time
        /// </summary>
        public override DateTime? NextCheckpoint(DateTime instant) => Normalize(instant).AddMonths(1);

        private static DateTime Normalize(DateTime instant) => new DateTime(instant.Year, instant.Month, 1, 0, 0, 0, instant.Kind);
    }
}
