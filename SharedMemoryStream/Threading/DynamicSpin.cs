// SharedMemoryStream (File: SharedMemoryStream\Threading\DynamicSpin.cs)
// Copyright (c) 2016 Laurent Le Guillermic
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

namespace System.Threading
{
    /// <summary>
    /// Provides atomic dynamic interlocked spin.
    /// </summary>
    public static class DynamicSpin
    {
        private static Dictionary<string, bool> _index = new Dictionary<string, bool>();
        private static int _lockIndex;

        /// <summary>
        /// Waits until available and then acquires the given spin name.
        /// </summary>
        /// <param name="spinName">Name of the spin.</param>
        /// <param name="timeout">The timeout.</param>
        /// <returns>Returns true if the spin has been acquired before timeout; otherwise, false.</returns>
        public static bool Acquire(string spinName, int timeout = 30000)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            while (CompareExchange(spinName, true, false))
            {
                if (sw.ElapsedMilliseconds > timeout)
                    return false;

                Thread.Sleep(1);
            }

            //Debug.WriteLine(spinName + " -> Acquired", "Debug");

            return true;
        }

        /// <summary>
        /// Releases the specified spin.
        /// </summary>
        /// <param name="spinName">Name of the spin.</param>
        public static void Release(string spinName)
        {
            CompareExchange(spinName, false, true);
            //Debug.WriteLine(spinName + " -> Released", "Debug");
        }

        /// <summary>
        /// Releases all spins.
        /// </summary>
        public static void ReleaseAll()
        {
            try
            {
                // Spin until the "lock" is released.
                while (Interlocked.CompareExchange(ref _lockIndex, 1, 0) == 0)
                {
                    Thread.Sleep(1);
                }

                _index.Clear();
            }
            finally
            {
                // Avoid dead lock.
                Interlocked.Exchange(ref _lockIndex, 0);
            }
        }

        /// <summary>
        /// For the given key, compares two 32-bit signed integers for equality and, if they are equal,
        /// replaces one of the values.
        /// </summary>
        /// <param name="key">The destination key, whose value is compared with comparand and possibly replaced.</param>
        /// <param name="value">The value that replaces the destination value if the comparison results in equality.</param>
        /// <param name="comparand">The value that is compared to the value at key location.</param>
        /// <returns>The original value in the key.</returns>
        private static bool CompareExchange(string key, bool value, bool comparand)
        {
            try
            {
                // Spin until the "lock" is released.
                while (Interlocked.CompareExchange(ref _lockIndex, 1, 0) == 0)
                {
                    Thread.Sleep(1);
                }

                bool ret;
                if (!_index.TryGetValue(key, out ret))
                {
                    _index.Add(key, value);
                    ret = comparand;
                }
                else
                {
                    _index[key] = value;
                }

                return ret;
            }
            finally
            {
                // Avoid dead lock.
                Interlocked.Exchange(ref _lockIndex, 0);
            }
        }
    }
}
