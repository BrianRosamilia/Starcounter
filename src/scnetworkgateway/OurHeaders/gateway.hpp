#pragma once
#ifndef GATEWAY_HPP
#define GATEWAY_HPP

// Connectivity headers include.
#include "common/macro_definitions.hpp"
#include "common/config_param.hpp"
#include "common/shared_interface.hpp"
#include "common/database_shared_memory_parameters.hpp"
#include "common/monitor_interface.hpp"
#include "common/circular_buffer.hpp"
#include "common/bounded_buffer.hpp"
#include "common/chunk.hpp"
#include "common/shared_chunk_pool.hpp"
#include "common/chunk_pool.hpp"
#include "common/channel.hpp"
#include "common/scheduler_channel.hpp"
#include "common/common_scheduler_interface.hpp"
#include "common/scheduler_interface.hpp"
#include "common/common_client_interface.hpp"
#include "common/client_interface.hpp"
#include "common/client_number.hpp"
#include "common/macro_definitions.hpp"
#include "common/interprocess.hpp"
#include "common/name_definitions.hpp"

// Level0 includes.
#include <sccoredbg2.h>
#include <sccorelog.h>

// HTTP related stuff.
#include "../../HTTP/HttpParser/OurHeaders/http_request.hpp"

// BMX/Blast2 include.
#include "../Chunks/bmx/bmx.hpp"
#include "../Chunks/bmx/chunk_helper.h"

#include "../../Starcounter.Internal/Constants/MixedCodeConstants.cs"

// Internal includes.
#include "utilities.hpp"
#include "profiler.hpp"

#pragma warning(pop)
#pragma warning(pop)

namespace starcounter {
namespace network {

// Data types definitions.
typedef uint32_t channel_chunk;
typedef uint64_t random_salt_type;
typedef uint32_t session_index_type;
typedef uint8_t scheduler_id_type;
typedef uint64_t socket_timestamp_type;
typedef int64_t echo_id_type;
typedef uint64_t ip_info_type;
typedef int16_t uri_index_type;
typedef int16_t port_index_type;
typedef int8_t db_index_type;
typedef int8_t worker_id_type;
typedef int8_t chunk_store_type;

// Statistics macros.
#define GW_COLLECT_SOCKET_STATISTICS
//#define GW_DETAILED_STATISTICS
//#define GW_ECHO_STATISTICS

// Diagnostics macros.
//#define GW_SOCKET_DIAG
//#define GW_HTTP_DIAG
//#define GW_WEBSOCKET_DIAG
//#define GW_ERRORS_DIAG
//#define GW_WARNINGS_DIAG
//#define GW_CHUNKS_DIAG
#define GW_DATABASES_DIAG
//#define GW_SESSIONS_DIAG
#define GW_COLLECT_INACTIVE_SOCKETS
//#define GW_LOOPBACK_AGGREGATION
//#define GW_IOCP_IMMEDIATE_COMPLETION

#ifdef GW_DEV_DEBUG
#define GW_SC_BEGIN_FUNC
#define GW_SC_END_FUNC
#define GW_ASSERT assert
#define GW_ASSERT_DEBUG assert
#else
#define GW_SC_BEGIN_FUNC _SC_BEGIN_FUNC
#define GW_SC_END_FUNC _SC_END_FUNC
#define GW_ASSERT _SC_ASSERT
#define GW_ASSERT_DEBUG _SC_ASSERT_DEBUG
#endif

//#define GW_PONG_MODE
//#define GW_TESTING_MODE
//#define GW_LOOPED_TEST_MODE
//#define GW_PROFILER_ON
//#define GW_LIMITED_ECHO_TEST

// Checking that macro definitions are correct.
#ifdef GW_LOOPED_TEST_MODE
#define GW_TESTING_MODE
#endif

#ifndef GW_TESTING_MODE
// Enabling proxy if we are not in testing mode.
#define GW_PROXY_MODE
#endif

// TODO: Move error codes to errors XML!
enum GatewayErrorCodes
{
    SCERRGWFAILEDWSARECV = 12346,
    SCERRGWSOCKETCLOSEDBYPEER,
    SCERRGWFAILEDACCEPTEX,
    SCERRGWFAILEDWSASEND,
    SCERRGWDISCONNECTAFTERSENDFLAG,
    SCERRGWDISCONNECTFLAG,
    SCERRGWWEBSOCKETUNKNOWNOPCODE,
    SCERRGWWEBSOCKETNOMASK,
    SCERRGWMAXPORTHANDLERS,
    SCERRGWWRONGHANDLERTYPE,
    SCERRGWHANDLERNOTFOUND,
    SCERRGWPORTNOTHANDLED,
    SCERRGWNONHTTPPROTOCOL,
    SCERRGWHTTPNONWEBSOCKETSUPGRADE,
    SCERRGWHTTPWRONGWEBSOCKETSVERSION,
    SCERRGWHTTPINCORRECTDATA,
    SCERRGWHTTPPROCESSFAILED,
    SCERRGWWRONGBMXCHUNKTYPE,
    SCERRGWWRONGARGS,
    SCERRGWCANTCREATELOGDIR,
    SCERRGWCANTLOADXMLSETTINGS,
    SCERRGWFAILEDASSERTCORRECTSTATE,
    SCERRGWPATHTOIPCMONITORDIR,
    SCERRGWACTIVEDBLISTENPROBLEM,
    SCERRGWHTTPSPROCESSFAILED,
    SCERRGWWORKERISDEAD,
    SCERRGWDATABASEMONITORISDEAD,
    SCERRGWCONNECTEXFAILED,
    SCERRGWACCEPTEXFAILED,
    SCERRGWWORKERROUTINEFAILED,
    SCERRGWMAXHANDLERSREACHED,
    SCERRGWPORTPROCESSFAILED,
    SCERRGWCANTRELEASETOSHAREDPOOL,
    SCERRGWFAILEDFINDNEXTCHANGENOTIFICATION,
    SCERRGWWRONGMAXIDLESESSIONLIFETIME,
    SCERRGWWRONGDATABASEINDEX,
    SCERRGWCHANNELSEVENTSTHREADISDEAD,
    SCERRGWSESSIONSCLEANUPTHREADISDEAD,
    SCERRGWGATEWAYLOGGINGTHREADISDEAD,
    SCERRGWSOMETHREADDIED,
    SCERRGWOPERATIONONWRONGSOCKET,
    SCERRGWOPERATIONONWRONGSOCKETWHENPUSHING,
    SCERRGWTESTTIMEOUT,
    SCERRGWTESTFAILED,
    SCERRGWTESTFINISHED,
    SCERRGWFAILEDTOBINDPORT,
    SCERRGWFAILEDTOATTACHSOCKETTOIOCP,
    SCERRGWFAILEDTOLISTENONSOCKET,
    SCERRGWIPISNOTONWHITELIST,
    SCERRGWWRONGDBINDEX,
    SCERRGWMAXHTTPHEADERSSIZEREACHED,
    SCERRGWMAXCHUNKSIZEREACHED,
    SCERRGWMAXDATASIZEREACHED
};


// Maximum number of ports the gateway operates with.
const int32_t MAX_PORTS_NUM = 16;

// Maximum number of URIs the gateway operates with.
const int32_t MAX_URIS_NUM = 1024;

// Maximum number of handlers per port.
const int32_t MAX_RAW_HANDLERS_PER_PORT = 256;

// Maximum number of URI handlers per port.
const int32_t MAX_URI_HANDLERS_PER_PORT = 16;

// Maximum number of chunks to pop at once.
const int32_t MAX_CHUNKS_TO_POP_AT_ONCE = 8192;

// Maximum number of gateway chunks.
const int32_t MAX_GATEWAY_CHUNKS = 1024 * 1024;

// Maximum number of fetched OVLs at once.
const int32_t MAX_FETCHED_OVLS = 10;

// Aggregation buffer size.
const int32_t AGGREGATION_BUFFER_SIZE = 1024 * 1024 * 16;

// Size of circular log buffer.
const int32_t GW_LOG_BUFFER_SIZE = 8192 * 32;

// Maximum number of proxied URIs.
const int32_t MAX_PROXIED_URIS = 32;

// Number of sockets to increase the accept roof.
const int32_t ACCEPT_ROOF_STEP_SIZE = 1;

// Offset of data blob in socket data.
const int32_t SOCKET_DATA_OFFSET_BLOB = MixedCodeConstants::SOCKET_DATA_OFFSET_BLOB;

// Size of OVERLAPPED structure.
const int32_t OVERLAPPED_SIZE = sizeof(OVERLAPPED);

// Bad database index.
const db_index_type INVALID_DB_INDEX = -1;

// Bad worker index.
const worker_id_type INVALID_WORKER_INDEX = -1;

// Bad port index.
const int32_t INVALID_PORT_INDEX = -1;

// Bad index.
const int32_t INVALID_INDEX = -1;

// Invalid socket index.
const session_index_type INVALID_SOCKET_INDEX = ~0;

// Bad port number.
const int32_t INVALID_PORT_NUMBER = 0;

// Bad URI index.
const uri_index_type INVALID_URI_INDEX = -1;

// Invalid parameter index in user delegate.
const uint8_t INVALID_PARAMETER_INDEX = 255;

// Bad chunk index.
const core::chunk_index INVALID_CHUNK_INDEX = MixedCodeConstants::INVALID_CHUNK_INDEX;

// Bad linear session index.
const session_index_type INVALID_SESSION_INDEX = ~0;

// Bad view model index.
const session_index_type INVALID_VIEW_MODEL_INDEX = ~0;

// Bad scheduler index.
const scheduler_id_type INVALID_SCHEDULER_ID = 255;

// Bad session salt.
const random_salt_type INVALID_SESSION_SALT = 0;

// Bad Apps session salt.
const random_salt_type INVALID_APPS_SESSION_SALT = 0;

// Invalid Apps unique number.
const uint64_t INVALID_APPS_UNIQUE_SESSION_NUMBER = ~(uint64_t)0;

// Bad unique database number.
const random_salt_type INVALID_UNIQUE_DB_NUMBER = 0;

// Maximum number of chunks to keep in private chunk pool
// until we release them to shared chunk pool.
const int32_t MAX_CHUNKS_IN_PRIVATE_POOL = 128;
const int32_t MAX_CHUNKS_IN_PRIVATE_POOL_DOUBLE = MAX_CHUNKS_IN_PRIVATE_POOL * 2;

// Size of local/remove address structure.
const int32_t SOCKADDR_SIZE_EXT = sizeof(sockaddr_in) + 16;

// Maximum number of active databases.
const int32_t MAX_ACTIVE_DATABASES = 16;

// Maximum number of workers.
const int32_t MAX_WORKER_THREADS = 32;

// Maximum number of active server ports.
const int32_t MAX_ACTIVE_SERVER_PORTS = 32;

// Maximum port handle integer.
const int32_t MAX_POSSIBLE_CONNECTIONS = 10000000;

// Maximum number of test echoes.
const int32_t MAX_TEST_ECHOES = 50000000;

// Number of seconds monitor thread sleeps between checks.
const int32_t GW_MONITOR_THREAD_TIMEOUT_SECONDS = 5;

// Maximum reusable connect sockets per worker.
const int32_t MAX_REUSABLE_CONNECT_SOCKETS_PER_WORKER = 10000;

// Maximum blacklisted IPs per worker.
const int32_t MAX_BLACK_LIST_IPS_PER_WORKER = 10000;

// Socket life time multiplier.
const int32_t SOCKET_LIFETIME_MULTIPLIER = 3;

// First port number used for binding.
const uint16_t FIRST_BIND_PORT_NUM = 1500;

// Maximum length of gateway statistics string.
const int32_t MAX_STATS_LENGTH = 1024 * 64;

// Gateway mode.
enum GatewayTestingMode
{
    // NOTE: HTTP echo requests should come first!
    // They serve as direct array indexes.
    MODE_GATEWAY_HTTP = 0,
    MODE_GATEWAY_SMC_HTTP = 1,
    MODE_GATEWAY_SMC_APPS_HTTP = 2,

