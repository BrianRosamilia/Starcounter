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

// When decoded, is 16 bytes in length.
const char *kSecWebSocketKey = "Sec-WebSocket-Key";
const int32_t kSecWebSocketKeyLen = static_cast<int32_t> (strlen(kSecWebSocketKey));

// Server response security field.
const char *kSecWebSocketAccept = "Sec-WebSocket-Accept";
const int32_t kSecWebSocketAcceptLen = static_cast<int32_t> (strlen(kSecWebSocketAccept));

// Should be 13.
const char *kSecWebSocketVersion = "Sec-WebSocket-Version";
const int32_t kSecWebSocketVersionLen = static_cast<int32_t> (strlen(kSecWebSocketVersion));

// Which protocols the client would like to speak.
const char *kSecWebSocketProtocol = "Sec-WebSocket-Protocol";
const int32_t kSecWebSocketProtocolLen = static_cast<int32_t> (strlen(kSecWebSocketProtocol));

const char *kWsGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
const int32_t kWsGuidLen = static_cast<int32_t> (strlen(kWsGuid));

// The server MUST close the connection upon receiving a frame that is not masked.
// A server MUST NOT mask any frames that it sends to the client. 
// A client MUST close a connection if it detects a masked frame.

const char *kWsHsResponseStaticPart =
    "HTTP/1.1 101 Switching Protocols\r\n"
    "Upgrade: websocket\r\n"
    "Connection: Upgrade\r\n"
    "Server: SC\r\n";

const int32_t kWsHsResponseStaticPartLen = static_cast<int32_t> (strlen(kWsHsResponseStaticPart));

const char* SecWebSocketAccept = "Sec-WebSocket-Accept: ";
const int32_t SecWebSocketAcceptLen = static_cast<int32_t> (strlen(SecWebSocketAccept));

const char* SecWebSocketProtocol = "Sec-WebSocket-Protocol: ";
const int32_t SecWebSocketProtocolLen = static_cast<int32_t> (strlen(SecWebSocketProtocol));

const char *kWsBadProto =
    "HTTP/1.1 400 Bad Request\r\n"
    "Sec-WebSocket-Version: 13\r\n"
    "Content-Length: 0\r\n"
    "Pragma: no-cache\r\n"
    "\r\n";

const int32_t kWsBadProtoLen = static_cast<int32_t> (strlen(kWsBadProto));

//////////////////////////////////////////////////////////
/////////////////THREAD STATIC DATA///////////////////////
//////////////////////////////////////////////////////////

__declspec(thread) char* g_ts_client_key_;
__declspec(thread) char* g_ts_sub_protocol_;
__declspec(thread) uint8_t g_ts_client_key_len_;
__declspec(thread) uint8_t g_ts_sub_protocol_len_;

// Sets the client key.
void WsProto::SetClientKey(char *client_key, int32_t client_key_len)
{
    g_ts_client_key_ = client_key;
    g_ts_client_key_len_ = client_key_len;
}

// Sets the sub protocol.
void WsProto::SetSubProtocol(char *sub_protocol, int32_t sub_protocol_len)
{
    g_ts_sub_protocol_ = sub_protocol;
    g_ts_sub_protocol_len_ = sub_protocol_len;
}

// Resets the structure.
void WsProto::Reset()
{
    g_ts_client_key_ = NULL;
    g_ts_client_key_len_ = 0;

    g_ts_sub_protocol_ = NULL;
    g_ts_sub_protocol_len_ = 0;
}

// Initializes the structure.
void WsProto::Init()
{
    frame_info_.Reset();
    Reset();
}

