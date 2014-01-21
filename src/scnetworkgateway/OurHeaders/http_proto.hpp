#pragma once
#ifndef HTTP_PROTO_HPP
#define HTTP_PROTO_HPP

namespace starcounter {
namespace network {

void HttpGlobalInit();
class WsProto;
class GatewayWorker;

class RegisteredUri
{
    // Indicates index of session parameter in this URI.
    uint8_t session_param_index_;

    // Is a gateway URI handler.
    bool is_gateway_uri_;

    // Unique handler lists.
    LinearList<HandlersList*, bmx::MAX_NUMBER_OF_HANDLERS_IN_LIST> handler_lists_;

public:

    // Is gateway URI.
    bool get_is_gateway_uri()
    {
        return is_gateway_uri_;
    }

    // Indicates index of session parameter in this URI.
    uint8_t get_session_param_index()
    {
        return session_param_index_;
    }

    // Converts handler id and database index.
    static BMX_HANDLER_TYPE CreateHandlerInfoType(
        BMX_HANDLER_TYPE handler_info,
        db_index_type db_index)
    {
        return db_index | (handler_info << 8);
    }

    // Converts handler id and database index.
    static void ParseHandlerInfoType(
        BMX_HANDLER_TYPE handler_info,
        BMX_HANDLER_TYPE& handler_id,
        db_index_type& db_index)
    {
        db_index = (db_index_type) handler_info;
        handler_id = (BMX_HANDLER_TYPE) (handler_info >> 8);
    }

    // Getting first handler entry.
    void WriteUserParameters(
        uint8_t* param_types,
        uint8_t* num_params)
    {
        // TODO: Make the investigation about same URI handlers.
        //GW_ASSERT(1 == handler_lists_.get_num_entries());

        HandlersList* handlers_list = handler_lists_[0];

        // TODO: This constrain is not needed.
        GW_ASSERT(1 == handlers_list->get_num_entries());

        memcpy(param_types, handlers_list->get_param_types(), MixedCodeConstants::MAX_URI_CALLBACK_PARAMS);
        *num_params = handlers_list->get_num_params();
    }

    // Getting number of native parameters in user delegate.
    uint8_t GetNumberOfNativeParameters()
    {
        return handler_lists_[0]->get_num_params();
    }

    // Getting number of handler lists.
    uint32_t GetHandlersListsNumber()
    {
        return handler_lists_.get_num_entries();
    }

    // Getting registered URI.
    char* get_original_uri_info()
    {
        return handler_lists_[0]->get_original_uri_info();
    }

    // Getting URI length in characters.
    uint32_t get_original_uri_info_len_chars()
    {
        return handler_lists_[0]->get_original_uri_info_len_chars();
    }

    // Getting registered URI.
    char* get_processed_uri_info()
    {
        return handler_lists_[0]->get_processed_uri_info();
    }

    // Getting URI length in characters.
    uint32_t get_processed_uri_info_len_chars()
    {
        return handler_lists_[0]->get_processed_uri_info_len_chars();
    }

    // Constructor.
    RegisteredUri()
    {
        Reset();
    }

    // Checking if handlers list is empty.
    bool IsEmpty()
    {
        return handler_lists_.IsEmpty();
    }

    // Removes certain entry.
    bool RemoveEntry(HandlersList* handlers_list)
    {
        return handler_lists_.RemoveEntry(handlers_list);
    }

    // Removes certain entry.
    bool ContainsDb(db_index_type db_index)
    {
        return (FindDb(db_index) >= 0);
    }

    // Removes certain entry.
    db_index_type GetFirstDbIndex()
    {
        return handler_lists_[0]->get_db_index();
    }

    // Removes certain entry.
    db_index_type FindDb(db_index_type db_index)
    {
        // Going through all handler list.
        for (int32_t i = 0; i < handler_lists_.get_num_entries(); i++)
        {
            if (handler_lists_[i]->get_db_index() == db_index)
            {
                return i;
            }
        }

        return INVALID_DB_INDEX;
    }

    // Removes certain entry.
    bool RemoveEntry(db_index_type db_index)
    {
        bool removed = false;

        // Going through all handler list.
        for (int32_t i = 0; i < handler_lists_.get_num_entries(); i++)
        {
            // Checking if database index is the same.
            if (handler_lists_[i]->get_db_index() == db_index)
            {
                handler_lists_.RemoveByIndex(i);
                i--;

                removed = true;

                // Not stopping, going through all entries.
            }
        }

        return removed;
    }

