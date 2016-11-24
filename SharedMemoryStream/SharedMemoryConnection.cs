using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.IO;

#if NET40Plus
using System.Collections.Concurrent;
#endif

namespace System.IO.SharedMemory
{
    /// <summary>
    /// Represents a connection between a shared memory client and server.
    /// </summary>
    /// <typeparam name="TRead">Reference type to read from the shared memory</typeparam>
    /// <typeparam name="TWrite">Reference type to write to the shared memory</typeparam>
    public class SharedMemoryConnection<TRead, TWrite>
        where TRead : class
        where TWrite : class
    {
        /// <summary>
        /// Gets the connection's unique identifier.
        /// </summary>
        public readonly int Id;

        /// <summary>
        /// Gets the connection's name.
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// Gets a value indicating whether the shared memory is connected or not.
        /// </summary>
        public bool IsConnected { get { return _streamWrapper.IsConnected; } }

        /// <summary>
        /// Invoked when the shared memory connection terminates.
        /// </summary>
        public event ConnectionEventHandler<TRead, TWrite> Disconnected;

        /// <summary>
        /// Invoked whenever a message is received from the other end of the shared memory.
        /// </summary>
        public event ConnectionMessageEventHandler<TRead, TWrite> ReceiveMessage;

        /// <summary>
        /// Invoked when an exception is thrown during any read/write operation over the shared memory.
        /// </summary>
        public event ConnectionExceptionEventHandler<TRead, TWrite> Error;

        private readonly SharedMemoryStreamWrapper<TRead, TWrite> _streamWrapper;

        private readonly AutoResetEvent _writeSignal = new AutoResetEvent(false);

        /// <summary>
        /// To support Multithread, we should use BlockingCollection.
        /// </summary>
        private readonly BlockingCollection<TWrite> _writeQueue = new BlockingCollection<TWrite>();

#if !NET40Plus
        /// <summary> 
        /// Provides blocking and bounding capabilities for thread-safe collections. 
        /// </summary>
        class BlockingCollection<T> where T : class
        {
            private readonly System.Collections.Queue _queue = Collections.Queue.Synchronized(new Collections.Queue());

            /// <summary>
            /// Adds the item to the <see cref="T:BlockingCollection{T}"/>.
            /// </summary>
            /// <param name="item">The item to be added to the collection. The value can be a null reference.</param>
            public void Add(T item)
            {
                _queue.Enqueue(item);
            }

            /// <summary>Takes an item from the <see cref="T:BlockingCollection{T}"/>.</summary>
            /// <returns>The item removed from the collection.</returns>
            public T Take()
            {
                return (T)_queue.Dequeue();
            }         
        }
#endif

        private bool _notifiedSucceeded;

        /// <summary>
        /// Initializes a new instance of the <see cref="SharedMemoryConnection{TRead, TWrite}"/> class.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="name">The name.</param>
        /// <param name="serverStream">The server stream.</param>
        internal SharedMemoryConnection(int id, string name, SharedMemoryStream serverStream)
        {
            Id = id;
            Name = name;
            _streamWrapper = new SharedMemoryStreamWrapper<TRead, TWrite>(serverStream);
        }

        /// <summary>
        /// Begins reading from and writing to the shared memory on a background thread.
        /// This method returns immediately.
        /// </summary>
        public void Open()
        {
            var readWorker = new Worker();
            readWorker.Succeeded += OnSucceeded;
            readWorker.Error += OnError;
            readWorker.DoWork(ReadSharedMemory);

            var writeWorker = new Worker();
            writeWorker.Succeeded += OnSucceeded;
            writeWorker.Error += OnError;
            writeWorker.DoWork(WriteSharedMemory);
        }

        /// <summary>
        /// Adds the specified <paramref name="message"/> to the write queue.
        /// The message will be written to the shared memory by the background thread
        /// at the next available opportunity.
        /// </summary>
        /// <param name="message"></param>
        public void PushMessage(TWrite message)
        {
            _writeQueue.Add(message);
            _writeSignal.Set();
        }

        /// <summary>
        /// Closes the connection and underlying <c>SharedMemoryStream</c>.
        /// </summary>
        public void Close()
        {
            _streamWrapper.Close();
            _writeSignal.Set();
        }

        /// <summary>
        ///     Invoked on the UI thread.
        /// </summary>
        private void OnSucceeded()
        {
            // Only notify observers once
            if (_notifiedSucceeded)
                return;

            _notifiedSucceeded = true;

            if (Disconnected != null)
                Disconnected(this);
        }

        /// <summary>
        ///     Invoked on the UI thread.
        /// </summary>
        /// <param name="exception"></param>
        private void OnError(Exception exception)
        {
            if (Error != null)
                Error(this, exception);
        }

        /// <summary>
        ///     Invoked on the background thread.
        /// </summary>
        /// <exception cref="SerializationException">An object in the graph of type parameter <typeparamref name="TRead"/> is not marked as serializable.</exception>
        private void ReadSharedMemory()
        {

            while (IsConnected && _streamWrapper.CanRead)
            {
                try
                {
                    var obj = _streamWrapper.ReadObject();
                    if (obj == null)
                    {
                        Close();
                        return;
                    }
                    if (ReceiveMessage != null)
                        ReceiveMessage(this, obj);
                }
                catch
                {
                    //we must igonre exception, otherwise, the wrapper will stop work.
                }
            }
            
        }

        /// <summary>
        ///     Invoked on the background thread.
        /// </summary>
        /// <exception cref="SerializationException">An object in the graph of type parameter <typeparamref name="TWrite"/> is not marked as serializable.</exception>
        private void WriteSharedMemory()
        {
            
                while (IsConnected && _streamWrapper.CanWrite)
                {
                    try
                    {
                        //using blockcollection, we needn't use singal to wait for result.
                        //_writeSignal.WaitOne();
                        //while (_writeQueue.Count > 0)
                        {
                            _streamWrapper.WriteObject(_writeQueue.Take());
                            _streamWrapper.WaitForSharedMemoryDrain();
                        }
                    }
                    catch
                    {
                    //we must igonre exception, otherwise, the wrapper will stop work.
                }
            }
          
        }
    }

