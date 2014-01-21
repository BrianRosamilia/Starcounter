#pragma once
#ifndef SOCKET_DATA_HPP
#define SOCKET_DATA_HPP

namespace starcounter {
namespace network {

class GatewayWorker;
class HttpProto;

enum SOCKET_DATA_FLAGS
{
    SOCKET_DATA_FLAGS_TO_DATABASE_DIRECTION = 1,
    SOCKET_DATA_FLAGS_SOCKET_REPRESENTER = 2,
    SOCKET_DATA_FLAGS_ACCUMULATING_STATE = 2 << 1,
    SOCKET_DATA_FLAGS_DISCONNECT_AFTER_SEND = MixedCodeConstants::SOCKET_DATA_FLAGS_DISCONNECT_AFTER_SEND,
    SOCKET_DATA_FLAGS_ACTIVE_CONN = 2 << 3,
    SOCKET_DATA_FLAGS_JUST_SEND = MixedCodeConstants::SOCKET_DATA_FLAGS_JUST_SEND,
    SOCKET_DATA_FLAGS_JUST_DISCONNECT = MixedCodeConstants::SOCKET_DATA_FLAGS_DISCONNECT,
    SOCKET_DATA_FLAGS_TRIGGER_DISCONNECT = 2 << 6,
    HTTP_WS_FLAGS_COMPLETE_HEADER = 2 << 7,
    HTTP_WS_FLAGS_PROXIED_SERVER_SOCKET = 2 << 8,
    HTTP_WS_FLAGS_UNKNOWN_PROXIED_PROTO = 2 << 9,
    HTTP_WS_FLAGS_GRACEFULLY_CLOSE = MixedCodeConstants::HTTP_WS_FLAGS_GRACEFULLY_CLOSE,
    SOCKET_DATA_FLAGS_AGGREGATED = MixedCodeConstants::SOCKET_DATA_FLAGS_AGGREGATED,
    SOCKET_DATA_FLAGS_ON_HOST_ACCUMULATION = MixedCodeConstants::SOCKET_DATA_FLAGS_ON_HOST_ACCUMULATION
};

// Socket data chunk.
class WorkerDbInterface;
class SocketDataChunk
{
    // Overlapped structure used for WinSock
    // (must be 8-bytes aligned).
    OVERLAPPED ovl_;

    /////////////////////////
    // 8 bytes aligned data.
    /////////////////////////

    // Unique number for socket.
    random_salt_type unique_socket_id_;

    // Client IP address information.
    ip_info_type client_ip_info_;

    /////////////////////////
    // 4 bytes aligned data.
    /////////////////////////

    // Socket identifier.
    session_index_type socket_info_index_;

    // Socket data flags.
    uint32_t flags_;

    // Aggregation unique index.
    uint32_t unique_aggr_index_;

    /////////////////////////
    // 2 bytes aligned data.
    /////////////////////////

    // Offset in bytes from the beginning of the socket data to place
    // where user data should be written.
    uint16_t user_data_offset_in_socket_data_;

    /////////////////////////
    // 1 bytes aligned data.
    /////////////////////////

    // Current type of network operation.
    uint8_t type_of_network_oper_;

    // Type of network protocol.
    uint8_t type_of_network_protocol_;

    // Gateway chunk store index.
    chunk_store_type chunk_store_index_;

    /////////////////////////
    // Data structures.
    /////////////////////////

    // Accumulative buffer chunk.
    AccumBuffer accum_buf_;

    // HTTP protocol instance.
    HttpProto http_proto_;

    // WebSocket related data.
    WsProto ws_proto_;

    // Starcounter session information.
    ScSessionStruct session_;

    // Accept or parameters or temporary data.
    uint8_t accept_or_params_or_temp_data_[MixedCodeConstants::PARAMS_INFO_MAX_SIZE_BYTES];

    // Blob buffer itself.
    uint8_t data_blob_[1];

public:

    // Setting accumulating flag.
    void set_accumulating_flag()
    {
        flags_ |= SOCKET_DATA_FLAGS_ACCUMULATING_STATE;
    }

    // Checking that gateway chunk is valid.
    void CheckForValidity()
    {
        GW_ASSERT((chunk_store_index_ >= 0) && (chunk_store_index_ < NumGatewayChunkSizes));
    }

    // Invalidating gateway chunk when returning to store.
    void InvalidateWhenReturning()
    {
        chunk_store_index_ = -1;
        accum_buf_.Invalidate();
    }

    chunk_store_type get_chunk_store_index()
    {
        return chunk_store_index_;
    }

    void set_chunk_store_index(chunk_store_type store_type)
    {
        chunk_store_index_ = store_type;
    }

    worker_id_type get_bound_worker_id()
    {
        return session_.gw_worker_id_;
    }

    void set_bound_worker_id(worker_id_type worker_id)
    {
        session_.gw_worker_id_ = worker_id;
    }

    void PlainCopySocketDataInfoHeaders(SocketDataChunk* sd)
    {
        // NOTE:
        // Saving new chunk store index, otherwise it would be overwritten.
        chunk_store_type saved_new_store_index = chunk_store_index_;
        memcpy(this, sd, sizeof(SocketDataChunk));
        chunk_store_index_ = saved_new_store_index;

        // Resetting the accumulative buffer because it was overwritten.
        ResetAccumBuffer();
    }

