using System;
using Xunit;

namespace Serilog.Sinks.File.Tests
{
    public class RollingIntervalExtensionsTests
    {
        public static object[][] IntervalInstantCurrentNextCheckpoint => new[]
        {
            new object[]{ RollingInterval.Infinite, new DateTime(2018, 01, 01),           null, null },
            new object[]{ RollingInterval.Year,     new DateTime(2018, 01, 01),           new DateTime(2018, 01, 01), new DateTime(2019, 01, 01) },
            new object[]{ RollingInterval.Year,     new DateTime(2018, 06, 01),           new DateTime(2018, 01, 01), new DateTime(2019, 01, 01) },
            new object[]{ RollingInterval.Month,    new DateTime(2018, 01, 01),           new DateTime(2018, 01, 01), new DateTime(2018, 02, 01) },
            new object[]{ RollingInterval.Month,    new DateTime(2018, 01, 14),           new DateTime(2018, 01, 01), new DateTime(2018, 02, 01) },
            new object[]{ RollingInterval.Day,      new DateTime(2018, 01, 01),           new DateTime(2018, 01, 01), new DateTime(2018, 01, 02) },
            new object[]{ RollingInterval.Day,      new DateTime(2018, 01, 01, 12, 0, 0), new DateTime(2018, 01, 01), new DateTime(2018, 01, 02) },
            new object[]{ RollingInterval.Hour,     new DateTime(2018, 01, 01, 0, 0, 0),  new DateTime(2018, 01, 01), new DateTime(2018, 01, 01, 1, 0, 0) },
            new object[]{ RollingInterval.Hour,     new DateTime(2018, 01, 01, 0, 30, 0), new DateTime(2018, 01, 01), new DateTime(2018, 01, 01, 1, 0, 0) },
            new object[]{ RollingInterval.Minute,   new DateTime(2018, 01, 01, 0, 0, 0),  new DateTime(2018, 01, 01), new DateTime(2018, 01, 01, 0, 1, 0) },
            new object[]{ RollingInterval.Minute,   new DateTime(2018, 01, 01, 0, 0, 30), new DateTime(2018, 01, 01), new DateTime(2018, 01, 01, 0, 1, 0) }
        };

        [Theory]
        [MemberData(nameof(IntervalInstantCurrentNextCheckpoint))]
        public void NextIntervalTests(RollingInterval interval, DateTime instant, DateTime? currentCheckpoint, DateTime? nextCheckpoint)
        {
            var current = interval.GetCurrentCheckpoint(instant);
            Assert.Equal(currentCheckpoint, current);

            var next = interval.GetNextCheckpoint(instant);
            Assert.Equal(nextCheckpoint, next);
        }
    }
}