    MODE_GATEWAY_RAW = 3,
    MODE_GATEWAY_SMC_RAW = 4,
    MODE_GATEWAY_SMC_APPS_RAW = 5,

    MODE_GATEWAY_UNKNOWN = 6
};

const int32_t NumGatewayChunkSizes = 6;
const int32_t DefaultGatewayChunkSizeType = 1;

const int32_t GatewayChunkSizes[NumGatewayChunkSizes] = {
    512,
    2 * 1024, // Default chunk size.
    8 * 1024,
    32 * 1024,
    128 * 1024,
    512 * 1024
};

const int32_t GatewayChunkStoresSizes[NumGatewayChunkSizes] = {
    100000,
    500000, // Default chunk size.
    100000,
    50000,
    50000,
    1000
};

const int32_t GatewayChunkDataSizes[NumGatewayChunkSizes] = {
    GatewayChunkSizes[0] - SOCKET_DATA_OFFSET_BLOB,
    GatewayChunkSizes[1] - SOCKET_DATA_OFFSET_BLOB,
    GatewayChunkSizes[2] - SOCKET_DATA_OFFSET_BLOB,
    GatewayChunkSizes[3] - SOCKET_DATA_OFFSET_BLOB,
    GatewayChunkSizes[4] - SOCKET_DATA_OFFSET_BLOB,
    GatewayChunkSizes[5] - SOCKET_DATA_OFFSET_BLOB
};

inline chunk_store_type ObtainGatewayChunkType(int32_t data_size)
{
    for (int32_t i = 0; i < NumGatewayChunkSizes; i++)
        if (data_size <= GatewayChunkDataSizes[i])
            return i;

    GW_ASSERT(false);
    return INVALID_INDEX;
}

struct AggregationStruct
{
    random_salt_type unique_socket_id_;
    int32_t size_bytes_;
    session_index_type socket_info_index_;
    int32_t unique_aggr_index_;
    uint16_t port_number_;
    uint8_t flags;
};

const int32_t AggregationStructSizeBytes = sizeof(AggregationStruct);

const int32_t NumGatewayModes = 7;
const char* const GatewayTestingModeStrings[NumGatewayModes] = 
{
    "MODE_GATEWAY_HTTP",
    "MODE_GATEWAY_SMC_HTTP",
    "MODE_GATEWAY_SMC_APPS_HTTP",

    "MODE_GATEWAY_RAW",
    "MODE_GATEWAY_SMC_RAW",
    "MODE_GATEWAY_SMC_APPS_RAW",

    "MODE_GATEWAY_UNKNOWN"
};

inline GatewayTestingMode GetGatewayTestingMode(std::string modeString)
{
    for (int32_t i = 0; i < NumGatewayModes; i++)
    {
        if (modeString == GatewayTestingModeStrings[i])
            return (GatewayTestingMode)i;
    }
    return GatewayTestingMode::MODE_GATEWAY_UNKNOWN;
}

struct HttpTestInformation
{
    const char* const method_and_uri_info;
    int32_t method_and_uri_info_len;

    const char* const http_request_str;
    int32_t http_request_len;

    int32_t http_request_insert_point;
};

const int32_t kNumTestHttpEchoRequests = 3;

// User data offset in blobs for different protocols.
const int32_t HTTP_BLOB_USER_DATA_OFFSET = 0;
const int32_t HTTPS_BLOB_USER_DATA_OFFSET = 2048;
const int32_t RAW_BLOB_USER_DATA_OFFSET = 0;
const int32_t AGGR_BLOB_USER_DATA_OFFSET = 64;
const int32_t SUBPORT_BLOB_USER_DATA_OFFSET = 32;
const int32_t WS_MAX_FRAME_INFO_SIZE = 16;

// Error code type.
#define GW_ERR_CHECK(err_code) if (0 != err_code) return err_code

// Printing prefixes.
#define GW_PRINT_WORKER GW_COUT << "[" << (int32_t)worker_id_ << "]: "
#define GW_PRINT_WORKER_DB GW_COUT << "[" << (int32_t)worker_id_ << "][" << (int32_t)db_index_ << "]: "
#define GW_PRINT_GLOBAL GW_COUT << "Global: "

// Gateway program name.
const wchar_t* const GW_PROGRAM_NAME = L"scnetworkgateway";
const char* const GW_PROCESS_NAME = "networkgateway";
const wchar_t* const GW_DEFAULT_CONFIG_NAME = L"scnetworkgateway.xml";

// Type of operation on the socket.
enum SocketOperType
{
    // Active connections statistics.
    RECEIVE_SOCKET_OPER,
    DISCONNECT_SOCKET_OPER,

    // Non-active connections.
    ACCEPT_SOCKET_OPER,
    SEND_SOCKET_OPER,
    CONNECT_SOCKET_OPER,
    UNKNOWN_SOCKET_OPER
};

class SocketDataChunk;

// Defining reference type for socket data chunk.
typedef SocketDataChunk*& SocketDataChunkRef;

class GatewayWorker;
class HandlersList;

typedef uint32_t (*GENERIC_HANDLER_CALLBACK) (
    HandlersList* hl,
    GatewayWorker *gw,
    SocketDataChunkRef sd,
    BMX_HANDLER_TYPE handler_info,
    bool* is_handled);

#ifdef GW_LOOPED_TEST_MODE

// Looped processors.
typedef uint32_t (*ECHO_REQUEST_CREATOR) (char* buf, echo_id_type echo_id, uint32_t* num_request_bytes);
typedef uint32_t (*ECHO_RESPONSE_PROCESSOR) (char* buf, uint32_t buf_len, echo_id_type* echo_id);

extern uint32_t DefaultHttpEchoRequestCreator(char* buf, echo_id_type echo_id, uint32_t* num_request_bytes);
extern uint32_t DefaultHttpEchoResponseProcessor(char* buf, uint32_t buf_len, echo_id_type* echo_id);

extern uint32_t DefaultRawEchoRequestCreator(char* buf, echo_id_type echo_id, uint32_t* num_request_bytes);
extern uint32_t DefaultRawEchoResponseProcessor(char* buf, uint32_t buf_len, echo_id_type* echo_id);

#endif

uint32_t OuterPortProcessData(
    HandlersList* hl,
    GatewayWorker *gw,
    SocketDataChunkRef sd,
    BMX_HANDLER_TYPE handler_info,
    bool* is_handled);

uint32_t AppsPortProcessData(
    HandlersList* hl,
    GatewayWorker *gw,
    SocketDataChunkRef sd,
    BMX_HANDLER_TYPE handler_info,
    bool* is_handled);

#ifdef GW_TESTING_MODE

uint32_t GatewayPortProcessEcho(
    HandlersList* hl,
    GatewayWorker *gw,
    SocketDataChunkRef sd,
    BMX_HANDLER_TYPE handler_info,
    bool* is_handled);

#endif

uint32_t OuterSubportProcessData(
    HandlersList* hl,
    GatewayWorker *gw,
    SocketDataChunkRef sd,
    BMX_HANDLER_TYPE handler_info,
    bool* is_handled);

uint32_t AppsSubportProcessData(
    HandlersList* hl,
    GatewayWorker *gw,
    SocketDataChunkRef sd,
    BMX_HANDLER_TYPE handler_info,
    bool* is_handled);

#ifdef GW_TESTING_MODE

uint32_t GatewaySubportProcessEcho(
    HandlersList* hl,
    GatewayWorker *gw,
    SocketDataChunkRef sd,
    BMX_HANDLER_TYPE handler_info,
    bool* is_handled);

#endif

uint32_t OuterUriProcessData(
    HandlersList* hl,
    GatewayWorker *gw,
    SocketDataChunkRef sd,
    BMX_HANDLER_TYPE handler_info,
    bool* is_handled);

uint32_t AppsUriProcessData(
    HandlersList* hl,
    GatewayWorker *gw,
    SocketDataChunkRef sd,
    BMX_HANDLER_TYPE handler_info,
    bool* is_handled);

#ifdef GW_TESTING_MODE

uint32_t GatewayUriProcessEcho(
    HandlersList* hl,
    GatewayWorker *gw,
    SocketDataChunkRef sd,
    BMX_HANDLER_TYPE handler_info,
    bool* is_handled);

#endif

// HTTP/WebSockets statistics for Gateway.
uint32_t GatewayStatisticsInfo(
    HandlersList* hl,
    GatewayWorker *gw,
    SocketDataChunkRef sd,
    BMX_HANDLER_TYPE handler_info,
    bool* is_handled);

// POST sockets for Gateway.
uint32_t PostSocketResource(
    HandlersList* hl,
    GatewayWorker *gw,
    SocketDataChunkRef sd,
    BMX_HANDLER_TYPE handler_info,
    bool* is_handled);

// DELETE sockets for Gateway.
uint32_t DeleteSocketResource(
    HandlersList* hl,
    GatewayWorker *gw,
    SocketDataChunkRef sd,
    BMX_HANDLER_TYPE handler_info,
    bool* is_handled);

// Aggregation on gateway.
uint32_t PortAggregator(
    HandlersList* hl,
    GatewayWorker *gw,
    SocketDataChunkRef sd,
    BMX_HANDLER_TYPE handler_info,
    bool* is_handled);

uint32_t GatewayUriProcessProxy(
    HandlersList* hl,
    GatewayWorker *gw,
    SocketDataChunkRef sd,
    BMX_HANDLER_TYPE handler_info,
    bool* is_handled);

// Waking up a thread using APC.
void WakeUpThreadUsingAPC(HANDLE thread_handle);

extern std::string GetOperTypeString(SocketOperType typeOfOper);

// Pointers to extended WinSock functions.
extern LPFN_ACCEPTEX AcceptExFunc;
extern LPFN_CONNECTEX ConnectExFunc;
extern LPFN_DISCONNECTEX DisconnectExFunc;

template <class T, uint32_t MaxElems>
class LinearList
{
    T elems_[MaxElems];
    uint32_t num_entries_;

public:

