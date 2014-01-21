#pragma once
#ifndef WORKER_HPP
#define WORKER_HPP

namespace starcounter {
namespace network {

class WorkerChunks
{
    // Chunk stores.
    LinearQueue<SocketDataChunk*, MAX_GATEWAY_CHUNKS> worker_chunks_[NumGatewayChunkSizes];

    // Number of allocated chunks for each chunks store.
    int32_t num_allocated_chunks_[NumGatewayChunkSizes];

public:

    WorkerChunks()
    {
        memset(num_allocated_chunks_, 0, sizeof(num_allocated_chunks_));
    }

    void PrintInfo(std::stringstream& stats_stream)
    {
        stats_stream << "Allocated chunks: ";

        for (int32_t i = 0; i < NumGatewayChunkSizes; i++)
            stats_stream << num_allocated_chunks_[i] << ", ";

        stats_stream << "<br>";
    }

    int32_t GetNumberAllocatedChunks(chunk_store_type store_index)
    {
        return num_allocated_chunks_[store_index];
    }

    // Releasing existing chunk to the list.
    void ReleaseChunk(SocketDataChunkRef sd)
    {
        chunk_store_type store_index = sd->get_chunk_store_index();

        // Checking if we should completely dispose the chunk.
        if (worker_chunks_[store_index].get_num_entries() >= GatewayChunkStoresSizes[store_index])
        {
            _aligned_free(sd);
            num_allocated_chunks_[store_index]--;
        }
        else
        {
            worker_chunks_[store_index].PushBack(sd);
        }

        sd->InvalidateWhenReturning();
        sd = NULL;
    }

    // Getting free chunk from the list or creating a new one.
    SocketDataChunk* ObtainChunk(int32_t data_size = GatewayChunkDataSizes[DefaultGatewayChunkSizeType])
    {
        chunk_store_type chunk_store_index = ObtainGatewayChunkType(data_size);

        return ObtainChunkByStoreIndex(chunk_store_index);
    }

    // Getting free chunk from the list or creating a new one.
    SocketDataChunk* ObtainChunkByStoreIndex(chunk_store_type chunk_store_index)
    {
        SocketDataChunk* sd;

        // Checking if a free chunk is available.
        if (worker_chunks_[chunk_store_index].get_num_entries())
        {
            sd = worker_chunks_[chunk_store_index].PopBack();
            goto RETURN_SD;
        }

        // Creating new chunk.
        sd = (SocketDataChunk*) _aligned_malloc(GatewayChunkSizes[chunk_store_index], MEMORY_ALLOCATION_ALIGNMENT);
        num_allocated_chunks_[chunk_store_index]++;

RETURN_SD:

        sd->set_chunk_store_index(chunk_store_index);
        return sd;
    }
};

class Profiler;
class WorkerDbInterface;
class GatewayWorker
{
    // Performance profiler.
    Profiler profiler_;

    // Worker ID.
    worker_id_type worker_id_;

    // Worker IOCP handle.
    HANDLE worker_iocp_;

    // Worker statistics.
    int64_t worker_stats_bytes_received_,
        worker_stats_bytes_sent_,
        worker_stats_sent_num_,
        worker_stats_recv_num_,
        worker_stats_num_bound_sockets_;

#ifdef GW_COLLECT_SOCKET_STATISTICS
    int64_t port_num_active_conns_[MAX_PORTS_NUM];
#endif

    // All actively connected databases.
    WorkerDbInterface* worker_dbs_[MAX_ACTIVE_DATABASES];

    // Worker suspend state.
    volatile bool worker_suspended_unsafe_;

    // Some worker temporary data.
    char uri_lower_case_[MixedCodeConstants::MAX_URI_STRING_LEN];

    // Random generator.
    random_generator* rand_gen_;

    // Clone made during last iteration.
    SocketDataChunk* sd_receive_clone_;

    // List of reusable connect sockets.
    LinearQueue<SOCKET, MAX_REUSABLE_CONNECT_SOCKETS_PER_WORKER> reusable_connect_sockets_;

    // List of reusable accept sockets.
    LinearQueue<SOCKET, MAX_REUSABLE_CONNECT_SOCKETS_PER_WORKER> reusable_accept_sockets_;

    // Number of created connections calculated for worker.
    int32_t num_created_conns_worker_;

    // List of sockets indexes to be disconnected.
    std::list<session_index_type> sockets_indexes_to_disconnect_;

    // Aggregation sockets waiting for send.
    LinearList<SocketDataChunk*, 256> aggr_sds_to_send_;

    // Aggregation timer.
    PreciseTimer aggr_timer_;

