// SharedMemoryStream (File: SharedMemoryStream\IO\SharedMemoryStreamReader.cs)
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
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;

namespace System.IO
{
    /// <summary>
    /// Wraps a <see cref="SharedMemoryStream"/> object and reads from it.  Deserializes binary data sent by a <see cref="SharedMemoryStreamWriter{T}"/>
    /// into a .NET CLR object specified by <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Reference type to deserialize data to</typeparam>
    public class SharedMemoryStreamReader<T> : IDisposable
    {
        private string _spinName = null;
        private readonly BinaryFormatter _binaryFormatter = new BinaryFormatter();
        private readonly NetSerializer.Serializer _fastBinaryFormatter = new NetSerializer.Serializer(new[] { typeof(T) });

        /// <summary>
        /// Gets the underlying <c>CircularBufferStream</c> object.
        /// </summary>
        public SharedMemoryStream BaseStream { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the shared memory stream is connected.
        /// </summary>
        public bool IsConnected { get; private set; }

        /// <summary>
        /// Constructs a new <c>SharedMemoryStreamReader</c> object that reads data from the given <paramref name="stream"/>.
        /// </summary>
        /// <param name="stream">Shared memory stream to read from</param>
        public SharedMemoryStreamReader(SharedMemoryStream stream)
        {
            BaseStream = stream;
            IsConnected = !stream.ShuttingDown;
            _spinName = stream.Name + "_reader";
        }

        /// <summary>
        /// Deserializes the specified object.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <returns></returns>
        /// <exception cref="SerializationException">An object in the graph of type parameter <typeparamref name="T" /> is not marked as serializable.</exception>
        private T Deserialize(byte[] obj)
        {
            if (typeof(T) == typeof(byte))
            {
                // Type is byte.
                return (T)(object)obj[0];
            }
            else if (typeof(T) == typeof(byte[]))
            {
                // Type is array of byte.
                return (T)(object)obj;
            }
            else if (typeof(T) == typeof(string))
            {
                // Type is string.
                char[] chars = new char[obj.Length / sizeof(char)];
                System.Buffer.BlockCopy(obj, 0, chars, 0, obj.Length);
                return (T)(object)new string(chars);
            }
            else
            {
                // Type is something else.
                try
                {
                    using (var memoryStream = new MemoryStream(obj))
                    {
                        return (T)_fastBinaryFormatter.Deserialize(memoryStream);
                    }
                }
                catch
                {
                    // if fast serialization did not work, try slow .net one.
                    using (var memoryStream = new MemoryStream(obj))
                    {
                        return (T)_binaryFormatter.Deserialize(memoryStream);
                    }
                }
            }
        }

        /// <summary>
        /// Reads the length of the next message (in bytes) from the client.
        /// </summary>
        /// <returns>Number of bytes of data the client will be sending.</returns>
        /// <exception cref="InvalidOperationException">The shared memory stream is disconnected, waiting to connect, or the handle has not been set.</exception>
        /// <exception cref="IOException">Any I/O error occurred.</exception>
        private int ReadLength()
        {
            const int lensize = sizeof(int);
            var lenbuf = new byte[lensize];
            var bytesRead = BaseStream.Read(lenbuf, 0, lensize);
            if (bytesRead == 0)
            {
                IsConnected = false;
                return 0;
            }
            if (bytesRead != lensize)
                throw new IOException(string.Format("Expected {0} bytes but read {1}", lensize, bytesRead));
            return IPAddress.NetworkToHostOrder(BitConverter.ToInt32(lenbuf, 0));
        }

        /// <summary>
        /// Reads the object of the given length.
        /// </summary>
        /// <param name="len">The length of the object to read.</param>
        /// <returns></returns>
        /// <exception cref="SerializationException">An object in the graph of type parameter <typeparamref name="T" /> is not marked as serializable.</exception>
        private T ReadObject(int len)
        {
            var data = new byte[len];
            
            int rd = 0;
            do
            {
                rd += BaseStream.Read(data, rd, len);                
            } while (rd < len);

            return Deserialize(data);
        }

        /// <summary>
        /// Reads the next object from the shared memory stream. This method blocks until an object is sent
        /// or the shared memory stream is disconnected.
        /// </summary>
        /// <returns>The next object read from the shared memory stream, or <c>null</c> if the shared memory stream disconnected.</returns>
        /// <exception cref="SerializationException">An object in the graph of type parameter <typeparamref name="T"/> is not marked as serializable.</exception>
        public T ReadObject()
        {
            if (DynamicSpin.Acquire(_spinName))
            {
                try
                {
                    int len = 0;
                    do
                    {
                        len = ReadLength();
                    } while (len == 0);

                    return ReadObject(len);
                }
                finally
                {
                    DynamicSpin.Release(_spinName);
                }
            }
            else
            {
                throw new TimeoutException("Unable to read the underlying stream, The read operation has timed out.");
            }
        }

        /// <summary>
        /// Closes this SharedMemoryStreamWriter and releases any system resources associated with the
        /// SharedMemoryStreamWriter. Following a call to Close, any operations on the SharedMemoryStreamWriter
        /// may raise exceptions. This default method is empty, but descendant
        /// classes can override the method to provide the appropriate
        /// functionality.
        /// </summary>
        public virtual void Close()
        {
            if (_spinName != null)
                DynamicSpin.Release(_spinName);
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        public void Dispose()
        {
            if (_spinName != null)
                DynamicSpin.Release(_spinName);
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
