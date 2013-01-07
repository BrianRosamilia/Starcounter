#include "static_headers.hpp"
#include "gateway.hpp"
#include "handlers.hpp"
#include "ws_proto.hpp"
#include "http_proto.hpp"
#include "socket_data.hpp"
#include "tls_proto.hpp"
#include "worker_db_interface.hpp"
#include "worker.hpp"

#pragma comment(lib, "Ws2_32.lib")
#pragma comment(lib, "winmm.lib")

namespace starcounter {
namespace network {

// Main network gateway object.
Gateway g_gateway;

// Logging system.
/*
TeeLogStream *g_cout = NULL;
TeeDevice *g_log_tee = NULL;
std::ofstream *g_log_stream = NULL;
*/

// Pointers to extended WinSock functions.
LPFN_ACCEPTEX AcceptExFunc = NULL;
LPFN_CONNECTEX ConnectExFunc = NULL;
LPFN_DISCONNECTEX DisconnectExFunc = NULL;

GUID AcceptExGuid = WSAID_ACCEPTEX,
    ConnectExGuid = WSAID_CONNECTEX,
    DisconnectExGuid = WSAID_DISCONNECTEX;

std::string GetOperTypeString(SocketOperType typeOfOper)
{
    switch (typeOfOper)
    {
        case SEND_SOCKET_OPER: return "SEND_SOCKET_OPER";
        case RECEIVE_SOCKET_OPER: return "RECEIVE_SOCKET_OPER";
        case ACCEPT_SOCKET_OPER: return "ACCEPT_SOCKET_OPER";
        case CONNECT_SOCKET_OPER: return "CONNECT_SOCKET_OPER";
        case DISCONNECT_SOCKET_OPER: return "DISCONNECT_SOCKET_OPER";
        case UNKNOWN_SOCKET_OPER: return "UNKNOWN_SOCKET_OPER";
    }
    return "ERROR_SOCKET_OPER";
}

// Writing to log once object is destroyed.
ThreadSafeCout::~ThreadSafeCout()
{
#ifdef GW_LOGGING_ON

    std::string str = ss.str();
    g_gateway.get_gw_log_writer()->WriteToLog(str.c_str(), str.length());

#endif
}

void GatewayLogWriter::Init(std::wstring& log_file_path)
{
    log_write_pos_ = 0;

    log_read_pos_ = 0;

    accum_length_ = 0;

    InitializeCriticalSection(&write_lock_);

    // Opening log file for writes.
    log_file_handle_ = CreateFile(
        (log_file_path + L"_new").c_str(),
        GENERIC_WRITE,
        FILE_SHARE_READ,
        NULL,
        OPEN_ALWAYS,
        FILE_ATTRIBUTE_NORMAL,
        NULL);

    GW_ASSERT(INVALID_HANDLE_VALUE != log_file_handle_);

    // Seeking to the end of file.
    DWORD file_ptr = SetFilePointer(
        log_file_handle_,
        NULL,
        NULL,
        FILE_END
        );

    GW_ASSERT(INVALID_SET_FILE_POINTER != file_ptr);
}

// Writes given string to log buffer.
#ifdef GW_LOGGING_ON
void GatewayLogWriter::WriteToLog(const char* text, int32_t text_len)
{
    EnterCriticalSection(&write_lock_);

#ifdef GW_LOG_TO_FILE

    int32_t left_space = GW_LOG_BUFFER_SIZE - log_write_pos_;

    // Checking if text fits in the buffer.
    if (text_len <= left_space)
    {
        memcpy(log_buf_ + log_write_pos_, text, text_len);
        log_write_pos_ += text_len;
    }
    // Checking if we should write to the beginning.
    else if (left_space == 0)
    {
        memcpy(log_buf_, text, text_len);
        log_write_pos_ = text_len;
    }
    // Splitted write.
    else
    {
        memcpy(log_buf_ + log_write_pos_, text, left_space);
        memcpy(log_buf_, text + left_space, text_len - left_space);
        log_write_pos_ = text_len - left_space;
    }

    // Adjusting accumulated log size.
    accum_length_ += text_len;
    GW_ASSERT(accum_length_ < GW_LOG_BUFFER_SIZE);

#endif

    // Printing everything to console as well.
#ifdef GW_LOG_TO_CONSOLE
    std::cout << text;
#endif

    LeaveCriticalSection(&write_lock_);
}

// Dump accumulated logs in buffer to file.
void GatewayLogWriter::DumpToLogFile()
{
    if (log_write_pos_ == log_read_pos_)
        return;

    // Saving current write position of log.
    int32_t log_write_pos = log_write_pos_;
    int32_t num_bytes_to_write;
    BOOL err_code;

    // Simplest case when writes are linear in log buffer.
    if (log_write_pos > log_read_pos_)
    {
        num_bytes_to_write = log_write_pos - log_read_pos_;

        err_code = WriteFile(
            log_file_handle_,
            log_buf_ + log_read_pos_,
            num_bytes_to_write,
            NULL,
            NULL
            );

        GW_ASSERT(TRUE == err_code);
    }
    else
    {
        num_bytes_to_write = GW_LOG_BUFFER_SIZE - log_read_pos_;

        if (num_bytes_to_write)
        {
            // Write log_read_pos_ to LOG_BUFFER_SIZE.
            err_code = WriteFile(
                log_file_handle_,
                log_buf_ + log_read_pos_,
                num_bytes_to_write,
                NULL,
                NULL
                );

            GW_ASSERT(TRUE == err_code);
        }

        num_bytes_to_write = log_write_pos;

        if (num_bytes_to_write)
        {
            // Write 0 to log_write_pos.
            err_code = WriteFile(
                log_file_handle_,
                log_buf_,
                num_bytes_to_write,
                NULL,
                NULL
                );

            GW_ASSERT(TRUE == err_code);
        }

        num_bytes_to_write =  GW_LOG_BUFFER_SIZE - log_read_pos_ + log_write_pos;
    }

    // Shifting log read position.
    log_read_pos_ = log_write_pos;

    // Adjusting accumulated log size.
    accum_length_ -= num_bytes_to_write;
    GW_ASSERT(accum_length_ >= 0);
}
#endif

Gateway::Gateway()
{
    // Number of worker threads.
    setting_num_workers_ = 0;

    // Maximum total number of sockets.
    setting_max_connections_ = 0;

    // Default inactive session timeout in seconds.
    setting_inactive_session_timeout_seconds_ = 60 * 20;

    // Starcounter server type.
    setting_sc_server_type_ = "Personal";

    // All worker structures.
    gw_workers_ = NULL;

    // Worker thread handles.
    worker_thread_handles_ = NULL;

    // IOCP handle.
    iocp_ = INVALID_HANDLE_VALUE;

    // Number of active databases.
    num_dbs_slots_ = 0;

    // Init unique sequence number.
    db_seq_num_ = 0;

    // Initializing scheduler information.
    num_schedulers_ = 0;

    // No reverse proxies by default.
    num_reversed_proxies_ = 0;

    // Starting linear unique socket with 0.
    unique_socket_id_ = 0;

    // Server address for testing.
    server_addr_ = NULL;

    // Initializing critical sections.
    InitializeCriticalSection(&cs_session_);
    InitializeCriticalSection(&cs_global_lock_);
    InitializeCriticalSection(&cs_sessions_cleanup_);
    InitializeCriticalSection(&cs_statistics_);

    // Creating gateway handlers table.
    gw_handlers_ = new HandlersTable();

    // Initial number of server ports.
    num_server_ports_unsafe_ = 0;

    // Resetting number of processed HTTP requests.
    num_processed_http_requests_unsafe_ = 0;

    // Initializing to first bind port number.
    last_bind_port_num_unsafe_ = FIRST_BIND_PORT_NUM;

    // First bind interface number.
    last_bind_interface_num_unsafe_ = 0;

    // Resetting Starcounter log handle.
    sc_log_handle_ = INVALID_LOG_HANDLE;

    // Empty global statistics.
    global_statistics_stream_ << "Empty string!";
    memcpy(global_statistics_string_, kHttpStatsHeader, kHttpStatsHeaderLength);

#ifdef GW_TESTING_MODE

    // Master IP address.
    setting_master_ip_ = "";

    // Indicates if this node is a master node.
    setting_is_master_ = true;

    // Number of connections to establish to master.
    setting_num_connections_to_master_ = 0;
    setting_num_connections_to_master_per_worker_ = 0;

    // Number of echoes to send to master node from clients.
    setting_num_echoes_to_master_ = 0;

    // Setting operational mode.
    setting_mode_ = GatewayTestingMode::MODE_APPS_HTTP;

    // Number of operations per second.
    num_ops_per_second_ = 0;

    // Number of measures.
    num_ops_measures_ = 0;

#endif    
}

// Reading command line arguments.
// 1: Server type.
// 2: Gateway configuration file path.
// 3: Starcounter server output directory path.
uint32_t Gateway::ProcessArgumentsAndInitLog(int argc, wchar_t* argv[])
{
    // Checking correct number of arguments.
    if (argc < 4)
    {
        std::wcout << GW_PROGRAM_NAME << L".exe [ServerTypeName] [PathToGatewayXmlConfig] [PathToOutputDirectory]" << std::endl;
        std::wcout << L"Example: " << GW_PROGRAM_NAME << L".exe personal \"c:\\github\\NetworkGateway\\src\\scripts\\server.xml\" \"c:\\github\\Level1\\bin\\Debug\\.db.output\"" << std::endl;

        return SCERRGWWRONGARGS;
    }

    // Reading the Starcounter log directory.
    setting_output_dir_ = argv[3];

    // Opening Starcounter log.
    uint32_t err_code = g_gateway.OpenStarcounterLog();
    if (err_code)
        return err_code;

    // Converting Starcounter server type to narrow char.
    char temp[128];
    wcstombs(temp, argv[1], 128);

    // Copying other fields.
    setting_sc_server_type_ = temp;

    // Converting to upper case.
    std::transform(
        setting_sc_server_type_.begin(),
        setting_sc_server_type_.end(),
        setting_sc_server_type_.begin(),
        ::toupper);

    setting_config_file_path_ = argv[2];

    std::wstring setting_log_file_dir_ = setting_output_dir_ + L"\\" + GW_PROGRAM_NAME;

    // Trying to create network gateway log directory.
    if ((!CreateDirectory(setting_log_file_dir_.c_str(), NULL)) &&
        (ERROR_ALREADY_EXISTS != GetLastError()))
    {
        std::wcout << L"Can't create network gateway log directory: " << setting_log_file_dir_ << std::endl;

        return SCERRGWCANTCREATELOGDIR;
    }

    // Obtaining full path to log directory.
    wchar_t log_file_dir_full[1024];
    DWORD num_copied_chars = GetFullPathName(setting_log_file_dir_.c_str(), 1024, log_file_dir_full, NULL);
    GW_ASSERT(num_copied_chars != 0);

    // Full path to gateway log file.
    setting_log_file_path_ = std::wstring(log_file_dir_full) + L"\\" + GW_PROGRAM_NAME + L".log";

    // Deleting old log file first.
    DeleteFile(setting_log_file_path_.c_str());

    return 0;
}

// Initializes server socket.
void ServerPort::Init(int32_t port_index, uint16_t port_number, SOCKET listening_sock, int32_t blob_user_data_offset)
{
    GW_ASSERT((port_index >= 0) && (port_index < MAX_PORTS_NUM));

    // Allocating needed tables.
    port_handlers_ = new PortHandlers();
    registered_uris_ = new RegisteredUris();
    registered_subports_ = new RegisteredSubports();

    listening_sock_ = listening_sock;
    port_number_ = port_number;
    blob_user_data_offset_ = blob_user_data_offset;
    port_handlers_->set_port_number(port_number_);
    port_index_ = port_index;
}

// Resets the number of created sockets and active connections.
void ServerPort::Reset()
{
    InterlockedAnd64(&(num_accepting_sockets_unsafe_), 0);

#ifdef GW_COLLECT_SOCKET_STATISTICS

    for (int32_t w = 0; w < g_gateway.setting_num_workers(); w++)
    {
        g_gateway.get_worker(w)->SetNumActiveConnections(port_index_, 0);
    }

    InterlockedAnd64(&num_allocated_accept_sockets_unsafe_, 0);
    InterlockedAnd64(&num_allocated_connect_sockets_unsafe_, 0);

#endif
}

// Removes this port.
void ServerPort::EraseDb(int32_t db_index)
{
    // Deleting port handlers if any.
    port_handlers_->RemoveEntry(db_index);

    // Deleting URI handlers if any.
    registered_uris_->RemoveEntry(db_index);

    // Deleting subport handlers if any.
    registered_subports_->RemoveEntry(db_index);
}

// Checking if port is unused by any database.
bool ServerPort::IsEmpty()
{
    // Checks port index first.
    if (INVALID_PORT_INDEX == port_index_)
        return true;

    // Checking port handlers.
    if (port_handlers_ && (!port_handlers_->IsEmpty()))
        return false;

    // Checking URIs.
    if (registered_uris_ && (!registered_uris_->IsEmpty()))
        return false;

    // Checking subports.
    if (registered_subports_ && (!registered_subports_->IsEmpty()))
        return false;

#ifdef GW_COLLECT_SOCKET_STATISTICS

    // Checking connections.
    if (NumberOfActiveConnections())
        return false;

#endif

    return true;
}

#ifdef GW_COLLECT_SOCKET_STATISTICS

// Retrieves the number of active connections.
int64_t ServerPort::NumberOfActiveConnections()
{
    return g_gateway.NumberOfActiveConnectionsPerPort(port_index_);
}

#endif

// Removes this port.
void ServerPort::Erase()
{
    // Closing socket which will results in Disconnect.
    if (INVALID_SOCKET != listening_sock_)
    {
        if (closesocket(listening_sock_))
        {
#ifdef GW_ERRORS_DIAG
            GW_COUT << "closesocket() failed." << GW_ENDL;
#endif
            PrintLastError();
        }
        listening_sock_ = INVALID_SOCKET;
    }

    if (port_handlers_)
    {
        delete [] port_handlers_;
        port_handlers_ = NULL;
    }

    if (registered_uris_)
    {
        delete [] registered_uris_;
        registered_uris_ = NULL;
    }

    if (registered_subports_)
    {
        delete [] registered_subports_;
        registered_subports_ = NULL;
    }

    port_number_ = INVALID_PORT_NUMBER;
    blob_user_data_offset_ = -1;
    port_index_ = INVALID_PORT_INDEX;

    Reset();
}

// Printing the registered URIs.
void ServerPort::Print()
{
    port_handlers_->Print();
    registered_uris_->Print(port_number_);
}

ServerPort::ServerPort()
{
    listening_sock_ = INVALID_SOCKET;
    port_handlers_ = NULL;
    registered_uris_ = NULL;
    registered_subports_ = NULL;

    Erase();
}

ServerPort::~ServerPort()
{
}

// Loads configuration settings from provided XML file.
uint32_t Gateway::LoadSettings(std::wstring configFilePath)
{
    uint32_t err_code;

    // Opening file stream.
    std::ifstream config_file_stream(configFilePath);
    if (!config_file_stream.is_open())
        return SCERRGWCANTLOADXMLSETTINGS;

    // Copying config contents into a string.
    std::stringstream str_stream;
    str_stream << config_file_stream.rdbuf();
    std::string tmp_str = str_stream.str();
    char* config_contents = new char[tmp_str.size() + 1];
    strcpy_s(config_contents, tmp_str.size() + 1, tmp_str.c_str());

    using namespace rapidxml;
    xml_document<> doc; // Character type defaults to char.
    doc.parse<0>(config_contents); // 0 means default parse flags.

    xml_node<> *rootElem = doc.first_node("NetworkGateway");

    // Getting local interfaces.
    xml_node<> *localIpElem = rootElem->first_node("LocalIP");
    while(localIpElem)
    {
        setting_local_interfaces_.push_back(localIpElem->value());
        localIpElem = localIpElem->next_sibling("LocalIP");
    }

    // Getting workers number.
    setting_num_workers_ = atoi(rootElem->first_node("WorkersNumber")->value());

    // Getting maximum connection number.
    setting_max_connections_ = atoi(rootElem->first_node("MaxConnections")->value());

    // Getting inactive session timeout.
    setting_inactive_session_timeout_seconds_ = atoi(rootElem->first_node("InactiveSessionTimeout")->value());

    // Getting gateway statistics port.
    setting_gw_stats_port_ = (uint16_t)atoi(rootElem->first_node("GatewayStatisticsPort")->value());

    // Just enforcing minimum session timeout multiplier.
    if ((setting_inactive_session_timeout_seconds_ % SESSION_LIFETIME_MULTIPLIER) != 0)
        return SCERRGWWRONGMAXIDLESESSIONLIFETIME;

    // Setting minimum session time.
    min_inactive_session_life_seconds_ = setting_inactive_session_timeout_seconds_ / SESSION_LIFETIME_MULTIPLIER;

    // Initializing global timer.
    global_timer_unsafe_ = min_inactive_session_life_seconds_;

#ifdef GW_TESTING_MODE

    // Getting master node IP address.
    setting_master_ip_ = rootElem->first_node("MasterIP")->value();

    // Master node does not need its own IP.
    if (setting_master_ip_ == "")
        setting_is_master_ = true;
    else
        setting_is_master_ = false;

    // Number of connections to establish to master.
    setting_num_connections_to_master_ = atoi(rootElem->first_node("NumConnectionsToMaster")->value());
    GW_ASSERT((setting_num_connections_to_master_ % (setting_num_workers_ * ACCEPT_ROOF_STEP_SIZE)) == 0);
    setting_num_connections_to_master_per_worker_ = setting_num_connections_to_master_ / setting_num_workers_;

    // Number of echoes to send to master node from clients.
    setting_num_echoes_to_master_ = atoi(rootElem->first_node("NumEchoesToMaster")->value());
    ResetEchoTests();

    // Obtaining testing mode.
    setting_mode_ = (GatewayTestingMode) atoi(rootElem->first_node("TestingMode")->value());

#ifdef GW_LOOPED_TEST_MODE
    switch (setting_mode_)
    {
        case GatewayTestingMode::MODE_GATEWAY_HTTP:
        case GatewayTestingMode::MODE_APPS_HTTP:
        {
            looped_echo_request_creator_ = DefaultHttpEchoRequestCreator;
            looped_echo_response_processor_ = DefaultHttpEchoResponseProcessor;
            
            break;
        }
        
        case GatewayTestingMode::MODE_GATEWAY_PING:
        case GatewayTestingMode::MODE_APPS_PING:
        {
            looped_echo_request_creator_ = DefaultRawEchoRequestCreator;
            looped_echo_response_processor_ = DefaultRawEchoResponseProcessor;

            break;
        }
    }
#endif

#endif

#ifdef GW_PROXY_MODE

    // Checking if we have reverse proxies.
    xml_node<char>* proxies_node = rootElem->first_node("ReverseProxies");
    if (proxies_node)
    {
        xml_node<char>* proxy_node = proxies_node->first_node("ReverseProxy");
        int32_t n = 0;
        while (proxy_node)
        {
            // Filling reverse proxy information.
            reverse_proxies_[n].ip_ = proxy_node->first_node("ServerIP")->value();
            reverse_proxies_[n].port_ = atoi(proxy_node->first_node("ServerPort")->value());
            reverse_proxies_[n].uri_ = proxy_node->first_node("URI")->value();
            reverse_proxies_[n].uri_len_ = reverse_proxies_[n].uri_.length();

            // Loading proxied servers.
            sockaddr_in* server_addr = &reverse_proxies_[n].addr_;
            memset(server_addr, 0, sizeof(sockaddr_in));
            server_addr->sin_family = AF_INET;
            server_addr->sin_addr.s_addr = inet_addr(reverse_proxies_[n].ip_.c_str());
            server_addr->sin_port = htons(reverse_proxies_[n].port_);

            // Getting next reverse proxy information.
            proxy_node = proxy_node->next_sibling("ReverseProxy");

            n++;
        }

        num_reversed_proxies_ = n;
    }

#endif

    // Creating double output object.
    /*
    g_log_stream = new std::ofstream(setting_log_file_path_, std::ios::out | std::ios::app);
    g_log_tee = new TeeDevice(std::cout, *g_log_stream);
    g_cout = new TeeLogStream(*g_log_tee);
    */

    // Predefined ports constants.
    PortType portTypes[NUM_PREDEFINED_PORT_TYPES] = { HTTP_PORT, HTTPS_PORT, WEBSOCKETS_PORT, GENSOCKETS_PORT, AGGREGATION_PORT };
    std::string portNames[NUM_PREDEFINED_PORT_TYPES] = { "HttpPort", "HttpsPort", "WebSocketsPort", "GenSocketsPort", "AggregationPort" };
    GENERIC_HANDLER_CALLBACK portHandlerTypes[NUM_PREDEFINED_PORT_TYPES] = { AppsUriProcessData, HttpsProcessData, AppsUriProcessData, AppsPortProcessData, AppsPortProcessData };
    uint16_t portNumbers[NUM_PREDEFINED_PORT_TYPES] = { 80, 443, 80, 123, 12345 };
    uint32_t userDataOffsetsInBlob[NUM_PREDEFINED_PORT_TYPES] = { HTTP_BLOB_USER_DATA_OFFSET, HTTPS_BLOB_USER_DATA_OFFSET, WS_BLOB_USER_DATA_OFFSET, RAW_BLOB_USER_DATA_OFFSET, AGGR_BLOB_USER_DATA_OFFSET };

    // Going through all ports.
    for (int32_t i = 0; i < NUM_PREDEFINED_PORT_TYPES; i++)
    {
        portNumbers[i] = atoi(rootElem->first_node(portNames[i].c_str())->value());
        if (portNumbers[i] <= 0)
            continue;

        GW_COUT << portNames[i] << ": " << portNumbers[i] << GW_ENDL;

        // Checking if several protocols are on the same port.
        int32_t samePortIndex = INVALID_PORT_INDEX;
        for (int32_t k = 0; k < i; k++)
        {
            if ((portNumbers[i] > 0) && (portNumbers[i] == portNumbers[k]))
            {
                samePortIndex = k;
                break;
            }
        }

        // Creating a new port entry.
        err_code = g_gateway.get_gw_handlers()->RegisterPortHandler(NULL, portNumbers[i], 0, portHandlerTypes[i], -1);
        GW_ERR_CHECK(err_code);
    }

    // Allocating data for worker sessions.
    all_sessions_unsafe_ = (ScSessionStructPlus*)_aligned_malloc(sizeof(ScSessionStructPlus) * setting_max_connections_, 64);
    free_session_indexes_unsafe_ = new session_index_type[setting_max_connections_];
    sessions_to_cleanup_unsafe_ = new session_index_type[setting_max_connections_];
    num_active_sessions_unsafe_ = 0;
    num_sessions_to_cleanup_unsafe_ = 0;

    // Cleaning all sessions and free session indexes.
    for (int32_t i = 0; i < setting_max_connections_; i++)
    {
        // Filling up indexes linearly.
        free_session_indexes_unsafe_[i] = i;

        // Resetting all sessions.
        all_sessions_unsafe_[i].session_.Reset();

        // Resetting sessions time stamps.
        all_sessions_unsafe_[i].session_timestamp_ = 0;
    }

    // Cleaning all sockets data.
    for (int32_t i = 0; i < MAX_SOCKET_HANDLE; i++)
        deleted_sockets_bitset_[i] = false;

    delete [] config_contents;

    return 0;
}

// Assert some correct state parameters.
uint32_t Gateway::AssertCorrectState()
{
    SocketDataChunk* test_sdc = new SocketDataChunk();
    uint32_t err_code;

    // Checking correct socket data.
    err_code = test_sdc->AssertCorrectState();
    if (err_code)
        goto FAILED;

    // Checking overall gateway stuff.
    GW_ASSERT(sizeof(ScSessionStruct) == bmx::SESSION_STRUCT_SIZE);

    GW_ASSERT(sizeof(ScSessionStructPlus) == 64);

    return 0;

FAILED:
    delete test_sdc;

    return SCERRGWFAILEDASSERTCORRECTSTATE;
}

// Creates socket and binds it to server port.
uint32_t Gateway::CreateListeningSocketAndBindToPort(GatewayWorker *gw, uint16_t port_num, SOCKET& sock)
{
    // Creating socket.
    sock = WSASocket(AF_INET, SOCK_STREAM, IPPROTO_TCP, NULL, 0, WSA_FLAG_OVERLAPPED);
    if (sock == INVALID_SOCKET)
    {
        GW_COUT << "WSASocket() failed." << GW_ENDL;
        return PrintLastError();
    }

    // Getting IOCP handle.
    HANDLE iocp = NULL;
    if (!gw) iocp = g_gateway.get_iocp();
    else iocp = gw->get_worker_iocp();

    // Attaching socket to IOCP.
    HANDLE temp = CreateIoCompletionPort((HANDLE) sock, iocp, 0, setting_num_workers_);
    if (temp != iocp)
    {
        GW_COUT << "Wrong IOCP returned when adding reference." << GW_ENDL;
        return PrintLastError();
    }

    // Skipping completion port if operation is already successful.
    SetFileCompletionNotificationModes((HANDLE) sock, FILE_SKIP_COMPLETION_PORT_ON_SUCCESS);

    // The socket address to be passed to bind.
    sockaddr_in binding_addr;
    memset(&binding_addr, 0, sizeof(sockaddr_in));
    binding_addr.sin_family = AF_INET;
    binding_addr.sin_addr.s_addr = INADDR_ANY;
    binding_addr.sin_port = htons(port_num);

    // Binding socket to certain interface and port.
    if (bind(sock, (SOCKADDR*) &binding_addr, sizeof(binding_addr)))
    {
        GW_COUT << "Failed to bind server port " << port_num << GW_ENDL;
        return PrintLastError();
    }

    // Listening to connections.
    if (listen(sock, SOMAXCONN))
    {
        GW_COUT << "Error listening on server socket." << GW_ENDL;
        return PrintLastError();
    }

    return 0;
}

// Creates new connections on all workers.
uint32_t Gateway::CreateNewConnectionsAllWorkers(int32_t how_many, uint16_t port_num, int32_t db_index)
{
    // Getting server port index.
    int32_t port_index = g_gateway.FindServerPortIndex(port_num);

    // Creating new connections for each worker.
    for (int32_t i = 0; i < setting_num_workers_; i++)
    {
        uint32_t errCode = gw_workers_[i].CreateNewConnections(how_many, port_index, db_index);
        GW_ERR_CHECK(errCode);
    }

    return 0;
}

// Stub APC function that does nothing.
void __stdcall EmptyApcFunction(ULONG_PTR arg) {
    // Does nothing.
}

// Database channels events monitor thread.
uint32_t __stdcall DatabaseChannelsEventsMonitorRoutine(LPVOID params)
{
    // Index of the database as parameter.
    int32_t db_index = *(int32_t*)params;

    // Determine the worker by interface or channel,
    // and wake up that worker.
	HANDLE worker_thread_handle[MAX_WORKER_THREADS];
	HANDLE work_events[MAX_WORKER_THREADS];
	std::size_t number_of_work_events = 0;
	core::shared_interface* db_shared_int = 0;
	std::size_t work_event_index = 0;
	
    // Setting checking events for each worker.
	for (std::size_t worker_id = 0; worker_id < g_gateway.setting_num_workers(); ++worker_id)
    {
		WorkerDbInterface* db_int = g_gateway.get_worker(worker_id)->GetWorkerDb(db_index);
		db_shared_int = db_int->get_shared_int();

		work_events[worker_id] = db_shared_int->open_client_work_event(db_shared_int->get_client_number());
		++number_of_work_events;

		// Waiting for all channels.
		// TODO: Unset notify flag when worker thread is spinning.
		db_shared_int->client_interface().set_notify_flag(true);

		// Sending APC on the determined worker.
		worker_thread_handle[worker_id] = g_gateway.get_worker_thread_handle(worker_id);
        
		// Waking up the worker thread with APC.
		QueueUserAPC(EmptyApcFunction, worker_thread_handle[worker_id], 0);
	}
	
    // Looping until the database dies (TODO, does not work, forcedly killed).
    while (!g_gateway.GetDatabase(db_index)->IsEmpty())
    {
        // Waiting forever for more events on channels.
        db_shared_int->client_interface().wait_for_work(work_event_index, work_events, number_of_work_events);

		// Waking up the worker thread with APC.
		QueueUserAPC(EmptyApcFunction, worker_thread_handle[work_event_index], 0);
    }

    return 0;
}

// Checks for new/existing databases and updates corresponding shared memory structures.
uint32_t Gateway::CheckDatabaseChanges(std::wstring active_dbs_file_path)
{
    std::ifstream ad_file(active_dbs_file_path);

    // Just quiting if file can't be opened.
    if (ad_file.is_open() == false)
        return 0;

    int32_t server_name_len = setting_sc_server_type_.length();

    // Enabling database down tracking flag.
    for (int32_t i = 0; i < num_dbs_slots_; i++)
        db_did_go_down_[i] = true;

    // Reading file line by line.
    uint32_t err_code;
    std::string current_db_name;
    while (getline(ad_file, current_db_name))
    {
        // Skipping incorrect database names.
        if (current_db_name.compare(0, server_name_len, setting_sc_server_type_) != 0)
            continue;

        // Extracting the database name (skipping the server name and underscore).
        current_db_name = current_db_name.substr(server_name_len + 1, current_db_name.length() - server_name_len);

        bool is_new_database = true;
        for (int32_t i = 0; i < num_dbs_slots_; i++)
        {
            // Checking if it is an existing database.
            if (0 == active_databases_[i].db_name().compare(current_db_name))
            {
                // Existing database is up.
                db_did_go_down_[i] = false;
                is_new_database = false;

                // Creating new shared memory interface.
                break;
            }
        }

#ifdef GW_TESTING_MODE

        // Checking certain database names.
        if (g_gateway.setting_is_master())
        {
            if (current_db_name == "MYDB_CLIENT")
                is_new_database = false;
        }
        else
        {
            if (current_db_name == "MYDB_SERVER")
                is_new_database = false;
        }
#endif

        // We have a new database being up.
        if (is_new_database)
        {

#ifdef GW_DATABASES_DIAG
            GW_PRINT_GLOBAL << "Attaching a new database: " << current_db_name << GW_ENDL;
#endif

            // Entering global lock.
            EnterGlobalLock();

            // Finding first empty slot.
            int32_t empty_db_index = 0;
            for (empty_db_index = 0; empty_db_index < num_dbs_slots_; ++empty_db_index)
            {
                // Checking if database slot is empty.
                if (active_databases_[empty_db_index].IsEmpty())
                    break;
            }

            // Workers shared interface instances.
            core::shared_interface *workers_shared_ints =
                new core::shared_interface[setting_num_workers_];
            
            // Adding to the databases list.
            err_code = InitSharedMemory(current_db_name, workers_shared_ints);
            if (err_code != 0)
            {
                // Leaving global lock.
                LeaveGlobalLock();
                return err_code;
            }

            // Filling necessary fields.
            active_databases_[empty_db_index].Init(current_db_name, ++db_seq_num_, empty_db_index);
            db_did_go_down_[empty_db_index] = false;

            // Increasing number of active databases.
            if (empty_db_index >= num_dbs_slots_)
                num_dbs_slots_++;

            // Adding to workers database interfaces.
            for (int32_t i = 0; i < setting_num_workers_; i++)
            {
                err_code = gw_workers_[i].AddNewDatabase(empty_db_index, workers_shared_ints[i]);
                if (err_code)
                {
                    // Leaving global lock.
                    LeaveGlobalLock();
                    return err_code;
                }
            }

            // Spawning channels events monitor.
            err_code = active_databases_[empty_db_index].SpawnChannelsEventsMonitor();
            if (err_code)
            {
                // Leaving global lock.
                LeaveGlobalLock();
                return err_code;
            }

#ifdef GW_TESTING_MODE

            uint16_t port_number = GATEWAY_TEST_PORT_NUMBER_SERVER;
            if (!g_gateway.setting_is_master())
                port_number++;

            // Checking if we are in Gateway HTTP mode.
            switch (g_gateway.setting_mode())
            {
                case GatewayTestingMode::MODE_GATEWAY_HTTP:
                {
                    // Registering URI handlers.
                    err_code = AddUriHandler(
                        &gw_workers_[0],
                        gw_handlers_,
                        port_number,
                        kHttpEchoUrl,
                        kHttpEchoUrlLength,
                        bmx::HTTP_METHODS::OTHER_METHOD,
                        bmx::INVALID_HANDLER_ID,
                        empty_db_index,
                        GatewayUriProcessEcho);

                    if (err_code)
                    {
                        // Leaving global lock.
                        LeaveGlobalLock();

                        return err_code;
                    }

                    break;
                }
                    
                case GatewayTestingMode::MODE_GATEWAY_PING:
                {
                    // Registering port handler.
                    err_code = AddPortHandler(
                        &gw_workers_[0],
                        gw_handlers_,
                        port_number,
                        bmx::INVALID_HANDLER_ID,
                        empty_db_index,
                        GatewayPortProcessEcho);

                    if (err_code)
                    {
                        // Leaving global lock.
                        LeaveGlobalLock();

                        return err_code;
                    }

                    break;
                }
            }

#endif

#ifdef GW_PROXY_MODE

            // Registering all proxies.
            for (int32_t i = 0; i < num_reversed_proxies_; i++)
            {
                // Registering URI handlers.
                err_code = AddUriHandler(
                    &gw_workers_[0],
                    gw_handlers_,
                    GATEWAY_TEST_PORT_NUMBER_SERVER,
                    reverse_proxies_[i].uri_.c_str(),
                    reverse_proxies_[i].uri_len_,
                    bmx::HTTP_METHODS::OTHER_METHOD,
                    bmx::INVALID_HANDLER_ID,
                    empty_db_index,
                    GatewayUriProcessProxy);

                if (err_code)
                {
                    // Leaving global lock.
                    LeaveGlobalLock();

                    return err_code;
                }
            }

#endif

#ifdef GW_GLOBAL_STATISTICS

            // Registering URI handler for gateway statistics.
            err_code = AddUriHandler(
                &gw_workers_[0],
                gw_handlers_,
                setting_gw_stats_port_,
                "/",
                1,
                bmx::HTTP_METHODS::OTHER_METHOD,
                bmx::INVALID_HANDLER_ID,
                empty_db_index,
                GatewayStatisticsInfo);

            if (err_code)
            {
                // Leaving global lock.
                LeaveGlobalLock();

                return err_code;
            }
#endif

            // Leaving global lock.
            LeaveGlobalLock();

            // Registering push channel on first worker.
            err_code = gw_workers_[0].GetWorkerDb(empty_db_index)->RegisterAllPushChannels();
            GW_ERR_CHECK(err_code);
        }
    }

    // Checking what databases went down.
    for (int32_t s = 0; s < num_dbs_slots_; s++)
    {
        if ((db_did_go_down_[s]) && (!active_databases_[s].IsDeletionStarted()))
        {
#ifdef GW_DATABASES_DIAG
            GW_PRINT_GLOBAL << "Start detaching dead database: " << active_databases_[s].db_name() << GW_ENDL;
#endif

            // Entering global lock.
            EnterGlobalLock();

            // Start database deletion.
            active_databases_[s].StartDeletion();

            // Killing channels events monitor thread.
            active_databases_[s].KillChannelsEventsMonitor();

            // Leaving global lock.
            LeaveGlobalLock();
        }
    }

    return 0;
}

// Active database constructor.
ActiveDatabase::ActiveDatabase()
{
    apps_unique_session_numbers_unsafe_ = NULL;
    apps_session_salts_unsafe_ = NULL;
    user_handlers_ = NULL;

    StartDeletion();
}

// Initializes this active database slot.
void ActiveDatabase::Init(std::string db_name, uint64_t unique_num, int32_t db_index)
{
    // Creating new Apps sessions up to maximum number of connections.
    if (!apps_unique_session_numbers_unsafe_)
    {
        apps_unique_session_numbers_unsafe_ = new apps_unique_session_num_type[g_gateway.setting_max_connections()];
        apps_session_salts_unsafe_ = new session_salt_type[g_gateway.setting_max_connections()];
    }

    // Cleaning all Apps session numbers.
    for (int32_t i = 0; i < g_gateway.setting_max_connections(); i++)
    {
        apps_unique_session_numbers_unsafe_[i] = INVALID_APPS_UNIQUE_SESSION_NUMBER;
        apps_session_salts_unsafe_[i] = INVALID_SESSION_SALT;
    }

    // Creating fresh handlers table.
    user_handlers_ = new HandlersTable();

    db_name_ = db_name;
    unique_num_unsafe_ = unique_num;
    db_index_ = db_index;
    were_sockets_closed_ = false;

    num_confirmed_push_channels_ = 0;
    is_empty_ = false;
}

// Checks if its enough confirmed push channels.
bool ActiveDatabase::IsAllPushChannelsConfirmed()
{
    return (num_confirmed_push_channels_ >= g_gateway.get_num_schedulers());
}

// Checks if this database slot empty.
bool ActiveDatabase::IsEmpty()
{
    if (is_empty_)
        return true;

    // Checking if all chunks for this database were released.
    is_empty_ = (INVALID_UNIQUE_DB_NUMBER == unique_num_unsafe_) &&
        (0 == g_gateway.NumberUsedChunksPerDatabase(db_index_));

    return is_empty_;
}

// Makes this database slot empty.
void ActiveDatabase::StartDeletion()
{
    // Closing all database sockets/sessions data.
    CloseSocketData();

    // Removing handlers table.
    if (user_handlers_)
    {
        delete user_handlers_;
        user_handlers_ = NULL;
    }

    unique_num_unsafe_ = INVALID_UNIQUE_DB_NUMBER;
    db_name_ = "";
}

// Closes all tracked sockets.
void ActiveDatabase::CloseSocketData()
{
    // Checking if sockets were already closed.
    if (were_sockets_closed_)
        return;

    // Marking closure.
    were_sockets_closed_ = true;

    // Checking if just marking for deletion.
    for (SOCKET s = 0; s < MAX_SOCKET_HANDLE; s++)
    {
        bool needs_deletion = false;

        // Checking if socket was active in any workers.
        for (int32_t w = 0; w < g_gateway.setting_num_workers(); w++)
        {
            if (g_gateway.get_worker(w)->GetWorkerDb(db_index_)->GetSocketState(s))
                needs_deletion = true;

            g_gateway.get_worker(w)->GetWorkerDb(db_index_)->UntrackSocket(s);
        }

        // Checking if socket is active.
        if (needs_deletion)
        {
            // Marking deleted socket.
            g_gateway.MarkSocketDelete(s);

            // NOTE: Can't kill the session here, because it can be used by other databases.

            // NOTE: Closing socket which will results in stop of all pending operations on that socket.
            if (closesocket(s))
            {
#ifdef GW_ERRORS_DIAG
                GW_COUT << "closesocket() failed." << GW_ENDL;
#endif
                PrintLastError();
            }
        }
    }
}

// Initializes WinSock, all core data structures, binds server sockets.
uint32_t Gateway::Init()
{
    // Checking if already initialized.
    GW_ASSERT((gw_workers_ == NULL) && (worker_thread_handles_ == NULL));

    // Initialize WinSock.
    WSADATA wsaData = { 0 };
    int32_t errCode = WSAStartup(MAKEWORD(2, 2), &wsaData);
    if (errCode != 0)
    {
        GW_COUT << "WSAStartup() failed: " << errCode << GW_ENDL;
        return errCode;
    }

    // Allocating workers data.
    gw_workers_ = new GatewayWorker[setting_num_workers_];
    worker_thread_handles_ = new HANDLE[setting_num_workers_];

    // Creating IO completion port.
    iocp_ = CreateIoCompletionPort(INVALID_HANDLE_VALUE, NULL, 0, setting_num_workers_);
    if (iocp_ == NULL)
    {
        GW_COUT << "Failed to create IOCP." << GW_ENDL;
        return PrintLastError();
    }

    // Filling up worker parameters.
    for (int i = 0; i < setting_num_workers_; i++)
    {
        int32_t errCode = gw_workers_[i].Init(i);
        if (errCode != 0)
            return errCode;
    }

#ifdef GW_TESTING_MODE
    // Creating and activating server sockets.
    if (setting_is_master_)
    {
#endif

        // Going throw all needed ports.
        for(int32_t p = 0; p < num_server_ports_unsafe_; p++)
        {
            SOCKET server_socket = INVALID_SOCKET;

            // Creating socket and binding to port (only on the first worker).
            uint32_t errCode = CreateListeningSocketAndBindToPort(&gw_workers_[0], server_ports_[p].get_port_number(), server_socket);
            GW_ERR_CHECK(errCode);
        }

#ifdef GW_TESTING_MODE
    }
#endif

    // Obtaining function pointers (AcceptEx, ConnectEx, DisconnectEx).
    uint32_t temp;
    SOCKET tempSocket = WSASocket(AF_INET, SOCK_STREAM, IPPROTO_TCP, NULL, 0, WSA_FLAG_OVERLAPPED);
    if (tempSocket == INVALID_SOCKET)
    {
        GW_COUT << "WSASocket() failed." << GW_ENDL;
        return PrintLastError();
    }

    if (WSAIoctl(tempSocket, SIO_GET_EXTENSION_FUNCTION_POINTER, &AcceptExGuid, sizeof(AcceptExGuid), &AcceptExFunc, sizeof(AcceptExFunc), (LPDWORD)&temp, NULL, NULL))
    {
        GW_COUT << "Failed WSAIoctl(AcceptEx)." << GW_ENDL;
        return PrintLastError();
    }

    if (WSAIoctl(tempSocket, SIO_GET_EXTENSION_FUNCTION_POINTER, &ConnectExGuid, sizeof(ConnectExGuid), &ConnectExFunc, sizeof(ConnectExFunc), (LPDWORD)&temp, NULL, NULL))
    {
        GW_COUT << "Failed WSAIoctl(ConnectEx)." << GW_ENDL;
        return PrintLastError();
    }

    if (WSAIoctl(tempSocket, SIO_GET_EXTENSION_FUNCTION_POINTER, &DisconnectExGuid, sizeof(DisconnectExGuid), &DisconnectExFunc, sizeof(DisconnectExFunc), (LPDWORD)&temp, NULL, NULL))
    {
        GW_COUT << "Failed WSAIoctl(DisconnectEx)." << GW_ENDL;
        return PrintLastError();
    }
    closesocket(tempSocket);

    // Global HTTP init.
    HttpGlobalInit();

    // Initializing Gateway logger.
    gw_log_writer_.Init(setting_log_file_path_);

#ifdef GW_TESTING_MODE

    // Creating server address if we are on client.
    if (!g_gateway.setting_is_master())
    {
        server_addr_ = new sockaddr_in;
        memset(server_addr_, 0, sizeof(sockaddr_in));
        server_addr_->sin_family = AF_INET;
        server_addr_->sin_addr.s_addr = inet_addr(g_gateway.setting_master_ip().c_str());
        server_addr_->sin_port = htons(GATEWAY_TEST_PORT_NUMBER_SERVER);
    }

#endif

    // Indicating that network gateway is ready
    // (should be first line of the output).
    GW_COUT << "Gateway is ready!" << GW_ENDL;

    // Indicating begin of new logging session.
    time_t rawtime;
    time(&rawtime);
    tm *timeinfo = localtime(&rawtime);
    GW_PRINT_GLOBAL << "New logging session: " << asctime(timeinfo) << GW_ENDL;

    return 0;
}

// Initializes everything related to shared memory.
uint32_t Gateway::InitSharedMemory(std::string setting_databaseName,
    core::shared_interface* sharedInt)
{
    using namespace core;

    // Construct the database_shared_memory_parameters_name. The format is
    // <DATABASE_NAME_PREFIX>_<SERVER_TYPE>_<DATABASE_NAME>_0
    std::string shm_params_name = (std::string)DATABASE_NAME_PREFIX + "_" +
        setting_sc_server_type_ + "_" + boost::to_upper_copy(setting_databaseName) + "_0";

    // Open the database shared memory parameters file and obtains a pointer to
    // the shared structure.
    database_shared_memory_parameters_ptr db_shm_params(shm_params_name.c_str());

    // Construct the monitor interface name. The format is
    // <SERVER_NAME>_<MONITOR_INTERFACE_SUFFIX>.
    std::string mon_int_name = (std::string)db_shm_params->get_server_name() + "_" +
        MONITOR_INTERFACE_SUFFIX;

    // Get monitor_interface_ptr for monitor_interface_name.
    monitor_interface_ptr the_monitor_interface(mon_int_name.c_str());

    // Send registration request to the monitor and try to acquire an owner_id.
    // Without an owner_id we can not proceed and have to exit.
    // Get process id and store it in the monitor_interface.
    pid_type pid;
    pid.set_current();
    owner_id the_owner_id;
    uint32_t error_code;

    // Try to register this client process pid. Wait up to 10000 ms.
    if ((error_code = the_monitor_interface->register_client_process(pid,
        the_owner_id, 10000/*ms*/)) != 0)
    {
        // Failed to register this client process pid.
        GW_COUT << "Can't register client process, error code: " << error_code << GW_ENDL;
        return error_code;
    }

    // Open the database shared memory segment.
    if (db_shm_params->get_sequence_number() == 0)
    {
        // Cannot open the database shared memory segment, because it is not
        // initialized yet.
        GW_COUT << "Cannot open the database shared memory segment!" << GW_ENDL;
    }

    // Name of the database shared memory segment.
    std::string shm_seg_name = std::string(DATABASE_NAME_PREFIX) + "_" +
        setting_sc_server_type_ + "_" +
        boost::to_upper_copy(setting_databaseName) + "_" +
        boost::lexical_cast<std::string>(db_shm_params->get_sequence_number());

    // Construct a shared_interface.
    for (int32_t i = 0; i < setting_num_workers_; i++)
    {
        sharedInt[i].init(shm_seg_name.c_str(), mon_int_name.c_str(), pid, the_owner_id);
    }

    // Obtaining number of active schedulers.
    if (num_schedulers_ == 0)
    {
        num_schedulers_ = sharedInt[0].common_scheduler_interface().number_of_active_schedulers();
        GW_PRINT_GLOBAL << "Number of active schedulers: " << num_schedulers_ << GW_ENDL;
    }

    return 0;
}

// Current global statistics value.
const char* Gateway::GetGlobalStatisticsString(int32_t* out_len)
{
    *out_len = 0;

    EnterCriticalSection(&cs_statistics_);

    // Getting number of written characters.
    int32_t n = global_statistics_stream_.tellp();
    GW_ASSERT(n < MAX_STATS_LENGTH);
    *out_len = kHttpStatsHeaderLength + n;

    // Copying characters from stream to given buffer.
    global_statistics_stream_.seekg(0);
    global_statistics_stream_.rdbuf()->sgetn(global_statistics_string_ + kHttpStatsHeaderLength, n);
    global_statistics_string_[kHttpStatsHeaderLength + n] = '\0';

    // Making length a white space.
    *(uint64_t*)(global_statistics_string_ + kHttpStatsHeaderInsertPoint) = 0x2020202020202020;
    
    // Converting body length to string.
    WriteUIntToString(global_statistics_string_ + kHttpStatsHeaderInsertPoint, n);

    LeaveCriticalSection(&cs_statistics_);

    return global_statistics_string_;
}

// Check and wait for global lock.
void Gateway::SuspendWorker(GatewayWorker* gw)
{
    gw->set_worker_suspended(true);

    // Entering the critical section.
    EnterCriticalSection(&cs_global_lock_);

    gw->set_worker_suspended(false);
        
    // Leaving the critical section.
    LeaveCriticalSection(&cs_global_lock_);
}

// Getting specific worker information.
GatewayWorker* Gateway::get_worker(int32_t worker_id)
{
    return gw_workers_ + worker_id;
}

// Delete all information associated with given database from server ports.
uint32_t Gateway::DeletePortsForDb(int32_t db_index)
{
    // Going through all ports.
    for (int32_t i = 0; i < num_server_ports_unsafe_; i++)
    {
        // Deleting port handlers if any.
        server_ports_[i].EraseDb(db_index);

        // Checking if port is not used anywhere.
        if (server_ports_[i].IsEmpty())
            server_ports_[i].Erase();
    }

    // Removing deleted trailing server ports.
    for (int32_t i = (num_server_ports_unsafe_ - 1); i >= 0; i--)
    {
        // Removing until one server port is not empty.
        if (!server_ports_[i].IsEmpty())
            break;

        num_server_ports_unsafe_--;
    }

    return 0;
}

// Getting the number of used sockets.
int64_t Gateway::NumberUsedSocketsAllWorkersAndDatabases()
{
    int64_t num_used_sockets = 0;

    for (int32_t d = 0; d < num_dbs_slots_; d++)
    {
        for (int32_t w = 0; w < setting_num_workers_; w++)
        {
            num_used_sockets += gw_workers_[w].NumberUsedSocketPerDatabase(d);
        }
    }

    return num_used_sockets;
}

// Getting the number of reusable connect sockets.
int64_t Gateway::NumberOfReusableConnectSockets()
{
    int64_t num_reusable_connect_sockets = 0;

    for (int32_t w = 0; w < setting_num_workers_; w++)
    {
        num_reusable_connect_sockets += gw_workers_[w].NumberOfReusableConnectSockets();
    }

    return num_reusable_connect_sockets;
}

// Getting the number of used sockets per database.
int64_t Gateway::NumberUsedSocketsPerDatabase(int32_t db_index)
{
    int64_t num_used_sockets = 0;

    for (int32_t w = 0; w < setting_num_workers_; w++)
    {
        num_used_sockets += gw_workers_[w].NumberUsedSocketPerDatabase(db_index);
    }

    return num_used_sockets;
}

// Getting the number of used sockets per worker.
int64_t Gateway::NumberUsedSocketsPerWorker(int32_t worker_id)
{
    int64_t num_used_sockets = 0;

    for (int32_t d = 0; d < num_dbs_slots_; d++)
    {
        num_used_sockets += gw_workers_[worker_id].NumberUsedSocketPerDatabase(d);
    }

    return num_used_sockets;
}

// Getting the total number of used chunks for all databases.
int64_t Gateway::NumberUsedChunksAllWorkersAndDatabases()
{
    int64_t total_used_chunks = 0;
    for (int32_t d = 0; d < num_dbs_slots_; d++)
    {
        for (int32_t w = 0; w < setting_num_workers_; w++)
        {
            total_used_chunks += (gw_workers_[w].NumberUsedChunksPerDatabasePerWorker(d));
        }
    }

    return total_used_chunks;
}

// Getting the number of used chunks per database.
int64_t Gateway::NumberUsedChunksPerDatabase(int32_t db_index)
{
    int64_t num_used_chunks = 0;

    for (int32_t w = 0; w < setting_num_workers_; w++)
    {
        num_used_chunks += gw_workers_[w].NumberUsedChunksPerDatabasePerWorker(db_index);
    }

    return num_used_chunks;
}

// Getting the number of used chunks per worker.
int64_t Gateway::NumberUsedChunksPerWorker(int32_t worker_id)
{
    int64_t num_used_chunks = 0;

    for (int32_t d = 0; d < num_dbs_slots_; d++)
    {
        num_used_chunks += gw_workers_[worker_id].NumberUsedChunksPerDatabasePerWorker(d);
    }

    return num_used_chunks;
}

#ifdef GW_COLLECT_SOCKET_STATISTICS

// Getting the number of active connections per port.
int64_t Gateway::NumberOfActiveConnectionsPerPort(int32_t port_index)
{
    int64_t num_active_conns = 0;

    for (int32_t w = 0; w < setting_num_workers_; w++)
    {
        num_active_conns += gw_workers_[w].NumberOfActiveConnectionsPerPortPerWorker(port_index);
    }

    return num_active_conns;
}

#endif

// Waits for all workers to suspend.
void Gateway::WaitAllWorkersSuspended()
{
    int32_t num_worker_locked = 0;

    // First waking up all workers.
    WakeUpAllWorkers();

    // Waiting for all workers to suspend.
    while (num_worker_locked < setting_num_workers_)
    {
        Sleep(1);
        num_worker_locked = 0;
        for (int32_t i = 0; i < setting_num_workers_; i++)
        {
            if (gw_workers_[i].worker_suspended())
                num_worker_locked++;
        }
    }
}

// Waking up all workers if they are sleeping.
void Gateway::WakeUpAllWorkers()
{
    // Waking up all the workers if needed.
    for (int32_t i = 0; i < g_gateway.setting_num_workers(); i++)
    {
        // Obtaining worker thread handle to call an APC event.
        HANDLE worker_thread_handle = g_gateway.get_worker_thread_handle(i);

        // Waking up the worker with APC.
        QueueUserAPC(EmptyApcFunction, worker_thread_handle, 0);
    }
}

// Entry point for monitoring databases thread.
uint32_t __stdcall MonitorDatabases(LPVOID params)
{
    uint32_t err_code = 0;

    // Creating path to IPC monitor directory active databases.
    std::wstring active_databases_dir = g_gateway.get_setting_output_dir() + L"\\"+ W_DEFAULT_MONITOR_DIR_NAME + L"\\" + W_DEFAULT_MONITOR_ACTIVE_DATABASES_FILE_NAME + L"\\";

    // Obtaining full path to IPC monitor directory.
    wchar_t active_databases_dir_full[1024];
    if (!GetFullPathName(active_databases_dir.c_str(), 1024, active_databases_dir_full, NULL))
    {
        GW_PRINT_GLOBAL << "Can't obtain full path for IPC monitor output directory: " << PrintLastError() << GW_ENDL;
        return SCERRGWPATHTOIPCMONITORDIR;
    }

    // Waiting until active databases directory is up.
    while (GetFileAttributes(active_databases_dir_full) == INVALID_FILE_ATTRIBUTES)
    {
        GW_PRINT_GLOBAL << "Please start the IPC monitor process first!" << GW_ENDL;
        Sleep(500);
    }

    // Creating path to active databases file.
    std::wstring active_databases_file_path = active_databases_dir_full;
    active_databases_file_path += W_DEFAULT_MONITOR_ACTIVE_DATABASES_FILE_NAME;

    // Setting listener on monitor output directory.
    HANDLE dir_changes_hook_handle = FindFirstChangeNotification(active_databases_dir_full, FALSE, FILE_NOTIFY_CHANGE_LAST_WRITE);
    if ((INVALID_HANDLE_VALUE == dir_changes_hook_handle) || (NULL == dir_changes_hook_handle))
    {
        GW_PRINT_GLOBAL << "Can't listen for active databases directory changes: " << PrintLastError() << GW_ENDL;
        FindCloseChangeNotification(dir_changes_hook_handle);

        return SCERRGWACTIVEDBLISTENPROBLEM;
    }

    while (1)
    {
        // Waiting infinitely on directory changes.
        DWORD wait_status = WaitForSingleObject(dir_changes_hook_handle, INFINITE);
        GW_PRINT_GLOBAL << "Changes in active databases directory detected." << GW_ENDL;

        switch (wait_status)
        {
            case WAIT_OBJECT_0:
            {
                // Checking for any database changes in active databases file.
                err_code = g_gateway.CheckDatabaseChanges(active_databases_file_path);

                if (err_code)
                {
                    FindCloseChangeNotification(dir_changes_hook_handle);
                    return err_code;
                }

                // Requests that the operating system signal a change notification
                // handle the next time it detects an appropriate change.
                if (FindNextChangeNotification(dir_changes_hook_handle) == FALSE)
                {
                    GW_PRINT_GLOBAL << "Failed to find next change notification on monitor active databases directory: " << PrintLastError() << GW_ENDL;
                    FindCloseChangeNotification(dir_changes_hook_handle);

                    return SCERRGWFAILEDFINDNEXTCHANGENOTIFICATION;
                }

                break;
            }

            default:
            {
                GW_PRINT_GLOBAL << "Error listening for active databases directory changes: " << PrintLastError() << GW_ENDL;
                FindCloseChangeNotification(dir_changes_hook_handle);

                return SCERRGWACTIVEDBLISTENPROBLEM;
            }
        }
    }

    return 0;
}

// Entry point for gateway worker.
uint32_t __stdcall MonitorDatabasesRoutine(LPVOID params)
{
    // Catching all unhandled exceptions in this thread.
    GW_SC_BEGIN_FUNC

    return MonitorDatabases(params);

    // Catching all unhandled exceptions in this thread.
    GW_SC_END_FUNC
}

// Entry point for gateway worker.
uint32_t __stdcall GatewayWorkerRoutine(LPVOID params)
{
    // Catching all unhandled exceptions in this thread.
    GW_SC_BEGIN_FUNC

    return ((GatewayWorker *)params)->WorkerRoutine();

    // Catching all unhandled exceptions in this thread.
    GW_SC_END_FUNC
}

// Entry point for channels events monitor.
uint32_t __stdcall AllDatabasesChannelsEventsMonitorRoutine(LPVOID params)
{
    // Catching all unhandled exceptions in this thread.
    GW_SC_BEGIN_FUNC

    while (true)
    {
        Sleep(100000);

        for (int32_t i = 0; i < g_gateway.get_num_dbs_slots(); i++)
        {

        }
    }

    return 0;

    // Catching all unhandled exceptions in this thread.
    GW_SC_END_FUNC
}

// Cleans up all collected inactive sessions.
uint32_t Gateway::CleanupInactiveSessions(GatewayWorker* gw)
{
    uint32_t err_code;

    EnterCriticalSection(&cs_sessions_cleanup_);

    // Going through all collected inactive sessions.
    int64_t num_sessions_to_cleanup = num_sessions_to_cleanup_unsafe_;
    for (int32_t i = 0; i < num_sessions_to_cleanup; i++)
    {
        // Getting existing session copy.
        ScSessionStruct global_session_copy = GetGlobalSessionDataCopy(sessions_to_cleanup_unsafe_[i]);

        // Checking if session is valid.
        if (global_session_copy.IsValid())
        {
            // Killing the session.
            bool session_was_killed;
            KillSession(sessions_to_cleanup_unsafe_[i], &session_was_killed);

            // Sending notification only if session was killed.
            if (session_was_killed)
            {
                // Pushing dead session message to all databases.
                for (int32_t d = 0; d < num_dbs_slots_; d++)
                {
                    ActiveDatabase* global_db = g_gateway.GetDatabase(d);

                    // Checking if database was already deleted.
                    if (global_db->IsDeletionStarted())
                        continue;

                    WorkerDbInterface *worker_db = gw->GetWorkerDb(d);

                    // Checking if database was already deleted.
                    if (!worker_db)
                        continue;

                    // Getting and checking Apps unique session number.
                    apps_unique_session_num_type apps_unique_session_num = global_db->GetAppsUniqueSessionNumber(global_session_copy.gw_session_index_);

                    // Checking if Apps session information is correct.
                    if (apps_unique_session_num != INVALID_APPS_UNIQUE_SESSION_NUMBER)
                    {
                        // Getting Apps session salt.
                        session_salt_type apps_session_salt = global_db->GetAppsSessionSalt(global_session_copy.gw_session_index_);

                        // Sending session destroyed message.
                        err_code = worker_db->PushDeadSession(
                            apps_unique_session_num,
                            apps_session_salt,
                            global_session_copy.scheduler_id_);

                        if (err_code)
                        {
                            LeaveCriticalSection(&cs_sessions_cleanup_);
                            return err_code;
                        }
                    }
                }

#ifdef GW_SESSIONS_DIAG
                GW_COUT << "Inactive session " << global_session_copy.gw_session_index_ << ":"
                    << global_session_copy.gw_session_salt_ << " was destroyed." << GW_ENDL;
#endif
            }
        }

        // Inactive session was successfully cleaned up.
        num_sessions_to_cleanup_unsafe_--;
    }

    GW_ASSERT(0 == num_sessions_to_cleanup_unsafe_);

    LeaveCriticalSection(&cs_sessions_cleanup_);

    return 0;
}

// Collects outdated sessions if any.
uint32_t Gateway::CollectInactiveSessions()
{
    // Checking if collected sessions cleanup is not finished yet.
    if (num_sessions_to_cleanup_unsafe_)
        return 0;

    EnterCriticalSection(&cs_sessions_cleanup_);

    GW_ASSERT(0 == num_sessions_to_cleanup_unsafe_);

    int32_t num_inactive = 0;

    // TODO: Optimize scanning range.
    for (int32_t i = 0; i < setting_max_connections_; i++)
    {
        // Checking if session touch time is older than inactive session timeout.
        if ((all_sessions_unsafe_[i].session_timestamp_) &&
            (global_timer_unsafe_ - all_sessions_unsafe_[i].session_timestamp_) >= setting_inactive_session_timeout_seconds_)
        {
            sessions_to_cleanup_unsafe_[num_inactive] = i;
            ++num_inactive;
        }

        // Checking if we have checked all active sessions.
        if (num_inactive >= num_active_sessions_unsafe_)
            break;
    }

    num_sessions_to_cleanup_unsafe_ = num_inactive;

    LeaveCriticalSection(&cs_sessions_cleanup_);

    if (num_active_sessions_unsafe_)
    {
        // Waking up the worker 0 thread with APC.
        QueueUserAPC(EmptyApcFunction, worker_thread_handles_[0], 0);
    }

    return 0;
}

// Entry point for inactive sessions cleanup.
uint32_t __stdcall InactiveSessionsCleanupRoutine(LPVOID params)
{
    // Catching all unhandled exceptions in this thread.
    GW_SC_BEGIN_FUNC

    while (true)
    {
        // Sleeping minimum interval.
        Sleep(g_gateway.get_min_inactive_session_life_seconds() * 1000);

        // Increasing global time by minimum number of seconds.
        g_gateway.step_global_timer_unsafe(g_gateway.get_min_inactive_session_life_seconds());

        // Collecting inactive sessions if any.
        g_gateway.CollectInactiveSessions();
    }

    return 0;

    // Catching all unhandled exceptions in this thread.
    GW_SC_END_FUNC
}

// Entry point for Gateway logging routine.
uint32_t __stdcall GatewayLoggingRoutine(LPVOID params)
{
    // Catching all unhandled exceptions in this thread.
    GW_SC_BEGIN_FUNC

    while (true)
    {
        // Sleeping some interval.
        Sleep(5000);

        // Dumping to gateway log file (if anything new was logged).
#ifdef GW_LOG_TO_FILE
        g_gateway.get_gw_log_writer()->DumpToLogFile();
#endif
    }

    return 0;

    // Catching all unhandled exceptions in this thread.
    GW_SC_END_FUNC
}

// Entry point for all threads monitor routine.
uint32_t __stdcall AllThreadsMonitorRoutine(LPVOID params)
{
    // Catching all unhandled exceptions in this thread.
    GW_SC_BEGIN_FUNC

    return g_gateway.AllThreadsMonitor();

    // Catching all unhandled exceptions in this thread.
    GW_SC_END_FUNC
}

// Cleaning up all global resources.
uint32_t Gateway::GlobalCleanup()
{
    // Closing IOCP.
    CloseHandle(iocp_);

    // Closing logging system.
    //delete g_cout;
    //delete g_log_tee;
    //delete g_log_stream;

    // Cleanup WinSock.
    WSACleanup();

    // Deleting critical sections.
    DeleteCriticalSection(&cs_session_);
    DeleteCriticalSection(&cs_global_lock_);
    DeleteCriticalSection(&cs_sessions_cleanup_);
    DeleteCriticalSection(&cs_statistics_);

    // Closing the log.
    CloseStarcounterLog();

    return 0;
}

// Checks that all Gateway threads are alive.
uint32_t Gateway::AllThreadsMonitor()
{
    while (1)
    {
        // Sleeping some interval.
        Sleep(5000);

        // Checking if workers are still running.
        for (int32_t i = 0; i < setting_num_workers_; i++)
        {
            if (!WaitForSingleObject(worker_thread_handles_[i], 0))
            {
                GW_COUT << "Worker " << i << " is dead." << GW_ENDL;
                return SCERRGWWORKERISDEAD;
            }
        }

        // Checking if database monitor thread is alive.
        if (!WaitForSingleObject(db_monitor_thread_handle_, 0))
        {
            GW_COUT << "Active databases monitor thread is dead." << GW_ENDL;
            return SCERRGWDATABASEMONITORISDEAD;
        }

        // Checking if database channels events thread is alive.
        if (!WaitForSingleObject(channels_events_thread_handle_, 0))
        {
            GW_COUT << "Channels events thread is dead." << GW_ENDL;
            return SCERRGWCHANNELSEVENTSTHREADISDEAD;
        }

        // Checking if dead sessions cleanup thread is alive.
        if (!WaitForSingleObject(dead_sessions_cleanup_thread_handle_, 0))
        {
            GW_COUT << "Dead sessions cleanup thread is dead." << GW_ENDL;
            return SCERRGWSESSIONSCLEANUPTHREADISDEAD;
        }

        // Checking if gateway logging thread is alive.
        if (!WaitForSingleObject(gateway_logging_thread_handle_, 0))
        {
            GW_COUT << "Gateway logging thread is dead." << GW_ENDL;
            return SCERRGWGATEWAYLOGGINGTHREADISDEAD;
        }
    }

    return 0;
}

// Prints statistics, monitors all gateway threads, etc.
uint32_t Gateway::StatisticsAndMonitoringRoutine()
{
    // Previous statistics values.
    int64_t prevBytesReceivedAllWorkers = 0,
        prevBytesSentAllWorkers = 0,
        prevSentNumAllWorkers = 0,
        prevRecvNumAllWorkers = 0,
        prevProcessedHttpRequestsAllWorkers = 0;

    // New statistics values.
    int64_t newBytesReceivedAllWorkers = 0,
        newBytesSentAllWorkers = 0,
        newSentNumAllWorkers = 0,
        newRecvNumAllWorkers = 0,
        newProcessedHttpRequestsAllWorkers = 0;

    // Difference between new and previous statistics values.
    int64_t diffBytesReceivedAllWorkers = 0,
        diffBytesSentAllWorkers = 0,
        diffSentNumAllWorkers = 0,
        diffRecvNumAllWorkers = 0,
        diffProcessedHttpRequestsAllWorkers = 0;

    while(1)
    {
        // Waiting some time for statistics updates.
        Sleep(1000);

        // Checking that all gateway threads are alive.
        if (!WaitForSingleObject(all_threads_monitor_handle_, 0))
        {
            GW_COUT << "Some of the gateway threads are dead. Exiting process." << GW_ENDL;

            ExitProcess(SCERRGWSOMETHREADDIED);
        }

        // Resetting new values.
        newBytesReceivedAllWorkers = 0;
        newBytesSentAllWorkers = 0;
        newSentNumAllWorkers = 0;
        newRecvNumAllWorkers = 0;
        newProcessedHttpRequestsAllWorkers = g_gateway.get_num_processed_http_requests();

        // Fetching new statistics.
        for (int32_t i = 0; i < setting_num_workers_; i++)
        {
            newBytesReceivedAllWorkers += gw_workers_[i].get_worker_stats_bytes_received();
            newBytesSentAllWorkers += gw_workers_[i].get_worker_stats_bytes_sent();
            newSentNumAllWorkers += gw_workers_[i].get_worker_stats_sent_num();
            newRecvNumAllWorkers += gw_workers_[i].get_worker_stats_recv_num();
        }

        // Calculating differences.
        diffBytesReceivedAllWorkers = newBytesReceivedAllWorkers - prevBytesReceivedAllWorkers;
        diffBytesSentAllWorkers = newBytesSentAllWorkers - prevBytesSentAllWorkers;
        diffSentNumAllWorkers = newSentNumAllWorkers - prevSentNumAllWorkers;
        diffRecvNumAllWorkers = newRecvNumAllWorkers - prevRecvNumAllWorkers;
        diffProcessedHttpRequestsAllWorkers = newProcessedHttpRequestsAllWorkers - prevProcessedHttpRequestsAllWorkers;

        // Updating previous values.
        prevBytesReceivedAllWorkers = newBytesReceivedAllWorkers;
        prevBytesSentAllWorkers = newBytesSentAllWorkers;
        prevSentNumAllWorkers = newSentNumAllWorkers;
        prevRecvNumAllWorkers = newRecvNumAllWorkers;
        prevProcessedHttpRequestsAllWorkers = newProcessedHttpRequestsAllWorkers;

        // Calculating bandwidth.
        double recv_bandwidth_mbit_total = ((diffBytesReceivedAllWorkers * 8) / 1000000.0);
        double send_bandwidth_mbit_total = ((diffBytesSentAllWorkers * 8) / 1000000.0);

#ifdef GW_TESTING_MODE
        switch (g_gateway.setting_mode())
        {
            case GatewayTestingMode::MODE_GATEWAY_HTTP:
            case GatewayTestingMode::MODE_APPS_HTTP:
            {
                num_ops_per_second_ += diffProcessedHttpRequestsAllWorkers;
                break;
            }

            case GatewayTestingMode::MODE_GATEWAY_PING:
            case GatewayTestingMode::MODE_APPS_PING:
            {
                num_ops_per_second_ += diffRecvNumAllWorkers;
                break;
            }
        }

        num_ops_measures_++;
#endif

#ifdef GW_GLOBAL_STATISTICS

        EnterCriticalSection(&cs_statistics_);

        // Emptying the statistics stream.
        global_statistics_stream_.clear();
        global_statistics_stream_.seekp(0);

        // Global statistics.
        global_statistics_stream_ << "Global: " <<
            "Active chunks " << g_gateway.NumberUsedChunksAllWorkersAndDatabases() <<
            ", active sessions " << g_gateway.get_num_active_sessions_unsafe() <<
            ", used sockets " << g_gateway.NumberUsedSocketsAllWorkersAndDatabases() <<
            ", reusable conn socks " << g_gateway.NumberOfReusableConnectSockets() <<
            "<br>" << GW_ENDL;

        // Individual workers statistics.
        for (int32_t worker_id_ = 0; worker_id_ < setting_num_workers_; worker_id_++)
        {
            global_statistics_stream_ << "[" << worker_id_ << "]: " <<
                "recv_bytes " << gw_workers_[worker_id_].get_worker_stats_bytes_received() <<
                ", recv_times " << gw_workers_[worker_id_].get_worker_stats_recv_num() <<
                ", sent_bytes " << gw_workers_[worker_id_].get_worker_stats_bytes_sent() <<
                ", sent_times " << gw_workers_[worker_id_].get_worker_stats_sent_num() <<
                "<br>" << GW_ENDL;
        }

        // Printing handlers information for each attached database and gateway.
        for (int32_t p = 0; p < num_server_ports_unsafe_; p++)
        {
            if (!server_ports_->IsEmpty())
            {
                global_statistics_stream_ << "Port " << server_ports_[p].get_port_number() <<

#ifdef GW_COLLECT_SOCKET_STATISTICS
                    ": active conns " << server_ports_[p].NumberOfActiveConnections() <<
#endif

                    ": accepting socks " << server_ports_[p].get_num_accepting_sockets() <<

#ifdef GW_COLLECT_SOCKET_STATISTICS
                    ", alloc acc-socks " << server_ports_[p].get_num_allocated_accept_sockets() <<
                    ", alloc conn-socks " << server_ports_[p].get_num_allocated_connect_sockets() <<
#endif                        
                    "<br>" << GW_ENDL;
            }
        }

        // Printing all workers stats.
        global_statistics_stream_ << "All workers last sec " <<
            "recv_times " << diffRecvNumAllWorkers <<
            ", http_requests " << diffProcessedHttpRequestsAllWorkers <<
            ", recv_bandwidth " << recv_bandwidth_mbit_total << " mbit/sec" <<
            ", sent_times " << diffSentNumAllWorkers <<
            ", send_bandwidth " << send_bandwidth_mbit_total << " mbit/sec" <<
            "<br>" << GW_ENDL;

        LeaveCriticalSection(&cs_statistics_);

        // Printing the statistics string.
        //int32_t len;
        //std::cout << GetGlobalStatisticsString(&len);
#endif

    }

    return 0;
}

// Starts gateway workers and statistics printer.
uint32_t Gateway::StartWorkerAndManagementThreads(
    LPTHREAD_START_ROUTINE workerRoutine,
    LPTHREAD_START_ROUTINE monitorDatabasesRoutine,
    LPTHREAD_START_ROUTINE channelsEventsMonitorRoutine,
    LPTHREAD_START_ROUTINE deadSessionsCleanupRoutine,
    LPTHREAD_START_ROUTINE gatewayLoggingRoutine,
    LPTHREAD_START_ROUTINE allThreadsMonitorRoutine)
{
    // Allocating threads-related data structures.
    uint32_t *workerThreadIds = new uint32_t[setting_num_workers_];

    // Starting workers one by one.
    for (int i = 0; i < setting_num_workers_; i++)
    {
        // Creating threads.
        worker_thread_handles_[i] = CreateThread(
            NULL, // Default security attributes.
            0, // Use default stack size.
            workerRoutine, // Thread function name.
            &gw_workers_[i], // Argument to thread function.
            0, // Use default creation flags.
            (LPDWORD)&workerThreadIds[i]); // Returns the thread identifier.

        // Checking if threads are created.
        GW_ASSERT(worker_thread_handles_[i] != NULL);
    }

    uint32_t dbScanThreadId;

    // Starting database scanning thread.
    db_monitor_thread_handle_ = CreateThread(
        NULL, // Default security attributes.
        0, // Use default stack size.
        monitorDatabasesRoutine, // Thread function name.
        NULL, // Argument to thread function.
        0, // Use default creation flags.
        (LPDWORD)&dbScanThreadId); // Returns the thread identifier.

    // Checking if thread is created.
    GW_ASSERT(db_monitor_thread_handle_ != NULL);

    uint32_t channelsEventsThreadId;

    // Starting channels events monitor thread.
    channels_events_thread_handle_ = CreateThread(
        NULL, // Default security attributes.
        0, // Use default stack size.
        channelsEventsMonitorRoutine, // Thread function name.
        NULL, // Argument to thread function.
        0, // Use default creation flags.
        (LPDWORD)&channelsEventsThreadId); // Returns the thread identifier.

    // Checking if thread is created.
    GW_ASSERT(channels_events_thread_handle_ != NULL);

    uint32_t deadSessionsCleanupThreadId;

    // Starting dead sessions cleanup thread.
    dead_sessions_cleanup_thread_handle_ = CreateThread(
        NULL, // Default security attributes.
        0, // Use default stack size.
        deadSessionsCleanupRoutine, // Thread function name.
        NULL, // Argument to thread function.
        0, // Use default creation flags.
        (LPDWORD)&deadSessionsCleanupThreadId); // Returns the thread identifier.

    // Checking if thread is created.
    GW_ASSERT(dead_sessions_cleanup_thread_handle_ != NULL);

    uint32_t gatewayLogRoutineThreadId;

    // Starting dead sessions cleanup thread.
    gateway_logging_thread_handle_ = CreateThread(
        NULL, // Default security attributes.
        0, // Use default stack size.
        gatewayLoggingRoutine, // Thread function name.
        NULL, // Argument to thread function.
        0, // Use default creation flags.
        (LPDWORD)&gatewayLogRoutineThreadId); // Returns the thread identifier.

    // Checking if thread is created.
    GW_ASSERT(gateway_logging_thread_handle_ != NULL);

    // Starting dead sessions cleanup thread.
    all_threads_monitor_handle_ = CreateThread(
        NULL, // Default security attributes.
        0, // Use default stack size.
        allThreadsMonitorRoutine, // Thread function name.
        NULL, // Argument to thread function.
        0, // Use default creation flags.
        (LPDWORD)&gatewayLogRoutineThreadId); // Returns the thread identifier.

    // Checking if thread is created.
    GW_ASSERT(all_threads_monitor_handle_ != NULL);

    // Printing statistics.
    uint32_t err_code = g_gateway.StatisticsAndMonitoringRoutine();

    // Close all thread handles and free memory allocations.
    for(int i = 0; i < setting_num_workers_; i++)
        CloseHandle(worker_thread_handles_[i]);

    delete workerThreadIds;
    delete worker_thread_handles_;
    delete [] gw_workers_;

    // Checking if any error occurred.
    GW_ERR_CHECK(err_code);

    return 0;
}

int32_t Gateway::StartGateway()
{
    uint32_t errCode;

    // Assert some correct state.
    errCode = AssertCorrectState();
    if (errCode)
    {
        GW_COUT << "Asserting correct state failed." << GW_ENDL;
        return errCode;
    }

    // Loading configuration settings.
    errCode = LoadSettings(setting_config_file_path_);
    if (errCode)
    {
        GW_COUT << "Loading configuration settings failed." << GW_ENDL;
        return errCode;
    }

    // Creating data structures and binding sockets.
    errCode = Init();
    if (errCode)
        return errCode;

    // Starting workers and statistics printer.
    errCode = StartWorkerAndManagementThreads(
        (LPTHREAD_START_ROUTINE)GatewayWorkerRoutine,
        (LPTHREAD_START_ROUTINE)MonitorDatabasesRoutine,
        (LPTHREAD_START_ROUTINE)AllDatabasesChannelsEventsMonitorRoutine,
        (LPTHREAD_START_ROUTINE)InactiveSessionsCleanupRoutine,
        (LPTHREAD_START_ROUTINE)GatewayLoggingRoutine,
        (LPTHREAD_START_ROUTINE)AllThreadsMonitorRoutine);

    if (errCode)
        return errCode;

    return 0;
}

#ifdef GW_TESTING_MODE

// Gracefully shutdowns all needed processes after test is finished.
uint32_t Gateway::ShutdownTest(bool success)
{
    // Checking if we are on the build server.
    bool is_on_build_server = (NULL != std::getenv("SC_RUNNING_ON_BUILD_SERVER"));

    if (success)
    {
        // Test finished successfully, printing the results.
        GW_COUT << "Echo test finished successfully!" << GW_ENDL;
        GW_COUT << "Average number of ops per second: " << GetAverageOpsPerSecond() << GW_ENDL;

        if (is_on_build_server)
            ExitProcess(0);

        return 0;
    }

    // This is a test failure.
    GW_COUT << "ERROR with echo testing!" << GW_ENDL;

    if (is_on_build_server)
        ExitProcess(1);

    return 0;
}

#endif

// Adds some URI handler: either Apps or Gateway.
uint32_t Gateway::AddUriHandler(
    GatewayWorker* gw,
    HandlersTable* handlers_table,
    uint16_t port,
    const char* uri,
    uint32_t uri_len_chars,
    bmx::HTTP_METHODS http_method,
    BMX_HANDLER_TYPE handler_id,
    int32_t db_index,
    GENERIC_HANDLER_CALLBACK handler_proc)
{
    uint32_t err_code;

    // Registering URI handler.
    err_code = handlers_table->RegisterUriHandler(
        gw,
        port,
        uri,
        uri_len_chars,
        http_method,
        handler_id,
        handler_proc,
        db_index);

    GW_ERR_CHECK(err_code);

    // Search for handler index by URI string.
    BMX_HANDLER_TYPE handler_index = handlers_table->FindUriHandlerIndex(port, uri, uri_len_chars);

    // Getting the port structure.
    ServerPort* server_port = g_gateway.FindServerPort(port);

    // Registering URI on port.
    RegisteredUris* all_port_uris = server_port->get_registered_uris();
    int32_t index = all_port_uris->FindRegisteredUri(uri, uri_len_chars);

    // Checking if there is an entry.
    if (index < 0)
    {
        // Creating totally new URI entry.
        RegisteredUri new_entry(
            uri,
            uri_len_chars,
            db_index,
            handlers_table->get_handler_list(handler_index));

        // Adding entry to global list.
        all_port_uris->AddEntry(new_entry);
    }
    else
    {
        // Obtaining existing URI entry.
        RegisteredUri reg_uri = all_port_uris->GetEntryByIndex(index);

        // Checking if there is no database for this URI.
        if (!reg_uri.ContainsDb(db_index))
        {
            // Creating new unique handlers list for this database.
            UniqueHandlerList uhl(db_index, handlers_table->get_handler_list(handler_index));

            // Adding new handler list for this database to the URI.
            reg_uri.Add(uhl);
        }
    }
    GW_ERR_CHECK(err_code);

    // Printing port information.
    server_port->Print();

    return 0;
}

// Adds some port handler: either Apps or Gateway.
uint32_t Gateway::AddPortHandler(
    GatewayWorker* gw,
    HandlersTable* handlers_table,
    uint16_t port,
    BMX_HANDLER_TYPE handler_id,
    int32_t db_index,
    GENERIC_HANDLER_CALLBACK handler_proc)
{
    return handlers_table->RegisterPortHandler(
        gw,
        port,
        handler_id,
        handler_proc,
        db_index);
}

// Adds some sub-port handler: either Apps or Gateway.
uint32_t Gateway::AddSubPortHandler(
    GatewayWorker* gw,
    HandlersTable* handlers_table,
    uint16_t port,
    bmx::BMX_SUBPORT_TYPE subport,
    BMX_HANDLER_TYPE handler_id,
    int32_t db_index,
    GENERIC_HANDLER_CALLBACK handler_proc)
{
    return handlers_table->RegisterSubPortHandler(
        gw,
        port,
        subport,
        handler_id,
        handler_proc,
        db_index);
}

// Opens Starcounter log for writing.
uint32_t Gateway::OpenStarcounterLog()
{
    uint32_t err_code = sccorelog_init(0);
    if (err_code)
        return err_code;

    err_code = sccorelog_connect_to_logs(GW_PROGRAM_NAME, NULL, &sc_log_handle_);
    if (err_code)
        return err_code;

    err_code = sccorelog_bind_logs_to_dir(sc_log_handle_, setting_output_dir_.c_str());
    if (err_code)
        return err_code;

    return 0;
}

// Closes Starcounter log.
void Gateway::CloseStarcounterLog()
{
    uint32_t err_code = sccorelog_release_logs(sc_log_handle_);

    GW_ASSERT(0 == err_code);
}

// Write critical into log.
void Gateway::LogWriteCritical(const wchar_t* msg)
{
    uint32_t err_code = sccorelog_kernel_write_to_logs(sc_log_handle_, SC_ENTRY_CRITICAL, msg);

    GW_ASSERT(0 == err_code);

    err_code = sccorelog_flush_to_logs(sc_log_handle_);

    GW_ASSERT(0 == err_code);
}

} // namespace network
} // namespace starcounter

