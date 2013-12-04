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
    db_index_type db_index,
    core::chunk_index chunk_index,
    worker_id_type bound_worker_id)
{
    flags_ = 0;

    socket_info_index_ = INVALID_SESSION_INDEX;

    db_index_ = db_index;

    ResetUserDataOffset();

    user_data_written_bytes_ = 0;
    socket_info_index_ = socket_info_index;
    
    session_.Reset();

    chunk_index_ = chunk_index;

    // Checking if its an aggregation socket.
    if (g_gateway.IsAggregatingPort(socket_info_index))
    {
        GatewayMemoryChunk* gwc = g_gateway.ObtainGatewayMemoryChunk();
        set_big_accumulation_chunk_flag();
        accum_buf_.Init(gwc->buffer_len_bytes_, gwc->buf_, true);
        accum_buf_.set_first_chunk_orig_buf_ptr((uint8_t*)gwc);
    }
    else
    {
        // Configuring data buffer.
        ResetAccumBuffer();
    }

    set_to_database_direction_flag();

    set_type_of_network_oper(UNKNOWN_SOCKET_OPER);
    SetTypeOfNetworkProtocol(MixedCodeConstants::NetworkProtocolType::PROTOCOL_HTTP1);

    set_unique_socket_id(g_gateway.GetUniqueSocketId(socket_info_index));
    set_bound_worker_id(bound_worker_id);

    num_chunks_ = 1;

    // Initializing HTTP/WEBSOCKETS data structures.
    get_http_proto()->Reset();
    get_ws_proto()->Reset();
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

    // Checking if big gateway chunk is used.
    if (get_big_accumulation_chunk_flag())
    {
        flags_ = 0;
        accum_buf_.ResetToOriginalState();
        set_big_accumulation_chunk_flag();
    }
    else
    {
        flags_ = 0;
        ResetAccumBuffer();
    }
}

// Gets last linked smc.
shared_memory_chunk* SocketDataChunk::ObtainLastLinkedSmc(GatewayWorker* gw)
{
    if (1 == num_chunks_)
        return get_smc();

    return gw->GetSmcFromChunkIndex(db_index_, get_smc()->get_next());
}

// Returns gateway chunk to gateway if any.
void SocketDataChunk::ReturnGatewayChunk()
{
    if (get_big_accumulation_chunk_flag())
    {
        GatewayMemoryChunk* gmc = (GatewayMemoryChunk*) accum_buf_.get_first_chunk_orig_buf_ptr();
        GW_ASSERT_DEBUG(AGGREGATION_BUFFER_SIZE == gmc->buffer_len_bytes_);

        g_gateway.ReturnGatewayMemoryChunk(gmc);
        reset_big_accumulation_chunk_flag();
    }
}

// Continues accumulation if needed.
uint32_t SocketDataChunk::ContinueAccumulation(GatewayWorker* gw, bool* is_accumulated)
{
    uint32_t err_code;
    *is_accumulated = false;

    // Checking if we have not completely accumulated all data.
    if (!accum_buf_.IsAccumulationComplete())
    {
        // Checking if current chunk buffer is full.
        if (accum_buf_.IsBufferFilled())
        {
            // Getting last chunk where we previously accumulated data.
            shared_memory_chunk* last_smc = ObtainLastLinkedSmc(gw);

            // Getting new chunk and attaching to last one to it.
            WorkerDbInterface* worker_db = gw->GetWorkerDb(db_index_);
            core::chunk_index new_chunk_index;
            shared_memory_chunk *new_smc;
            err_code = worker_db->GetOneChunkFromPrivatePool(&new_chunk_index, &new_smc);
            if (err_code)
                return err_code;

            // Incrementing number of chunks.
            num_chunks_++;

            // Linking new chunk to current chunk.
            last_smc->set_link(new_chunk_index);

            // Saving last linked chunk index.
            SaveLastLinkedChunk(new_chunk_index);

            // Setting new chunk as a new buffer.
            accum_buf_.Init(MixedCodeConstants::CHUNK_MAX_DATA_BYTES, (uint8_t*)new_smc, false);
        }
    }
    else
    {
        // All data has been received.
        *is_accumulated = true;

        // Restoring the socket data link.
        accum_buf_.RestoreToFirstChunk();
        get_smc()->terminate_next();
    }

    return 0;
}

