#include "static_headers.hpp"
#include "gateway.hpp"
#include "handlers.hpp"
#include "ws_proto.hpp"
#include "http_proto.hpp"
#include "socket_data.hpp"
#include "tls_proto.hpp"
#include "worker_db_interface.hpp"
#include "worker.hpp"
#include "urimatch_codegen.hpp"

#pragma comment(lib, "Ws2_32.lib")
#pragma comment(lib, "winmm.lib")

namespace starcounter {
namespace network {

// Main network gateway object.
Gateway g_gateway;

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
ServerLoggingSafe::~ServerLoggingSafe()
{
    switch (t_)
    {
        case GW_LOGGING_ERROR_TYPE:
            g_gateway.LogWriteError(ss_.str().c_str());
            break;

        case GW_LOGGING_WARNING_TYPE:
            g_gateway.LogWriteWarning(ss_.str().c_str());
            break;

        case GW_LOGGING_NOTICE_TYPE:
            g_gateway.LogWriteNotice(ss_.str().c_str());
            break;

        case GW_LOGGING_CRITICAL_TYPE:
            g_gateway.LogWriteCritical(ss_.str().c_str());
            break;
    }
    
}

// Writing to log once object is destroyed.
CoutSafe::~CoutSafe()
{
#ifdef GW_LOGGING_ON

    std::string str = ss_.str();
    g_gateway.get_gw_log_writer()->WriteToLog(str.c_str(), static_cast<int32_t>(str.length()));

#endif
}

void GatewayLogWriter::Init(const std::wstring& log_file_path)
{
    log_write_pos_ = 0;

    log_read_pos_ = 0;

    accum_length_ = 0;

    InitializeCriticalSection(&write_lock_);

#ifdef GW_LOG_TO_FILE

    // Opening log file for writes.
    log_file_handle_ = CreateFile(
        (log_file_path).c_str(),
        GENERIC_WRITE,
        FILE_SHARE_READ,
        NULL,
        OPEN_EXISTING,
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

#endif   
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

    // Default inactive socket timeout in seconds.
    setting_inactive_socket_timeout_seconds_ = 60 * 20;

    // Starcounter server type.
    setting_sc_server_type_upper_ = MixedCodeConstants::DefaultPersonalServerNameUpper;

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

    // Reset gateway owner id and pid.
    gateway_owner_id_ = 0;
    gateway_pid_ = 0;

    // No reverse proxies by default.
    num_reversed_proxies_ = 0;

    // Starting linear unique socket with 0.
    unique_socket_id_ = 0;

    // Server address for testing.
    server_addr_ = NULL;

    // Initializing critical sections.
    InitializeCriticalSection(&cs_global_lock_);
    InitializeCriticalSection(&cs_sockets_cleanup_);
    InitializeCriticalSection(&cs_statistics_);

    num_pending_sends_ = 0;
    num_aggregated_sent_messages_ = 0;
    num_aggregated_recv_messages_ = 0;
    num_aggregated_send_queued_messages_ = 0;

    // Creating gateway handlers table.
    gw_handlers_ = new HandlersTable();

    // Initial number of server ports.
    num_server_ports_slots_ = 0;

    // Resetting number of processed HTTP requests.
    num_processed_http_requests_unsafe_ = 0;

    // Initializing to first bind port number.
    last_bind_port_num_unsafe_ = FIRST_BIND_PORT_NUM;

    // First bind interface number.
    last_bind_interface_num_unsafe_ = 0;

    // Resetting Starcounter log handle.
    sc_log_handle_ = MixedCodeConstants::INVALID_SERVER_LOG_HANDLE;

    // Empty global statistics.
    memcpy(global_statistics_string_, kHttpStatsHeader, kHttpStatsHeaderLength);

    codegen_uri_matcher_ = NULL;

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
    setting_mode_ = GatewayTestingMode::MODE_GATEWAY_SMC_HTTP;

    // Number of operations per second.
    num_gateway_ops_per_second_ = 0;

    // Test finish critical section.
    InitializeCriticalSection(&cs_test_finished_);

    // Number of measures.
    num_ops_measures_ = 0;

    cmd_setting_num_workers_ = 0;
    cmd_setting_mode_ = MODE_GATEWAY_UNKNOWN;
    cmd_setting_num_connections_to_master_ = 0;
    cmd_setting_num_echoes_to_master_ = 0;
    cmd_setting_max_test_time_seconds_ = 0;

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
    setting_server_output_dir_ = argv[3];

    char temp[128];

#ifdef GW_TESTING_MODE
    if (argc > 4)
    {
        GW_ASSERT(argc == 10);

        cmd_setting_num_workers_ = _wtoi(argv[4]);
        
        size_t num_written;
        wcstombs_s(&num_written, temp, 128, argv[5], _TRUNCATE);
        cmd_setting_mode_ = GetGatewayTestingMode(temp);
        cmd_setting_num_connections_to_master_ = _wtoi(argv[6]);
        cmd_setting_num_echoes_to_master_ = _wtoi(argv[7]);
        cmd_setting_max_test_time_seconds_ = _wtoi(argv[8]);

        wcstombs_s(&num_written, temp, 128, argv[9], _TRUNCATE);
        cmd_setting_stats_name_ = std::string(temp);
    }
#endif

    // Opening Starcounter log.
    uint32_t err_code = g_gateway.OpenStarcounterLog();
    if (err_code)
        return err_code;

    // Converting Starcounter server type to narrow char.
    size_t conv_chars;
    wcstombs_s(&conv_chars, temp, 128, argv[1], _TRUNCATE);

    // Copying other fields.
    setting_sc_server_type_upper_ = temp;

    // Converting to upper case.
    std::transform(
        setting_sc_server_type_upper_.begin(),
        setting_sc_server_type_upper_.end(),
        setting_sc_server_type_upper_.begin(),
        ::toupper);

    setting_config_file_path_ = argv[2];

    // Getting executable directory.
    wchar_t exe_dir[1024];
    DWORD r = GetModuleFileName(NULL, exe_dir, 1024);
    GW_ASSERT((r > 0) && (r < 1024));

    // Getting directory name from executable path.
    int32_t c = r;
    while (c > 0)
    {
        c--;

        if (exe_dir[c] == L'\\')
            break;
    }
    exe_dir[c] = L'\0';

    // Creating Starcounter bin directory property.
    setting_sc_bin_dir_ = std::wstring(exe_dir);

    // Specifying gateway output directory as sub-folder for server output.
    setting_gateway_output_dir_ = setting_server_output_dir_ + L"\\" + GW_PROGRAM_NAME;

    // Trying to create network gateway log directory.
    if ((!CreateDirectory(setting_gateway_output_dir_.c_str(), NULL)) &&
        (ERROR_ALREADY_EXISTS != GetLastError()))
    {
        std::wcout << L"Can't create network gateway log directory: " << setting_gateway_output_dir_ << std::endl;

        return SCERRGWCANTCREATELOGDIR;
    }

    // Obtaining full path to log directory.
    wchar_t log_file_dir_full[1024];
    DWORD num_copied_chars = GetFullPathName(setting_gateway_output_dir_.c_str(), 1024, log_file_dir_full, NULL);
    GW_ASSERT(num_copied_chars != 0);

    // Full path to gateway log file.
    setting_log_file_path_ = std::wstring(log_file_dir_full) + L"\\" + GW_PROGRAM_NAME + L".log";

    // Deleting old log file first.
    DeleteFile(setting_log_file_path_.c_str());

    return 0;
}

// Initializes server socket.
void ServerPort::Init(int32_t port_index, uint16_t port_number, SOCKET listening_sock)
{
    GW_ASSERT((port_index >= 0) && (port_index < MAX_PORTS_NUM));

    // Allocating needed tables.
    port_handlers_ = new PortHandlers();
    registered_uris_ = new RegisteredUris(port_number);
    registered_subports_ = new RegisteredSubports();

    listening_sock_ = listening_sock;
    port_number_ = port_number;
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
void ServerPort::EraseDb(db_index_type db_index)
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
#ifdef GW_WARNINGS_DIAG
            GW_COUT << "closesocket() failed." << GW_ENDL;
            PrintLastError();
#endif
        }
        listening_sock_ = INVALID_SOCKET;
    }

    if (port_handlers_)
    {
        delete port_handlers_;
        port_handlers_ = NULL;
    }

    if (registered_uris_)
    {
        delete registered_uris_;
        registered_uris_ = NULL;
    }

    if (registered_subports_)
    {
        delete registered_subports_;
        registered_subports_ = NULL;
    }

    port_number_ = INVALID_PORT_NUMBER;
    port_index_ = INVALID_PORT_INDEX;

