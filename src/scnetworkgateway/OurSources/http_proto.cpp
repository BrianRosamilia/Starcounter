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

const char* const kHttpGatewayPongResponse =
    "HTTP/1.1 200 OK\r\n"
    "Content-Type: text/html; charset=UTF-8\r\n"
    "Content-Length: 5\r\n"
    "\r\n"
    "Pong!";

const int32_t kHttpGatewayPongResponseLength = static_cast<int32_t> (strlen(kHttpGatewayPongResponse));

const char* const kHttpServiceUnavailable =
    "HTTP/1.1 503 Service Unavailable\r\n"
    "Content-Length: 0\r\n"
    "\r\n";

const int32_t kHttpServiceUnavailableLength = static_cast<int32_t> (strlen(kHttpServiceUnavailable));

const char* const kHttpTooBigUpload =
    "HTTP/1.1 413 Request Entity Too Large\r\n"
    "Content-Type: text/html; charset=UTF-8\r\n"
    "Content-Length: 50\r\n"
    "\r\n"
    "Maximum supported HTTP request content size is 32 Mb!";

const int32_t kHttpTooBigUploadLength = static_cast<int32_t> (strlen(kHttpTooBigUpload));

const char* const kHttpNoContent =
    "HTTP/1.1 204 No Content\r\n"
    "Content-Length: 0\r\n"
    "\r\n";

const int32_t kHttpNoContentLength = static_cast<int32_t> (strlen(kHttpNoContent));

const char* const kHttpBadRequest =
    "HTTP/1.1 400 Bad Request\r\n"
    "Content-Length: 0\r\n"
    "\r\n";

const int32_t kHttpBadRequestLength = static_cast<int32_t> (strlen(kHttpBadRequest));

const char* const kHttpNotFoundPrefix =
    "HTTP/1.1 404 Not Found\r\n"
    "Content-Type: text/html; charset=UTF-8\r\n"
    "Content-Length: ";

const int32_t kHttpNotFoundPrefixLength = static_cast<int32_t> (strlen(kHttpNotFoundPrefix));

const char* const kHttpNotFoundMessage = "URI not found: ";
const int32_t kHttpNotFoundMessageLength = static_cast<int32_t> (strlen(kHttpNotFoundMessage));

//////////////////////////////////////////////////////////
/////////////////THREAD STATIC DATA///////////////////////
//////////////////////////////////////////////////////////

__declspec(thread) http_parser g_ts_http_parser_;
__declspec(thread) uint8_t g_ts_last_field_;
__declspec(thread) bool g_xhreferer_read_;
__declspec(thread) SocketDataChunk* g_ts_sd_;
__declspec(thread) HttpRequest* g_ts_http_request_;

// Constructs HTTP 404 response.
inline int32_t ConstructHttp404(uint8_t* const dest, const int32_t dest_max_bytes, const char* uri, const int32_t uri_len)
{
    GW_ASSERT(dest_max_bytes > 128);

    int32_t content_len = kHttpNotFoundMessageLength + uri_len;

    if (content_len >= dest_max_bytes - 128)
        content_len = kHttpNotFoundMessageLength;

    char cont_len_string[16];
    _itoa_s(content_len, cont_len_string, 16, 10);

    int32_t offset = 0;
    offset = InjectData(dest, offset, kHttpNotFoundPrefix, kHttpNotFoundPrefixLength);
    offset = InjectData(dest, offset, cont_len_string, static_cast<int32_t>(strlen(cont_len_string)));
    offset = InjectData(dest, offset, "\r\n\r\n", 4);
    offset = InjectData(dest, offset, kHttpNotFoundMessage, kHttpNotFoundMessageLength);

    if (content_len > kHttpNotFoundMessageLength)
        offset = InjectData(dest, offset, uri, uri_len);

    return offset;
}

// Destructor.
RegisteredUris::~RegisteredUris()
{
    if (clang_engine_)
    {
        g_gateway.ClangDestroyEngineFunc(clang_engine_);
        clang_engine_ = NULL;
    }
}

// Running all registered handlers.
uint32_t RegisteredUri::RunHandlers(GatewayWorker *gw, SocketDataChunkRef sd, bool* is_handled)
{
    uint32_t err_code;

    // Going through all handler list.
    for (int32_t i = 0; i < handler_lists_.get_num_entries(); i++)
    {
        // Ensuring that initial database is zero.
        //GW_ASSERT(0 == sd->get_db_index());

        // Running handlers.
        err_code = handler_lists_[i]->RunHandlers(gw, sd, is_handled);

        // Checking if information was handled and no errors occurred.
        if (*is_handled || err_code)
            return err_code;
    }

    return 0;
}

// Fetches method and URI from HTTP request data.
inline uint32_t GetMethodAndUri(
    char* http_data,
    uint32_t http_data_len,
    uint32_t* out_method_and_uri_len,
    uint32_t* out_uri_offset,
    uint32_t uri_max_len)
{
    uint32_t pos = 0;

    // Reading method.
    while (pos < http_data_len)
    {
        if (http_data[pos] == ' ')
            break;

        pos++;
    }

    // Copying offset to URI.
    *out_uri_offset = pos + 1;

    // Reading URI.
    pos++;
    while (pos < http_data_len)
    {
        if (http_data[pos] == ' ')
        {
            pos++;

            break;
        }

        pos++;
    }

    // TODO!
    // Checking that we have HTTP protocol.
    if (pos < http_data_len)
    {
        // Checking for HTTP keyword.
        if (*(uint32_t*)(http_data + pos) != *(uint32_t*)"HTTP")
        {
            // Wrong protocol.
            return SCERRGWNONHTTPPROTOCOL;
        }
    }
    else
    {
        // Either wrong protocol or not enough accumulated data.
        return SCERRGWNONHTTPPROTOCOL;
    }

    // Checking if method and URI has correct length.
    if (pos < uri_max_len)
    {
        // Setting output length.
        *out_method_and_uri_len = pos;

        return 0;
    }

    // Wrong protocol.
    return SCERRGWNONHTTPPROTOCOL;
}