// Clones existing socket data chunk for receiving.
uint32_t SocketDataChunk::CloneToReceive(GatewayWorker *gw)
{
    // Only socket representer can clone to its receive.
    if (!get_socket_representer_flag())
        return 0;

    SocketDataChunk* sd_clone = NULL;

    // NOTE: Cloning to receive only on database 0 chunks.
    uint32_t err_code = gw->CreateSocketData(socket_info_index_, 0, sd_clone);
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
    GW_COUT << "Cloned socket " << socket_info_index_ << ":" << GetSocket() << ":" << unique_socket_id_ << ":" << chunk_index_ << ":" << (uint64_t)this << " to socket " <<
        sd_clone->get_socket_info_index() << ":" << sd_clone->GetSocket() << ":" << sd_clone->get_unique_socket_id() << ":" << sd_clone->get_chunk_index() << ":" << (uint64_t)sd_clone << GW_ENDL;
#endif

    return 0;
}

// Start sending on socket.
uint32_t SocketDataChunk::SendMultipleChunks(GatewayWorker* gw, uint32_t *num_sent_bytes)
{
    set_type_of_network_oper(SEND_SOCKET_OPER);

    memset(&ovl_, 0, OVERLAPPED_SIZE);

#ifdef GW_LOOPED_TEST_MODE
    PushToMeasuredNetworkEmulationQueue(gw);
    return WSA_IO_PENDING;
#endif

    // Getting the contents of WSABUFs chunk.
    WSABUF* wsa_bufs = (WSABUF*) gw->GetSmcFromChunkIndex(db_index_, GetNextLinkedChunkIndex());

    // Getting socket number.
    SOCKET s = GetSocket();

    // NOTE: Need to subtract one chunks from being included in send.
    return WSASend(s, wsa_bufs, num_chunks_ - 1, (LPDWORD)num_sent_bytes, 0, &ovl_, NULL);
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
    uint32_t err_code = gw->CreateSocketData(socket_info_index, g_gateway.get_num_dbs_slots() - 1, sd);
    if (err_code)
    {
        // New chunk can not be obtained.
        return err_code;
    }

    AccumBuffer* accum_buf = sd->get_accum_buf();

    // Checking if data fits inside chunk.
    if (data_len < (int32_t)accum_buf->get_chunk_num_available_bytes())
    {
        // Checking if message should be copied.
        memcpy(accum_buf->get_chunk_orig_buf_ptr(), data, data_len);
    }
    else // Multiple chunks response.
    {
        WorkerDbInterface* worker_db = gw->GetWorkerDb(sd->get_db_index());
        int32_t total_processed_bytes = 0;

        // Copying user data to multiple chunks.
        err_code = worker_db->WriteBigDataToChunks(
            data,
            data_len,
            sd->get_chunk_index(),
            &total_processed_bytes,
            sd->GetAccumOrigBufferChunkOffset(),
            false,
            true
            );

        if (err_code)
            return err_code;

        GW_ASSERT(total_processed_bytes == data_len);
    }

    *new_sd = sd;

    return 0;
}

// Clone current socket data to push it.
uint32_t SocketDataChunk::CloneToPush(
    GatewayWorker* gw,
    SocketDataChunk** new_sd)
{
    // TODO: Add support for linked chunks.
    GW_ASSERT(1 == get_num_chunks());

#ifndef GW_MEMORY_MANAGEMENT

    core::chunk_index new_chunk_index;
    shared_memory_chunk* new_smc;

    // Getting a chunk from new database.
    uint32_t err_code = gw->GetWorkerDb(db_index_)->GetOneChunkFromPrivatePool(&new_chunk_index, &new_smc);
    if (err_code)
    {
        // New chunk can not be obtained.
        return err_code;
    }

    // Copying chunk data.
    memcpy(new_smc, get_smc(), MixedCodeConstants::SHM_CHUNK_SIZE);

    // Socket data inside chunk.
    (*new_sd) = (SocketDataChunk*)((uint8_t*)new_smc + MixedCodeConstants::CHUNK_OFFSET_SOCKET_DATA);

    // Changing new chunk index.
    (*new_sd)->set_chunk_index(new_chunk_index);

#else

    (*new_sd) = gw->GetWorkerChunks()->ObtainChunk();

    (*new_sd)->CopyFromAnotherSocketData(this);

    // Changing new chunk index.
    (*new_sd)->set_chunk_index(INVALID_CHUNK_INDEX);

#endif

    // Adjusting accumulative buffer.
    int32_t offset_bytes_from_sd = static_cast<int32_t> (get_accum_buf()->get_chunk_orig_buf_ptr() - (uint8_t*) this);
    (*new_sd)->get_accum_buf()->CloneBasedOnNewBaseAddress((uint8_t*) (*new_sd) + offset_bytes_from_sd, get_accum_buf());

    // This socket becomes unattached.
    (*new_sd)->reset_socket_representer_flag();
    (*new_sd)->reset_socket_diag_active_conn_flag();

    return 0;
}

