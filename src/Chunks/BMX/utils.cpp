#include "bmx.hpp"

// Obtains new shared memory chunk.
EXTERN_C uint32_t __stdcall sc_bmx_obtain_new_chunk(
    starcounter::core::chunk_index* new_chunk_index,
    uint8_t** new_chunk_mem)
{
    // Obtaining chunk memory.
    return cm_acquire_shared_memory_chunk(new_chunk_index, new_chunk_mem);
}

// Writing linked chunks data to a given buffer and releasing all chunks except first.
EXTERN_C uint32_t __stdcall sc_bmx_plain_copy_and_release_chunks(
    starcounter::core::chunk_index first_chunk_index,
    uint8_t* first_chunk_data,
    uint8_t* buffer
    )
{
    _SC_BEGIN_FUNC

    uint32_t err_code;
    int32_t cur_offset = 0;

    uint8_t* chunk_mem;
    shared_memory_chunk* smc = (shared_memory_chunk*) first_chunk_data;
    starcounter::core::chunk_index cur_chunk_index = first_chunk_index;

    do
    {
        // Obtaining chunk memory.
        err_code = cm_get_shared_memory_chunk(cur_chunk_index, &chunk_mem);
        _SC_ASSERT(err_code == 0);

        smc = (shared_memory_chunk*) chunk_mem;

        // Copying the whole chunk data.
        memcpy(buffer + cur_offset, chunk_mem, starcounter::MixedCodeConstants::CHUNK_MAX_DATA_BYTES);
        cur_offset += starcounter::MixedCodeConstants::CHUNK_MAX_DATA_BYTES;

        // Getting next chunk.
        cur_chunk_index = smc->get_link();
    }
    while (cur_chunk_index != shared_memory_chunk::link_terminator);

    // Returning all linked chunks (except first one) to private/shared pool.
    shared_memory_chunk* first_smc = ((shared_memory_chunk*) first_chunk_data);
    err_code = cm_release_linked_shared_memory_chunks(first_smc->get_link());
    if (err_code)
        return err_code;

    // Terminating the first chunk.
    first_smc->terminate_link();

    return 0;

    _SC_END_FUNC
}

// Writing all chunks data to given buffer.
EXTERN_C uint32_t __stdcall sc_bmx_copy_all_chunks(
    starcounter::core::chunk_index chunk_index,
    uint8_t* first_smc,
    int32_t first_chunk_offset,
    int32_t total_copy_bytes,
    uint8_t* dest_buffer,
    int32_t dest_buffer_size
    )
{
    uint32_t err_code;
    int32_t dest_offset = 0;

    // Copying first chunk data.
    int32_t cur_chunk_data_size = starcounter::MixedCodeConstants::CHUNK_MAX_DATA_BYTES - first_chunk_offset;
    memcpy(dest_buffer, first_smc + first_chunk_offset, cur_chunk_data_size);
    dest_offset += cur_chunk_data_size;

    // Number of bytes left to copy.
    int32_t bytes_left = total_copy_bytes - cur_chunk_data_size;

    // Next chunk copy size.
    cur_chunk_data_size = bytes_left;
    if (cur_chunk_data_size > starcounter::MixedCodeConstants::CHUNK_MAX_DATA_BYTES)
        cur_chunk_data_size = starcounter::MixedCodeConstants::CHUNK_MAX_DATA_BYTES;

    uint8_t* chunk_mem;
    shared_memory_chunk* smc = (shared_memory_chunk*) first_smc;
    starcounter::core::chunk_index cur_chunk_index = smc->get_link();

    // Until we get the last chunk in chain.
    while (cur_chunk_index != shared_memory_chunk::link_terminator)
    {
        // Obtaining chunk memory.
        err_code = cm_get_shared_memory_chunk(cur_chunk_index, &chunk_mem);
        _SC_ASSERT(err_code == 0);

        smc = (shared_memory_chunk*) chunk_mem;

        // Copying.
        memcpy(dest_buffer + dest_offset, chunk_mem, cur_chunk_data_size);
        dest_offset += cur_chunk_data_size;

        // Decreasing number of bytes left to be processed.
        bytes_left -= cur_chunk_data_size;
        if (bytes_left < starcounter::MixedCodeConstants::CHUNK_MAX_DATA_BYTES)
            cur_chunk_data_size = bytes_left;

        // Getting next chunk in chain.
        cur_chunk_index = smc->get_link();
    }

    // Returning all additional chunks to private pool: 
    err_code = cm_release_linked_shared_memory_chunks(((shared_memory_chunk*) first_smc)->get_link());
    if (err_code)
        return err_code;

    return 0;
}

