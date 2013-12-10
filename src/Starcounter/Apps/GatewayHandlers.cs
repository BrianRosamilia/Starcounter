// ***********************************************************************
// <copyright file="GatewayHandlers.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using System;
using System.Runtime.InteropServices;
using System.Text;
using HttpStructs;
using Starcounter.Internal;
using Starcounter.Advanced;
using System.Diagnostics;

namespace Starcounter
{
    /// <summary>
    /// Struct PortHandlerParams
    /// </summary>
    public struct PortHandlerParams
    {
        /// <summary>
        /// The user session id
        /// </summary>
        public UInt64 UserSessionId;

        /// <summary>
        /// The data stream
        /// </summary>
        public NetworkDataStream DataStream;
    }

    /// <summary>
    /// Delegate PortCallback
    /// </summary>
    /// <param name="info">The info.</param>
    /// <returns>Boolean.</returns>
    public delegate Boolean PortCallback(
		PortHandlerParams info
	);

    /// <summary>
    /// Struct SubportHandlerParams
    /// </summary>
    public struct SubportHandlerParams
    {
        /// <summary>
        /// The user session id
        /// </summary>
        public UInt64 UserSessionId;

        /// <summary>
        /// The subport id
        /// </summary>
        public UInt32 SubportId;

        /// <summary>
        /// The data stream
        /// </summary>
        public NetworkDataStream DataStream;
    }

    /// <summary>
    /// Delegate SubportCallback
    /// </summary>
    /// <param name="info">The info.</param>
    /// <returns>Boolean.</returns>
    public delegate Boolean SubportCallback(
        SubportHandlerParams info
    );

    /// <summary>
    /// Class GatewayHandlers
    /// </summary>
	internal unsafe class GatewayHandlers
	{
        /// <summary>
        /// Maximum number of user handlers to register.
        /// </summary>
        const Int32 MAX_HANDLERS = 1024;

        /// <summary>
        /// The port_handlers_
        /// </summary>
        private static PortCallback[] port_handlers_;
        /// <summary>
        /// The subport_handlers_
        /// </summary>
        private static SubportCallback[] subport_handlers_;
        /// <summary>
        /// The uri_handlers_
        /// </summary>
        private static HandlersManagement.UriCallbackDelegate[] uri_handlers_;

        /// <summary>
        /// The port_outer_handler_
        /// </summary>
		private static bmx.BMX_HANDLER_CALLBACK port_outer_handler_;
        /// <summary>
        /// The subport_outer_handler_
        /// </summary>
        private static bmx.BMX_HANDLER_CALLBACK subport_outer_handler_;
        /// <summary>
        /// The uri_outer_handler_
        /// </summary>
        private static bmx.BMX_HANDLER_CALLBACK uri_outer_handler_;

        /// <summary>
        /// Initializes static members of the <see cref="GatewayHandlers" /> class.
        /// </summary>
        static GatewayHandlers()
		{
            port_handlers_ = new PortCallback[MAX_HANDLERS];
            subport_handlers_ = new SubportCallback[MAX_HANDLERS];
            uri_handlers_ = new HandlersManagement.UriCallbackDelegate[MAX_HANDLERS];

            port_outer_handler_ = new bmx.BMX_HANDLER_CALLBACK(PortOuterHandler);
            subport_outer_handler_ = new bmx.BMX_HANDLER_CALLBACK(SubportOuterHandler);
            uri_outer_handler_ = new bmx.BMX_HANDLER_CALLBACK(HandleIncomingHttpRequest);
		}