    void CopyFromAnotherSocketData(SocketDataChunk* sd)
    {
        // First copying socket data headers.
        PlainCopySocketDataInfoHeaders(sd);

        AccumBuffer* ab = sd->get_accum_buf();

        GW_ASSERT(static_cast<int32_t>(ab->get_accum_len_bytes()) <= GatewayChunkDataSizes[chunk_store_index_]);

        memcpy(data_blob_, ab->get_chunk_orig_buf_ptr(), ab->get_accum_len_bytes());

        // Adjusting the accumulative buffer.
        accum_buf_.AddAccumulatedBytes(ab->get_accum_len_bytes());
    }

    void CopyFromOneChunkIPCSocketData(SocketDataChunk* ipc_sd, int32_t num_bytes_to_copy)
    {
        // First copying socket data headers.
        PlainCopySocketDataInfoHeaders(ipc_sd);

        // Setting some specific accumulative buffer fields.
        accum_buf_.set_desired_accum_bytes(ipc_sd->get_accum_buf()->get_desired_accum_bytes());
        accum_buf_.set_chunk_num_available_bytes(ipc_sd->get_user_data_length_bytes());

        memcpy(data_blob_, (uint8_t*)ipc_sd + ipc_sd->get_user_data_offset_in_socket_data(), num_bytes_to_copy);

        set_user_data_offset_in_socket_data(static_cast<uint16_t>(data_blob_ - (uint8_t*)this));
    }

    // Get Http protocol instance.
    HttpProto* get_http_proto()
    {
        return &http_proto_;
    }

    // WebSockets protocol info.
    WsProto* get_ws_proto()
    {
        return &ws_proto_;
    }

    session_index_type get_unique_aggr_index()
    {
        return unique_aggr_index_;
    }

    void set_unique_aggr_index(session_index_type unique_aggr_index)
    {
        unique_aggr_index_ = unique_aggr_index;
    }

    // Gets socket data accumulated data offset.
    uint32_t GetAccumOrigBufferSocketDataOffset()
    {
        return static_cast<uint32_t>(accum_buf_.get_chunk_orig_buf_ptr() - (uint8_t*)this);
    }

    // Prepare buffer to send outside.
    void PrepareForSend(uint8_t *data, uint32_t num_bytes)
    {
        accum_buf_.PrepareForSend(data, num_bytes);
        set_user_data_offset_in_socket_data(static_cast<uint32_t>(data - (uint8_t*)this));
    }

    // Resets safe flags.
    void ResetSafeFlags()
    {
        reset_accumulating_flag();
        reset_to_database_direction_flag();
        reset_complete_header_flag();
    }

#ifdef GW_LOOPED_TEST_MODE

    // Pushing given sd to network emulation queue.
    void PushToMeasuredNetworkEmulationQueue(GatewayWorker* gw);
    void PushToPreparationNetworkEmulationQueue(GatewayWorker* gw);

#endif