// Clones the existing chunk into a new one by copying the specified header.
uint32_t __stdcall sc_bmx_clone_chunk(
    starcounter::core::chunk_index src_chunk_index,
    int32_t offset,
    int32_t num_bytes_to_copy,
    starcounter::core::chunk_index* new_chunk_index
    )
{
    uint32_t err_code;

    // Acquiring new chunk.
    uint8_t* new_chunk_buf;
    err_code = cm_acquire_shared_memory_chunk(new_chunk_index, &new_chunk_buf);
    _SC_ASSERT(err_code == 0);
    if (err_code)
        return err_code;

    // Getting chunk memory address.
    uint8_t* src_chunk_buf;
    err_code = cm_get_shared_memory_chunk(src_chunk_index, &src_chunk_buf);
    _SC_ASSERT(err_code == 0);

    // Copying memory from original chunk to copy.
    memcpy(new_chunk_buf, src_chunk_buf + offset, num_bytes_to_copy);

    return 0;
}

// Writes given big linear buffer into obtained linked chunks.
uint32_t __stdcall sc_bmx_write_to_chunks(
    uint8_t* buf,
    int32_t buf_len_bytes,
    starcounter::core::chunk_index the_chunk_index,
    int32_t* actual_written_bytes,
    int32_t first_chunk_offset,
    bool just_sending_flag
    )
{
    // Maximum number of bytes that will be written in this call.
    int32_t num_bytes_left_first_chunk = starcounter::MixedCodeConstants::CHUNK_MAX_DATA_BYTES - first_chunk_offset;
    _SC_ASSERT(num_bytes_left_first_chunk > 0);
    _SC_ASSERT(num_bytes_left_first_chunk + first_chunk_offset == starcounter::MixedCodeConstants::CHUNK_MAX_DATA_BYTES);

    // Getting chunk memory address.
    uint8_t* cur_smc;
    uint32_t err_code = cm_get_shared_memory_chunk(the_chunk_index, &cur_smc);
    _SC_ASSERT(err_code == 0);

    // Checking if we should just send the chunks.
    if (just_sending_flag)
        (*(uint32_t*)(cur_smc + starcounter::MixedCodeConstants::CHUNK_OFFSET_SOCKET_FLAGS)) |= starcounter::MixedCodeConstants::SOCKET_DATA_FLAGS_JUST_SEND;

    // Checking if this chunk was aggregated.
    bool aggregated_flag =
        (0 != ((*(uint32_t*)(cur_smc + starcounter::MixedCodeConstants::CHUNK_OFFSET_SOCKET_FLAGS)) & starcounter::MixedCodeConstants::SOCKET_DATA_FLAGS_AGGREGATED));

    _SC_ASSERT(!aggregated_flag);

#ifdef GW_MEMORY_MANAGEMENT

    // Enforcing usage of one chunk.
    if (buf_len_bytes > num_bytes_left_first_chunk)
        buf_len_bytes = num_bytes_left_first_chunk;
    
#endif

    // Checking if data fits in one chunk.
    if (buf_len_bytes <= num_bytes_left_first_chunk)
    {
        // Writing to first chunk.
        _SC_ASSERT(first_chunk_offset < starcounter::MixedCodeConstants::CHUNK_MAX_DATA_BYTES);
        _SC_ASSERT(buf_len_bytes < starcounter::MixedCodeConstants::CHUNK_MAX_DATA_BYTES);
        memcpy(cur_smc + first_chunk_offset, buf, buf_len_bytes);

        // Setting the number of written bytes.
        *(uint32_t*)(cur_smc + starcounter::MixedCodeConstants::CHUNK_OFFSET_USER_DATA_WRITTEN_BYTES) = buf_len_bytes;

        // Setting total number of chunks.
        *(uint16_t*)(cur_smc + starcounter::MixedCodeConstants::CHUNK_OFFSET_NUM_CHUNKS) = 1;

        // Setting number of total written bytes.
        *actual_written_bytes = buf_len_bytes;

        return 0;
    }
    
    // Number of chunks to use.
    int32_t num_extra_chunks = ((buf_len_bytes - num_bytes_left_first_chunk) / starcounter::MixedCodeConstants::CHUNK_MAX_DATA_BYTES) + 1;
    _SC_ASSERT(num_extra_chunks > 0);

    int32_t num_bytes_to_write = buf_len_bytes;

    // Checking if more than maximum chunks we can take at once.
    if ((num_extra_chunks >= starcounter::bmx::MAX_EXTRA_LINKED_WSABUFS) && (!aggregated_flag))
    {
        num_extra_chunks = starcounter::bmx::MAX_EXTRA_LINKED_WSABUFS - 1;
        num_bytes_to_write = starcounter::bmx::MAX_BYTES_EXTRA_LINKED_WSABUFS + num_bytes_left_first_chunk;
    }

    // Setting total number of chunks.
    *(uint16_t*)(cur_smc + starcounter::MixedCodeConstants::CHUNK_OFFSET_NUM_CHUNKS) = 1 + num_extra_chunks;

    // Acquiring linked chunks.
    err_code = cm_acquire_linked_shared_memory_chunks_counted(the_chunk_index, num_extra_chunks);
    _SC_ASSERT(err_code == 0);

    if (err_code)
    {
        // Releasing the original chunk.
        uint32_t err_code2 = cm_release_linked_shared_memory_chunks(the_chunk_index);
        _SC_ASSERT(err_code2 == 0);

        return err_code;
    }

    // Setting the number of written bytes.
    *(uint32_t*)(cur_smc + starcounter::MixedCodeConstants::CHUNK_OFFSET_USER_DATA_WRITTEN_BYTES) = num_bytes_to_write;

    // Going through each linked chunk and write data there.
    int32_t left_bytes_to_write = num_bytes_to_write;
    int32_t num_bytes_to_write_in_chunk = starcounter::MixedCodeConstants::CHUNK_MAX_DATA_BYTES;
    
    // Writing to first chunk.
    _SC_ASSERT(first_chunk_offset < starcounter::MixedCodeConstants::CHUNK_MAX_DATA_BYTES);
    _SC_ASSERT(num_bytes_left_first_chunk < starcounter::MixedCodeConstants::CHUNK_MAX_DATA_BYTES);
    memcpy(cur_smc + first_chunk_offset, buf, num_bytes_left_first_chunk);
    left_bytes_to_write -= num_bytes_left_first_chunk;
    _SC_ASSERT(left_bytes_to_write >= 0);

    // Checking how many bytes to write next time.
    if (left_bytes_to_write < starcounter::MixedCodeConstants::CHUNK_MAX_DATA_BYTES)
    {
        // Checking if we copied everything.
        if (left_bytes_to_write == 0)
        {
            // Setting number of total written bytes.
            *actual_written_bytes = num_bytes_to_write;

            return 0;
        }

        num_bytes_to_write_in_chunk = left_bytes_to_write;
    }

    // Processing until have some bytes to write.
    while (true)
    {
        // Getting next chunk in chain.
        the_chunk_index = ((shared_memory_chunk*)cur_smc)->get_link();
        _SC_ASSERT(the_chunk_index != shared_memory_chunk::link_terminator);
        _SC_ASSERT(the_chunk_index < starcounter::MixedCodeConstants::SHM_CHUNKS_DEFAULT_NUMBER);

        // Getting chunk memory address.
        err_code = cm_get_shared_memory_chunk(the_chunk_index, &cur_smc);
        _SC_ASSERT(err_code == 0);

        // Copying memory.
        _SC_ASSERT(num_bytes_to_write >= left_bytes_to_write);
        _SC_ASSERT(left_bytes_to_write > 0);
        _SC_ASSERT(num_bytes_to_write_in_chunk > 0 && num_bytes_to_write_in_chunk <= starcounter::MixedCodeConstants::CHUNK_MAX_DATA_BYTES);
        _SC_ASSERT(num_bytes_to_write - left_bytes_to_write < buf_len_bytes);
        memcpy(cur_smc, buf + num_bytes_to_write - left_bytes_to_write, num_bytes_to_write_in_chunk);
        left_bytes_to_write -= num_bytes_to_write_in_chunk;
        _SC_ASSERT(left_bytes_to_write >= 0);

        // Checking how many bytes to write next time.
        if (left_bytes_to_write < starcounter::MixedCodeConstants::CHUNK_MAX_DATA_BYTES)
        {
            // Checking if we copied everything.
            if (left_bytes_to_write == 0)
                break;

            num_bytes_to_write_in_chunk = left_bytes_to_write;
        }
    }

    // Setting number of total written bytes.
    *actual_written_bytes = num_bytes_to_write;

    return 0;
}