    LinearList()
    {
        num_entries_ = 0;
    }

    int32_t get_num_entries()
    {
        return num_entries_;
    }

    T& operator[](uint32_t index)
    {
        return elems_[index];
    }

    T* GetElemPtr(uint32_t index)
    {
        return elems_ + index;
    }

    bool IsEmpty()
    {
        return (0 == num_entries_);
    }

    void Add(T& new_elem)
    {
        elems_[num_entries_] = new_elem;
        num_entries_++;
    }

    uint32_t AddEmpty()
    {
        num_entries_++;

        return num_entries_;
    }

    void Clear()
    {
        num_entries_ = 0;
    }

    void RemoveByIndex(uint32_t index)
    {
        // Checking if it was not the last handler in the array.
        if (index < (num_entries_ - 1))
        {
            // Shifting all forward handlers.
            for (uint32_t k = index; k < (num_entries_ - 1); ++k)
                elems_[k] = elems_[k + 1];
        }

        // Number of entries decreased by one.
        num_entries_--;
    }

    bool RemoveEntry(T& elem)
    {
        for (uint32_t i = 0; i < num_entries_; i++)
        {
            if (elem == elems_[i])
            {
                // Checking if it was not the last handler in the array.
                if (i < (num_entries_ - 1))
                {
                    // Shifting all forward handlers.
                    for (uint32_t k = i; k < (num_entries_ - 1); ++k)
                        elems_[k] = elems_[k + 1];
                }

                // Number of entries decreased by one.
                num_entries_--;

                return true;
            }
        }

        return false;
    }

    bool Find(T& elem)
    {
        for (uint32_t i = 0; i < num_entries_; i++)
        {
            if (elem == elems_[i])
            {
                return true;
            }
        }

        return false;
    }

    void Sort()
    {
        std::sort(elems_, elems_ + num_entries_);
    }
};

template <class T, uint32_t MaxElems>
class LinearQueue
{
    T elems_[MaxElems];
    uint32_t push_index_;
    uint32_t pop_index_;
    int32_t stripe_length_;

public:

    LinearQueue()
    {
        Clear();
    }

    void Clear()
    {
        push_index_ = 0;
        pop_index_ = 0;
        stripe_length_ = 0;
    }

    void PushBack(T& new_elem)
    {
        elems_[push_index_] = new_elem;
        push_index_++;
        stripe_length_++;
        if (stripe_length_ > MaxElems)
            std::cout << "";

        if (push_index_ == MaxElems)
            push_index_ = 0;
    }

    T& PopBack()
    {
        push_index_--;
        T& ret_value = elems_[push_index_];
        stripe_length_--;
        GW_ASSERT(stripe_length_ >= 0);

        if (push_index_ < 0)
            push_index_ = 0;

        return ret_value;
    }

    T& PopFront()
    {
        T& ret_value = elems_[pop_index_];
        pop_index_++;
        stripe_length_--;
        GW_ASSERT(stripe_length_ >= 0);

        if (pop_index_ == MaxElems)
            pop_index_ = 0;

        return ret_value;
    }

    int32_t get_num_entries()
    {
        return stripe_length_;
    }
};

// Accumulative buffer.
class AccumBuffer
{
    // Available bytes number in chunk.
    uint32_t chunk_num_available_bytes_;

    // Current buffer pointer in chunk.
    uint8_t* chunk_cur_buf_ptr_;

    // Initial data pointer in chunk.
    uint8_t* chunk_orig_buf_ptr_;

    // Original chunk total length.
    uint32_t chunk_orig_buf_len_bytes_;

    // Total number of bytes accumulated.
    uint32_t accumulated_len_bytes_;

    // Desired number of bytes to accumulate.
    uint32_t desired_accum_bytes_;

public:

    uint32_t* get_desired_accum_bytes_addr()
    {
        return &desired_accum_bytes_;
    }

    uint32_t* get_chunk_num_available_bytes_addr()
    {
        return &chunk_num_available_bytes_;
    }

    // Makes accumulative buffer non-usable.
    void Invalidate()
    {
        chunk_cur_buf_ptr_ = NULL;
        chunk_orig_buf_ptr_ = NULL;
    }

    // Moves data in accumulative buffer to top.
    uint8_t* MoveDataToTopAndContinueReceive(uint8_t* cur_data_ptr, int32_t num_copy_bytes)
    {
        // Checking if we have anything to move.
        if ((cur_data_ptr > chunk_orig_buf_ptr_) &&
            (cur_data_ptr + num_copy_bytes <= chunk_orig_buf_ptr_ + chunk_orig_buf_len_bytes_))
        {
            memmove(chunk_orig_buf_ptr_, cur_data_ptr, num_copy_bytes);
            chunk_cur_buf_ptr_ = chunk_orig_buf_ptr_ + num_copy_bytes;

            chunk_num_available_bytes_ = chunk_orig_buf_len_bytes_ - num_copy_bytes;
            accumulated_len_bytes_ = num_copy_bytes;
        }

        return chunk_orig_buf_ptr_;
    }

    // Initializes accumulative buffer.
    void Init(
        uint32_t buf_total_len_bytes,
        uint8_t* orig_buf_ptr,
        bool reset_accum_len)
    {
        chunk_orig_buf_len_bytes_ = buf_total_len_bytes;
        chunk_num_available_bytes_ = buf_total_len_bytes;
        chunk_orig_buf_ptr_ = orig_buf_ptr;
        chunk_cur_buf_ptr_ = orig_buf_ptr;

        // Checking if we need to reset accumulated length.
        if (reset_accum_len)
        {
            desired_accum_bytes_ = 0;
            accumulated_len_bytes_ = 0;
        }
        else
        {
            uint32_t remaining = desired_accum_bytes_ - accumulated_len_bytes_;
            if (chunk_num_available_bytes_ > remaining)
                chunk_num_available_bytes_ = remaining;
        }
    }

    // Initializes accumulative buffer.
    void Init(
        uint32_t buf_total_len_bytes,
        uint32_t orig_buf_ptr_shift_bytes)
    {
        chunk_orig_buf_len_bytes_ = buf_total_len_bytes;
        chunk_num_available_bytes_ = buf_total_len_bytes;
        chunk_orig_buf_ptr_ = chunk_orig_buf_ptr_ + orig_buf_ptr_shift_bytes;
        chunk_cur_buf_ptr_ = chunk_orig_buf_ptr_;
        desired_accum_bytes_ = buf_total_len_bytes;
        accumulated_len_bytes_ = buf_total_len_bytes;
    }

    // Initializes accumulative buffer.
    void SetAccumulation(
        uint32_t buf_total_len_bytes,
        uint32_t orig_buf_ptr_shift_bytes)
    {
        chunk_orig_buf_len_bytes_ -= orig_buf_ptr_shift_bytes;
        chunk_num_available_bytes_ -= orig_buf_ptr_shift_bytes;
        chunk_orig_buf_ptr_ += orig_buf_ptr_shift_bytes;
        chunk_cur_buf_ptr_ = chunk_orig_buf_ptr_;
        desired_accum_bytes_ = buf_total_len_bytes;
        accumulated_len_bytes_ = buf_total_len_bytes;
    }

    uint32_t get_chunk_num_available_bytes()
    {
        return chunk_num_available_bytes_;
    }

    void set_chunk_num_available_bytes(uint32_t num_bytes)
    {
        chunk_num_available_bytes_ = num_bytes;
    }

    void RevertBeforeSend()
    {
        chunk_num_available_bytes_ = chunk_orig_buf_len_bytes_ - chunk_num_available_bytes_;
    }

    uint32_t get_chunk_orig_buf_len_bytes()
    {
        return chunk_orig_buf_len_bytes_;
    }

    void WriteBytesToSend(void* data, uint32_t data_len)
    {
        memcpy(chunk_orig_buf_ptr_ + chunk_orig_buf_len_bytes_ - chunk_num_available_bytes_, data, data_len);
        chunk_num_available_bytes_ -= data_len;
    }

    // Get buffer length.
    uint32_t GetNumLeftBytesInChunk(uint8_t* cur_ptr)
    {
        return static_cast<uint32_t> (chunk_orig_buf_ptr_ + chunk_orig_buf_len_bytes_ - cur_ptr);
    }

    uint32_t get_desired_accum_bytes()
    {
        return desired_accum_bytes_;
    }

    void set_desired_accum_bytes(uint32_t desired_num_bytes)
    {
        desired_accum_bytes_ = desired_num_bytes;
    }

    // Resets to original state.
    void ResetToOriginalState()
    {
        chunk_cur_buf_ptr_ = chunk_orig_buf_ptr_;
        chunk_num_available_bytes_ = chunk_orig_buf_len_bytes_;
        accumulated_len_bytes_ = 0;
        desired_accum_bytes_ = 0;
    }

    // Adds accumulated bytes.
    void AddAccumulatedBytes(int32_t num_bytes)
    {
        accumulated_len_bytes_ += num_bytes;
        chunk_cur_buf_ptr_ += num_bytes;
        chunk_num_available_bytes_ -= num_bytes;
    }

    // Prepare buffer to send outside.
    void PrepareForSend(uint8_t *data, uint32_t num_bytes)
    {
        chunk_num_available_bytes_ = num_bytes;
        chunk_cur_buf_ptr_ = data;
        accumulated_len_bytes_ = 0;
    }