// Unmasks frame and pushes it to database.
uint32_t WsProto::UnmaskFrameAndPush(GatewayWorker *gw, SocketDataChunkRef sd, BMX_HANDLER_TYPE user_handler_id)
{
    uint8_t* payload = sd->get_accum_buf()->get_chunk_orig_buf_ptr() + frame_info_.payload_offset_;

    switch (frame_info_.opcode_)
    {
        case WS_OPCODE_CONTINUATION: // TODO: Fix full support!
        case WS_OPCODE_TEXT:
        case WS_OPCODE_BINARY:
        {
            // Unmasking data.
            UnMaskPayload(gw, sd, frame_info_.payload_len_, frame_info_.mask_, payload);

            // Setting request offsets.
            HttpRequest* req = sd->get_http_proto()->get_http_request();
            req->request_len_bytes_ = frame_info_.payload_len_;
            req->request_offset_ = static_cast<uint32_t> (payload - (uint8_t*)sd);
            req->content_len_bytes_ = req->request_len_bytes_;
            req->content_offset_ = req->request_offset_;

            // Determining user data offset.
            uint32_t user_data_offset = static_cast<uint32_t> (payload - (uint8_t *) sd);
            sd->set_user_data_offset_in_socket_data(user_data_offset);

            // Push chunk to corresponding channel/scheduler.
            return gw->PushSocketDataToDb(sd, user_handler_id);
        }

        case WS_OPCODE_CLOSE:
        {
            uint32_t payload_len = frame_info_.payload_len_;

            // Send the response Close message.
            UnMaskPayload(gw, sd, payload_len, frame_info_.mask_, payload);
            payload = WritePayload(gw, sd, WS_OPCODE_CLOSE, false, WS_FRAME_SINGLE, payload_len, payload, payload_len);

            // Sending resource not found and closing the connection.
            sd->set_disconnect_after_send_flag();

            // Prepare buffer to send outside.
            sd->PrepareForSend(payload, payload_len);

            // Sending data.
            return gw->Send(sd);
        }

		case WS_OPCODE_PONG:
        case WS_OPCODE_PING:
        {
            uint32_t payload_len = frame_info_.payload_len_;

            // Send the response Pong.
            UnMaskPayload(gw, sd, payload_len, frame_info_.mask_, payload);
            payload = WritePayload(gw, sd, WS_OPCODE_PONG, false, WS_FRAME_SINGLE, payload_len, payload, payload_len);

            // Prepare buffer to send outside.
            sd->PrepareForSend(payload, payload_len);

            // Sending data.
            return gw->Send(sd);
        }

        default:
        {
            // Peer wants to close the WebSocket.
            return SCERRGWWEBSOCKETUNKNOWNOPCODE;
        }
    }
}

// Processes incoming WebSocket frames.
uint32_t WsProto::ProcessWsDataToDb(
    GatewayWorker *gw,
    SocketDataChunkRef sd,
    BMX_HANDLER_TYPE user_handler_id,
    bool* is_handled)
{
    // Handled successfully.
    *is_handled = true;

    uint32_t err_code = 0;

    // Obtaining saved user handler id.
    user_handler_id = sd->GetSavedUserHandlerId();
    GW_ASSERT_DEBUG(bmx::BMX_INVALID_HANDLER_INFO != user_handler_id);

    SocketDataChunk* sd_push_to_db = NULL;
    uint8_t* orig_data_ptr = sd->get_accum_buf()->get_chunk_orig_buf_ptr();
    uint32_t num_accum_bytes = sd->get_accum_buf()->get_accum_len_bytes();
    uint32_t num_processed_bytes = 0;

    // Checking if we have already parsed the frame.
    if (sd->get_complete_header_flag())
        goto DATA_ACCUMULATED;

    // Since WebSocket frames can be grouped into one network packet
    // we have to processes all of them in a loop.
    while (true)
    {
        // Processing current frame.
        uint8_t* cur_data_ptr = orig_data_ptr + num_processed_bytes;

        // Obtaining frame info.
        err_code = ParseFrameInfo(sd, cur_data_ptr, orig_data_ptr + num_accum_bytes);
        if (err_code)
            return err_code;

        // Checking frame information is complete.
        if (!sd->get_complete_header_flag())
        {
            // Checking if we need to move current data up.
            cur_data_ptr = sd->get_accum_buf()->MoveDataToTopAndContinueReceive(cur_data_ptr, num_accum_bytes - num_processed_bytes);

            // Returning socket to receiving state.
            return gw->Receive(sd);
        }

        uint8_t* payload = sd->get_accum_buf()->get_chunk_orig_buf_ptr() + frame_info_.payload_offset_;

        // Calculating number of bytes processed.
        int32_t header_len = static_cast<int32_t> (payload - cur_data_ptr);
        num_processed_bytes += header_len;

        // Continue accumulating data.
        if (num_processed_bytes + frame_info_.payload_len_ > num_accum_bytes)
        {
            // Setting the desired number of bytes to accumulate.
            err_code = gw->StartAccumulation(
                sd,
                static_cast<uint32_t>(header_len + frame_info_.payload_len_),
                header_len + num_accum_bytes - num_processed_bytes);

            if (err_code)
                return err_code;

            // Checking if we have not accumulated everything yet.
            return gw->Receive(sd);
        }
        // Checking if it is not the last frame.
        else
        {
            // Checking if all received data processed.
            if (num_processed_bytes + frame_info_.payload_len_ == num_accum_bytes)
            {
                sd_push_to_db = NULL;
            }
            else
            {
                // Cloning chunk to push it to database.
                err_code = sd->CloneToPush(gw, &sd_push_to_db);
                if (err_code)
                    return err_code;
            }
        }

        // Payload size has been checked, so we can add payload as processed.
        num_processed_bytes += static_cast<uint32_t>(frame_info_.payload_len_);

#ifdef GW_WEBSOCKET_DIAG
        GW_COUT << "[" << gw->get_worker_id() << "]: " << "WS_OPCODE: " << frame_info_.opcode_ << GW_ENDL;
#endif

DATA_ACCUMULATED:

        // Data is complete, no more frames, creating parallel receive clone.
        if (NULL == sd_push_to_db)
        {
            // Aggregation is done separately.
            if (!sd->GetSocketAggregatedFlag())
            {
                err_code = sd->CloneToReceive(gw);
                if (err_code)
                    return err_code;
            }

            // Unmasking frame and pushing to database.
            return UnmaskFrameAndPush(gw, sd, user_handler_id);
        }
        else
        {
            // Unmasking frame and pushing to database.
            err_code = UnmaskFrameAndPush(gw, sd_push_to_db, user_handler_id);
            if (err_code)
                return err_code;
        }
    }

    return 0;
}