// Does everything to send the small linear user buffer to the client.
uint32_t __stdcall sc_bmx_send_small_buffer(
    uint8_t gw_worker_id,
    uint8_t* buf,
    int32_t buf_len_bytes,
    starcounter::core::chunk_index& the_chunk_index,
    int32_t chunk_user_data_offset    
    )
{
    uint8_t* cur_chunk_buf;
    uint32_t err_code = cm_get_shared_memory_chunk(the_chunk_index, &cur_chunk_buf);
    _SC_ASSERT(err_code == 0);

    // Setting number of actual user bytes written.
    *(uint32_t*)(cur_chunk_buf + starcounter::MixedCodeConstants::CHUNK_OFFSET_USER_DATA_WRITTEN_BYTES) = buf_len_bytes;

    // Setting total number of chunks.
    *(uint16_t*)(cur_chunk_buf + starcounter::MixedCodeConstants::CHUNK_OFFSET_NUM_CHUNKS) = 1;

    // Copying buffer into chunk.
    memcpy(cur_chunk_buf + chunk_user_data_offset, buf, buf_len_bytes);

    _SC_ASSERT(*(uint32_t*)(cur_chunk_buf + starcounter::MixedCodeConstants::CHUNK_OFFSET_SOCKET_DATA + starcounter::MixedCodeConstants::SOCKET_DATA_OFFSET_SOCKET_INDEX_NUMBER) < 100100);
    shared_memory_chunk* smc = (shared_memory_chunk*)cur_chunk_buf;
    _SC_ASSERT(smc->is_terminated());

    // Sending linked chunks to client.
    err_code = cm_send_to_client(gw_worker_id, the_chunk_index);
    _SC_ASSERT(err_code == 0);

    the_chunk_index = shared_memory_chunk::link_terminator;

    return 0;
}

