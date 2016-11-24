using System.IO;
using System.Threading;
using System;
using System.Collections.Generic;

namespace System.IO.SharedMemory
{
    /// <summary>
    /// Wraps a <see cref="SharedMemoryStream"/> and provides multiple simultaneous client connection handling.
    /// </summary>
    /// <typeparam name="TReadWrite">Reference type to read from and write to the shared memory</typeparam>
    public class SharedMemoryServer<TReadWrite> : SharedMemoryServer<TReadWrite, TReadWrite> where TReadWrite : class
    {
        /// <summary>
        /// Constructs a new <c>SharedMemoryServer</c> object that listens for client connections on the given <paramref name="name"/>.
        /// </summary>
        /// <param name="name">Name of the shared memory to listen on</param>
        public SharedMemoryServer(string name)
            : base(name)
        {
        }
    }

    /// <summary>
    /// Wraps a <see cref="SharedMemoryStream"/> and provides multiple simultaneous client connection handling.
    /// </summary>
    /// <typeparam name="TRead">Reference type to read from the shared memory</typeparam>
    /// <typeparam name="TWrite">Reference type to write to the shared memory</typeparam>
    public class SharedMemoryServer<TRead, TWrite>
        where TRead : class
        where TWrite : class
    {
        /// <summary>
        /// Invoked whenever a client connects to the server.
        /// </summary>
        public event ConnectionEventHandler<TRead, TWrite> ClientConnected;

        /// <summary>
        /// Invoked whenever a client disconnects from the server.
        /// </summary>
        public event ConnectionEventHandler<TRead, TWrite> ClientDisconnected;

        /// <summary>
        /// Invoked whenever a client sends a message to the server.
        /// </summary>
        public event ConnectionMessageEventHandler<TRead, TWrite> ClientMessage;

        /// <summary>
        /// Invoked whenever an exception is thrown during a read or write operation.
        /// </summary>
        public event SharedMemoryExceptionEventHandler Error;

        private readonly string _name;
        private readonly List<SharedMemoryConnection<TRead, TWrite>> _connections = new List<SharedMemoryConnection<TRead, TWrite>>();

        private int _nextId;

        private volatile bool _shouldKeepRunning;
#pragma warning disable 414
        private volatile bool _isRunning;
#pragma warning restore 414

        /// <summary>
        /// Constructs a new <c>SharedMemoryServer</c> object that listens for client connections on the given <paramref name="name" />.
        /// </summary>
        /// <param name="name">Name of the shared memory to listen on.</param>
        public SharedMemoryServer(string name)
        {
            _name = name;
        }

        /// <summary>
        /// Begins listening for client connections in a separate background thread.
        /// This method returns immediately.
        /// </summary>
        public void Start()
        {
            _shouldKeepRunning = true;
            var worker = new Worker();
            worker.Error += OnError;
            worker.DoWork(ListenSync);
        }

        /// <summary>
        /// Sends a message to all connected clients asynchronously.
        /// This method returns immediately, possibly before the message has been sent to all clients.
        /// </summary>
        /// <param name="message"></param>
        public void PushMessage(TWrite message)
        {
            lock (_connections)
            {
                foreach (var client in _connections)
                {
                    client.PushMessage(message);
                }
            }
        }

        /// <summary>
        /// push message to the given client.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="clientName"></param>
        public void PushMessage(TWrite message, string clientName)
        {
            lock (_connections)
            {
                foreach (var client in _connections)
                {
                    if (client.Name == clientName)
                        client.PushMessage(message);
                }
            }
        }

        /// <summary>
        /// Closes all open client connections and stops listening for new ones.
        /// </summary>
        public void Stop()
        {
            _shouldKeepRunning = false;

            lock (_connections)
            {
                foreach (var client in _connections.ToArray())
                {
                    client.Close();
                }
            }

            // If background thread is still listening for a client to connect,
            // initiate a dummy connection that will allow the thread to exit.
            //dummy connection will use the local server name.
            var dummyClient = new SharedMemoryClient<TRead, TWrite>(_name, ".");
            dummyClient.Start();
            dummyClient.WaitForConnection(TimeSpan.FromSeconds(2));
            dummyClient.Stop();
            dummyClient.WaitForDisconnection(TimeSpan.FromSeconds(2));
        }

        #region Private methods

        private void ListenSync()
        {
            _isRunning = true;
            while (_shouldKeepRunning)
            {
                WaitForConnection(_name);
            }
            _isRunning = false;
        }

        private void WaitForConnection(string name)
        {
            SharedMemoryStream data = null;
            SharedMemoryConnection<TRead, TWrite> connection = null;

            var connectionName = GetNextConnectionName(name);

            try
            {
                // Send the client the name of the data channel to use
                using (SharedMemoryStream handshake = new SharedMemoryStream(name, 3, 4096))
                using (SharedMemoryStreamWrapper<string> handshakeWrapper = new SharedMemoryStreamWrapper<string>(handshake))
                {
                    handshakeWrapper.WriteObject(connectionName);
                    handshakeWrapper.WaitForSharedMemoryDrain();
                }

                // Wait for the client to connect to the data pipe
                data = SharedMemoryServerFactory.Create(connectionName);
                data.WaitForConnection();

                // Add the client's connection to the list of connections
                connection = SharedMemoryConnectionFactory.CreateConnection<TRead, TWrite>(data);
                connection.ReceiveMessage += ClientOnReceiveMessage;
                connection.Disconnected += ClientOnDisconnected;
                connection.Error += ConnectionOnError;
                connection.Open();

                lock (_connections)
                {
                    _connections.Add(connection);
                }

                ClientOnConnected(connection);
            }
            // Catch the IOException that is raised if the pipe is broken or disconnected.
            catch (Exception e)
            {
                Console.Error.WriteLine("Named pipe is broken or disconnected: {0}", e);

                Cleanup(handshake);
                Cleanup(data);

                ClientOnDisconnected(connection);
            }
        }

        private void ClientOnConnected(SharedMemoryConnection<TRead, TWrite> connection)
        {
            if (ClientConnected != null)
                ClientConnected(connection);
        }

        private void ClientOnReceiveMessage(SharedMemoryConnection<TRead, TWrite> connection, TRead message)
        {
            if (ClientMessage != null)
                ClientMessage(connection, message);
        }

        private void ClientOnDisconnected(SharedMemoryConnection<TRead, TWrite> connection)
        {
            if (connection == null)
                return;

            lock (_connections)
            {
                _connections.Remove(connection);
            }

            if (ClientDisconnected != null)
                ClientDisconnected(connection);
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

        private string GetNextConnectionName(string name)
        {
            return string.Format("{0}_{1}", name, ++_nextId);
        }

        private static void Cleanup(SharedMemoryStream stream)
        {
            if (stream == null) return;
            using (var x = stream)
            {
                x.Close();
            }
        }

        #endregion
    }

    static class SharedMemoryServerFactory
    {
        public static SharedMemoryStream CreateAndConnect(string name)
        {
            var sms = Create(name);
            sms.WaitForConnection();
            return sms;
        }

        public static SharedMemoryStream Create(string name)
        {
            return new SharedMemoryStream(name);
        }
    }
}
