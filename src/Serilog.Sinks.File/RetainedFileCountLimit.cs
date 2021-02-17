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
using System.Collections.Generic;

namespace Serilog
{
    /// <summary>
    /// Class representing a limitation of the number of files retained.
    /// This class allows on the fly changes of the number of files retained.
    /// The new retension count is thus applied when one of the following
    /// conditions is true:
    /// - A new File is created
    /// - The RetainedFileCount Observable emits a new value
    /// </summary>
    public class RetainedFileCountLimit
    {
        private List<Action<int>> observers = new List<Action<int>>();
        private int _retensionCount = 0;

        /// <summary>
        /// Property that allows to get and set the Retained File Count
        /// </summary>
        public int RetainedFileCount
        {
            get => _retensionCount;
            set
            {
                if (value < 1) throw new ArgumentException("Zero or negative value provided; retained file count limit must be at least 1");

                _retensionCount = value;
                foreach (var observer in observers)
                {
                    observer(_retensionCount);
                }
            }
        }

        /// <summary>
        /// Constructor of the TetainedFileCountLimit class that specifies an initial retension count value
        /// </summary>
        /// <param name="initialCount"></param>
        public RetainedFileCountLimit(int initialCount)
        {
            _retensionCount = initialCount;
        }

        /// <summary>
        /// Subscribe for Retained File Count changes
        /// </summary>
        /// <param name="observer">Action that should be called to inform about a change</param>
        public void Subscribe(Action<int> observer)
        {
            if (!observers.Contains(observer))
                observers.Add(observer);
        }

        /// <summary>
        /// Unsubscribe for Retained File Count changes
        /// </summary>
        /// <param name="observer">Action that should be unsubscribed from changes</param>
        public void Unsubscribe(Action<int> observer)
        {
            if (observers.Contains(observer))
                observers.Remove(observer);
        }

        /// <summary>
        /// Allow implicit conversion from int to RetainedFileCountLimit
        /// </summary>
        /// <param name="value"></param>
        public static implicit operator RetainedFileCountLimit(int value) => new RetainedFileCountLimit(value);
    }
}
