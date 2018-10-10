using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Serilog.Sinks.File.Tests
{
    public class PathRollerTests
    {
        [Fact]
        public void TheLogFileIncludesDateToken()
        {
            var roller = new PathRoller(Path.Combine("Logs", "log-.txt"), RollingInterval.Day);
            var now = new DateTime(2013, 7, 14, 3, 24, 9, 980);
            string path;
            roller.GetLogFilePath(now, null, out path);
            AssertEqualAbsolute(Path.Combine("Logs", "log-20130714.txt"), path);
        }

        [Fact]
        public void ANonZeroIncrementIsIncludedAndPadded()
        {
            var roller = new PathRoller(Path.Combine("Logs", "log-.txt"), RollingInterval.Day);
            var now = new DateTime(2013, 7, 14, 3, 24, 9, 980);
            string path;
            roller.GetLogFilePath(now, 12, out path);
            AssertEqualAbsolute(Path.Combine("Logs", "log-20130714_012.txt"), path);
        }

        static void AssertEqualAbsolute(string path1, string path2)
        {
            var abs1 = Path.GetFullPath(path1);
            var abs2 = Path.GetFullPath(path2);
            Assert.Equal(abs1, abs2);
        }

        [Fact]
        public void TheRollerReturnsTheLogFileDirectory()
        {
            var roller = new PathRoller(Path.Combine("Logs", "log-.txt"), RollingInterval.Day);
            AssertEqualAbsolute("Logs", roller.LogFileDirectory);
        }

        [Fact]
        public void TheLogFileIsNotRequiredToIncludeAnExtension()
        {
            var roller = new PathRoller(Path.Combine("Logs", "log-"), RollingInterval.Day);
            var now = new DateTime(2013, 7, 14, 3, 24, 9, 980);
            string path;
            roller.GetLogFilePath(now, null, out path);
            AssertEqualAbsolute(Path.Combine("Logs", "log-20130714"), path);
        }

        [Fact]
        public void TheLogFileIsNotRequiredToIncludeADirectory()
        {
            var roller = new PathRoller("log-", RollingInterval.Day);
            var now = new DateTime(2013, 7, 14, 3, 24, 9, 980);
            string path;
            roller.GetLogFilePath(now, null, out path);
            AssertEqualAbsolute("log-20130714", path);
        }

        [Fact]
        public void MatchingExcludesSimilarButNonmatchingFiles()
        {
            var roller = new PathRoller("log-.txt", RollingInterval.Day);
            const string similar1 = "log-0.txt";
            const string similar2 = "log-helloyou.txt";
            var matched = roller.SelectMatches(new[] { similar1, similar2 });
            Assert.Equal(0, matched.Count());
        }

        [Fact]
        public void TheDirectorSearchPatternUsesWildcardInPlaceOfDate()
        {
            var roller = new PathRoller(Path.Combine("Logs", "log-.txt"), RollingInterval.Day);
            Assert.Equal("log-*.txt", roller.DirectorySearchPattern);
        }

        [Theory]
        [InlineData("log-.txt", "log-20131210.txt", "log-20131210_031.txt", RollingInterval.Day)]
        [InlineData("log-.txt", "log-2013121013.txt", "log-2013121013_031.txt", RollingInterval.Hour)]
        public void MatchingSelectsFiles(string template, string zeroth, string thirtyFirst, RollingInterval interval)
        {
            var roller = new PathRoller(template, interval);
            var matched = roller.SelectMatches(new[] { zeroth, thirtyFirst }).ToArray();
            Assert.Equal(2, matched.Length);
            Assert.Equal(null, matched[0].SequenceNumber);
            Assert.Equal(31, matched[1].SequenceNumber);
        }

        [Theory]
        [InlineData("log-.txt", "log-20150101.txt", "log-20141231.txt", RollingInterval.Day)]
        [InlineData("log-.txt", "log-2015010110.txt", "log-2015010109.txt", RollingInterval.Hour)]
        public void MatchingParsesSubstitutions(string template, string newer, string older, RollingInterval interval)
        {
            var roller = new PathRoller(template, interval);
            var matched = roller.SelectMatches(new[] { older, newer }).OrderByDescending(m => m.DateTime).Select(m => m.Filename).ToArray();
            Assert.Equal(new[] { newer, older }, matched);
        }
    }
}