    // Worker chunks.
    WorkerChunks worker_chunks_;

#ifdef GW_LOOPED_TEST_MODE
    LinearQueue<SocketDataChunk*, MAX_TEST_ECHOES> emulated_measured_network_events_queue_;
    LinearQueue<SocketDataChunk*, MAX_TEST_ECHOES> emulated_preparation_network_events_queue_;
#endif

    // Avoiding false sharing.
    uint8_t pad[CACHE_LINE_SIZE];

public:

    // Worker chunks.
    WorkerChunks* GetWorkerChunks()
    {
        return &worker_chunks_;
    }

    // Performs a send of given socket data on aggregation socket.
    uint32_t SendOnAggregationSocket(SocketDataChunkRef sd);

    // Tries to find current aggregation socket data from aggregation socket index.
    SocketDataChunk* FindAggregationSdBySocketIndex(session_index_type aggr_socket_info_index);

    // Returns given socket data chunk to private chunk pool.
    void ReturnSocketDataChunksToPool(SocketDataChunkRef sd);

    // Adds socket data chunk to aggregation queue.
    void AddToAggregation(SocketDataChunkRef sd);

    // Processes all aggregated chunks.
    uint32_t SendAggregatedChunks();

    // Pushes socket for further reuse.
    void PushToReusableAcceptSockets(SOCKET sock)
    {
        reusable_accept_sockets_.PushBack(sock);
    }

    // Adds socket to be disconnected.
    void AddSocketToDisconnectListUnsafe(session_index_type socket_index)
    {
        sockets_indexes_to_disconnect_.push_back(socket_index);
    }

    // Starting accumulation.
    uint32_t StartAccumulation(SocketDataChunkRef sd, uint32_t total_desired_bytes, uint32_t num_already_accumulated)
    {
        // Enabling accumulative state.
        sd->set_accumulating_flag();

        // Checking if the host accumulation should be involved.
        if (total_desired_bytes > static_cast<uint32_t>(GatewayChunkDataSizes[NumGatewayChunkSizes - 1]))
        {
            // We need to accumulate on host.
            sd->set_on_host_accumulation_flag();

            return SCERRGWMAXDATASIZEREACHED;
        }

        // Checking if data that needs accumulation fits into chunk.
        if (sd->get_accum_buf()->get_chunk_orig_buf_len_bytes() < total_desired_bytes)
        {
            uint32_t err_code = SocketDataChunk::ChangeToBigger(this, sd, total_desired_bytes);
            if (err_code)
                return err_code;
        }

        GW_ASSERT(sd->get_accum_buf()->get_chunk_orig_buf_len_bytes() >= total_desired_bytes);

        sd->get_accum_buf()->StartAccumulation(total_desired_bytes, num_already_accumulated);

        return 0;
    }

    // Processes sockets that should be disconnected.
    void ProcessSocketDisconnectList();

#ifdef GW_LOOPED_TEST_MODE

    int64_t GetNumberOfPreparationNetworkEvents()
    {
        return emulated_preparation_network_events_queue_.get_num_entries();
    }

    // Pushing given sd to network emulation queue.
    void PushToMeasuredNetworkEmulationQueue(SocketDataChunk* sd)
    {
        emulated_measured_network_events_queue_.PushBack(sd);
    }

    // Pushing given sd to network emulation queue.
    void PushToPreparationNetworkEmulationQueue(SocketDataChunk* sd)
    {
        emulated_preparation_network_events_queue_.PushBack(sd);
    }

    // Processes looped queue.
    bool ProcessEmulatedNetworkOperations(OVERLAPPED_ENTRY *removedOvls, uint32_t* removedOvlsNum, int32_t max_fetched);

#endif

    // Getting number of reusable connect sockets.
    int32_t NumberOfReusableConnectSockets()
    {
        return reusable_connect_sockets_.get_num_entries();
    }

    // Getting number of reusable accept sockets.
    int32_t NumberOfReusableAcceptSockets()
    {
        return reusable_accept_sockets_.get_num_entries();
    }

    // Getting number of chunks in overflow queue.
    int64_t NumberOverflowChunksPerDatabasePerWorker(db_index_type db_index)
    {
        if (worker_dbs_[db_index] != NULL)
        {
            return worker_dbs_[db_index]->GetNumberOverflowedChunks();
        }

        return 0;
    }

    // Clone made during last iteration.
    SocketDataChunkRef get_sd_receive_clone()
    {
        return sd_receive_clone_;
    }

    // Checks if cloning was performed and does operations.
    uint32_t ProcessReceiveClones(bool just_delete_clone);

