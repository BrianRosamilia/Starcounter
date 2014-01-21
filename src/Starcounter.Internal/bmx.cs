// ***********************************************************************
// <copyright file="bmx.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using Starcounter.Advanced;
using System;
using System.Runtime.InteropServices;
using System.Security;

namespace Starcounter.Internal
{
    /// <summary>
    /// Class bmx
    /// </summary>
    [SuppressUnmanagedCodeSecurity]
    public static class bmx
    {
        /// <summary>
        /// Struct BMX_TASK_INFO
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct BMX_TASK_INFO
        {
            internal Byte flags;
            internal Byte scheduler_number;
            internal UInt16 handler_id;
            internal Byte client_worker_id;
            internal UInt32 chunk_index;
        };

        /// <summary>
        /// Delegate BMX_HANDLER_CALLBACK
        /// </summary>
        /// <param name="session_id">The session_id.</param>
        /// <param name="raw_chunk">The raw_chunk.</param>
        /// <param name="task_info">The task_info.</param>
        /// <param name="is_handled">The is_handled.</param>
        /// <returns>UInt32.</returns>
        public unsafe delegate UInt32 BMX_HANDLER_CALLBACK(
            UInt64 session_id,
            Byte* raw_chunk,
            BMX_TASK_INFO* task_info,
            Boolean* is_handled
        );

