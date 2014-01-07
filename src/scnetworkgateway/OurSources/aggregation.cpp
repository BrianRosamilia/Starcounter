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

// Aggregation on gateway.
uint32_t PortAggregator(
    HandlersList* hl,
    GatewayWorker *gw,
    SocketDataChunkRef sd,
    BMX_HANDLER_TYPE handler_info,
    bool* is_handled)
{
    uint32_t err_code;

    // Handled successfully.
    *is_handled = true;

    AccumBuffer* big_accum_buf = sd->get_accum_buf();
    GW_ASSERT(big_accum_buf->get_chunk_orig_buf_len_bytes() == AGGREGATION_BUFFER_SIZE);

    SocketDataChunk* sd_push_to_db = NULL;
    uint8_t* orig_data_ptr = big_accum_buf->get_chunk_orig_buf_ptr();
    int32_t num_accum_bytes = big_accum_buf->get_accum_len_bytes(), num_processed_bytes = 0;
    AggregationStruct ags;

    session_index_type aggr_socket_info_index = sd->get_socket_info_index();

    while (num_processed_bytes < num_accum_bytes)
    {
        // Processing current frame.
        uint8_t* cur_data_ptr = orig_data_ptr + num_processed_bytes;

        // Checking if frame is complete.
        if (num_accum_bytes - num_processed_bytes <= AggregationStructSizeBytes)
        {
            // Checking if we need to move current data up.
            cur_data_ptr = big_accum_buf->MoveDataToTopAndContinueReceive(cur_data_ptr, num_accum_bytes - num_processed_bytes);

            // Returning socket to receiving state.
            return gw->Receive(sd);
        }

        // Getting the frame info.
        ags = *(AggregationStruct*) cur_data_ptr;

        // Shifting to data structure size.
        num_processed_bytes += AggregationStructSizeBytes;

        // Checking if whole request is received.
        if (num_processed_bytes + ags.size_bytes_ > num_accum_bytes)
        {
            // Checking if we need to move current data up.
            big_accum_buf->MoveDataToTopAndContinueReceive(cur_data_ptr, num_accum_bytes - num_processed_bytes + AggregationStructSizeBytes);

            // Returning socket to receiving state.
            return gw->Receive(sd);
        }

        // Checking if we have already created socket.
        GW_ASSERT(true == g_gateway.CompareUniqueSocketId(ags.socket_info_index_, ags.unique_socket_id_));

        // Cloning chunk to push it to database.
        err_code = sd->CreateSocketDataFromBigBuffer(gw, ags.socket_info_index_, ags.size_bytes_, orig_data_ptr + num_processed_bytes, &sd_push_to_db);
        if (err_code)
            return err_code;

        // Applying special parameters to socket data.
        g_gateway.ApplySocketInfoToSocketData(sd_push_to_db, ags.socket_info_index_, ags.unique_socket_id_);

        // Setting this aggregation socket.
        g_gateway.SetAggregationSocketIndex(ags.socket_info_index_, sd->get_socket_info_index());

        // Setting aggregation socket.
        sd_push_to_db->set_unique_aggr_index(ags.unique_aggr_index_);
        sd_push_to_db->set_aggregated_flag();

        // Changing accumulative buffer accordingly.
        sd_push_to_db->get_accum_buf()->SetAccumulation(ags.size_bytes_, 0);

        // Payload size has been checked, so we can add payload as processed.
        num_processed_bytes += static_cast<uint32_t>(ags.size_bytes_);

        g_gateway.num_aggregated_recv_messages_++;

        // Running handler.
        err_code = gw->RunReceiveHandlers(sd_push_to_db);
        if (err_code)
            return err_code;
    }

    // Returning socket to original receiving state.
    big_accum_buf->ResetToOriginalState();
    return gw->Receive(sd);
}