    static class SharedMemoryConnectionFactory
    {
        private static int _lastId;

        public static SharedMemoryConnection<TRead, TWrite> CreateConnection<TRead, TWrite>(SharedMemoryStream sharedMemoryStream)
            where TRead : class
            where TWrite : class
        {
            return new SharedMemoryConnection<TRead, TWrite>(++_lastId, "Client " + _lastId, sharedMemoryStream);
        }
    }

    /// <summary>
    /// Handles new connections.
    /// </summary>
    /// <param name="connection">The newly established connection</param>
    /// <typeparam name="TRead">Reference type</typeparam>
    /// <typeparam name="TWrite">Reference type</typeparam>
    public delegate void ConnectionEventHandler<TRead, TWrite>(SharedMemoryConnection<TRead, TWrite> connection)
        where TRead : class
        where TWrite : class;

    /// <summary>
    /// Handles messages received from a named pipe.
    /// </summary>
    /// <typeparam name="TRead">Reference type</typeparam>
    /// <typeparam name="TWrite">Reference type</typeparam>
    /// <param name="connection">Connection that received the message</param>
    /// <param name="message">Message sent by the other end of the pipe</param>
    public delegate void ConnectionMessageEventHandler<TRead, TWrite>(SharedMemoryConnection<TRead, TWrite> connection, TRead message)
        where TRead : class
        where TWrite : class;

    /// <summary>
    /// Handles exceptions thrown during read/write operations.
    /// </summary>
    /// <typeparam name="TRead">Reference type</typeparam>
    /// <typeparam name="TWrite">Reference type</typeparam>
    /// <param name="connection">Connection that threw the exception</param>
    /// <param name="exception">The exception that was thrown</param>
    public delegate void ConnectionExceptionEventHandler<TRead, TWrite>(SharedMemoryConnection<TRead, TWrite> connection, Exception exception)
        where TRead : class
        where TWrite : class;
}
