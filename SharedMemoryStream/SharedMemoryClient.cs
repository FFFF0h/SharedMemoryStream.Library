﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;

namespace System.IO.SharedMemory
{
    /// <summary>
    /// Wraps a <see cref="NamedPipeClientStream"/>.
    /// </summary>
    /// <typeparam name="TReadWrite">Reference type to read from and write to the named pipe</typeparam>
    public class SharedMemoryClient<TReadWrite> : SharedMemoryClient<TReadWrite, TReadWrite> where TReadWrite : class
    {
        /// <summary>
        /// Constructs a new <c>NamedPipeClient</c> to connect to the <see cref="SharedMemoryServer{TReadWrite}"/> specified by <paramref name="pipeName"/>.
        /// </summary>
        /// <param name="pipeName">Name of the server's pipe</param>
        /// <param name="serverName">server name default is local.</param>
        public SharedMemoryClient(string pipeName, string serverName = ".")
            : base(pipeName, serverName)
        {
        }
    }

    /// <summary>
    /// Wraps a <see cref="NamedPipeClientStream"/>.
    /// </summary>
    /// <typeparam name="TRead">Reference type to read from the named pipe</typeparam>
    /// <typeparam name="TWrite">Reference type to write to the named pipe</typeparam>
    public class SharedMemoryClient<TRead, TWrite>
        where TRead : class
        where TWrite : class
    {
        /// <summary>
        /// Gets or sets whether the client should attempt to reconnect when the pipe breaks
        /// due to an error or the other end terminating the connection.
        /// Default value is <c>true</c>.
        /// </summary>
        public bool AutoReconnect { get; set; }

        /// <summary>
        /// Invoked whenever a message is received from the server.
        /// </summary>
        public event ConnectionMessageEventHandler<TRead, TWrite> ServerMessage;

        /// <summary>
        /// Invoked when the client disconnects from the server (e.g., the pipe is closed or broken).
        /// </summary>
        public event ConnectionEventHandler<TRead, TWrite> Disconnected;

        /// <summary>
        /// Invoked whenever an exception is thrown during a read or write operation on the named pipe.
        /// </summary>
        public event SharedMemoryExceptionEventHandler Error;

        private readonly string _pipeName;
        private SharedMemoryConnection<TRead, TWrite> _connection;

        private readonly AutoResetEvent _connected = new AutoResetEvent(false);
        private readonly AutoResetEvent _disconnected = new AutoResetEvent(false);

        private volatile bool _closedExplicitly;
        /// <summary>
        /// the server name, which client will connect to.
        /// </summary>
        private string _serverName { get; set; }

        /// <summary>
        /// Constructs a new <c>NamedPipeClient</c> to connect to the <see cref="SharedMemoryServer{TReadWrite}"/> specified by <paramref name="pipeName"/>.
        /// </summary>
        /// <param name="pipeName">Name of the server's pipe</param>
        /// <param name="serverName">the Name of the server, default is  local machine</param>
        public SharedMemoryClient(string pipeName, string serverName)
        {
            _pipeName = pipeName;
            _serverName = serverName;
            AutoReconnect = true;
        }

        /// <summary>
        /// Connects to the named pipe server asynchronously.
        /// This method returns immediately, possibly before the connection has been established.
        /// </summary>
        public void Start()
        {
            _closedExplicitly = false;
            var worker = new Worker();
            worker.Error += OnError;
            worker.DoWork(ListenSync);
        }

        /// <summary>
        ///     Sends a message to the server over a named pipe.
        /// </summary>
        /// <param name="message">Message to send to the server.</param>
        public void PushMessage(TWrite message)
        {
            if (_connection != null)
                _connection.PushMessage(message);
        }

        /// <summary>
        /// Closes the named pipe.
        /// </summary>
        public void Stop()
        {
            _closedExplicitly = true;
            if (_connection != null)
                _connection.Close();
        }

        #region Wait for connection/disconnection

        /// <summary>
        /// Waits for connection.
        /// </summary>
        public void WaitForConnection()
        {
            _connected.WaitOne();
        }

        /// <summary>
        /// Waits for connection.
        /// </summary>
        /// <param name="millisecondsTimeout">The milliseconds timeout.</param>
        public void WaitForConnection(int millisecondsTimeout)
        {
            _connected.WaitOne(millisecondsTimeout);
        }

        /// <summary>
        /// Waits for connection.
        /// </summary>
        /// <param name="timeout">The timeout.</param>
        public void WaitForConnection(TimeSpan timeout)
        {
            _connected.WaitOne(timeout);
        }

        /// <summary>
        /// Waits for disconnection.
        /// </summary>
        public void WaitForDisconnection()
        {
            _disconnected.WaitOne();
        }

        /// <summary>
        /// Waits for disconnection.
        /// </summary>
        /// <param name="millisecondsTimeout">The milliseconds timeout.</param>
        public void WaitForDisconnection(int millisecondsTimeout)
        {
            _disconnected.WaitOne(millisecondsTimeout);
        }

        /// <summary>
        /// Waits for disconnection.
        /// </summary>
        /// <param name="timeout">The timeout.</param>
        public void WaitForDisconnection(TimeSpan timeout)
        {
            _disconnected.WaitOne(timeout);
        }

        #endregion

        #region Private methods

        private void ListenSync()
        {
            // Get the name of the data channel that should be used from now on by this.
            var handshake = SharedMemoryClientFactory.Connect<string, string>(_pipeName);
            var dataName = handshake.ReadObject();
            handshake.Close();

            // Connect to the actual data pipe
            var data = SharedMemoryClientFactory.CreateAndConnect(dataName);

            // Create a Connection object for the data pipe
            _connection = SharedMemoryConnectionFactory.CreateConnection<TRead, TWrite>(data);
            _connection.Disconnected += OnDisconnected;
            _connection.ReceiveMessage += OnReceiveMessage;
            _connection.Error += ConnectionOnError;
            _connection.Open();

            _connected.Set();
        }

        private void OnDisconnected(SharedMemoryConnection<TRead, TWrite> connection)
        {
            if (Disconnected != null)
                Disconnected(connection);

            _disconnected.Set();

            // Reconnect
            if (AutoReconnect && !_closedExplicitly)
                Start();
        }

        private void OnReceiveMessage(SharedMemoryConnection<TRead, TWrite> connection, TRead message)
        {
            if (ServerMessage != null)
                ServerMessage(connection, message);
        }

        /// <summary>
        ///     Invoked on the UI thread.
        /// </summary>
        private void ConnectionOnError(SharedMemoryConnection<TRead, TWrite> connection, Exception exception)
        {
            OnError(exception);
        }

        /// <summary>
        ///     Invoked on the UI thread.
        /// </summary>
        /// <param name="exception"></param>
        private void OnError(Exception exception)
        {
            if (Error != null)
                Error(exception);
        }

        #endregion
    }

    static class SharedMemoryClientFactory
    {
        public static SharedMemoryStreamWrapper<TRead, TWrite> Connect<TRead, TWrite>(string pipeName)
            where TRead : class
            where TWrite : class
        {
            return new SharedMemoryStreamWrapper<TRead, TWrite>(CreateAndConnect(pipeName));
        }

        public static SharedMemoryStream CreateAndConnect(string name)
        {
            var sms = Create(name);
            sms.Connect();
            return sms;
        }

        private static SharedMemoryStream Create(string name)
        {
            return new SharedMemoryStream(name);
        }
    }
}