        /// <summary>
        /// Ports the outer handler.
        /// </summary>
        /// <param name="session_id">The session_id.</param>
        /// <param name="raw_chunk">The raw_chunk.</param>
        /// <param name="task_info">The task_info.</param>
        /// <param name="is_handled">The is_handled.</param>
        /// <returns>UInt32.</returns>
        private unsafe static UInt32 PortOuterHandler(
			UInt64 session_id,
			Byte* raw_chunk, 
			bmx.BMX_TASK_INFO* task_info,
			Boolean* is_handled)
		{
            *is_handled = false;

            UInt32 chunk_index = task_info->chunk_index;
            //Console.WriteLine("Handler called, session: " + session_id + ", chunk: " + chunk_index);

            // Fetching the callback.
            PortCallback user_callback = port_handlers_[task_info->handler_id];
			if (user_callback == null)
                throw ErrorCode.ToException(Error.SCERRUNSPECIFIED); // SCERRHANDLERNOTFOUND

            // Determining if chunk is single.
            Boolean is_single_chunk = ((task_info->flags & 0x01) == 0);

            // Creating parameters.
            PortHandlerParams handler_params = new PortHandlerParams
            {
                UserSessionId = *(UInt32*)(raw_chunk + MixedCodeConstants.CHUNK_OFFSET_SESSION_LINEAR_INDEX),
                DataStream = new NetworkDataStream(raw_chunk, is_single_chunk, task_info->chunk_index, task_info->client_worker_id)
            };

            // Calling user callback.
            *is_handled = user_callback(handler_params);
            
            // Reset managed task state before exiting managed task entry point.
            TaskHelper.Reset();

			return 0;
		}

        /// <summary>
        /// Subports the outer handler.
        /// </summary>
        /// <param name="session_id">The session_id.</param>
        /// <param name="raw_chunk">The raw_chunk.</param>
        /// <param name="task_info">The task_info.</param>
        /// <param name="is_handled">The is_handled.</param>
        /// <returns>UInt32.</returns>
        private unsafe static UInt32 SubportOuterHandler(
            UInt64 session_id,
            Byte* raw_chunk,
            bmx.BMX_TASK_INFO* task_info,
            Boolean* is_handled)
        {
            *is_handled = false;

            UInt32 chunk_index = task_info->chunk_index;
            //Console.WriteLine("Handler called, session: " + session_id + ", chunk: " + chunk_index);

            // Fetching the callback.
            SubportCallback user_callback = subport_handlers_[task_info->handler_id];
            if (user_callback == null)
                throw ErrorCode.ToException(Error.SCERRUNSPECIFIED); // SCERRHANDLERNOTFOUND

            // Determining if chunk is single.
            Boolean is_single_chunk = ((task_info->flags & 0x01) == 0);

            // Creating parameters.
            SubportHandlerParams handler_params = new SubportHandlerParams
            {
                UserSessionId = *(UInt32*)(raw_chunk + MixedCodeConstants.CHUNK_OFFSET_SESSION_LINEAR_INDEX),
                SubportId = 0,
                DataStream = new NetworkDataStream(raw_chunk, is_single_chunk, task_info->chunk_index, task_info->client_worker_id)
            };

            // Calling user callback.
            *is_handled = user_callback(handler_params);
            
            // Reset managed task state before exiting managed task entry point.
            TaskHelper.Reset();

            return 0;
        }

        static String AggrRespString =
            "HTTP/1.1 200 OK\r\n" +
            "Content-Type: text/html; charset=UTF-8\r\n" +
            "Content-Length: 10\r\n\r\n1234567890";

        static Byte[] AggrRespBytes = UTF8Encoding.ASCII.GetBytes(AggrRespString);

