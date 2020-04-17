// Copyright 2019 Serilog Contributors
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
using System.Text;

namespace Serilog.Sinks.File
{
    /// <summary>
    /// FileLifecycleHooks extension methods
    /// </summary>
    public static class FileLifecycleHooksExtensions
    {
        /// <summary>
        /// Creates a chain of <see cref="FileLifecycleHooks"/> that have their methods called sequentially
        /// Can be used to compose <see cref="FileLifecycleHooks"/> together; e.g. add header information to each log file and
        /// compress it.
        /// </summary>
        /// <example>
        /// <code>
        /// var hooks = new GZipHooks().ChainTo(new HeaderWriter("File Header"));
        /// </code>
        /// </example>
        /// <param name="first">The first <see cref="FileLifecycleHooks"/> to have its methods called in the chain</param>
        /// <param name="second">The second <see cref="FileLifecycleHooks"/> to have its methods called in the chain</param>
        /// <returns></returns>
        public static FileLifecycleHooks ChainTo(this FileLifecycleHooks first, FileLifecycleHooks second)
        {
            return new FileLifeCycleHookChain(first, second);
        }

        class FileLifeCycleHookChain : FileLifecycleHooks
        {
            private readonly FileLifecycleHooks[] hooks;

            public FileLifeCycleHookChain(params FileLifecycleHooks[] hooks)
            {
                this.hooks = hooks ?? throw new ArgumentNullException(nameof(hooks));
            }

            public override Stream OnFileOpened(Stream underlyingStream, Encoding encoding)
            {
                for (int i = 0; i < hooks.Length; i++)
                {
                    underlyingStream = hooks[i].OnFileOpened(underlyingStream, encoding);
                }
                return underlyingStream;
            }

            public override void OnFileDeleting(string path)
            {
                for (int i = 0; i < hooks.Length; i++)
                {
                    hooks[i].OnFileDeleting(path);
                }
            }
        }
    }
}