    // Prepare buffer to proxy outside.
    void PrepareToSendOnProxy()
    {
        chunk_cur_buf_ptr_ = chunk_orig_buf_ptr_;
        chunk_num_available_bytes_ = accumulated_len_bytes_;

        GW_ASSERT_DEBUG(0 == desired_accum_bytes_);
        accumulated_len_bytes_ = 0;
    }

    // Starting accumulation.
    void StartAccumulation(uint32_t total_desired_bytes, uint32_t num_already_accumulated)
    {
        desired_accum_bytes_ = total_desired_bytes;
        accumulated_len_bytes_ = num_already_accumulated;
    }

    // Returns pointer to original data buffer.
    uint8_t* get_chunk_orig_buf_ptr()
    {
        return chunk_orig_buf_ptr_;
    }

    // Returns the size in bytes of accumulated data.
    uint32_t get_accum_len_bytes()
    {
        return accumulated_len_bytes_;
    }

    // Checks if accumulating buffer is filled.
    bool IsBufferFilled()
    {
        return 0 == chunk_num_available_bytes_;
    }

    // Checking if all needed bytes are accumulated.
    bool IsAccumulationComplete()
    {
        return accumulated_len_bytes_ == desired_accum_bytes_;
    }
};

// Represents a session in terms of gateway/Apps.
struct ScSessionStruct
{
    // Unique random number.
    random_salt_type random_salt_;

    // Session linear index.
    session_index_type linear_index_;

    // Scheduler id.
    scheduler_id_type scheduler_id_;

    // Worker id.
    worker_id_type gw_worker_id_;

    // Reset.
    void Reset()
    {
        // NOTE: We don't reset the scheduler id and worker id, since they are used for socket
        // sending data in order, even when session is not created!

        linear_index_ = INVALID_SESSION_INDEX;
        random_salt_ = INVALID_APPS_SESSION_SALT;
    }

    // Constructing session from string.
    void FillFromString(const char* str_in, uint32_t len_bytes)
    {
        GW_ASSERT(MixedCodeConstants::SESSION_STRING_LEN_CHARS == len_bytes);

        random_salt_ = static_cast<random_salt_type> (hex_string_to_uint64(str_in, 16));
        linear_index_ = static_cast<session_index_type> (hex_string_to_uint64(str_in + 16, 6));
        scheduler_id_ = static_cast<scheduler_id_type> (hex_string_to_uint64(str_in + 22, 2));
    }

    // Compare socket stamps of two sessions.
    bool CompareSalts(random_salt_type session_salt)
    {
        if (INVALID_SESSION_INDEX == linear_index_)
            return false;

        return random_salt_ == session_salt;
    }

    // Checks if session is active.
    bool IsActive()
    {
        return (INVALID_SESSION_INDEX != linear_index_);
    }
};

enum SOCKET_FLAGS
{
    SOCKET_FLAGS_AGGREGATED = 1,
    SOCKET_FLAGS_PROXY_CONNECT = 2
};

// Structure that facilitates the socket.
_declspec(align(MEMORY_ALLOCATION_ALIGNMENT)) struct ScSocketInfoStruct
{
    // Entry to lock-free free list.
    SLIST_ENTRY free_socket_indexes_entry_;

    // Main session structure attached to this socket.
    ScSessionStruct session_;

    //////////////////////////////
    //////// 64 bits data ////////
    //////////////////////////////

    // Socket last activity timestamp.
    socket_timestamp_type socket_timestamp_;

    // Unique number for socket.
    random_salt_type unique_socket_id_;

    // Determined handler id.
    BMX_HANDLER_TYPE saved_user_handler_id_;

    // Socket number.
    SOCKET socket_;

    //////////////////////////////
    //////// 32 bits data ////////
    //////////////////////////////

    // Proxy socket identifier.
    session_index_type proxy_socket_info_index_;

    // This socket info index.
    session_index_type read_only_index_;

    // Aggregation socket index.
    session_index_type aggr_socket_info_index_;

    // Number of bytes left for accumulation.
    uint32_t accum_data_bytes_left_;

    //////////////////////////////
    //////// 16 bits data ////////
    //////////////////////////////

    // Port index.
    port_index_type port_index_;

    //////////////////////////////
    //////// 8 bits data /////////
    //////////////////////////////

    // Index to already determined database.
    db_index_type dest_db_index_;

    // Some flags on socket.
    uint8_t flags_;

    // Network protocol flag.
    uint8_t type_of_network_protocol_;

    // Avoiding false sharing between workers.
    uint8_t pad[CACHE_LINE_SIZE];

    bool get_socket_aggregated_flag()
    {
        return (flags_ & SOCKET_FLAGS::SOCKET_FLAGS_AGGREGATED) != 0;
    }

    void set_socket_aggregated_flag()
    {
        flags_ |= SOCKET_FLAGS::SOCKET_FLAGS_AGGREGATED;
    }

    bool get_socket_proxy_connect_flag()
    {
        return (flags_ & SOCKET_FLAGS::SOCKET_FLAGS_PROXY_CONNECT) != 0;
    }

    void set_socket_proxy_connect_flag()
    {
        flags_ |= SOCKET_FLAGS::SOCKET_FLAGS_PROXY_CONNECT;
    }

    ScSocketInfoStruct()
    {
        Reset();
    }

    void ResetTimestamp()
    {
        socket_timestamp_ = 0;
    }

    // Resets the session struct.
    void Reset()
    {
        unique_socket_id_ = INVALID_SESSION_SALT;
        session_.Reset();

        // NOTE: Fields that stays the same for the same socket.
        //socket_ = INVALID_SOCKET;
        //port_index_ = INVALID_PORT_INDEX;

        ResetTimestamp();
        type_of_network_protocol_ = MixedCodeConstants::NetworkProtocolType::PROTOCOL_HTTP1;
        saved_user_handler_id_ = bmx::BMX_INVALID_HANDLER_INFO;
        flags_ = 0;
        dest_db_index_ = INVALID_DB_INDEX;
        proxy_socket_info_index_ = INVALID_SESSION_INDEX;
        aggr_socket_info_index_ = INVALID_SESSION_INDEX;
    }
};

// Represents an active database.
uint32_t __stdcall DatabaseChannelsEventsMonitorRoutine(LPVOID params);
class HandlersTable;
class RegisteredUris;
class ActiveDatabase
{
    // Index of this database in global namespace.
    db_index_type db_index_;

    // Original database name.
    std::string db_name_;

    // Shared memory segment name.
    std::string shm_seg_name_;

    // Unique sequence number.
    volatile uint64_t unique_num_unsafe_;

    // Database handlers.
    HandlersTable* user_handlers_;

    // Channels events monitor thread handle.
    HANDLE channels_events_thread_handle_;

    // Indicates if database is ready to be deleted.
    volatile bool is_empty_;

    // Indicates if database is ready to be cleaned up.
    volatile bool is_ready_for_cleanup_;

    // Number of released workers.
    int32_t num_holding_workers_;

    // Critical section for database checks.
    CRITICAL_SECTION cs_db_checks_;

public:

    // Shared memory segment name.
    const std::string& get_shm_seg_name()
    {
        return shm_seg_name_;
    }

    // Printing the database information.
    void PrintInfo(std::stringstream& global_port_statistics_stream);

    // Releasing worker.
    void ReleaseHoldingWorker()
    {
        num_holding_workers_--;
    }

    // Spawns channels events monitor thread.
    uint32_t SpawnChannelsEventsMonitor()
    {
        uint32_t channelsEventsThreadId;

        // Starting channels events monitor thread.
        channels_events_thread_handle_ = CreateThread(
            NULL, // Default security attributes.
            0, // Use default stack size.
            (LPTHREAD_START_ROUTINE)DatabaseChannelsEventsMonitorRoutine, // Thread function name.
            &db_index_, // Argument to thread function.
            0, // Use default creation flags.
            (LPDWORD)&channelsEventsThreadId); // Returns the thread identifier.

        return (NULL == channels_events_thread_handle_);
    }

    // Kill the channels events monitor.
    void KillChannelsEventsMonitor()
    {
        TerminateThread(channels_events_thread_handle_, 0);
        channels_events_thread_handle_ = NULL;
    }

    // Gets database name.
    const std::string& get_db_name()
    {
        return db_name_;
    }

    // Gets database handlers.
    HandlersTable* get_user_handlers()
    {
        return user_handlers_;
    }

    // Closes all tracked sockets.
    void CloseSocketData();

    // Makes this database slot empty.
    void StartDeletion();

    // Makes this database slot empty.
    void Reset(bool hard_reset);

    // Checks if this database slot empty.
    bool IsEmpty();

    // Checks if this database is ready to be cleaned up by workers (no used chunks, etc).
    bool IsReadyForCleanup();

    // Checks if this database slot emptying was started.
    bool IsDeletionStarted()
    {
        return (INVALID_UNIQUE_DB_NUMBER == unique_num_unsafe_);
    }

    // Active database constructor.
    ActiveDatabase();

    // Initializes this active database slot.
    void Init(std::string db_name, uint64_t unique_num, db_index_type db_index);
};

// Represents an active server port.
class HandlersList;
class SocketDataChunk;
class PortHandlers;
class RegisteredUris;
class RegisteredSubports;
class ServerPort
{
    // Socket.
    SOCKET listening_sock_;

    // Port number, e.g. 80, 443.
    uint16_t port_number_;

    // Statistics.
    volatile int64_t num_accepting_sockets_unsafe_;

#ifdef GW_COLLECT_SOCKET_STATISTICS
    volatile int64_t num_allocated_accept_sockets_unsafe_;
    volatile int64_t num_allocated_connect_sockets_unsafe_;
#endif

    // Ports handler lists.
    PortHandlers* port_handlers_;

    // All registered URIs belonging to this port.
    RegisteredUris* registered_uris_;

    // All registered subports belonging to this port.
    // TODO: Fix full support!
    RegisteredSubports* registered_subports_;

    // This port index in global array.
    int32_t port_index_;

    // Is this an aggregation port.
    bool aggregating_flag_;

public:

    // Sets an aggregating port flag.
    void set_aggregating_flag()
    {
        aggregating_flag_ = true;
    }

    // Gets an aggregating port flag.
    bool get_aggregating_flag()
    {
        return aggregating_flag_;
    }

    // Printing the registered URIs.
    void PrintInfo(std::stringstream& global_port_statistics_stream);

    // Getting registered URIs.
    RegisteredUris* get_registered_uris()
    {
        return registered_uris_;
    }

    // Getting registered port handlers.
    PortHandlers* get_port_handlers()
    {
        return port_handlers_;
    }