    // Initializing the entry.
    RegisteredUri(
        uint8_t session_param_index,
        db_index_type db_index,
        HandlersList* handlers_list,
        bool is_gateway_uri)
    {
        // Creating and pushing new handlers list.
        handler_lists_.Add(handlers_list);

        session_param_index_ = session_param_index;

        is_gateway_uri_ = is_gateway_uri;
    }

    // Adding new handlers list.
    void Add(HandlersList* handlers_list)
    {
        handler_lists_.Add(handlers_list);
    }

    // Resetting entry.
    void Reset()
    {
        // Removing all handlers lists.
        handler_lists_.Clear();

        session_param_index_ = INVALID_PARAMETER_INDEX;

        is_gateway_uri_ = false;
    }

    // Running all registered handlers.
    uint32_t RunHandlers(GatewayWorker *gw, SocketDataChunkRef sd, bool* is_handled);
};

class RegisteredUris
{
    // Array of all registered URIs.
    LinearList<RegisteredUri, bmx::MAX_TOTAL_NUMBER_OF_HANDLERS> reg_uris_;

    // Pointer to generated code verification function.
    MixedCodeConstants::MatchUriType latest_match_uri_func_;

    // Handle to the latest generated library.
    HMODULE latest_gen_dll_handle_;

    // Established Clang engine for this port.
    void* clang_engine_;

    // Port to which this URI matcher belongs.
    uint16_t port_number_;

public:

    uint16_t get_port_number()
    {
        return port_number_;
    }

    // Setting latest uri matching function pointer.
    void set_latest_match_uri_func(MixedCodeConstants::MatchUriType latest_match_uri_func)
    {
        latest_match_uri_func_ = latest_match_uri_func;
    }

    MixedCodeConstants::MatchUriType get_latest_match_uri_func()
    {
        return latest_match_uri_func_;
    }

    void** get_clang_engine_addr()
    {
        return &clang_engine_;
    }

    // Setting latest uri matching dll handle.
    void set_latest_gen_dll_handle(HMODULE latest_gen_dll_handle)
    {
        latest_gen_dll_handle_ = latest_gen_dll_handle;
    }

    // Checks if generated dll is loaded and unloads it.
    void UnloadLatestUriMatcherDllIfAny()
    {
        if (latest_gen_dll_handle_)
        {
            BOOL success = FreeLibrary(latest_gen_dll_handle_);
            GW_ASSERT(TRUE == success);

            latest_gen_dll_handle_ = NULL;
        }
    }

    // Getting array of RegisteredUriManaged.
    std::vector<MixedCodeConstants::RegisteredUriManaged> GetRegisteredUriManaged()
    {
        std::vector<MixedCodeConstants::RegisteredUriManaged> uris_vec;

        // Going through all URIs.
        for (int32_t i = 0; i < reg_uris_.get_num_entries(); i++)
        {
            if (!reg_uris_[i].IsEmpty())
            {
                MixedCodeConstants::RegisteredUriManaged reg_uri;

                // Getting original uri info.
                reg_uri.original_uri_info_string = reg_uris_[i].get_original_uri_info();
                reg_uri.original_uri_info_len_chars = reg_uris_[i].get_original_uri_info_len_chars();

                // Getting processed uri info.
                reg_uri.processed_uri_info_string = reg_uris_[i].get_processed_uri_info();
                reg_uri.processed_uri_info_len_chars = reg_uris_[i].get_processed_uri_info_len_chars();

                reg_uris_[i].WriteUserParameters(reg_uri.param_types, &reg_uri.num_params);

                // TODO: Resolve this hack with only positive handler ids in generated code.
                reg_uri.handler_id = i + 1;

                uris_vec.push_back(reg_uri);
            }
        }

        return uris_vec;
    }

    // Constructor.
    RegisteredUris(uint16_t port_number)
    {
        latest_gen_dll_handle_ = NULL;
        clang_engine_ = NULL;
        latest_match_uri_func_ = NULL;
        port_number_ = port_number;
    }

    // Destructor.
    ~RegisteredUris();

