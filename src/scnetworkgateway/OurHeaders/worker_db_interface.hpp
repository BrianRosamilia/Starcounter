#pragma once
#ifndef WORKER_DB_INTERFACE_HPP
#define WORKER_DB_INTERFACE_HPP

namespace starcounter {
namespace network {

class GatewayWorker;
class WorkerDbInterface
{
    enum {
        // The number of times to scan through all channels trying to push or
        // pop before waiting, in case did not push or pop for spin_count_reset
        // number of times. Need to experiment with this value.
        // NOTE: scan_counter_preset must be > 0.
        scan_counter_preset = 1 << 20,

        // The thread can give up waiting after wait_for_work_milli_seconds, but
        // if not, set it to INFINITE.
        wait_for_work_milli_seconds = 1000
    };

    // Each worker has a copy of the gateway's shared_interface. The pointers to
    // various objects in shared memory are copied, but each worker will
    // initialize its own client_number.
    core::shared_interface shared_int_;

    // An array of indexes to channels, unordered.
    core::channel_number* channels_;

    // Private chunk pool.
    core::chunk_pool<core::chunk_index> private_chunk_pool_;

    // Database index.
    db_index_type db_index_;

    // Worker id to which this interface belongs.
    worker_id_type worker_id_;

    // Number of active schedulers.
    int32_t num_schedulers_;

    // Current scheduler id.
    int32_t cur_scheduler_id_;

    // Acquires needed amount of chunks from shared pool.
    uint32_t AcquireIPCChunksFromSharedPool(int32_t num_ipc_chunks)
    {
        // Acquire chunks from the shared chunk pool to this worker private chunk pool.
        int32_t num_acquired_chunks = static_cast<int32_t> (shared_int_.acquire_from_shared_to_private(
            private_chunk_pool_, num_ipc_chunks, &shared_int_.client_interface(), 1000));

        //GW_ASSERT(num_acquired_chunks == num_ipc_chunks);

        // Checking that number of acquired chunks is correct.
        if (num_acquired_chunks != num_ipc_chunks)
        {
            // Some problem acquiring enough chunks.
#ifdef GW_ERRORS_DIAG
            GW_COUT << "Problem acquiring chunks from shared chunk pool." << GW_ENDL;
#endif
            //Sleep(1);
            return SCERRACQUIRELINKEDCHUNKS;
        }

        return 0;
    }

public:

    // Round-robin scheduler number.
    uint32_t GenerateSchedulerId()
    {
        cur_scheduler_id_++;
        if (cur_scheduler_id_ >= num_schedulers_)
            cur_scheduler_id_ = 0;

        return cur_scheduler_id_;
    }

    // Writes given big linear buffer into obtained linked chunks.
    uint32_t WorkerDbInterface::WriteBigDataToIPCChunks(
        uint8_t* buf,
        int32_t buf_len_bytes,
        starcounter::core::chunk_index cur_chunk_index,
        int32_t first_chunk_offset,
        int32_t* actual_written_bytes,
        uint16_t* num_ipc_chunks
        );

    // Getting the number of overflowed chunks.
    int64_t GetNumberOverflowedChunks()
    {
        int64_t num_overflow_chunks = 0;

        for (int32_t s = 0; s < num_schedulers_; s++)
        {
            // Obtaining the channel.
            core::channel_type& the_channel = shared_int_.channel(channels_[s]);

            // Getting number of overflowed chunks on this channel.
            num_overflow_chunks += the_channel.in_overflow().count();
        }

        return num_overflow_chunks;
    }

    // Sends session destroyed message.
    uint32_t PushSessionDestroy(
        session_index_type linear_index,
        random_salt_type random_salt,
        uint8_t scheduler_id);

    // Sends error message.
    uint32_t PushErrorMessage(
        scheduler_id_type sched_id,
        uint32_t err_code_num,
        const wchar_t* const err_msg);

    // Getting shared interface.
    core::shared_interface* get_shared_int()
    {
        return &shared_int_;
    }

    // Declares gateway ready for database pushes.
    uint32_t SetGatewayReadyForDbPushes();

    // Resets the existing interface.
    void Reset()
    {
        db_index_ = INVALID_DB_INDEX;
        worker_id_ = INVALID_WORKER_INDEX;

        num_schedulers_ = 0;
        cur_scheduler_id_ = 0;

        if (channels_)
        {
            delete[] channels_;
            channels_ = NULL;
        }
    }

    // Allocates different channels and pools.
    WorkerDbInterface(
        const int32_t new_slot_index,
        const int32_t worker_id);

    // Deallocates active database.
    ~WorkerDbInterface()
    {
#if 0
        // Freeing all occupied channels.
        for (std::size_t s = 0; s < num_schedulers_; s++)
        {
            core::channel_type& the_channel = shared_int_.channel(channels_[s]);

            // Asserting that there are none overflowed chunks.
            GW_ASSERT (true == the_channel.in_overflow().empty());

            the_channel.set_to_be_released();
        }
#endif

        // Deleting channels.
        delete[] channels_;
        channels_ = NULL;
    }