// Clone current socket data to another database.
uint32_t SocketDataChunk::CloneToAnotherDatabase(
    GatewayWorker*gw,
    int32_t new_db_index,
    SocketDataChunk** new_sd)
{
    core::chunk_index new_db_chunk_index;
    shared_memory_chunk* new_db_smc;

    // Getting a chunk from new database.
    WorkerDbInterface* new_worker_db = gw->GetWorkerDb(new_db_index);
    GW_ASSERT(NULL != new_worker_db);

    uint32_t err_code = new_worker_db->GetOneChunkFromPrivatePool(&new_db_chunk_index, &new_db_smc);
    if (err_code)
        return err_code;

#ifndef GW_MEMORY_MANAGEMENT

    // Copying chunk data.
    memcpy(new_db_smc, get_smc(), MixedCodeConstants::SHM_CHUNK_SIZE);

#else

    // Copying chunk data.
    memcpy(new_db_smc + MixedCodeConstants::CHUNK_OFFSET_SOCKET_DATA, this, MixedCodeConstants::SHM_CHUNK_SIZE);

#endif

    // Sealing the new chunk.
    new_db_smc->terminate_link();
    new_db_smc->terminate_next();

    // Socket data inside chunk.
    (*new_sd) = (SocketDataChunk*)((uint8_t*)new_db_smc + MixedCodeConstants::CHUNK_OFFSET_SOCKET_DATA);

    // Attaching to new database.
    (*new_sd)->AttachToDatabase(new_db_index);

    // Changing new chunk index.
    (*new_sd)->set_chunk_index(new_db_chunk_index);
    (*new_sd)->reset_socket_representer_flag();
    (*new_sd)->reset_socket_diag_active_conn_flag();

    // Adjusting accumulative buffer.
    int32_t offset_bytes_from_sd = static_cast<int32_t> (get_accum_buf()->get_chunk_orig_buf_ptr() - (uint8_t*) this);
    (*new_sd)->get_accum_buf()->CloneBasedOnNewBaseAddress((uint8_t*) (*new_sd) + offset_bytes_from_sd, get_accum_buf());

#ifndef GW_MEMORY_MANAGEMENT

    core::chunk_index prev_db_chunk_index = get_smc()->get_link();
    shared_memory_chunk* prev_db_smc;

    // Copying all linked chunks.
    for (uint16_t i = 0; i < num_chunks_ - 1; i++)
    {
        GW_ASSERT(INVALID_CHUNK_INDEX != prev_db_chunk_index);

        shared_memory_chunk* cur_new_db_smc = new_db_smc;

        err_code = new_worker_db->GetOneChunkFromPrivatePool(&new_db_chunk_index, &new_db_smc);
        if (err_code)
            return err_code;

        // Getting link to previous database linked chunk.
        prev_db_smc = gw->GetSmcFromChunkIndex(db_index_, prev_db_chunk_index);

        // Copying chunk data.
        memcpy(new_db_smc, prev_db_smc, MixedCodeConstants::SHM_CHUNK_SIZE);
        new_db_smc->terminate_link();
        new_db_smc->terminate_next();

        // Linking previous chunk to the newly obtained.
        cur_new_db_smc->set_link(new_db_chunk_index);

        prev_db_chunk_index = prev_db_smc->get_link();
    }

    GW_ASSERT(new_db_smc->is_terminated());

#endif

    return 0;
}

