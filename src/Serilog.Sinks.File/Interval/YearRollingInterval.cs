using System;

namespace Serilog.Interval
{
    /// <summary>
    /// Year Rolling Interval
    /// </summary>
    public class YearRollingInterval : RollingInterval
    {
        /// <summary>
        /// Format of rolling file name
        /// </summary>
        public override string Format => "yyyy";

        /// <summary>
        /// Normalize time to current checkpoint
        /// </summary>
        public override DateTime? CurrentCheckpoint(DateTime instant) => Normalize(instant);

        /// <summary>
        /// Calculate next checkpoint from time
        /// </summary>
        public override DateTime? NextCheckpoint(DateTime instant) => Normalize(instant).AddYears(1);

        private static DateTime Normalize(DateTime instant) => new DateTime(instant.Year, 1, 1, 0, 0, 0, instant.Kind);
    }
}