// Fetches method and URI from HTTP request data.
inline uint32_t GetMethodAndUriLowerCase(
    char* http_data,
    uint32_t http_data_len,
    char* out_methoduri_lower_case,
    uint32_t* out_len,
    uint32_t* out_uri_offset,
    uint32_t uri_max_len)
{
    uint32_t pos = 0;

    // Reading method.
    while (pos < http_data_len)
    {
        if (http_data[pos] == ' ')
            break;

        pos++;
    }

    // Copying offset to URI.
    *out_uri_offset = pos + 1;

    // Reading URI.
    pos++;
    while (pos < http_data_len)
    {
        if (http_data[pos] == ' ')
        {
            pos++;
            break;
        }

        pos++;
    }

    // TODO!
    // Checking that we have HTTP protocol.
    if (pos < http_data_len)
    {
        // Checking for HTTP keyword.
        if (*(uint32_t*)(http_data + pos) != *(uint32_t*)"HTTP")
        {
            // Wrong protocol.
            return SCERRGWNONHTTPPROTOCOL;
        }
    }
    else
    {
        // Either wrong protocol or not enough accumulated data.
        return SCERRGWNONHTTPPROTOCOL;
    }

    // Checking if method and URI has correct length.
    if (pos < uri_max_len)
    {
        // Copying string.
        strncpy_s(out_methoduri_lower_case, pos + 1, http_data, pos);

        // Converting to lower case.
        _strlwr_s(out_methoduri_lower_case + (*out_uri_offset), pos + 1 - (*out_uri_offset));

        // Setting output length.
        *out_len = pos;

        return 0;
    }

    // Wrong protocol.
    return SCERRGWNONHTTPPROTOCOL;
}

inline int HttpProto::OnMessageBegin(http_parser* p)
{
#ifdef GW_HTTP_DIAG
    GW_COUT << "OnMessageBegin" << GW_ENDL;
#endif

    return 0;
}

inline int HttpProto::OnHeadersComplete(http_parser* p)
{
#ifdef GW_HTTP_DIAG
    GW_COUT << "OnHeadersComplete" << GW_ENDL;
#endif

    // Setting complete header flag.
    g_ts_sd_->set_complete_header_flag();
    
    // NOTE: p->nread already points to the content after additional \r\n
    // thats why we subtracting 2 bytes.

    // Setting headers length (skipping 2 bytes for \r\n).
    g_ts_http_request_->headers_len_bytes_ = p->nread - 2 - g_ts_http_request_->headers_len_bytes_;

    // Setting content data offset.
    g_ts_http_request_->content_offset_ = g_ts_sd_->GetAccumOrigBufferSocketDataOffset() + p->nread;

    return 0;
}

inline int HttpProto::OnMessageComplete(http_parser* p)
{
#ifdef GW_HTTP_DIAG
    GW_COUT << "OnMessageComplete" << GW_ENDL;
#endif

    return 0;
}

inline int HttpProto::OnUri(http_parser* p, const char *at, size_t length)
{
#ifdef GW_HTTP_DIAG
    char at_ref[256];
    memcpy(at_ref, at, length);
    at_ref[length] = '\0';
    GW_COUT << "OnUri: " << at_ref << GW_ENDL;
#endif

    // Setting the reference to URI.
    g_ts_http_request_->uri_offset_ = static_cast<uint16_t>(at - (char *)g_ts_sd_);
    g_ts_http_request_->uri_len_bytes_ = static_cast<uint16_t>(length);

    return 0;
}

inline int HttpProto::OnHeaderField(http_parser* p, const char *at, size_t length)
{
#ifdef GW_HTTP_DIAG
    char at_ref[256];
    memcpy(at_ref, at, length);
    at_ref[length] = '\0';
    GW_COUT << "OnHeaderField: " << at_ref << GW_ENDL;
#endif

    // Determining what header field is that.
    g_ts_last_field_ = DetermineField(at, length);

    // Setting headers beginning.
    if (!g_ts_http_request_->headers_offset_)
    {
        g_ts_http_request_->headers_len_bytes_ = static_cast<uint16_t>(p->nread - length - 1);
        g_ts_http_request_->headers_offset_ = static_cast<uint16_t>(at - (char*)g_ts_sd_);
    }

    return 0;
}

// Processes the session information.
inline void HttpProto::ProcessSessionString(SocketDataChunkRef sd, const char* session_id_start)
{
    // Parsing the session.
    sd->GetSessionStruct()->FillFromString(session_id_start, MixedCodeConstants::SESSION_STRING_LEN_CHARS);

    // Setting the session offset.
    http_request_.session_string_offset_ = static_cast<uint16_t>(session_id_start - (char*)sd);

    // Checking if we have session related socket.
    sd->SetGlobalSessionIfEmpty();

    // Comparing with global session now.
    // NOTE: We don't care what session the socket has.
    /*if (!sd->CompareGlobalSessionSalt())
    {
#ifdef GW_SESSIONS_DIAG
        GW_COUT << "Session stored in the HTTP header is wrong/outdated." << GW_ENDL;
#endif

        // Resetting the session information.
        http_request_.session_string_offset_ = 0;
        sd->ResetSdSession();
    }*/
}

