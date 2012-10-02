#include "static_headers.hpp"
#include "gateway.hpp"
#include "handlers.hpp"
#include "ws_proto.hpp"
#include "http_proto.hpp"
#include "socket_data.hpp"
#include "tls_proto.hpp"
#include "worker.hpp"

#pragma comment(lib, "Ws2_32.lib")
#pragma comment(lib, "winmm.lib")

namespace starcounter {
namespace network {

// Main network gateway object.
Gateway g_gateway;

// Logging system.
TeeLogStream *g_cout = NULL;
TeeDevice *g_log_tee = NULL;
std::ofstream *g_log_stream = NULL;

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
        case SEND_OPER: return "SEND_OPER";
        case RECEIVE_OPER: return "RECEIVE_OPER";
        case ACCEPT_OPER: return "ACCEPT_OPER";
        case CONNECT_OPER: return "CONNECT_OPER";
        case DISCONNECT_OPER: return "DISCONNECT_OPER";
        case TO_DB_OPER: return "TODB_OPER";
        case FROM_DB_OPER: return "FROMDB_OPER";
        case UNKNOWN_OPER: return "UNKNOWN_OPER";
    }
    return "ERROR_OPER";
}

Gateway::Gateway()
{
    // Number of worker threads.
    setting_num_workers_ = 0;

    // Master IP address.
    setting_master_ip_ = "";

    // Indicates if this node is a master node.
    setting_is_master_ = true;

    // Maximum total number of sockets.
    setting_maxConnections_ = 0;

    // Maximum number of same operations processed in a queue.
    setting_max_same_ops_in_queue_ = 1000;

    // Maximum amount of overlapped structures fetched at once.
    setting_max_fetched_ovls_ = 1000;

    // Starcounter server type.
    setting_sc_server_type_ = "Personal";

    // All worker structures.
    gw_workers_ = NULL;

    // Worker thread handles.
    thread_handles_ = NULL;

    // IOCP handle.
    iocp_ = INVALID_HANDLE_VALUE;

    // Number of active databases.
    num_dbs_slots_ = 0;

    // Init unique sequence number.
    db_seq_num_ = 0;

    // Initializing scheduler information.
    global_scheduler_id_unsafe_ = 0;
    active_sched_num_read_only_ = 0;

    server_addr_ = NULL;
    global_sleep_ms_ = 0;

    // Initializing critical sections.
    InitializeCriticalSection(&cs_session_);
    InitializeCriticalSection(&cs_global_lock_);

    // Creating gateway handlers table.
    gw_handlers_ = new HandlersTable();

    // Initial number of server ports.
    num_server_ports_ = 0;
}

// Reading command line arguments.
// 1: Server type.
// 2: Gateway configuration file path.
// 3: Shared memory monitor logging directory path.
uint32_t Gateway::ReadArguments(int argc, wchar_t* argv[])
{
    // Checking correct number of arguments.
    if (argc < 4)
    {
        std::cout << "ScGateway.exe [ServerTypeName] [PathToGatewayXmlConfig] [PathToMonitorOutputDirectory]" << std::endl;
        std::cout << "Example: ScGateway.exe personal \"c:\\github\\NetworkGateway\\src\\scripts\\server.xml\" \"c:\\github\\Orange\\bin\\Debug\\.db.output\"" << std::endl;

        return 1;
    }

    // Converting Starcounter server type to narrow char.
    char temp[128];
    wcstombs(temp, argv[1], 128);

    // Copying other fields.
    setting_sc_server_type_ = temp;
    setting_config_file_path_ = argv[2];
    setting_log_file_dir_ = argv[3];
    setting_log_file_path_ = setting_log_file_dir_ + L"\\NetworkGateway.log";

    return 0;
}

// Initializes server socket.
void ServerPort::Init(uint16_t port_number, SOCKET listening_sock, int32_t blob_user_data_offset)
{
    // Allocating needed tables.
    port_handlers_ = new PortHandlers();
    registered_uris_ = new RegisteredUris();
    registered_subports_ = new RegisteredSubports();

    listening_sock_ = listening_sock;
    port_number_ = port_number;
    blob_user_data_offset_ = blob_user_data_offset;
    port_handlers_->set_port_number(port_number_);
}