    // Invalidates code generation.
    void InvalidateUriMatcherFunction()
    {
        latest_match_uri_func_ = NULL;
    }

    // Runs the generated URI matcher and gets handler information as a result.
    uri_index_type RunCodegenUriMatcher(char* uri_info, uint32_t uri_info_len, uint8_t* params_storage)
    {
        // Pointing to parameters storage.
        MixedCodeConstants::UserDelegateParamInfo** out_params = (MixedCodeConstants::UserDelegateParamInfo**)&params_storage;

        // TODO: Resolve this hack with only positive handler ids in generated code.
        return latest_match_uri_func_(uri_info, uri_info_len, out_params) - 1;
    }

    // Printing the registered URIs.
    void PrintRegisteredUris(std::stringstream& stats_stream, uint16_t port_num)
    {
        stats_stream << "Following URIs are registered: " << "<br>";
        for (int32_t i = 0; i < reg_uris_.get_num_entries(); i++)
        {
            stats_stream << "    \"" << reg_uris_[i].get_processed_uri_info() << "\" in";

            // Checking if its gateway or database URI.
            if (!reg_uris_[i].get_is_gateway_uri())
            {
                // Database handler.
                stats_stream << " database \"" << g_gateway.GetDatabase(reg_uris_[i].GetFirstDbIndex())->get_db_name() << "\"";
            }
            else
            {
                // Gateway handler.
                stats_stream << " gateway";
            }

            stats_stream << "<br>";
        }
    }

    // Checking if entry already exists.
    bool ContainsEntry(char* uri)
    {
        int32_t index = FindRegisteredUri(uri);
        return (index >= 0);
    }

    // Checking if entry already exists.
    bool ContainsEntry(char* uri, db_index_type db_index)
    {
        int32_t index = FindRegisteredUri(uri);

        // Checking if entry found.
        if (index >= 0)
        {
            // Checking database index.
            if (reg_uris_[index].ContainsDb(db_index))
                return true;
        }

        return false;
    }

    // Getting entry by index.
    RegisteredUri* GetEntryByIndex(int32_t index)
    {
        return reg_uris_.GetElemPtr(index);
    }

    // Adding new entry.
    void AddNewUri(RegisteredUri& new_entry)
    {
        // Adding new entry to the back.
        reg_uris_.Add(new_entry);

        // Invalidating URI matcher.
        InvalidateUriMatcherFunction();
    }

    // Checking if registered URIs is empty.
    bool IsEmpty()
    {
        return reg_uris_.IsEmpty();
    }

    // Removes certain URI.
    void RemoveUriByIndex(int32_t index)
    {
        // Removing entry.
        reg_uris_.RemoveByIndex(index);

        // Invalidating URI matcher.
        InvalidateUriMatcherFunction();
    }

    // Removing certain entry.
    bool RemoveEntry(HandlersList* handlers_list)
    {
        bool removed = false;

        // Going through all entries.
        for (int32_t i = 0; i < reg_uris_.get_num_entries(); i++)
        {
            if (reg_uris_[i].RemoveEntry(handlers_list))
            {
                if (reg_uris_[i].IsEmpty())
                {
                    // Removing entry.
                    RemoveUriByIndex(i);
                    --i;
                }

                removed = true;

                // Not stopping, going through all entries.
            }
        }

        return removed;
    }

    // Removing certain entry.
    bool RemoveEntry(db_index_type db_index)
    {
        bool removed = false;

        // Going through all entries.
        for (int32_t i = 0; i < reg_uris_.get_num_entries(); ++i)
        {
            if (reg_uris_[i].RemoveEntry(db_index))
            {
                if (reg_uris_[i].IsEmpty())
                {
                    // Removing entry.
                    RemoveUriByIndex(i);
                    --i;
                }

                removed = true;

                // Not stopping, going through all entries.
            }
        }

        return removed;
    }

    // Removing certain entry.
    bool RemoveEntry(char* processed_uri_info)
    {
        int32_t index = FindRegisteredUri(processed_uri_info);

        // Checking if entry found.
        if (index >= 0)
        {
            RemoveUriByIndex(index);
            return true;
        }

        return false;
    }