inline int HttpProto::OnHeaderValue(http_parser* p, const char *at, size_t length)
{
#ifdef GW_HTTP_DIAG
    char at_ref[256];
    memcpy(at_ref, at, length);
    at_ref[length] = '\0';
    GW_COUT << "OnHeaderValue: " << at_ref << GW_ENDL;
#endif

    // Processing last field type.
    switch (g_ts_last_field_)
    {
        case REFERRER_FIELD:
        {
            // Do nothing if X-Referer field is already processed.
            if (g_xhreferer_read_)
                break;
        }

        case XREFERRER_FIELD:
        {
            // Pointing to the actual value of a session.
            const char* session_id_start = at + length - MixedCodeConstants::SESSION_STRING_LEN_CHARS;

            // Checking if Starcounter session id is presented.
            if ((MixedCodeConstants::SESSION_STRING_LEN_CHARS < length) &&
                (*(session_id_start - 1) == '/'))
            {
               g_ts_sd_->get_http_proto()->ProcessSessionString(g_ts_sd_, session_id_start);

               // Checking if X-Referer field is read.
               if (XREFERRER_FIELD == g_ts_last_field_)
                   g_xhreferer_read_ = true;
            }

            break;
        }

        case COOKIE_FIELD:
        {
            // Null terminating the string.
            char c = at[length - 1];
            ((char*)at)[length - 1] = '\0';

            // Checking if session is contained within cookies.
            const char* session_cookie = strstr(at, MixedCodeConstants::ScSessionCookieName);
            ((char*)at)[length - 1] = c;

            // XReferer has higher priority and overrides session value.
            if (session_cookie && !g_xhreferer_read_)
            {
                // Skipping session cookie name and equals character.
                int32_t offset = MixedCodeConstants::ScSessionCookieNameLength + 1;
                while (session_cookie[offset] == ' ')
                    offset++;

                g_ts_sd_->get_http_proto()->ProcessSessionString(g_ts_sd_, session_cookie + offset);
            }

            break;
        }

        case CONTENT_LENGTH_FIELD:
        {
            // Calculating content length.
            g_ts_http_request_->content_len_bytes_ = (int32_t) p->content_length;

            break;
        }

        case ACCEPT_ENCODING_FIELD:
        {
            // Null-terminating the string.
            char* atnull = (char*)at;
            char c = atnull[length];
            atnull[length] = '\0';

            // Checking if Gzip is accepted.
            if (strstr(atnull, "gzip"))
                g_ts_http_request_->gzip_accepted_ = true;

            // Restoring old character.
            atnull[length] = c;

            break;
        }

        case UPGRADE_FIELD:
        {
            // Double checking if its a WebSocket upgrade.
            if (*(uint32_t*)(at + 4) != *(uint32_t*)"ocke")
                return SCERRGWHTTPNONWEBSOCKETSUPGRADE;

            break;
        }

        case WS_KEY_FIELD:
        {
            GW_ASSERT_DEBUG(24 == length);
            g_ts_sd_->get_ws_proto()->SetClientKey((char *)at, static_cast<int32_t>(length));
            break;
        }

        case WS_PROTOCOL_FIELD:
        {
            if (length > 32)
                return SCERRGWHTTPINCORRECTDATA;

            g_ts_sd_->get_ws_proto()->SetSubProtocol((char *)at, static_cast<int32_t>(length));
            break;
        }

        case WS_VERSION_FIELD:
        {
            // Checking the WebSocket protocol version.
            if (*(uint16_t*)at != *(uint16_t*)"13")
                return SCERRGWHTTPWRONGWEBSOCKETSVERSION;

            break;
        }
    }

    return 0;
}

inline int HttpProto::OnBody(http_parser* p, const char *at, size_t length)
{
#ifdef GW_HTTP_DIAG
    GW_COUT << "OnBody" << GW_ENDL;
#endif

    // Setting content parameters.
    if (g_ts_http_request_->content_len_bytes_ < 0)
        g_ts_http_request_->content_len_bytes_ = static_cast<uint32_t>(length);

    return 0;
}

// Global HTTP parser settings.
http_parser_settings g_httpParserSettings;

void HttpGlobalInit()
{
    // Setting HTTP callbacks.
    g_httpParserSettings.on_body = HttpProto::OnBody;
    g_httpParserSettings.on_header_field = HttpProto::OnHeaderField;
    g_httpParserSettings.on_header_value = HttpProto::OnHeaderValue;
    g_httpParserSettings.on_headers_complete = HttpProto::OnHeadersComplete;
    g_httpParserSettings.on_message_begin = HttpProto::OnMessageBegin;
    g_httpParserSettings.on_message_complete = HttpProto::OnMessageComplete;
    g_httpParserSettings.on_url = HttpProto::OnUri;
}

