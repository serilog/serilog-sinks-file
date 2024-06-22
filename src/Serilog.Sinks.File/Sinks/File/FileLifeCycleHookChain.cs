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

using System.Text;

namespace Serilog.Sinks.File;

sealed class FileLifeCycleHookChain : FileLifecycleHooks
{
    readonly FileLifecycleHooks _first;
    readonly FileLifecycleHooks _second;

    public FileLifeCycleHookChain(FileLifecycleHooks first, FileLifecycleHooks second)
    {
        _first = first ?? throw new ArgumentNullException(nameof(first));
        _second = second ?? throw new ArgumentNullException(nameof(second));
    }

    public override Stream OnFileOpened(string path, Stream underlyingStream, Encoding encoding)
    {
        var firstStreamResult = _first.OnFileOpened(path, underlyingStream, encoding);
        var secondStreamResult = _second.OnFileOpened(path, firstStreamResult, encoding);

        return secondStreamResult;
    }

    public override void OnFileDeleting(string path)
    {
        _first.OnFileDeleting(path);
        _second.OnFileDeleting(path);
    }
}