    // Sets the clone for the next iteration.
    void SetReceiveClone(SocketDataChunkRef sd_clone)
    {
        // Only one clone at a time is possible.
        GW_ASSERT(sd_receive_clone_ == NULL);

        sd_receive_clone_ = sd_clone;
    }

    // Creating accepting sockets on all ports and for all databases.
    uint32_t CheckAcceptingSocketsOnAllActivePorts();

    // Changes number of accepting sockets.
    int64_t ChangeNumAcceptingSockets(int32_t port_index, int64_t change_value)
    {
#ifdef GW_DETAILED_STATISTICS
        GW_COUT << "ChangeNumAcceptingSockets: " << change_value << GW_ENDL;
#endif

        return g_gateway.get_server_port(port_index)->ChangeNumAcceptingSockets(change_value);
    }

#ifdef GW_COLLECT_SOCKET_STATISTICS

    // Changes number of active connections.
    void ChangeNumActiveConnections(int32_t port_index, int64_t change_value)
    {
#ifdef GW_DETAILED_STATISTICS
        GW_COUT << "ChangeNumActiveConnections: " << change_value << GW_ENDL;
#endif

        port_num_active_conns_[port_index] += change_value;

        // Changing number of active sockets.
        g_gateway.ChangeNumActiveSockets(change_value);
    }

    // Set number of active connections.
    void SetNumActiveConnections(int32_t port_index, int64_t set_value)
    {
        port_num_active_conns_[port_index] += set_value;
    }

    // Getting number of active connections per port.
    int64_t NumberOfActiveConnectionsPerPortPerWorker(int32_t port_index)
    {
        return port_num_active_conns_[port_index];
    }

#endif

    // Generates a new scheduler id.
    scheduler_id_type GenerateSchedulerId()
    {
        return GetWorkerDb(0)->GenerateSchedulerId();
    }

    // Getting random generator.
    random_generator* get_random()
    {
        return rand_gen_;
    }

    // Getting worker temporary URI.
    char* get_uri_lower_case()
    {
        return uri_lower_case_;
    }

    // Adds new active database.
    uint32_t AddNewDatabase(db_index_type db_index);

    // Sets worker suspend state.
    void set_worker_suspended(bool value)
    {
        worker_suspended_unsafe_ = value;
    }

    // Gets worker suspend state.
    bool worker_suspended()
    {
        return worker_suspended_unsafe_;
    }

    // Gets global lock.
    void EnterGlobalLock()
    {
        worker_suspended_unsafe_ = true;

        // Entering global lock.
        g_gateway.EnterGlobalLock();
    }

    // Releases global lock.
    void LeaveGlobalLock()
    {
        worker_suspended_unsafe_ = false;

        // Leaving global lock.
        g_gateway.LeaveGlobalLock();
    }

    // Getting one of the active databases.
    WorkerDbInterface* GetWorkerDb(db_index_type db_index)
    {
        // Checking for correct database.
        GW_ASSERT_DEBUG(db_index < g_gateway.get_num_dbs_slots());

        return worker_dbs_[db_index];
    }

    // Deleting inactive database.
    void DeleteInactiveDatabase(db_index_type db_index);

    // Sends given predefined response.
    uint32_t SendPredefinedMessage(
        SocketDataChunkRef sd,
        const char* message,
        const int32_t message_len);

    // Sends given body.
    uint32_t SendHttpBody(
        SocketDataChunkRef sd,
        const char* body,
        const int32_t body_len);

    // Getting the bytes received statistics.
    int64_t get_worker_stats_bytes_received()
    {
        return worker_stats_bytes_received_;
    }

    int64_t get_worker_stats_num_bound_sockets()
    {
        return worker_stats_num_bound_sockets_;
    }

    // Getting the bytes sent statistics.
    int64_t get_worker_stats_bytes_sent()
    {
        return worker_stats_bytes_sent_;
    }

    // Getting the number of sends statistics.
    int64_t get_worker_stats_sent_num()
    {
        return worker_stats_sent_num_;
    }

    // Getting the number of receives statistics.
    int64_t get_worker_stats_recv_num()
    {
        return worker_stats_recv_num_;
    }

    // Printing the worker information.
    void PrintInfo(std::stringstream& stats_stream)
    {
        stats_stream << "Bytes received: " << worker_stats_bytes_received_ << "<br>";
        stats_stream << "Packets received: " << worker_stats_recv_num_ << "<br>";
        stats_stream << "Bytes sent: " << worker_stats_bytes_sent_ << "<br>";
        stats_stream << "Packets sent: " << worker_stats_sent_num_ << "<br>";
        stats_stream << "Bound sockets: " << worker_stats_num_bound_sockets_ << "<br>";
        worker_chunks_.PrintInfo(stats_stream);
    }