// Removes this port.
void ServerPort::EraseDb(int32_t db_index)
{
    // Deleting port handlers if any.
    get_port_handlers()->RemoveEntry(db_index);

    // Deleting URI handlers if any.
    get_registered_uris()->RemoveEntry(db_index);

    // Deleting subport handlers if any.
    get_registered_subports()->RemoveEntry(db_index);
}

// Checking if port is unused by any database.
bool ServerPort::IsEmpty()
{
    // Checking port handlers.
    if (port_handlers_ && (!port_handlers_->IsEmpty()))
        return false;

    // Checking URIs.
    if (registered_uris_ && (!registered_uris_->IsEmpty()))
        return false;

    // Checking subports.
    if (registered_subports_ && (!registered_subports_->IsEmpty()))
        return false;

    // Checking every database.
    for (int32_t i = 0; i < g_gateway.get_num_dbs_slots(); i++)
    {
        if (!EmptyForDb(i))
            return false;
    }

    return true;
}

// Removes this port.
void ServerPort::Erase()
{
    // Closing socket which will results in Disconnect.
    if (INVALID_SOCKET != listening_sock_)
    {
        if (closesocket(listening_sock_))
        {
#ifdef GW_ERRORS_DIAG
            GW_COUT << "closesocket() failed." << std::endl;
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

    port_number_ = 0;
    blob_user_data_offset_ = -1;
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
        return 1;

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
        localIpElem = localIpElem->next_sibling();
    }

    // Getting master node IP address.
    setting_master_ip_ = rootElem->first_node("MasterIP")->value();

    // Master node does not need its own IP.
    if (setting_master_ip_ == "")
        setting_is_master_ = true;
    else
        setting_is_master_ = false;

    // Getting workers number.
    setting_num_workers_ = atoi(rootElem->first_node("WorkersNumber")->value());

    // Getting maximum connection number.
    setting_maxConnections_ = atoi(rootElem->first_node("MaxConnections")->value());

    // Creating double output object.
    g_log_stream = new std::ofstream(setting_log_file_path_, std::ios::out | std::ios::app);
    g_log_tee = new TeeDevice(std::cout, *g_log_stream);
    g_cout = new TeeLogStream(*g_log_tee);

    // Predefined ports constants.
    PortType portTypes[NUM_PREDEFINED_PORT_TYPES] = { HTTP_PORT, HTTPS_PORT, WEBSOCKETS_PORT, GENSOCKETS_PORT, AGGREGATION_PORT };
    std::string portNames[NUM_PREDEFINED_PORT_TYPES] = { "HttpPort", "HttpsPort", "WebSocketsPort", "GenSocketsPort", "AggregationPort" };
    GENERIC_HANDLER_CALLBACK portHandlerTypes[NUM_PREDEFINED_PORT_TYPES] = { UriProcessData, HttpsProcessData, UriProcessData, PortProcessData, PortProcessData };
    uint16_t portNumbers[NUM_PREDEFINED_PORT_TYPES] = { 80, 443, 80, 123, 12345 };
    uint32_t userDataOffsetsInBlob[NUM_PREDEFINED_PORT_TYPES] = { HTTP_BLOB_USER_DATA_OFFSET, HTTPS_BLOB_USER_DATA_OFFSET, WS_BLOB_USER_DATA_OFFSET, RAW_BLOB_USER_DATA_OFFSET, AGGR_BLOB_USER_DATA_OFFSET };

    // Going through all ports.
    for (int32_t i = 0; i < NUM_PREDEFINED_PORT_TYPES; i++)
    {
        portNumbers[i] = atoi(rootElem->first_node(portNames[i].c_str())->value());
        if (portNumbers[i] <= 0)
            continue;

        GW_COUT << portNames[i] << ": " << portNumbers[i] << std::endl;

        // Checking if several protocols are on the same port.
        int32_t samePortIndex = -1;
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
    all_sessions_unsafe_ = new SessionData[setting_maxConnections_];
    free_session_indexes_unsafe_ = new int32_t[setting_maxConnections_];
    num_active_sessions_unsafe_ = 0;

    // Filling up indexes linearly.
    for (int32_t i = 0; i < setting_maxConnections_; i++)
        free_session_indexes_unsafe_[i] = i;

    delete [] config_contents;
    return 0;
}

// Creates socket and binds it to server port.
uint32_t Gateway::CreateListeningSocketAndBindToPort(GatewayWorker *gw, uint16_t port_num, SOCKET& sock)
{
    // Creating socket.
    sock = WSASocket(AF_INET, SOCK_STREAM, IPPROTO_TCP, NULL, 0, WSA_FLAG_OVERLAPPED);
    if (sock == INVALID_SOCKET)
    {
        GW_COUT << "WSASocket() failed." << std::endl;
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
        GW_COUT << "Wrong IOCP returned when adding reference." << std::endl;
        return PrintLastError();
    }

    // Skipping completion port if operation is already successful.
    SetFileCompletionNotificationModes((HANDLE) sock, FILE_SKIP_COMPLETION_PORT_ON_SUCCESS);

    // The socket address to be passed to bind.
    sockaddr_in bindAddr;
    memset(&bindAddr, 0, sizeof(sockaddr_in));
    bindAddr.sin_family = AF_INET;
    bindAddr.sin_addr.s_addr = INADDR_ANY; //inet_addr(g_data.g_MasterIP.c_str());
    bindAddr.sin_port = htons(port_num);

    // Binding socket to certain interface and port.
    if (bind(sock, (SOCKADDR*) &bindAddr, sizeof(bindAddr)))
    {
        GW_COUT << "Failed to bind server port " << port_num << std::endl;
        return PrintLastError();
    }

    // Listening to connections.
    if (listen(sock, SOMAXCONN))
    {
        GW_COUT << "Error listening on server socket." << std::endl;
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

// Scans for new/existing databases and updates corresponding shared memory structures.
uint32_t Gateway::ScanDatabases()
{
    int32_t server_name_len = setting_sc_server_type_.length();
    std::ifstream ad_file(setting_log_file_dir_ + L"\\active_databases");

    // Just quiting if file can't be opened.
    if (ad_file.is_open() == false)
        return 0;

    // Enabling database down tracking flag.
    for (int32_t i = 0; i < num_dbs_slots_; i++)
        db_did_go_down_[i] = true;

    // Reading file line by line.
    std::string current_db_name;
    while(getline(ad_file, current_db_name))
    {
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

        // We have a new database being up.
        if (is_new_database)
        {
#ifdef GW_GENERAL_DIAG
            GW_COUT << "Attaching a new database: " << current_db_name << std::endl;
#endif

            // Entering global lock.
            EnterGlobalLock();

            // Finding first empty slot.
            int32_t empty_slot = 0;
            for (empty_slot = 0; empty_slot < num_dbs_slots_; ++empty_slot)
            {
                // Checking if database slot is empty.
                if (active_databases_[empty_slot].IsEmpty())
                    break;
            }

            // Workers shared interface instances.
            core::shared_interface *workers_shared_ints =
                new core::shared_interface[setting_num_workers_];
            
            // Adding to the databases list.
            uint32_t errCode = InitSharedMemory(current_db_name, workers_shared_ints);
            if (errCode != 0)
            {
                // Leaving global lock.
                LeaveGlobalLock();
                return errCode;
            }

            // Filling necessary fields.
            active_databases_[empty_slot].Init(current_db_name, ++db_seq_num_, empty_slot);
            db_did_go_down_[empty_slot] = false;

            // Increasing number of active databases.
            if (empty_slot >= num_dbs_slots_)
                num_dbs_slots_++;

            // Adding to workers database interfaces.
            for (int32_t i = 0; i < setting_num_workers_; i++)
            {
                uint32_t errCode = gw_workers_[i].AddNewDatabase(empty_slot, workers_shared_ints[i]);
                if (errCode != 0)
                {
                    // Leaving global lock.
                    LeaveGlobalLock();
                    return errCode;
                }
            }

            // Leaving global lock.
            LeaveGlobalLock();
        }
    }

    // Checking what databases went down.
    for (int32_t s = 0; s < num_dbs_slots_; s++)
    {
        if ((db_did_go_down_[s]) && (!active_databases_[s].IsDeletionStarted()))
        {
#ifdef GW_GENERAL_DIAG
            GW_COUT << "Start detaching dead database: " << active_databases_[s].db_name() << std::endl;
#endif

            // Entering global lock.
            EnterGlobalLock();

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
    StartDeletion();
}

// Initializes this active database slot.
void ActiveDatabase::Init(std::string db_name, uint64_t unique_num, int32_t db_index)
{
    // Creating fresh handlers table.
    user_handlers_ = new HandlersTable();

    db_name_ = db_name;
    unique_num_ = unique_num;
    db_index_ = db_index;
    were_sockets_closed_ = false;

    num_used_sockets_ = 0;
    num_used_chunks_ = 0;
    for (int32_t i = 0; i < MAX_SOCKET_HANDLE; i++)
        active_sockets_[i] = false;
}

// Destructor.
ActiveDatabase::~ActiveDatabase()
{
    StartDeletion();
}

// Makes this database slot empty.
void ActiveDatabase::StartDeletion()
{
    // Removing handlers table.
    if (user_handlers_)
    {
        delete user_handlers_;
        user_handlers_ = NULL;
    }

    // Setting incorrect index immediately.
    db_index_ = -1;

    unique_num_ = 0;
    db_name_ = "";

    // Closing all database sockets.
    CloseSockets();
}

// Closes all tracked sockets.
void ActiveDatabase::CloseSockets()
{
    // Checking if sockets were already closed.
    if (were_sockets_closed_)
        return;

    // Marking closure.
    were_sockets_closed_ = true;

    // Checking if just marking for deletion.
    for (SOCKET s = 0; s < MAX_SOCKET_HANDLE; s++)
    {
        // Checking if socket is active.
        if (active_sockets_[s])
        {
            // Marking deleted socket.
            g_gateway.MarkDeleteSocket(s);

            // Closing socket which will results in Disconnect.
            if (closesocket(s))
            {
#ifdef GW_ERRORS_DIAG
                GW_COUT << "closesocket() failed." << std::endl;
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
    if ((gw_workers_ != NULL) || (thread_handles_ != NULL))
    {
        GW_COUT << "Workers data was already initialized." << std::endl;
        return 1;
    }

    // Initialize WinSock.
    WSADATA wsaData = {0};
    int32_t errCode = WSAStartup(MAKEWORD(2, 2), &wsaData);
    if (errCode != 0)
    {
        GW_COUT << "WSAStartup() failed: " << errCode << std::endl;
        return errCode;
    }

    // Allocating workers data.
    gw_workers_ = new GatewayWorker[setting_num_workers_];
    thread_handles_ = new HANDLE[setting_num_workers_];

    // Creating IO completion port.
    iocp_ = CreateIoCompletionPort(INVALID_HANDLE_VALUE, NULL, 0, setting_num_workers_);
    if (iocp_ == NULL)
    {
        GW_COUT << "Failed to create IOCP." << std::endl;
        return PrintLastError();
    }

    // Filling up worker parameters.
    for (int i = 0; i < setting_num_workers_; i++)
    {
        int32_t errCode = gw_workers_[i].Init(i);
        if (errCode != 0)
            return errCode;
    }

    // Creating and activating server sockets.
    if (setting_is_master_)
    {
        // Going throw all needed ports.
        for(int32_t p = 0; p < num_server_ports_; p++)
        {
            SOCKET server_socket = INVALID_SOCKET;

            // Creating socket and binding to port (only on the first worker).
            uint32_t errCode = CreateListeningSocketAndBindToPort(&gw_workers_[0], server_ports_[p].get_port_number(), server_socket);
            GW_ERR_CHECK(errCode);
        }
    }

    // Obtaining function pointers (AcceptEx, ConnectEx, DisconnectEx).
    uint32_t temp;
    SOCKET tempSocket = WSASocket(AF_INET, SOCK_STREAM, IPPROTO_TCP, NULL, 0, WSA_FLAG_OVERLAPPED);
    if (tempSocket == INVALID_SOCKET)
    {
        GW_COUT << "WSASocket() failed." << std::endl;
        return PrintLastError();
    }

    if (WSAIoctl(tempSocket, SIO_GET_EXTENSION_FUNCTION_POINTER, &AcceptExGuid, sizeof(AcceptExGuid), &AcceptExFunc, sizeof(AcceptExFunc), (LPDWORD)&temp, NULL, NULL))
    {
        GW_COUT << "Failed WSAIoctl(AcceptEx)." << std::endl;
        return PrintLastError();
    }

    if (WSAIoctl(tempSocket, SIO_GET_EXTENSION_FUNCTION_POINTER, &ConnectExGuid, sizeof(ConnectExGuid), &ConnectExFunc, sizeof(ConnectExFunc), (LPDWORD)&temp, NULL, NULL))
    {
        GW_COUT << "Failed WSAIoctl(ConnectEx)." << std::endl;
        return PrintLastError();
    }

    if (WSAIoctl(tempSocket, SIO_GET_EXTENSION_FUNCTION_POINTER, &DisconnectExGuid, sizeof(DisconnectExGuid), &DisconnectExFunc, sizeof(DisconnectExFunc), (LPDWORD)&temp, NULL, NULL))
    {
        GW_COUT << "Failed WSAIoctl(DisconnectEx)." << std::endl;
        return PrintLastError();
    }
    closesocket(tempSocket);

    // Global HTTP init.
    HttpGlobalInit();

    // Checking if we are not master node.
    if (!g_gateway.setting_is_master())
    {
        server_addr_ = new sockaddr_in;
        memset(server_addr_, 0, sizeof(sockaddr_in));
        server_addr_->sin_family = AF_INET;
        server_addr_->sin_addr.s_addr = inet_addr(g_gateway.setting_master_ip().c_str());
        server_addr_->sin_port = htons(80); // TODO
    }

    // Cleaning sockets deletion flag.
    for (int32_t i = 0; i < MAX_SOCKET_HANDLE; i++)
        deleted_sockets_[i] = false;

    // Indicating that network gateway is ready
    // (should be first line of the output).
    GW_COUT << "Gateway is ready!" << std::endl;

    // Indicating begin of new logging session.
    time_t rawtime;
    time(&rawtime);
    tm *timeinfo = localtime(&rawtime);
    GW_COUT << "New logging session: " << asctime(timeinfo) << std::endl;

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
        boost::to_upper_copy(setting_sc_server_type_) + "_" +
        boost::to_upper_copy(setting_databaseName) + "_0";

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
        GW_COUT << "Can't register client process, error code: " << error_code << std::endl;
        return 1;
    }

    // Open the database shared memory segment.
    if (db_shm_params->get_sequence_number() == 0)
    {
        // Cannot open the database shared memory segment, because it is not
        // initialized yet.
        GW_COUT << "Cannot open the database shared memory segment!" << std::endl;
    }

    // Name of the database shared memory segment.
    std::string shm_seg_name = std::string(DATABASE_NAME_PREFIX) + "_" +
        boost::to_upper_copy(setting_sc_server_type_) + "_" +
        boost::to_upper_copy(setting_databaseName) + "_" +
        boost::lexical_cast<std::string>(db_shm_params->get_sequence_number());

    // Construct a shared_interface.
    for (int32_t i = 0; i < setting_num_workers_; i++)
    {
        sharedInt[i].init(shm_seg_name.c_str(), mon_int_name.c_str(), pid, the_owner_id);
    }

    // Obtaining number of active schedulers.
    if (active_sched_num_read_only_ == 0)
    {
        active_sched_num_read_only_ = sharedInt[0].common_scheduler_interface().number_of_active_schedulers();
        GW_COUT << "Number of active schedulers: " << active_sched_num_read_only_ << std::endl;
    }

    return 0;
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

// Delete all information associated with given database from server ports.
uint32_t Gateway::DeletePortsForDb(int32_t db_index)
{
    // Going through all ports.
    for (int32_t i = 0; i < num_server_ports_; i++)
    {
        // Deleting port handlers if any.
        server_ports_[i].EraseDb(db_index);

        // Checking if port is not used anywhere.
        if (server_ports_[i].IsEmpty())
            server_ports_[i].Erase();
    }

    // Removing deleted trailing server ports.
    for (int32_t i = (num_server_ports_ - 1); i >= 0; i--)
    {
        // Removing until one server port is not empty.
        if (!server_ports_[i].IsEmpty())
            break;

        num_server_ports_--;
    }

    return 0;
}

// Waits for all workers to suspend.
void Gateway::WaitAllWorkersSuspended()
{
    int32_t num_worker_locked = 0;

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

// Entry point for gateway worker.
uint32_t __stdcall IOCPSocketsWorkerRoutine(LPVOID params)
{
    return ((GatewayWorker *)params)->IOCPSocketsWorker();
}

// Cleaning up all global resources.
uint32_t Gateway::GlobalCleanup()
{
    // Closing IOCP.
    CloseHandle(iocp_);

    // Closing logging system.
    delete g_cout;
    delete g_log_tee;
    delete g_log_stream;

    // Cleanup WinSock.
    WSACleanup();

    // Deleting critical sections.
    DeleteCriticalSection(&cs_session_);
    DeleteCriticalSection(&cs_global_lock_);

    return 0;
}

// Create new session based on random salt, linear index, scheduler.
void SessionData::GenerateNewSession(GatewayWorker *gw, uint32_t sessionIndex, uint32_t schedulerId)
{
    session_struct_.Init(gw->Random->uint64(), sessionIndex, schedulerId);

    num_visits_ = 0;
    attached_socket_ = INVALID_SOCKET;
    socket_stamp_ = gw->Random->uint64();
}

// Prints statistics, scans for database updates in the background, etc.
uint32_t Gateway::GatewayParallelLoop()
{
    // Previous statistics values.
    int64_t prevBytesReceivedAllWorkers = 0,
        prevSentNumAllWorkers = 0,
        prevRecvNumAllWorkers = 0;

    // New statistics values.
    int64_t newBytesReceivedAllWorkers = 0,
        newSentNumAllWorkers = 0,
        newRecvNumAllWorkers = 0;

    // Difference between new and previous statistics values.
    int64_t diffBytesReceivedAllWorkers = 0,
        diffSentNumAllWorkers = 0,
        diffRecvNumAllWorkers = 0;

    while(TRUE)
    {
        // Waiting some time for statistics updates.
        Sleep(1000);

        // Checking if workers are still running.
        for (int32_t i = 0; i < setting_num_workers_; i++)
        {
            if (!WaitForSingleObject(thread_handles_[i], 0))
            {
                GW_COUT << "Worker " << i << " is dead. Quiting..." << std::endl;
                return 1;
            }
        }

        // Resetting new values.
        newBytesReceivedAllWorkers = 0;
        newSentNumAllWorkers = 0;
        newRecvNumAllWorkers = 0;

        // Fetching new statistics.
        for (int32_t i = 0; i < setting_num_workers_; i++)
        {
            newBytesReceivedAllWorkers += gw_workers_[i].worker_stats_bytes_received();
            newSentNumAllWorkers += gw_workers_[i].worker_stats_sent_num();
            newRecvNumAllWorkers += gw_workers_[i].worker_stats_recv_num();
        }

        // Calculating differences.
        diffBytesReceivedAllWorkers = newBytesReceivedAllWorkers - prevBytesReceivedAllWorkers;
        diffSentNumAllWorkers = newSentNumAllWorkers - prevSentNumAllWorkers;
        diffRecvNumAllWorkers = newRecvNumAllWorkers - prevRecvNumAllWorkers;

        // Updating previous values.
        prevBytesReceivedAllWorkers = newBytesReceivedAllWorkers;
        prevSentNumAllWorkers = newSentNumAllWorkers;
        prevRecvNumAllWorkers = newRecvNumAllWorkers;

        // Printing the statistics.
        double bandWidthMbitSTotal = (diffBytesReceivedAllWorkers / 1000000.0);

        // Calculating global sleep interval.
        if ((diffRecvNumAllWorkers > 0) || (diffSentNumAllWorkers > 0))
            global_sleep_ms_ = 0;
        else
            global_sleep_ms_ = 1;
        
#ifdef GW_GLOBAL_STATISTICS

        // Global statistics.
        GW_COUT << "Global active chunks " << g_gateway.GetTotalNumUsedChunks() <<
            ", sockets " << g_gateway.GetTotalNumUsedSockets() << std::endl;

        // Individual workers statistics.
        for (int32_t i = 0; i < setting_num_workers_; i++)
        {
            GW_COUT << "[" << i << "]:" <<
                 " recv_bytes " << gw_workers_[i].worker_stats_bytes_received() <<
                ", sent_times " << gw_workers_[i].worker_stats_sent_num() <<
                ", recv_times " << gw_workers_[i].worker_stats_recv_num() << std::endl;
        }

        // Printing handlers information for each attached database and gateway.
        for (int32_t p = 0; p < num_server_ports_; p++)
        {
            for (int32_t d = 0; d < num_dbs_slots_; d++)
            {
                if (!server_ports_->EmptyForDb(d))
                {
                    GW_COUT << "Port " << server_ports_[p].get_port_number() <<
                        ", db \"" << active_databases_[d].db_name() << "\"" <<
                        ": active conns " << server_ports_[p].get_num_active_conns(d) <<
                        ", sockets created " << server_ports_[p].get_num_created_sockets(d) << std::endl;
                }
            }
        }

        // Printing all workers stats.
        GW_COUT << "All workers last sec" <<
             " sent_times " << diffSentNumAllWorkers <<
            ", recv_times " << diffRecvNumAllWorkers <<
            ", bandwidth " << bandWidthMbitSTotal <<
            " mbit/sec" << std::endl << std::endl;

#endif

        // Scanning for databases changes.
        uint32_t err_code = g_gateway.ScanDatabases();
        GW_ERR_CHECK(err_code);
    }

    return 0;
}

// Starts gateway workers and statistics printer.
uint32_t Gateway::StartWorkersAndGatewayLoop(LPTHREAD_START_ROUTINE workerRoutine)
{
    // Allocating threads-related data structures.
    uint32_t *threadIds = new uint32_t[setting_num_workers_];

    for (int i = 0; i < setting_num_workers_; i++)
    {
        // Creating threads.
        thread_handles_[i] = CreateThread(
            NULL, // Default security attributes.
            0, // Use default stack size.
            workerRoutine, // Thread function name.
            &gw_workers_[i], // Argument to thread function.
            0, // Use default creation flags.
            (LPDWORD)&threadIds[i]); // Returns the thread identifier.

        // Checking if threads are created.
        if (thread_handles_[i] == NULL)
        {
            GW_COUT << "CreateThread() failed." << std::endl;
            return 1;
        }
    }

    // Printing statistics.
    uint32_t errCode = g_gateway.GatewayParallelLoop();

    // Close all thread handles and free memory allocations.
    for(int i = 0; i < setting_num_workers_; i++)
        CloseHandle(thread_handles_[i]);

    delete threadIds;
    delete thread_handles_;
    delete [] gw_workers_;

    // Checking if any error occurred.
    GW_ERR_CHECK(errCode);

    return 0;
}

int32_t Gateway::StartGateway()
{
    // Loading configuration settings.
    uint32_t errCode = LoadSettings(setting_config_file_path_);
    if (errCode != 0)
    {
        GW_COUT << "Loading configuration settings failed." << std::endl;
        return errCode;
    }

    // Creating data structures and binding sockets.
    errCode = Init();
    if (errCode != 0)
        return errCode;

    // Starting workers and statistics printer.
    errCode = StartWorkersAndGatewayLoop((LPTHREAD_START_ROUTINE)IOCPSocketsWorkerRoutine);
    if (errCode != 0)
        return errCode;

    return 0;
}

} // namespace network
} // namespace starcounter

int wmain(int argc, wchar_t* argv[], wchar_t* envp[])
{
    using namespace starcounter::network;
    uint32_t err_code;

    // Setting I/O as low priority.
    SetPriorityClass(GetCurrentProcess(), PROCESS_MODE_BACKGROUND_BEGIN);

    // Reading arguments.
    err_code = g_gateway.ReadArguments(argc, argv);
    if (err_code) return err_code;

    // Stating the network gateway.
    err_code = g_gateway.StartGateway();
    if (err_code) return err_code;

    // Cleaning up resources.
    err_code = g_gateway.GlobalCleanup();
    if (err_code) return err_code;

    GW_COUT << "Press any key to exit." << std::endl;
    _getch();

    return 0;
}