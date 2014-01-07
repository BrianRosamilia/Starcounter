#if __cplusplus

#pragma once

// C++ code--removes public keyword for C++
#define public

typedef char* const String;
typedef unsigned int uint;

namespace starcounter
{
namespace MixedCodeConstants
{

#else

// C# code
using System;
using System.Runtime.InteropServices;
namespace Starcounter.Internal
{
    public class MixedCodeConstants
    {
        //... C# Specific class code

#endif

        // These items are members of the shared C# class and global in C++.

        /// <summary>
        /// Maximum size of BMX header in the beginning of the chunk
        /// after which the gateway data can be placed.
        /// </summary>
        public const int BMX_HEADER_MAX_SIZE_BYTES = 32;

        /// <summary>
        /// Offset of socket data in chunk.
        /// </summary>
        public const int CHUNK_OFFSET_SOCKET_DATA = BMX_HEADER_MAX_SIZE_BYTES;

        /// <summary>
        /// Data offset/size constants. 
        /// </summary>
        public const int BMX_HANDLER_SIZE = 2;

        /// <summary>
        /// Session string length in characters.
        /// </summary>
        public const int SESSION_STRING_LEN_CHARS = 24;

        /// <summary>
        /// Size of the session structure in bytes.
        /// </summary>
        public const int SESSION_STRUCT_SIZE = 16;

        /// <summary>
        /// Just send flag.
        /// </summary>
        public const int SOCKET_DATA_FLAGS_JUST_SEND = 2 << 4;

        /// <summary>
        /// Disconnect after send flag.
        /// </summary>
        public const int SOCKET_DATA_FLAGS_DISCONNECT_AFTER_SEND = 2 << 2;

        /// <summary>
        /// Just disconnect flag.
        /// </summary>
        public const int SOCKET_DATA_FLAGS_DISCONNECT = 2 << 5;

        /// <summary>
        /// Gracefully close flag.
        /// </summary>
        public const int HTTP_WS_FLAGS_GRACEFULLY_CLOSE = 2 << 10;

        /// <summary>
        /// Is socket data aggregated.
        /// </summary>
        public const int SOCKET_DATA_FLAGS_AGGREGATED = 2 << 13;

        /// <summary>
        /// Is socket data in host accumulation.
        /// </summary>
        public const int SOCKET_DATA_FLAGS_ON_HOST_ACCUMULATION = 2 << 14;

        /// <summary>
        /// Invalid chunk index.
        /// </summary>
        public const uint INVALID_CHUNK_INDEX = 0xFFFFFFFF;
        
        /// <summary>
        /// Offsets in socket data and chunk.
        /// </summary>
        public const int SOCKET_DATA_OFFSET_SESSION = 152;
        public const int CHUNK_OFFSET_SESSION = 184;
        public const int CHUNK_OFFSET_SESSION_SCHEDULER_ID = 196;
        public const int CHUNK_OFFSET_SESSION_LINEAR_INDEX = 192;
        public const int CHUNK_OFFSET_SESSION_RANDOM_SALT = 184;
        public const int SOCKET_DATA_OFFSET_PARAMS_INFO = 168;
        public const int SOCKET_DATA_OFFSET_BLOB = 232;
        public const int CHUNK_OFFSET_NUM_IPC_CHUNKS = 32;
        public const int CHUNK_OFFSET_SOCKET_FLAGS = 84;
        public const int SOCKET_DATA_OFFSET_NETWORK_PROTO_TYPE = 63;
        public const int SOCKET_DATA_OFFSET_CLIENT_IP = 40;
        public const int SOCKET_DATA_OFFSET_HTTP_REQUEST = 112;
        public const int SOCKET_DATA_NUM_CLONE_BYTES = 168;
        public const int CHUNK_OFFSET_USER_DATA_OFFSET_IN_SOCKET_DATA = 92;
        public const int CHUNK_OFFSET_USER_DATA_TOTAL_LENGTH = 136;
        public const int CHUNK_OFFSET_USER_DATA_WRITTEN_BYTES = 104;
        public const int SOCKET_DATA_OFFSET_SOCKET_UNIQUE_ID = 32;
        public const int SOCKET_DATA_OFFSET_SOCKET_INDEX_NUMBER = 48;
        public const int SOCKET_DATA_OFFSET_WS_OPCODE = 151;
        public const int SOCKET_DATA_OFFSET_BOUND_WORKER_ID = 165;
        
        /// <summary>
        /// Maximum number of URI callback parameters.
        /// </summary>
        public const int MAX_URI_CALLBACK_PARAMS = 16;

        /// <summary>
        /// Parameters info max size bytes.
        /// </summary>
        public const int PARAMS_INFO_MAX_SIZE_BYTES = 64;

        /// <summary>
        /// Shared memory chunk size.
        /// </summary>
        public const int SHM_CHUNK_SIZE = 512;