// Determines the correct HTTP handler.
uint32_t HttpProto::HttpUriDispatcher(
    HandlersList* hl,
    GatewayWorker *gw,
    SocketDataChunkRef sd,
    BMX_HANDLER_TYPE handler_index,
    bool* is_handled)
{

#ifdef GW_TESTING_MODE

    // Checking if we are in gateway HTTP mode.
    if (MODE_GATEWAY_HTTP == g_gateway.setting_mode())
        return GatewayHttpWsProcessEcho(hl, gw, sd, handler_index, is_handled);

#endif

    // Checking if data goes to user code.
    if (sd->get_to_database_direction_flag())
    {
        // Checking if we are already passed the WebSockets handshake.
        if (sd->IsWebSocket())
            return sd->get_ws_proto()->ProcessWsDataToDb(gw, sd, handler_index, is_handled);

        // Obtaining method and URI.
        char* method_and_uri = (char*)sd->get_accum_buf()->get_chunk_orig_buf_ptr();
        uint32_t method_and_uri_len, uri_offset;

        // Getting method and URI information.
        uint32_t err_code = GetMethodAndUri(
            method_and_uri,
            sd->get_accum_buf()->get_accum_len_bytes(),
            &method_and_uri_len,
            &uri_offset,
            MixedCodeConstants::MAX_URI_STRING_LEN);

        /*
        // TODO: Support alternative lower-case strategy when URI didn't match.
        method_and_uri_lower_case = gw->get_uri_lower_case();

        // Converting URI to lower case.
        err_code = GetMethodAndUriLowerCase(
            (char*)sd->get_accum_buf()->get_orig_buf_ptr(),
            sd->get_accum_buf()->get_accum_len_bytes(),
            method_and_uri_lower_case,
            &method_and_uri_len,
            &uri_offset,
            bmx::MAX_URI_STRING_LEN);
        */

        // Checking for any errors.
        if (err_code)
        {
#ifdef GW_PROXY_MODE

            // Checking if we are proxying.
            if (sd->HasProxySocket())
            {
                // Set the unknown proxied protocol here.
                sd->set_unknown_proxied_proto_flag();

                // Just running proxy processing.
                return GatewayHttpWsReverseProxy(NULL, gw, sd, handler_index, is_handled);
            }
#endif

            // Returning socket to receiving state.
            err_code = gw->Receive(sd);
            GW_ERR_CHECK(err_code);

            // Handled successfully.
            *is_handled = true;

            return 0;
        }

        // Now we have method and URI and ready to search specific URI handler.

        // Getting the corresponding port number.
        ServerPort* server_port = g_gateway.get_server_port(sd->GetPortIndex());
        uint16_t port_num = server_port->get_port_number();
        RegisteredUris* port_uris = server_port->get_registered_uris();

        // Determining which matched handler to pick.
        uri_index_type matched_index = INVALID_URI_INDEX;

        // Checking if URI matching code is generated.
        if (NULL == port_uris->get_latest_match_uri_func())
        {
            // Checking if there are any port URIs registered,
            if (port_uris->IsEmpty())
                return SCERRREQUESTONUNREGISTEREDURI;

            // Entering global lock.
            gw->EnterGlobalLock();

            // Checking once again since maybe it was already generated.
            if (NULL == port_uris->get_latest_match_uri_func())
            {
                // Generating and loading URI matcher.
                err_code = g_gateway.GenerateUriMatcher(port_uris);
            }

            // Releasing global lock.
            gw->LeaveGlobalLock();

            if (err_code)
                return err_code;
        }

        // Getting the matched uri index.
        matched_index = port_uris->RunCodegenUriMatcher(method_and_uri, method_and_uri_len, sd->get_accept_or_params_data());

        // Checking if we failed to find again.
        if (matched_index < 0)
        {
            // Handled successfully.
            *is_handled = true;

            // Sending resource not found and closing the connection.
            sd->set_disconnect_after_send_flag();

            // Creating 404 message.
            char stack_temp_mem[512];
            int32_t resp_len_bytes = ConstructHttp404((uint8_t*)stack_temp_mem, 512, method_and_uri, method_and_uri_len);

            return gw->SendPredefinedMessage(sd, stack_temp_mem, resp_len_bytes);
        }

        // Getting matched URI index.
        RegisteredUri* matched_uri = port_uris->GetEntryByIndex(matched_index);

        // Setting matched URI index.
        sd->SetDestDbIndex(matched_uri->GetFirstDbIndex());

        // Checking if we have a session parameter.
        if (matched_uri->get_session_param_index() != INVALID_PARAMETER_INDEX)
        {
            MixedCodeConstants::UserDelegateParamInfo* p = ((MixedCodeConstants::UserDelegateParamInfo*)sd->get_accept_or_params_data()) + matched_uri->get_session_param_index();
            ProcessSessionString(sd, method_and_uri + p->offset_);
        }

        // Setting determined HTTP URI settings (e.g. for reverse proxy).
        sd->get_http_proto()->http_request_.uri_offset_ = sd->GetAccumOrigBufferSocketDataOffset() + uri_offset;
        sd->get_http_proto()->http_request_.uri_len_bytes_ = method_and_uri_len - uri_offset;

        // Running determined handler now.
        return matched_uri->RunHandlers(gw, sd, is_handled);
    }
    else
    {
        // Just running standard response processing.
        return AppsHttpWsProcessData(hl, gw, sd, handler_index, is_handled);
    }

    return 0;
}

// Resets the parser related fields.
void HttpProto::ResetParser(SocketDataChunkRef sd)
{
    g_ts_last_field_ = UNKNOWN_FIELD;
    g_ts_http_request_ = sd->get_http_proto()->get_http_request();
    g_ts_sd_ = sd;
    g_xhreferer_read_ = false;

    http_request_.Reset();

    http_parser_init(&g_ts_http_parser_, HTTP_REQUEST);
}

