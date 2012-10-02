#pragma once
#ifndef GATEWAY_HPP
#define GATEWAY_HPP

namespace starcounter {
namespace network {

// Some defines, e.g. debugging, statistics, etc.
//#define GW_GLOBAL_STATISTICS
//#define GW_SOCKET_DIAG
//#define GW_HTTP_DIAG
//#define GW_WEBSOCKET_DIAG
#define GW_ERRORS_DIAG
#define GW_WARNINGS_DIAG
#define GW_GENERAL_DIAG
//#define GW_CHUNKS_DIAG
#define GW_DATABASES_DIAG
//#define GW_SESSIONS_DIAG

// Maximum number of ports the gateway operates with.
const int32_t MAX_PORTS_NUM = 16;

// Maximum number of URIs the gateway operates with.
const int32_t MAX_URIS_NUM = 1024;

// Maximum number of handlers per port.
const int32_t MAX_RAW_HANDLERS_PER_PORT = 256;

// Maximum number of URI handlers per port.
const int32_t MAX_URI_HANDLERS_PER_PORT = 16;

// Length of accepting data structure.
const int32_t ACCEPT_DATA_SIZE_BYTES = 64;

// Maximum number of receive/send WSABUFs.
const int32_t MAX_WSA_BUFS = 32;

// Number of sockets to increase the accept roof.
const int32_t ACCEPT_ROOF_STEP_SIZE = 1;

// Maximum size of BMX header in the beginning of the chunk
// after which the gateway data can be placed.
const int32_t BMX_HEADER_MAX_SIZE_BYTES = 24;

// Maximum size of gateway header in bytes.
const int32_t GATEWAY_HEADER_MAX_SIZE_BYTES = 1024;

// Length of blob data in bytes.
const int32_t DATA_BLOB_SIZE_BYTES = core::chunk_size - BMX_HEADER_MAX_SIZE_BYTES - GATEWAY_HEADER_MAX_SIZE_BYTES;

// Minimum size of response data for HTTP/WebSockets.
const int32_t HTTP_WS_MIN_RESPONSE_SIZE = 512;

// Size of OVERLAPPED structure.
const int32_t OVERLAPPED_SIZE = sizeof(OVERLAPPED);

// Size of chunk index in bytes.
const int32_t CHUNK_INDEX_SIZE = sizeof(core::chunk_index);

// Bad chunk index.
const uint32_t INVALID_CHUNK_INDEX = ~0;

// Bad linear session index.
const int32_t INVALID_SESSION_INDEX = -1;

// Maximum number of chunks to keep in private chunk pool
// until we release them to shared chunk pool.
const int32_t MAX_CHUNKS_IN_PRIVATE_POOL = 2048;

// Number of chunks to leave in private chunk pool after releasing
// rest of the chunks to shared chunk pool.
const int32_t NUM_CHUNKS_TO_LEAVE_IN_PRIVATE_POOL = 256;

// Offset of overlapped data structure inside socket data chunk:
const int32_t OVL_OFFSET_IN_CHUNK = 24;

// Number of predefined gateway port types
const int32_t NUM_PREDEFINED_PORT_TYPES = 5;

// Size of local/remove address structure.
const int32_t SOCKADDR_SIZE_EXT = sizeof(sockaddr_in) + 16;

// Maximum number of active databases.
const int32_t MAX_ACTIVE_DATABASES = 32;

// Maximum number of active server ports.
const int32_t MAX_ACTIVE_SERVER_PORTS = 32;

// Maximum port handle integer.
const int32_t MAX_SOCKET_HANDLE = 100000;

// Session string length in characters.
const int32_t SC_SESSION_STRING_LEN_CHARS = 24;

// User data offset in blobs for different protocols.
const int32_t HTTP_BLOB_USER_DATA_OFFSET = 0;
const int32_t HTTPS_BLOB_USER_DATA_OFFSET = 2048;
const int32_t RAW_BLOB_USER_DATA_OFFSET = 0;
const int32_t AGGR_BLOB_USER_DATA_OFFSET = 64;
const int32_t SUBPORT_BLOB_USER_DATA_OFFSET = 32;
const int32_t WS_BLOB_USER_DATA_OFFSET = 16;

// Offset in bytes for HttpRequest structure.
const int32_t HTTP_REQUEST_OFFSET_BYTES = 184;

// Error code type.
#define GW_ERR_CHECK(err_code) if (0 != err_code) return err_code

// Channel chunk type.
typedef uint32_t channel_chunk;

// Port types.
enum PortType
{
    GENSOCKETS_PORT = 1,
    HTTP_PORT = 2,
    WEBSOCKETS_PORT = 4,
    HTTPS_PORT = 8,
    AGGREGATION_PORT = 16
};

// Type of operation on the socket.
enum SocketOperType
{
    SEND_OPER,
    RECEIVE_OPER,
    ACCEPT_OPER,
    CONNECT_OPER,
    DISCONNECT_OPER,
    TO_DB_OPER,
    FROM_DB_OPER,
    UNKNOWN_OPER
};

class SocketDataChunk;
class GatewayWorker;

typedef uint32_t (*GENERIC_HANDLER_CALLBACK) (
    GatewayWorker *gw,
    SocketDataChunk *sd,
    BMX_HANDLER_TYPE handler_id,
    bool* is_handled);

uint32_t OuterPortProcessData(
    GatewayWorker *gw,
    SocketDataChunk *sd,
    BMX_HANDLER_TYPE handler_id,
    bool* is_handled);

uint32_t PortProcessData(
    GatewayWorker *gw,
    SocketDataChunk *sd,
    BMX_HANDLER_TYPE handler_id,
    bool* is_handled);

uint32_t OuterSubportProcessData(
    GatewayWorker *gw,
    SocketDataChunk *sd,
    BMX_HANDLER_TYPE handler_id,
    bool* is_handled);

uint32_t SubportProcessData(
    GatewayWorker *gw,
    SocketDataChunk *sd,
    BMX_HANDLER_TYPE handler_id,
    bool* is_handled);

uint32_t OuterUriProcessData(
    GatewayWorker *gw,
    SocketDataChunk *sd,
    BMX_HANDLER_TYPE handler_id,
    bool* is_handled);

uint32_t UriProcessData(
    GatewayWorker *gw,
    SocketDataChunk *sd,
    BMX_HANDLER_TYPE handler_id,
    bool* is_handled);

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