        /// <summary>
        /// Shared memory chunks default number.
        /// </summary>
        public const int SHM_CHUNKS_DEFAULT_NUMBER = 1 << 16;

        /// <summary>
        /// Linked chunk flag.
        /// </summary>
        public const int LINKED_CHUNKS_FLAG = 1;

        /// <summary>
        /// Number of clone bytes in chunk.
        /// </summary>
        public const int CHUNK_NUM_CLONE_BYTES = CHUNK_OFFSET_SOCKET_DATA + SOCKET_DATA_NUM_CLONE_BYTES;

        /// <summary>
        /// Chunk link size.
        /// </summary>
        public const int CHUNK_LINK_SIZE = 8;

        /// <summary>
        /// Chunk data max size.
        /// </summary>
        public const int CHUNK_MAX_DATA_BYTES = SHM_CHUNK_SIZE - CHUNK_LINK_SIZE;

        /// <summary>
        /// Socket data max size.
        /// </summary>
        public const int SOCKET_DATA_MAX_SIZE = CHUNK_MAX_DATA_BYTES - CHUNK_OFFSET_SOCKET_DATA;

        /// <summary>
        /// Maximum extra linked IPC chunks.
        /// </summary>
        public const int MAX_EXTRA_LINKED_IPC_CHUNKS = 32;

        /// <summary>
        /// Maximum linked chunks bytes.
        /// </summary>
        public const int MAX_BYTES_EXTRA_LINKED_IPC_CHUNKS = MAX_EXTRA_LINKED_IPC_CHUNKS * CHUNK_MAX_DATA_BYTES;

        /// <summary>
        /// Size of socket data blob.
        /// </summary>
        public const int SOCKET_DATA_BLOB_SIZE_BYTES = SOCKET_DATA_MAX_SIZE - SOCKET_DATA_OFFSET_BLOB;

        // Maximum URI string length.
        public const int MAX_URI_STRING_LEN = 512;

        // Session parameter type number in user delegate.
        public const int REST_ARG_SESSION = 10;

        // Bad server log handler.
        public const int INVALID_SERVER_LOG_HANDLE = 0;

        /// <summary>
        /// Example of string constant.
        /// </summary>
        public const String DefaultPersonalServerNameUpper = "PERSONAL";

        /// <summary>
        /// Name of Administrator application.
        /// </summary>
        public const String AdministratorAppName = "Administrator";

        /// <summary>
        /// Name of session cookie.
        /// </summary>
        public const String ScSessionCookieName = "ScSessionCookie";

        /// <summary>
        /// Session cookie length.
        /// </summary>
        public const int ScSessionCookieNameLength = 15;

        /// <summary>
        /// Denotes the type of network protocol.
        /// </summary>
        public enum NetworkProtocolType
        {
            PROTOCOL_RAW_PORT,
            PROTOCOL_SUB_PORT,
            PROTOCOL_HTTP1,
            PROTOCOL_WEBSOCKETS,
            PROTOCOL_HTTP2
        };

#if !__cplusplus

        // C# code

        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct RegisteredUriManaged
        {
            public unsafe IntPtr original_uri_info_string;
            public UInt32 original_uri_info_len_chars;
            public unsafe IntPtr processed_uri_info_string;
            public UInt32 processed_uri_info_len_chars;
            public Int32 handler_id;
            public fixed Byte param_types[MixedCodeConstants.MAX_URI_CALLBACK_PARAMS];
            public Byte num_params;
        };

        public struct UserDelegateParamInfo
        {
            public UInt16 offset_;
            public UInt16 len_;

            public UserDelegateParamInfo(UInt16 offset, UInt16 len)
            {
                offset_ = offset;
                len_ = len;
            }
        }
    };

#endif

#if __cplusplus

	// C++ code

    typedef uint64_t server_log_handle_type;

    struct RegisteredUriManaged
    {
        char* original_uri_info_string;
        uint32_t original_uri_info_len_chars;
        char* processed_uri_info_string;
        uint32_t processed_uri_info_len_chars;
        int32_t handler_id;
        uint8_t param_types[MixedCodeConstants::MAX_URI_CALLBACK_PARAMS];
        uint8_t num_params;
    };

    struct UserDelegateParamInfo
    {
        uint16_t offset_;
        uint16_t len_;
    };

    typedef int32_t (*MatchUriType) (char* uri_info, uint32_t uri_info_len, UserDelegateParamInfo** params);

    typedef uint32_t (*GenerateNativeUriMatcherType) (
        server_log_handle_type server_log_handle,
        const char* const root_function_name,
        RegisteredUriManaged* uri_infos,
        uint32_t num_uris,
        char* gen_code_str,
        uint32_t* gen_code_str_num_bytes
        );

#undef public
}
}

#else

    // C# code
}

#endif