    // Getting registered subports.
    RegisteredSubports* get_registered_subports()
    {
        return registered_subports_;
    }

    // Removes this port.
    void EraseDb(db_index_type db_index);

    // Removes this port.
    void Erase();

    // Resets the number of created sockets and active connections.
    void Reset();

    // Checking if port is unused by any database.
    bool IsEmpty();

    // Initializes server socket.
    void Init(int32_t port_index, uint16_t port_number, SOCKET port_socket);

    // Server port.
    ServerPort();

    // Server port.
    ~ServerPort();

    // Getting port listening socket.
    SOCKET get_listening_sock()
    {
        return listening_sock_;
    }

    // Getting port number.
    uint16_t get_port_number()
    {
        return port_number_;
    }

    // Retrieves the number of active connections.
    int64_t NumberOfActiveConnections();

    // Retrieves the number of accepting sockets.
    int64_t get_num_accepting_sockets()
    {
        return num_accepting_sockets_unsafe_;
    }

    // Increments or decrements the number of accepting sockets.
    int64_t ChangeNumAcceptingSockets(int64_t change_value)
    {
#ifdef GW_DETAILED_STATISTICS
        GW_COUT << "ChangeNumAcceptingSockets: " << change_value << " of " << num_accepting_sockets_unsafe_ << GW_ENDL;
#endif

        InterlockedAdd64(&num_accepting_sockets_unsafe_, change_value);
        return num_accepting_sockets_unsafe_;
    }

#ifdef GW_COLLECT_SOCKET_STATISTICS

    // Retrieves the number of allocated accept sockets.
    int64_t get_num_allocated_accept_sockets()
    {
        return num_allocated_accept_sockets_unsafe_;
    }

    // Increments or decrements the number of created accept sockets.
    int64_t ChangeNumAllocatedAcceptSockets(int64_t change_value)
    {
        InterlockedAdd64(&num_allocated_accept_sockets_unsafe_, change_value);
        return num_allocated_accept_sockets_unsafe_;
    }

    // Retrieves the number of allocated connect sockets.
    int64_t get_num_allocated_connect_sockets()
    {
        return num_allocated_connect_sockets_unsafe_;
    }

    // Increments or decrements the number of created connect sockets.
    int64_t ChangeNumAllocatedConnectSockets(int64_t change_value)
    {
        InterlockedAdd64(&num_allocated_connect_sockets_unsafe_, change_value);
        return num_allocated_connect_sockets_unsafe_;
    }

#endif
};

// Information about the reversed proxy.
struct ReverseProxyInfo
{
    // Uri that is being proxied.
    std::string matching_method_and_uri_;
    int32_t matching_method_and_uri_len_;

    // Uri that is being proxied.
    std::string matching_method_and_uri_processed_;
    int32_t matching_method_and_uri_processed_len_;

    // IP address of the destination server.
    std::string destination_ip_;

    // Port of the destination server.
    uint16_t destination_port_;

    // Source port which to used for redirection to proxied service.
    uint16_t sc_proxy_port_;

    // Proxied service address socket info.
    sockaddr_in destination_addr_;
};

class GatewayLogWriter
{
    // Critical section for exclusive writes.
    CRITICAL_SECTION write_lock_;

    // Circular buffer for log entries.
    char log_buf_[GW_LOG_BUFFER_SIZE];

    // Current write position.
    int32_t log_write_pos_;

    // Current log read position.
    int32_t log_read_pos_;

    // Log file handle.
    HANDLE log_file_handle_;

    // Length of accumulated but not dumped data.
    int32_t accum_length_;

public:

    void Init(const std::wstring& log_file_path);

    ~GatewayLogWriter()
    {
        DeleteCriticalSection(&write_lock_);

        CloseHandle(log_file_handle_);
    }

#ifdef GW_LOGGING_ON

    // Writes given string to log buffer.
    void WriteToLog(const char* text, int32_t text_len);

    // Dump accumulated logs in buffer to file.
    void DumpToLogFile();

#endif
};

typedef void* (*GwClangCompileCodeAndGetFuntion)(
    void** clang_engine,
    const char* code_str,
    const char* func_name,
    bool accumulate_old_modules);

typedef void (*ClangDestroyEngineType) (
    void* clang_engine
    );

class CodegenUriMatcher;
class GatewayWorker;
class Gateway
{
    ////////////////////////
    // CONSTANTS
    ////////////////////////

    // The size of the array to hold the server name and active databases updated event name,
    // including terminating null. The format is:
    // "Local\<server_name>_ipc_monitor_active_databases_updated_event"
    static const std::size_t active_databases_updated_event_name_size = 256;

    ////////////////////////
    // SETTINGS
    ////////////////////////

    // Maximum total number of sockets aka connections.
    uint32_t setting_max_connections_;

    // Maximum receive content length size in bytes.
    uint32_t setting_maximum_receive_content_length_;

    // Starcounter server type upper case.
    std::string setting_sc_server_type_upper_;

    // Gateway log file name.
    std::wstring setting_server_output_dir_;
    std::wstring setting_gateway_output_dir_;
    std::wstring setting_log_file_path_;
    std::wstring setting_sc_bin_dir_;

    // Gateway config file name.
    std::wstring setting_config_file_path_;

    // Number of worker threads.
    int32_t setting_num_workers_;

    // Gateway statistics port.
    uint16_t setting_gw_stats_port_;

    // Gateway aggregation port.
    uint16_t setting_aggregation_port_;

    // Inactive socket timeout in seconds.
    int32_t setting_inactive_socket_timeout_seconds_;
    int32_t min_inactive_socket_life_seconds_;

    // Local network interfaces to bind on.
    std::vector<std::string> setting_local_interfaces_;

#ifdef GW_TESTING_MODE

    // Master node IP address.
    std::string setting_master_ip_;

    // Indicates if this node is a master node.
    bool setting_is_master_;

    // Number of connections to make to master node.
    int32_t setting_num_connections_to_master_;

    // Number of connections to make to master node per worker.
    int32_t setting_num_connections_to_master_per_worker_;

    // Number of tracked echoes to master.
    int32_t setting_num_echoes_to_master_;

    // Server test port.
    uint16_t setting_server_test_port_;

    // Gateway operational mode.
    GatewayTestingMode setting_mode_;

    // Maximum running time for tests.
    int32_t setting_max_test_time_seconds_;

    // Name of the statistics.
    std::string setting_stats_name_;

    int32_t cmd_setting_num_workers_;
    GatewayTestingMode cmd_setting_mode_;
    int32_t cmd_setting_num_connections_to_master_;
    int32_t cmd_setting_num_echoes_to_master_;
    int32_t cmd_setting_max_test_time_seconds_;
    std::string cmd_setting_stats_name_;

#endif

    ////////////////////////
    // ACTIVE DATABASES
    ////////////////////////

    // List of active databases.
    ActiveDatabase active_databases_[MAX_ACTIVE_DATABASES];

    // Indicates what databases went down.
    bool db_did_go_down_[MAX_ACTIVE_DATABASES];

    // Current number of database slots.
    int32_t num_dbs_slots_;

    // Unique database sequence number.
    uint64_t db_seq_num_;

    // Event to wait for active databases update.
    HANDLE active_databases_updates_event_;

    // Monitor shared interface.
    core::monitor_interface_ptr shm_monitor_interface_;

    // Gateway pid.
    core::pid_type gateway_pid_;

    // Gateway owner id.
    core::owner_id gateway_owner_id_;

    // Shared memory monitor interface name.
    std::string shm_monitor_int_name_;

    ////////////////////////
    // WORKERS
    ////////////////////////

    // All worker structures.
    GatewayWorker* gw_workers_;

    // Worker thread handles.
    HANDLE* worker_thread_handles_;

    // Active databases monitor thread handle.
    HANDLE db_monitor_thread_handle_;

    // Channels events monitor thread handle.
    HANDLE channels_events_thread_handle_;

    // Dead sockets cleanup thread handle.
    HANDLE dead_sockets_cleanup_thread_handle_;

    // Gateway logging thread handle.
    HANDLE gateway_logging_thread_handle_;

    // All threads monitor thread handle.
    HANDLE all_threads_monitor_handle_;

    ////////////////////////
    // SOCKETS INFOS
    ////////////////////////

    // All sockets information.
    ScSocketInfoStruct* all_sockets_infos_unsafe_;

    // Free socket indexes.
    PSLIST_HEADER free_socket_indexes_unsafe_;

    // Number of active sockets.
    volatile int64_t num_active_sockets_;

    // Global timer to keep track on old connections.
    volatile socket_timestamp_type global_timer_unsafe_;

    // Sockets to cleanup.
    session_index_type* sockets_to_cleanup_unsafe_;
    volatile int64_t num_sockets_to_cleanup_unsafe_;

    // Critical section for sockets cleanup.
    CRITICAL_SECTION cs_sockets_cleanup_;

    ////////////////////////
    // GLOBAL LOCKING
    ////////////////////////

    // Critical section on global lock.
    CRITICAL_SECTION cs_global_lock_;

    // Global lock.
    volatile bool global_lock_unsafe_;

    ////////////////////////
    // OTHER STUFF
    ////////////////////////

    // Unique linear socket id.
    random_salt_type unique_socket_id_;

    // Handle to Starcounter log.
    MixedCodeConstants::server_log_handle_type sc_log_handle_;

    // Specific gateway log writer.
    GatewayLogWriter gw_log_writer_;

#ifdef GW_TESTING_MODE

    // Confirmed HTTP requests map.
    bool confirmed_echoes_shared_[MAX_TEST_ECHOES];

    // Starts measured test.
    bool started_measured_test_;
    volatile bool finished_measured_test_;

    // Time when test started.
    uint64_t test_begin_time_;

    // Number of confirmed echoes.
    volatile int64_t num_confirmed_echoes_unsafe_;

    // Critical section for test finish.
    CRITICAL_SECTION cs_test_finished_;

    // Current echo number.
    volatile int64_t current_echo_number_unsafe_;

    // Number of operations per second.
    int64_t num_gateway_ops_per_second_;

    // Number of measures.
    int64_t num_ops_measures_;

    // Predefined HTTP requests for tests.
    HttpTestInformation* http_tests_information_;

#ifdef GW_LOOPED_TEST_MODE

    // Looped echo processors.
    ECHO_RESPONSE_PROCESSOR looped_echo_response_processor_;

    ECHO_REQUEST_CREATOR looped_echo_request_creator_;

#endif

#endif