    uint32_t get_num_entries()
    {
        return num_entries_;
    }

    T& operator[](uint32_t index)
    {
        return elems_[index];
    }

    T* GetElemRef(uint32_t index)
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

    bool Remove(T& elem)
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

class NewAccumBuffer
{
    // Number of active buffers.
    int32_t num_active_buf;

    // Total length of data.
    int32_t total_data_len;

    // Buffers themselves.
    WSABUF buffs[MAX_WSA_BUFS];
};

// Accumulative buffer.
class AccumBuffer
{
    // Length of the buffer used in the next operation: receive, send.
    ULONG buf_len_bytes_;

    // Current buffer pointer (assuming data accumulation).
    uint8_t* cur_buf_ptr_;

    // Initial data pointer.
    uint8_t* orig_buf_ptr_;

    // Original buffer total length.
    ULONG orig_buf_len_bytes_;

    // Total number of bytes accumulated.
    ULONG accum_len_bytes_;

    // Number of bytes received last time.
    ULONG last_recv_bytes_;

public:

    // Default initializer.
    AccumBuffer()
    {
        buf_len_bytes_ = 0;
        cur_buf_ptr_ = NULL;
        orig_buf_ptr_ = NULL;
        orig_buf_len_bytes_ = 0;
        accum_len_bytes_ = 0;
        last_recv_bytes_ = 0;
    }

    // Initializes accumulative buffer.
    void Init(ULONG bufTotalLenBytes, uint8_t* origBufPtr)
    {
        orig_buf_len_bytes_ = bufTotalLenBytes;
        buf_len_bytes_ = bufTotalLenBytes;
        orig_buf_ptr_ = origBufPtr;
        cur_buf_ptr_ = origBufPtr;
        accum_len_bytes_ = 0;
        last_recv_bytes_ = 0;
    }

    // Retrieves length of the buffer.
    ULONG buf_len_bytes()
    {
        return buf_len_bytes_;
    }

    // Setting the data pointer for the next operation.
    void SetDataPointer(uint8_t *dataPointer)
    {
        cur_buf_ptr_ = dataPointer;
    }

    // Adds accumulated bytes.
    void AddAccumulatedBytes(int32_t numBytes)
    {
        accum_len_bytes_ += numBytes;
    }