    // Checks if socket data is in correct state.
    uint32_t AssertCorrectState()
    {
        uint8_t* sd = (uint8_t*) this;
        uint8_t* smc = (uint8_t*) this - MixedCodeConstants::CHUNK_OFFSET_SOCKET_DATA;

        std::cout << "offset ovl_ = "<< ((uint8_t*)&ovl_ - sd) << std::endl;
        std::cout << "offset session_ = "<< ((uint8_t*)&session_ - sd) << std::endl;
        std::cout << "offset unique_socket_id_ = "<< ((uint8_t*)&unique_socket_id_ - sd) << std::endl;
        std::cout << "offset client_ip_info_ = "<< ((uint8_t*)&client_ip_info_ - sd) << std::endl;
        std::cout << "offset socket_info_index_ = "<< ((uint8_t*)&socket_info_index_ - sd) << std::endl;
        std::cout << "offset user_data_written_bytes_ = "<< ((uint8_t*)accum_buf_.get_chunk_num_available_bytes_addr() - sd) << std::endl;
        std::cout << "offset flags_ = "<< ((uint8_t*)&flags_ - sd) << std::endl;
        std::cout << "offset unique_aggr_index_ = "<< ((uint8_t*)&unique_aggr_index_ - sd) << std::endl;
        std::cout << "offset num_ipc_chunks_ = "<< ((uint8_t*)&user_data_offset_in_socket_data_ - sd) << std::endl;
        std::cout << "offset user_data_offset_in_socket_data_ = "<< ((uint8_t*)&user_data_offset_in_socket_data_ - sd) << std::endl;
        std::cout << "offset type_of_network_oper_ = "<< ((uint8_t*)&type_of_network_oper_ - sd) << std::endl;
        std::cout << "offset type_of_network_protocol_ = "<< ((uint8_t*)&type_of_network_protocol_ - sd) << std::endl;
        std::cout << "offset accum_buf_ = "<< ((uint8_t*)&accum_buf_ - sd) << std::endl;
        std::cout << "offset http_proto_ = "<< ((uint8_t*)&http_proto_ - sd) << std::endl;
        std::cout << "offset ws_proto_ = "<< ((uint8_t*)&ws_proto_ - sd) << std::endl;
        std::cout << "offset accept_or_params_or_temp_data_ = "<< ((uint8_t*)&accept_or_params_or_temp_data_ - sd) << std::endl;
        std::cout << "offset data_blob_ = "<< ((uint8_t*)&data_blob_ - sd) << std::endl;

        std::cout << std::endl;
        std::cout << std::endl;

        std::cout << "sizeof AccumBuffer = "<< sizeof(AccumBuffer) << std::endl;
        std::cout << "sizeof HttpProto = "<< sizeof(HttpProto) << std::endl;
        std::cout << "sizeof WsProto = "<< sizeof(WsProto) << std::endl;
        std::cout << "sizeof accept_or_params_or_temp_data_ = "<< sizeof(accept_or_params_or_temp_data_) << std::endl;
        std::cout << "sizeof ScSessionStruct = "<< sizeof(ScSessionStruct) << std::endl;
        std::cout << "sizeof HttpRequest = "<< sizeof(HttpRequest) << std::endl;
        std::cout << "sizeof http_parser = "<< sizeof(http_parser) << std::endl;
        std::cout << "sizeof WsProto = "<< sizeof(WsProto) << std::endl;
        std::cout << "sizeof OVERLAPPED = "<< sizeof(OVERLAPPED) << std::endl;

        std::cout << std::endl;
        std::cout << std::endl;

        std::cout << "SOCKET_DATA_OFFSET_SESSION = " << ((uint8_t*)&session_ - sd) << std::endl;
        std::cout << "CHUNK_OFFSET_SESSION = " << ((uint8_t*)&session_ - smc) << std::endl;
        std::cout << "CHUNK_OFFSET_SESSION_SCHEDULER_ID = "<< (&session_.scheduler_id_ - smc) << std::endl;
        std::cout << "CHUNK_OFFSET_SESSION_LINEAR_INDEX = "<< ((uint8_t*)&session_.linear_index_ - smc) << std::endl;
        std::cout << "CHUNK_OFFSET_SESSION_RANDOM_SALT = "<< ((uint8_t*)&session_.random_salt_ - smc) << std::endl;
        std::cout << "SOCKET_DATA_OFFSET_PARAMS_INFO = "<< (accept_or_params_or_temp_data_ - sd) << std::endl;
        std::cout << "SOCKET_DATA_OFFSET_BLOB = "<< (data_blob_ - sd) << std::endl;
        std::cout << "CHUNK_OFFSET_NUM_IPC_CHUNKS = "<< ((uint8_t*)&ovl_ - smc) << std::endl;

        std::cout << "CHUNK_OFFSET_SOCKET_FLAGS = "<< ((uint8_t*)&flags_ - smc) << std::endl;
        std::cout << "SOCKET_DATA_OFFSET_NETWORK_PROTO_TYPE = "<< ((uint8_t*)&type_of_network_protocol_ - sd) << std::endl;
        std::cout << "SOCKET_DATA_OFFSET_CLIENT_IP = "<< ((uint8_t*)&client_ip_info_ - sd) << std::endl;
        std::cout << "SOCKET_DATA_OFFSET_HTTP_REQUEST = "<< ((uint8_t*)get_http_proto()->get_http_request() - sd) << std::endl;
        std::cout << "SOCKET_DATA_NUM_CLONE_BYTES = "<< ((uint8_t*)&accept_or_params_or_temp_data_ - sd) << std::endl;
        std::cout << "CHUNK_OFFSET_USER_DATA_OFFSET_IN_SOCKET_DATA = "<< ((uint8_t*)&user_data_offset_in_socket_data_ - smc) << std::endl;
        std::cout << "CHUNK_OFFSET_USER_DATA_TOTAL_LENGTH = "<< ((uint8_t*)accum_buf_.get_desired_accum_bytes_addr() - smc) << std::endl;

        std::cout << "CHUNK_OFFSET_USER_DATA_WRITTEN_BYTES = "<< ((uint8_t*)accum_buf_.get_chunk_num_available_bytes_addr() - smc) << std::endl;
        std::cout << "SOCKET_DATA_OFFSET_SOCKET_UNIQUE_ID = "<< ((uint8_t*)&unique_socket_id_ - sd) << std::endl;
        std::cout << "SOCKET_DATA_OFFSET_SOCKET_INDEX_NUMBER = "<< ((uint8_t*)&socket_info_index_ - sd) << std::endl;
        std::cout << "SOCKET_DATA_OFFSET_WS_OPCODE = "<< (&get_ws_proto()->get_frame_info()->opcode_ - sd) << std::endl;
        std::cout << "SOCKET_DATA_OFFSET_BOUND_WORKER_ID = "<< ((uint8_t*)&(session_.gw_worker_id_) - sd) << std::endl;

        GW_ASSERT(8 == sizeof(SOCKET));
        GW_ASSERT(8 == sizeof(random_salt_type));
        GW_ASSERT(8 == sizeof(BMX_HANDLER_TYPE));
        GW_ASSERT(4 == sizeof(core::chunk_index));
        GW_ASSERT(16 == sizeof(ScSessionStruct));
        GW_ASSERT(sizeof(ScSessionStruct) == MixedCodeConstants::SESSION_STRUCT_SIZE);

        GW_ASSERT(MixedCodeConstants::SHM_CHUNK_SIZE == starcounter::core::chunk_size);
        GW_ASSERT(MixedCodeConstants::CHUNK_LINK_SIZE == shared_memory_chunk::link_size);

        GW_ASSERT((sd - smc) == MixedCodeConstants::CHUNK_OFFSET_SOCKET_DATA);

        GW_ASSERT(((uint8_t*)&session_ - smc) == MixedCodeConstants::CHUNK_OFFSET_SESSION);
        GW_ASSERT((&session_.scheduler_id_ - smc) == MixedCodeConstants::CHUNK_OFFSET_SESSION_SCHEDULER_ID);
        GW_ASSERT(((uint8_t*)&session_.linear_index_ - smc) == MixedCodeConstants::CHUNK_OFFSET_SESSION_LINEAR_INDEX);
        GW_ASSERT(((uint8_t*)&session_.random_salt_ - smc) == MixedCodeConstants::CHUNK_OFFSET_SESSION_RANDOM_SALT);

        GW_ASSERT((accept_or_params_or_temp_data_ - sd) == MixedCodeConstants::SOCKET_DATA_OFFSET_PARAMS_INFO);

        GW_ASSERT((data_blob_ - sd) == SOCKET_DATA_OFFSET_BLOB);

        GW_ASSERT(((uint8_t*)&ovl_ - smc) == MixedCodeConstants::CHUNK_OFFSET_NUM_IPC_CHUNKS);

        GW_ASSERT(((uint8_t*)&flags_ - smc) == MixedCodeConstants::CHUNK_OFFSET_SOCKET_FLAGS);

        GW_ASSERT(((uint8_t*)&type_of_network_protocol_ - sd) == MixedCodeConstants::SOCKET_DATA_OFFSET_NETWORK_PROTO_TYPE);

        GW_ASSERT(((uint8_t*)&client_ip_info_ - sd) == MixedCodeConstants::SOCKET_DATA_OFFSET_CLIENT_IP);

        GW_ASSERT(((uint8_t*)get_http_proto()->get_http_request() - sd) == MixedCodeConstants::SOCKET_DATA_OFFSET_HTTP_REQUEST);

        GW_ASSERT(((uint8_t*)&accept_or_params_or_temp_data_ - sd) == MixedCodeConstants::SOCKET_DATA_NUM_CLONE_BYTES);

        GW_ASSERT(((uint8_t*)&user_data_offset_in_socket_data_ - smc) == MixedCodeConstants::CHUNK_OFFSET_USER_DATA_OFFSET_IN_SOCKET_DATA);

        GW_ASSERT(((uint8_t*)accum_buf_.get_desired_accum_bytes_addr() - smc) == MixedCodeConstants::CHUNK_OFFSET_USER_DATA_TOTAL_LENGTH);

        GW_ASSERT(((uint8_t*)accum_buf_.get_chunk_num_available_bytes_addr() - smc) == MixedCodeConstants::CHUNK_OFFSET_USER_DATA_WRITTEN_BYTES);

        GW_ASSERT(((uint8_t*)&unique_socket_id_ - sd) == MixedCodeConstants::SOCKET_DATA_OFFSET_SOCKET_UNIQUE_ID);

        GW_ASSERT(((uint8_t*)&socket_info_index_ - sd) == MixedCodeConstants::SOCKET_DATA_OFFSET_SOCKET_INDEX_NUMBER);

        GW_ASSERT((&get_ws_proto()->get_frame_info()->opcode_ - sd) == MixedCodeConstants::SOCKET_DATA_OFFSET_WS_OPCODE);

        GW_ASSERT(((uint8_t*)&(session_.gw_worker_id_) - sd) == MixedCodeConstants::SOCKET_DATA_OFFSET_BOUND_WORKER_ID);

        return 0;
    }