// Processes payload data from database.
uint32_t WsProto::ProcessWsDataFromDb(GatewayWorker *gw, SocketDataChunkRef sd, BMX_HANDLER_TYPE user_handler_id, bool* is_handled)
{
    // Handled successfully.
    *is_handled = true;

    // Checking if we want to disconnect the socket.
    if (sd->get_disconnect_socket_flag())
        return SCERRGWDISCONNECTFLAG;

    // Checking if this socket data is for send only.
    if (sd->get_socket_just_send_flag())
    {
        sd->reset_socket_just_send_flag();

        goto JUST_SEND_SOCKET_DATA;
    }

    // Getting user data.
    uint8_t* payload = sd->UserDataBuffer();
    uint8_t* orig_payload = payload;

    // Length of user data in bytes.
    uint32_t total_payload_len = sd->get_accum_buf()->get_desired_accum_bytes();
    uint32_t cur_payload_len = sd->get_user_data_length_bytes();

    // Checking if we are sending last frame.
    if (sd->get_gracefully_close_flag())
    {
        sd->reset_gracefully_close_flag();
        sd->set_disconnect_after_send_flag();
        frame_info_.opcode_ = WS_OPCODE_CLOSE;
    }

    // Place where masked data should be written.
    payload = WritePayload(gw, sd, frame_info_.opcode_, false, WS_FRAME_SINGLE, total_payload_len, payload, cur_payload_len);

    // Prepare buffer to send outside.
    sd->PrepareForSend(payload, cur_payload_len);

    // Calculating difference between original user data and post-processed.
    int32_t diff = static_cast<int32_t>(orig_payload - payload);

    // Adjusting user data parameters.
    sd->set_user_data_offset_in_socket_data(sd->get_user_data_offset_in_socket_data() - diff);

JUST_SEND_SOCKET_DATA:

    // Sending data.
    return gw->Send(sd);
}