    // Preparing socket buffer for the new communication.
    void ResetBufferForNewOperation()
    {
        cur_buf_ptr_ = orig_buf_ptr_;
        accum_len_bytes_ = 0;
        buf_len_bytes_ = orig_buf_len_bytes_;
    }

    // Prepare buffer to send outside.
    void PrepareForSend(uint8_t *userData, ULONG numBytes)
    {
        buf_len_bytes_ = numBytes;
        cur_buf_ptr_ = userData;
        accum_len_bytes_ = 0;
    }

    // Prepare socket to continue receiving.
    void ContinueReceive()
    {
        cur_buf_ptr_ += last_recv_bytes_;
        buf_len_bytes_ -= last_recv_bytes_;
    }

    // Setting the number of bytes retrieved at last receive.
    void SetLastReceivedBytes(ULONG lenBytes)
    {
        last_recv_bytes_ = lenBytes;
    }

    // Returns pointer to original data buffer.
    uint8_t* get_orig_buf_ptr()
    {
        return orig_buf_ptr_;
    }

    // Returns pointer to response data.
    uint8_t* ResponseDataStart()
    {
        return orig_buf_ptr_ + accum_len_bytes_;
    }

    // Returns the size in bytes of accumulated data.
    ULONG get_accum_len_bytes()
    {
        return accum_len_bytes_;
    }
};

struct ScSessionStruct
{
    // Session random salt.
    uint64_t random_salt_;

    // Unique session linear index.
    // Points to the element in sessions linear array.
    uint32_t session_index_;

    // Scheduler ID.
    uint32_t scheduler_id_;

    // Initializes.
    void Init(uint64_t random_salt, uint32_t session_index, uint32_t scheduler_id)
    {
        random_salt_ = random_salt;
        session_index_ = session_index;
        scheduler_id_ = scheduler_id;
    }

    // Comparing two sessions.
    bool IsEqual(uint64_t random_salt, uint32_t session_index)
    {
        return (random_salt_ == random_salt) && (session_index_ == session_index);
    }

    // Comparing two sessions.
    bool IsEqual(ScSessionStruct& session_struct)
    {
        return IsEqual(session_struct.random_salt_, session_struct.session_index_);
    }

    // Converts session to string.
    int32_t ConvertToString(char *str_out)
    {
        // Translating session index.
        int32_t sessionStringLen = uint64_to_hex_string(session_index_, str_out, 8);

        // Translating session random salt.
        sessionStringLen += uint64_to_hex_string(random_salt_, str_out + sessionStringLen, 16);

        return sessionStringLen;
    }

    // Copying one session structure into another.
    void Copy(ScSessionStruct* session_struct)
    {
        random_salt_ = session_struct->random_salt_;
        session_index_ = session_struct->session_index_;
        scheduler_id_ = session_struct->scheduler_id_;
    }
};

// Session related data.
class GatewayWorker;
class SessionData
{
    // Session structure.
    ScSessionStruct session_struct_;

    // Used to track one-to-one relationship between attached socket and user session.
    uint64_t socket_stamp_;

    // Number of visits.
    uint32_t num_visits_;

    // Socket to which this session is attached.
    SOCKET attached_socket_;

public:

    // Getting session structure.
    ScSessionStruct* get_session_struct()
    {
        return &session_struct_;
    }

    // Gets attached socket.
    SOCKET attached_socket()
    {
        return attached_socket_;
    }

    // Scheduler ID.
    uint32_t get_scheduler_id()
    {
        return session_struct_.scheduler_id_;
    }

    // Number of visits.
    uint32_t num_visits()
    {
        return num_visits_;
    }

    // Unique session linear index.
    // Points to the element in sessions linear array.
    uint32_t session_index()
    {
        return session_struct_.session_index_;
    }

    // Used to track one-to-one relationship between attached socket and user session.
    uint64_t socket_stamp()
    {
        return socket_stamp_;
    }

    // Attaches a new socket.
    void AttachSocket(SOCKET newSocket, uint64_t newStamp)
    {
#ifdef GW_SESSIONS_DIAG
        GW_COUT << "Session: " << session_struct_.session_index_ << ", new socket attached: " << newSocket << std::endl;
#endif
        attached_socket_ = newSocket;
        socket_stamp_ = newStamp;
    }

    // Create new session based on random salt, linear index, scheduler.
    void GenerateNewSession(GatewayWorker *gw, uint32_t sessionIndex, uint32_t schedulerId);

