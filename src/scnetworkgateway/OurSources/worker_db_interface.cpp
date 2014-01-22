#include "static_headers.hpp"
#include "gateway.hpp"
#include "handlers.hpp"
#include "ws_proto.hpp"
#include "http_proto.hpp"
#include "socket_data.hpp"
#include "tls_proto.hpp"
#include "worker_db_interface.hpp"
#include "worker.hpp"
#include "common/macro_definitions.hpp"

namespace starcounter {
namespace network {

// Releases chunks from private chunk pool to the shared chunk pool.
uint32_t WorkerDbInterface::ReleaseToSharedChunkPool(int32_t num_ipc_chunks)
{
    // Release chunks from this worker threads private chunk pool to the shared chunk pool.
    int32_t released_chunks_num = static_cast<int32_t> (shared_int_.release_from_private_to_shared(
        private_chunk_pool_, num_ipc_chunks, &shared_int_.client_interface(), 1000));

    GW_ASSERT(released_chunks_num == num_ipc_chunks);

    return 0;
}

// Obtains chunk from a private pool if its not empty
// (otherwise fetches from shared chunk pool).
uint32_t WorkerDbInterface::GetOneChunkFromPrivatePool(
    core::chunk_index* chunk_index,
    shared_memory_chunk** ipc_smc)
{
    // Trying to fetch chunk from private pool.
    uint32_t err_code;
    while (!private_chunk_pool_.acquire_linked_chunks_counted(&shared_int_.chunk(0), *chunk_index, 1))
    {
        // Getting chunks from shared chunk pool.
        err_code = AcquireIPCChunksFromSharedPool(MAX_CHUNKS_IN_PRIVATE_POOL);
        if (err_code)
            return err_code;
    }

    // Getting data pointer.
    (*ipc_smc) = (shared_memory_chunk *)(&shared_int_.chunk(*chunk_index));

#ifdef GW_CHUNKS_DIAG
    GW_PRINT_WORKER_DB << "Getting new chunk: " << *chunk_index << GW_ENDL;
#endif

    return 0;
}

// Scans all channels for any incoming chunks.
uint32_t WorkerDbInterface::ScanChannels(GatewayWorker *gw, uint32_t& next_sleep_interval_ms)
{
    core::chunk_index ipc_first_chunk_index;
    uint32_t err_code;

    // Number of popped chunks on all channels.
    int32_t num_popped_chunks = 0;

    // Indicates if chunk was popped on any channel.
    bool chunk_popped = true;

    // Looping until we have chunks to pop and we don't exceed the maximum number.
    while (chunk_popped && (num_popped_chunks < MAX_CHUNKS_TO_POP_AT_ONCE))
    {
        // Flag will be set if any chunk is popped.
        chunk_popped = false;

        // Running through all channels.
        for (scheduler_id_type sched_id = 0; sched_id < num_schedulers_; sched_id++)
        {
            // Obtaining the channel.
            core::channel_type& the_channel = shared_int_.channel(channels_[sched_id]);

            // Trying to pop the chunk from channel.
            if (false == the_channel.out.try_pop_back(&ipc_first_chunk_index))
                continue;

            // Chunk was found.
            chunk_popped = true;
            num_popped_chunks++;

            // A message on channel ch was received. Notify the database
            // that the out queue in this channel is not full.
            the_channel.scheduler()->notify(shared_int_.scheduler_work_event(the_channel.get_scheduler_number()));

            // Get the chunk.
            shared_memory_chunk* ipc_smc = (shared_memory_chunk*) GetSharedMemoryChunkFromIndex(ipc_first_chunk_index);

            // Check if its a BMX handlers management message.
            if (bmx::BMX_MANAGEMENT_HANDLER_INFO == ipc_smc->get_bmx_handler_info())
            {
                GW_ASSERT(ipc_smc->is_terminated());

                // Entering global lock.
                gw->EnterGlobalLock();

                // Handling management chunks.
                err_code = HandleManagementChunks(sched_id, gw, ipc_smc);

                // Releasing management chunks.
                ReturnLinkedChunksToPool(ipc_first_chunk_index);

                // Releasing global lock.
                gw->LeaveGlobalLock();

                if (err_code)
                    return err_code;

                continue;
            }

            SocketDataChunk* ipc_sd = (SocketDataChunk*)((uint8_t *)ipc_smc + MixedCodeConstants::CHUNK_OFFSET_SOCKET_DATA);

            SocketDataChunk* sd = gw->GetWorkerChunks()->ObtainChunk(ipc_sd->get_user_data_length_bytes());

            // Checking if its one or several chunks.
            if (ipc_smc->is_terminated())
            {
                sd->CopyFromOneChunkIPCSocketData(ipc_sd, ipc_sd->get_user_data_length_bytes());
            }
            else
            {
                err_code = sd->CopyIPCChunksToGatewayChunk(this, ipc_sd);

                if (err_code)
                    return err_code;
            }

            // Releasing IPC chunks.
            ReturnLinkedChunksToPool(ipc_first_chunk_index);

            // Initializing socket data that arrived from database.
            sd->PreInitSocketDataFromDb();

            // Checking that socket arrived on correct worker.
            GW_ASSERT(sd->get_bound_worker_id() == worker_id_);
            GW_ASSERT(sd->GetBoundWorkerId() == worker_id_);

            // Checking for socket data correctness.
            GW_ASSERT(sd->get_socket_info_index() < g_gateway.setting_max_connections());

#ifdef GW_CHUNKS_DIAG
            GW_PRINT_WORKER_DB << "Popping chunk: socket index " << sd->get_socket_info_index() << ":" << sd->get_unique_socket_id() << ":" << (uint64_t)sd << GW_ENDL;
#endif

            // NOTE: We always override the global session with active session received from database.
            sd->ForceSetGlobalSessionIfEmpty();

            // Checking if data is aggregated.
            /*if (sd->GetSocketAggregatedFlag())
            {
                char body[1024];
                int32_t body_len = sd->get_http_proto()->get_http_request()->content_len_bytes_;
                memcpy(body, (char*)sd + sd->get_http_proto()->get_http_request()->content_offset_, body_len);
                err_code = gw->SendHttpBody(sd, body, body_len);
                GW_ASSERT (0 == err_code);
                continue;
            }*/

            // Put the chunk into from database queue.
            err_code = gw->RunFromDbHandlers(sd);

            // Checking if any error occurred during socket operations.
            if (err_code)
            {
                // Disconnecting this socket data.
                gw->DisconnectAndReleaseChunk(sd);

                // Releasing the cloned chunk.
                gw->ProcessReceiveClones(true);
            }
            else
            {
                // Processing clones during last iteration.
                gw->ProcessReceiveClones(false);
            }
        }
    }

    // Push everything from overflow queue.
    PushOverflowChunks();

    // Checking if any chunks were popped.
    if (num_popped_chunks > 0)
        next_sleep_interval_ms = 0;

    return 0;
}

// Writes given big linear buffer into obtained linked chunks.
uint32_t WorkerDbInterface::WriteBigDataToIPCChunks(
    uint8_t* buf,
    int32_t buf_len_bytes,
    starcounter::core::chunk_index cur_chunk_index,
    int32_t first_chunk_offset,
    int32_t* actual_written_bytes,
    uint16_t* num_ipc_chunks
    )
{
    // Maximum number of bytes that will be written in this call.
    int32_t num_bytes_left_first_chunk = starcounter::MixedCodeConstants::CHUNK_MAX_DATA_BYTES - first_chunk_offset;

    // Number of chunks to use.
    int32_t num_extra_chunks = 0;
    if (buf_len_bytes > num_bytes_left_first_chunk)
        num_extra_chunks = ((buf_len_bytes - num_bytes_left_first_chunk) / starcounter::MixedCodeConstants::CHUNK_MAX_DATA_BYTES) + 1;

    // Getting chunk memory address.
    uint8_t* cur_chunk_buf = (uint8_t *)(&shared_int_.chunk(cur_chunk_index));

    // Setting total number of IPC chunks.
    *num_ipc_chunks = num_extra_chunks + 1;

    // Setting number of total written bytes.
    *actual_written_bytes = buf_len_bytes;

    // Setting the number of written bytes.
    *(uint32_t*)(cur_chunk_buf + starcounter::MixedCodeConstants::CHUNK_OFFSET_USER_DATA_WRITTEN_BYTES) = buf_len_bytes;

    // Checking if the original chunk is sufficient.
    if (0 == num_extra_chunks)
    {
        memcpy(cur_chunk_buf + first_chunk_offset, buf, buf_len_bytes);

        return 0;
    }

    // Checking if more than maximum chunks we can take at once.
    //GW_ASSERT(num_extra_chunks <= MixedCodeConstants::MAX_EXTRA_LINKED_IPC_CHUNKS);

    // Acquiring linked chunks.
    starcounter::core::chunk_index new_chunk_index;
    uint32_t err_code = GetMultipleChunksFromPrivatePool(&new_chunk_index, num_extra_chunks);
    GW_ASSERT(0 == err_code);

    // Linking to the first chunk.
    ((shared_memory_chunk*)cur_chunk_buf)->set_link(new_chunk_index);

    // Going through each linked chunk and write data there.
    int32_t left_bytes_to_write = buf_len_bytes;
    int32_t num_bytes_to_write_in_chunk = starcounter::MixedCodeConstants::CHUNK_MAX_DATA_BYTES;

    // Writing to first chunk.
    memcpy(cur_chunk_buf + first_chunk_offset, buf, num_bytes_left_first_chunk);
    left_bytes_to_write -= num_bytes_left_first_chunk;

    // Checking how many bytes to write next time.
    if (left_bytes_to_write < starcounter::MixedCodeConstants::CHUNK_MAX_DATA_BYTES)
    {
        // Checking if we copied everything.
        if (left_bytes_to_write <= 0)
            return 0;

        num_bytes_to_write_in_chunk = left_bytes_to_write;
    }

    // Processing until have some bytes to write.
    while (true)
    {
        // Getting next chunk in chain.
        cur_chunk_index = ((shared_memory_chunk*)cur_chunk_buf)->get_link();

        // Getting chunk memory address.
        cur_chunk_buf = (uint8_t *)(&shared_int_.chunk(cur_chunk_index));

        // Copying memory.
        memcpy(cur_chunk_buf, buf + buf_len_bytes - left_bytes_to_write, num_bytes_to_write_in_chunk);
        left_bytes_to_write -= num_bytes_to_write_in_chunk;

        // Checking how many bytes to write next time.
        if (left_bytes_to_write < starcounter::MixedCodeConstants::CHUNK_MAX_DATA_BYTES)
        {
            // Checking if we copied everything.
            if (left_bytes_to_write <= 0)
                break;

            num_bytes_to_write_in_chunk = left_bytes_to_write;
        }
    }

    return 0;
}

// Push given chunk to database queue.
void WorkerDbInterface::PushLinkedChunksToDb(
    core::chunk_index the_chunk_index,
    int16_t sched_id)
{
    // Assuring that session goes to correct scheduler.
    GW_ASSERT (sched_id < num_schedulers_);

    // Obtaining the channel.
    core::channel_type& the_channel = shared_int_.channel(channels_[sched_id]);

    // Trying to push chunk if overflow is empty.
    if ((the_channel.in_overflow().not_empty()) ||
        (!TryPushToChannel(the_channel, the_chunk_index)))
    {
#ifdef GW_CHUNKS_DIAG
        GW_PRINT_WORKER << "Couldn't push chunk into channel. Putting to overflow pool." << GW_ENDL;
#endif

        // The overflow queue is not empty so the message is first pushed to
        // the overflow queue, to preserve the order of production.
        the_channel.in_overflow().push_back(the_chunk_index);
    }
}

// Returns given chunk to private chunk pool.
// NOTE: This function should always succeed.
void WorkerDbInterface::ReturnLinkedChunksToPool(core::chunk_index& first_linked_chunk)
{
#ifdef GW_CHUNKS_DIAG
    GW_PRINT_WORKER_DB << "Returning linked chunks to pool: " << first_linked_chunk << GW_ENDL;
#endif

    // Releasing chunk to private pool.
    bool success = private_chunk_pool_.release_linked_chunks(&shared_int_.chunk(0), first_linked_chunk);
    GW_ASSERT(success == true);

    // Check if there are too many private chunks so
    // we need to release them to the shared chunk pool.
    if (private_chunk_pool_.size() > MAX_CHUNKS_IN_PRIVATE_POOL_DOUBLE)
    {
        uint32_t err_code = ReleaseToSharedChunkPool(static_cast<int32_t> (private_chunk_pool_.size() - MAX_CHUNKS_IN_PRIVATE_POOL));

        // NOTE: The error can happen when for example the database is already dead
        // but for the future with centralized chunk pool this should never happen!

        // TODO: Uncomment when centralized chunks are ready!
        //GW_ASSERT(0 == err_code);
    }

    first_linked_chunk = INVALID_CHUNK_INDEX;
}

// Releases all private chunks to shared chunk pool.
void WorkerDbInterface::ReturnAllPrivateChunksToSharedPool()
{
    // Checking if there are any chunks in private pool.
    if (private_chunk_pool_.size() > 0)
    {
        uint32_t err_code = ReleaseToSharedChunkPool(static_cast<int32_t> (private_chunk_pool_.size()));

        // NOTE: The error can happen when for example the database is already dead
        // but for the future with centralized chunk pool this should never happen!

        // TODO: Uncomment when centralized chunks are ready!
        //GW_ASSERT(0 == err_code);
    }
}

// Push given chunk to database queue.
uint32_t WorkerDbInterface::PushSocketDataToDb(
    GatewayWorker* gw,
    SocketDataChunkRef sd,
    BMX_HANDLER_TYPE user_handler_id)
{
#ifdef GW_CHUNKS_DIAG
    GW_PRINT_WORKER_DB << "Pushing chunk: socket index " << sd->get_socket_info_index() << ":" << sd->get_unique_socket_id() << ":" << (uint64_t)sd << GW_ENDL;
#endif

    uint16_t num_ipc_chunks;
    core::chunk_index first_chunk_index;
    SocketDataChunk* ipc_sd;
    uint32_t err_code = sd->CopyGatewayChunkToIPCChunks(this, &ipc_sd, &first_chunk_index, &num_ipc_chunks);
    GW_ASSERT(0 == err_code);

    // Returning gateway chunk to pool.
    gw->ReturnSocketDataChunksToPool(sd);

    // Setting number of chunks.
    ipc_sd->SetNumberOfIPCChunks(num_ipc_chunks);
    
    // Obtaining the current scheduler id.
    scheduler_id_type sched_id = 255;

    // Checking if socket has active session.
    if (ipc_sd->HasActiveSession())
        sched_id = ipc_sd->get_scheduler_id();

    // Modifying chunk data to use correct handler.
    shared_memory_chunk *ipc_smc = (shared_memory_chunk*)((uint8_t*)ipc_sd - MixedCodeConstants::CHUNK_OFFSET_SOCKET_DATA);
    ipc_smc->set_bmx_handler_info(user_handler_id); // User code handler id.
    ipc_smc->set_request_size(4);

    // Checking scheduler id validity.
    if (sched_id >= num_schedulers_)
    {
        sched_id = GenerateSchedulerId();
        ipc_sd->set_scheduler_id(sched_id);
    }

    // Checking that scheduler is correct for this database.
    GW_ASSERT(sched_id < num_schedulers_);

    // Pushing socket data as a chunk.
    PushLinkedChunksToDb(first_chunk_index, sched_id);

    return 0;
}

// Pushes session destroyed message.
uint32_t WorkerDbInterface::PushSessionDestroy(
    session_index_type linear_index,
    random_salt_type random_salt,
    uint8_t sched_id)
{
    // Get a reference to the chunk.
    shared_memory_chunk *ipc_smc = NULL;

    // Getting a free chunk.
    core::chunk_index new_chunk;
    uint32_t err_code = GetOneChunkFromPrivatePool(&new_chunk, &ipc_smc);
    GW_ERR_CHECK(err_code);

    // Predefined BMX management handler.
    ipc_smc->set_bmx_handler_info(bmx::BMX_MANAGEMENT_HANDLER_INFO);

    request_chunk_part* request = ipc_smc->get_request_chunk();
    request->reset_offset();

    // Writing BMX message type.
    request->write(bmx::BMX_SESSION_DESTROY);

    // Writing Apps unique session number.
    request->write(linear_index);

    // Writing Apps unique salt.
    request->write(random_salt);

    // Pushing the chunk.
    PushLinkedChunksToDb(new_chunk, sched_id);

    return 0;
}

// Sends error message.
uint32_t WorkerDbInterface::PushErrorMessage(
    scheduler_id_type sched_id,
    uint32_t err_code_num,
    const wchar_t* const err_msg)
{
    // Get a reference to the chunk.
    shared_memory_chunk *ipc_smc = NULL;

    // Getting a free chunk.
    core::chunk_index new_chunk_index;
    uint32_t err_code = GetOneChunkFromPrivatePool(&new_chunk_index, &ipc_smc);
    if (err_code)
        return err_code;

    // Predefined BMX management handler.
    ipc_smc->set_bmx_handler_info(bmx::BMX_MANAGEMENT_HANDLER_INFO);

    request_chunk_part* request = ipc_smc->get_request_chunk();
    request->reset_offset();

    // Writing BMX message type.
    request->write(bmx::BMX_ERROR);

    // Writing error code number.
    request->write(err_code_num);

    // Writing error string.
    request->write_wstring(err_msg, static_cast<uint32_t> (wcslen(err_msg)));

    // Pushing the chunk.
    PushLinkedChunksToDb(new_chunk_index, sched_id);

    return 0;
}

// Allocates different channels and pools.
WorkerDbInterface::WorkerDbInterface(
    const int32_t new_db_index,
    const int32_t worker_id)
{
    channels_ = NULL;

    Reset();

    // Setting private/overflow chunk pool capacity.
    private_chunk_pool_.set_capacity(core::chunks_total_number_max);

    db_index_ = new_db_index;
    worker_id_ = worker_id;

    ActiveDatabase* active_db = g_gateway.GetDatabase(db_index_);

    // Initializing worker shared memory interface.
    shared_int_.init(
        active_db->get_shm_seg_name().c_str(),
        g_gateway.get_shm_monitor_int_name().c_str(),
        g_gateway.get_gateway_pid(),
        g_gateway.get_gateway_owner_id());

    // Wait until the scheduler interface has been properly initialized.
    while (
        shared_int_.common_scheduler_interface().number_of_active_schedulers()
        != shared_int_.common_scheduler_interface().scheduler_count()
        ) ::Sleep(0);

    // Allocating channels.
    num_schedulers_ = static_cast<int32_t> (shared_int_.common_scheduler_interface().number_of_active_schedulers());
    channels_ = new core::channel_number[num_schedulers_];

    // Getting unique client interface for this worker.
    bool shared_int_acquired = shared_int_.acquire_client_number2(worker_id);
    GW_ASSERT(true == shared_int_acquired);

#if 0
	bool shared_int_acquired = shared_int_.acquire_client_number();
    GW_ASSERT(true == shared_int_acquired);
#endif

#ifdef GW_DATABASES_DIAG
    GW_PRINT_WORKER << "Database \"" << active_db->get_db_name() <<
        "\" acquired client interface " << shared_int_.get_client_number() << " and " << num_schedulers_ << " channel(s): ";
#endif

    // Acquiring unique channel for each scheduler.
    for (int32_t s = 0; s < num_schedulers_; ++s)
    {
		channels_[s] = (worker_id * num_schedulers_) + s;
        bool channel_acquired = shared_int_.acquire_channel2(channels_[s], static_cast<core::scheduler_number> (s));
        GW_ASSERT(true == channel_acquired);

#ifdef GW_DATABASES_DIAG
        GW_COUT << channels_[s] << ", ";
#endif
    }

#ifdef GW_DATABASES_DIAG
    GW_COUT << GW_ENDL;
#endif
}

// Declares gateway ready for database pushes.
uint32_t WorkerDbInterface::SetGatewayReadyForDbPushes()
{
    // Signal to server that worker is available. Needed for server push.
    shared_int_.client_interface().set_available();

    return 0;
}

// Handles management chunks.
uint32_t WorkerDbInterface::HandleManagementChunks(
    scheduler_id_type sched_id,
    GatewayWorker *gw,
    shared_memory_chunk* ipc_smc)
{
    // Getting the response part of the chunk.
    response_chunk_part* resp_chunk = ipc_smc->get_response_chunk();
    uint32_t response_size = resp_chunk->get_offset();
    GW_ASSERT (0 != response_size);

    uint32_t err_code = 0;
    uint32_t offset = 0;

    resp_chunk->reset_offset();
    while (offset < response_size)
    {
        // Reading BMX message type.
        uint8_t bmx_type = resp_chunk->read_uint8();

        switch (bmx_type)
        {
            case bmx::BMX_PONG:
            {
                uint64_t ping_data = -1;
                
                // Jumping over 8 bytes because we reseted the offset.
                resp_chunk->skip(8);

                err_code = sc_bmx_parse_pong(ipc_smc, &ping_data);
                if (err_code)
                    return err_code;

                GW_PRINT_WORKER << "Pong with data: " << ping_data << GW_ENDL;

                return 0;
            }

            case bmx::BMX_REGISTER_PORT:
            {
                // Reading handler info.
                BMX_HANDLER_TYPE handler_info = resp_chunk->read_handler_info();

                // Reading port number.
                uint16_t port = resp_chunk->read_uint16();

#ifdef GW_TESTING_MODE
                if ((g_gateway.setting_mode() != GatewayTestingMode::MODE_GATEWAY_SMC_RAW) &&
                    (g_gateway.setting_mode() != GatewayTestingMode::MODE_GATEWAY_SMC_APPS_RAW))
                {
                    GW_ASSERT(false);
                }
#endif

                GW_PRINT_WORKER << "New port " << port << " user handler registration with handler id: " << handler_info << GW_ENDL;

                // Registering handler on active database.
                HandlersTable* handlers_table = g_gateway.GetDatabase(db_index_)->get_user_handlers();

                // Registering handler on active database.
                err_code = g_gateway.AddPortHandler(
                    gw,
                    handlers_table,
                    port,
                    handler_info,
                    db_index_,
                    AppsPortProcessData);

                if (err_code)
                {
                    wchar_t temp_str[MixedCodeConstants::MAX_URI_STRING_LEN];
                    swprintf_s(temp_str, MixedCodeConstants::MAX_URI_STRING_LEN, L"Can't register port handler on port %d", port);

                    // Pushing error message to initial database.
                    PushErrorMessage(sched_id, err_code, temp_str);

                    // Ignoring error code if its existing handler.
                    if (SCERRHANDLERALREADYREGISTERED == err_code)
                        err_code = 0;
                }

                break;
            }
            
            case bmx::BMX_REGISTER_PORT_SUBPORT:
            {
                // Reading handler info.
                BMX_HANDLER_TYPE handler_info = resp_chunk->read_handler_info();

                // Reading port number.
                uint16_t port = resp_chunk->read_uint16();

                // Reading subport.
                bmx::BMX_SUBPORT_TYPE subport = resp_chunk->read_uint32();

                GW_PRINT_WORKER << "New subport " << subport << " port " << port << " user handler registration with handler id: " << handler_info << GW_ENDL;
                
                // Registering handler on active database.
                HandlersTable* handlers_table = g_gateway.GetDatabase(db_index_)->get_user_handlers();

                // Registering handler on active database.
                err_code = g_gateway.AddSubPortHandler(
                    gw,
                    handlers_table,
                    port,
                    subport,
                    handler_info,
                    db_index_,
                    AppsSubportProcessData);

                if (err_code)
                {
                    wchar_t temp_str[MixedCodeConstants::MAX_URI_STRING_LEN];
                    swprintf_s(temp_str, MixedCodeConstants::MAX_URI_STRING_LEN, L"Can't register sub-port handler on port %d", port);

                    // Pushing error message to initial database.
                    PushErrorMessage(sched_id, err_code, temp_str);

                    // Ignoring error code if its existing handler.
                    if (SCERRHANDLERALREADYREGISTERED == err_code)
                        err_code = 0;
                }

                break;
            }

            case bmx::BMX_REGISTER_URI:
            {
                // Reading handler info.
                BMX_HANDLER_TYPE handler_info = resp_chunk->read_handler_info();

                // Reading port number.
                uint16_t port = resp_chunk->read_uint16();

                // Reading URIs.
                char original_uri_info[MixedCodeConstants::MAX_URI_STRING_LEN];
                uint32_t original_uri_info_len_chars = resp_chunk->read_uint32();
                resp_chunk->read_string(original_uri_info, original_uri_info_len_chars, MixedCodeConstants::MAX_URI_STRING_LEN);

                char processed_uri_info[MixedCodeConstants::MAX_URI_STRING_LEN];
                uint32_t processed_uri_info_len_chars = resp_chunk->read_uint32();
                resp_chunk->read_string(processed_uri_info, processed_uri_info_len_chars, MixedCodeConstants::MAX_URI_STRING_LEN);

                // Reading number of parameters.
                uint8_t num_params = resp_chunk->read_uint8();

                // Reading parameter types.
                uint8_t param_types[MixedCodeConstants::MAX_URI_CALLBACK_PARAMS];
                resp_chunk->copy_data_to_buffer(param_types, MixedCodeConstants::MAX_URI_CALLBACK_PARAMS);

                // Reading protocol type.
                MixedCodeConstants::NetworkProtocolType proto_type = (MixedCodeConstants::NetworkProtocolType)resp_chunk->read_uint8();

#ifdef GW_TESTING_MODE

                HttpTestInformation* http_test_info = g_gateway.GetHttpTestInformation();

                // Comparing URI with current test URI.
                if (!http_test_info || strcmp(original_uri_info, http_test_info->method_and_uri_info))
                    break;
#endif

                GW_PRINT_WORKER << "New URI handler \"" << processed_uri_info << "\" on port " << port << " registration with handler id: " << handler_info << GW_ENDL;

                // Registering handler on active database.
                HandlersTable* handlers_table = g_gateway.GetDatabase(db_index_)->get_user_handlers();

                // Registering determined URI Apps handler.
                err_code = g_gateway.AddUriHandler(
                    gw,
                    handlers_table,
                    port,
                    original_uri_info,
                    original_uri_info_len_chars,
                    processed_uri_info,
                    processed_uri_info_len_chars,
                    param_types,
                    num_params,
                    handler_info,
                    db_index_,
                    AppsUriProcessData);

                if (err_code)
                {
                    wchar_t temp_str[MixedCodeConstants::MAX_URI_STRING_LEN];
                    swprintf_s(temp_str, MixedCodeConstants::MAX_URI_STRING_LEN, L"Can't register URI handler '%S' on port %d", original_uri_info, port);

                    // Pushing error message to initial database.
                    PushErrorMessage(sched_id, err_code, temp_str);

                    // Ignoring error code if its existing handler.
                    if (SCERRHANDLERALREADYREGISTERED == err_code)
                        err_code = 0;
                }

                break;
            }

            case bmx::BMX_UNREGISTER:
            {
                // Reading handler id.
                BMX_HANDLER_TYPE handler_info = resp_chunk->read_handler_info();

                GW_PRINT_WORKER << "User handler unregistration for handler id: " << handler_info << GW_ENDL;

                // Getting handlers list.
                HandlersTable* handlers_table = g_gateway.GetDatabase(db_index_)->get_user_handlers();
                HandlersList* handlers_list = handlers_table->FindHandler(handler_info);
                GW_ASSERT(handlers_list != NULL);

                // Removing handler with certain id.
                err_code = handlers_table->UnregisterHandler(handler_info);

                // Removing from global data structures.
                ServerPort* server_port = g_gateway.FindServerPort(handlers_list->get_port());
                GW_ASSERT(server_port != NULL);

                // Removing this handler from all possible places.
                server_port->get_registered_uris()->RemoveEntry(handlers_list);
                server_port->get_registered_subports()->RemoveEntry(handlers_list);
                server_port->get_port_handlers()->RemoveEntry(handlers_list);

                // Checking if server port became empty.
                if (server_port->IsEmpty())
                    server_port->Erase();

                return 0;
            }

            case bmx::BMX_SESSION_DESTROY:
            {
                GW_ASSERT(false);
                break;
            }

            default:
            {
                return SCERRGWWRONGBMXCHUNKTYPE;
            }
        }

        // Checking for error code after registrations.
        if (err_code)
        {
            switch (err_code)
            {
                case SCERRGWFAILEDTOBINDPORT:
                    // Ignore.
                    break;

                default:
                    return err_code;
            }
        }

        offset = resp_chunk->get_offset();
    }

    return 0;
}

} // namespace network
} // namespace starcounter