// Processes all aggregated chunks.
uint32_t GatewayWorker::SendAggregatedChunks()
{
    uint32_t err_code;

    for (int32_t i = 0; i < aggr_sds_to_send_.get_num_entries(); i++)
    {
        SocketDataChunk* aggr_sd = aggr_sds_to_send_[i];

        // Removing this aggregation socket data from list.
        aggr_sds_to_send_.RemoveByIndex(i);

        // Reverting accumulating buffer before send.
        aggr_sd->get_accum_buf()->RevertBeforeSend();

        // Sending it.
        err_code = Send(aggr_sd);
        if (err_code)
            return err_code;
    }

    return 0;
}

// Tries to find current aggregation socket data from aggregation socket index.
SocketDataChunk* GatewayWorker::FindAggregationSdBySocketIndex(session_index_type aggr_socket_info_index)
{
    for (int32_t i = 0; i < aggr_sds_to_send_.get_num_entries(); i++)
    {
        if (aggr_socket_info_index == aggr_sds_to_send_[i]->get_socket_info_index())
            return aggr_sds_to_send_[i];
    }

    return NULL;
}

// Performs a send of given socket data on aggregation socket.
uint32_t GatewayWorker::SendOnAggregationSocket(SocketDataChunkRef sd)
{
    session_index_type aggr_socket_info_index = sd->GetAggregationSocketIndex();
    SocketDataChunk* aggr_sd = FindAggregationSdBySocketIndex(aggr_socket_info_index);
    uint32_t err_code;

WRITE_TO_AGGR_SD:

    if (aggr_sd)
    {
        // Checking if data fits in socket data.
        AccumBuffer* aggr_accum_buf = aggr_sd->get_accum_buf();
        uint32_t total_num_bytes = sd->get_user_data_length_bytes() + AggregationStructSizeBytes;

        // NOTE: Asserting that maximum data to send fits in big aggregation chunk.
        GW_ASSERT(total_num_bytes < aggr_accum_buf->get_chunk_orig_buf_len_bytes());

        // Checking if data fits in the current buffer space.
        if (aggr_accum_buf->get_chunk_num_available_bytes() >= total_num_bytes)
        {
            AggregationStruct* aggr_struct = (AggregationStruct*) ((uint8_t*)sd + sd->get_user_data_offset_in_socket_data() - AggregationStructSizeBytes);
            aggr_struct->flags = sd->get_type_of_network_oper();
            aggr_struct->port_number_ = g_gateway.get_server_port(sd->GetPortIndex())->get_port_number();
            aggr_struct->size_bytes_ = sd->get_user_data_length_bytes();
            aggr_struct->socket_info_index_ = sd->get_socket_info_index();
            aggr_struct->unique_socket_id_ = sd->get_unique_socket_id();
            aggr_struct->unique_aggr_index_ = static_cast<int32_t>(sd->get_unique_aggr_index());

            // Writing given buffer to send.
            aggr_accum_buf->WriteBytesToSend(aggr_struct, total_num_bytes);
            
            // Releasing the chunk.
            ReturnSocketDataChunksToPool(sd);

            // Checking if aggregation buffer is filled.
            if (0 == aggr_accum_buf->get_chunk_num_available_bytes())
            {
                // Removing this aggregation socket data from list.
                aggr_sds_to_send_.RemoveEntry(aggr_sd);

                // Reverting accumulating buffer before send.
                aggr_accum_buf->RevertBeforeSend();

                // Sending it.
                err_code = Send(aggr_sd);
                if (err_code)
                    return err_code;
            }

            return 0;
        }
        else
        {
            // Removing this aggregation socket data from list.
            aggr_sds_to_send_.RemoveEntry(aggr_sd);

            // Reverting accumulating buffer before send.
            aggr_accum_buf->RevertBeforeSend();

            // Sending it.
            err_code = Send(aggr_sd);
            if (err_code)
                return err_code;

            GW_ASSERT(NULL == aggr_sd);

            goto WRITE_TO_AGGR_SD;
        }
    }
    else
    {
        // Creating new socket data.
        err_code = CreateSocketData(aggr_socket_info_index, aggr_sd);
        if (err_code)
            return err_code;

        // Adding new aggregation sd to list.
        aggr_sds_to_send_.Add(aggr_sd);

        goto WRITE_TO_AGGR_SD;
    }

    return 0;
}

} // namespace network
} // namespace starcounter