    // Increase number of visits in this session.
    void IncreaseVisits()
    {
        num_visits_++;
    }

    // Compare socket stamps of two sessions.
    bool CompareSocketStamps(uint64_t socketStamp)
    {
        return socket_stamp_ == socketStamp;
    }

    // Compare random salt of two sessions.
    bool CompareSalt(uint64_t randomSalt)
    {
        return session_struct_.random_salt_ == randomSalt;
    }

    // Compare two sessions.
    bool Compare(uint64_t randomSalt, uint32_t sessionIndex)
    {
        return session_struct_.IsEqual(randomSalt, sessionIndex);
    }

    // Compare two sessions.
    bool Compare(SessionData *sessionData)
    {
        return session_struct_.IsEqual(sessionData->session_struct_);
    }

    // Converts session to string.
    int32_t ConvertToString(char *str_out)
    {
        return session_struct_.ConvertToString(str_out);
    }
};

// Represents an active database.
class HandlersTable;
class RegisteredUris;
class ActiveDatabase
{
    // Index of this database in global namespace.
    int32_t db_index_;

    // Original database name.
    std::string db_name_;

    // Unique sequence number.
    volatile uint64_t unique_num_;

    // Open socket handles.
    bool active_sockets_[MAX_SOCKET_HANDLE];

    // Number of used sockets.
    volatile int64_t num_used_sockets_;

    // Number of used chunks.
    volatile int64_t num_used_chunks_;

    // Indicates if closure was performed.
    bool were_sockets_closed_;

    // Database handlers.
    HandlersTable* user_handlers_;

public:

    // Gets database handlers.
    HandlersTable* get_user_handlers()
    {
        return user_handlers_;
    }

    // Getting the number of used chunks.
    int64_t num_used_chunks()
    {
        return num_used_chunks_;
    }

    // Getting the number of used sockets.
    int64_t num_used_sockets()
    {
        return num_used_sockets_;
    }

    // Increments or decrements the number of active chunks.
    void ChangeNumUsedChunks(int64_t change_value)
    {
        InterlockedAdd64(&num_used_chunks_, change_value);
    }

    // Closes all tracked sockets.
    void CloseSockets();

    // Tracks certain socket.
    uint32_t TrackSocket(SOCKET s)
    {
        if (active_sockets_[s])
            return 1;

        InterlockedAdd64(&num_used_sockets_, 1);
        active_sockets_[s] = true;
        return 0;
    }

    // Untracks certain socket.
    uint32_t UntrackSocket(SOCKET s)
    {
        if (!active_sockets_[s])
            return 1;

        InterlockedAdd64(&num_used_sockets_, -1);
        active_sockets_[s] = false;
        return 0;
    }

    // Makes this database slot empty.
    void StartDeletion();

    // Checks if this database slot empty.
    bool IsEmpty()
    {
        return ((0 == unique_num_) && (0 == num_used_chunks_) && (0 == num_used_sockets_));
    }

    // Checks if this database slot emptying was started.
    bool IsDeletionStarted()
    {
        return (0 == unique_num_);
    }

    // Active database constructor.
    ActiveDatabase();

    // Destructor.
    ~ActiveDatabase();

    // Gets the name of the database.
    std::string db_name()
    {
        return db_name_;
    }

    // Returns unique number for this database.
    uint64_t unique_num()
    {
        return unique_num_;
    }

    // Initializes this active database slot.
    void Init(std::string new_name, uint64_t new_unique_num, int32_t db_index);
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
    volatile int64_t num_created_sockets_[MAX_ACTIVE_DATABASES];
    volatile int64_t num_active_conns_[MAX_ACTIVE_DATABASES];

    // Offset for the user data to be written.
    int32_t blob_user_data_offset_;

    // Ports handler lists.
    PortHandlers* port_handlers_;

    // All registered URIs belonging to this port.
    RegisteredUris* registered_uris_;

    // All registered subports belonging to this port.
    // TODO: Fix full support!
    RegisteredSubports* registered_subports_;

public:

    // Printing the registered URIs.
    void Print();

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

    // Getting user data offset in blob.
    int32_t get_blob_user_data_offset()
    {
        return blob_user_data_offset_;
    }

    // Removes this port.
    void EraseDb(int32_t db_index);

    // Removes this port.
    void Erase();

