using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Serilog.Sinks.File.Tests
{
    public class FormattedPathRollerTests
    {
        [Fact]
        public void TheLogFileFormatWorksWithInfiniteInterval()
        {
            var roller = PathRoller.CreateForFormattedPath( "Logs", "'log-'yyyy-MM-dd'.txt'", RollingInterval.Infinite);
            var now = new DateTime(2013, 7, 14, 3, 24, 9, 980);
            string path;
            roller.GetLogFilePath(now, null, out path);
            AssertEqualAbsolute(Path.Combine("Logs", "log-0001-01-01.txt"), path);
        }

        [Fact]
        public void TheLogFileIncludesDateToken()
        {
            var roller = PathRoller.CreateForFormattedPath( "Logs", "'log-'yyyy-MM-dd'.txt'", RollingInterval.Day);
            var now = new DateTime(2013, 7, 14, 3, 24, 9, 980);
            string path;
            roller.GetLogFilePath(now, null, out path);
            AssertEqualAbsolute(Path.Combine("Logs", "log-2013-07-14.txt"), path);
        }

        [Fact]
        public void TheLogFileIncludesDateTokenAndOtherComponentsAreReset()
        {
            var roller = PathRoller.CreateForFormattedPath("Logs", "'log-'yyyy-MM-dd-HH-mm-ss'.txt'", RollingInterval.Day);
            var now = new DateTime(2013, 7, 14, 3, 24, 9, 980);
            string path;
            roller.GetLogFilePath(now, null, out path);
            AssertEqualAbsolute(Path.Combine("Logs", "log-2013-07-14-00-00-00.txt"), path);
        }

        [Fact]
        public void ANonZeroIncrementIsIncludedAndPadded()
        {
            var roller = PathRoller.CreateForFormattedPath("Logs", "'log-'yyyy-MM-dd'.txt'", RollingInterval.Day);
            var now = new DateTime(2013, 7, 14, 3, 24, 9, 980);
            string path;
            roller.GetLogFilePath(now, 12, out path);
            AssertEqualAbsolute(Path.Combine("Logs", "log-2013-07-14_012.txt"), path);
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
            var roller = PathRoller.CreateForFormattedPath("Logs", "'log-'yyyy-MM-dd'.txt'", RollingInterval.Day);
            AssertEqualAbsolute("Logs", roller.LogFileDirectory);
        }

        [Fact]
        public void TheLogFileIsNotRequiredToIncludeAnExtension()
        {
            var roller = PathRoller.CreateForFormattedPath("Logs", "'log-'yyyy-MM-dd", RollingInterval.Day);
            var now = new DateTime(2013, 7, 14, 3, 24, 9, 980);
            string path;
            roller.GetLogFilePath(now, sequenceNumber: null, path: out path);
            AssertEqualAbsolute(Path.Combine("Logs", "log-2013-07-14"), path);
        }

        [Fact]
        public void TheLogFileIsNotRequiredToIncludeADirectory()
        {
            var roller = PathRoller.CreateForFormattedPath( logDirectoryPath: null, filePathFormat: "'log-'yyyy-MM-dd", interval: RollingInterval.Day); // Note this also excludes a file-name extension too.
            var now = new DateTime(2013, 7, 14, 3, 24, 9, 980);
            string path;
            roller.GetLogFilePath(now, null, out path);
            AssertEqualAbsolute("log-2013-07-14", path);
        }

        [Theory]
        [InlineData( null , @"yyyy-MM'\\Log-'yyyy-MM-dd'.log'"        , "2013-07\\Log-2013-07-14.log")]
        [InlineData( "Log", @"'Month 'yyyy-MM'\\Log-'yyyy-MM-dd'.log'", "Log\\Month 2013-07\\Log-2013-07-14.log")]
        public void TheLogFileFormatCanIncludeFormattedSubdirectories(string logDirectoryPath, string format, string expected)
        {
            var roller = PathRoller.CreateForFormattedPath( logDirectoryPath: logDirectoryPath, filePathFormat: format, interval: RollingInterval.Day);
            var now = new DateTime(2013, 7, 14, 3, 24, 9, 980);
            string path;
            roller.GetLogFilePath(now, null, out path);
            AssertEqualAbsolute(expected, path);
        }

        [Fact]
        public void MatchingExcludesSimilarButNonmatchingFiles()
        {
            var roller = PathRoller.CreateForFormattedPath( "Logs", "'log-'yyyy-MM-dd'.txt'", RollingInterval.Day);
            const string similar1 = "log-0.txt";
            const string similar2 = "log-helloyou.txt";
            const string similar3 = "log-9999-99-99.txt";
            var matched = roller.SelectMatches(new[] { new FileInfo( similar1 ), new FileInfo( similar2 ), new FileInfo( similar3 ) });
            Assert.Equal(0, matched.Count());
        }

        [Theory]
        [InlineData( null , "'log-'yyyy-MM-dd'.txt'", "log-*.txt" )]
        [InlineData( "Log", "yyyy-MM-dd'.txt'"      , "*.txt"     )]
        [InlineData( "Log", "yyyy-MM-dd"            , "*"         )]
        public void TheDirectorySearchPatternUsesWildcardInPlaceOfDate(string logDirectoryPath, string format, string expected)
        {
            var roller = PathRoller.CreateForFormattedPath("Logs", , RollingInterval.Day);
            Assert.Equal("log-*.txt", roller.DirectorySearchPattern);
        }

        [Fact]
        public void TheDirectorySearchPatternUsesWildcardInPlaceOfDate4()
        {
            var roller = PathRoller.CreateForFormattedPath("Logs", "'log-'yyyy-MM-dd' fnord 'yyyy-MM-dd'.txt'", RollingInterval.Day);
            Assert.Equal("log-*.txt", roller.DirectorySearchPattern);
        }

        [Theory]
        [InlineData("'log-'yyyy-MM-dd'.txt'"   , "log-2013-12-10.txt"   , "log-2013-12-10_031.txt", RollingInterval.Day)] // "_031" is the sequence number, not a DateTime component.
        [InlineData("'log-'yyyy-MM-dd_HH'.txt'", "log-2013-12-10_13.txt", "log-2013-12-10_13_031.txt", RollingInterval.Hour)]
        public void MatchingSelectsFiles(string template, string zeroth, string thirtyFirst, RollingInterval interval)
        {
            var roller = PathRoller.CreateForFormattedPath( Directory.GetCurrentDirectory(), template, interval );
            var matched = roller.SelectMatches(new[] { new FileInfo( zeroth ), new FileInfo( thirtyFirst ) }).ToArray();
            Assert.Equal(2, matched.Length);
            Assert.Equal(null, matched[0].SequenceNumber);
            Assert.Equal(31, matched[1].SequenceNumber);
        }

        [Theory]
        [InlineData("'log-'yyyy-MM-dd'.txt'"   , "log-2015-01-01.txt"   , "log-2014-12-31.txt"   , RollingInterval.Day )]
        [InlineData("'log-'yyyy-MM-dd-HH'.txt'", "log-2015-01-01-10.txt", "log-2015-01-01-09.txt", RollingInterval.Hour)]
        public void MatchingParsesSubstitutions(string template, string newer, string older, RollingInterval interval)
        {
            var roller = PathRoller.CreateForFormattedPath( Directory.GetCurrentDirectory(), template, interval );

            string[] actual = roller
                .SelectMatches(new[] { new FileInfo( older ), new FileInfo( newer ) })
                .OrderByDescending(m => m.DateTime)
                .Select(m => m.File.Name)
                .ToArray();

            string[] expected = new[]
            {
                newer,
                older
            };

            Assert.Equal( expected: expected, actual: actual );
        }

        [Theory]
        [InlineData("'log-'MM-dd'.txt'", RollingInterval.Infinite )]
        [InlineData("'log-'MM-dd'.txt'", RollingInterval.Year )]
        [InlineData("'log-'MM-dd'.txt'", RollingInterval.Month )]
        [InlineData("'log-'MM-dd'.txt'", RollingInterval.Day )]
        [InlineData("'log-'MM-dd'.txt'", RollingInterval.Hour )]
        [InlineData("'log-'MM-dd'.txt'", RollingInterval.Minute )]
        public void ConstructorComplainsIfFormatLacksRequiredSpecifiers(string template, RollingInterval interval)
        {
            Assert.Throws( typeof(ArgumentException), () => {

                var roller = PathRoller.CreateForFormattedPath( Directory.GetCurrentDirectory(), template, interval );

                Assert.True( condition: false, userMessage: nameof(PathRoller.CreateForFormattedPath) + " should throw, so this assertion should never be encountered." );
            } );
        }

        [Theory]
        [InlineData( RollingInterval.Infinite )]
        [InlineData( RollingInterval.Year )]
        [InlineData( RollingInterval.Month )]
        [InlineData( RollingInterval.Day )]
        [InlineData( RollingInterval.Hour )]
        [InlineData( RollingInterval.Minute )]
        public void ConstructorAcceptsFormatsMorePreciseThanInterval(RollingInterval interval)
        {
            const string format = "'Log-'yyyy-MM-dd' time 'HH-mm-ss'.log'";

            var roller = PathRoller.CreateForFormattedPath( Directory.GetCurrentDirectory(), format, interval );

            // NOOP. If it doesn't throw, we're okay.
        }
    }
}