    // Gateway handlers.
    HandlersTable* gw_handlers_;

    // All server ports.
    ServerPort server_ports_[MAX_ACTIVE_SERVER_PORTS];

    // Number of used server ports slots.
    volatile int32_t num_server_ports_slots_;

    // Number of processed HTTP requests.
    volatile int64_t num_processed_http_requests_unsafe_;

    // The socket address of the server.
    sockaddr_in* server_addr_;

    // List of proxied servers.
    ReverseProxyInfo reverse_proxies_[MAX_PROXIED_URIS];
    int32_t num_reversed_proxies_;

    // White list with allowed IP-addresses.
    LinearList<ip_info_type, MAX_BLACK_LIST_IPS_PER_WORKER> white_ips_list_;

    // Global IOCP handle.
    HANDLE iocp_;

    // Last bound port number.
    volatile int64_t last_bind_port_num_unsafe_;

    // Last bound interface number.
    volatile int64_t last_bind_interface_num_unsafe_;

    // Current global statistics stream.
    std::stringstream global_statistics_stream_;
    std::stringstream global_port_statistics_stream_;
    std::stringstream global_databases_statistics_stream_;
    std::stringstream global_workers_statistics_stream_;
    char global_statistics_string_[MAX_STATS_LENGTH + 1];

    // Critical section for statistics.
    CRITICAL_SECTION cs_statistics_;

    // Codegen URI matcher.
    CodegenUriMatcher* codegen_uri_matcher_;

public:

    int64_t num_pending_sends_;
    int64_t num_aggregated_sent_messages_;
    int64_t num_aggregated_recv_messages_;
    int64_t num_aggregated_send_queued_messages_;

    // Gets aggregation port number.
    uint16_t setting_aggregation_port()
    {
        return setting_aggregation_port_;
    }

    // Gets free socket index.
    session_index_type ObtainFreeSocketIndex(
        GatewayWorker* gw,
        SOCKET s,
        int32_t port_index,
        bool proxy_connect_socket);

    // Releases used socket index.
    void ReleaseSocketIndex(GatewayWorker* gw, session_index_type index);

    // Checks if IP is on white list.
    bool CheckIpForWhiteList(ip_info_type ip)
    {
        // Checking if white IPs list is empty.
        if (white_ips_list_.IsEmpty())
            return true;

        // TODO: Optimize check to be a switch statement.
        for (int32_t i = 0; i < white_ips_list_.get_num_entries(); i++)
        {
            if (ip == white_ips_list_[i])
                return true;
        }

        return false;
    }

    // Printing statistics for all ports.
    void PrintPortStatistics(std::stringstream& stats_stream);

    // Printing statistics for all databases.
    void PrintDatabaseStatistics(std::stringstream& stats_stream);

    // Printing statistics for all workers.
    void PrintWorkersStatistics(std::stringstream& stats_stream);

    // Handle to Starcounter log.
    MixedCodeConstants::server_log_handle_type get_sc_log_handle()
    {
        return sc_log_handle_;
    }

    // Constant reference to monitor interface.
    const core::monitor_interface_ptr& the_monitor_interface() const {
        return shm_monitor_interface_;
    }

    // Get a reference to the active_databases_updates_event_.
	HANDLE& active_databases_updates_event() {
		return active_databases_updates_event_;
	}

    // Pointer to Clang compile and get function pointer.
    GwClangCompileCodeAndGetFuntion ClangCompileAndGetFunc;

    // Destroys existing Clang engine.
    ClangDestroyEngineType ClangDestroyEngineFunc;

    // Generate the code using managed generator.
    uint32_t GenerateUriMatcher(RegisteredUris* port_uris);

    // Codegen URI matcher.
    CodegenUriMatcher* get_codegen_uri_matcher()
    {
        return codegen_uri_matcher_;
    }

    // Checks if port for this socket is aggregating.
    bool IsAggregatingPort(session_index_type socket_index)
    {
        return server_ports_[all_sockets_infos_unsafe_[socket_index].port_index_].get_aggregating_flag();
    }

    // Checking if unique socket number is correct.
    bool CompareUniqueSocketId(session_index_type socket_index, random_salt_type unique_socket_id)
    {
        GW_ASSERT_DEBUG(socket_index < setting_max_connections_);

        return (all_sockets_infos_unsafe_[socket_index].unique_socket_id_ == unique_socket_id);
    }

    // Get type of network protocol for this socket.
    MixedCodeConstants::NetworkProtocolType GetTypeOfNetworkProtocol(session_index_type socket_index)
    {
        GW_ASSERT_DEBUG(socket_index < setting_max_connections_);

        return (MixedCodeConstants::NetworkProtocolType) all_sockets_infos_unsafe_[socket_index].type_of_network_protocol_;
    }

    // Setting client IP address info.
    void SetSavedUserHandlerId(session_index_type socket_index, BMX_HANDLER_TYPE saved_user_handler_id)
    {
        GW_ASSERT_DEBUG(socket_index < setting_max_connections_);

        all_sockets_infos_unsafe_[socket_index].saved_user_handler_id_ = saved_user_handler_id;
    }

    // Getting client IP address info.
    BMX_HANDLER_TYPE GetSavedUserHandlerId(session_index_type socket_index)
    {
        GW_ASSERT_DEBUG(socket_index < setting_max_connections_);

        return all_sockets_infos_unsafe_[socket_index].saved_user_handler_id_;
    }

    // Resets socket info when socket is disconnected.
    void ResetSocketInfoOnDisconnect(session_index_type socket_index)
    {
        GW_ASSERT_DEBUG(socket_index < setting_max_connections_);

        all_sockets_infos_unsafe_[socket_index].Reset();
    }

    // Getting destination database index.
    db_index_type GetDestDbIndex(session_index_type socket_index)
    {
        GW_ASSERT_DEBUG(socket_index < setting_max_connections_);

        return all_sockets_infos_unsafe_[socket_index].dest_db_index_;
    }

    // Setting destination database index.
    void SetDestDbIndex(session_index_type socket_index, db_index_type db_index)
    {
        GW_ASSERT_DEBUG(socket_index < setting_max_connections_);

        all_sockets_infos_unsafe_[socket_index].dest_db_index_ = db_index;
    }

    // Getting socket id.
    int32_t GetPortIndex(session_index_type socket_index)
    {
        GW_ASSERT_DEBUG(socket_index < setting_max_connections_);

        return all_sockets_infos_unsafe_[socket_index].port_index_;
    }

    // Getting worker id.
    worker_id_type GetBoundWorkerId(session_index_type socket_index)
    {
        GW_ASSERT_DEBUG(socket_index < setting_max_connections_);

        return all_sockets_infos_unsafe_[socket_index].session_.gw_worker_id_;
    }

    // Getting scheduler id.
    int32_t GetSchedulerId(session_index_type socket_index)
    {
        GW_ASSERT_DEBUG(socket_index < setting_max_connections_);

        return all_sockets_infos_unsafe_[socket_index].session_.scheduler_id_;
    }

    // Getting socket index.
    SOCKET GetSocket(session_index_type socket_index)
    {
        GW_ASSERT_DEBUG(socket_index < setting_max_connections_);

        return all_sockets_infos_unsafe_[socket_index].socket_;
    }

    // Checks for proxy socket.
    bool HasProxySocket(session_index_type socket_index)
    {
        GW_ASSERT_DEBUG(socket_index < setting_max_connections_);

        return INVALID_SESSION_INDEX != all_sockets_infos_unsafe_[socket_index].proxy_socket_info_index_;
    }

    // Checks for proxy connect socket flag.
    bool IsProxyConnectSocket(session_index_type socket_index)
    {
        GW_ASSERT_DEBUG(socket_index < setting_max_connections_);

        return all_sockets_infos_unsafe_[socket_index].get_socket_proxy_connect_flag();
    }

    // Getting aggregation socket index.
    session_index_type GetAggregationSocketIndex(session_index_type socket_index)
    {
        GW_ASSERT_DEBUG(socket_index < setting_max_connections_);

        return all_sockets_infos_unsafe_[socket_index].aggr_socket_info_index_;
    }

    // Setting aggregation socket index.
    void SetAggregationSocketIndex(session_index_type socket_index, session_index_type aggr_socket_info_index)
    {
        GW_ASSERT_DEBUG(socket_index < setting_max_connections_);

        all_sockets_infos_unsafe_[socket_index].aggr_socket_info_index_ = aggr_socket_info_index;
    }

    // Getting aggregated socket flag.
    bool GetSocketAggregatedFlag(session_index_type socket_index)
    {
        GW_ASSERT_DEBUG(socket_index < setting_max_connections_);

        return all_sockets_infos_unsafe_[socket_index].get_socket_aggregated_flag();
    }

    // Setting aggregated socket flag.
    void SetSocketAggregatedFlag(session_index_type socket_index)
    {
        GW_ASSERT_DEBUG(socket_index < setting_max_connections_);

        all_sockets_infos_unsafe_[socket_index].set_socket_aggregated_flag();
    }

    session_index_type GetProxySocketIndex(session_index_type socket_index)
    {
        GW_ASSERT_DEBUG(socket_index < setting_max_connections_);

        return all_sockets_infos_unsafe_[socket_index].proxy_socket_info_index_;
    }

    void SetProxySocketIndex(session_index_type socket_index, session_index_type proxy_socket_index)
    {
        GW_ASSERT_DEBUG(socket_index < setting_max_connections_);

        all_sockets_infos_unsafe_[socket_index].proxy_socket_info_index_ = proxy_socket_index;
    }

    session_index_type GetAccumulatedBytesLeft(session_index_type socket_index)
    {
        GW_ASSERT_DEBUG(socket_index < setting_max_connections_);

        return all_sockets_infos_unsafe_[socket_index].accum_data_bytes_left_;
    }

    void SetAccumulatedBytesLeft(session_index_type socket_index, uint32_t num_bytes)
    {
        GW_ASSERT_DEBUG(socket_index < setting_max_connections_);

        all_sockets_infos_unsafe_[socket_index].accum_data_bytes_left_ = num_bytes;
    }

    void DecrementAccumulatedBytesLeft(session_index_type socket_index, uint32_t decr_bytes)
    {
        GW_ASSERT_DEBUG(socket_index < setting_max_connections_);

        all_sockets_infos_unsafe_[socket_index].accum_data_bytes_left_ -= decr_bytes;
    }

    // Applying session parameters to socket data.
    bool ApplySocketInfoToSocketData(SocketDataChunkRef sd, session_index_type socket_index, random_salt_type unique_socket_id);