    // Checks if this database slot empty.
    bool EmptyForDb(int32_t db_index)
    {
        return (num_created_sockets_[db_index] == 0) && (num_active_conns_[db_index] == 0);
    }

    // Checking if port is unused by any database.
    bool IsEmpty();

    // Initializes server socket.
    void Init(uint16_t port_number, SOCKET port_socket, int32_t blob_user_data_offset);

    // Server port.
    ServerPort();

    // Server port.
    ~ServerPort();

    // Getting port socket.
    SOCKET get_port_socket()
    {
        return listening_sock_;
    }

    // Getting port number.
    uint16_t get_port_number()
    {
        return port_number_;
    }

    // Retrieves the number of active connections.
    int64_t get_num_active_conns(int32_t db_index)
    {
        return num_active_conns_[db_index];
    }

    // Retrieves the number of created sockets.
    int64_t get_num_created_sockets(int32_t db_index)
    {
        return num_created_sockets_[db_index];
    }

    // Increments or decrements the number of created sockets.
    int64_t ChangeNumCreatedSockets(int32_t db_index, int64_t changeValue)
    {
        InterlockedAdd64(&(num_created_sockets_[db_index]), changeValue);
        return num_created_sockets_[db_index];
    }

    // Increments or decrements the number of active connections.
    int64_t ChangeNumActiveConns(int32_t db_index, int64_t changeValue)
    {
        InterlockedAdd64(&(num_active_conns_[db_index]), changeValue);
        return num_active_conns_[db_index];
    }

    // Resets the number of created sockets and active connections.
    void Reset(int32_t db_index)
    {
        InterlockedAnd64(&(num_created_sockets_[db_index]), 0);
        InterlockedAnd64(&(num_active_conns_[db_index]), 0);
    }
};

class GatewayWorker;
class SessionData;
class Gateway
{
    ////////////////////////
    // SETTINGS
    ////////////////////////

    // Maximum total number of sockets aka connections.
    int32_t setting_maxConnections_;

    // Master node IP address.
    std::string setting_master_ip_;

    // Starcounter server type.
    std::string setting_sc_server_type_;

    // Indicates if this node is a master node.
    bool setting_is_master_;

    // Gateway log file name.
    std::wstring setting_log_file_dir_;
    std::wstring setting_log_file_path_;

    // Gateway config file name.
    std::wstring setting_config_file_path_;

    // Local network interfaces to bind on.
    std::vector<std::string> setting_local_interfaces_;

    // Maximum number of same operations processed in a queue.
    int32_t setting_max_same_ops_in_queue_;

    // Maximum amount of overlapped structures fetched at once.
    int32_t setting_max_fetched_ovls_;

    // Number of worker threads.
    int32_t setting_num_workers_;

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

    ////////////////////////
    // WORKERS
    ////////////////////////

    // All worker structures.
    GatewayWorker* gw_workers_;

    // Worker thread handles.
    HANDLE* thread_handles_;

    ////////////////////////
    // SESSIONS
    ////////////////////////

    // All sessions information.
    SessionData *all_sessions_unsafe_;

    // Free session indexes.
    int32_t *free_session_indexes_unsafe_;

    // Number of active sessions.
    volatile int32_t num_active_sessions_unsafe_;

    // Round-robin global scheduler number.
    uint32_t global_scheduler_id_unsafe_;

    ////////////////////////
    // GLOBAL LOCKING
    ////////////////////////

    // Critical section for sessions manipulation.
    CRITICAL_SECTION cs_session_;

    // Critical section on global lock.
    CRITICAL_SECTION cs_global_lock_;

    // Global lock.
    volatile bool global_lock_;

    ////////////////////////
    // OTHER STUFF
    ////////////////////////

    // Represents delete state for all sockets.
    bool deleted_sockets_[MAX_SOCKET_HANDLE];

    // Gateway handlers.
    HandlersTable* gw_handlers_;

    // All server ports.
    ServerPort server_ports_[MAX_ACTIVE_SERVER_PORTS];

    // Number of used server ports.
    volatile int32_t num_server_ports_;

    // The socket address of the server.
    sockaddr_in* server_addr_;

    // Number of active schedulers.
    uint32_t active_sched_num_read_only_;

    // Black list with malicious IP-addresses.
    std::list<uint32_t> black_list_ips_unsafe_;

    // Global IOCP handle.
    HANDLE iocp_;

    // Global sleep interval.
    volatile int32_t global_sleep_ms_;

public:

