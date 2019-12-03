// Copyright 2013-2017 Serilog Contributors
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

using System;
using System.IO;
#if ATOMIC_APPEND
using System.Security.AccessControl;
#endif

namespace Serilog.Sinks.File
{
    delegate void FileDelete(string path);
    delegate string[] DirectoryGetFiles(string logFileDirectory, string directorySearchPattern);
    delegate bool DirectoryExists(string logFileDirectory);
#if ATOMIC_APPEND
    delegate FileStream NewFileStream(string path, FileMode mode, FileSystemRights rights, FileShare share, int bufferSize, FileOptions options);
#endif
    delegate FileStream FileOpen(string path, FileMode mode, FileAccess access, FileShare share);
    delegate DirectoryInfo DirectoryCreateDirectory(string path);

    static class IO
    {
        public static FileDelete DefaultFileDelete => System.IO.File.Delete;
        [ThreadStatic] private static FileDelete _testFileDelete;
        private static FileDelete _fileDelete;
        public static FileDelete FileDelete { get => _fileDelete ?? _testFileDelete ?? DefaultFileDelete; }

        public static DirectoryGetFiles DefaultDirectoryGetFiles => Directory.GetFiles;
        [ThreadStatic] private static DirectoryGetFiles _testDirectoryGetFiles;
        private static DirectoryGetFiles _directoryGetFiles;
        public static DirectoryGetFiles DirectoryGetFiles { get => _directoryGetFiles ?? _testDirectoryGetFiles ?? DefaultDirectoryGetFiles; }

        public static DirectoryExists DefaultDirectoryExists => Directory.Exists;
        [ThreadStatic] private static DirectoryExists _testDirectoryExists;
        private static DirectoryExists _directoryExists;
        public static DirectoryExists DirectoryExists { get => _directoryExists ?? _testDirectoryExists ?? DefaultDirectoryExists; }

#if ATOMIC_APPEND
        public static NewFileStream DefaultNewFileStream => (path, mode, rights, share, bufferSize, options) => new FileStream(path, mode, rights, share, bufferSize, options);
        [ThreadStatic] private static NewFileStream _testNewFileStream;
        private static NewFileStream _newFileStream;
        public static NewFileStream NewFileStream { get => _newFileStream ?? _testNewFileStream ?? DefaultNewFileStream; }
#endif
        public static FileOpen DefaultFileOpen => System.IO.File.Open;
        [ThreadStatic] private static FileOpen _testFileOpen;
        private static FileOpen _fileOpen;
        public static FileOpen FileOpen { get => _fileOpen ?? _testFileOpen ?? DefaultFileOpen; }

        public static DirectoryCreateDirectory DefaultDirectoryCreateDirectory => Directory.CreateDirectory;
        [ThreadStatic] private static DirectoryCreateDirectory _testDirectoryCreateDirectory;
        private static DirectoryCreateDirectory _directoryCreateDirectory;
        public static DirectoryCreateDirectory DirectoryCreateDirectory { get => _directoryCreateDirectory ?? _testDirectoryCreateDirectory ?? DefaultDirectoryCreateDirectory; }


        static IO()
        {
            Reset();
        }

        /// <summary>
        /// Set IO operation to specific implementation
        /// </summary>
        /// <remarks>
        /// Test implemetation is stored in <see cref="ThreadStaticAttribute"/> field and default implementation is set to null.
        /// Passing null as an argument resets vlaue to defaults.
        /// </remarks>
        public static void Reset(
            FileDelete fileDelete = null,
            DirectoryGetFiles directoryGetFiles = null,
            DirectoryExists directoryExists = null,
#if ATOMIC_APPEND
            NewFileStream newFileStream = null,
#endif
            FileOpen fileOpen = null,
            DirectoryCreateDirectory directoryCreateDirectory = null)
        {
            SetField(ref _fileDelete, ref _testFileDelete, fileDelete ?? DefaultFileDelete, fileDelete != null);
            SetField(ref _directoryGetFiles, ref _testDirectoryGetFiles, directoryGetFiles ?? DefaultDirectoryGetFiles, directoryGetFiles != null);
            SetField(ref _directoryExists, ref _testDirectoryExists, directoryExists ?? DefaultDirectoryExists, directoryExists != null);
#if ATOMIC_APPEND
            SetField(ref _newFileStream, ref _testNewFileStream, newFileStream ?? DefaultNewFileStream, newFileStream != null);
#endif
            SetField(ref _fileOpen, ref _testFileOpen, fileOpen ?? DefaultFileOpen, fileOpen != null);
            SetField(ref _directoryCreateDirectory, ref _testDirectoryCreateDirectory, directoryCreateDirectory ?? DefaultDirectoryCreateDirectory, directoryCreateDirectory != null);
        }

        private static void SetField<T>(ref T field, ref T testField, T value, bool setTestInplementation = false) where T : class
        {
            if (setTestInplementation)
            {
                field = null;
                testField = value;
            }
            else
            {
                field = value;
                testField = null;
            }
        }
    }
}