    // Worker initialization function.
    int32_t Init(int32_t workerId);

    // Default constructor.
    GatewayWorker()    
    {
    }

    // Gets a worker profiler reference.
    Profiler& get_profiler()
    {
        return profiler_;
    }

    // Destructor.
    ~GatewayWorker()
    {
        // Deleting only necessary stuff.
        for (int32_t i = 0; i < MAX_ACTIVE_DATABASES; i++)
        {
            if (worker_dbs_[i])
            {
                delete worker_dbs_[i];
                worker_dbs_[i] = NULL;
            }
        }
    }

    // Main worker function.
    uint32_t WorkerRoutine();

    // Getting worker ID.
    worker_id_type get_worker_id() { return worker_id_; }

    // Gets worker IOCP.
    HANDLE get_worker_iocp() { return worker_iocp_; }

    // Used to create new connections when reaching the limit.
    uint32_t CreateNewConnections(int32_t how_many, int32_t port_index);

#ifdef GW_PROXY_MODE
    // Allocates a bunch of new connections.
    uint32_t CreateProxySocket(SocketDataChunkRef proxy_sd);
#endif

    // Functions to process finished IOCP events.
    uint32_t FinishReceive(SocketDataChunkRef sd, int32_t numBytesReceived, bool& called_from_receive);
    uint32_t FinishSend(SocketDataChunkRef sd, int32_t numBytesSent);
    uint32_t FinishDisconnect(SocketDataChunkRef sd);
    uint32_t FinishConnect(SocketDataChunkRef sd);
    uint32_t FinishAccept(SocketDataChunkRef sd);

    // Running connect on socket data.
    uint32_t Connect(SocketDataChunkRef sd, sockaddr_in *serverAddr);

    // Running disconnect on socket data.
    void DisconnectAndReleaseChunk(SocketDataChunkRef sd);

    // Disconnects arbitrary socket.
    uint32_t DisconnectSocket(session_index_type socket_index);

    // Initiates receive on arbitrary socket.
    uint32_t ReceiveOnSocket(session_index_type socket_index);

    // Running send on socket data.
    uint32_t Send(SocketDataChunkRef sd);

    // Running receive on socket data.
    uint32_t Receive(SocketDataChunkRef sd);

    // Running accept on socket data.
    uint32_t Accept(SocketDataChunkRef sd);

    // Processes socket data to database.
    uint32_t RunReceiveHandlers(SocketDataChunkRef sd)
    {
        // Putting socket data to database.
        sd->PrepareToDb();

        bool is_handled = false;

        // Here we have to process socket data using handlers.
        uint32_t err_code = RunHandlers(this, sd, &is_handled);
        if (err_code)
        {
            // Ban the fucking IP.

            return err_code;
        }

        return 0;
    }

    // Processes socket data from database.
    uint32_t RunFromDbHandlers(SocketDataChunkRef sd)
    {
        // Putting socket data from database.
        sd->PrepareFromDb();

        bool is_handled = false;

        // Here we have to process socket data using handlers.
        uint32_t err_code = RunHandlers(this, sd, &is_handled);
        if (err_code)
        {
            // Ban the fucking IP.

            return err_code;
        }

        return 0;
    }

    // Does general data processing using port handlers.
    uint32_t RunHandlers(GatewayWorker *gw, SocketDataChunkRef sd, bool* is_handled)
    {
        return g_gateway.get_server_port(sd->GetPortIndex())->get_port_handlers()->RunHandlers(gw, sd, is_handled);
    }

    // Push given chunk to database queue.
    uint32_t PushSocketDataToDb(SocketDataChunkRef sd, BMX_HANDLER_TYPE handler_id);

    // Scans all channels for any incoming chunks.
    uint32_t ScanChannels(uint32_t& next_sleep_interval_ms);

    // Creates the socket data structure.
    uint32_t CreateSocketData(
        session_index_type socket_info_index,
        SocketDataChunkRef out_sd);

#ifdef GW_TESTING_MODE

    int32_t get_num_created_conns_worker()
    {
        return num_created_conns_worker_;
    }

    // Sends HTTP echo to master.
    uint32_t SendHttpEcho(SocketDataChunkRef sd, echo_id_type echo_id);

    // Checks if measured test should be started and begins it.
    void BeginMeasuredTestIfReady();

    // Sends raw echo to master.
    uint32_t SendRawEcho(SocketDataChunkRef sd, echo_id_type echo_id);

#endif
};

} // namespace network
} // namespace starcounter

#endif // WORKER_HPP