    // Creates new socket info.
    void CreateNewSocketInfo(session_index_type socket_index, int32_t port_index, worker_id_type worker_id)
    {
        all_sockets_infos_unsafe_[socket_index].port_index_ = port_index;
        all_sockets_infos_unsafe_[socket_index].session_.gw_worker_id_ = worker_id;
    }

    // Setting new unique socket number.
    random_salt_type GenerateUniqueSocketInfoIds(session_index_type socket_index, scheduler_id_type scheduler_id)
    {
        GW_ASSERT(socket_index < setting_max_connections_);

        random_salt_type unique_id = get_unique_socket_id();

        all_sockets_infos_unsafe_[socket_index].unique_socket_id_ = unique_id;
        all_sockets_infos_unsafe_[socket_index].session_.scheduler_id_ = scheduler_id;

#ifdef GW_SOCKET_DIAG
        GW_COUT << "New unique socket id " << socket_index << ":" << unique_id << GW_ENDL;
#endif

        return unique_id;
    }

    // Getting unique socket number.
    random_salt_type GetUniqueSocketId(session_index_type socket_index)
    {
        return all_sockets_infos_unsafe_[socket_index].unique_socket_id_;
    }

    // Unique linear socket id.
    random_salt_type get_unique_socket_id()
    {
        // NOTE: Doing simple increment here.
        return ++unique_socket_id_;
    }

    // Checks that all Gateway threads are alive.
    uint32_t GatewayMonitor();

    // Getting Gateway log writer.
    GatewayLogWriter* get_gw_log_writer()
    {
        return &gw_log_writer_;
    }

    // Full path to gateway log file.
    const std::wstring& setting_log_file_path()
    {
        return setting_log_file_path_;
    }

    // Current global statistics value.
    const char* GetGlobalStatisticsString(int32_t* out_len);

    // Getting the number of used sockets.
    int64_t NumberCreatedSocketsAllWorkers();

    // Getting the number of reusable connect sockets.
    int64_t NumberOfReusableConnectSockets();

    // Getting the number of used sockets per worker.
    int64_t NumberUsedSocketsPerWorker(int32_t worker_id);

    // Last bind port number.
    uint16_t get_last_bind_port_num()
    {
        return static_cast<uint16_t>(last_bind_port_num_unsafe_);
    }

    // Last bind interface number.
    int64_t get_last_bind_interface_num()
    {
        return last_bind_interface_num_unsafe_;
    }

    // Generating new port/interface.
    void GenerateNewBindPortInterfaceNumbers()
    {
        InterlockedAdd64(&last_bind_port_num_unsafe_, 1);

        if (last_bind_port_num_unsafe_ == (0xFFFF - setting_num_workers_))
        {
            // NOTE: The following is done only by one worker.

            // Resetting the port number.
            last_bind_port_num_unsafe_ = FIRST_BIND_PORT_NUM;

            // Checking how we should change the interface number.
            if (last_bind_interface_num_unsafe_ == (setting_local_interfaces_.size() - 1))
                last_bind_interface_num_unsafe_ = 0;
            else
                InterlockedAdd64(&last_bind_interface_num_unsafe_, 1);
        }
    }

    // Getting minimum socket lifetime.
    int32_t get_min_inactive_socket_life_seconds()
    {
        return min_inactive_socket_life_seconds_;
    }

    // Returns instance of proxied server.
    ReverseProxyInfo* GetProxiedServerAddress(int32_t index)
    {
        return reverse_proxies_ + index;
    }

    // Adds some URI handler: either Apps or Gateway.
    uint32_t AddUriHandler(
        GatewayWorker *gw,
        HandlersTable* handlers_table,
        uint16_t port,
        const char* original_uri_info,
        uint32_t original_uri_info_len_chars,
        const char* processed_uri_info,
        uint32_t processed_uri_info_len_chars,
        uint8_t* param_types,
        int32_t num_params,
        BMX_HANDLER_TYPE user_handler_id,
        db_index_type db_index,
        GENERIC_HANDLER_CALLBACK handler_proc,
        bool is_gateway_handler = false,
        ReverseProxyInfo* reverse_proxy_info = NULL);

    // Adds some port handler: either Apps or Gateway.
    uint32_t AddPortHandler(
        GatewayWorker *gw,
        HandlersTable* handlers_table,
        uint16_t port,
        BMX_HANDLER_TYPE handler_info,
        db_index_type db_index,
        GENERIC_HANDLER_CALLBACK handler_proc);

    // Adds some sub-port handler: either Apps or Gateway.
    uint32_t AddSubPortHandler(
        GatewayWorker *gw,
        HandlersTable* handlers_table,
        uint16_t port,
        bmx::BMX_SUBPORT_TYPE subport,
        BMX_HANDLER_TYPE handler_info,
        db_index_type db_index,
        GENERIC_HANDLER_CALLBACK handler_proc);

#ifdef GW_TESTING_MODE

    // Number of connections to make to master node.
    int32_t setting_num_connections_to_master()
    {
        return setting_num_connections_to_master_;
    }

    // Number of connections to make to master node per worker.
    int32_t setting_num_connections_to_master_per_worker()
    {
        return setting_num_connections_to_master_per_worker_;
    }

    // Number of tracked echoes to master.
    int32_t setting_num_echoes_to_master()
    {
        return setting_num_echoes_to_master_;
    }

    // Server test port.
    int32_t setting_server_test_port()
    {
        return setting_server_test_port_;
    }

    // Registering confirmed HTTP echo.
    void ConfirmEcho(int64_t echo_num)
    {
        GW_ASSERT(echo_num < setting_num_echoes_to_master_);

        if (false != confirmed_echoes_shared_[echo_num])
        {
            GW_COUT << "Echo index occupied: " << echo_num << GW_ENDL;
            GW_ASSERT(false);
        }

        confirmed_echoes_shared_[echo_num] = true;
        //GW_COUT << "Confirmed: " << echo_num << GW_ENDL;

        InterlockedIncrement64(&num_confirmed_echoes_unsafe_);
    }

    // Getting number of confirmed echoes.
    int64_t get_num_confirmed_echoes()
    {
        return num_confirmed_echoes_unsafe_;
    }

    // Checks that echo responses are correct.
    bool CheckConfirmedEchoResponses(GatewayWorker* gw);

    // Starts measured test.
    bool get_started_measured_test()
    {
        return started_measured_test_;
    }

    // Starts measured test.
    void StartMeasuredTest()
    {
        num_confirmed_echoes_unsafe_ = 0;
        started_measured_test_ = true;
        finished_measured_test_ = false;
        test_begin_time_ = timeGetTime();
    }

    // Resetting echo tests.
    void ResetEchoTests()
    {
        num_confirmed_echoes_unsafe_ = 0;
        current_echo_number_unsafe_ = -1;
        started_measured_test_ = false;
        finished_measured_test_ = false;
        test_begin_time_ = 0;

        for (int32_t i = 0; i < setting_num_echoes_to_master_; i++)
            confirmed_echoes_shared_[i] = false;
    }

    // Incrementing and getting next echo number.
    int64_t GetNextEchoNumber()
    {
        return InterlockedIncrement64(&current_echo_number_unsafe_);
    }

    // Checks if all echoes have been sent already.
    bool AllEchoesSent()
    {
        return current_echo_number_unsafe_ >= (setting_num_echoes_to_master_ - 1);
    }

    // Gateway operational mode.
    GatewayTestingMode setting_mode()
    {
        return setting_mode_;
    }

    // Master node IP address.
    std::string setting_master_ip()
    {
        return setting_master_ip_;
    }

    // Is master node?
    bool setting_is_master()
    {
        return setting_is_master_;
    }

    // Getting average number of measured operations.
    int64_t GetAverageOpsPerSecond()
    {
        GW_ASSERT(0 != num_ops_measures_);

        int64_t aver_num_ops = num_gateway_ops_per_second_ / num_ops_measures_;

        num_gateway_ops_per_second_ = 0;
        num_ops_measures_ = 0;

        return aver_num_ops;
    }

    // Calculates number of created connections for all workers.
    int64_t GetNumberOfCreatedConnectionsAllWorkers();

    // Getting number of preparation network events.
    int64_t GetNumberOfPreparationNetworkEventsAllWorkers();

    void InitTestHttpEchoRequests();

    HttpTestInformation* GetHttpTestInformation()
    {
        if (setting_mode_ < kNumTestHttpEchoRequests)
            return http_tests_information_ + setting_mode_;

        return NULL;
    }

    // Generates test HTTP request.
    uint32_t GenerateHttpRequest(char* buf, echo_id_type echo_id)
    {
        // Getting current test HTTP request type.
        HttpTestInformation* test_info = GetHttpTestInformation();

        // Copying HTTP response.
        memcpy(buf, test_info->http_request_str, test_info->http_request_len);

        // Inserting number into HTTP ping request.
        uint64_to_hex_string(echo_id, buf + test_info->http_request_insert_point, 8, false);

        return test_info->http_request_len;
    }

#ifdef GW_LOOPED_TEST_MODE
    // Looped echo processors.
    ECHO_RESPONSE_PROCESSOR get_looped_echo_response_processor()
    {
        return looped_echo_response_processor_;
    }

    ECHO_REQUEST_CREATOR get_looped_echo_request_creator()
    {
        return looped_echo_request_creator_;
    }

#endif

#endif

#ifdef GW_COLLECT_SOCKET_STATISTICS

    // Increments number of processed HTTP requests.
    void IncrementNumProcessedHttpRequests()
    {
        InterlockedAdd64(&num_processed_http_requests_unsafe_, 1);
    }

#endif

    // Get number of processed HTTP requests.
    int64_t get_num_processed_http_requests()
    {
        return num_processed_http_requests_unsafe_;
    }

    // Number of active sockets.
    int64_t get_num_active_sockets()
    {
        return num_active_sockets_;
    }

    // Change number of active sockets.
    void ChangeNumActiveSockets(int64_t value)
    {
        InterlockedAdd64(&num_active_sockets_, value);
    }

    // Collects outdated sockets if any.
    uint32_t CollectInactiveSockets();

    // Gets number of sockets to cleanup.
    int64_t get_num_sockets_to_cleanup_unsafe()
    {
        return num_sockets_to_cleanup_unsafe_;
    }

    // Cleans up all collected inactive sockets.
    uint32_t CleanupInactiveSocketsOnWorkerZero();