    // Getting state of the socket.
    bool ShouldSocketBeDeleted(SOCKET sock)
    {
        return deleted_sockets_[sock];
    }

    // Deletes specific socket.
    void MarkDeleteSocket(SOCKET sock)
    {
        deleted_sockets_[sock] = true;
    }

    // Makes specific socket available.
    void MarkSocketAlive(SOCKET sock)
    {
        deleted_sockets_[sock] = false;
    }

    // Getting gateway handlers.
    HandlersTable* get_gw_handlers()
    {
        return gw_handlers_;
    }

    // Checks if certain server port exists.
    ServerPort* FindServerPort(uint16_t port_num)
    {
        for (int32_t i = 0; i < num_server_ports_; i++)
        {
            if (port_num == server_ports_[i].get_port_number())
                return server_ports_ + i;
        }

        return NULL;
    }

    // Checks if certain server port exists.
    int32_t FindServerPortIndex(uint16_t port_num)
    {
        for (int32_t i = 0; i < num_server_ports_; i++)
        {
            if (port_num == server_ports_[i].get_port_number())
                return i;
        }

        return -1;
    }

    // Adds new server port.
    int32_t AddServerPort(uint16_t port_num, SOCKET listening_sock, int32_t blob_user_data_offset)
    {
        // Looking for an empty server port slot.
        int32_t empty_slot = 0;
        for (empty_slot = 0; empty_slot < num_server_ports_; ++empty_slot)
        {
            if (server_ports_[empty_slot].IsEmpty())
                break;
        }

        // Initializing server port on this slot.
        server_ports_[empty_slot].Init(port_num, listening_sock, blob_user_data_offset);

        // Checking if it was the last slot.
        if (empty_slot >= num_server_ports_)
            num_server_ports_++;

        return empty_slot;
    }

    // Runs all port handlers.
    uint32_t RunAllHandlers();

    // Delete all handlers associated with given database.
    uint32_t DeletePortsForDb(int32_t db_index);

    // Get active server ports.
    ServerPort* get_server_port(int32_t port_index)
    {
        return server_ports_ + port_index;
    }

    // Get number of active server ports.
    int32_t get_num_server_ports()
    {
        return num_server_ports_;
    }

    // Gets global sleep interval.
    int32_t get_global_sleep_ms()
    {
        return global_sleep_ms_;
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
        if (global_lock_)
        {
            while (global_lock_)
                Sleep(1);
        }

        // Entering the critical section.
        EnterCriticalSection(&cs_global_lock_);

        global_lock_ = true;
        WaitAllWorkersSuspended();

        // Now we are sure that all workers are suspended.
    }

    // Waits for all workers to suspend.
    void WaitAllWorkersSuspended();

    // Releases global lock.
    void LeaveGlobalLock()
    {
        global_lock_ = false;

        // Leaving critical section.
        LeaveCriticalSection(&cs_global_lock_);
    }

    // Gets global lock value.
    bool global_lock()
    {
        return global_lock_;
    }

    // Sets global lock value.
    void set_global_lock(bool lock_value)
    {
        global_lock_ = lock_value;
    }

    // Returns active database on this slot index.
    ActiveDatabase* GetDatabase(int32_t db_index)
    {
        return active_databases_ + db_index;
    }

    // Get number of active databases.
    int32_t get_num_dbs_slots()
    {
        return num_dbs_slots_;
    }

    // Reading command line arguments.
    uint32_t ReadArguments(int argc, wchar_t* argv[]);

    // Maximum number of same operations processed in a queue.
    int32_t setting_max_same_ops_in_queue()
    {
        return setting_max_same_ops_in_queue_;
    }

    // Maximum amount of overlapped structures fetched at once.
    int32_t setting_max_fetched_ovls()
    {
        return setting_max_fetched_ovls_;
    }

    // Master node IP address.
    std::string setting_master_ip()
    {
        return setting_master_ip_;
    }

    // Get number of active schedulers.
    uint32_t active_sched_num_read_only()
    {
        return active_sched_num_read_only_;
    }

    // Get number of workers.
    int32_t setting_num_workers()
    {
        return setting_num_workers_;
    }