    // Sets unique socket id.
    void set_unique_socket_id(random_salt_type unique_socket_id)
    {
        unique_socket_id_ = unique_socket_id;
    }

    // Deletes global session and sends message to database to delete session there.
    uint32_t SendDeleteSession(GatewayWorker* gw);

    // Returns all linked chunks except the main one.
    uint32_t ReturnExtraLinkedChunks(GatewayWorker* gw);

    // Getting type of network protocol.
    MixedCodeConstants::NetworkProtocolType get_type_of_network_protocol()
    {
        return (MixedCodeConstants::NetworkProtocolType) type_of_network_protocol_;
    }

    // Checking if its a WebSocket protocol.
    bool IsWebSocket()
    {
        return MixedCodeConstants::NetworkProtocolType::PROTOCOL_WEBSOCKETS == get_type_of_network_protocol();
    }

    // Getting to database direction flag.
    bool get_to_database_direction_flag()
    {
        return flags_ & SOCKET_DATA_FLAGS_TO_DATABASE_DIRECTION;
    }

    // Setting to database direction flag.
    void set_to_database_direction_flag()
    {
        flags_ |= SOCKET_DATA_FLAGS_TO_DATABASE_DIRECTION;
    }

    // ReSetting to database direction flag.
    void reset_to_database_direction_flag()
    {
        flags_ &= ~SOCKET_DATA_FLAGS_TO_DATABASE_DIRECTION;
    }