// Does everything to send the big linear user buffer to the client.
uint32_t __stdcall sc_bmx_send_big_buffer(
    uint8_t gw_worker_id,
    uint8_t* buf,
    int32_t buf_len_bytes,
    starcounter::core::chunk_index& the_chunk_index,
    int32_t first_chunk_offset
    )
{
    starcounter::core::chunk_index new_chunk_index;
    int32_t last_written_bytes;
    uint32_t err_code;

    int32_t total_processed_bytes = 0;
    bool just_sending_flag = false;

    // Looping until all user bytes are sent.
    while (true)
    {
        // Copying user data to multiple chunks.
        err_code = sc_bmx_write_to_chunks(
            buf + total_processed_bytes,
            buf_len_bytes - total_processed_bytes,
            the_chunk_index,
            &last_written_bytes,
            first_chunk_offset,
            just_sending_flag
            );

        if (err_code)
            return err_code;

        // Increasing total sent bytes.
        total_processed_bytes += last_written_bytes;
        _SC_ASSERT(total_processed_bytes <= buf_len_bytes);

        uint8_t* cur_chunk_buf;
        uint32_t err_code = cm_get_shared_memory_chunk(the_chunk_index, &cur_chunk_buf);
        _SC_ASSERT(err_code == 0);

        shared_memory_chunk* smc = (shared_memory_chunk*)cur_chunk_buf;
        if (last_written_bytes <= starcounter::MixedCodeConstants::CHUNK_MAX_DATA_BYTES - first_chunk_offset)
            _SC_ASSERT(smc->is_terminated());

        _SC_ASSERT(*(uint32_t*)(cur_chunk_buf + starcounter::MixedCodeConstants::CHUNK_OFFSET_SOCKET_DATA + starcounter::MixedCodeConstants::SOCKET_DATA_OFFSET_SOCKET_INDEX_NUMBER) < 100100);

        // Checking if we have processed everything.
        if (total_processed_bytes < buf_len_bytes)
        {
            // Obtaining a new chunk and copying the original chunk header there.
            err_code = sc_bmx_clone_chunk(the_chunk_index, 0, starcounter::MixedCodeConstants::CHUNK_NUM_CLONE_BYTES, &new_chunk_index);
            if (err_code)
                return err_code;

            // Sending linked chunks to client.
            err_code = cm_send_to_client(gw_worker_id, the_chunk_index);
            _SC_ASSERT(err_code == 0);

            // Switching to next cloned chunk.
            the_chunk_index = new_chunk_index;

            // The rest of the data is just for sending.
            just_sending_flag = true;

            //std::cout << "Sent bytes: " << total_sent_bytes << std::endl;
        }
        else
        {
            // Sending linked chunks to client.
            err_code = cm_send_to_client(gw_worker_id, the_chunk_index);
            _SC_ASSERT(err_code == 0);
            the_chunk_index = shared_memory_chunk::link_terminator;

            //std::cout << "Sent bytes: " << total_sent_bytes << std::endl;

            break;
        }
    }

    return 0;
}

