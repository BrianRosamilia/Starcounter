#pragma once
#ifndef WORKER_DB_INTERFACE_HPP
#define WORKER_DB_INTERFACE_HPP

namespace starcounter {
namespace network {
    
class GatewayWorker;
class WorkerDbInterface
{
    // Each worker has a copy of the gateway's shared_interface. The pointers to
    // various objects in shared memory are copied, but each worker will
    // initialize its own client_number.
    core::shared_interface shared_int_;

    // An array of indexes to channels, unordered.
    core::channel_number* channels_;

    // Private chunk pool.
    core::chunk_pool<core::chunk_index> private_chunk_pool_;

    // Overflow chunk pools.
    core::chunk_pool<channel_chunk> private_overflow_pool_;

    // Database index.
    int32_t db_index_;

public:

    // Index into databases array.
    uint16_t db_slot_index()
    {
        return db_index_;
    }

    // Initializes shared memory interface.
    uint32_t Init(
        const int32_t new_slot_index,
        const core::shared_interface& workerSharedInt,
        GatewayWorker *gw);

    // Allocates different channels and pools.
    WorkerDbInterface()
    {
        db_index_ = -1;

        // Allocating channels.
        channels_ = new core::channel_number[g_gateway.active_sched_num_read_only()];

        // Setting private/overflow chunk pool capacity.
        private_chunk_pool_.set_capacity(core::chunks_total_number_max);
        private_overflow_pool_.set_capacity(core::chunks_total_number_max);
    }

    // Deallocates active database.
    ~WorkerDbInterface()
    {
        // Freeing all occupied channels.
        for (std::size_t i = 0; i < g_gateway.active_sched_num_read_only(); i++)
        {
            core::channel_type& the_channel = shared_int_.channel(channels_[i]);
            the_channel.set_to_be_released();
        }

        // Deleting channels.
        delete[] channels_;
        channels_ = NULL;
    }

    // Checks if there is anything in overflow buffer and pushes all chunks from there.
    uint32_t PushOverflowChunksIfAny()
    {
        // Checking if anything is in overflow pool.
        if (private_overflow_pool_.empty())
            return 0;

        uint32_t chunk_index_and_sched; // This type must be uint32_t.
        std::size_t current_overflow_size = private_overflow_pool_.size();

        // Try to empty the overflow buffer, but only those elements
        // that are currently in the buffer. Those that fail to be
        // pushed are put back in the buffer and those are attempted to
        // be pushed the next time around.
        for (std::size_t i = 0; i < current_overflow_size; ++i)
        {
            private_overflow_pool_.pop_back(&chunk_index_and_sched);
            core::chunk_index chunk_index = chunk_index_and_sched & 0xFFFFFFUL;
            uint32_t sched_num = (chunk_index_and_sched >> 24) & 0xFFUL;

            // Pushing chunk using standard procedure.
            uint32_t errCode = PushChunkToDb(chunk_index, sched_num, false);
            GW_ERR_CHECK(errCode);
        }

        return 0;
    }

    // Push whatever chunks we have to channels.
    uint32_t PushChunkToDb(core::chunk_index chunk_index, int32_t sched_num, bool not_overflow_chunk);
    uint32_t PushSocketDataToDb(GatewayWorker* gw, SocketDataChunk *sd, BMX_HANDLER_TYPE handler_id);

    // Releases chunks from private chunk pool to the shared chunk pool.
    int32_t ReleaseToSharedChunkPool(int32_t num_chunks);

    // Scans all channels for any incoming chunks.
    uint32_t ScanChannels(GatewayWorker *gw);

    // Obtains chunk from a private pool if its not empty
    // (otherwise fetches from shared chunk pool).
    core::chunk_index GetChunkFromPrivatePool(shared_memory_chunk **chunk_data)
    {
        // Pop chunk index from private chunk pool.
        core::chunk_index chunk_index;

        // Trying to fetch chunk from private pool.
        while (private_chunk_pool_.acquire_linked_chunks(&shared_int_.chunk(0), chunk_index, 1) == false)
        {
            // Getting chunks from shared chunk pool.
            if (AcquireChunksFromSharedPool(ACCEPT_ROOF_STEP_SIZE) <= 0)
                return INVALID_CHUNK_INDEX;
        }

        // Chunk has been acquired.
        g_gateway.GetDatabase(db_index_)->ChangeNumUsedChunks(1);

        // Getting data pointer.
        (*chunk_data) = (shared_memory_chunk *)(&shared_int_.chunk(chunk_index));

#ifdef GW_CHUNKS_DIAG
        GW_COUT << "Getting new chunk: " << chunk_index << std::endl;
#endif

        return chunk_index;
    }

    // Acquires needed amount of chunks from shared pool.
    int32_t AcquireChunksFromSharedPool(int32_t num_chunks)
    {
        core::chunk_index current_chunk_index;

        // Acquire chunks from the shared chunk pool to this worker private chunk pool.
        int32_t num_acquired_chunks = shared_int_.acquire_from_shared_to_private(
            private_chunk_pool_, num_chunks, &shared_int_.client_interface(), 1000);

        // Checking that number of acquired chunks is correct.
        if (num_acquired_chunks != num_chunks)
        {
            // Some problem acquiring enough chunks.
#ifdef GW_ERRORS_DIAG
            GW_COUT << "Problem acquiring chunks from shared chunk pool." << std::endl;
#endif
        }

        // Return number of acquired chunks.
        return num_acquired_chunks;
    }

    // Returns given socket data chunk to private chunk pool.
    uint32_t ReturnChunkToPool(GatewayWorker *gw, SocketDataChunk *sd);

    // Returns given chunk to private chunk pool.
    uint32_t ReturnChunkToPool(core::chunk_index& chunk_index);

    // Registers push channel.
    uint32_t RegisterPushChannel();

    // Requesting previously registered handlers.
    uint32_t RequestRegisteredHandlers();

    // Handles management chunks.
    uint32_t HandleManagementChunks(GatewayWorker *gw, shared_memory_chunk* smc);
};

} // namespace network
} // namespace starcounter

#endif // WORKER_DB_INTERFACE_HPP