    // Updates current global timer value on given socket.
    void UpdateSocketTimeStamp(session_index_type index)
    {
        all_sockets_infos_unsafe_[index].socket_timestamp_ = global_timer_unsafe_;
    }

    // Sets connection type on given socket.
    void SetTypeOfNetworkProtocol(session_index_type index,
        MixedCodeConstants::NetworkProtocolType proto_type)
    {
        all_sockets_infos_unsafe_[index].type_of_network_protocol_ = proto_type;
    }

    // Steps global timer value.
    void step_global_timer_unsafe(int32_t value)
    {
        global_timer_unsafe_ += value;
    }

    // Gateway pid.
    core::pid_type get_gateway_pid()
    {
        return gateway_pid_;
    }

    // Gateway owner id.
    core::owner_id get_gateway_owner_id()
    {
        return gateway_owner_id_;
    }

    // Shared memory monitor interface name.
    const std::string& get_shm_monitor_int_name()
    {
        return shm_monitor_int_name_;
    }

    uint32_t setting_maximum_receive_content_length()
    {
        return setting_maximum_receive_content_length_;
    }

    // Getting settings log file directory.
    const std::wstring& get_setting_server_output_dir()
    {
        return setting_server_output_dir_;
    }

    // Getting gateway output directory.
    const std::wstring& get_setting_gateway_output_dir()
    {
        return setting_gateway_output_dir_;
    }

    // Getting Starcounter bin directory.
    const std::wstring& get_setting_sc_bin_dir()
    {
        return setting_sc_bin_dir_;
    }

    // Starcounter server type upper case.
    const std::string& setting_sc_server_type_upper()
    {
        return setting_sc_server_type_upper_;
    }

    // Getting maximum number of connections.
    uint32_t setting_max_connections()
    {
        return setting_max_connections_;
    }

    // Getting specific worker information.
    GatewayWorker* get_worker(int32_t worker_id);

    // Getting specific worker handle.
    HANDLE get_worker_thread_handle(int32_t worker_id)
    {
        return worker_thread_handles_[worker_id];
    }

    // Getting gateway handlers.
    HandlersTable* get_gw_handlers()
    {
        return gw_handlers_;
    }

    // Checks if certain server port exists.
    ServerPort* FindServerPort(uint16_t port_num)
    {
        for (int32_t i = 0; i < num_server_ports_slots_; i++)
        {
            if (port_num == server_ports_[i].get_port_number())
                return server_ports_ + i;
        }

        return NULL;
    }

    // Checks if certain server port exists.
    int32_t FindServerPortIndex(uint16_t port_num)
    {
        for (int32_t i = 0; i < num_server_ports_slots_; i++)
        {
            if (port_num == server_ports_[i].get_port_number())
                return i;
        }

        return INVALID_PORT_INDEX;
    }

    // Adds new server port.
    ServerPort* AddServerPort(uint16_t port_num, SOCKET listening_sock)
    {
        // Looking for an empty server port slot.
        int32_t empty_slot = 0;
        for (empty_slot = 0; empty_slot < num_server_ports_slots_; ++empty_slot)
        {
            if (server_ports_[empty_slot].IsEmpty())
                break;
        }

        // Initializing server port on this slot.
        server_ports_[empty_slot].Init(empty_slot, port_num, listening_sock);

        // Checking if it was the last slot.
        if (empty_slot >= num_server_ports_slots_)
            num_server_ports_slots_++;

        return server_ports_ + empty_slot;
    }

    // Runs all port handlers.
    uint32_t RunAllHandlers();

    // Delete all handlers associated with given database.
    uint32_t EraseDatabaseFromPorts(db_index_type db_index);

    // Cleans up empty ports.
    void CleanUpEmptyPorts();

    // Get active server ports.
    ServerPort* get_server_port(int32_t port_index)
    {
        return server_ports_ + port_index;
    }

    // Get number of active server ports.
    int32_t get_num_server_ports_slots()
    {
        return num_server_ports_slots_;
    }

    // Gets server address.
    sockaddr_in* get_server_addr()
    {
        return server_addr_;
    }

    // Check and wait for global lock.
    void SuspendWorker(GatewayWorker* gw);

    // Enters global lock and waits for workers.
    void EnterGlobalLock()
    {
        // Checking if already locked.
        while (global_lock_unsafe_);

        // Entering the critical section.
        EnterCriticalSection(&cs_global_lock_);

        // Setting the global lock key.
        global_lock_unsafe_ = true;

        // Waiting until all workers reach the safe point and freeze there.
        WaitAllWorkersSuspended();

        // Now we are sure that all workers are suspended.
    }

    // Waits for all workers to suspend.
    void WaitAllWorkersSuspended();

    // Waking up all workers.
    void WakeUpAllWorkers();

    // Opens active databases events with monitor.
    uint32_t OpenActiveDatabasesUpdatedEvent();

    // Releases global lock.
    void LeaveGlobalLock()
    {
        global_lock_unsafe_ = false;

        // Leaving critical section.
        LeaveCriticalSection(&cs_global_lock_);
    }

    // Gets global lock value.
    bool global_lock()
    {
        return global_lock_unsafe_;
    }

    // Returns active database on this slot index.
    ActiveDatabase* GetDatabase(db_index_type db_index)
    {
        return active_databases_ + db_index;
    }

    // Get number of active databases.
    db_index_type get_num_dbs_slots()
    {
        return num_dbs_slots_;
    }

    // Reading command line arguments.
    uint32_t ProcessArgumentsAndInitLog(int argc, wchar_t* argv[]);

    // Get number of workers.
    int32_t setting_num_workers()
    {
        return setting_num_workers_;
    }

    // Getting the total number of overflow chunks for all databases.
    int64_t NumberOverflowChunksAllWorkersAndDatabases();

    // Getting the number of overflow chunks per database.
    int64_t NumberOverflowChunksPerDatabase(db_index_type db_index);

    // Getting the number of active connections per port.
    int64_t NumberOfActiveConnectionsPerPort(int32_t port_index);

    // Get IOCP.
    HANDLE get_iocp()
    {
        return iocp_;
    }

    // Local network interfaces to bind on.
    std::vector<std::string> setting_local_interfaces()
    {
        return setting_local_interfaces_;
    }

    // Constructor.
    Gateway();

    // Load settings from XML.
    uint32_t LoadSettings(std::wstring configFilePath);

    // Assert some correct state parameters.
    uint32_t AssertCorrectState();

    // Initialize the network gateway.
    uint32_t Init();

    // Checking for database changes.
    uint32_t CheckDatabaseChanges(const std::set<std::string>& active_databases);

    // Print statistics.
    uint32_t StatisticsAndMonitoringRoutine();

    // Safely shutdowns the gateway.
    void ShutdownGateway(GatewayWorker* gw, int32_t exit_code);

    // Creates socket and binds it to server port.
    uint32_t CreateListeningSocketAndBindToPort(GatewayWorker *gw, uint16_t port_num, SOCKET& sock);

    // Creates new connections on all workers.
    uint32_t CreateNewConnectionsAllWorkers(int32_t howMany, uint16_t port_num, db_index_type db_index);

    // Start workers.
    uint32_t StartWorkerAndManagementThreads(
        LPTHREAD_START_ROUTINE workerRoutine,
        LPTHREAD_START_ROUTINE scanDbsRoutine,
        LPTHREAD_START_ROUTINE channelsEventsMonitorRoutine,
        LPTHREAD_START_ROUTINE inactiveSocketsCleanupRoutine,
        LPTHREAD_START_ROUTINE gatewayLoggingRoutine,
        LPTHREAD_START_ROUTINE threadsMonitorRoutine);

    // Cleanup resources.
    uint32_t GlobalCleanup();

    // Main function to start network gateway.
    int32_t StartGateway();

    // Destructor.
    ~Gateway();

    // Opens Starcounter log for writing.
    uint32_t OpenStarcounterLog();

    // Closes Starcounter log.
    void CloseStarcounterLog();

    // Write critical into log.
    void LogWriteCritical(const wchar_t* msg);
    void LogWriteError(const wchar_t* msg);
    void LogWriteWarning(const wchar_t* msg);
    void LogWriteNotice(const wchar_t* msg);
    void LogWriteGeneral(const wchar_t* msg, uint32_t log_type);

    // Checks if global session data is active.
    bool IsGlobalSessionActive(session_index_type index)
    {
        return all_sockets_infos_unsafe_[index].session_.IsActive();
    }

    // Checks if global session data is active.
    bool CompareGlobalSessionSalt(
        session_index_type index,
        random_salt_type random_salt)
    {
        return all_sockets_infos_unsafe_[index].session_.CompareSalts(random_salt);
    }

    // Gets session data by index.
    ScSessionStruct GetGlobalSessionCopy(session_index_type index)
    {
        // Checking validity of linear session index other wise return a wrong copy.
        if (INVALID_SESSION_INDEX == index)
            return ScSessionStruct();

        // Fetching the session by index.
        return all_sockets_infos_unsafe_[index].session_;
    }

    // Gets socket info data by index.
    ScSocketInfoStruct GetGlobalSocketInfoCopy(session_index_type socket_index)
    {
        // Checking validity of linear session index other wise return a wrong copy.
        if (INVALID_SESSION_INDEX == socket_index)
            return ScSocketInfoStruct();

        // Fetching the session by index.
        return all_sockets_infos_unsafe_[socket_index];
    }

    // Gets session data by index.
    void SetGlobalSessionCopy(
        session_index_type index,
        ScSessionStruct session_copy)
    {
        // Fetching the session by index.
        all_sockets_infos_unsafe_[index].session_ = session_copy;
    }

    // Gets session data by index.
    void DeleteGlobalSession(session_index_type index)
    {
        // Fetching the session by index.
        all_sockets_infos_unsafe_[index].session_.Reset();
    }

    // Gets scheduler id for specific session.
    scheduler_id_type GetGlobalSessionSchedulerId(session_index_type index)
    {
        // Checking validity of linear session index other wise return a wrong copy.
        if (INVALID_SESSION_INDEX == index)
            return INVALID_SCHEDULER_ID;

        // Fetching the session by index.
        return all_sockets_infos_unsafe_[index].session_.scheduler_id_;
    }

#ifdef GW_TESTING_MODE
    // Gracefully shutdowns all needed processes after test is finished.
    uint32_t ShutdownTest(GatewayWorker* gw, bool success);
#endif
};

// Globally accessed gateway object.
extern Gateway g_gateway;

} // namespace network
} // namespace starcounter

#endif // GATEWAY_HPP