// Parses the HTTP request and pushes processed data to database.
uint32_t HttpProto::AppsHttpWsProcessData(
    HandlersList* hl,
    GatewayWorker *gw,
    SocketDataChunkRef sd,
    BMX_HANDLER_TYPE handler_id,
    bool* is_handled)
{
    uint32_t err_code;

    // Checking if data goes to user code.
    if (sd->get_to_database_direction_flag())
    {
        // Getting reference to accumulative buffer.
        AccumBuffer* accum_buf = sd->get_accum_buf();

        // Checking if we are in fill-up mode.
        if (sd->get_complete_header_flag())
            goto ALL_DATA_ACCUMULATED;

        // Checking if we are already passed the WebSockets handshake.
        if (sd->IsWebSocket())
            return sd->get_ws_proto()->ProcessWsDataToDb(gw, sd, handler_id, is_handled);

        // Resetting the parsing structure.
        ResetParser(sd);

        // We can immediately set the request offset.
        http_request_.request_offset_ = sd->GetAccumOrigBufferSocketDataOffset();

        // Executing HTTP parser.
        size_t bytes_parsed = http_parser_execute(
            &g_ts_http_parser_,
            &g_httpParserSettings,
            (const char *)accum_buf->get_chunk_orig_buf_ptr(),
            accum_buf->get_accum_len_bytes());

        // Checking if we should continue receiving the headers.
        if (!sd->get_complete_header_flag())
        {
            // NOTE: At this point we don't really know here what the final size of the request will be.
            // That is why we just extend the chunk to next bigger one, without specifying the size.

            // Checking if any space left in chunk.
            if (sd->get_accum_buf()->get_chunk_num_available_bytes() <= 0)
            {
                err_code = SocketDataChunk::ChangeToBigger(gw, sd);

                if (err_code)
                    return err_code;
            }

            // Returning socket to receiving state.
            err_code = gw->Receive(sd);
            if (err_code)
                return err_code;

            // Handled successfully.
            *is_handled = true;

            return 0;
        }

        // Checking if we have WebSockets upgrade.
        if (g_ts_http_parser_.upgrade)
        {
#ifdef GW_WEBSOCKET_DIAG
            GW_COUT << "Upgrade to another protocol detected, data: " << GW_ENDL;
#endif

            // Perform WebSockets handshake.
            return sd->get_ws_proto()->DoHandshake(gw, sd, handler_id, is_handled);
        }
        // Handle error. Usually just close the connection.
        else if (bytes_parsed != (accum_buf->get_accum_len_bytes()))
        {
            GW_ASSERT(bytes_parsed < accum_buf->get_accum_len_bytes());

            GW_COUT << "HTTP packet has incorrect data!" << GW_ENDL;
            return SCERRGWHTTPINCORRECTDATA;
        }
        // Standard HTTP.
        else
        {
            // Getting the HTTP method.
            http_method method = (http_method)g_ts_http_parser_.method;
            switch (method)
            {
            case http_method::HTTP_GET: 
                http_request_.http_method_ = bmx::HTTP_METHODS::GET_METHOD;
                break;

            case http_method::HTTP_POST: 
                http_request_.http_method_ = bmx::HTTP_METHODS::POST_METHOD;
                break;

            case http_method::HTTP_PUT: 
                http_request_.http_method_ = bmx::HTTP_METHODS::PUT_METHOD;
                break;

            case http_method::HTTP_DELETE: 
                http_request_.http_method_ = bmx::HTTP_METHODS::DELETE_METHOD;
                break;

            case http_method::HTTP_HEAD: 
                http_request_.http_method_ = bmx::HTTP_METHODS::HEAD_METHOD;
                break;

            case http_method::HTTP_OPTIONS: 
                http_request_.http_method_ = bmx::HTTP_METHODS::OPTIONS_METHOD;
                break;

            case http_method::HTTP_TRACE: 
                http_request_.http_method_ = bmx::HTTP_METHODS::TRACE_METHOD;
                break;

            case http_method::HTTP_PATCH: 
                http_request_.http_method_ = bmx::HTTP_METHODS::PATCH_METHOD;
                break;

            default: 
                http_request_.http_method_ = bmx::HTTP_METHODS::OTHER_METHOD;
                break;
            }

            // Calculating total request size.
            http_request_.request_len_bytes_ = http_request_.content_offset_ - http_request_.request_offset_ + http_request_.content_len_bytes_;

            // Checking if we have any content at all.
            if (http_request_.content_len_bytes_ > 0)
            {
                GW_ASSERT(http_request_.content_offset_ > http_request_.headers_offset_);

                // Checking if we need to continue receiving the content.
                if (http_request_.request_len_bytes_ > accum_buf->get_accum_len_bytes())
                {
                    // Checking for maximum supported HTTP request content size.
                    if (http_request_.content_len_bytes_ > g_gateway.setting_maximum_receive_content_length())
                    {
                        // Handled successfully.
                        *is_handled = true;

                        // Setting disconnect after send flag.
                        sd->set_disconnect_after_send_flag();

#ifdef GW_WARNINGS_DIAG
                        GW_COUT << "Maximum supported HTTP request content size is 32 Mb!" << GW_ENDL;
#endif

                        // Sending corresponding HTTP response.
                        return gw->SendPredefinedMessage(sd, kHttpTooBigUpload, kHttpTooBigUploadLength);
                    }

                    // Setting the desired number of bytes to accumulate.
                    err_code = gw->StartAccumulation(
                        sd,
                        http_request_.request_len_bytes_,
                        accum_buf->get_accum_len_bytes());

                    if (err_code)
                        return err_code;

                    // Handled successfully.
                    *is_handled = true;

                    // Checking if we have not accumulated everything yet.
                    return gw->Receive(sd);
                }
            }

ALL_DATA_ACCUMULATED:

            // We don't need complete header flag anymore.
            sd->reset_complete_header_flag();

#ifdef GW_COLLECT_SOCKET_STATISTICS
            g_gateway.IncrementNumProcessedHttpRequests();
#endif

            // Skipping cloning when in testing mode.
#ifndef GW_TESTING_MODE

            // Aggregation is done separately.
            if (!sd->GetSocketAggregatedFlag())
            {
                // Posting cloning receive since all data is accumulated.
                err_code = sd->CloneToReceive(gw);
                if (err_code)
                    return err_code;
            }

#endif

            // Checking type of response.
#ifdef GW_PONG_MODE

            // Sending Pong response.
            err_code = gw->SendPredefinedMessage(sd, kHttpGatewayPongResponse, kHttpGatewayPongResponseLength);
            if (err_code)
                return err_code;

#else

            // Resetting user data parameters.
            sd->ResetUserDataOffset();

#ifdef GW_LOOPBACK_AGGREGATION
            if (sd->GetSocketAggregatedFlag())
            {
                char body[1024];
                int32_t body_len = http_request_.content_len_bytes_;
                memcpy(body, (char*)sd + http_request_.content_offset_, body_len);
                err_code = gw->SendHttpBody(sd, body, body_len);
                if (err_code)
                    return err_code;
            }
            else
#endif
            // Push chunk to corresponding channel/scheduler.
            err_code = gw->PushSocketDataToDb(sd, handler_id);
            if (err_code)
                return err_code;

#endif

            // Printing the outgoing packet.
#ifdef GW_HTTP_DIAG
            GW_COUT << sd->get_accum_buf()->get_chunk_orig_buf_ptr() << GW_ENDL;
#endif
        }

        // Handled successfully.
        *is_handled = true;

        return 0;
    }
    // Checking if data comes from user code.
    else
    {
        // Checking if we are already passed the WebSockets handshake.
        if (sd->IsWebSocket())
            return sd->get_ws_proto()->ProcessWsDataFromDb(gw, sd, handler_id, is_handled);

        // Handled successfully.
        *is_handled = true;

        // Checking if we want to disconnect the socket.
        if (sd->get_disconnect_socket_flag())
            return SCERRGWDISCONNECTFLAG;

        // Prepare buffer to send outside.
        sd->PrepareForSend(sd->UserDataBuffer(), sd->get_user_data_length_bytes());

        // Sending data.
        err_code = gw->Send(sd);
        GW_ERR_CHECK(err_code);

        return 0;
    }

    return SCERRGWHTTPPROCESSFAILED;
}