    Reset();
}

// Printing the registered URIs.
void ServerPort::PrintInfo(std::stringstream& stats_stream)
{
    stats_stream << "Accepting sockets: " << get_num_accepting_sockets() << "<br>";

#ifdef GW_COLLECT_SOCKET_STATISTICS
    stats_stream << "Active connections: " << NumberOfActiveConnections() << "<br>";
    //stats_stream << "Allocated Accept sockets: " << get_num_allocated_accept_sockets() << "<br>";
    //stats_stream << "Allocated Connect sockets: " << get_num_allocated_connect_sockets() << "<br>";
#endif

    stats_stream << "<br>";

    //port_handlers_->PrintRegisteredHandlers(global_port_statistics_stream);
    registered_uris_->PrintRegisteredUris(stats_stream, port_number_);
}

// Printing the database information.
void ActiveDatabase::PrintInfo(std::stringstream& stats_stream)
{
    stats_stream << "Database \"" << db_name_ << "\" (index " << db_index_ << ") info: " << "<br>";
    stats_stream << "Overflow chunks: " << g_gateway.NumberOverflowChunksPerDatabase(db_index_) << "<br><br>";
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
    // Opening file stream.
    std::ifstream config_file_stream;
    config_file_stream.open(configFilePath);
    if (!config_file_stream.is_open())
    {
        config_file_stream.open(GW_DEFAULT_CONFIG_NAME);
        if (!config_file_stream.is_open())
        {
            return SCERRGWCANTLOADXMLSETTINGS;
        }
    }

    // Copying config contents into a string.
    std::stringstream str_stream;
    str_stream << config_file_stream.rdbuf();
    std::string tmp_str = str_stream.str();
    char* config_contents = new char[tmp_str.size() + 1];
    strcpy_s(config_contents, tmp_str.size() + 1, tmp_str.c_str());

    try
    {
        using namespace rapidxml;
        xml_document<> doc; // Character type defaults to char.
        doc.parse<0>(config_contents); // 0 means default parse flags.

        xml_node<> *root_elem = doc.first_node("NetworkGateway");
        if (!root_elem)
        {
            g_gateway.LogWriteCritical(L"Gateway XML: Can't read NetworkGateway property.");
            return SCERRGWCANTLOADXMLSETTINGS;
        }

        // Getting local interfaces.
        xml_node<>* node_elem = root_elem->first_node("BindingIP");
        while(node_elem)
        {
            setting_local_interfaces_.push_back(node_elem->value());
            node_elem = node_elem->next_sibling("BindingIP");
        }

        // Getting workers number.
        node_elem = root_elem->first_node("WorkersNumber");
        if (!node_elem)
        {
            g_gateway.LogWriteCritical(L"Gateway XML: Can't read WorkersNumber property.");
            return SCERRGWCANTLOADXMLSETTINGS;
        }

        setting_num_workers_ = atoi(node_elem->value());
        if (setting_num_workers_ <= 0 || setting_num_workers_ > 16)
        {
            g_gateway.LogWriteCritical(L"Gateway XML: Unsupported WorkersNumber value.");
            return SCERRGWCANTLOADXMLSETTINGS;
        }

#ifdef GW_TESTING_MODE
        if (cmd_setting_num_workers_)
            setting_num_workers_ = cmd_setting_num_workers_;
#endif

        // Getting maximum connection number.
        node_elem = root_elem->first_node("MaxConnections");
        if (!node_elem)
        {
            g_gateway.LogWriteCritical(L"Gateway XML: Can't read MaxConnections property.");
            return SCERRGWCANTLOADXMLSETTINGS;
        }

        setting_max_connections_ = atoi(node_elem->value());
        if (setting_max_connections_ < 10 || setting_max_connections_ > 10000000)
        {
            g_gateway.LogWriteCritical(L"Gateway XML: Unsupported MaxConnections value.");
            return SCERRGWCANTLOADXMLSETTINGS;
        }

        // Getting maximum connection number.
        node_elem = root_elem->first_node("MaximumReceiveContentLength");
        if (!node_elem)
        {
            g_gateway.LogWriteCritical(L"Gateway XML: Can't read MaximumReceiveContentLength property.");
            return SCERRGWCANTLOADXMLSETTINGS;
        }

        setting_maximum_receive_content_length_ = atoi(node_elem->value());
        if (setting_maximum_receive_content_length_ < 4096 || setting_maximum_receive_content_length_ > 67108864)
        {
            g_gateway.LogWriteCritical(L"Gateway XML: Unsupported MaximumReceiveContentLength value.");
            return SCERRGWCANTLOADXMLSETTINGS;
        }

        // Getting inactive socket timeout.
        node_elem = root_elem->first_node("InactiveConnectionTimeout");
        if (!node_elem)
        {
            g_gateway.LogWriteCritical(L"Gateway XML: Can't read InactiveConnectionTimeout property.");
            return SCERRGWCANTLOADXMLSETTINGS;
        }

        setting_inactive_socket_timeout_seconds_ = atoi(node_elem->value());
        if (setting_inactive_socket_timeout_seconds_ <= 0 || setting_inactive_socket_timeout_seconds_ > 100000)
        {
            g_gateway.LogWriteCritical(L"Gateway XML: Unsupported InactiveConnectionTimeout value.");
            return SCERRGWCANTLOADXMLSETTINGS;
        }

        // Getting gateway statistics port number.
        node_elem = root_elem->first_node("GatewayStatisticsPort");
        if (!node_elem)
        {
            g_gateway.LogWriteCritical(L"Gateway XML: Can't read GatewayStatisticsPort property.");
            return SCERRGWCANTLOADXMLSETTINGS;
        }

        setting_gw_stats_port_ = (uint16_t)atoi(node_elem->value());
        if (setting_gw_stats_port_ <= 0 || setting_gw_stats_port_ >= 65536)
        {
            g_gateway.LogWriteCritical(L"Gateway XML: Unsupported GatewayStatisticsPort value.");
            return SCERRGWCANTLOADXMLSETTINGS;
        }

        // Getting aggregation port number.
        node_elem = root_elem->first_node("AggregationPort");
        if (node_elem)
        {
            setting_aggregation_port_ = (uint16_t)atoi(node_elem->value());
            if (setting_aggregation_port_ <= 0 || setting_aggregation_port_ >= 65536)
            {
                g_gateway.LogWriteCritical(L"Gateway XML: Unsupported AggregationPort value.");
                return SCERRGWCANTLOADXMLSETTINGS;
            }
        }

        // Just enforcing minimum socket timeout multiplier.
        if ((setting_inactive_socket_timeout_seconds_ % SOCKET_LIFETIME_MULTIPLIER) != 0)
            return SCERRGWWRONGMAXIDLESESSIONLIFETIME;

        // Setting minimum socket life time.
        min_inactive_socket_life_seconds_ = setting_inactive_socket_timeout_seconds_ / SOCKET_LIFETIME_MULTIPLIER;

        // Initializing global timer.
        global_timer_unsafe_ = min_inactive_socket_life_seconds_;

#ifdef GW_TESTING_MODE

        // Getting master node IP address.
        node_elem = root_elem->first_node("MasterIP");
        GW_ASSERT(node_elem);
        setting_master_ip_ = node_elem->value();
        
        // Master node does not need its own IP.
        if (setting_master_ip_ == "")
            setting_is_master_ = true;
        else
            setting_is_master_ = false;

        // Server test port.
        setting_server_test_port_ = 123;
        GW_ASSERT(setting_server_test_port_ > 0 && setting_server_test_port_ < 65536);

        // Number of connections to establish to master.
        setting_num_connections_to_master_ = cmd_setting_num_connections_to_master_;

        GW_ASSERT((setting_num_connections_to_master_ % (setting_num_workers_ * ACCEPT_ROOF_STEP_SIZE)) == 0);
        setting_num_connections_to_master_per_worker_ = setting_num_connections_to_master_ / setting_num_workers_;

        // Number of echoes to send to master node from clients.
        setting_num_echoes_to_master_ = cmd_setting_num_echoes_to_master_;

        GW_ASSERT(setting_num_echoes_to_master_ <= MAX_TEST_ECHOES);
        ResetEchoTests();

        // Obtaining testing mode.
        setting_mode_ = cmd_setting_mode_;

        // Maximum running time for tests.
        setting_max_test_time_seconds_ = cmd_setting_max_test_time_seconds_;
        GW_ASSERT(setting_max_test_time_seconds_ > 0 && setting_max_test_time_seconds_ < 10000);
        GW_ASSERT((setting_max_test_time_seconds_ % GW_MONITOR_THREAD_TIMEOUT_SECONDS) == 0);

        // Loading statistics name.
        setting_stats_name_ = cmd_setting_stats_name_;

#ifdef GW_LOOPED_TEST_MODE
        switch (setting_mode_)
        {
            case GatewayTestingMode::MODE_GATEWAY_HTTP:
            case GatewayTestingMode::MODE_GATEWAY_SMC_HTTP:
            case GatewayTestingMode::MODE_GATEWAY_SMC_APPS_HTTP:
            {
                looped_echo_request_creator_ = DefaultHttpEchoRequestCreator;
                looped_echo_response_processor_ = DefaultHttpEchoResponseProcessor;

                break;
            }

            case GatewayTestingMode::MODE_GATEWAY_RAW:
            case GatewayTestingMode::MODE_GATEWAY_SMC_RAW:
            case GatewayTestingMode::MODE_GATEWAY_SMC_APPS_RAW:
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
        xml_node<char>* proxies_node = root_elem->first_node("ReverseProxies");
        if (proxies_node)
        {
            xml_node<char>* proxy_node = proxies_node->first_node("ReverseProxy");
            if (!node_elem)
            {
                g_gateway.LogWriteCritical(L"Gateway XML: Can't read ReverseProxy property.");
                return SCERRGWCANTLOADXMLSETTINGS;
            }

            int32_t n = 0;
            while (proxy_node)
            {
                // Filling reverse proxy information.
                node_elem = proxy_node->first_node("DestinationDNS");
                if (!node_elem)
                {
                    node_elem = proxy_node->first_node("DestinationIP");
                    if (!node_elem)
                    {
                        g_gateway.LogWriteCritical(L"Gateway XML: Can't read DestinationIP property. Either DestinationDNS or DestinationIP property should be specified.");
                        return SCERRGWCANTLOADXMLSETTINGS;
                    }
                    reverse_proxies_[n].destination_ip_ = node_elem->value();
                }
                else
                {
                    addrinfo *dns_addr_info = NULL;

                    // Obtaining server IP from DNS name.
                    uint32_t err_code = GetAddrInfoA(node_elem->value(), NULL, NULL, &dns_addr_info);
                    if (err_code)
                    {
                        std::wstring temp = L"Reverse proxy: Can't obtain IP address from DNS name: ";
                        std::wstring ws_temp;
                        std::string s_temp = node_elem->value();
                        ws_temp.assign(s_temp.begin(), s_temp.end());
                        temp += ws_temp;

                        g_gateway.LogWriteCritical(temp.c_str());
                        return SCERRGWCANTLOADXMLSETTINGS;
                    }

                    // Checking if its IPv4 address.
                    if (dns_addr_info->ai_family != AF_INET)
                    {
                        std::wstring temp = L"Reverse proxy: Only resolved IPv4 addresses are supported at the moment: ";
                        std::wstring ws_temp;
                        std::string s_temp = node_elem->value();
                        ws_temp.assign(s_temp.begin(), s_temp.end());
                        temp += ws_temp;

                        g_gateway.LogWriteCritical(temp.c_str());
                        return SCERRGWCANTLOADXMLSETTINGS;
                    }

                    // Getting the first IP address.
                    reverse_proxies_[n].destination_ip_ = inet_ntoa(((struct sockaddr_in *) dns_addr_info->ai_addr)->sin_addr);
                }

                node_elem = proxy_node->first_node("DestinationPort");
                if (!node_elem)
                {
                    g_gateway.LogWriteCritical(L"Gateway XML: Can't read DestinationPort property.");
                    return SCERRGWCANTLOADXMLSETTINGS;
                }

                reverse_proxies_[n].destination_port_ = atoi(node_elem->value());
                if (reverse_proxies_[n].destination_port_ <= 0 || reverse_proxies_[n].destination_port_  >= 65536)
                {
                    g_gateway.LogWriteCritical(L"Gateway XML: Reverse proxy has incorrect DestinationPort number.");
                    return SCERRGWCANTLOADXMLSETTINGS;
                }

                node_elem = proxy_node->first_node("StarcounterProxyPort");
                if (!node_elem)
                {
                    g_gateway.LogWriteCritical(L"Gateway XML: Can't read StarcounterProxyPort property.");
                    return SCERRGWCANTLOADXMLSETTINGS;
                }

                reverse_proxies_[n].sc_proxy_port_ = atoi(node_elem->value());
                if (reverse_proxies_[n].sc_proxy_port_ <= 0 || reverse_proxies_[n].sc_proxy_port_  >= 65536)
                {
                    g_gateway.LogWriteCritical(L"Gateway XML: Reverse proxy has incorrect StarcounterProxyPort number.");
                    return SCERRGWCANTLOADXMLSETTINGS;
                }

                node_elem = proxy_node->first_node("MatchingMethodAndUri");
                reverse_proxies_[n].matching_method_and_uri_ = node_elem->value();
                reverse_proxies_[n].matching_method_and_uri_processed_ = reverse_proxies_[n].matching_method_and_uri_ + " ";

                reverse_proxies_[n].matching_method_and_uri_len_ = static_cast<int32_t> (reverse_proxies_[n].matching_method_and_uri_.length());
                reverse_proxies_[n].matching_method_and_uri_processed_len_ = reverse_proxies_[n].matching_method_and_uri_len_ + 1;

                // Loading proxied servers.
                sockaddr_in* server_addr = &reverse_proxies_[n].destination_addr_;
                memset(server_addr, 0, sizeof(sockaddr_in));
                server_addr->sin_family = AF_INET;
                server_addr->sin_addr.s_addr = inet_addr(reverse_proxies_[n].destination_ip_.c_str());
                server_addr->sin_port = htons(reverse_proxies_[n].destination_port_);

                // Getting next reverse proxy information.
                proxy_node = proxy_node->next_sibling("ReverseProxy");

                n++;
            }

            num_reversed_proxies_ = n;
        }

#endif
    }
    catch (...)
    {
        GW_COUT << "Error loading gateway XML settings!" << GW_ENDL;
        return SCERRGWCANTLOADXMLSETTINGS;
    }

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

    GW_ASSERT(core::chunk_type::link_size == MixedCodeConstants::CHUNK_LINK_SIZE);
    GW_ASSERT(sizeof(core::chunk_type::link_type) == MixedCodeConstants::CHUNK_LINK_SIZE / 2);

    GW_ASSERT(core::chunk_type::static_header_size == MixedCodeConstants::BMX_HEADER_MAX_SIZE_BYTES);
    GW_ASSERT(core::chunk_type::static_data_size == MixedCodeConstants::SOCKET_DATA_MAX_SIZE);

    GW_ASSERT(sizeof(ScSessionStruct) == MixedCodeConstants::SESSION_STRUCT_SIZE);

    GW_ASSERT(CONTENT_LENGTH_HEADER_VALUE_8BYTES == *(int64_t*)"Content-Length: ");
    GW_ASSERT(UPGRADE_HEADER_VALUE_8BYTES == *(int64_t*)"Upgrade:");
    GW_ASSERT(WEBSOCKET_HEADER_VALUE_8BYTES == *(int64_t*)"Sec-WebSocket: ");
    GW_ASSERT(REFERER_HEADER_VALUE_8BYTES == *(int64_t*)"Referer: ");
    GW_ASSERT(XREFERER_HEADER_VALUE_8BYTES == *(int64_t*)"X-Referer: ");

    GW_ASSERT(0 == (sizeof(ScSocketInfoStruct) % MEMORY_ALLOCATION_ALIGNMENT));

    GW_ASSERT(GatewayChunkSizes[NumGatewayChunkSizes - 1] > (MixedCodeConstants::MAX_EXTRA_LINKED_IPC_CHUNKS + 1) * MixedCodeConstants::CHUNK_MAX_DATA_BYTES);

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
    if (!gw)
        iocp = g_gateway.get_iocp();
    else
        iocp = gw->get_worker_iocp();

    // Attaching socket to IOCP.
    HANDLE temp = CreateIoCompletionPort((HANDLE) sock, iocp, 0, setting_num_workers_);
    if (temp != iocp)
    {
        PrintLastError(true);
        closesocket(sock);
        sock = INVALID_SOCKET;

        GW_COUT << "Wrong IOCP returned when adding reference." << GW_ENDL;
        return SCERRGWFAILEDTOATTACHSOCKETTOIOCP;
    }

#ifdef GW_IOCP_IMMEDIATE_COMPLETION
    // Skipping completion port if operation is already successful.
    SetFileCompletionNotificationModes((HANDLE) sock, FILE_SKIP_COMPLETION_PORT_ON_SUCCESS);
#endif

    // The socket address to be passed to bind.
    sockaddr_in binding_addr;
    memset(&binding_addr, 0, sizeof(sockaddr_in));
    binding_addr.sin_family = AF_INET;
    binding_addr.sin_port = htons(port_num);

    // Checking if we have local interfaces to bind.
    if (g_gateway.setting_local_interfaces().size() > 0)
        binding_addr.sin_addr.s_addr = inet_addr(g_gateway.setting_local_interfaces().at(0).c_str());
    else
        binding_addr.sin_addr.s_addr = INADDR_ANY;

    // Binding socket to certain interface and port.
    if (bind(sock, (SOCKADDR*) &binding_addr, sizeof(binding_addr)))
    {
        PrintLastError(true);
        closesocket(sock);
        sock = INVALID_SOCKET;
       
        GW_LOG_ERROR << L"Failed to bind server port: " << port_num << L". Please check that port is not occupied by any software." << GW_WENDL;
        return SCERRGWFAILEDTOBINDPORT;
    }

    // Listening to connections.
    if (listen(sock, SOMAXCONN))
    {
        PrintLastError(true);
        closesocket(sock);
        sock = INVALID_SOCKET;

        GW_COUT << "Error listening on server socket." << GW_ENDL;
        return SCERRGWFAILEDTOLISTENONSOCKET;
    }

    return 0;
}

// Creates new connections on all workers.
uint32_t Gateway::CreateNewConnectionsAllWorkers(int32_t how_many, uint16_t port_num, db_index_type db_index)
{
    // Getting server port index.
    int32_t port_index = g_gateway.FindServerPortIndex(port_num);

    // Creating new connections for each worker.
    for (int32_t i = 0; i < setting_num_workers_; i++)
    {
        uint32_t err_code = gw_workers_[i].CreateNewConnections(how_many, port_index);
        if (err_code)
            return err_code;
    }

    return 0;
}

// Applying session parameters to socket data.
bool Gateway::ApplySocketInfoToSocketData(SocketDataChunkRef sd, session_index_type socket_index, random_salt_type unique_socket_id)
{
    GW_ASSERT(socket_index < setting_max_connections_);

    if (CompareUniqueSocketId(socket_index, unique_socket_id))
    {
        ScSocketInfoStruct si = all_sockets_infos_unsafe_[socket_index];

        sd->AssignSession(si.session_);
        sd->set_unique_socket_id(si.unique_socket_id_);
        sd->set_socket_info_index(si.read_only_index_);
        sd->set_type_of_network_protocol((MixedCodeConstants::NetworkProtocolType)si.type_of_network_protocol_);

        return true;
    }

    return false;
}

// Gets free socket index.
session_index_type Gateway::ObtainFreeSocketIndex(
    GatewayWorker* gw,
    SOCKET s,
    int32_t port_index,
    bool proxy_connect_socket)
{
    PSLIST_ENTRY free_socket_index_entry = InterlockedPopEntrySList(free_socket_indexes_unsafe_);
    GW_ASSERT(free_socket_index_entry);

    // NOTE: Socket info has the same address as socket index entry.
    ScSocketInfoStruct* si = (ScSocketInfoStruct*) free_socket_index_entry;

    // Resetting the socket structure.
    si->Reset();

    // Marking socket as alive.
    si->socket_ = s;

    // Checking if this socket is used for connecting to remote machine.
    if (proxy_connect_socket)
        si->set_socket_proxy_connect_flag();

    // Creating new socket info.
    CreateNewSocketInfo(si->read_only_index_, port_index, gw->get_worker_id());

    // Creating unique ids.
    GenerateUniqueSocketInfoIds(si->read_only_index_, gw->GenerateSchedulerId());

    return si->read_only_index_;
}

// Stub APC function that does nothing.
void __stdcall EmptyApcFunction(ULONG_PTR arg) {
    // Does nothing.
}

// Waking up a thread using APC.
void WakeUpThreadUsingAPC(HANDLE thread_handle)
{
    // Waking up the worker thread with APC.
    QueueUserAPC(EmptyApcFunction, thread_handle, 0);
}

// Database channels events monitor thread.
uint32_t __stdcall DatabaseChannelsEventsMonitorRoutine(LPVOID params)
{
    // Index of the database as parameter.
    db_index_type db_index = *(db_index_type*)params;

    // Determine the worker by interface or channel,
    // and wake up that worker.
	HANDLE worker_thread_handle[MAX_WORKER_THREADS];
	HANDLE work_events[MAX_WORKER_THREADS];
	std::size_t work_event_index = 0;
	
    // Setting checking events for each worker.
	for (int32_t worker_id = 0; worker_id < g_gateway.setting_num_workers(); ++worker_id)
    {
		WorkerDbInterface* db_int = g_gateway.get_worker(worker_id)->GetWorkerDb(db_index);
		core::shared_interface* db_shared_int = db_int->get_shared_int();

        // Creating work event handle.
		work_events[worker_id] = db_shared_int->open_client_work_event(db_shared_int->get_client_number());

		// Waiting for all channels.
		// TODO: Unset notify flag when worker thread is spinning.
		db_shared_int->client_interface().set_notify_flag(true);

		// Sending APC on the determined worker.
		worker_thread_handle[worker_id] = g_gateway.get_worker_thread_handle(worker_id);
        
		// Waking up the worker thread with APC.
        WakeUpThreadUsingAPC(worker_thread_handle[worker_id]);
	}

    // Obtaining client interface.
    core::client_interface_type& client_int = g_gateway.get_worker(0)->GetWorkerDb(db_index)->get_shared_int()->client_interface();

    // Looping until the database dies (TODO, does not work, forcedly killed).
    while (true)
    {
        // Waiting forever for more events on channels.
        client_int.wait_for_work(work_event_index, work_events, g_gateway.setting_num_workers());

        // Waking up the worker thread with APC.
        WakeUpThreadUsingAPC(worker_thread_handle[work_event_index]);
    }

    return 0;
}

// Checks for new/existing databases and updates corresponding shared memory structures.
uint32_t Gateway::CheckDatabaseChanges(const std::set<std::string>& active_databases)
{
    int32_t server_name_len = static_cast<int32_t> (setting_sc_server_type_upper_.length());

    // Enabling database down tracking flag.
    for (int32_t i = 0; i < num_dbs_slots_; i++)
        db_did_go_down_[i] = true;

    // Reading file line by line.
    uint32_t err_code;
    
    // Running through all databases.
    for (std::set<std::string>::iterator it = active_databases.begin();
        it != active_databases.end();
        ++it)
    {
        // Getting current database.
        std::string current_db_name = *it;

        // Skipping incorrect database names.
        if (current_db_name.compare(0, server_name_len, setting_sc_server_type_upper_) != 0)
            continue;

        // Extracting the database name (skipping the server name and underscore).
        current_db_name = current_db_name.substr(server_name_len + 1, current_db_name.length() - server_name_len);

        bool is_new_database = true;
        for (int32_t i = 0; i < num_dbs_slots_; i++)
        {
            // Checking if it is an existing database.
            if (0 == active_databases_[i].get_db_name().compare(current_db_name))
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
            {
                GW_COUT << "Ignoring database '" << current_db_name << "' since we are in server mode." << GW_ENDL;
                is_new_database = false;
            }
        }
        else
        {
            if (current_db_name == "MYDB_SERVER")
            {
                GW_COUT << "Ignoring database '" << current_db_name << "' since we are in client mode." << GW_ENDL;
                is_new_database = false;
            }
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

            GW_ASSERT(empty_db_index < MAX_ACTIVE_DATABASES);

            // Filling necessary fields.
            active_databases_[empty_db_index].Init(
                current_db_name,
                ++db_seq_num_,
                empty_db_index);

            db_did_go_down_[empty_db_index] = false;

            // Increasing number of active databases.
            if (empty_db_index >= num_dbs_slots_)
                num_dbs_slots_++;

            // Adding to workers database interfaces.
            bool db_init_failed = false;
            for (int32_t i = 0; i < setting_num_workers_; i++)
            {
                try
                {
                    err_code = gw_workers_[i].AddNewDatabase(empty_db_index);
                }
                catch (...)
                {
                    // Reporting a warning to server log.
                    std::wstring temp_str = std::wstring(L"Attaching new database failed: ") +
                        std::wstring(current_db_name.begin(), current_db_name.end());
                    g_gateway.LogWriteWarning(temp_str.c_str());

                    // Deleting worker database parts.
                    for (int32_t i = 0; i < setting_num_workers_; i++)
                        gw_workers_[i].DeleteInactiveDatabase(empty_db_index);

                    // Resetting newly created database.
                    active_databases_[empty_db_index].Reset(true);

                    // Removing the database slot if it was the last.
                    if (num_dbs_slots_ == empty_db_index + 1)
                        num_dbs_slots_--;

                    // Leaving global lock.
                    LeaveGlobalLock();

                    db_init_failed = true;
                    
                    break;
                }
                
                if (err_code)
                {
                    // Leaving global lock.
                    LeaveGlobalLock();
                    return err_code;
                }
            }

            // Checking if any error occurred.
            if (db_init_failed)
                continue;

            // Spawning channels events monitor.
            err_code = active_databases_[empty_db_index].SpawnChannelsEventsMonitor();
            if (err_code)
            {
                // Leaving global lock.
                LeaveGlobalLock();
                return err_code;
            }

#ifdef GW_TESTING_MODE

            uint16_t port_number = g_gateway.setting_server_test_port();
            if (!g_gateway.setting_is_master())
                port_number++;

            // Checking if we are in Gateway HTTP mode.
            switch (g_gateway.setting_mode())
            {
                // Registering pure gateway handler here.
                case GatewayTestingMode::MODE_GATEWAY_RAW:
                {
                    // Registering port handler.
                    err_code = AddPortHandler(
                        &gw_workers_[0],
                        gw_handlers_,
                        port_number,
                        bmx::BMX_INVALID_HANDLER_INFO,
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

                // Registering pure gateway handler here.
                case GatewayTestingMode::MODE_GATEWAY_HTTP:
                {
                    // Registering URI handlers.
                    err_code = AddUriHandler(
                        &gw_workers_[0],
                        gw_handlers_,
                        port_number,
                        http_tests_information_[g_gateway.setting_mode()].method_and_uri_info,
                        http_tests_information_[g_gateway.setting_mode()].method_and_uri_info_len,
                        http_tests_information_[g_gateway.setting_mode()].method_and_uri_info,
                        http_tests_information_[g_gateway.setting_mode()].method_and_uri_info_len,
                        NULL,
                        0,
                        bmx::BMX_INVALID_HANDLER_INFO,
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
            }

#else

            // Only registering gateway handlers with Administrator database which is first.
            if (0 == empty_db_index)
            {
#ifdef GW_PROXY_MODE

                // Registering all proxies.
                for (int32_t i = 0; i < num_reversed_proxies_; i++)
                {
                    // Registering URI handlers.
                    err_code = AddUriHandler(
                        &gw_workers_[0],
                        gw_handlers_,
                        reverse_proxies_[i].sc_proxy_port_,
                        reverse_proxies_[i].matching_method_and_uri_.c_str(),
                        reverse_proxies_[i].matching_method_and_uri_len_,
                        reverse_proxies_[i].matching_method_and_uri_processed_.c_str(),
                        reverse_proxies_[i].matching_method_and_uri_processed_len_,
                        NULL,
                        0,
                        bmx::BMX_INVALID_HANDLER_INFO,
                        empty_db_index,
                        GatewayUriProcessProxy,
                        false,
                        reverse_proxies_ + i);

                    if (err_code)
                    {
                        // Leaving global lock.
                        LeaveGlobalLock();

                        return err_code;
                    }
                }

#endif

                // Registering URI handler for gateway statistics.
                err_code = AddUriHandler(
                    &gw_workers_[0],
                    gw_handlers_,
                    setting_gw_stats_port_,
                    "GET /gwstats",
                    12,
                    "GET /gwstats ",
                    13,
                    NULL,
                    0,
                    bmx::BMX_INVALID_HANDLER_INFO,
                    empty_db_index,
                    GatewayStatisticsInfo,
                    true);

                if (err_code)
                {
                    // Leaving global lock.
                    LeaveGlobalLock();

                    return err_code;
                }

                // Registering URI handler for socket resource creation.
                err_code = AddUriHandler(
                    &gw_workers_[0],
                    gw_handlers_,
                    setting_gw_stats_port_,
                    "POST /socket",
                    12,
                    "POST /socket ",
                    13,
                    NULL,
                    0,
                    bmx::BMX_INVALID_HANDLER_INFO,
                    empty_db_index,
                    PostSocketResource,
                    true);

                if (err_code)
                {
                    // Leaving global lock.
                    LeaveGlobalLock();

                    return err_code;
                }

                // Registering URI handler for socket resource deletion.
                err_code = AddUriHandler(
                    &gw_workers_[0],
                    gw_handlers_,
                    setting_gw_stats_port_,
                    "DELETE /socket",
                    14,
                    "DELETE /socket ",
                    15,
                    NULL,
                    0,
                    bmx::BMX_INVALID_HANDLER_INFO,
                    empty_db_index,
                    DeleteSocketResource,
                    true);

                if (err_code)
                {
                    // Leaving global lock.
                    LeaveGlobalLock();

                    return err_code;
                }

                if (0 != setting_aggregation_port_)
                {
                    // Registering port handler for aggregation.
                    err_code = AddPortHandler(
                        &gw_workers_[0],
                        gw_handlers_,
                        setting_aggregation_port_,
                        bmx::BMX_INVALID_HANDLER_INFO,
                        empty_db_index,
                        PortAggregator);

                    if (err_code)
                    {
                        // Leaving global lock.
                        LeaveGlobalLock();

                        return err_code;
                    }
                }
            }

#endif

            // Leaving global lock.
            LeaveGlobalLock();

            // Registering gateway ready on first worker.
            for (int32_t i = 0; i < setting_num_workers_; i++)
            {
                err_code = gw_workers_[i].GetWorkerDb(empty_db_index)->SetGatewayReadyForDbPushes();
                GW_ERR_CHECK(err_code);
            }
        }
    }

    // Checking what databases went down.
    for (int32_t s = 0; s < num_dbs_slots_; s++)
    {
        if ((db_did_go_down_[s]) && (!active_databases_[s].IsDeletionStarted()))
        {
#ifdef GW_DATABASES_DIAG
            GW_PRINT_GLOBAL << "Start detaching dead database: " << active_databases_[s].get_db_name() << GW_ENDL;
#endif

            // Entering global lock.
            EnterGlobalLock();

            // Killing channels events monitor thread.
            active_databases_[s].KillChannelsEventsMonitor();

            // Start database deletion.
            active_databases_[s].StartDeletion();

            // Leaving global lock.
            LeaveGlobalLock();
        }
    }

    return 0;
}

// Active database constructor.
ActiveDatabase::ActiveDatabase()
{
    user_handlers_ = NULL;
    num_holding_workers_ = 0;
    InitializeCriticalSection(&cs_db_checks_);

    StartDeletion();
}

// Initializes this active database slot.
void ActiveDatabase::Init(
    std::string db_name,
    uint64_t unique_num,
    db_index_type db_index)
{
    // Creating fresh handlers table.
    user_handlers_ = new HandlersTable();

    db_name_ = db_name;
    unique_num_unsafe_ = unique_num;
    db_index_ = db_index;

    is_empty_ = false;
    is_ready_for_cleanup_ = false;

    num_holding_workers_ = g_gateway.setting_num_workers();

    // Construct the database_shared_memory_parameters_name. The format is
    // <DATABASE_NAME_PREFIX>_<SERVER_TYPE>_<DATABASE_NAME>_0
    std::string shm_params_name = (std::string)DATABASE_NAME_PREFIX + "_" +
        g_gateway.setting_sc_server_type_upper() + "_" + StringToUpperCopy(db_name_) + "_0";

    // Open the database shared memory parameters file and obtains a pointer to
    // the shared structure.
    core::database_shared_memory_parameters_ptr db_shm_params(shm_params_name.c_str());

    // Name of the database shared memory segment.
    char seq_num_str[16];
    _itoa_s(db_shm_params->get_sequence_number(), seq_num_str, 16, 10);
    shm_seg_name_ = std::string(DATABASE_NAME_PREFIX) + "_" +
        g_gateway.setting_sc_server_type_upper() + "_" +
        StringToUpperCopy(db_name_) + "_" +
        std::string(seq_num_str);
}

// Resets database slot.
void ActiveDatabase::Reset(bool hard_reset)
{
    // Removing handlers table.
    if (user_handlers_)
    {
        delete user_handlers_;
        user_handlers_ = NULL;
    }

    unique_num_unsafe_ = INVALID_UNIQUE_DB_NUMBER;
    db_name_ = "";

    if (hard_reset)
    {
        is_empty_ = true;
    }
}

// Checks if this database slot empty.
bool ActiveDatabase::IsEmpty()
{
    if (is_empty_)
        return true;

    EnterCriticalSection(&cs_db_checks_);

    // Checking if all chunks for this database were released.
    is_empty_ = (num_holding_workers_ == 0) && IsReadyForCleanup();

    LeaveCriticalSection(&cs_db_checks_);

    return is_empty_;
}

bool ActiveDatabase::IsReadyForCleanup()
{
    if (is_ready_for_cleanup_)
        return true;

    EnterCriticalSection(&cs_db_checks_);

    // Checking if all chunks for this database were released.
    is_ready_for_cleanup_ =
        (INVALID_UNIQUE_DB_NUMBER == unique_num_unsafe_) &&
        (0 == g_gateway.NumberOverflowChunksPerDatabase(db_index_));

    LeaveCriticalSection(&cs_db_checks_);

    return is_ready_for_cleanup_;
}

// Makes this database slot empty.
void ActiveDatabase::StartDeletion()
{
    // Deleting all associated info with this database from ports.
    uint err_code = g_gateway.EraseDatabaseFromPorts(db_index_);
    GW_ASSERT(0 == err_code);

    // Resetting slot.
    Reset(false);
}

// Initializes WinSock, all core data structures, binds server sockets.
uint32_t Gateway::Init()
{
    // Allocating data for sockets infos.
    all_sockets_infos_unsafe_ = (ScSocketInfoStruct*) _aligned_malloc(sizeof(ScSocketInfoStruct) * setting_max_connections_, MEMORY_ALLOCATION_ALIGNMENT);
    free_socket_indexes_unsafe_ = (PSLIST_HEADER) _aligned_malloc(sizeof(SLIST_HEADER), MEMORY_ALLOCATION_ALIGNMENT);
    GW_ASSERT(free_socket_indexes_unsafe_);
    InitializeSListHead(free_socket_indexes_unsafe_);

    gateway_mem_chunks_unsafe_ = (PSLIST_HEADER) _aligned_malloc(sizeof(SLIST_HEADER), MEMORY_ALLOCATION_ALIGNMENT);
    GW_ASSERT(gateway_mem_chunks_unsafe_);
    InitializeSListHead(gateway_mem_chunks_unsafe_);

    sockets_to_cleanup_unsafe_ = new session_index_type[setting_max_connections_];
    num_active_sockets_ = 0;
    num_sockets_to_cleanup_unsafe_ = 0;

    // Cleaning all socket infos and setting indexes.
    for (int32_t i = setting_max_connections_ - 1; i >= 0; i--)
    {
        // Resetting all sockets infos.
        all_sockets_infos_unsafe_[i].Reset();
        all_sockets_infos_unsafe_[i].read_only_index_ = i;

        // Pushing to free indexes list.
        InterlockedPushEntrySList(free_socket_indexes_unsafe_, &(all_sockets_infos_unsafe_[i].free_socket_indexes_entry_));
    }

    // Checking if already initialized.
    GW_ASSERT((gw_workers_ == NULL) && (worker_thread_handles_ == NULL));

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
        for (int32_t p = 0; p < num_server_ports_slots_; p++)
        {
            // Skipping empty port.
            if (server_ports_[p].IsEmpty())
                continue;

            SOCKET server_socket = INVALID_SOCKET;

            // Creating socket and binding to port (only on the first worker).
            uint32_t err_code = CreateListeningSocketAndBindToPort(
                &gw_workers_[0],
                server_ports_[p].get_port_number(),
                server_socket);

            GW_ERR_CHECK(err_code);
        }

#ifdef GW_TESTING_MODE
    }

#endif

    // Obtaining function pointers (AcceptEx, ConnectEx, DisconnectEx).
    uint32_t temp;
    SOCKET temp_socket = WSASocket(AF_INET, SOCK_STREAM, IPPROTO_TCP, NULL, 0, WSA_FLAG_OVERLAPPED);
    if (temp_socket == INVALID_SOCKET)
    {
        GW_COUT << "WSASocket() failed." << GW_ENDL;
        return PrintLastError();
    }

    if (WSAIoctl(temp_socket, SIO_GET_EXTENSION_FUNCTION_POINTER, &AcceptExGuid, sizeof(AcceptExGuid), &AcceptExFunc, sizeof(AcceptExFunc), (LPDWORD)&temp, NULL, NULL))
    {
        GW_COUT << "Failed WSAIoctl(AcceptEx)." << GW_ENDL;
        return PrintLastError();
    }

    if (WSAIoctl(temp_socket, SIO_GET_EXTENSION_FUNCTION_POINTER, &ConnectExGuid, sizeof(ConnectExGuid), &ConnectExFunc, sizeof(ConnectExFunc), (LPDWORD)&temp, NULL, NULL))
    {
        GW_COUT << "Failed WSAIoctl(ConnectEx)." << GW_ENDL;
        return PrintLastError();
    }

    if (WSAIoctl(temp_socket, SIO_GET_EXTENSION_FUNCTION_POINTER, &DisconnectExGuid, sizeof(DisconnectExGuid), &DisconnectExFunc, sizeof(DisconnectExFunc), (LPDWORD)&temp, NULL, NULL))
    {
        GW_COUT << "Failed WSAIoctl(DisconnectEx)." << GW_ENDL;
        return PrintLastError();
    }
    closesocket(temp_socket);

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
        server_addr_->sin_port = htons(g_gateway.setting_server_test_port());
    }

    InitTestHttpEchoRequests();

#endif

    // Loading URI codegen matcher.
    codegen_uri_matcher_ = new CodegenUriMatcher();
    codegen_uri_matcher_->Init();

    // Loading Clang for URI matching.
    HMODULE clang_dll = LoadLibrary(L"GatewayClang.dll");
    GW_ASSERT(clang_dll != NULL);

    typedef void (*GwClangInit)();
    GwClangInit clang_init = (GwClangInit) GetProcAddress(clang_dll, "GwClangInit");
    GW_ASSERT(clang_init != NULL);
    clang_init();

    ClangCompileAndGetFunc = (GwClangCompileCodeAndGetFuntion) GetProcAddress(
        clang_dll,
        "GwClangCompileCodeAndGetFuntion");

    GW_ASSERT(ClangCompileAndGetFunc != NULL);

    ClangDestroyEngineFunc = (ClangDestroyEngineType) GetProcAddress(
        clang_dll,
        "GwClangDestroyEngine");

    GW_ASSERT(ClangDestroyEngineFunc != NULL);

    // Running a test compilation.
    typedef int (*example_main_func_type) ();
    example_main_func_type example_main_func;
    void* clang_engine = NULL;
    void** clang_engine_addr = &clang_engine;
    example_main_func = (example_main_func_type) g_gateway.ClangCompileAndGetFunc(
        clang_engine_addr,
        "int main() { return 124; }",
        "main",
        false);

    GW_ASSERT(example_main_func != NULL);

    g_gateway.ClangDestroyEngineFunc(clang_engine);

    // Registering shared memory monitor interface.
    shm_monitor_int_name_ = setting_sc_server_type_upper_ + "_" + MONITOR_INTERFACE_SUFFIX;

    // Waiting until we can open shared memory monitor interface.
    GW_COUT << "Opening scipcmonitor interface: ";

    // Get monitor_interface_ptr for monitor_interface_name.
    shm_monitor_interface_.init(shm_monitor_int_name_.c_str());
    GW_COUT << "opened!" << GW_ENDL;

#if 0
    // Send registration request to the monitor and try to acquire an owner_id.
    // Without an owner_id we can not proceed and have to exit.
    // Get process id and store it in the monitor_interface.
    gateway_pid_.set_current();

    // Try to register gateway process pid. Wait up to 10000 ms.
    uint32_t err_code = shm_monitor_interface_->register_client_process(gateway_pid_, gateway_owner_id_, 10000/*ms*/);
    GW_ASSERT(0 == err_code);
#else
    gateway_pid_.set_current();
	gateway_owner_id_ = 3;
#endif

    // Indicating that network gateway is ready
    // (should be first line of the output).
    GW_COUT << "Gateway is ready!" << GW_ENDL;

    // Indicating begin of new logging session.
    time_t raw_time;
    time(&raw_time);
    tm timeinfo;
    localtime_s(&timeinfo, &raw_time);
    char temp_time_str[32];
    asctime_s(temp_time_str, 32, &timeinfo);
    GW_PRINT_GLOBAL << "New logging session: " << temp_time_str << GW_ENDL;

    return 0;
}

// Printing statistics for all workers.
void Gateway::PrintWorkersStatistics(std::stringstream& stats_stream)
{
    // Emptying the statistics stream.
    stats_stream.str(std::string());

    // Going through all ports.
    stats_stream << "<h4>WORKERS</h4>";
    for (int32_t w = 0; w < setting_num_workers_; w++)
    {
        stats_stream << "<b>Worker " << w << ":</b><br>";

        gw_workers_[w].PrintInfo(stats_stream);

        stats_stream << "<br>";
    }
}

// Printing statistics for all ports.
void Gateway::PrintPortStatistics(std::stringstream& stats_stream)
{
    // Emptying the statistics stream.
    stats_stream.str(std::string());

    // Going through all ports.
    stats_stream << "<h4>PORTS</h4>";
    for (int32_t p = 0; p < num_server_ports_slots_; p++)
    {
        // Checking that port is not empty.
        if (!server_ports_[p].IsEmpty())
        {
            stats_stream << "<b>Port " << server_ports_[p].get_port_number() << ":</b><br>";

            server_ports_[p].PrintInfo(stats_stream);

            stats_stream << "<br>";
        }
    }
}

// Printing statistics for all databases.
void Gateway::PrintDatabaseStatistics(std::stringstream& stats_stream)
{
    // Emptying the statistics stream.
    stats_stream.str(std::string());

    // Going through all databases.
    stats_stream << "<h4>DATABASES</h4>";
    for (int32_t d = 0; d < num_dbs_slots_; d++)
    {
        if (!active_databases_[d].IsEmpty())
        {
            active_databases_[d].PrintInfo(stats_stream);
        }
    }
}

// Current global statistics value.
const char* Gateway::GetGlobalStatisticsString(int32_t* out_stats_len_bytes)
{
    *out_stats_len_bytes = 0;

    EnterCriticalSection(&cs_statistics_);

    // Printing port information.
    PrintPortStatistics(global_port_statistics_stream_);

    // Printing database statistics.
    PrintDatabaseStatistics(global_databases_statistics_stream_);

    // Printing workers statistics.
    PrintWorkersStatistics(global_workers_statistics_stream_);

    // Filing everything into one stream.
    std::stringstream all_stats_stream;
    all_stats_stream << global_port_statistics_stream_.str();
    all_stats_stream << global_databases_statistics_stream_.str();
    all_stats_stream << global_workers_statistics_stream_.str();
    all_stats_stream << global_statistics_stream_.str();

    // Total number of bytes in HTTP response.
    int32_t total_response_bytes = kHttpStatsHeaderLength;

    // Getting number of written bytes to the stream.
    int32_t all_stats_bytes = static_cast<int32_t> (all_stats_stream.tellp());
    total_response_bytes += all_stats_bytes;

    // Checking for not too big statistics.
    if (total_response_bytes >= MAX_STATS_LENGTH)
    {
        all_stats_bytes = MAX_STATS_LENGTH - kHttpStatsHeaderLength;
        total_response_bytes = MAX_STATS_LENGTH;
    }

    // Copying characters from stream to given buffer.
    all_stats_stream.seekg(0);
    all_stats_stream.rdbuf()->sgetn(global_statistics_string_ + kHttpStatsHeaderLength, all_stats_bytes);

    // Sealing the string.
    global_statistics_string_[total_response_bytes] = '\0';

    // Making length a white space.
    *(uint64_t*)(global_statistics_string_ + kHttpStatsHeaderInsertPoint) = 0x2020202020202020;
    
    // Converting content length to string.
    WriteUIntToString(global_statistics_string_ + kHttpStatsHeaderInsertPoint, total_response_bytes - kHttpStatsHeaderLength);

    LeaveCriticalSection(&cs_statistics_);

    // Calculating final data length in bytes.
    *out_stats_len_bytes = total_response_bytes;

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
uint32_t Gateway::EraseDatabaseFromPorts(db_index_type db_index)
{
    // Going through all ports.
    for (int32_t i = 0; i < num_server_ports_slots_; i++)
    {
        // Checking that port is not empty.
        if (!server_ports_[i].IsEmpty())
        {
            // Deleting port handlers if any.
            server_ports_[i].EraseDb(db_index);
        }
    }

    // Removing empty server ports.
    CleanUpEmptyPorts();

    return 0;
}

// Cleans up empty ports.
void Gateway::CleanUpEmptyPorts()
{
    // Going through all ports.
    for (int32_t i = 0; i < num_server_ports_slots_; i++)
    {
        // Checking if port is not used anywhere.
        if (server_ports_[i].IsEmpty())
            server_ports_[i].Erase();
    }

    // Removing deleted trailing server ports.
    for (int32_t i = (num_server_ports_slots_ - 1); i >= 0; i--)
    {
        // Removing until one server port is not empty.
        if (!server_ports_[i].IsEmpty())
            break;

        num_server_ports_slots_--;
    }
}

// Getting the number of created sockets.
int64_t Gateway::NumberCreatedSocketsAllWorkers()
{
    int64_t num_used_sockets = 0;

    for (int32_t w = 0; w < setting_num_workers_; w++)
        num_used_sockets += gw_workers_[w].get_worker_stats_num_bound_sockets();

    return num_used_sockets;
}

// Getting the number of reusable connect sockets.
int64_t Gateway::NumberOfReusableConnectSockets()
{
    int64_t num_reusable_connect_sockets = 0;

    for (int32_t w = 0; w < setting_num_workers_; w++)
        num_reusable_connect_sockets += gw_workers_[w].NumberOfReusableConnectSockets();

    return num_reusable_connect_sockets;
}

// Getting the total number of overflow chunks for all databases.
int64_t Gateway::NumberOverflowChunksAllWorkersAndDatabases()
{
    int64_t num_overflow_chunks = 0;

    for (int32_t d = 0; d < num_dbs_slots_; d++)
        num_overflow_chunks += NumberOverflowChunksPerDatabase(d);

    return num_overflow_chunks;
}

// Getting the number of overflow chunks per database.
int64_t Gateway::NumberOverflowChunksPerDatabase(db_index_type db_index)
{
    int64_t num_overflow_chunks = 0;

    for (int32_t w = 0; w < setting_num_workers_; w++)
        num_overflow_chunks += gw_workers_[w].NumberOverflowChunksPerDatabasePerWorker(db_index);

    return num_overflow_chunks;
}

#ifdef GW_COLLECT_SOCKET_STATISTICS

// Getting the number of active connections per port.
int64_t Gateway::NumberOfActiveConnectionsPerPort(int32_t port_index)
{
    int64_t num_active_conns = 0;

    for (int32_t w = 0; w < setting_num_workers_; w++)
        num_active_conns += gw_workers_[w].NumberOfActiveConnectionsPerPortPerWorker(port_index);

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
        WakeUpThreadUsingAPC(worker_thread_handle);
    }
}

// Opens active databases events with monitor.
uint32_t Gateway::OpenActiveDatabasesUpdatedEvent()
{
    // Number of characters in the multi-byte string after being converted.
    std::size_t length;

    // Construct the active_databases_updated_event_name.
    char active_databases_updated_event_name[active_databases_updated_event_name_size];

    // Format: "Local\<server_name>_ipc_monitor_cleanup_event".
    // Example: "Local\PERSONAL_ipc_monitor_cleanup_event"
    if ((length = _snprintf_s(active_databases_updated_event_name, _countof
        (active_databases_updated_event_name), active_databases_updated_event_name_size
        -1 /* null */, "Local\\%s_"ACTIVE_DATABASES_UPDATED_EVENT, setting_sc_server_type_upper_.c_str())) < 0) {
            return SCERRFORMATACTIVEDBUPDATEDEVNAME;
    }
    active_databases_updated_event_name[length] = '\0';

    wchar_t w_active_databases_updated_event_name[active_databases_updated_event_name_size];

    if ((mbstowcs_s(&length, w_active_databases_updated_event_name, active_databases_updated_event_name_size,
        active_databases_updated_event_name, _TRUNCATE)) != 0) {
            // Failed to convert active_databases_updated_event_name to multi-byte string.
            return SCERRCONVERTACTIVEDBUPDATEDEVMBS;
    }
    w_active_databases_updated_event_name[length] = L'\0';

    // Open the active_databases_updated_event_name.
    if ((active_databases_updates_event() = ::OpenEvent(SYNCHRONIZE | EVENT_MODIFY_STATE,
        FALSE, w_active_databases_updated_event_name)) == NULL) {
            // Failed to open the active_databases_updated_event.
            return SCERROPENACTIVEDBUPDATEDEV;
    }

    return 0;
}

// Entry point for monitoring databases thread.
uint32_t __stdcall MonitorDatabases(LPVOID params)
{
    uint32_t err_code = 0;
    std::set<std::string> active_databases_set;

    // Opening active databases event with monitor.
    err_code = g_gateway.OpenActiveDatabasesUpdatedEvent();
    if (err_code)
        return err_code;

    GW_COUT << "Waiting for active database update events..." << GW_ENDL;

    while (true)
    {
        // Waiting for shared monitor notification.
        switch (::WaitForSingleObject(g_gateway.active_databases_updates_event(), INFINITE))
        {
            case WAIT_OBJECT_0:
            {
                // The IPC monitor updated the active databases set.
                g_gateway.the_monitor_interface()->active_database_set()
                    .copy(active_databases_set, g_gateway.active_databases_updates_event());

                // Checking for any database changes in active databases file.
                err_code = g_gateway.CheckDatabaseChanges(active_databases_set);
                if (err_code)
                    return err_code;

                break;
            }

            case WAIT_TIMEOUT:
            {
                break;
            }
                
            case WAIT_FAILED:
            {
                break;
            }
        }
    }

    return 0;
}

// Entry point for gateway worker.
uint32_t __stdcall MonitorDatabasesRoutine(LPVOID params)
{
    uint32_t err_code = 0;

    // Catching all unhandled exceptions in this thread.
    GW_SC_BEGIN_FUNC

    // Starting routine.
    err_code = MonitorDatabases(params);

    // Catching all unhandled exceptions in this thread.
    GW_SC_END_FUNC

    wchar_t temp[256];
    wsprintf(temp, L"Databases monitoring thread exited with error code: %d", err_code);
    g_gateway.LogWriteError(temp);

    return err_code;
}

// Entry point for gateway worker.
uint32_t __stdcall GatewayWorkerRoutine(LPVOID params)
{
    uint32_t err_code = 0;

    // Catching all unhandled exceptions in this thread.
    GW_SC_BEGIN_FUNC

    // Starting routine.
    err_code = ((GatewayWorker *)params)->WorkerRoutine();

    // Catching all unhandled exceptions in this thread.
    GW_SC_END_FUNC

    wchar_t temp[256];
    wsprintf(temp, L"Gateway worker thread exited with error code: %d", err_code);
    g_gateway.LogWriteError(temp);

    return err_code;
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

// Cleans up all collected inactive sockets.
uint32_t Gateway::CleanupInactiveSocketsOnWorkerZero()
{
    EnterCriticalSection(&cs_sockets_cleanup_);

    // Going through all collected inactive sockets.
    int64_t num_sockets_to_cleanup = num_sockets_to_cleanup_unsafe_;

    for (int32_t i = 0; i < num_sockets_to_cleanup; i++)
    {
        // Getting existing socket info reference.
        ScSocketInfoStruct* global_socket_info_ref = all_sockets_infos_unsafe_ + sockets_to_cleanup_unsafe_[i];

        // Checking if socket is active.
        if (0 != global_socket_info_ref->socket_timestamp_)
        {
            // Resetting the socket info cell.
            global_socket_info_ref->session_.Reset();

            // Setting the socket time stamp to zero.
            global_socket_info_ref->ResetTimestamp();

#ifdef GW_SESSIONS_DIAG
            GW_COUT << "Disconnecting inactive socket " << sockets_to_cleanup_unsafe_[i] << "." << GW_ENDL;
#endif

            // Adding socket to disconnect queue.
            g_gateway.get_worker(0)->AddSocketToDisconnectListUnsafe(sockets_to_cleanup_unsafe_[i]);
        }

        // Inactive socket was successfully cleaned up.
        num_sockets_to_cleanup_unsafe_--;
    }

    GW_ASSERT(0 == num_sockets_to_cleanup_unsafe_);

    LeaveCriticalSection(&cs_sockets_cleanup_);

    return 0;
}

// Collects outdated sockets if any.
uint32_t Gateway::CollectInactiveSockets()
{
    // Checking if collected sockets cleanup is not finished yet.
    if (num_sockets_to_cleanup_unsafe_)
        return 0;

    EnterCriticalSection(&cs_sockets_cleanup_);

    GW_ASSERT(0 == num_sockets_to_cleanup_unsafe_);

    int32_t num_inactive = 0;

    // TODO: Optimize scanning range.
    for (uint32_t i = 0; i < setting_max_connections_; i++)
    {
        // Checking if socket touch time is older than inactive socket timeout.
        if ((all_sockets_infos_unsafe_[i].socket_timestamp_) &&
            (global_timer_unsafe_ - all_sockets_infos_unsafe_[i].socket_timestamp_) >= setting_inactive_socket_timeout_seconds_)
        {
            // Disconnecting socket.
            switch (all_sockets_infos_unsafe_[i].type_of_network_protocol_)
            {
                case MixedCodeConstants::NetworkProtocolType::PROTOCOL_HTTP1:
                {
                    sockets_to_cleanup_unsafe_[num_inactive] = i;
                    ++num_inactive;

                    break;
                }

                /*case MixedCodeConstants::NetworkProtocolType::PROTOCOL_WEBSOCKETS:
                {
                    sockets_to_cleanup_unsafe_[num_inactive] = i;
                    ++num_inactive;

                    break;
                }*/
            }
        }

        // Checking if we have checked all active sockets.
        if (num_inactive >= num_active_sockets_)
            break;
    }

    num_sockets_to_cleanup_unsafe_ = num_inactive;

    LeaveCriticalSection(&cs_sockets_cleanup_);

    if (num_active_sockets_)
    {
        // Waking up the worker 0 thread with APC.
        WakeUpThreadUsingAPC(worker_thread_handles_[0]);
    }

    return 0;
}

// Entry point for inactive sockets cleanup.
uint32_t __stdcall InactiveSocketsCleanupRoutine(LPVOID params)
{
    // Catching all unhandled exceptions in this thread.
    GW_SC_BEGIN_FUNC

    while (true)
    {
#ifdef GW_COLLECT_INACTIVE_SOCKETS

        // Sleeping minimum interval.
        Sleep(g_gateway.get_min_inactive_socket_life_seconds() * 1000);

        // Increasing global time by minimum number of seconds.
        g_gateway.step_global_timer_unsafe(g_gateway.get_min_inactive_socket_life_seconds());

        // Collecting inactive sockets if any.
        g_gateway.CollectInactiveSockets();

#else
        Sleep(100000);
#endif
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
uint32_t __stdcall GatewayMonitorRoutine(LPVOID params)
{
    uint32_t err_code = 0;

    // Catching all unhandled exceptions in this thread.
    GW_SC_BEGIN_FUNC

    // Starting routine.
    err_code = g_gateway.GatewayMonitor();

    // Catching all unhandled exceptions in this thread.
    GW_SC_END_FUNC

    wchar_t temp[256];
    wsprintf(temp, L"Gateway monitoring thread exited with error code: %d", err_code);
    g_gateway.LogWriteError(temp);

    return err_code;
}

// Cleaning up all global resources.
uint32_t Gateway::GlobalCleanup()
{
    // Closing IOCP.
    CloseHandle(iocp_);

    // Cleanup WinSock.
    WSACleanup();

    // Deleting critical sections.
    DeleteCriticalSection(&cs_global_lock_);
    DeleteCriticalSection(&cs_sockets_cleanup_);
    DeleteCriticalSection(&cs_statistics_);

    // Closing the log.
    CloseStarcounterLog();

    return 0;
}

// Checks that all Gateway threads are alive.
uint32_t Gateway::GatewayMonitor()
{
    int32_t running_time_seconds = 0;

    while (1)
    {
        // Sleeping some interval.
        Sleep(GW_MONITOR_THREAD_TIMEOUT_SECONDS * 1000);

        // Checking if workers are still running.
        for (int32_t i = 0; i < setting_num_workers_; i++)
        {
            // Waking up the worker thread with APC.
            WakeUpThreadUsingAPC(g_gateway.get_worker_thread_handle(i));

            // Checking if alive.
            if (!WaitForSingleObject(worker_thread_handles_[i], 0))
            {
                // Stating explicitly that this worker is dead.
                gw_workers_[i].set_worker_suspended(true);

                // Printing diagnostics.
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

        // Checking if dead sockets cleanup thread is alive.
        if (!WaitForSingleObject(dead_sockets_cleanup_thread_handle_, 0))
        {
            GW_COUT << "Dead sockets cleanup thread is dead." << GW_ENDL;
            return SCERRGWSESSIONSCLEANUPTHREADISDEAD;
        }

        // Checking if gateway logging thread is alive.
        if (!WaitForSingleObject(gateway_logging_thread_handle_, 0))
        {
            GW_COUT << "Gateway logging thread is dead." << GW_ENDL;
            return SCERRGWGATEWAYLOGGINGTHREADISDEAD;
        }

        // Checking if we are still running when we should not.
#ifdef GW_TESTING_MODE

        running_time_seconds += GW_MONITOR_THREAD_TIMEOUT_SECONDS;
        if (running_time_seconds >= setting_max_test_time_seconds_)
        {
            GW_COUT << "Test timed out. Exiting process." << GW_ENDL;

            ShutdownGateway(NULL, SCERRGWTESTTIMEOUT);
        }

#endif
    }

    return 0;
}

// Safely shutdowns the gateway.
void Gateway::ShutdownGateway(GatewayWorker* gw, int32_t exit_code)
{
    // Entering safe mode.
    if (gw)
        gw->EnterGlobalLock();
    else
        EnterGlobalLock();

    // Killing process with given exit code.
    ExitProcess(exit_code);
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

            ShutdownGateway(NULL, SCERRGWSOMETHREADDIED);
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

        // Checking if we have started measurements.
        if (started_measured_test_)
        {
            switch (g_gateway.setting_mode())
            {
                case GatewayTestingMode::MODE_GATEWAY_HTTP:
                case GatewayTestingMode::MODE_GATEWAY_SMC_HTTP:
                case GatewayTestingMode::MODE_GATEWAY_SMC_APPS_HTTP:
                {
                    num_gateway_ops_per_second_ += diffProcessedHttpRequestsAllWorkers;
                    break;
                }

                case GatewayTestingMode::MODE_GATEWAY_RAW:
                case GatewayTestingMode::MODE_GATEWAY_SMC_RAW:
                case GatewayTestingMode::MODE_GATEWAY_SMC_APPS_RAW:
                {
                    num_gateway_ops_per_second_ += diffRecvNumAllWorkers;
                    break;
                }
            }

            num_ops_measures_++;
        }

#endif

        EnterCriticalSection(&cs_statistics_);

        // Emptying the statistics stream.
        global_statistics_stream_.str(std::string());

        // Global statistics.
        global_statistics_stream_ << "<h4>GLOBAL</h4>";

        // Printing all workers stats.
        global_statistics_stream_ << "All workers last second: " <<
            "received times " << diffRecvNumAllWorkers <<
            ", HTTP requests " << diffProcessedHttpRequestsAllWorkers <<
            ", receive bandwidth " << recv_bandwidth_mbit_total << " mbit/sec" <<
            ", sent times " << diffSentNumAllWorkers <<
            ", send bandwidth " << send_bandwidth_mbit_total << " mbit/sec" <<
            "<br>" << GW_ENDL;

#ifdef GW_LOGGING_ON

        // Individual workers statistics.
        for (int32_t worker_id_ = 0; worker_id_ < setting_num_workers_; worker_id_++)
        {
            global_statistics_stream_ << "Worker " << worker_id_ << ": " <<
                "recv_bytes " << gw_workers_[worker_id_].get_worker_stats_bytes_received() <<
                ", recv_times " << gw_workers_[worker_id_].get_worker_stats_recv_num() <<
                ", sent_bytes " << gw_workers_[worker_id_].get_worker_stats_bytes_sent() <<
                ", sent_times " << gw_workers_[worker_id_].get_worker_stats_sent_num() <<
                ", bound_sockets " << gw_workers_[worker_id_].get_worker_stats_num_bound_sockets() <<
                "<br>" << GW_ENDL;
        }

        // !!!!!!!!!!!!!
        // NOTE: The following statistics can be dangerous since its not protected by global lock!
        // That's why we enable it only for tests.

        global_statistics_stream_ << "Overflow chunks " << g_gateway.NumberOverflowChunksAllWorkersAndDatabases() <<
            ", active sockets " << g_gateway.get_num_active_sockets() <<
            ", created sockets " << g_gateway.NumberCreatedSocketsAllWorkers() <<
            ", reusable conn-socks " << g_gateway.NumberOfReusableConnectSockets() <<
            "<br>" << GW_ENDL;

        // Printing handlers information for each attached database and gateway.
        for (int32_t p = 0; p < num_server_ports_slots_; p++)
        {
            // Checking if port is alive.
            if (!server_ports_[p].IsEmpty())
            {
                global_statistics_stream_ << "Port " << server_ports_[p].get_port_number() << ":" <<

#ifdef GW_COLLECT_SOCKET_STATISTICS
                    " active conns " << server_ports_[p].NumberOfActiveConnections() <<
#endif

                    " accepting socks " << server_ports_[p].get_num_accepting_sockets() <<

#ifdef GW_COLLECT_SOCKET_STATISTICS
                    ", alloc acc-socks " << server_ports_[p].get_num_allocated_accept_sockets() <<
                    ", alloc conn-socks " << server_ports_[p].get_num_allocated_connect_sockets() <<
#endif                        
                    "<br>" << GW_ENDL;
            }
        }

#ifdef GW_TESTING_MODE
        global_statistics_stream_ << "Perf Test Info: num confirmed echoes " << num_confirmed_echoes_unsafe_ <<
            "(" << setting_num_echoes_to_master_ << "), num sent echoes " << (current_echo_number_unsafe_ + 1) <<
            "(" << setting_num_echoes_to_master_ << ")" << "<br>" << GW_ENDL;
#endif

#endif

        // Printing the statistics string to console.
#ifdef GW_LOG_TO_CONSOLE
        std::cout << global_statistics_stream_.str();
#endif

        LeaveCriticalSection(&cs_statistics_);

    }

    return 0;
}

// Starts gateway workers and statistics printer.
uint32_t Gateway::StartWorkerAndManagementThreads(
    LPTHREAD_START_ROUTINE workerRoutine,
    LPTHREAD_START_ROUTINE monitorDatabasesRoutine,
    LPTHREAD_START_ROUTINE channelsEventsMonitorRoutine,
    LPTHREAD_START_ROUTINE inactiveSocketsCleanupRoutine,
    LPTHREAD_START_ROUTINE gatewayLoggingRoutine,
    LPTHREAD_START_ROUTINE allThreadsMonitorRoutine)
{
    // Allocating threads-related data structures.
    uint32_t *worker_thread_ids = new uint32_t[setting_num_workers_];

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
            (LPDWORD)&worker_thread_ids[i]); // Returns the thread identifier.

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

    uint32_t inactiveSocketsCleanupThreadId;

    // Starting dead sockets cleanup thread.
    dead_sockets_cleanup_thread_handle_ = CreateThread(
        NULL, // Default security attributes.
        0, // Use default stack size.
        inactiveSocketsCleanupRoutine, // Thread function name.
        NULL, // Argument to thread function.
        0, // Use default creation flags.
        (LPDWORD)&inactiveSocketsCleanupThreadId); // Returns the thread identifier.

    // Checking if thread is created.
    GW_ASSERT(dead_sockets_cleanup_thread_handle_ != NULL);

    uint32_t gatewayLogRoutineThreadId;

    // Starting dead sockets cleanup thread.
    gateway_logging_thread_handle_ = CreateThread(
        NULL, // Default security attributes.
        0, // Use default stack size.
        gatewayLoggingRoutine, // Thread function name.
        NULL, // Argument to thread function.
        0, // Use default creation flags.
        (LPDWORD)&gatewayLogRoutineThreadId); // Returns the thread identifier.

    // Checking if thread is created.
    GW_ASSERT(gateway_logging_thread_handle_ != NULL);

    // Starting dead sockets cleanup thread.
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

    delete worker_thread_ids;
    delete worker_thread_handles_;
    delete [] gw_workers_;
    gw_workers_ = NULL;

    // Checking if any error occurred.
    GW_ERR_CHECK(err_code);

    return 0;
}

// Destructor.
Gateway::~Gateway()
{
    // Deleting only necessary stuff.
    if (gw_workers_)
    {
        delete [] gw_workers_;
        gw_workers_ = NULL;
    }
}

int32_t Gateway::StartGateway()
{
    uint32_t err_code;

    // Assert some correct state.
    err_code = AssertCorrectState();
    if (err_code)
    {
        GW_COUT << "Asserting correct state failed." << GW_ENDL;
        return err_code;
    }

    // Initialize WinSock.
    WSADATA wsaData = { 0 };
    err_code = WSAStartup(MAKEWORD(2, 2), &wsaData);
    if (err_code != 0)
    {
        GW_COUT << "WSAStartup() failed: " << err_code << GW_ENDL;
        return err_code;
    }

    // Loading configuration settings.
    err_code = LoadSettings(setting_config_file_path_);
    if (err_code)
    {
        GW_COUT << "Loading configuration settings failed." << GW_ENDL;
        return err_code;
    }

    // Creating data structures and binding sockets.
    err_code = Init();
    if (err_code)
        return err_code;

    // Starting workers and statistics printer.
    err_code = StartWorkerAndManagementThreads(
        (LPTHREAD_START_ROUTINE)GatewayWorkerRoutine,
        (LPTHREAD_START_ROUTINE)MonitorDatabasesRoutine,
        (LPTHREAD_START_ROUTINE)AllDatabasesChannelsEventsMonitorRoutine,
        (LPTHREAD_START_ROUTINE)InactiveSocketsCleanupRoutine,
        (LPTHREAD_START_ROUTINE)GatewayLoggingRoutine,
        (LPTHREAD_START_ROUTINE)GatewayMonitorRoutine);

    if (err_code)
        return err_code;

    return 0;
}

#ifdef GW_TESTING_MODE

// Calculates number of created connections for all workers.
int64_t Gateway::GetNumberOfCreatedConnectionsAllWorkers()
{
    int64_t num_created_conns = 0;
    for (int32_t w = 0; w < setting_num_workers_; w++)
    {
        num_created_conns += gw_workers_[w].get_num_created_conns_worker();
    }
    return num_created_conns;
}

#ifdef GW_LOOPED_TEST_MODE

// Getting number of preparation network events.
int64_t Gateway::GetNumberOfPreparationNetworkEventsAllWorkers()
{
    int64_t num_preparation_events = 0;
    for (int32_t w = 0; w < setting_num_workers_; w++)
    {
        num_preparation_events += gw_workers_[w].GetNumberOfPreparationNetworkEvents();
    }
    return num_preparation_events;
}

#endif

// Checks that echo responses are correct.
bool Gateway::CheckConfirmedEchoResponses(GatewayWorker* gw)
{
    // First checking if we have confirmed all echoes.
    if (num_confirmed_echoes_unsafe_ != setting_num_echoes_to_master_)
        return false;

    EnterCriticalSection(&cs_test_finished_);

    // Checking if test already finished.
    if (finished_measured_test_)
    {
        LeaveCriticalSection(&cs_test_finished_);
        return false;
    }

    // Running through all echoes.
    for (int32_t i = 0; i < setting_num_echoes_to_master_; i++)
    {
        // Checking that each echo is confirmed.
        if (confirmed_echoes_shared_[i] != true)
        {
            GW_COUT << "Incorrect echo: " << i << GW_ENDL;

            // Failing test if echo is not confirmed.
            ShutdownTest(gw, false);

            return false;
        }
    }

    // Gracefully finishing the test.
    ShutdownTest(gw, true);

    return true;
}

// Gracefully shutdowns all needed processes after test is finished.
uint32_t Gateway::ShutdownTest(GatewayWorker* gw, bool success)
{
    // Test has finished.
    finished_measured_test_ = true;
    LeaveCriticalSection(&cs_test_finished_);

    // Checking if we are on the build server.
    char* envvar_str;
    size_t num_written;
    _dupenv_s(&envvar_str, &num_written, "SC_RUNNING_ON_BUILD_SERVER");
    bool is_on_build_server = false;
    if (envvar_str)
    {
        // Checking for exactly True value.
        if (0 == strcmp(envvar_str, "True"))
            is_on_build_server = true;
    }

    if (success)
    {
        GW_COUT << "Echo test finished successfully!" << GW_ENDL;

        int64_t test_finish_time = timeGetTime();

        // Test finished successfully, printing the results.
        int64_t ops_per_second = GetAverageOpsPerSecond();

        GW_COUT << "Average number of ops per second: " << ops_per_second <<
            ". Took " << test_finish_time - test_begin_time_ << " ms." << GW_ENDL;

        // Reporting statistics to build output and build statistics file.
        ReportStatistics(setting_stats_name_.c_str(), static_cast<double> (ops_per_second));

        if (is_on_build_server)
            ShutdownGateway(gw, 0);

        return 0;
    }

    // This is a test failure.
    GW_COUT << "ERROR with echo testing!" << GW_ENDL;

    if (is_on_build_server)
        ShutdownGateway(gw, SCERRGWTESTFAILED);

    return 0;
}

#endif

// Generate the code using managed generator.
uint32_t Gateway::GenerateUriMatcher(RegisteredUris* port_uris)
{
    // Getting registered URIs.
    std::vector<MixedCodeConstants::RegisteredUriManaged> uris_managed = port_uris->GetRegisteredUriManaged();

    // Creating root URI matching function name.
    char root_function_name[32];
    sprintf_s(root_function_name, 32, "MatchUriForPort%d", port_uris->get_port_number());

    // Calling managed function.
    uint32_t err_code = codegen_uri_matcher_->GenerateUriMatcher(
        root_function_name,
        &uris_managed.front(),
        static_cast<uint32_t>(uris_managed.size()));

    // Checking that code generation always succeeds.
    GW_ASSERT(0 == err_code);

    MixedCodeConstants::MatchUriType match_uri_func;
    HMODULE gen_dll_handle;

    // Unloading existing matcher DLL if any.
    port_uris->UnloadLatestUriMatcherDllIfAny();

    // Constructing dll name;
    std::wostringstream dll_name;
    dll_name << L"codegen_uri_matcher_" << port_uris->get_port_number();

    // Building URI matcher from generated code and loading the library.
    err_code = codegen_uri_matcher_->CompileIfNeededAndLoadDll(
        UriMatchCodegenCompilerType::COMPILER_CLANG,
        dll_name.str(),
        root_function_name,
        port_uris->get_clang_engine_addr(),
        &match_uri_func,
        &gen_dll_handle);

    // Checking that code generation always succeeds.
    GW_ASSERT(0 == err_code);

    // Setting the entry point for new URI matcher.
    port_uris->set_latest_match_uri_func(match_uri_func);
    port_uris->set_latest_gen_dll_handle(gen_dll_handle);

    return 0;
}

// Adds some URI handler: either Apps or Gateway.
uint32_t Gateway::AddUriHandler(
    GatewayWorker* gw,
    HandlersTable* handlers_table,
    uint16_t port,
    const char* original_uri_info,
    uint32_t original_uri_info_len_chars,
    const char* processed_uri_info,
    uint32_t processed_uri_info_len_chars,
    uint8_t* param_types,
    int32_t num_params,
    BMX_HANDLER_TYPE handler_id,
    db_index_type db_index,
    GENERIC_HANDLER_CALLBACK handler_proc,
    bool is_gateway_handler,
    ReverseProxyInfo* reverse_proxy_info)
{
    uint32_t err_code;

    // Registering URI handler.
    BMX_HANDLER_INDEX_TYPE new_handler_index;
    err_code = handlers_table->RegisterUriHandler(
        gw,
        port,
        original_uri_info,
        original_uri_info_len_chars,
        processed_uri_info,
        processed_uri_info_len_chars,
        param_types,
        num_params,
        handler_id,
        handler_proc,
        db_index,
        new_handler_index,
        reverse_proxy_info);

    if (err_code)
        return err_code;

    // Search for handler index by URI string.
    BMX_HANDLER_TYPE handler_index = handlers_table->FindUriHandlerIndex(
        port,
        processed_uri_info,
        processed_uri_info_len_chars);

    // Getting the port structure.
    ServerPort* server_port = g_gateway.FindServerPort(port);

    // Registering URI on port.
    RegisteredUris* port_uris = server_port->get_registered_uris();
    uri_index_type uri_index = port_uris->FindRegisteredUri(processed_uri_info);

    // Checking if there is an entry.
    if (INVALID_URI_INDEX == uri_index)
    {
        // Checking if there is a session in parameters.
        uint8_t session_param_index = INVALID_PARAMETER_INDEX;
        for (int32_t i = 0; i < num_params; i++)
        {
            // Checking for REST_ARG_SESSION.
            if (MixedCodeConstants::REST_ARG_SESSION == param_types[i])
            {
                session_param_index = i;
                break;
            }
        }

        // Creating totally new URI entry.
        RegisteredUri new_entry(
            session_param_index,
            db_index,
            handlers_table->get_handler_list(handler_index),
            is_gateway_handler);

        // Adding entry to global list.
        port_uris->AddNewUri(new_entry);
    }
    else
    {
        // Disallowing handler duplicates.
        return SCERRHANDLERALREADYREGISTERED;

        /*
        // Obtaining existing URI entry.
        RegisteredUri* reg_uri = port_uris->GetEntryByIndex(uri_index);

        // Checking if there is no database for this URI.
        if (!reg_uri->ContainsDb(db_index))
        {
            // Adding new handler list for this database to the URI.
            reg_uri->Add(handlers_table->get_handler_list(handler_index));
        }
        */
    }

    if (err_code)
        return err_code;

    return 0;
}

// Adds some port handler: either Apps or Gateway.
uint32_t Gateway::AddPortHandler(
    GatewayWorker* gw,
    HandlersTable* handlers_table,
    uint16_t port,
    BMX_HANDLER_TYPE handler_info,
    db_index_type db_index,
    GENERIC_HANDLER_CALLBACK handler_proc)
{
    BMX_HANDLER_INDEX_TYPE new_handler_index;
    return handlers_table->RegisterPortHandler(
        gw,
        port,
        handler_info,
        handler_proc,
        db_index,
        new_handler_index);
}

// Adds some sub-port handler: either Apps or Gateway.
uint32_t Gateway::AddSubPortHandler(
    GatewayWorker* gw,
    HandlersTable* handlers_table,
    uint16_t port,
    bmx::BMX_SUBPORT_TYPE subport,
    BMX_HANDLER_TYPE handler_id,
    db_index_type db_index,
    GENERIC_HANDLER_CALLBACK handler_proc)
{
    BMX_HANDLER_INDEX_TYPE new_handler_index;
    return handlers_table->RegisterSubPortHandler(
        gw,
        port,
        subport,
        handler_id,
        handler_proc,
        db_index,
        new_handler_index);
}

extern "C" int32_t make_sc_process_uri(const char *server_name, const char *process_name, wchar_t *buffer, size_t *pbuffer_size);
extern "C" int32_t make_sc_server_uri(const char *server_name, wchar_t *buffer, size_t *pbuffer_size);

// Opens Starcounter log for writing.
uint32_t Gateway::OpenStarcounterLog()
{
	size_t host_name_size;
	wchar_t *host_name;
	uint32_t err_code;

	host_name_size = 0;
//	make_sc_server_uri(setting_sc_server_type_.c_str(), 0, &host_name_size);
	make_sc_process_uri(setting_sc_server_type_upper_.c_str(), GW_PROCESS_NAME, 0, &host_name_size);
	host_name = (wchar_t *)malloc(host_name_size * sizeof(wchar_t));
	if (host_name)
	{
//		make_sc_server_uri(setting_sc_server_type_.c_str(), host_name, &host_name_size);
		make_sc_process_uri(setting_sc_server_type_upper_.c_str(), GW_PROCESS_NAME, host_name, &host_name_size);

		err_code = sccorelog_init(0);
		if (err_code) goto err;

		err_code = sccorelog_connect_to_logs(host_name, setting_server_output_dir_.c_str(), NULL, &sc_log_handle_);
		if (err_code) goto err;

		goto end;
	}
	
	err_code = SCERROUTOFMEMORY;
err:
end:
	if (host_name) free(host_name);
	return err_code;
}

// Releases used socket index.
void Gateway::ReleaseSocketIndex(GatewayWorker* gw, session_index_type index)
{
    gw->PushToReusableAcceptSockets(all_sockets_infos_unsafe_[index].socket_);

    // Pushing to free indexes list.
    InterlockedPushEntrySList(free_socket_indexes_unsafe_, &(all_sockets_infos_unsafe_[index].free_socket_indexes_entry_));
}

// Closes Starcounter log.
void Gateway::CloseStarcounterLog()
{
    uint32_t err_code = sccorelog_release_logs(sc_log_handle_);

    GW_ASSERT(0 == err_code);
}

void Gateway::LogWriteCritical(const wchar_t* msg)
{
    LogWriteGeneral(msg, SC_ENTRY_CRITICAL);
}

void Gateway::LogWriteError(const wchar_t* msg)
{
    LogWriteGeneral(msg, SC_ENTRY_ERROR);
}

void Gateway::LogWriteWarning(const wchar_t* msg)
{
    LogWriteGeneral(msg, SC_ENTRY_WARNING);
}

void Gateway::LogWriteNotice(const wchar_t* msg)
{
    LogWriteGeneral(msg, SC_ENTRY_NOTICE);
}

void Gateway::LogWriteGeneral(const wchar_t* msg, uint32_t log_type)
{
	// NOTE:
	// No asserts in critical log handler. Assertion fails calls critical log
	// handler to log.

    uint32_t err_code;
	
	if (msg)
	{
	    err_code = sccorelog_kernel_write_to_logs(sc_log_handle_, log_type, 0, msg);
	
	    //GW_ASSERT(0 == err_code);
	}

    err_code = sccorelog_flush_to_logs(sc_log_handle_);

    //GW_ASSERT(0 == err_code);
}

#ifdef GW_TESTING_MODE

HttpTestInformation g_test_http_echo_requests[kNumTestHttpEchoRequests] =
{
    {
        "POST /gw-http-echo", 0,

        "POST /gw-http-echo HTTP/1.1\r\n"
        "Content-Type: text/html\r\n"
        "Content-Length: 8\r\n"
        "\r\n"
        "@@@@@@@@", 0, 0
    },

    {
        "POST /smc-http-echo", 0,

        "POST /smc-http-echo HTTP/1.1\r\n"
        "Content-Type: text/html\r\n"
        "Content-Length: 8\r\n"
        "\r\n"
        "@@@@@@@@", 0, 0
    },
    {
        "POST /apps-http-echo", 0,

        "POST /apps-http-echo HTTP/1.1\r\n"
        "Content-Type: text/html\r\n"
        "Content-Length: 8\r\n"
        "\r\n"
        "@@@@@@@@", 0, 0
    }
};

void Gateway::InitTestHttpEchoRequests()
{
    http_tests_information_ = g_test_http_echo_requests;

    for (int32_t i = 0; i < kNumTestHttpEchoRequests; i++)
    {
        http_tests_information_[i].method_and_uri_info_len = static_cast<int32_t> (strlen(http_tests_information_[i].method_and_uri_info));
        http_tests_information_[i].http_request_len = static_cast<int32_t> (strlen(http_tests_information_[i].http_request_str));
        http_tests_information_[i].http_request_insert_point = static_cast<int32_t> (strstr(http_tests_information_[i].http_request_str, "@") - http_tests_information_[i].http_request_str);
    }
}

#endif

} // namespace network
} // namespace starcounter

VOID LogGatewayCrash(VOID *pc, LPCWSTR str)
{
    starcounter::network::g_gateway.LogWriteCritical(str);
}

int wmain(int argc, wchar_t* argv[], wchar_t* envp[])
{
    // Catching all unhandled exceptions in this thread.
    GW_SC_BEGIN_FUNC

    // Setting the critical log handler.
    set_critical_log_handler(LogGatewayCrash, NULL);

    using namespace starcounter::network;
    uint32_t err_code;

    // Processing arguments and initializing log file.
    err_code = g_gateway.ProcessArgumentsAndInitLog(argc, argv);
    if (err_code)
        return err_code;

    // Stating the network gateway.
    err_code = g_gateway.StartGateway();
    if (err_code)
    {
        wchar_t temp[256];
        wsprintf(temp, L"Gateway exited with error code: %d", err_code);
        g_gateway.LogWriteError(temp);

        return err_code;
    }

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