// Performs the WebSocket handshake.
uint32_t WsProto::DoHandshake(GatewayWorker *gw, SocketDataChunkRef sd, BMX_HANDLER_TYPE user_handler_id, bool* is_handled)
{
    // Handled successfully.
    *is_handled = true;

    uint32_t err_code;

    // Generating handshake response.
    char handshake_resp_temp[128];
    strncpy_s(handshake_resp_temp, 128, g_ts_client_key_, _TRUNCATE);
    strncpy_s(handshake_resp_temp + g_ts_client_key_len_, 128 - g_ts_client_key_len_, kWsGuid, _TRUNCATE);

    // Generating SHA1 into temp buffer.
    char* sha1_begin = handshake_resp_temp + g_ts_client_key_len_ + kWsGuidLen;
    SHA1((uint8_t *)handshake_resp_temp, g_ts_client_key_len_ + kWsGuidLen, (uint8_t *)sha1_begin);

    // Converting SHA1 into Base64.
    char* base64_begin = sha1_begin + 20;
    base64_encodestate b64;
    base64_init_encodestate(&b64);
    uint8_t base64_len = static_cast<uint8_t>(base64_encode_block(sha1_begin, 20, base64_begin, &b64));
    base64_len += static_cast<uint8_t>(base64_encode_blockend(base64_begin + base64_len, &b64) - 1);

    // Copying sub-protocol data.
    char sub_protocol_temp[32];
    GW_ASSERT_DEBUG(g_ts_sub_protocol_len_ < 32);
    memcpy(sub_protocol_temp, g_ts_sub_protocol_, g_ts_sub_protocol_len_);

    // Pointing to the beginning of the data.
    uint8_t* resp_data_begin = sd->get_accum_buf()->get_chunk_orig_buf_ptr();

    // Copying initial header in response buffer.
    int32_t resp_len_bytes = InjectData(resp_data_begin, 0, kWsHsResponseStaticPart, kWsHsResponseStaticPartLen);

    // Sec-WebSocket-Accept.
    GW_ASSERT_DEBUG(28 == base64_len);
    resp_len_bytes = InjectData(resp_data_begin, resp_len_bytes, SecWebSocketAccept, SecWebSocketAcceptLen);
    resp_len_bytes = InjectData(resp_data_begin, resp_len_bytes, base64_begin, base64_len);
    resp_len_bytes = InjectData(resp_data_begin, resp_len_bytes, "\r\n", 2);

    // Sec-WebSocket-Protocol.
    if (g_ts_sub_protocol_len_)
    {
        resp_len_bytes = InjectData(resp_data_begin, resp_len_bytes, SecWebSocketProtocol, SecWebSocketProtocolLen);
        resp_len_bytes = InjectData(resp_data_begin, resp_len_bytes, sub_protocol_temp, g_ts_sub_protocol_len_);
        resp_len_bytes = InjectData(resp_data_begin, resp_len_bytes, "\r\n", 2);
    }
    
    // Remaining empty line.
    resp_len_bytes = InjectData(resp_data_begin, resp_len_bytes, "\r\n", 2);

    // Setting WebSocket handshake flag.
    sd->SetTypeOfNetworkProtocol(MixedCodeConstants::NetworkProtocolType::PROTOCOL_WEBSOCKETS);

    // Setting fixed handler id.
    sd->SetSavedUserHandlerId(user_handler_id);

    // Prepare buffer to send outside.
    sd->PrepareForSend(resp_data_begin, resp_len_bytes);

    // Sending data.
    err_code = gw->Send(sd);
    if (err_code)
        return err_code;

    // Printing the outgoing packet.
#ifdef GW_WEBSOCKET_DIAG
    GW_COUT << resp_data_begin << GW_ENDL;
#endif

    return 0;
}

// Masks or unmasks payload.
void WsProto::MaskUnMask(
    uint8_t* payload,
    int32_t payload_len,
    uint64_t mask_8bytes,
    int8_t& num_remaining_bytes)
{
    GW_ASSERT_DEBUG(num_remaining_bytes < 8);

    int32_t processed_bytes = 0, bytes_left = payload_len;

    // Checking if we have any remaining bytes.
    if (num_remaining_bytes)
    {
        int32_t num_begin_bytes = 8 - num_remaining_bytes;

        for (int32_t i = 0; i < num_remaining_bytes; i++)
            payload[i] = payload[i] ^ (((uint8_t*)&mask_8bytes)[num_begin_bytes + i]);

        processed_bytes += num_remaining_bytes;
    }

    // Masking until all bytes are processed.
    while (processed_bytes < payload_len)
    {
        int32_t tail_bytes_num = bytes_left - processed_bytes;
        if (tail_bytes_num < 8)
        {
            // Processing last bytes.
            for (int32_t i = 0; i < tail_bytes_num; i++)
                payload[processed_bytes + i] = payload[processed_bytes + i] ^ (((uint8_t*)&mask_8bytes)[i]);

            num_remaining_bytes = 8 - tail_bytes_num;

            return;
        }
        else
        {
            *((uint64_t*)(payload + processed_bytes)) ^= mask_8bytes;
            processed_bytes += 8;
        }
    }
}

// Masks or unmasks payload.
void WsProto::UnMaskPayload(
    GatewayWorker *gw,
    SocketDataChunkRef sd,
    const uint32_t payload_len_bytes,
    const uint64_t mask,
    uint8_t* payload)
{
    // TODO: Retrieve remaining bytes from saved value.
    int8_t num_remaining_bytes = 0;
    uint64_t mask_8bytes = mask | (mask << 32);

    MaskUnMask(payload, payload_len_bytes, mask_8bytes, num_remaining_bytes);

    // TODO: Save number of remaining bytes for the next payload chunk.
}