#ifdef GW_TESTING_MODE

// Parses the HTTP request and pushes processed data to database.
uint32_t HttpProto::GatewayHttpWsProcessEcho(
    HandlersList* hl,
    GatewayWorker *gw,
    SocketDataChunkRef sd,
    BMX_HANDLER_TYPE handler_id,
    bool* is_handled)
{
    uint32_t err_code;

    // Getting reference to accumulative buffer.
    AccumBuffer* accum_buf = sd->get_accum_buf();

    // Checking in what mode we are.
    if (g_gateway.setting_is_master())
    {
        // Checking if we are in fill-up mode.
        if (sd->get_complete_header_flag())
            goto ALL_DATA_ACCUMULATED;

        // Checking if we are already passed the WebSockets handshake.
        if (sd->IsWebSocket())
            return sd->get_ws_proto()->ProcessWsDataToDb(gw, sd, handler_id, is_handled);

        // Resetting the parsing structure.
        ResetParser(sd);

        // We can immediately set the request offset.
        http_request_.request_offset_ = sd->GetAccumOrigBufferSocketDataOffset();

        // Executing HTTP parser.
        size_t bytes_parsed = http_parser_execute(
            &g_ts_http_parser_,
            &g_httpParserSettings,
            (const char *)accum_buf->get_chunk_orig_buf_ptr(),
            accum_buf->get_accum_len_bytes());

        // Checking if we should continue receiving the headers.
        if (!sd->get_complete_header_flag())
        {
            // NOTE: At this point we don't really know here what the final size of the request will be.
            // That is why we just extend the chunk to next bigger one, without specifying the size.

            // Checking if any space left in chunk.
            if (sd->get_accum_buf()->get_chunk_num_available_bytes() <= 0)
            {
                err_code = SocketDataChunk::ChangeToBigger(gw, sd);

                if (err_code)
                    return err_code;
            }

            // Returning socket to receiving state.
            err_code = gw->Receive(sd);
            if (err_code)
                return err_code;

            // Handled successfully.
            *is_handled = true;

            return 0;
        }

        // Checking if we have WebSockets upgrade.
        if (g_ts_http_parser_.upgrade)
        {
#ifdef GW_WEBSOCKET_DIAG
            GW_COUT << "Upgrade to another protocol detected, data: " << GW_ENDL;
#endif

            // Perform WebSockets handshake.
            return sd->get_ws_proto()->DoHandshake(gw, sd, handler_id, is_handled);
        }
        // Handle error. Usually just close the connection.
        else if (bytes_parsed != (accum_buf->get_accum_len_bytes()))
        {
            GW_ASSERT(bytes_parsed < accum_buf->get_accum_len_bytes());

            GW_COUT << "HTTP packet has incorrect data!" << GW_ENDL;
            return SCERRGWHTTPINCORRECTDATA;
        }
        // Standard HTTP.
        else
        {
            // Getting the HTTP method.
            http_method method = (http_method)g_ts_http_parser_.method;
            switch (method)
            {
            case http_method::HTTP_GET: 
                http_request_.http_method_ = bmx::HTTP_METHODS::GET_METHOD;
                break;

            case http_method::HTTP_POST: 
                http_request_.http_method_ = bmx::HTTP_METHODS::POST_METHOD;
                break;

            case http_method::HTTP_PUT: 
                http_request_.http_method_ = bmx::HTTP_METHODS::PUT_METHOD;
                break;

            case http_method::HTTP_DELETE: 
                http_request_.http_method_ = bmx::HTTP_METHODS::DELETE_METHOD;
                break;

            case http_method::HTTP_HEAD: 
                http_request_.http_method_ = bmx::HTTP_METHODS::HEAD_METHOD;
                break;

            case http_method::HTTP_OPTIONS: 
                http_request_.http_method_ = bmx::HTTP_METHODS::OPTIONS_METHOD;
                break;

            case http_method::HTTP_TRACE: 
                http_request_.http_method_ = bmx::HTTP_METHODS::TRACE_METHOD;
                break;

            case http_method::HTTP_PATCH: 
                http_request_.http_method_ = bmx::HTTP_METHODS::PATCH_METHOD;
                break;

            default: 
                http_request_.http_method_ = bmx::HTTP_METHODS::OTHER_METHOD;
                break;
            }

            // Calculating total request size.
            http_request_.request_len_bytes_ = http_request_.content_offset_ - http_request_.request_offset_ + http_request_.content_len_bytes_;

            // Checking if we have any content at all.
            if (http_request_.content_len_bytes_ > 0)
            {
                GW_ASSERT(http_request_.content_offset_ > http_request_.headers_offset_);

                // Checking if we need to continue receiving the content.
                if (http_request_.request_len_bytes_ > accum_buf->get_accum_len_bytes())
                {
                    // Checking for maximum supported HTTP request content size.
                    if (http_request_.content_len_bytes_ > g_gateway.setting_maximum_receive_content_length())
                    {
                        // Handled successfully.
                        *is_handled = true;

                        // Setting disconnect after send flag.
                        sd->set_disconnect_after_send_flag();

#ifdef GW_WARNINGS_DIAG
                        GW_COUT << "Maximum supported HTTP request content size is 32 Mb!" << GW_ENDL;
#endif

                        // Sending corresponding HTTP response.
                        return gw->SendPredefinedMessage(sd, kHttpTooBigUpload, kHttpTooBigUploadLength);
                    }

                    // Setting the desired number of bytes to accumulate.
                    err_code = gw->StartAccumulation(
                        sd,
                        http_request_.request_len_bytes_,
                        accum_buf->get_accum_len_bytes());

                    if (err_code)
                        return err_code;

                    // Handled successfully.
                    *is_handled = true;

                    // Checking if we have not accumulated everything yet.
                    return gw->Receive(sd);
                }
            }

ALL_DATA_ACCUMULATED:

            // We don't need complete header flag anymore.
            sd->reset_complete_header_flag();

#ifdef GW_COLLECT_SOCKET_STATISTICS
            g_gateway.IncrementNumProcessedHttpRequests();
#endif

            // Translating HTTP content.
            GW_ASSERT(http_request_.content_len_bytes_ == kHttpEchoContentLength);

            // Converting the string to number.
            //echo_id_type echo_id = hex_string_to_uint64((char*)sd + http_request_.content_offset_, kHttpGatewayEchoRequestBodyLength);

            // Saving echo string into temporary buffer.
            char copied_echo_string[kHttpEchoContentLength];
            memcpy(copied_echo_string, (char*)sd + http_request_.content_offset_, kHttpEchoContentLength);

            // Coping echo HTTP response.
            memcpy(sd->get_accum_buf()->get_chunk_orig_buf_ptr(), kHttpEchoResponse, kHttpEchoResponseLength);

            // Inserting echo id into HTTP response.
            memcpy(sd->get_accum_buf()->get_chunk_orig_buf_ptr() + kHttpEchoResponseInsertPoint, copied_echo_string, kHttpEchoContentLength);

            // Sending echo response.
            err_code = gw->SendPredefinedMessage(sd, NULL, kHttpEchoResponseLength);
            if (err_code)
                return err_code;

            // Handled successfully.
            *is_handled = true;

            return 0;
        }
    }
    else
    {
        // Asserting correct number of bytes received.
        GW_ASSERT(sd->get_accum_buf()->get_accum_len_bytes() == kHttpEchoResponseLength);

        // Obtaining original echo number.
        //echo_id_type echo_id = *(int32_t*)sd->get_accum_buf()->get_orig_buf_ptr();
        echo_id_type echo_id = hex_string_to_uint64((char*)sd->get_accum_buf()->get_chunk_orig_buf_ptr() + kHttpEchoResponseInsertPoint, kHttpEchoContentLength);

#ifdef GW_ECHO_STATISTICS
        GW_COUT << "Received echo: " << echo_id << GW_ENDL;
#endif

#ifdef GW_LIMITED_ECHO_TEST
        // Confirming received echo.
        g_gateway.ConfirmEcho(echo_id);
#endif

        // Handled successfully.
        *is_handled = true;

        // Checking if all echo responses are returned.
        if (g_gateway.CheckConfirmedEchoResponses(gw))
        {
            return SCERRGWTESTFINISHED;
                        
            /*
            EnterGlobalLock();
            g_gateway.ResetEchoTests();
            LeaveGlobalLock();
            return 0;
            */
        }
        else
        {
            goto SEND_HTTP_ECHO_TO_MASTER;
        }

SEND_HTTP_ECHO_TO_MASTER:

        // Checking that not all echoes are sent.
        if (!g_gateway.AllEchoesSent())
        {
            // Generating echo number.
            echo_id_type new_echo_num = 0;

#ifdef GW_LIMITED_ECHO_TEST
            new_echo_num = g_gateway.GetNextEchoNumber();
#endif

            // Sending echo request to server.
            return gw->SendHttpEcho(sd, new_echo_num);
        }
    }

    return SCERRGWHTTPPROCESSFAILED;
}