        /// <summary>
        /// This is the main entry point of incoming HTTP requests.
        /// It is called from the Gateway via the shared memory IPC (interprocess communication).
        /// </summary>
        /// <param name="session_id">The session_id.</param>
        /// <param name="raw_chunk">The raw_chunk.</param>
        /// <param name="task_info">The task_info.</param>
        /// <param name="is_handled">The is_handled.</param>
        /// <returns>UInt32.</returns>
        private unsafe static UInt32 HandleIncomingHttpRequest(
            UInt64 session_id,
            Byte* raw_chunk,
            bmx.BMX_TASK_INFO* task_info,
            Boolean* is_handled)
        {
            try
            {
                *is_handled = false;

                UInt32 chunk_index = task_info->chunk_index;
                //Console.WriteLine("Handler called, session: " + session_id + ", chunk: " + chunk_index);

                // Fetching the callback.
                HandlersManagement.UriCallbackDelegate user_callback = uri_handlers_[task_info->handler_id];
                if (user_callback == null)
                    throw ErrorCode.ToException(Error.SCERRUNSPECIFIED); // SCERRHANDLERNOTFOUND

                // Determining if chunk is single.
                Boolean is_single_chunk = ((task_info->flags & MixedCodeConstants.LINKED_CHUNKS_FLAG) == 0);

                // Socket data begin.
                Byte* socket_data_begin = raw_chunk + MixedCodeConstants.CHUNK_OFFSET_SOCKET_DATA;

                // Getting type of network protocol.
                MixedCodeConstants.NetworkProtocolType protocol_type = 
                    (MixedCodeConstants.NetworkProtocolType)(*(Byte*)(socket_data_begin + MixedCodeConstants.SOCKET_DATA_OFFSET_NETWORK_PROTO_TYPE));

                // Checking if we are accumulating on host.
                if (((*(UInt32*)(raw_chunk + MixedCodeConstants.CHUNK_OFFSET_SOCKET_FLAGS)) & MixedCodeConstants.SOCKET_DATA_FLAGS_ON_HOST_ACCUMULATION) != 0)
                {

                }

                // Checking if we need to process linked chunks.
                if (!is_single_chunk)
                {
                    // Creating network data stream object.
                    NetworkDataStream data_stream = new NetworkDataStream(raw_chunk, is_single_chunk, task_info->chunk_index, task_info->client_worker_id);

                    UInt16 num_chunks = *(UInt16*)(raw_chunk + MixedCodeConstants.CHUNK_OFFSET_NUM_CHUNKS);

                    Byte[] plain_chunks_data = new Byte[num_chunks * MixedCodeConstants.SHM_CHUNK_SIZE];

                    fixed (Byte* p_plain_chunks_data = plain_chunks_data)
                    {
                        // Copying all chunks data.
                        UInt32 errorCode = bmx.sc_bmx_plain_copy_and_release_chunks(
                            chunk_index,
                            raw_chunk,
                            p_plain_chunks_data);

                        if (errorCode != 0)
                            throw ErrorCode.ToException(errorCode);

                        // Obtaining Request structure.
                        Request http_request = new Request(
                            raw_chunk,
                            is_single_chunk,
                            chunk_index,
                            task_info->handler_id,
                            p_plain_chunks_data + MixedCodeConstants.CHUNK_OFFSET_SOCKET_DATA + MixedCodeConstants.SOCKET_DATA_OFFSET_HTTP_REQUEST,
                            p_plain_chunks_data + MixedCodeConstants.CHUNK_OFFSET_SOCKET_DATA,
                            data_stream,
                            protocol_type);

                        // Checking for the correct session.
                        http_request.GetAppsSessionInterface();

                        // Calling user callback.
                        *is_handled = user_callback(http_request);
                    }
                }
                else
                {
                    /*if (((*(UInt32*)(raw_chunk + MixedCodeConstants.CHUNK_OFFSET_SOCKET_FLAGS)) & MixedCodeConstants.SOCKET_DATA_FLAGS_AGGREGATED) != 0)
                    {
                        data_stream.Init(raw_chunk, true, chunk_index);
                        data_stream.SendResponseInternal(AggrRespBytes, 0, AggrRespBytes.Length, Response.ConnectionFlags.NoSpecialFlags);

                        return 0;
                    }*/

                    // Creating network data stream object.
                    NetworkDataStream data_stream = new NetworkDataStream(raw_chunk, is_single_chunk, task_info->chunk_index, task_info->client_worker_id);

                    // Obtaining Request structure.
                    Request http_request = new Request(
                        raw_chunk,
                        is_single_chunk,
                        task_info->chunk_index,
                        task_info->handler_id,
                        socket_data_begin + MixedCodeConstants.SOCKET_DATA_OFFSET_HTTP_REQUEST,
                        socket_data_begin,
                        data_stream,
                        protocol_type);

                    // Checking for the correct session.
                    http_request.GetAppsSessionInterface();

                    // Calling user callback.
                    *is_handled = user_callback(http_request);
                }
            
                // Reset managed task state before exiting managed task entry point.
                TaskHelper.Reset();
            }
            catch (Exception exc)
            {
                LogSources.Hosting.LogException(exc);
                throw exc;
            }

            return 0;
        }

