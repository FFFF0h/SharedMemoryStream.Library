// SharedMemoryStream (File: SharedMemoryStream\IO\SharedMemoryStreamWrapper.cs)
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
using System.IO;
using System.IO.SharedMemory;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace System.IO
{
    /// <summary>
    /// Wraps a <see cref="SharedMemoryStream"/> object to read and write .NET CLR objects.
    /// </summary>
    /// <typeparam name="TReadWrite">Reference type to read from and write to the pipe</typeparam>
    public class SharedMemoryStreamWrapper<TReadWrite> : SharedMemoryStreamWrapper<TReadWrite, TReadWrite>
        where TReadWrite : class
    {
        /// <summary>
        /// Constructs a new <c>SharedMemoryStreamWrapper</c> object that reads from and writes to the given <paramref name="stream"/>.
        /// </summary>
        /// <param name="stream">Stream to read from and write to</param>
        public SharedMemoryStreamWrapper(SharedMemoryStream stream)
            : base(stream)
        {
        }
    }

    /// <summary>
    /// Wraps a <see cref="SharedMemoryStream"/> object to read and write .NET CLR objects.
    /// </summary>
    /// <typeparam name="TRead">Reference type to <b>read</b> from the stream</typeparam>
    /// <typeparam name="TWrite">Reference type to <b>write</b> to the stream</typeparam>
    public class SharedMemoryStreamWrapper<TRead, TWrite>
        where TRead : class
        where TWrite : class
    {
        /// <summary>
        /// Gets the underlying <c>CircularBufferStream</c> object.
        /// </summary>
        public SharedMemoryStream BaseStream { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether the <see cref="BaseStream"/> object is connected or not.
        /// </summary>
        /// <returns>
        ///     <c>true</c> if the <see cref="BaseStream"/> object is connected; otherwise, <c>false</c>.
        /// </returns>
        public bool IsConnected
        {
            get { return BaseStream.ShuttingDown && _reader.IsConnected; }
        }

        /// <summary>
        ///     Gets a value indicating whether the current stream supports read operations.
        /// </summary>
        /// <returns>
        ///     <c>true</c> if the stream supports read operations; otherwise, <c>false</c>.
        /// </returns>
        public bool CanRead
        {
            get { return BaseStream.CanRead; }
        }

        /// <summary>
        ///     Gets a value indicating whether the current stream supports write operations.
        /// </summary>
        /// <returns>
        ///     <c>true</c> if the stream supports write operations; otherwise, <c>false</c>.
        /// </returns>
        public bool CanWrite
        {
            get { return BaseStream.CanWrite; }
        }

        private readonly SharedMemoryStreamReader<TRead> _reader;
        private readonly SharedMemoryStreamWriter<TWrite> _writer;

        /// <summary>
        /// Constructs a new <c>SharedMemoryStreamWrapper</c> object that reads from and writes to the given <paramref name="stream"/>.
        /// </summary>
        /// <param name="stream">Shared memory stream to read from and write to</param>
        public SharedMemoryStreamWrapper(SharedMemoryStream stream)
        {
            BaseStream = stream;
            _reader = new SharedMemoryStreamReader<TRead>(BaseStream);
            _writer = new SharedMemoryStreamWriter<TWrite>(BaseStream);
        }

        /// <summary>
        /// Reads the next object from the shared memory stream. This method blocks until an object is sent
        /// or the stream is disconnected.
        /// </summary>
        /// <returns>The next object read from the shared memory stream, or <c>null</c> if the shared memory stream disconnected.</returns>
        /// <exception cref="SerializationException">An object in the graph of type parameter <typeparamref name="TRead"/> is not marked as serializable.</exception>
        public TRead ReadObject()
        {
            return _reader.ReadObject();
        }

        /// <summary>
        /// Writes an object to the shared memory stream. This method blocks until all data is sent.
        /// </summary>
        /// <param name="obj">Object to write to the shared memory stream</param>
        /// <exception cref="SerializationException">An object in the graph of type parameter <typeparamref name="TRead"/> is not marked as serializable.</exception>
        public void WriteObject(TWrite obj)
        {
            _writer.WriteObject(obj);
        }

        /// <summary>
        ///     Waits for the other end of the shared memory stream to read all sent bytes.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The shared memory stream is closed.</exception>
        /// <exception cref="NotSupportedException">The shared memory stream does not support write operations.</exception>
        /// <exception cref="IOException">The shared memory stream is broken or another I/O error occurred.</exception>
        public void WaitForSharedMemoryDrain()
        {
            //_writer.WaitForShareMemoryDrain();
        }

        /// <summary>
        ///     Closes the current stream and releases any resources (such as sockets and file handles) associated with the current stream.
        /// </summary>
        public void Close()
        {
            BaseStream.Close();
        }
    }
}