    // Getting socket just send flag.
    bool get_socket_just_send_flag()
    {
        return (flags_ & SOCKET_DATA_FLAGS_JUST_SEND) != 0;
    }

    // Setting socket just send flag.
    void reset_socket_just_send_flag()
    {
        flags_ &= ~SOCKET_DATA_FLAGS_JUST_SEND;
    }

    // Getting socket representer flag.
    bool get_socket_representer_flag()
    {
        return (flags_ & SOCKET_DATA_FLAGS_SOCKET_REPRESENTER) != 0;
    }

    // Setting socket representer flag.
    void set_socket_representer_flag()
    {
        flags_ |= SOCKET_DATA_FLAGS_SOCKET_REPRESENTER;
    }

    // Resetting socket representer flag.
    void reset_socket_representer_flag()
    {
        flags_ &= ~SOCKET_DATA_FLAGS_SOCKET_REPRESENTER;
    }

#ifdef GW_COLLECT_SOCKET_STATISTICS

    // Getting socket diagnostics active connection flag.
    bool get_socket_diag_active_conn_flag()
    {
        return (flags_ & SOCKET_DATA_FLAGS_ACTIVE_CONN) != 0;
    }

    // Setting socket diagnostics active connection flag.
    void set_socket_diag_active_conn_flag()
    {
        flags_ |= SOCKET_DATA_FLAGS_ACTIVE_CONN;
    }

    // ReSetting socket diagnostics active connection flag.
    void reset_socket_diag_active_conn_flag()
    {
        flags_ &= ~SOCKET_DATA_FLAGS_ACTIVE_CONN;
    }

#endif

#ifdef GW_PROXY_MODE

    // Getting proxying unknown protocol flag.
    bool get_unknown_proxied_proto_flag()
    {
        return (flags_ & HTTP_WS_FLAGS_UNKNOWN_PROXIED_PROTO) != 0;
    }

    // Setting proxying unknown protocol flag.
    void set_unknown_proxied_proto_flag()
    {
        flags_ |= HTTP_WS_FLAGS_UNKNOWN_PROXIED_PROTO;
    }

#endif

    // Getting accumulating flag.
    bool get_accumulating_flag()
    {
        return (flags_ & SOCKET_DATA_FLAGS_ACCUMULATING_STATE) != 0;
    }

    // ReSetting accumulating flag.
    void reset_accumulating_flag()
    {
        flags_ &= ~SOCKET_DATA_FLAGS_ACCUMULATING_STATE;
    }

    // Getting disconnect after send flag.
    bool get_disconnect_after_send_flag()
    {
        return (flags_ & SOCKET_DATA_FLAGS_DISCONNECT_AFTER_SEND) != 0;
    }

    // Setting disconnect after send flag.
    void set_disconnect_after_send_flag()
    {
        flags_ |= SOCKET_DATA_FLAGS_DISCONNECT_AFTER_SEND;
    }

    // ReSetting disconnect after send flag.
    void reset_disconnect_after_send_flag()
    {
        flags_ &= ~SOCKET_DATA_FLAGS_DISCONNECT_AFTER_SEND;
    }

    // Getting gracefully close flag.
    bool get_gracefully_close_flag()
    {
        return (flags_ & HTTP_WS_FLAGS_GRACEFULLY_CLOSE) != 0;
    }

    // Setting gracefully close flag.
    void set_gracefully_close_flag()
    {
        flags_ |= HTTP_WS_FLAGS_GRACEFULLY_CLOSE;
    }

    // ReSetting gracefully close flag.
    void reset_gracefully_close_flag()
    {
        flags_ &= ~HTTP_WS_FLAGS_GRACEFULLY_CLOSE;
    }

    bool get_aggregated_flag()
    {
        return (flags_ & SOCKET_DATA_FLAGS_AGGREGATED) != 0;
    }

    void set_aggregated_flag()
    {
        flags_ |= SOCKET_DATA_FLAGS_AGGREGATED;
    }

    void reset_aggregated_flag()
    {
        flags_ &= ~SOCKET_DATA_FLAGS_AGGREGATED;
    }

    bool get_on_host_accumulation_flag()
    {
        return (flags_ & SOCKET_DATA_FLAGS_ON_HOST_ACCUMULATION) != 0;
    }

    void set_on_host_accumulation_flag()
    {
        flags_ |= SOCKET_DATA_FLAGS_ON_HOST_ACCUMULATION;
    }

    void reset_on_host_accumulation_flag()
    {
        flags_ &= ~SOCKET_DATA_FLAGS_ON_HOST_ACCUMULATION;
    }

    // Getting disconnect socket flag.
    bool get_disconnect_socket_flag()
    {
        return (flags_ & SOCKET_DATA_FLAGS_JUST_DISCONNECT) != 0;
    }

    // Getting complete header flag.
    bool get_complete_header_flag()
    {
        return (flags_ & HTTP_WS_FLAGS_COMPLETE_HEADER) != 0;
    }

    // Setting complete header flag.
    void set_complete_header_flag()
    {
        flags_ |= HTTP_WS_FLAGS_COMPLETE_HEADER;
    }

    // ReSetting complete header flag.
    void reset_complete_header_flag()
    {
        flags_ &= ~HTTP_WS_FLAGS_COMPLETE_HEADER;
    }

