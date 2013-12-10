#define RECONNECT

using Starcounter.Advanced;
using Starcounter.Internal;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;

namespace Starcounter {
    internal class NodeTask
    {
        /// <summary>
        /// Private buffer size for each connection.
        /// </summary>
        public const Int32 PrivateBufferSize = 8192;

        /// <summary>
        /// Maximum number of pending asynchronous tasks.
        /// </summary>
        public const Int32 MaxNumPendingAsyncTasks = 64;

        /// <summary>
        /// Maximum number of pending aggregated tasks.
        /// </summary>
        public const Int32 MaxNumPendingAggregatedTasks = 8192;

        /// <summary>
        /// Tcp client.
        /// </summary>
        public TcpClient TcpClientObj = null;

        /// <summary>
        /// Connection socket.
        /// </summary>
        public Socket SocketObj = null;

        /// <summary>
        /// Buffer used for accumulation.
        /// </summary>
        internal Byte[] AccumBuffer = null;

        /// <summary>
        /// Timer used for receive timeout.
        /// </summary>
        public Timer ReceiveTimer = null;

        /// <summary>
        /// Indicates if connection has timed out.
        /// </summary>
        public Boolean ConnectionTimedOut = false;

        /// <summary>
        /// Response.
        /// </summary>
        public Response Resp = null;

        /// <summary>
        /// Total received bytes.
        /// </summary>
        public Int32 TotallyReceivedBytes = 0;

        /// <summary>
        /// Current receive offset.
        /// </summary>
        public Int32 ReceiveOffsetBytes = 0;

        /// <summary>
        /// Response size bytes.
        /// </summary>
        public Int32 ResponseSizeBytes = 0;

        /// <summary>
        /// Request bytes.
        /// </summary>
        public Byte[] RequestBytes = null;

        /// <summary>
        /// Size of request in bytes.
        /// </summary>
        public Int32 RequestBytesLength = 0;

        /// <summary>
        /// User delegate.
        /// </summary>
        public Action<Response, Object> UserDelegate = null;

        /// <summary>
        /// Bound scheduler id.
        /// </summary>
        public Byte BoundSchedulerId = 0;

        /// <summary>
        /// User object.
        /// </summary>
        public Object UserObject = null;

        /// <summary>
        /// Memory stream.
        /// </summary>
        public MemoryStream MemStream = null;

        /// <summary>
        /// Node to which this connection belongs.
        /// </summary>
        public Node NodeInst = null;

        /// <summary>
        /// Receive timeout in milliseconds.
        /// </summary>
        public Int32 ReceiveTimeoutMs { get; set; }

