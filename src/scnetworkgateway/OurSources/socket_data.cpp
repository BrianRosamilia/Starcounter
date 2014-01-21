#include "static_headers.hpp"
#include "gateway.hpp"
#include "handlers.hpp"
#include "ws_proto.hpp"
#include "http_proto.hpp"
#include "socket_data.hpp"
#include "tls_proto.hpp"
#include "worker_db_interface.hpp"
#include "worker.hpp"

namespace starcounter {
namespace network {

// Initialization.
void SocketDataChunk::Init(
    session_index_type socket_info_index,
    worker_id_type bound_worker_id)
{
    flags_ = 0;

    socket_info_index_ = INVALID_SESSION_INDEX;

    ResetUserDataOffset();

    socket_info_index_ = socket_info_index;
    
    session_.Reset();

    ResetAccumBuffer();

    set_to_database_direction_flag();

    set_type_of_network_oper(UNKNOWN_SOCKET_OPER);
    SetTypeOfNetworkProtocol(MixedCodeConstants::NetworkProtocolType::PROTOCOL_HTTP1);

    set_unique_socket_id(g_gateway.GetUniqueSocketId(socket_info_index));
    set_bound_worker_id(bound_worker_id);

    // Initializing HTTP/WEBSOCKETS data structures.
    get_http_proto()->Init();
    get_ws_proto()->Init();
}

// Resetting socket.
void SocketDataChunk::ResetOnDisconnect()
{
    // Resetting associated socket info.
    g_gateway.ResetSocketInfoOnDisconnect(socket_info_index_);

    set_to_database_direction_flag();

    set_type_of_network_oper(DISCONNECT_SOCKET_OPER);
    SetTypeOfNetworkProtocol(MixedCodeConstants::NetworkProtocolType::PROTOCOL_HTTP1);

    // Clearing attached session.
    //g_gateway.ClearSession(sessionIndex);

    // Removing reference to/from session.
    session_.Reset();

    // Resetting HTTP/WS stuff.
    get_http_proto()->Reset();
    get_ws_proto()->Reset();

    flags_ = 0;
    ResetAccumBuffer();
}

// Clones existing socket data chunk for receiving.
uint32_t SocketDataChunk::CloneToReceive(GatewayWorker *gw)
{
    // Only socket representer can clone to its receive.
    if (!get_socket_representer_flag())
        return 0;

    SocketDataChunk* sd_clone = NULL;

    // NOTE: Cloning to receive only on database 0 chunks.
    uint32_t err_code = gw->CreateSocketData(socket_info_index_, sd_clone);
    GW_ERR_CHECK(err_code);

    // Since another socket is going to be attached.
    reset_socket_representer_flag();

    // Copying session completely.
    sd_clone->session_ = session_;

    sd_clone->set_to_database_direction_flag();
    sd_clone->SetTypeOfNetworkProtocol(get_type_of_network_protocol());
    sd_clone->set_unique_socket_id(unique_socket_id_);
    sd_clone->set_socket_info_index(socket_info_index_);
    sd_clone->set_client_ip_info(client_ip_info_);

    // This socket becomes attached.
    sd_clone->set_socket_representer_flag();

#ifdef GW_COLLECT_SOCKET_STATISTICS
    bool active_conn = get_socket_diag_active_conn_flag();
    reset_socket_diag_active_conn_flag();
    if (active_conn)
        sd_clone->set_socket_diag_active_conn_flag();
    else
        sd_clone->reset_socket_diag_active_conn_flag();
#endif

    // Setting the clone for the next iteration.
    gw->SetReceiveClone(sd_clone);

#ifdef GW_SOCKET_DIAG
    GW_COUT << "Cloned socket " << socket_info_index_ << ":" << GetSocket() << ":" << unique_socket_id_ << ":" << (uint64_t)this << " to socket " <<
        sd_clone->get_socket_info_index() << ":" << sd_clone->GetSocket() << ":" << sd_clone->get_unique_socket_id() << ":" << (uint64_t)sd_clone << GW_ENDL;
#endif

    return 0;
}

// Clone current socket data to simply send it.
uint32_t SocketDataChunk::CreateSocketDataFromBigBuffer(
    GatewayWorker*gw,
    session_index_type socket_info_index,
    int32_t data_len,
    uint8_t* data,
    SocketDataChunk** new_sd)
{
    // Getting a chunk from new database.
    SocketDataChunk* sd;
    uint32_t err_code = gw->CreateSocketData(socket_info_index, sd);
    if (err_code)
    {
        // New chunk can not be obtained.
        return err_code;
    }

    AccumBuffer* accum_buf = sd->get_accum_buf();

    // Checking if data fits inside chunk.
    GW_ASSERT(data_len < (int32_t)accum_buf->get_chunk_num_available_bytes());

    // Checking if message should be copied.
    memcpy(accum_buf->get_chunk_orig_buf_ptr(), data, data_len);

    *new_sd = sd;

    return 0;
}

// Clone current socket data to a bigger one.
uint32_t SocketDataChunk::ChangeToBigger(
    GatewayWorker*gw,
    SocketDataChunkRef sd,
    int32_t data_size)
{
    // Checking if maximum chunk size is reached.
    if (sd->get_chunk_store_index() == NumGatewayChunkSizes)
        return SCERRGWMAXCHUNKSIZEREACHED;

    // Taking the chunk where accumulated buffer fits.
    SocketDataChunk* new_sd;
    if (data_size == 0)
        new_sd = gw->GetWorkerChunks()->ObtainChunkByStoreIndex(sd->get_chunk_store_index() + 1);
    else
        new_sd = gw->GetWorkerChunks()->ObtainChunk(data_size);

    // Copying the socket data headers and accumulated buffer.
    new_sd->CopyFromAnotherSocketData(sd);

    // Releasing chunk.
    gw->GetWorkerChunks()->ReleaseChunk(sd);

    sd = new_sd;

    return 0;
}

// Clone current socket data to push it.
uint32_t SocketDataChunk::CloneToPush(GatewayWorker* gw, SocketDataChunk** new_sd)
{
    GW_ASSERT(static_cast<int32_t>(get_accum_buf()->get_accum_len_bytes()) <= GatewayChunkDataSizes[chunk_store_index_]);

    // Taking the chunk where accumulated buffer fits.
    (*new_sd) = gw->GetWorkerChunks()->ObtainChunk(get_accum_buf()->get_accum_len_bytes());

    // Copying the socket data headers and accumulated buffer.
    (*new_sd)->CopyFromAnotherSocketData(this);

    // This socket becomes unattached.
    (*new_sd)->reset_socket_representer_flag();
    (*new_sd)->reset_socket_diag_active_conn_flag();

    return 0;
}

// Copies IPC chunks to gateway chunk.
uint32_t SocketDataChunk::CopyIPCChunksToGatewayChunk(
    WorkerDbInterface* worker_db,
    SocketDataChunk* ipc_sd)
{
    int32_t data_bytes_offset = MixedCodeConstants::SOCKET_DATA_MAX_SIZE - ipc_sd->get_user_data_offset_in_socket_data(),
        bytes_left = ipc_sd->get_user_data_length_bytes() - data_bytes_offset;

    // Checking that number of left bytes in linked chunks is correct.
    GW_ASSERT(bytes_left <= MixedCodeConstants::MAX_BYTES_EXTRA_LINKED_IPC_CHUNKS);

    // Copying first chunk.
    CopyFromOneChunkIPCSocketData(ipc_sd, data_bytes_offset);

    // Number of bytes to copy from IPC chunk.
    uint32_t cur_chunk_data_size = bytes_left;
    if (cur_chunk_data_size > starcounter::MixedCodeConstants::CHUNK_MAX_DATA_BYTES)
        cur_chunk_data_size = starcounter::MixedCodeConstants::CHUNK_MAX_DATA_BYTES;

    // Getting link to the first chunk in chain.
    shared_memory_chunk* ipc_smc = (shared_memory_chunk*)((uint8_t*)ipc_sd - MixedCodeConstants::CHUNK_OFFSET_SOCKET_DATA);
    core::chunk_index cur_chunk_index = ipc_smc->get_link();

    // Until we get the last chunk in chain.
    while (cur_chunk_index != shared_memory_chunk::link_terminator)
    {
        // Obtaining chunk memory.
        ipc_smc = worker_db->GetSharedMemoryChunkFromIndex(cur_chunk_index);

        // Copying current IPC chunk.
        memcpy(data_blob_ + data_bytes_offset, ipc_smc, cur_chunk_data_size);

        // Decreasing number of bytes left to be processed.
        data_bytes_offset += cur_chunk_data_size;
        bytes_left -= cur_chunk_data_size;
        if (bytes_left < starcounter::MixedCodeConstants::CHUNK_MAX_DATA_BYTES)
            cur_chunk_data_size = bytes_left;

        // Getting next chunk in chain.
        cur_chunk_index = ipc_smc->get_link();
    }

    // Checking that maximum number of WSABUFs in chunk is correct.
    GW_ASSERT(0 == bytes_left);

    return 0;
}

// Copies gateway chunk to IPC chunks.
uint32_t SocketDataChunk::CopyGatewayChunkToIPCChunks(
    WorkerDbInterface* worker_db,
    SocketDataChunk** new_ipc_sd,
    core::chunk_index* db_chunk_index,
    uint16_t* num_ipc_chunks)
{
    shared_memory_chunk* ipc_smc;

    uint32_t err_code = worker_db->GetOneChunkFromPrivatePool(db_chunk_index, &ipc_smc);
    if (err_code)
        return err_code;

    *new_ipc_sd = (SocketDataChunk*)((uint8_t*)ipc_smc + MixedCodeConstants::CHUNK_OFFSET_SOCKET_DATA);

    // Copying only socket data info without data buffer.
    memcpy(*new_ipc_sd, this, SOCKET_DATA_OFFSET_BLOB);

    int32_t actual_written_bytes = 0;

    // Copying data buffer separately.
    err_code = worker_db->WriteBigDataToIPCChunks(
        get_accum_buf()->get_chunk_orig_buf_ptr(),
        get_accum_buf()->get_accum_len_bytes(),
        *db_chunk_index,
        MixedCodeConstants::CHUNK_OFFSET_SOCKET_DATA + SOCKET_DATA_OFFSET_BLOB,
        &actual_written_bytes,
        num_ipc_chunks);

    GW_ASSERT(actual_written_bytes == get_accum_buf()->get_accum_len_bytes());

    return err_code;
}

// Deletes global session and sends message to database to delete session there.
uint32_t SocketDataChunk::SendDeleteSession(GatewayWorker* gw)
{
    // Verifying that session is correct and sending delete session to database.
    if (CompareUniqueSocketId() && CompareGlobalSessionSalt())
    {
        ScSessionStruct s = g_gateway.GetGlobalSessionCopy(socket_info_index_);

        return (gw->GetWorkerDb(GetDestDbIndex())->PushSessionDestroy(s.linear_index_, s.random_salt_, s.scheduler_id_));
    }

    return 0;
}

#ifdef GW_LOOPED_TEST_MODE

// Pushing given sd to network emulation queue.
// NOTE: Passing socket data as a pointer, not reference,
// since there is no need to pass a reference here
// (if something sd is converted to null outside this function).
void SocketDataChunk::PushToMeasuredNetworkEmulationQueue(GatewayWorker* gw)
{
    gw->PushToMeasuredNetworkEmulationQueue(this);
}

void SocketDataChunk::PushToPreparationNetworkEmulationQueue(GatewayWorker* gw)
{
    gw->PushToPreparationNetworkEmulationQueue(this);
}

#endif

} // namespace network
} // namespace starcounter