    // Tries pushing to channel and returns try if it did.
    bool TryPushToChannel(
        core::channel_type& the_channel,
        core::chunk_index the_chunk_index)
    {
        // Trying to push chunk if overflow is empty.
        if (the_channel.in.try_push_front(the_chunk_index))
        {
            // Successfully pushed the response message to the channel.

            // A message on channel ch was received. Notify the database
            // that the out queue in this channel is not full.
            the_channel.scheduler()->notify(shared_int_.scheduler_work_event
                (the_channel.get_scheduler_number()));

#ifdef GW_CHUNKS_DIAG
            GW_PRINT_WORKER_DB << "   successfully pushed: chunk " << the_chunk_index << GW_ENDL;
#endif

            return true;
        }

        return false;
    }

    // Tries to push existing overflow chunks on given scheduler.
    void PushOverflowedChunksOnScheduler(int32_t sched_id)
    {
        // Obtaining the channel.
        core::channel_type& the_channel = shared_int_.channel(channels_[sched_id]);
        core::channel_type::queue& overflow_queue = the_channel.in_overflow();

        // Checking if overflow pool is not empty.
        while (overflow_queue.not_empty())
        {
            // Popping back chunk.
            core::chunk_index the_chunk_index = overflow_queue.front();

            // Just getting number of chunks to push.
            SocketDataChunk* sd = (SocketDataChunk*)((uint8_t*)(&shared_int_.chunk(the_chunk_index)) + MixedCodeConstants::CHUNK_OFFSET_SOCKET_DATA);

            // Popping front chunk.
            overflow_queue.pop_front();

            // NOTE: If success - chunk is gone, we can't do any operations related to it!
            // That's why we do pop_front and then push_front.
            if (!TryPushToChannel(the_channel, the_chunk_index))
            {
                // Pushing chunk back to front since it wasn't pushed on channel.
                overflow_queue.push_front(the_chunk_index);
            }
        }
    }

    // Checks if there is anything in overflow buffer and pushes all chunks from there.
    void PushOverflowChunks()
    {
        for (int32_t s = 0; s < num_schedulers_; s++)
            PushOverflowedChunksOnScheduler(s);
    }

    // Push whatever chunks we have to channels.
    void PushLinkedChunksToDb(
        core::chunk_index chunk_index,
        int16_t scheduler_id);

    uint32_t PushSocketDataToDb(GatewayWorker* gw, SocketDataChunkRef sd, BMX_HANDLER_TYPE handler_id);

    // Releases chunks from private chunk pool to the shared chunk pool.
    uint32_t ReleaseToSharedChunkPool(int32_t num_ipc_chunks);

    // Scans all channels for any incoming chunks.
    uint32_t ScanChannels(GatewayWorker *gw, uint32_t& next_sleep_interval_ms);

    // Getting shared memory chunk.
    shared_memory_chunk* GetSharedMemoryChunkFromIndex(core::chunk_index the_chunk_index)
    {
        return (shared_memory_chunk *)(&shared_int_.chunk(the_chunk_index));
    }

    // Obtains chunk from a private pool if its not empty
    // (otherwise fetches from shared chunk pool).
    uint32_t GetOneChunkFromPrivatePool(core::chunk_index* chunk_index, shared_memory_chunk** smc);

    // Obtains chunks from a private pool if its not empty
    // (otherwise fetches from shared chunk pool).
    uint32_t GetMultipleChunksFromPrivatePool(
        core::chunk_index* new_chunk_index,
        uint32_t num_ipc_chunks)
    {
        // Trying to fetch chunk from private pool.
        uint32_t err_code;
        while (!private_chunk_pool_.acquire_linked_chunks_counted(&shared_int_.chunk(0), *new_chunk_index, num_ipc_chunks))
        {
            // Getting chunks from shared chunk pool.
            err_code = AcquireIPCChunksFromSharedPool(MAX_CHUNKS_IN_PRIVATE_POOL);
            if (err_code)
                return err_code;
        }

#ifdef GW_CHUNKS_DIAG
        GW_COUT << "Acquired new " << num_ipc_chunks << " linked chunks: " << *new_chunk_index << GW_ENDL;
#endif

        return 0;
    }

    // Returns given linked chunks to private chunk pool (and if needed then to shared).
    void ReturnLinkedChunksToPool(core::chunk_index& first_linked_chunk);

    // Returns all chunks from private pool to shared.
    void ReturnAllPrivateChunksToSharedPool();

    // Handles management chunks.
    uint32_t HandleManagementChunks(
        scheduler_id_type sched_id,
        GatewayWorker *gw,
        shared_memory_chunk* ipc_smc);
};

} // namespace network
} // namespace starcounter

#endif // WORKER_DB_INTERFACE_HPP
