#pragma once
#ifndef WS_PROTO_HPP
#define WS_PROTO_HPP

namespace starcounter {
namespace network {

// Message types.
enum WS_OPCODE
{
    WS_OPCODE_CONTINUATION = 0,
    WS_OPCODE_TEXT = 1,
    WS_OPCODE_BINARY = 2,
    WS_OPCODE_RESERVED1 = 3,
    WS_OPCODE_RESERVED2 = 4,
    WS_OPCODE_RESERVED3 = 5,
    WS_OPCODE_RESERVED4 = 6,
    WS_OPCODE_RESERVED5 = 7,
    WS_OPCODE_CLOSE = 8,
    WS_OPCODE_PING = 9,
    WS_OPCODE_PONG = 10,
    WS_OPCODE_RESERVED6 = 11,
    WS_OPCODE_RESERVED7 = 12,
    WS_OPCODE_RESERVED8 = 13,
    WS_OPCODE_RESERVED9 = 14,
    WS_OPCODE_RESERVED10 = 15
};

// Frame types.
enum WS_FRAGMENT_FLAG
{
    WS_FRAME_SINGLE,
    WS_FRAME_FIRST,
    WS_FRAME_CONT,
    WS_FRAME_LAST
};

// Frame info.
class WsProtoFrameInfo
{
    // Declaring a friend class to access private fields.
    friend class WsProto;
    friend class SocketDataChunk;

    // Masking value.
    uint64_t mask_;

    // Payload length in bytes.
    uint32_t payload_len_;

    // Payload offset in sd data blob.
    uint16_t payload_offset_;

    // Is final frame.
    bool is_final_;

    // Opcode type.
    uint8_t opcode_;

    void Reset()
    {
        memset(this, 0, sizeof(WsProtoFrameInfo));
    }
};

class GatewayWorker;
class SocketDataChunk;

class WsProto
{
    // Frame information.
    WsProtoFrameInfo frame_info_;

public:

    // WebSockets frame info.
    WsProtoFrameInfo* get_frame_info()
    {
        return &frame_info_;
    }

    // Sets the client key.
    void SetClientKey(char *client_key, int32_t client_key_len);

    // Sets the sub protocol.
    void SetSubProtocol(char *sub_protocol, int32_t sub_protocol_len);

    // Resets the structure.
    void Reset();

    void Init();

    uint32_t UnmaskFrameAndPush(GatewayWorker *gw, SocketDataChunkRef sd, BMX_HANDLER_TYPE user_handler_id);

    uint32_t ProcessWsDataToDb(GatewayWorker *gw, SocketDataChunkRef sd, BMX_HANDLER_TYPE user_handler_id, bool* is_handled);

    uint32_t ProcessWsDataFromDb(GatewayWorker *gw, SocketDataChunkRef sd, BMX_HANDLER_TYPE user_handler_id, bool* is_handled);

    uint32_t DoHandshake(GatewayWorker *gw, SocketDataChunkRef sd, BMX_HANDLER_TYPE user_handler_id, bool* is_handled);

    void MaskUnMask(
        uint8_t* data,
        int32_t data_len,
        uint64_t mask,
        int8_t& num_remaining_bytes);

    void UnMaskPayload(GatewayWorker* gw, SocketDataChunkRef sd, uint32_t payloadLen, uint64_t mask, uint8_t* data);

    uint8_t *WritePayload(GatewayWorker* gw, SocketDataChunkRef sd, uint8_t opcode, bool masking, WS_FRAGMENT_FLAG frame_type, uint32_t total_payload_len, uint8_t* payload, uint32_t& payload_len);

    uint32_t ParseFrameInfo(SocketDataChunkRef sd, uint8_t *data, uint8_t* limit);
};

} // namespace network
} // namespace starcounter

#endif // WS_PROTO_HPP