// The entry point to send any-size user data to client.
EXTERN_C uint32_t __stdcall sc_bmx_send_buffer(
    uint8_t gw_worker_id,
    uint8_t* buf,
    int32_t buf_len_bytes,
    starcounter::core::chunk_index* the_chunk_index,
    uint32_t conn_flags
    )
{
    _SC_BEGIN_FUNC

    uint8_t* cur_chunk_buf;
    uint32_t err_code = cm_get_shared_memory_chunk(*the_chunk_index, &cur_chunk_buf);
    _SC_ASSERT(err_code == 0);

    _SC_ASSERT(buf_len_bytes >= 0);
    _SC_ASSERT(NULL != cur_chunk_buf);
    _SC_ASSERT(shared_memory_chunk::link_terminator != *the_chunk_index);    

    // Points to user data offset in chunk.
    uint16_t chunk_user_data_offset = *(uint16_t*)(cur_chunk_buf + starcounter::MixedCodeConstants::CHUNK_OFFSET_USER_DATA_OFFSET_IN_SOCKET_DATA) +
        starcounter::MixedCodeConstants::CHUNK_OFFSET_SOCKET_DATA;

    _SC_ASSERT(chunk_user_data_offset < starcounter::MixedCodeConstants::CHUNK_MAX_DATA_BYTES);

    // Adding connection flags.
    (*(uint32_t*)(cur_chunk_buf + starcounter::MixedCodeConstants::CHUNK_OFFSET_SOCKET_FLAGS)) |= conn_flags;

    int32_t remaining_bytes_in_orig_chunk = (starcounter::MixedCodeConstants::CHUNK_MAX_DATA_BYTES - chunk_user_data_offset);

    // Setting non-bmx-management chunk type.
    (*(BMX_HANDLER_TYPE*)(cur_chunk_buf + starcounter::core::chunk_type::bmx_protocol_begin)) = starcounter::bmx::BMX_INVALID_HANDLER_INFO;

    // Setting request size to zero.
    (*(uint32_t*)(cur_chunk_buf + starcounter::core::chunk_type::request_size_begin)) = 0;

    // Checking if user data fits inside the request chunk.
    if (buf_len_bytes < remaining_bytes_in_orig_chunk)
    {
        // Sending using the same request chunk.
        err_code = sc_bmx_send_small_buffer(gw_worker_id, buf, buf_len_bytes, *the_chunk_index, chunk_user_data_offset);
        _SC_ASSERT(err_code == 0);
    }
    else
    {
        // Sending using multiple linked chunks.
        err_code = sc_bmx_send_big_buffer(gw_worker_id, buf, buf_len_bytes, *the_chunk_index, chunk_user_data_offset);
    }

    // Chunk becomes unusable.
    *the_chunk_index = shared_memory_chunk::link_terminator;

    return err_code;

    _SC_END_FUNC
}

// Releases given linked chunks.
EXTERN_C uint32_t __stdcall sc_bmx_release_linked_chunks(starcounter::core::chunk_index src_chunk_index)
{
    return cm_release_linked_shared_memory_chunks(src_chunk_index);
}

