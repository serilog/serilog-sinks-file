using Xunit;
using System.Threading;

namespace Serilog.Sinks.File.Tests
{
    public class IOTests
    {
        [Fact]
        public void ShouldUseDefaultImplementations()
        {
            Assert.Equal(IO.DefaultFileDelete, IO.FileDelete);
            Assert.Equal(IO.DefaultDirectoryGetFiles, IO.DirectoryGetFiles);
            Assert.Equal(IO.DefaultDirectoryExists, IO.DirectoryExists);
#if ATOMIC_APPEND
            Assert.Equal(IO.DefaultNewFileStream, IO.NewFileStream);
#endif
            Assert.Equal(IO.DefaultFileOpen, IO.FileOpen);
            Assert.Equal(IO.DefaultDirectoryCreateDirectory, IO.DirectoryCreateDirectory);
        }

        [Fact]
        public void ShouldResetImplementationOnCurrentCurrentThread()
        {
            void Thread1Work()
            {
                GetCustomImplementations(
                    out var fileDelete,
                    out var directoryGetFiles,
                    out var directoryExists,
#if ATOMIC_APPEND
                    out var newFileStream,
#endif
                    out var fileOpen,
                    out var directoryCreateDirectory);

                IO.Reset(fileDelete,
                    directoryGetFiles,
                    directoryExists,
#if ATOMIC_APPEND
                    newFileStream,
#endif
                    fileOpen,
                    directoryCreateDirectory);
            }

            var testThread = new Thread(Thread1Work);

            testThread.Start();
            testThread.Join();

            ShouldUseDefaultImplementations();
        }

        [Fact]
        public void ShouldBeAbleToResetCustomImplementationAndRevertToDefaults()
        {
            try
            {
                GetCustomImplementations(
                    out var fileDelete,
                    out var directoryGetFiles,
                    out var directoryExists,
#if ATOMIC_APPEND
                    out var newFileStream,
#endif
                    out var fileOpen,
                    out var directoryCreateDirectory);

                IO.Reset(
                    fileDelete,
                    directoryGetFiles,
                    directoryExists,
#if ATOMIC_APPEND
                    newFileStream,
#endif
                    fileOpen,
                    directoryCreateDirectory);

                Assert.Equal(fileDelete, IO.FileDelete);
                Assert.Equal(directoryGetFiles, IO.DirectoryGetFiles);
                Assert.Equal(directoryExists, IO.DirectoryExists);
#if ATOMIC_APPEND
                Assert.Equal(newFileStream, IO.NewFileStream);
#endif
                Assert.Equal(fileOpen, IO.FileOpen);
                Assert.Equal(directoryCreateDirectory, IO.DirectoryCreateDirectory);
            }
            finally
            {
                IO.Reset();
                ShouldUseDefaultImplementations();
            }
        }

        private static void GetCustomImplementations(
            out FileDelete fileDelete,
            out DirectoryGetFiles directoryGetFiles,
            out DirectoryExists directoryExists,
#if ATOMIC_APPEND
            out NewFileStream newFileStream,
#endif
            out FileOpen fileOpen,
            out DirectoryCreateDirectory directoryCreateDirectory)
        {
            fileDelete = (_) => { };
            directoryGetFiles = (_, __) => new string[0];
            directoryExists = (_) => false;
#if ATOMIC_APPEND
            newFileStream = (_, __, ___, ____, _____, ______) => null;
#endif
            fileOpen = (_, __, ___, ____) => null;
            directoryCreateDirectory = (_) => null;
        }
    }
}