    // Getting scheduler id.
    scheduler_id_type get_scheduler_id()
    {
        return session_.scheduler_id_;
    }

    // Setting scheduler id.
    void set_scheduler_id(scheduler_id_type sched_id)
    {
        session_.scheduler_id_ = sched_id;
    }

    // Getting session index.
    session_index_type get_session_index()
    {
        return session_.linear_index_;
    }

    // Getting session salt.
    random_salt_type get_session_salt()
    {
        return session_.random_salt_;
    }

    // Determines if this sd has active session attached.
    bool HasActiveSession()
    {
        return INVALID_SESSION_INDEX != session_.linear_index_;
    }

    // Getting unique id.
    random_salt_type get_unique_socket_id()
    {
        return unique_socket_id_;
    }

    // Checking if unique socket number is correct.
    bool CompareUniqueSocketId()
    {
        return g_gateway.CompareUniqueSocketId(socket_info_index_, unique_socket_id_);
    }

    // Setting type of network protocol.
    void set_type_of_network_protocol(MixedCodeConstants::NetworkProtocolType proto_type)
    {
        type_of_network_protocol_ = proto_type;
    }

    // Setting type of network protocol.
    void SetTypeOfNetworkProtocol(MixedCodeConstants::NetworkProtocolType proto_type)
    {
        type_of_network_protocol_ = proto_type;

        g_gateway.SetTypeOfNetworkProtocol(socket_info_index_, proto_type);
    }

    // Setting aggregated flag on socket.
    void SetSocketAggregatedFlag()
    {
        g_gateway.SetSocketAggregatedFlag(socket_info_index_);
    }

    // Getting aggregated flag on socket.
    bool GetSocketAggregatedFlag()
    {
        return g_gateway.GetSocketAggregatedFlag(socket_info_index_);
    }

    // Getting worker id to which this socket is bound.
    worker_id_type GetBoundWorkerId()
    {
        return g_gateway.GetBoundWorkerId(socket_info_index_);
    }

    // Getting destination database index.
    db_index_type GetDestDbIndex()
    {
        return g_gateway.GetDestDbIndex(socket_info_index_);
    }

    // Setting destination database index.
    void SetDestDbIndex(db_index_type db_index)
    {
        return g_gateway.SetDestDbIndex(socket_info_index_, db_index);
    }

    // Getting saved user handler id.
    BMX_HANDLER_TYPE GetSavedUserHandlerId()
    {
        return g_gateway.GetSavedUserHandlerId(socket_info_index_);
    }

    // Setting user handler id.
    void SetSavedUserHandlerId(BMX_HANDLER_TYPE saved_user_handler_id)
    {
        g_gateway.SetSavedUserHandlerId(socket_info_index_, saved_user_handler_id);
    }

    // Setting new unique socket number.
    void GenerateUniqueSocketInfoIds(scheduler_id_type scheduler_id)
    {
        session_.scheduler_id_ = scheduler_id;
        unique_socket_id_ = g_gateway.GenerateUniqueSocketInfoIds(socket_info_index_, session_.scheduler_id_);
    }

    // Sets session if socket is correct.
    void SetGlobalSessionIfEmpty()
    {
        // Checking unique socket id and session.
        if (!g_gateway.IsGlobalSessionActive(socket_info_index_))
            g_gateway.SetGlobalSessionCopy(socket_info_index_, session_);
    }

    // Forcedly sets session if socket is correct.
    void ForceSetGlobalSessionIfEmpty()
    {
        // Checking unique socket id and session.
        if (session_.IsActive())
            g_gateway.SetGlobalSessionCopy(socket_info_index_, session_);
    }

    // Updates connection timestamp if socket is correct.
    void UpdateConnectionTimeStamp()
    {
        g_gateway.UpdateSocketTimeStamp(socket_info_index_);
    }

    // Sets session if socket is correct.
    void SetSdSessionIfEmpty()
    {
        if ((!session_.IsActive()) && (g_gateway.IsGlobalSessionActive(socket_info_index_)))
            session_ = g_gateway.GetGlobalSessionCopy(socket_info_index_);
    }

    // Releases socket info index.
    void ReleaseSocketIndex(GatewayWorker* gw)
    {
        g_gateway.ReleaseSocketIndex(gw, socket_info_index_);
    }

    // Deletes global session.
    void DeleteGlobalSessionOnDisconnect()
    {
        g_gateway.DeleteGlobalSession(socket_info_index_);
    }

    // Checks if global session data is active.
    bool CompareGlobalSessionSalt()
    {
        return g_gateway.CompareGlobalSessionSalt(socket_info_index_, session_.random_salt_);
    }

    // Client IP information.
    ip_info_type get_client_ip_info()
    {
        return client_ip_info_;
    }

    // Sets client IP information.
    void set_client_ip_info(ip_info_type client_ip_info)
    {
        client_ip_info_ = client_ip_info;
    }

    // Returns port index.
    int32_t GetPortIndex()
    {
        int32_t port_index = g_gateway.GetPortIndex(socket_info_index_);
        GW_ASSERT ((port_index != INVALID_PORT_INDEX) && (port_index < g_gateway.get_num_server_ports_slots()));

        return port_index;
    }