// HTTP/WebSockets handler for Gateway.
uint32_t GatewayUriProcessEcho(HandlersList* hl, GatewayWorker *gw, SocketDataChunkRef sd, BMX_HANDLER_TYPE handler_id, bool* is_handled)
{
    return sd->get_http_proto()->GatewayHttpWsProcessEcho(hl, gw, sd, handler_id, is_handled);
}

#endif

// Outer HTTP/WebSockets handler.
uint32_t OuterUriProcessData(HandlersList* hl, GatewayWorker *gw, SocketDataChunkRef sd, BMX_HANDLER_TYPE handler_id, bool* is_handled)
{
    return sd->get_http_proto()->HttpUriDispatcher(hl, gw, sd, handler_id, is_handled);
}

// HTTP/WebSockets handler for Apps.
uint32_t AppsUriProcessData(HandlersList* hl, GatewayWorker *gw, SocketDataChunkRef sd, BMX_HANDLER_TYPE handler_id, bool* is_handled)
{
    return sd->get_http_proto()->AppsHttpWsProcessData(hl, gw, sd, handler_id, is_handled);
}

#ifdef GW_PROXY_MODE

// HTTP/WebSockets handler for Gateway proxy.
uint32_t GatewayUriProcessProxy(HandlersList* hl, GatewayWorker *gw, SocketDataChunkRef sd, BMX_HANDLER_TYPE handler_id, bool* is_handled)
{
    return sd->get_http_proto()->GatewayHttpWsReverseProxy(hl, gw, sd, handler_id, is_handled);
}

