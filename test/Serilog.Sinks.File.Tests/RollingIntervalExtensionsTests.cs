using Xunit;

namespace Serilog.Sinks.File.Tests;

public class RollingIntervalExtensionsTests
{
    public static object?[][] IntervalInstantCurrentNextCheckpoint =>
    [
        [RollingInterval.Infinite, new DateTime(2018, 01, 01),           null, null],
        [RollingInterval.Year,     new DateTime(2018, 01, 01),           new DateTime(2018, 01, 01), new DateTime(2019, 01, 01)],
        [RollingInterval.Year,     new DateTime(2018, 06, 01),           new DateTime(2018, 01, 01), new DateTime(2019, 01, 01)],
        [RollingInterval.Month,    new DateTime(2018, 01, 01),           new DateTime(2018, 01, 01), new DateTime(2018, 02, 01)],
        [RollingInterval.Month,    new DateTime(2018, 01, 14),           new DateTime(2018, 01, 01), new DateTime(2018, 02, 01)],
        [RollingInterval.Day,      new DateTime(2018, 01, 01),           new DateTime(2018, 01, 01), new DateTime(2018, 01, 02)],
        [RollingInterval.Day,      new DateTime(2018, 01, 01, 12, 0, 0), new DateTime(2018, 01, 01), new DateTime(2018, 01, 02)],
        [RollingInterval.Hour,     new DateTime(2018, 01, 01, 0, 0, 0),  new DateTime(2018, 01, 01), new DateTime(2018, 01, 01, 1, 0, 0)],
        [RollingInterval.Hour,     new DateTime(2018, 01, 01, 0, 30, 0), new DateTime(2018, 01, 01), new DateTime(2018, 01, 01, 1, 0, 0)],
        [RollingInterval.Minute,   new DateTime(2018, 01, 01, 0, 0, 0),  new DateTime(2018, 01, 01), new DateTime(2018, 01, 01, 0, 1, 0)],
        [RollingInterval.Minute,   new DateTime(2018, 01, 01, 0, 0, 30), new DateTime(2018, 01, 01), new DateTime(2018, 01, 01, 0, 1, 0)]
    ];

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