    // Returns scheduler id.
    scheduler_id_type GetSchedulerId()
    {
        return g_gateway.GetSchedulerId(socket_info_index_);
    }

    // Returns socket.
    SOCKET GetSocket()
    {
        return g_gateway.GetSocket(socket_info_index_);
    }

    // Returns true if has proxy socket.
    bool HasProxySocket()
    {
        return g_gateway.HasProxySocket(socket_info_index_);
    }

    // Returns true if its a proxy connect socket.
    bool IsProxyConnectSocket()
    {
        return g_gateway.IsProxyConnectSocket(socket_info_index_);
    }

    // Returns aggregation socket index.
    session_index_type GetAggregationSocketIndex()
    {
        return g_gateway.GetAggregationSocketIndex(socket_info_index_);
    }

    // Sets aggregation socket index.
    void SetAggregationSocketIndex(session_index_type aggr_socket_index)
    {
        g_gateway.SetAggregationSocketIndex(socket_info_index_, aggr_socket_index);
    }

    // Returns proxy socket index.
    session_index_type GetProxySocketIndex()
    {
        return g_gateway.GetProxySocketIndex(socket_info_index_);
    }

    // Sets proxy socket index.
    void SetProxySocketIndex(session_index_type proxy_socket_index)
    {
        g_gateway.SetProxySocketIndex(socket_info_index_, proxy_socket_index);
    }

    // Returns socket info index.
    session_index_type get_socket_info_index()
    {
        return socket_info_index_;
    }

    // Sets socket info index.
    void set_socket_info_index(session_index_type socket_info_index)
    {
        socket_info_index_ = socket_info_index;
    }

    // Sets number of IPC chunks.
    void SetNumberOfIPCChunks(uint16_t num_ip_chunks)
    {
        *(uint16_t*)(&ovl_) = num_ip_chunks;
    }

#ifdef GW_TESTING_MODE

    // Getting data blob pointer.
    uint8_t* get_data_blob()
    {
        return data_blob_;
    }

#endif

    // Copies IPC chunks to gateway chunk.
    uint32_t CopyIPCChunksToGatewayChunk(
        WorkerDbInterface* worker_db,
        SocketDataChunk* sd);

    // Copies gateway chunk to IPC chunks.
    uint32_t CopyGatewayChunkToIPCChunks(
        WorkerDbInterface* worker_db,
        SocketDataChunk** new_ipc_sd,
        core::chunk_index* db_chunk_index,
        uint16_t* num_ipc_chunks);

    // Overlapped structure used for WinSock.
    OVERLAPPED* get_ovl()
    {
        return &ovl_;
    }

    // Current type of network operation.
    SocketOperType get_type_of_network_oper()
    {
        return (SocketOperType) type_of_network_oper_;
    }

    // Current type of network operation.
    void set_type_of_network_oper(SocketOperType type_of_network_oper)
    {
        type_of_network_oper_ = (uint8_t)type_of_network_oper;
    }

    // Accept data or parameters data.
    uint8_t* const get_accept_or_params_data()
    {
        return accept_or_params_or_temp_data_;
    }

    // Offset in bytes from the beginning of the chunk to place
    // where user data should be written.
    void set_user_data_offset_in_socket_data(uint16_t user_data_offset_in_socket_data)
    {
        user_data_offset_in_socket_data_ = user_data_offset_in_socket_data;
    }

    // Offset in bytes from the beginning of the chunk to place
    // where user data should be written.
    uint16_t get_user_data_offset_in_socket_data()
    {
        return user_data_offset_in_socket_data_;
    }

    // Size in bytes of written user data.
    uint32_t get_user_data_length_bytes()
    {
        return accum_buf_.get_chunk_num_available_bytes();
    }

    // Data buffer chunk.
    AccumBuffer* get_accum_buf()
    {
        return &accum_buf_;
    }

    // Resets accumulating buffer to its default socket data values.
    void ResetAccumBuffer()
    {
        accum_buf_.Init(GatewayChunkDataSizes[chunk_store_index_], data_blob_, true);
    }

    // Exchanges sockets during proxying.
    void ExchangeToProxySocket()
    {
        session_index_type proxy_socket_info_index = GetProxySocketIndex();

        // Getting corresponding proxy socket id.
        random_salt_type proxy_unique_socket_id = g_gateway.GetUniqueSocketId(proxy_socket_info_index);

#ifdef GW_SOCKET_DIAG
        GW_COUT << "Exchanging sockets: " << socket_info_index_ << "<->" << proxy_socket_info_index << " and ids " <<
            unique_socket_id_ << "<->" << proxy_unique_socket_id << GW_ENDL;
#endif

        socket_info_index_ = proxy_socket_info_index;
        unique_socket_id_ = proxy_unique_socket_id;
    }

    // Initializes socket data that comes from database.
    void PreInitSocketDataFromDb()
    {
        type_of_network_protocol_ = g_gateway.GetTypeOfNetworkProtocol(socket_info_index_);
    }

    // Initialization.
    void Init(
        session_index_type socket_info_index,
        worker_id_type bound_worker_id);

    // Resetting socket.
    void ResetOnDisconnect();

    // Returns pointer to the beginning of user data.
    uint8_t* UserDataBuffer()
    {
        return (uint8_t*)this + user_data_offset_in_socket_data_;
    }