    // Getting the total number of used chunks for all databases.
    int64_t GetTotalNumUsedChunks()
    {
        int64_t totalActiveChunks = 0;
        for (int32_t i = 0; i < get_num_dbs_slots(); i++)
        {
            if (!active_databases_[i].IsEmpty())
                totalActiveChunks += (active_databases_[i].num_used_chunks());
        }

        return totalActiveChunks;
    }

    // Getting the number of used sockets for all databases.
    int64_t GetTotalNumUsedSockets()
    {
        int64_t totalActiveSockets = 0;
        for (int32_t i = 0; i < get_num_dbs_slots(); i++)
        {
            if (!active_databases_[i].IsEmpty())
                totalActiveSockets += (active_databases_[i].num_used_sockets());
        }

        return totalActiveSockets;
    }

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

    // Is master node?
    bool setting_is_master()
    {
        return setting_is_master_;
    }

    // Constructor.
    Gateway();

    // Load settings from XML.
    uint32_t LoadSettings(std::wstring configFilePath);

    // Initialize the network gateway.
    uint32_t Init();

    // Initializes shared memory.
    uint32_t InitSharedMemory(std::string setting_databaseName,
        core::shared_interface* sharedInt_readOnly);

    // Reading list of active databases.
    uint32_t ScanDatabases();

    // Print statistics.
    uint32_t GatewayParallelLoop();

    // Creates socket and binds it to server port.
    uint32_t CreateListeningSocketAndBindToPort(GatewayWorker *gw, uint16_t port_num, SOCKET& sock);

    // Creates new connections on all workers.
    uint32_t CreateNewConnectionsAllWorkers(int32_t howMany, uint16_t port_num, int32_t db_index);

    // Start workers.
    uint32_t StartWorkersAndGatewayLoop(LPTHREAD_START_ROUTINE workerRoutine);

    // Cleanup resources.
    uint32_t GlobalCleanup();

    // Main function to start network gateway.
    int32_t StartGateway();

    // Deletes existing session.
    uint32_t DeleteSession(uint32_t sessionIndex)
    {
        // Checking validity of linear session index.
        if (sessionIndex >= num_active_sessions_unsafe_)
            return 1;

        // Entering the critical section.
        EnterCriticalSection(&cs_session_);

        // Fetching the session by index.
        SessionData *sessionData = &all_sessions_unsafe_[sessionIndex];

        // Comparing two session data.
        if (!sessionData->Compare(sessionData))
        {
            // Leaving the critical section.
            LeaveCriticalSection(&cs_session_);

            return 1;
        }

        // Decrementing number of active sessions.
        num_active_sessions_unsafe_--;
        free_session_indexes_unsafe_[num_active_sessions_unsafe_] = sessionIndex;    

        // Leaving the critical section.
        LeaveCriticalSection(&cs_session_);

        return 0;
    }

    // Generates new global session.
    SessionData* GenerateNewSession(GatewayWorker *gw)
    {
        // Checking that we have not reached maximum number of sessions.
        if (num_active_sessions_unsafe_ >= setting_maxConnections_)
            return NULL;

        // Entering the critical section.
        EnterCriticalSection(&cs_session_);

        // Getting index of a free session data.
        int32_t freeSessionIndex = free_session_indexes_unsafe_[num_active_sessions_unsafe_];

        // Creating an instance of new unique session.
        all_sessions_unsafe_[freeSessionIndex].GenerateNewSession(gw, freeSessionIndex, global_scheduler_id_unsafe_++);
        if (global_scheduler_id_unsafe_ >= active_sched_num_read_only_)
            global_scheduler_id_unsafe_ = 0;

        // Incrementing number of active sessions.
        num_active_sessions_unsafe_++;

        // Leaving the critical section.
        LeaveCriticalSection(&cs_session_);

        // Returning new critical section.
        return all_sessions_unsafe_ + freeSessionIndex;
    }

    // Gets session data by index.
    SessionData *GetSessionData(uint32_t sessionIndex)
    {
        // Checking validity of linear session index.
        if (sessionIndex >= num_active_sessions_unsafe_)
            return NULL;

        // Fetching the session by index.
        return all_sessions_unsafe_ + sessionIndex;
    }

    // Attaches new socket to existing session.
    void AttachSocketToSession(SessionData *existingSession, SOCKET newSocket, uint64_t newStamp)
    {
        // Attaching new socket.
        existingSession->AttachSocket(newSocket, newStamp);
    }
};

extern Gateway g_gateway;

} // namespace network
} // namespace starcounter

#endif // GATEWAY_HPP