        /// <summary>
        /// Registers the port handler.
        /// </summary>
        /// <param name="port">The port.</param>
        /// <param name="portCallback">The port callback.</param>
        /// <param name="handlerId">The handler id.</param>
        public static void RegisterPortHandler(
			UInt16 port, 
			PortCallback portCallback,
            out UInt16 handlerId)
		{
            UInt64 handler_id;

            // Ensuring correct multi-threading handlers creation.
            lock (port_handlers_)
            {
                UInt32 errorCode = bmx.sc_bmx_register_port_handler(port, port_outer_handler_, &handler_id);
                if (errorCode != 0)
                    throw ErrorCode.ToException(errorCode, "Port number: " + port);

                port_handlers_[handler_id] = portCallback;

                // TODO
                handlerId = (UInt16)handler_id;
            }
		}

        public static void UnregisterPort(UInt16 port)
		{
            // Ensuring correct multi-threading handlers creation.
            lock (port_handlers_)
            {
                UInt32 errorCode = bmx.sc_bmx_unregister_port(port);
                if (errorCode != 0)
                    throw ErrorCode.ToException(errorCode, "Port number: " + port);
            }
		}

        // Registers subport handler.
        /// <summary>
        /// Registers the subport handler.
        /// </summary>
        /// <param name="port">The port.</param>
        /// <param name="subport">The subport.</param>
        /// <param name="subportCallback">The subport callback.</param>
        /// <param name="handlerId">The handler id.</param>
        public static void RegisterSubportHandler(
            UInt16 port,
            UInt32 subport,
            SubportCallback subportCallback,
            out UInt16 handlerId)
        {
            UInt64 handler_id;

            // Ensuring correct multi-threading handlers creation.
            lock (subport_handlers_)
            {
                UInt32 errorCode = bmx.sc_bmx_register_subport_handler(port, subport, subport_outer_handler_, &handler_id);
                if (errorCode != 0)
                    throw ErrorCode.ToException(errorCode, "Port number: " + port + ", Sub-port number: " + subport);

                subport_handlers_[handler_id] = subportCallback;

                // TODO
                handlerId = (UInt16)handler_id;
            }
        }

        public static void UnregisterSubport(
            UInt16 port,
            UInt32 subport)
        {
            // Ensuring correct multi-threading handlers creation.
            lock (subport_handlers_)
            {
                UInt32 errorCode = bmx.sc_bmx_unregister_subport(port, subport);
                if (errorCode != 0)
                    throw ErrorCode.ToException(errorCode, "Port number: " + port + ", Sub-port number: " + subport);
            }
        }

        /// <summary>
        /// Registers the URI handler.
        /// </summary>
        public static void RegisterUriHandler(
            UInt16 port,
            String originalUriInfo,
            String processedUriInfo,
            Byte[] paramTypes,
            HandlersManagement.UriCallbackDelegate uriCallback,
            MixedCodeConstants.NetworkProtocolType protoType,
            out UInt16 handlerId,
            out Int32 maxNumEntries)
        {
            Int32 maxNumEntriesTemp;
            UInt64 handler_id;
            Byte numParams = 0;
            if (null != paramTypes)
                numParams = (Byte)paramTypes.Length;

            // Ensuring correct multi-threading handlers creation.
            lock (uri_handlers_)
            {
                unsafe
                {
                    fixed (Byte* pp = paramTypes)
                    {
                        UInt32 errorCode = bmx.sc_bmx_register_uri_handler(
                            port,
                            originalUriInfo,
                            processedUriInfo,
                            pp,
                            numParams,
                            uri_outer_handler_,
                            protoType,
                            &handler_id,
                            &maxNumEntriesTemp);

                        if (errorCode != 0)
                            throw ErrorCode.ToException(errorCode, "URI string: " + originalUriInfo);
                    }
                }

                uri_handlers_[handler_id] = uriCallback;
                maxNumEntries = maxNumEntriesTemp;

                // TODO
                handlerId = (UInt16) handler_id;
            }
        }

        public static void UnregisterUriHandler(
            UInt16 port,
            String originalUriInfo)
        {
            // Ensuring correct multi-threading handlers creation.
            lock (uri_handlers_)
            {
                UInt32 errorCode = bmx.sc_bmx_unregister_uri(port, originalUriInfo);
                if (errorCode != 0)
                    throw ErrorCode.ToException(errorCode, "URI string: " + originalUriInfo);
            }
        }
	}
}