        /// <summary>
        /// Resets the connection details.
        /// </summary>
        public void Reset(
            Byte[] requestBytes,
            Int32 requestBytesLength,
            Action<Response, Object> userDelegate,
            Object userObject,
            Int32 receiveTimeoutMs,
            Byte boundSchedulerId)
        {
            Resp = null;
            TotallyReceivedBytes = 0;
            ReceiveOffsetBytes = 0;
            ResponseSizeBytes = 0;

            RequestBytes = requestBytes;
            RequestBytesLength = requestBytesLength;

            UserDelegate = userDelegate;
            UserObject = userObject;

            ReceiveTimeoutMs = receiveTimeoutMs;
            ConnectionTimedOut = false;

            MemStream = new MemoryStream();
            BoundSchedulerId = boundSchedulerId;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="hostName"></param>
        /// <param name="portNumber"></param>
        public NodeTask(Node nodeInst)
        {
            NodeInst = nodeInst;

            if (!NodeInst.UsesAggregation())
                AccumBuffer = new Byte[NodeTask.PrivateBufferSize];
        }

        /// <summary>
        /// Returns True if connection is established.
        /// </summary>
        public Boolean IsConnectionEstablished()
        {
            return (null != TcpClientObj);
        }

        /// <summary>
        /// Closes the connection.
        /// </summary>
        public void Close()
        {
            if (null != TcpClientObj)
            {
                TcpClientObj.Close();
                TcpClientObj = null;
            }

            if (null != SocketObj)
            {
                SocketObj.Close();
                SocketObj = null;
            }
        }

        /// <summary>
        /// Called when receive is finished on socket.
        /// </summary>
        /// <param name="ar"></param>
        void NetworkOnReceiveCallback(IAsyncResult ar)
        {
            try
            {
                // Checking if connection was timed out.
                if (ConnectionTimedOut)
                    throw new IOException("Connection timed out.");

                // Calling end read to indicate finished read operation.
                Int32 recievedBytes = SocketObj.EndReceive(ar);

                // Checking if remote host has closed the connection.
                if (0 == recievedBytes)
                    throw new IOException("Remote host closed the connection.");

                // Dumping data to memory stream.
                MemStream.Write(AccumBuffer, ReceiveOffsetBytes, recievedBytes);

                // Adding to total received size.
                TotallyReceivedBytes += recievedBytes;

                // Process the bytes here.
                if (Resp == null)
                {
                    try
                    {
                        // Trying to parse the response.
                        Resp = new Response(AccumBuffer, 0, TotallyReceivedBytes, null, false);

                        // Setting offset bytes to 0.
                        ReceiveOffsetBytes = 0;

                        // Getting the whole response size.
                        ResponseSizeBytes = Resp.ResponseLength;
                    }
                    catch (Exception exc)
                    {
                        // Continue to receive when there is not enough data.
                        Resp = null;
                        ReceiveOffsetBytes = TotallyReceivedBytes;

                        // Trying to fetch recognized error code.
                        UInt32 code;
                        if ((!ErrorCode.TryGetCode(exc, out code)) || (code != Error.SCERRAPPSHTTPPARSERINCOMPLETEHEADERS))
                        {
                            CallUserDelegateOnFailure(exc, false);

                            return;
                        }
                    }
                }

                // Checking if we have received everything.
                if ((Resp != null) && (TotallyReceivedBytes == ResponseSizeBytes))
                {
                    // Setting the response buffer.
                    Resp.SetResponseBuffer(MemStream.GetBuffer(), MemStream, TotallyReceivedBytes);

                    // Invoking user delegate.
                    NodeInst.CallUserDelegate(Resp, UserDelegate, UserObject, BoundSchedulerId);

                    // Freeing connection resources.
                    NodeInst.FreeConnection(this, false);
                }
                else
                {
                    // Read again. This callback will be called again.
                    SocketObj.BeginReceive(AccumBuffer, ReceiveOffsetBytes, PrivateBufferSize - ReceiveOffsetBytes, SocketFlags.None, NetworkOnReceiveCallback, null);
                }
            }
            catch (Exception exc)
            {
                CallUserDelegateOnFailure(exc, false);
            }
        }

        /// <summary>
        /// Called on receive timer expiration.
        /// </summary>
        /// <param name="obj"></param>
        void OnReceiveTimeout(object obj)
        {
            ReceiveTimer.Dispose();
            ConnectionTimedOut = true;
            SocketObj.Close(); // Closing the Socket cancels the async operation.
        }

        /// <summary>
        /// Called when send is finished on socket.
        /// </summary>
        /// <param name="ar"></param>
        void NetworkOnSendCallback(IAsyncResult ar)
        {
            try
            {
                // Calling end write to indicate finished write operation.
                Int32 numBytesSent = SocketObj.EndSend(ar);

                // Setting the receive timeout.
                SocketObj.ReceiveTimeout = ReceiveTimeoutMs;

                // Checking for correct number of bytes sent.
                if (numBytesSent != RequestBytesLength)
                {
                    CallUserDelegateOnFailure(new Exception("Socket has sent wrong amount of data!"), false);
                    return;
                }

                // Starting read operation.
                IAsyncResult res = SocketObj.BeginReceive(AccumBuffer, 0, PrivateBufferSize, SocketFlags.None, NetworkOnReceiveCallback, null);

                // Checking if receive is not completed immediately.
                if(!res.IsCompleted)
                {
                    // Checking if we have timeout on receive.
                    if (ReceiveTimeoutMs != 0)
                    {
                        // Scheduling a timer timeout job.
                        ReceiveTimer = new Timer(OnReceiveTimeout, null, ReceiveTimeoutMs, Timeout.Infinite);
                    }
                }
            }
            catch (Exception exc)
            {
                CallUserDelegateOnFailure(exc, false);
            }
        }

        /// <summary>
        /// Called when connect is finished on socket.
        /// </summary>
        /// <param name="ar"></param>
        void NetworkOnConnectCallback(IAsyncResult ar)
        {
            try
            {
                SocketObj.EndConnect(ar);

                TcpClientObj = new TcpClient();
                TcpClientObj.Client = SocketObj;

                SocketObj.BeginSend(RequestBytes, 0, RequestBytesLength, SocketFlags.None, NetworkOnSendCallback, null);
            }
            catch (Exception exc)
            {
                CallUserDelegateOnFailure(exc, false);
            }
        }

        /// <summary>
        /// Performs asynchronous request.
        /// </summary>
        public void PerformAsyncRequest()
        {
            try
            {
                // Obtaining existing or creating new connection.
                if (null == SocketObj)
                {
                    SocketObj = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                    SocketObj.BeginConnect(NodeInst.HostName, NodeInst.PortNumber, NetworkOnConnectCallback, null);
                }
                else
                {
#if RECONNECT
                    try
                    {
#endif
                        SocketObj.BeginSend(RequestBytes, 0, RequestBytesLength, SocketFlags.None, NetworkOnSendCallback, null);
#if RECONNECT
                    }
                    catch
                    {
                        // Seems connection was closed so reconnecting.
                        SocketObj = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                        SocketObj.BeginConnect(NodeInst.HostName, NodeInst.PortNumber, NetworkOnConnectCallback, null);
                    }
#endif
                }
            }
            catch (Exception exc)
            {
                // Connection wasn't established.
                SocketObj = null;
                TcpClientObj = null;

                CallUserDelegateOnFailure(exc, false);
            }
        }

        /// <summary>
        /// Constructs response from bytes.
        /// </summary>
        internal void ConstructResponseAndCallDelegate(Byte[] bytes, Int32 offset, Int32 resp_len_bytes)
        {
            try
            {
                // Trying to parse the response.
                Resp = new Response(bytes, offset, resp_len_bytes, null, false);

                // Getting the whole response size.
                ResponseSizeBytes = Resp.ResponseLength;
            }
            catch (Exception exc)
            {
                // Continue to receive when there is not enough data.
                Resp = null;

                // Trying to fetch recognized error code.
                UInt32 code;
                if ((!ErrorCode.TryGetCode(exc, out code)) || (code != Error.SCERRAPPSHTTPPARSERINCOMPLETEHEADERS))
                {
                    // Logging the exception to server log.
                    if (NodeInst.ShouldLogErrors)
                        Node.NodeLogException_(exc);

                    // Freeing connection resources.
                    NodeInst.FreeConnection(this, false);

                    throw exc;
                }
            }

            // Setting the response buffer.
            Byte[] resp_bytes = new Byte[resp_len_bytes];
            Buffer.BlockCopy(bytes, offset, resp_bytes, 0, resp_len_bytes);
            Resp.SetResponseBuffer(resp_bytes, null, resp_len_bytes);

            // Invoking user delegate.
            NodeInst.CallUserDelegate(Resp, UserDelegate, UserObject, BoundSchedulerId);
        }

        /// <summary>
        /// Performs synchronous request and returns the response.
        /// </summary>
        /// <returns></returns>
        public Response PerformSyncRequest()
        {
            try
            {
#if RECONNECT
                try
                {
#endif
                    // Checking if we are connected.
                    if (null == SocketObj)
                    {
                        TcpClientObj = new TcpClient(NodeInst.HostName, NodeInst.PortNumber);
                        SocketObj = TcpClientObj.Client;
                    }

                    // Sending the request.
                    Int32 bytesSent = SocketObj.Send(RequestBytes, 0, RequestBytesLength, SocketFlags.None);
                    Debug.Assert(RequestBytesLength == bytesSent);
#if RECONNECT
                }
                catch
                {
                    // Connection wasn't established.
                    SocketObj = null;
                    TcpClientObj = null;

                    // Assuming that existing TCP connection is down.
                    // So we need to create a new one.
                    TcpClientObj = new TcpClient(NodeInst.HostName, NodeInst.PortNumber);
                    SocketObj = TcpClientObj.Client;

                    SocketObj.Send(RequestBytes, 0, RequestBytesLength, SocketFlags.None);
                }
#endif

                Int32 recievedBytes = 0;

                // Setting the receive timeout.
                SocketObj.ReceiveTimeout = ReceiveTimeoutMs;

                // Looping until we get everything.
                while (true)
                {
                    // Reading the response into predefined buffer.
                    recievedBytes = SocketObj.Receive(AccumBuffer, ReceiveOffsetBytes, PrivateBufferSize - ReceiveOffsetBytes, SocketFlags.None);
                    if (recievedBytes <= 0)
                    {
                        SocketObj = null;
                        throw new IOException("Remote host closed the connection.");
                    }

                    // Dumping data to memory stream.
                    MemStream.Write(AccumBuffer, ReceiveOffsetBytes, recievedBytes);

                    // Adding to total received size.
                    TotallyReceivedBytes += recievedBytes;

                    if (Resp == null)
                    {
                        try
                        {
                            // Trying to parse the response.
                            Resp = new Response(AccumBuffer, 0, TotallyReceivedBytes, null, false);

                            // Setting offset bytes to 0.
                            ReceiveOffsetBytes = 0;

                            // Getting the whole response size.
                            ResponseSizeBytes = Resp.ResponseLength;
                        }
                        catch (Exception exc)
                        {
                            // Continue to receive when there is not enough data.
                            Resp = null;
                            ReceiveOffsetBytes = TotallyReceivedBytes;

                            // Trying to fetch recognized error code.
                            UInt32 code;
                            if ((!ErrorCode.TryGetCode(exc, out code)) || (code != Error.SCERRAPPSHTTPPARSERINCOMPLETEHEADERS))
                            {
                                // Logging the exception to server log.
                                if (NodeInst.ShouldLogErrors)
                                    Node.NodeLogException_(exc);

                                // Freeing connection resources.
                                NodeInst.FreeConnection(this, true);

                                throw exc;
                            }
                        }
                    }

                    // Checking if we have received everything.
                    if ((Resp != null) && (TotallyReceivedBytes == ResponseSizeBytes))
                        break;
                }

                // Setting the response buffer.
                Resp.SetResponseBuffer(MemStream.GetBuffer(), MemStream, TotallyReceivedBytes);

                // Freeing connection resources.
                NodeInst.FreeConnection(this, true);
            }
            catch (Exception exc)
            {
                CallUserDelegateOnFailure(exc, true);
            }

            return Resp;
        }

        /// <summary>
        /// Calls user delegate when response has failed.
        /// </summary>
        /// <param name="exc"></param>
        void CallUserDelegateOnFailure(Exception exc, Boolean isSyncCall)
        {
            // Logging the exception to server log.
            if (NodeInst.ShouldLogErrors)
                Node.NodeLogException_(exc);

            Resp = new Response()
            {
                StatusCode = 503,
                StatusDescription = "Service Unavailable",
                ContentType = "text/plain",
                Body = exc.ToString()
            };

            // Parsing the response.
            Resp.ConstructFromFields();
            Resp.ParseResponseFromUncompressed();

            // Invoking user delegate.
            if (null != UserDelegate)
                NodeInst.CallUserDelegate(Resp, UserDelegate, UserObject, BoundSchedulerId);

            // Freeing connection resources.
            NodeInst.FreeConnection(this, isSyncCall);
        }
    }
}