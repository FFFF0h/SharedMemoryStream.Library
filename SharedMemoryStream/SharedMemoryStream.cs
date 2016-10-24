// SharedMemoryStream (File: SharedMemoryStream\SharedMemoryStream.cs)
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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace System.IO.SharedMemory
{
    /// <summary>
    /// A stream based on the memory mapped circular buffer. Allows to share data between process without lock.
    /// </summary>
    public class SharedMemoryStream : Stream, IDisposable
    {
        private int _readTimeout = 1000;
        private int _writeTimeout = 1000;
        private CircularBuffer _circularBuffer;
        private string _spinNameRead = null;
        private string _spinNameWrite = null;

        #region Constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="SharedMemoryStream"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="nodeCount">The node.</param>
        /// <param name="nodeBufferSize">Size of the buffer.</param>
        public SharedMemoryStream(string name, int nodeCount = 1024, int nodeBufferSize = 4096)
        {
            _spinNameRead = name + "_internal_read";
            _spinNameWrite = name + "_internal_write";

            try
            {
                _circularBuffer = new CircularBuffer(name);
            }
            catch (FileNotFoundException)
            {
                _circularBuffer = new CircularBuffer(name, nodeCount, nodeBufferSize);
            }
        }
        #endregion

        #region Stream Properties
        /// <summary>
        /// When overridden in a derived class, gets a value indicating whether the current stream supports reading.
        /// </summary>
        public override bool CanRead
        {
            get { return _circularBuffer != null && !_circularBuffer.ShuttingDown; }
        }

        /// <summary>
        /// When overridden in a derived class, gets a value indicating whether the current stream supports seeking.
        /// </summary>
        /// <exception cref="System.NotImplementedException"></exception>
        public override bool CanSeek
        {
            get { return false; }
        }

        /// <summary>
        /// When overridden in a derived class, gets a value indicating whether the current stream supports writing.
        /// </summary>
        public override bool CanWrite
        {
            get { return _circularBuffer != null && !_circularBuffer.ShuttingDown; }
        }

        /// <summary>
        /// Gets a value that determines whether the current stream can time out.
        /// </summary>
        public override bool CanTimeout
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Gets or sets a value, in miliseconds, that determines how long the stream will attempt to read before timing out.
        /// </summary>
        public override int ReadTimeout
        {
            get
            {
                return _readTimeout;
            }
            set
            {
                _readTimeout = value;
            }
        }

        /// <summary>
        /// Gets or sets a value, in miliseconds, that determines how long the stream will attempt to write before timing out.
        /// </summary>
        public override int WriteTimeout
        {
            get
            {
                return _writeTimeout;
            }
            set
            {
                _writeTimeout = value;
            }
        }

        /// <summary>
        /// Clears all buffers for this stream and causes any buffered data to be written to the underlying device.
        /// </summary>
        public override void Flush()
        {
        }

        /// <summary>
        /// When overridden in a derived class, gets the length in bytes of the stream.
        /// </summary>
        public override long Length
        {
            get { return _circularBuffer.NodeBufferSize * (_circularBuffer.NodeCount - 1); }
        }

        /// <summary>
        /// When overridden in a derived class, gets or sets the position within the current stream.
        /// </summary>
        /// <exception cref="System.NotImplementedException">
        /// </exception>
        public override long Position
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// When overridden in a derived class, sets the position within the current stream.
        /// </summary>
        /// <param name="offset">A byte offset relative to the <paramref name="origin" /> parameter.</param>
        /// <param name="origin">A value of type <see cref="T:System.IO.SeekOrigin" /> indicating the reference point used to obtain the new position.</param>
        /// <returns>
        /// The new position within the current stream.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// When overridden in a derived class, sets the length of the current stream.
        /// </summary>
        /// <param name="value">The desired length of the current stream in bytes.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }
        #endregion

        #region Specific Properties
        /// <summary>
        /// Gets a value indicating whether this buffer is shutting down.
        /// </summary>
        /// <value>
        /// <c>true</c> if this buffer is shuting down; otherwise, <c>false</c>.
        /// </value>
        public bool ShuttingDown
        {
            get { return _circularBuffer != null && !_circularBuffer.ShuttingDown; }
        }

        /// <summary>
        /// Indicates whether this instance owns the shared memory (i.e. creator of the shared memory)
        /// </summary>
        public bool IsOwnerOfSharedMemory
        {
            get { return _circularBuffer != null && _circularBuffer.IsOwnerOfSharedMemory; }
        }

        /// <summary>
        /// Gets the node count of the underlying circular buffer.
        /// </summary>
        /// <value>
        /// The node count.
        /// </value>
        public int NodeCount
        {
            get
            {
                if (_circularBuffer != null)
                {
                    return _circularBuffer.NodeCount;
                }

                return 0;
            }
        }

        /// <summary>
        /// Gets the size of a node of the underlying circular buffer.
        /// </summary>
        /// <value>
        /// The size of a node.
        /// </value>
        public int NodeBufferSize
        {
            get
            {
                if (_circularBuffer != null)
                {
                    return _circularBuffer.NodeBufferSize;
                }

                return 0;
            }

        }

        /// <summary>
        /// Gets the free node count of the underlying circular buffer.
        /// </summary>
        /// <value>
        /// The free node count.
        /// </value>
        public int FreeNodeCount
        {
            get
            {
                if (_circularBuffer != null)
                    return _circularBuffer.FreeNodeCount;

                return 0;
            }
        }

        /// <summary>
        /// Gets the name of the stream.
        /// </summary>
        /// <value>
        /// The stream name.
        /// </value>
        public string Name
        {
            get
            {
                if (_circularBuffer != null)
                    return _circularBuffer.Name;

                return null;
            }
        }
#endregion

        #region Read/Write
        /// <summary>
        /// Reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.
        /// </summary>
        /// <param name="buffer">An array of bytes. When this method returns, the buffer contains the specified byte array with the values between <paramref name="offset" /> and (<paramref name="offset" /> + <paramref name="count" /> - 1) replaced by the bytes read from the current source.</param>
        /// <param name="offset">The zero-based byte offset in <paramref name="buffer" /> at which to begin storing the data read from the current stream.</param>
        /// <param name="count">The maximum number of bytes to be read from the current stream.</param>
        /// <returns>
        /// The total number of bytes read into the buffer. This can be less than the number of bytes requested if that many bytes are not currently available, or zero (0) if the end of the stream has been reached.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">buffer</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">count or offset</exception>
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");
            if (count < 0)
                throw new ArgumentOutOfRangeException("count");
            if (offset < 0)
                throw new ArgumentOutOfRangeException("offset");
            if (!CanRead)
                throw new NotSupportedException("Read is not supported.");

            int red = 0;
            bool hasTimedOut = false;

            // Enter critial path
            Thread.BeginCriticalRegion();

            // Wait for other threads to finish writing
            if (DynamicSpin.Acquire(_spinNameRead))
            {    
                try
                {
                    Stopwatch sw = new Stopwatch();
                    sw.Start();
                    while (red < count && _circularBuffer.HasNodeToRead && !_circularBuffer.ShuttingDown)
                    {
                        int rd = _circularBuffer.Read(buffer, offset, _readTimeout);
                        offset += rd;
                        red += rd;

                        // Reads timeout
                        if (sw.ElapsedMilliseconds > _readTimeout)
                        {
                            hasTimedOut = true;
                            break;
                        }
#if DEBUG                            
                        Debug.WriteLine(DateTime.Now.ToLongTimeString() + " - Reading: " + rd + " bytes, free nodes: " + _circularBuffer.FreeNodeCount, "Debug");
#endif
                    }
                    sw.Stop();
                }
                finally
                {
                    // Be shure to release the spin.
                    DynamicSpin.Release(_spinNameRead);
                }
            }
            Thread.EndCriticalRegion();

            if (hasTimedOut)
                throw new TimeoutException(string.Format("Waited {0} miliseconds", _readTimeout));


#if DEBUG
            if (red != 0)
                Debug.WriteLine(red + " byte(s) have been red from the underlying circular buffer.", "Information");
#endif

            return red;
        }

        /// <summary>
        /// Writes a sequence of bytes to the current stream and advances the current position within this stream by the number of bytes written.
        /// </summary>
        /// <param name="buffer">An array of bytes. This method copies <paramref name="count" /> bytes from <paramref name="buffer" /> to the current stream.</param>
        /// <param name="offset">The zero-based byte offset in <paramref name="buffer" /> at which to begin copying bytes to the current stream.</param>
        /// <param name="count">The number of bytes to be written to the current stream.</param>
        /// <exception cref="System.ArgumentNullException">buffer</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">count or offset</exception>
        /// <exception cref="System.IO.IOException">If there is not enougth free space to write data.</exception>
        /// <exception cref="System.OutOfMemoryException">If the underlying buffer is full.</exception>
        /// <exception cref="System.TimeoutException">If it exceed the time allowed to write data.</exception>
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");
            if (count < 0)
                throw new ArgumentOutOfRangeException("count");
            if (offset < 0)
                throw new ArgumentOutOfRangeException("offset");
            if (!CanWrite)
                throw new NotSupportedException("Write is not supported.");

            byte[] tempBuffer;
            if (count < buffer.Length - offset)
            {
                tempBuffer = new byte[count];
                Array.Copy(buffer, offset, tempBuffer, 0, count);
            }
            else
            {
                tempBuffer = buffer;
            }

            int written = 0;
            int toWrite = tempBuffer.Length - offset;

#if DEBUG
            int freeSize = (_circularBuffer.FreeNodeCount) * _circularBuffer.NodeBufferSize;
            Debug.WriteLine("Buffer to write: " + tempBuffer.Length + " bytes, Free space available: " + _circularBuffer.FreeNodeCount + "x" + _circularBuffer.NodeBufferSize + "=" + freeSize + " bytes", "Information");
#endif

            bool hasTimedOut = false;

            // Enter critial path
            Thread.BeginCriticalRegion();

            // Wait for other threads to finish writing
            if (DynamicSpin.Acquire(_spinNameWrite))
            {
                try
                {
                    // Writes into the buffer, if the buffer is full, it will wait until new node are freed.
                    Stopwatch sw = new Stopwatch();
                    sw.Start();
                    while (written < toWrite && !_circularBuffer.ShuttingDown)
                    {
                        int wr = _circularBuffer.Write(tempBuffer, offset, _readTimeout);
                        offset += wr;
                        written += wr;

                        // Writes timeout
                        if (sw.ElapsedMilliseconds > _writeTimeout)
                        {
                            hasTimedOut = true;
                            break;
                        }

#if DEBUG
                        Debug.WriteLine(DateTime.Now.ToLongTimeString() + " - Writing: " + wr + " bytes, free nodes: " + _circularBuffer.FreeNodeCount, "Debug");
#endif
                    }
                    sw.Stop();
                }
                finally
                {
                    // Be shure to release the spin.
                    DynamicSpin.Release(_spinNameWrite);
                }
            }
            Thread.EndCriticalRegion();

            if (hasTimedOut)
                throw new TimeoutException(string.Format("Waited {0} miliseconds", _readTimeout));

#if DEBUG
            if (written != 0)
                Debug.WriteLine(written + " byte(s) have been written to the underlying circular buffer.", "Information");
#endif
        }
#endregion

        #region Dispose
        /// <summary>
        /// Releases all resources used by the <see cref="T:System.IO.Stream" />.
        /// </summary>
        void IDisposable.Dispose()
        {
            if (_spinNameRead != null)
                DynamicSpin.Release(_spinNameRead);
            if (_spinNameWrite != null)
                DynamicSpin.Release(_spinNameWrite);

            if (_circularBuffer != null)
            {
                _circularBuffer.Dispose();
            }
        }
        #endregion
    }
}
