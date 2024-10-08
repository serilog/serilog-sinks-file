// Copyright 2017 Serilog Contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Serilog.Sinks.File;

internal static class RollingIntervalExtensions
{
    public static string GetFormat(this RollingInterval interval)
    {
        return interval switch
        {
            RollingInterval.Infinite => "",
            RollingInterval.Year => "yyyy",
            RollingInterval.Month => "yyyyMM",
            RollingInterval.Day or
                RollingInterval.Sunday or
                RollingInterval.Monday or
                RollingInterval.Tuesday or
                RollingInterval.Wednesday or
                RollingInterval.Thursday or
                RollingInterval.Friday or
                RollingInterval.Saturday => "yyyyMMdd",
            RollingInterval.Hour => "yyyyMMddHH",
            RollingInterval.Minute => "yyyyMMddHHmm",
            _ => throw new ArgumentException("Invalid rolling interval.")
        };
    }

    public static DateTime? GetCurrentCheckpoint(this RollingInterval interval, DateTime instant)
    {
        return interval switch
        {
            RollingInterval.Infinite => null,
            RollingInterval.Year => new DateTime(instant.Year, 1, 1, 0, 0, 0, instant.Kind),
            RollingInterval.Month => new DateTime(instant.Year, instant.Month, 1, 0, 0, 0, instant.Kind),
            RollingInterval.Sunday => GetDateForRollOnDay(instant, DayOfWeek.Sunday),
            RollingInterval.Monday => GetDateForRollOnDay(instant, DayOfWeek.Monday),
            RollingInterval.Tuesday => GetDateForRollOnDay(instant, DayOfWeek.Tuesday),
            RollingInterval.Wednesday => GetDateForRollOnDay(instant, DayOfWeek.Wednesday),
            RollingInterval.Thursday => GetDateForRollOnDay(instant, DayOfWeek.Thursday),
            RollingInterval.Friday => GetDateForRollOnDay(instant, DayOfWeek.Friday),
            RollingInterval.Saturday => GetDateForRollOnDay(instant, DayOfWeek.Saturday),
            RollingInterval.Day => new DateTime(instant.Year, instant.Month, instant.Day, 0, 0, 0, instant.Kind),
            RollingInterval.Hour => new DateTime(instant.Year, instant.Month, instant.Day, instant.Hour, 0, 0, instant.Kind),
            RollingInterval.Minute => new DateTime(instant.Year, instant.Month, instant.Day, instant.Hour, instant.Minute, 0, instant.Kind),
            _ => throw new ArgumentException("Invalid rolling interval.")
        };
    }

    public static DateTime? GetNextCheckpoint(this RollingInterval interval, DateTime instant)
    {
        var current = GetCurrentCheckpoint(interval, instant);
        if (current == null)
        {
            return null;
        }

        return interval switch
        {
            RollingInterval.Year => current.Value.AddYears(1),
            RollingInterval.Month => current.Value.AddMonths(1),
            RollingInterval.Sunday or
                RollingInterval.Monday or
                RollingInterval.Tuesday or
                RollingInterval.Wednesday or
                RollingInterval.Thursday or
                RollingInterval.Friday or
                RollingInterval.Saturday => current.Value.AddDays(7),
            RollingInterval.Day => current.Value.AddDays(1),
            RollingInterval.Hour => current.Value.AddHours(1),
            RollingInterval.Minute => current.Value.AddMinutes(1),
            _ => throw new ArgumentException("Invalid rolling interval.")
        };
    }

    private static DateTime? GetDateForRollOnDay(DateTime instant, DayOfWeek rollOnDayOfWeek)
    {
        int delta = rollOnDayOfWeek - instant.DayOfWeek;

        if (delta > 0)
        {
            // Adjust to get the previous roll date for DayOfWeek when the result is positive
            delta -= 7;
        }

        var date = instant.Date.AddDays(delta);

        return date;
    }
}