// Create WSA buffers.
uint32_t SocketDataChunk::CreateWSABuffers(
    WorkerDbInterface* worker_db,
    shared_memory_chunk* first_smc,
    uint32_t first_chunk_offset_bytes,
    uint32_t first_chunk_num_bytes,
    uint32_t total_bytes)
{
    // Getting total user data length.
    uint32_t bytes_left = total_bytes, cur_wsa_buf_offset = 0;

    // Looping through all chunks and creating corresponding
    // WSA buffers in the first chunk data blob.
    uint32_t cur_chunk_data_size = bytes_left;
    if (cur_chunk_data_size > starcounter::MixedCodeConstants::CHUNK_MAX_DATA_BYTES)
        cur_chunk_data_size = starcounter::MixedCodeConstants::CHUNK_MAX_DATA_BYTES;

    // Getting shared interface pointer.
    core::shared_interface* shared_int = worker_db->get_shared_int();

    // Extra WSABufs storage chunk.
    shared_memory_chunk* wsa_bufs_smc;
    uint32_t err_code;

    // Getting link to the first chunk in chain.
    shared_memory_chunk* smc = first_smc;
    core::chunk_index cur_chunk_index = smc->get_link();

    core::chunk_index wsa_bufs_chunk_index;

    // Getting new chunk from pool.
    err_code = worker_db->GetOneChunkFromPrivatePool(&wsa_bufs_chunk_index, &wsa_bufs_smc);
    if (err_code)
        return err_code;

    // Increasing number of chunks by one.
    num_chunks_++;

    // Inserting extra chunk in linked chunks.
    first_smc->set_link(wsa_bufs_chunk_index);
    wsa_bufs_smc->set_link(cur_chunk_index);

    // Checking if head chunk is involved.
    if (first_chunk_offset_bytes)
    {
        // Pointing to current WSABUF in blob.
        WSABUF* wsa_buf = (WSABUF*) ((uint8_t*)wsa_bufs_smc + cur_wsa_buf_offset);
        wsa_buf->len = first_chunk_num_bytes;
        wsa_buf->buf = (char *)first_smc + first_chunk_offset_bytes;
        cur_wsa_buf_offset += sizeof(WSABUF);

        // Decreasing number of bytes left to be processed.
        bytes_left -= first_chunk_num_bytes;
        if (bytes_left < starcounter::MixedCodeConstants::CHUNK_MAX_DATA_BYTES)
            cur_chunk_data_size = bytes_left;
    }

    // Until we get the last chunk in chain.
    while (cur_chunk_index != shared_memory_chunk::link_terminator)
    {
        // Obtaining chunk memory.
        smc = (shared_memory_chunk*) &(shared_int->chunk(cur_chunk_index));

        // Pointing to current WSABUF in blob.
        WSABUF* wsa_buf = (WSABUF*) ((uint8_t*)wsa_bufs_smc + cur_wsa_buf_offset);
        wsa_buf->len = cur_chunk_data_size;
        wsa_buf->buf = (char *)smc;
        cur_wsa_buf_offset += sizeof(WSABUF);

        // Decreasing number of bytes left to be processed.
        bytes_left -= cur_chunk_data_size;
        if (bytes_left < starcounter::MixedCodeConstants::CHUNK_MAX_DATA_BYTES)
            cur_chunk_data_size = bytes_left;

        // Getting next chunk in chain.
        cur_chunk_index = smc->get_link();
    }

    // Checking that maximum number of WSABUFs in chunk is correct.
    GW_ASSERT(num_chunks_ <= starcounter::bmx::MAX_EXTRA_LINKED_WSABUFS + 1);

    return 0;
}

// Returns all linked chunks except the main one.
uint32_t SocketDataChunk::ReturnExtraLinkedChunks(GatewayWorker* gw)
{
    // Checking if we have any linked chunks.
    if (1 == num_chunks_)
        return 0;

    // We have to return attached chunks.
    WorkerDbInterface *db = gw->GetWorkerDb(db_index_);
    GW_ASSERT(db != NULL);

    // Checking if any chunks are linked.
    shared_memory_chunk* smc = get_smc();

    // Checking that there are linked chunks.
    core::chunk_index first_linked_chunk = GetNextLinkedChunkIndex();
    GW_ASSERT(INVALID_CHUNK_INDEX != first_linked_chunk);

    // Restoring accumulative buffer.
    ResetAccumBuffer();

    // Returning all linked chunks back to pool.
    db->ReturnLinkedChunksToPool(num_chunks_ - 1, first_linked_chunk);

    // Since all linked chunks have been returned.
    smc->terminate_link();

    // Since chunks have been released.
    num_chunks_ = 1;

    return 0;
}

// Deletes global session and sends message to database to delete session there.
uint32_t SocketDataChunk::SendDeleteSession(GatewayWorker* gw)
{
    // Verifying that session is correct and sending delete session to database.
    if (CompareUniqueSocketId() && CompareGlobalSessionSalt())
    {
        ScSessionStruct s = g_gateway.GetGlobalSessionCopy(socket_info_index_);
        return (gw->GetWorkerDb(db_index_)->PushSessionDestroy(s.linear_index_, s.random_salt_, s.scheduler_id_));
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