        /// <summary>
        /// Sc_init_bmx_managers this instance.
        /// </summary>
        /// <returns>UInt32.</returns>
        [DllImport("bmx.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public extern static UInt32 sc_init_bmx_manager(
            GlobalSessions.DestroyAppsSessionCallback destroy_apps_session_callback,
            GlobalSessions.CreateNewAppsSessionCallback create_new_apps_session_callback,
            Diagnostics.ErrorHandlingCallback error_handling_callback
            );

#if false
        /// <summary>
        /// Sc_wait_for_bmx_readies this instance.
        /// </summary>
        [DllImport("bmx.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public extern static Int32 sc_wait_for_bmx_ready(UInt32 max_time_to_wait_ms);
#endif

        /// <summary>
        /// sc_bmx_copy_all_chunks the specified chunk_index.
        /// </summary>
        /// <returns>UInt32.</returns>
        [DllImport("bmx.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public unsafe extern static UInt32 sc_bmx_copy_all_chunks(
            UInt32 chunk_index,
            Byte* first_smc,
            UInt32 first_chunk_offset,
            UInt32 total_copy_bytes,
            Byte* dest_buffer,
            UInt32 dest_buffer_size
        );

        /// <summary>
        /// sc_bmx_plain_copy_and_release_chunks the specified chunk_index.
        /// </summary>
        /// <returns>UInt32.</returns>
        [DllImport("bmx.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public unsafe extern static UInt32 sc_bmx_plain_copy_and_release_chunks(
            UInt32 first_chunk_index,
            Byte* first_chunk_data,
            Byte* buffer
        );

        /// <summary>
        /// sc_bmx_obtain_new_chunk.
        /// </summary>
        /// <returns>UInt32.</returns>
        [DllImport("bmx.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public unsafe extern static UInt32 sc_bmx_obtain_new_chunk(
            UInt32* new_chunk_index,
            Byte** new_chunk_mem
        );

        /// <summary>
        /// Sc_bmx_send_buffers the specified buf.
        /// </summary>
        /// <param name="buf">The buf.</param>
        /// <param name="buf_len_bytes">The buf_len_bytes.</param>
        /// <param name="chunk_index">The chunk_index.</param>
        /// <returns>UInt32.</returns>
        [DllImport("bmx.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public unsafe extern static UInt32 sc_bmx_send_buffer(
            Byte gw_worker_id,
            Byte* buf,
            UInt32 buf_len_bytes,
            UInt32* the_chunk_index,
            UInt32 conn_flags
        );

        /// <summary>
        /// sc_bmx_release_linked_chunks.
        /// </summary>
        /// <param name="chunk_index">The chunk_index.</param>
        /// <returns>UInt32.</returns>
        [DllImport("bmx.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public unsafe extern static UInt32 sc_bmx_release_linked_chunks(UInt32 chunk_index);

        /// <summary>
        /// Sc_bmx_register_port_handlers the specified port.
        /// </summary>
        /// <param name="port">The port.</param>
        /// <param name="callback">The callback.</param>
        /// <param name="handler_id">The handler_id.</param>
        /// <returns>UInt32.</returns>
        [DllImport("bmx.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public unsafe extern static UInt32 sc_bmx_register_port_handler(
            UInt16 port,
            BMX_HANDLER_CALLBACK callback,
            UInt64* handler_id
        );

        [DllImport("bmx.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public unsafe extern static UInt32 sc_bmx_unregister_port(
            UInt16 port
        );

        /// <summary>
        /// Sc_bmx_register_subport_handlers the specified port.
        /// </summary>
        /// <param name="port">The port.</param>
        /// <param name="subport">The subport.</param>
        /// <param name="callback">The callback.</param>
        /// <param name="handler_id">The handler_id.</param>
        /// <returns>UInt32.</returns>
        [DllImport("bmx.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public unsafe extern static UInt32 sc_bmx_register_subport_handler(
            UInt16 port,
            UInt32 subport,
            BMX_HANDLER_CALLBACK callback,
            UInt64* handler_id
        );

        [DllImport("bmx.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public unsafe extern static UInt32 sc_bmx_unregister_subport(
            UInt16 port,
            UInt32 subport
        );

        /// <summary>
        /// Sc_bmx_register_uri_handlers the specified port.
        /// </summary>
        [DllImport("bmx.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public unsafe extern static UInt32 sc_bmx_register_uri_handler(
            UInt16 port,
            String originalUriInfo,
            String processedUriInfo,
            Byte* param_types,
            Byte num_params,
            BMX_HANDLER_CALLBACK callback,
            MixedCodeConstants.NetworkProtocolType proto_type,
            UInt64* handler_id,
            Int32* max_num_entries
        );

        [DllImport("bmx.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public unsafe extern static UInt32 sc_bmx_unregister_uri(
            UInt16 port,
            String originalUriInfo
        );

        /// <summary>
        /// Creates new Request based on session.
        /// </summary>
        public unsafe static Request GenerateNewRequest(
            ScSessionClass session,
            MixedCodeConstants.NetworkProtocolType protocol_type,
            Boolean isText)
        {
            UInt32 new_chunk_index;
            Byte* new_chunk_mem;
            UInt32 err_code = bmx.sc_bmx_obtain_new_chunk(&new_chunk_index, &new_chunk_mem);
            if (0 != err_code)
                throw ErrorCode.ToException(err_code, "Can't obtain new chunk for session push.");

            // Creating network data stream object.
            NetworkDataStream data_stream = new NetworkDataStream(new_chunk_mem, new_chunk_index, session.session_struct_.gw_worker_id_);

            Byte* socket_data_begin = new_chunk_mem + MixedCodeConstants.CHUNK_OFFSET_SOCKET_DATA;

            (*(ScSessionStruct*)(new_chunk_mem + MixedCodeConstants.CHUNK_OFFSET_SESSION)) = session.session_struct_;

            (*(UInt32*)(new_chunk_mem + MixedCodeConstants.CHUNK_OFFSET_SOCKET_FLAGS)) = 0;

            (*(Byte*)(socket_data_begin + MixedCodeConstants.SOCKET_DATA_OFFSET_NETWORK_PROTO_TYPE)) = (Byte) protocol_type;           

            (*(UInt32*)(socket_data_begin + MixedCodeConstants.SOCKET_DATA_OFFSET_SOCKET_INDEX_NUMBER)) = session.socket_index_num_;
            (*(UInt64*)(socket_data_begin + MixedCodeConstants.SOCKET_DATA_OFFSET_SOCKET_UNIQUE_ID)) = session.socket_unique_id_;

            (*(UInt16*)(new_chunk_mem + MixedCodeConstants.CHUNK_OFFSET_USER_DATA_OFFSET_IN_SOCKET_DATA)) =
                MixedCodeConstants.SOCKET_DATA_OFFSET_BLOB;

            // Checking if we have text or binary WebSocket frame.
            if (isText)
                (*(Byte*)(socket_data_begin + MixedCodeConstants.SOCKET_DATA_OFFSET_WS_OPCODE)) = 1;
            else
                (*(Byte*)(socket_data_begin + MixedCodeConstants.SOCKET_DATA_OFFSET_WS_OPCODE)) = 2;

            // Obtaining Request structure.
            Request new_req = new Request(
                new_chunk_mem,
                true,
                new_chunk_index,
                0,
                new_chunk_mem + MixedCodeConstants.CHUNK_OFFSET_SOCKET_DATA + MixedCodeConstants.SOCKET_DATA_OFFSET_HTTP_REQUEST,
                new_chunk_mem + MixedCodeConstants.CHUNK_OFFSET_SOCKET_DATA,
                data_stream,
                protocol_type);

            return new_req;
        }
    }
}