VOID SCAPI LogGatewayCrash(VOID *pc, LPCWSTR str)
{
    starcounter::network::g_gateway.LogWriteCritical(str);
}

int wmain(int argc, wchar_t* argv[], wchar_t* envp[])
{
    // Catching all unhandled exceptions in this thread.
    GW_SC_BEGIN_FUNC

    // Setting the critical log handler.
    _SetCriticalLogHandler(LogGatewayCrash, NULL);

    using namespace starcounter::network;
    uint32_t err_code;

    // Processing arguments and initializing log file.
    err_code = g_gateway.ProcessArgumentsAndInitLog(argc, argv);
    if (err_code)
        return err_code;

    //int32_t xxx = 0;
    //int32_t yyy = 123 / xxx;
    //GW_ASSERT(123 == 1);

    // Setting I/O as low priority.
    SetPriorityClass(GetCurrentProcess(), PROCESS_MODE_BACKGROUND_BEGIN);

    // Stating the network gateway.
    err_code = g_gateway.StartGateway();
    if (err_code)
        return err_code;

    // Cleaning up resources.
    err_code = g_gateway.GlobalCleanup();
    if (err_code)
        return err_code;

    //GW_COUT << "Press any key to exit." << GW_ENDL;
    //_getch();

    return 0;

    // Catching all unhandled exceptions in this thread.
    GW_SC_END_FUNC
}