    // Removing certain entry.
    bool RemoveEntry(db_index_type db_index, char* processed_uri_info)
    {
        bool removed = false;

        // Trying to find entry first.
        int32_t index = FindRegisteredUri(processed_uri_info);

        // If entry was found.
        if (index >= 0)
        {
            if (reg_uris_[index].RemoveEntry(db_index))
            {
                if (reg_uris_[index].IsEmpty())
                    RemoveUriByIndex(index);
                
                removed = true;

                // Not stopping, going through all entries.
            }
        }

        return removed;
    }

    // Find certain URI entry.
    uri_index_type FindRegisteredUri(const char* processed_uri_info)
    {
        // Going through all entries.
        for (uri_index_type i = 0; i < reg_uris_.get_num_entries(); i++)
        {
            // Doing exact comparison.
            if (!strcmp(processed_uri_info, reg_uris_[i].get_processed_uri_info()))
            {
                return i;
            }
        }

        // Returning negative if nothing is found.
        return INVALID_URI_INDEX;
    }
};

class HttpProto
{
    // Structure that holds HTTP request.
    HttpRequest http_request_;

public:

    // Getting HTTP request.
    HttpRequest* get_http_request()
    {
        return &http_request_;
    }

    // HTTP/WS parser callbacks.
    static int OnMessageComplete(http_parser* p);
    static int OnMessageBegin(http_parser* p);
    static int OnBody(http_parser* p, const char *at, size_t length);
    static int OnHeadersComplete(http_parser* p);
    static int OnUri(http_parser* p, const char *at, size_t length);
    static int OnHeaderField(http_parser* p, const char *at, size_t length);
    static int OnHeaderValue(http_parser* p, const char *at, size_t length);

    // Processes the session information.
    void ProcessSessionString(SocketDataChunkRef sd, const char* session_id_start);

    // Resets the HTTP/WS structure.
    void Reset()
    {
        
    }

    // Initializes the HTTP/WS structure.
    void Init()
    {
        http_request_.Reset();
    }

    // Resets the parser related fields.
    void ResetParser(SocketDataChunkRef sd);

    // Entry point for outer data processing.
    uint32_t HttpUriDispatcher(HandlersList* hl, GatewayWorker *gw, SocketDataChunkRef sd, BMX_HANDLER_TYPE handler_id, bool* is_handled);

    // Standard HTTP/WS handler once URI is determined.
    uint32_t AppsHttpWsProcessData(HandlersList* hl, GatewayWorker *gw, SocketDataChunkRef sd, BMX_HANDLER_TYPE handler_id, bool* is_handled);

    // Parses the HTTP request and pushes processed data to database.
    uint32_t GatewayHttpWsProcessEcho(
        HandlersList* hl,
        GatewayWorker *gw,
        SocketDataChunkRef sd,
        BMX_HANDLER_TYPE handler_id,
        bool* is_handled);

#ifdef GW_PROXY_MODE

    // Reverse proxies the HTTP traffic.
    uint32_t GatewayHttpWsReverseProxy(
        HandlersList* hl,
        GatewayWorker *gw,
        SocketDataChunkRef sd,
        BMX_HANDLER_TYPE handler_id,
        bool* is_handled);
#endif
};

const char* const kHttpStatsHeader =
    "HTTP/1.1 200 OK\r\n"
    "Content-Type: text/html\r\n"
    "Cache-control: no-cache\r\n"
    "Content-Length: @@@@@@@@\r\n"
    "\r\n";

const int32_t kHttpStatsHeaderLength = static_cast<int32_t> (strlen(kHttpStatsHeader));

const int32_t kHttpStatsHeaderInsertPoint = static_cast<int32_t> (strstr(kHttpStatsHeader, "@") - kHttpStatsHeader);

const char* const kHttpEchoResponse =
    "HTTP/1.1 200 OK\r\n"
    "Content-Type: text/html\r\n"
    "Content-Length: 8\r\n"
    "\r\n"
    "@@@@@@@@";

const int32_t kHttpEchoResponseLength = static_cast<int32_t> (strlen(kHttpEchoResponse));

const int32_t kHttpEchoResponseInsertPoint = static_cast<int32_t> (strstr(kHttpEchoResponse, "@") - kHttpEchoResponse);

const int32_t kHttpEchoContentLength = 8;

} // namespace network
} // namespace starcounter

#endif // HTTP_PROTO_HPP