#define swap64(y) ((static_cast<uint64_t>(ntohl(static_cast<uint32_t>(y))) << 32) | ntohl(static_cast<uint32_t>(y >> 32)))

// Parses WebSockets frame info.
uint32_t WsProto::ParseFrameInfo(SocketDataChunkRef sd, uint8_t *data, uint8_t* limit)
{
    // Getting final fragment bit.
    frame_info_.is_final_ = ((*data & 0x80) != 0);

    // Getting operation code.
    frame_info_.opcode_ = (*data & 0x0F);

    // Getting mask flag.
    data++;

    // Removing the mask flag.
    (*data) &= 0x7F;

    // Calculating the payload length.
    switch(*data)
    {
        // 16 bits.
        case 126:
        {
            frame_info_.payload_len_ = ntohs(*(uint16_t *)(data + 1));
            frame_info_.mask_ = *(uint32_t *)(data + 3);
            data += 7;
            break;
        }

        // 64 bits.
        case 127:
        {
            frame_info_.payload_len_ = swap64(*(uint64_t *)(data + 1));
            frame_info_.mask_ = *(uint32_t *)(data + 9);
            data += 13;
            break;
        }

        // 7 bits.
        default:
        {
            frame_info_.payload_len_ = *data;
            frame_info_.mask_ = *(uint32_t *)(data + 1);
            data += 5;
        }
    }

    // Increasing limit by one if payload length is 0.
    if (0 == frame_info_.payload_len_)
        limit++;

    // Checking if frame was successfully parsed.
    if (data < limit)
        sd->set_complete_header_flag();
    
    frame_info_.payload_offset_ = static_cast<uint32_t> (data - sd->get_accum_buf()->get_chunk_orig_buf_ptr());

    return 0;
}

// Write payload data.
uint8_t *WsProto::WritePayload(
    GatewayWorker* gw,
    SocketDataChunkRef sd,
    uint8_t opcode,
    bool masking,
    WS_FRAGMENT_FLAG frame_type,
    uint32_t total_payload_len,
    uint8_t* payload,
    uint32_t& cur_payload_len)
{
    // Pointing to destination memory.
    uint8_t *p = payload - 1;

    // Checking masking.
    if (masking)
        p -= 4;

    // Checking payload length.
    if (total_payload_len < 126)
        p -= 1;
    else if (total_payload_len <= 0xFFFF)
        p -= 3;
    else
        p -= 9;

    // Saving frame pointer.
    uint8_t *frame_start = p;

    // Applying opcode depending on the frame type.
    switch (frame_type)
    {
        case WS_FRAME_SINGLE:
        {
            *p = 0x80 | opcode;
            break;
        }

        case WS_FRAME_FIRST:
        {
            *p = opcode;
            break;
        }

        case WS_FRAME_CONT:
        {
            *p = 0;
            break;
        }

        case WS_FRAME_LAST:
        {
            *p = 0x80;
            break;
        }
    }

    // Shifting to payload length byte.
    p++;

    // Writing payload length.
    if (total_payload_len < 126)
    {
        *p = static_cast<uint8_t>(total_payload_len);
        p++;
    }
    else if (total_payload_len <= 0xFFFF)
    {
        *p = 126;
        (*(uint16_t *)(p + 1)) = htons(static_cast<uint16_t>(total_payload_len));
        p += 3;
    }
    else
    {
        *p = 127;
        (*(uint64_t *)(p + 1)) = swap64(static_cast<uint64_t>(total_payload_len));
        p += 9;
    }

    // Checking if we do masking.
    if (masking)
    {
        // Create some random mask for use.
        uint32_t mask = static_cast<uint32_t>(gw->get_random()->uint64());

        // Writing mask.
        *(uint32_t *)p = mask;
        *(frame_start + 1) |= 0x80;

        // Shifting to the payload itself.
        p += 4;

        // Do masking on all data.
        UnMaskPayload(gw, sd, cur_payload_len, mask, p);
    }

    // Returning total data length.
    cur_payload_len = static_cast<uint32_t>(payload - frame_start) + cur_payload_len;

    // Returning frame start address.
    return frame_start;
}

} // namespace network
} // namespace starcounter