    // Resets user data offset.
    void ResetUserDataOffset()
    {
        user_data_offset_in_socket_data_ = static_cast<uint32_t> (data_blob_ - (uint8_t*)this);
    }

    // Start receiving on socket.
    uint32_t Receive(GatewayWorker *gw, uint32_t *num_bytes)
    {
        set_type_of_network_oper(RECEIVE_SOCKET_OPER);

        memset(&ovl_, 0, OVERLAPPED_SIZE);

#ifdef GW_LOOPED_TEST_MODE
        PushToMeasuredNetworkEmulationQueue(gw);
        return WSA_IO_PENDING;
#endif

        DWORD flags = 0;
        return WSARecv(GetSocket(), (WSABUF *)&accum_buf_, 1, (LPDWORD)num_bytes, &flags, &ovl_, NULL);
    }

    // Start sending on socket.
    uint32_t Send(GatewayWorker* gw, uint32_t *numBytes)
    {
        set_type_of_network_oper(SEND_SOCKET_OPER);

        memset(&ovl_, 0, OVERLAPPED_SIZE);

#ifdef GW_LOOPED_TEST_MODE
        PushToMeasuredNetworkEmulationQueue(gw);
        return WSA_IO_PENDING;
#endif

        return WSASend(GetSocket(), (WSABUF *)&accum_buf_, 1, (LPDWORD)numBytes, 0, &ovl_, NULL);
    }

    // Start accepting on socket.
    uint32_t Accept(GatewayWorker* gw)
    {
        set_type_of_network_oper(ACCEPT_SOCKET_OPER);

        memset(&ovl_, 0, OVERLAPPED_SIZE);

#ifdef GW_LOOPED_TEST_MODE
        PushToPreparationNetworkEmulationQueue(gw);
        return FALSE;
#endif

        // Running Windows API AcceptEx function.
        return AcceptExFunc(
            g_gateway.get_server_port(GetPortIndex())->get_listening_sock(),
            GetSocket(),
            accept_or_params_or_temp_data_,
            0,
            SOCKADDR_SIZE_EXT,
            SOCKADDR_SIZE_EXT,
            NULL,
            &ovl_);
    }

    // Setting SO_UPDATE_ACCEPT_CONTEXT.
    uint32_t SetAcceptSocketOptions()
    {
        SOCKET listening_sock = g_gateway.get_server_port(GetPortIndex())->get_listening_sock();

#ifndef GW_LOOPED_TEST_MODE
        if (setsockopt(GetSocket(), SOL_SOCKET, SO_UPDATE_ACCEPT_CONTEXT, (char *)&listening_sock, sizeof(listening_sock)))
            return SCERRGWACCEPTEXFAILED;
#endif

        return 0;
    }

    // Start connecting on socket.
    uint32_t Connect(GatewayWorker* gw, sockaddr_in *serverAddr)
    {
        set_type_of_network_oper(CONNECT_SOCKET_OPER);

        memset(&ovl_, 0, OVERLAPPED_SIZE);

#ifdef GW_LOOPED_TEST_MODE
        PushToPreparationNetworkEmulationQueue(gw);
        return FALSE;
#endif

        return ConnectExFunc(GetSocket(), (SOCKADDR *) serverAddr, sizeof(sockaddr_in), NULL, 0, NULL, &ovl_);
    }

    // Start disconnecting socket.
    uint32_t Disconnect(GatewayWorker *gw)
    {
        set_type_of_network_oper(DISCONNECT_SOCKET_OPER);

        memset(&ovl_, 0, OVERLAPPED_SIZE);

#ifdef GW_LOOPED_TEST_MODE
        PushToMeasuredNetworkEmulationQueue(gw);
        return FALSE;
#endif

        return DisconnectExFunc(GetSocket(), &ovl_, TF_REUSE_SOCKET, 0);
    }

    // Puts socket data to database.
    void PrepareToDb()
    {
        // Setting database data direction flag.
        set_to_database_direction_flag();
    }

    // Puts socket data from database.
    void PrepareFromDb()
    {
        // Removing database data direction flag.
        reset_to_database_direction_flag();
    }

    // Clones existing socket data chunk.
    uint32_t CloneToReceive(GatewayWorker *gw);

    // Clone current socket data to simply send it.
    uint32_t CloneToPush(GatewayWorker*gw, SocketDataChunk** new_sd);

    // Clone current socket data to a bigger one.
    static uint32_t ChangeToBigger(
        GatewayWorker*gw,
        SocketDataChunkRef sd,
        int32_t data_size = 0);

    // Clone current socket data to simply send it.
    uint32_t CreateSocketDataFromBigBuffer(
        GatewayWorker*gw,
        session_index_type socket_info_index,
        int32_t data_len,
        uint8_t* data,
        SocketDataChunk** new_sd);

    // Attaches session to socket data.
    void AssignSession(ScSessionStruct& session)
    {
        // Setting session for this socket data.
        session_ = session;
    }

    // Getting session structure.
    ScSessionStruct* GetSessionStruct()
    {
        return &session_;
    }

    // Resets socket data session.
    void ResetSdSession()
    {
        // Resetting session data.
        session_.Reset();
    }
};

} // namespace network
} // namespace starcounter

#endif // SOCKET_DATA_HPP