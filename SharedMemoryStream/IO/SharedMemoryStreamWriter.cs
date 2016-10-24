// SharedMemoryStream (File: SharedMemoryStream\IO\SharedMemoryStreamWriter.cs)
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
    /// Wraps a <see cref="SharedMemoryStream"/> object and writes to it. Serializes .NET CLR objects specified by <typeparamref name="T"/>
    /// into binary form and writes them into a shared memory for a <see cref="SharedMemoryStreamReader{T}"/> to read and deserialize.
    /// </summary>
    /// <typeparam name="T">Reference type to serialize</typeparam>
    public class SharedMemoryStreamWriter<T> : IDisposable
    {
        private string _spinName = null;
        private readonly BinaryFormatter _binaryFormatter = new BinaryFormatter();
        private readonly NetSerializer.Serializer _fastBinaryFormatter = new NetSerializer.Serializer(new[] { typeof(T) });

        /// <summary>
        /// Gets the underlying <c>CircularBufferStream</c> object.
        /// </summary>
        public SharedMemoryStream BaseStream { get; private set; }

        /// <summary>
        /// Constructs a new <c>SharedMemoryStreamWriter</c> object that writes to given <paramref name="stream"/>.
        /// </summary>
        /// <param name="stream">Shared memory tream to write to</param>
        public SharedMemoryStreamWriter(SharedMemoryStream stream)
        {
            BaseStream = stream;
            _spinName = stream.Name + "_writer";
        }

        /// <summary>
        /// Serializes the specified object.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <returns></returns>
        /// <exception cref="SerializationException">An object in the graph of type parameter <typeparamref name="T" /> is not marked as serializable.</exception>
        private byte[] Serialize(T obj)
        {
            if (typeof(T) == typeof(byte))
            {
                // Type is byte.
                return new byte[] { (byte)(object)obj };
            }
            else if (typeof(T) == typeof(byte[]))
            {
                // Type is array of byte.
                return (byte[])(object)obj;
            }
            else if (typeof(T) == typeof(string))
            {
                // Type is string.
                string str = (string)(object)obj;
                byte[] bytes = new byte[str.Length * sizeof(char)];
                System.Buffer.BlockCopy(str.ToCharArray(), 0, bytes, 0, bytes.Length);
                return bytes;
            }
            else
            {
                // Type is something else.
                try
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        _fastBinaryFormatter.Serialize(memoryStream, obj);
                        return memoryStream.ToArray();
                    }
                }
                catch
                {
                    // if fast serialization did not work, try slow .net one.
                    try
                    {
                        using (var memoryStream = new MemoryStream())
                        {
                            _binaryFormatter.Serialize(memoryStream, obj);
                            return memoryStream.ToArray();
                        }
                    }
                    catch
                    {
                        //if any exception in the serialize, it will stop wrapper, so there will ignore any exception.
                        return null;
                    }
                }
            }
        }

        /// <summary>
        /// Calculates the number of nodes to use.
        /// </summary>
        /// <param name="realSize">Size of the object.</param>
        /// <returns></returns>
        private int CalculateNodeToUse(int realSize)
        {
            return (int)Math.Ceiling((double)realSize / BaseStream.NodeBufferSize);
        }

        /// <summary>
        /// Clears all buffers for this SharedMemoryStreamWriter and causes any buffered data to be
        /// written to the underlying device. This default method is empty, but
        /// descendant classes can override the method to provide the appropriate
        /// functionality.
        /// </summary>
        public void Flush()
        {
            BaseStream.Flush();
        }

        /// <summary>
        /// Tries to write an object to the shared memory stream.
        /// </summary>
        /// <param name="obj">Object to write to the shared memory stream</param>
        /// <param name="nodeCount">The node count.</param>
        /// <returns>
        /// True if the writes occured; otherwise false.
        /// </returns>
        /// <exception cref="SerializationException">An object in the graph of type parameter <typeparamref name="T" /> is not marked as serializable.</exception>
        private bool TryWriteObject(T obj, out int nodeCount)
        {
            var data = Serialize(obj);
            var lenbuf = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(data.Length));
            nodeCount = CalculateNodeToUse(lenbuf.Length) + CalculateNodeToUse(data.Length);

            // Atomic operation
            if (DynamicSpin.Acquire(_spinName))
            {
                try
                {
                    // Writes length of the data followed by the data, so we will know how many data de read.
                    BaseStream.Write(lenbuf, 0, lenbuf.Length);
                    BaseStream.Write(data, 0, data.Length);
                    Flush();
                    return true;
                }
                finally
                {
                    DynamicSpin.Release(_spinName);
                }
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Tries to write an object to the shared memory stream.
        /// </summary>
        /// <param name="obj">Object to write to the shared memory stream</param>
        /// <returns>
        /// True if the writes occured; otherwise false.
        /// </returns>
        /// <exception cref="SerializationException">An object in the graph of type parameter <typeparamref name="T" /> is not marked as serializable.</exception>
        public bool TryWriteObject(T obj)
        {
            int nodeCount;
            return TryWriteObject(obj, out nodeCount);
        }

        /// <summary>
        /// Writes an object to the shared memory stream. This method blocks until all data is sent.
        /// </summary>
        /// <param name="obj">Object to write to the shared memory stream</param>
        /// <exception cref="System.IO.IOException">Unable to write data into the stream, there is not enougth free space.</exception>
        /// <exception cref="SerializationException">An object in the graph of type parameter <typeparamref name="T" /> is not marked as serializable.</exception>
        public void WriteObject(T obj)
        {
            int nodeCount;
            if (!TryWriteObject(obj, out nodeCount))
                throw new IOException("Unable to write data into the stream, there is not enougth free space. (Data to write: " + nodeCount * BaseStream.NodeBufferSize + " bytes, Free space available: " + BaseStream.FreeNodeCount + "x" + BaseStream.NodeBufferSize + "=" + BaseStream.FreeNodeCount * BaseStream.NodeBufferSize + " bytes)");
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