using System;

namespace Serilog.Interval
{
    /// <summary>
    /// Specifies the frequency at which the log file should roll.
    /// </summary>
    public abstract class RollingInterval
    {
        /// <summary>
        /// Format of rolling file name
        /// </summary>
        public abstract string Format { get; }

        /// <summary>
        /// Normalize time to current checkpoint (year, month and etc)
        /// </summary>
        /// <param name="instant"></param>
        /// <returns></returns>
        public abstract DateTime? CurrentCheckpoint(DateTime instant);

        /// <summary>
        /// Calculate next checkpoint from time
        /// </summary>
        /// <param name="instant"></param>
        /// <returns></returns>
        public abstract DateTime? NextCheckpoint(DateTime instant);

        /// <summary>
        /// Create RollInterval
        /// </summary>
        /// <param name="intervalType"></param>
        /// <returns></returns>
        public static implicit operator RollingInterval(Serilog.RollingInterval intervalType)
        {
            switch (intervalType)
            {
                case Serilog.RollingInterval.Infinite:
                    return new InfiniteRollingInterval();
                case Serilog.RollingInterval.Year:
                    return new YearRollingInterval();
                case Serilog.RollingInterval.Month:
                    return new MonthRollingInterval();
                case Serilog.RollingInterval.Day:
                    return new DayRollingInterval();
                case Serilog.RollingInterval.Hour:
                    return new HourRollingInterval();
                case Serilog.RollingInterval.Minute:
                    return new MinuteRollingInterval();
                default:
                    throw new ArgumentException("Invalid rolling interval");
            }
        }
    }
}