// Reverse proxies the HTTP traffic.
uint32_t HttpProto::GatewayHttpWsReverseProxy(
    HandlersList* hl,
    GatewayWorker *gw,
    SocketDataChunkRef sd,
    BMX_HANDLER_TYPE handler_id,
    bool* is_handled)
{
    uint32_t err_code;

    // Not handled yet.
    *is_handled = true;

    // Checking if already in proxy mode.
    if (sd->HasProxySocket())
    {
        // Aggregation is done separately.
        if (!sd->GetSocketAggregatedFlag())
        {
            // Posting cloning receive for client.
            err_code = sd->CloneToReceive(gw);
            if (err_code)
                return err_code;
        }

        // Making sure that sd is just send.
        sd->reset_socket_representer_flag();

        // Finished receiving from proxied server,
        // now sending to the original user.
        sd->ExchangeToProxySocket();

        // Setting number of bytes to send.
        sd->get_accum_buf()->PrepareToSendOnProxy();

        // Sending data to user.
        return gw->Send(sd);
    }
    else // We have not started a proxy mode yet.
    {
        // Creating new socket to proxied server.
        err_code = gw->CreateProxySocket(sd);
        if (err_code)
            return err_code;

#ifdef GW_SOCKET_DIAG
        GW_COUT << "Created proxy socket: " << sd->get_socket_info_index() << ":" << sd->GetSocket() << ":" << sd->get_unique_socket_id() << ":" << (uint64_t)sd << GW_ENDL;
#endif

        // Re-enabling socket representer flag.
        sd->set_socket_representer_flag();

        // Setting number of bytes to send.
        sd->get_accum_buf()->PrepareToSendOnProxy();

        // Getting proxy information.
        ReverseProxyInfo* proxy_info = hl->get_reverse_proxy_info();

        // Connecting to the server.
        return gw->Connect(sd, &proxy_info->destination_addr_);
    }

    return SCERRGWHTTPPROCESSFAILED;
}

#endif

// HTTP/WebSockets statistics for Gateway.
uint32_t GatewayStatisticsInfo(HandlersList* hl, GatewayWorker *gw, SocketDataChunkRef sd, BMX_HANDLER_TYPE handler_id, bool* is_handled)
{
    int32_t resp_len_bytes;
    const char* stats_page_string = g_gateway.GetGlobalStatisticsString(&resp_len_bytes);
    *is_handled = true;

    return gw->SendPredefinedMessage(sd, stats_page_string, resp_len_bytes);
}

// POST sockets for Gateway.
uint32_t PostSocketResource(HandlersList* hl, GatewayWorker *gw, SocketDataChunkRef sd, BMX_HANDLER_TYPE handler_id, bool* is_handled)
{
    GW_ASSERT(false == sd->GetSocketAggregatedFlag());

    // Cloning for receiving immediately.
    sd->CloneToReceive(gw);

    // Getting the aggregation info.
    AggregationStruct ags = *(AggregationStruct*) (sd->get_accum_buf()->get_chunk_orig_buf_ptr() + sd->get_accum_buf()->get_accum_len_bytes() - AggregationStructSizeBytes);

    // Getting port handler.
    int32_t port_index = g_gateway.FindServerPortIndex(ags.port_number_);
    GW_ASSERT(INVALID_PORT_INDEX != port_index);

    // Getting new socket index.
    ags.socket_info_index_ = g_gateway.ObtainFreeSocketIndex(gw, INVALID_SOCKET, port_index, false);
    ags.unique_socket_id_ = g_gateway.GetUniqueSocketId(ags.socket_info_index_);

    // Setting some socket options.
    g_gateway.SetSocketAggregatedFlag(ags.socket_info_index_);

    char temp_buf[AggregationStructSizeBytes];
    *(AggregationStruct*) temp_buf = ags;

    *is_handled = true;

    return gw->SendHttpBody(sd, temp_buf, AggregationStructSizeBytes);
}

// DELETE sockets for Gateway.
uint32_t DeleteSocketResource(HandlersList* hl, GatewayWorker *gw, SocketDataChunkRef sd, BMX_HANDLER_TYPE handler_id, bool* is_handled)
{
    // Getting the aggregation info.
    AggregationStruct ags = *(AggregationStruct*) (sd->get_accum_buf()->get_chunk_orig_buf_ptr() + sd->get_accum_buf()->get_accum_len_bytes() - AggregationStructSizeBytes);

    // Checking if socket is legitimate.
    if (g_gateway.CompareUniqueSocketId(ags.socket_info_index_, ags.unique_socket_id_))
    {
        // Closing socket which will results in stop of all pending operations on that socket.
        gw->AddSocketToDisconnectListUnsafe(ags.socket_info_index_);
    }

    char temp_buf[AggregationStructSizeBytes];
    *(AggregationStruct*) temp_buf = ags;

    *is_handled = true;

    return gw->SendHttpBody(sd, temp_buf, AggregationStructSizeBytes);
}

} // namespace network
} // namespace starcounter
