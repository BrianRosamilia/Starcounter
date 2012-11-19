#include "http_parser.h"
#include "http_request.hpp"

namespace starcounter {
namespace network {

// Structure that is used during parsing.
struct HttpParserStruct
{
    // HttpProto is also an http_parser.
    http_parser http_parser_;

    // Structure that holds HTTP request.
    HttpRequest* http_request_;

    // HTTP/WebSockets fields.
    HttpWsFields last_field_;

    // Indicates if we have complete header.
    bool complete_header_flag_;

    // Pointer to request buffer.
    uint8_t* request_buf_;

    // Resets the parser related fields.
    void Reset(uint8_t* request_buf, HttpRequest* http_request)
    {
        request_buf_ = request_buf;
        http_request_ = http_request;

        last_field_ = UNKNOWN_FIELD;
        memset(http_request_, 0, sizeof(HttpRequest));
        http_parser_init(&http_parser_, HTTP_REQUEST);
        complete_header_flag_ = false;
    }
};

inline int OnMessageBegin(http_parser* p)
{
    // Do nothing.
    return 0;
}

inline int OnHeadersComplete(http_parser* p)
{
    HttpParserStruct *http = (HttpParserStruct *)p;

    // Setting complete header flag.
    http->complete_header_flag_ = true;

    // Setting headers length (skipping 4 bytes for \r\n\r\n).
    http->http_request_->headers_len_bytes_ = p->nread - 4 - http->http_request_->headers_len_bytes_;

    return 0;
}

inline int OnMessageComplete(http_parser* p)
{
    // Do nothing.
    return 0;
}

inline int OnUri(http_parser* p, const char *at, size_t length)
{
    HttpParserStruct *http = (HttpParserStruct *)p;

    // Setting the reference to URI.
    http->http_request_->uri_offset_ = (uint32_t)(at - (char*)http->request_buf_);
    http->http_request_->uri_len_bytes_ = (uint32_t)length;

    return 0;
}

inline int OnHeaderField(http_parser* p, const char *at, size_t length)
{
    HttpParserStruct *http = (HttpParserStruct *)p;

    // Determining what header field is that.
    http->last_field_ = DetermineField(at, length);

    // Saving header offset.
    http->http_request_->header_offsets_[http->http_request_->num_headers_] = (uint32_t)(at - (char*)http->request_buf_);
    http->http_request_->header_len_bytes_[http->http_request_->num_headers_] = (uint32_t)length;

    // Setting headers beginning.
    if (!http->http_request_->headers_offset_)
    {
        http->http_request_->headers_len_bytes_ = (uint32_t)(p->nread - length - 1);
        http->http_request_->headers_offset_ = (uint32_t)(at - (char*)http->request_buf_);
    }

    return 0;
}

inline int OnHeaderValue(http_parser* p, const char *at, size_t length)
{
    HttpParserStruct *http = (HttpParserStruct *)p;

    // Saving header length.
    http->http_request_->header_value_offsets_[http->http_request_->num_headers_] = (uint32_t)(at - (char*)http->request_buf_);
    http->http_request_->header_value_len_bytes_[http->http_request_->num_headers_] = (uint32_t)length;

    // Increasing number of saved headers.
    http->http_request_->num_headers_++;
    if (http->http_request_->num_headers_ >= MAX_HTTP_HEADERS)
    {
        // Too many HTTP headers.
        std::cout << "Too many HTTP headers detected, maximum allowed: " << MAX_HTTP_HEADERS << std::endl;
        return 1;
    }

    // Processing last field type.
    switch (http->last_field_)
    {
        case COOKIE_FIELD:
        {
            // Check if its an old session from a different socket.
            const char* session_id_value = GetSessionIdValue(at, length);

            // Checking if Starcounter session id is presented.
            if (session_id_value)
            {
                // Setting the session offset.
                http->http_request_->session_string_offset_ = (uint32_t)(session_id_value - (char*)http->request_buf_);
                http->http_request_->session_string_len_bytes_ = SC_SESSION_STRING_LEN_CHARS;
            }

            // Setting needed HttpRequest fields.
            http->http_request_->cookies_offset_ = (uint32_t)(at - (char*)http->request_buf_);
            http->http_request_->cookies_len_bytes_ = (uint32_t)length;

            break;
        }

        case CONTENT_LENGTH_FIELD:
        {
            // Calculating body length.
            http->http_request_->body_len_bytes_ = ParseDecimalStringToUint(at, length);

            break;
        }

        case ACCEPT_ENCODING_FIELD:
        {
            // Checking if Gzip is accepted.
            int32_t i = 0;
            while (i < length)
            {
                if (at[i] == 'g')
                {
                    if (at[i + 1] == 'z')
                    {
                        http->http_request_->gzip_accepted_ = true;
                        break;
                    }
                }
                i++;
            }

            break;
        }

        case ACCEPT_FIELD:
        {
            http->http_request_->accept_value_offset_ = (uint32_t)(at - (char*)http->request_buf_);
            http->http_request_->accept_value_len_bytes_ = (uint32_t)length;

            break;
        }
    }

    return 0;
}

inline int OnBody(http_parser* p, const char *at, size_t length)
{
    HttpParserStruct *http = (HttpParserStruct *)p;

    // Setting body parameters.
    if (http->http_request_->body_len_bytes_ <= 0)
        http->http_request_->body_len_bytes_ = (uint32_t)length;

    // Setting body data offset.
    http->http_request_->body_offset_ = (uint32_t)(at - (char*)http->request_buf_);

    return 0;
}

// Global HTTP parser settings.
http_parser_settings* g_httpParserSettings = NULL;

// Declaring thread-safe parser structure.
__declspec(thread) HttpParserStruct thread_parser;

// Initializes the internal Apps HTTP request parser.
EXTERN_C uint32_t sc_init_http_parser()
{
    g_httpParserSettings = new http_parser_settings();

    // Setting HTTP callbacks.
    g_httpParserSettings->on_body = OnBody;
    g_httpParserSettings->on_header_field = OnHeaderField;
    g_httpParserSettings->on_header_value = OnHeaderValue;
    g_httpParserSettings->on_headers_complete = OnHeadersComplete;
    g_httpParserSettings->on_message_begin = OnMessageBegin;
    g_httpParserSettings->on_message_complete = OnMessageComplete;
    g_httpParserSettings->on_url = OnUri;

    return 0;
}

// Parses HTTP request from the given buffer and returns corresponding instance of HttpRequest.
EXTERN_C uint32_t sc_parse_http_request(uint8_t* request_buf, uint32_t request_size_bytes, uint8_t* out_http_request)
{
    assert(g_httpParserSettings != NULL);

    // Resetting the parser structure.
    thread_parser.Reset(request_buf, (HttpRequest*)out_http_request);

    // Executing HTTP parser.
    size_t bytes_parsed = http_parser_execute(
        (http_parser *)&thread_parser,
        g_httpParserSettings,
        (const char *)request_buf,
        request_size_bytes);

    // Checking if we have complete data.
    if (!thread_parser.complete_header_flag_)
    {
        std::cout << "Incomplete HTTP request headers supplied!" << std::endl;

        return SCERRAPPSHTTPPARSERINCOMPLETEHEADERS;
    }

    // Checking that all bytes are parsed.
    if (bytes_parsed != request_size_bytes)
    {
        std::cout << "Provided HTTP request has incorrect data!" << std::endl;

        return SCERRAPPSHTTPPARSERINCORRECT;
    }

    HttpRequest* http_request = thread_parser.http_request_;

    // Getting the HTTP method.
    http_method method = (http_method)thread_parser.http_parser_.method;
    switch (method)
    {
    case http_method::HTTP_GET: 
        http_request->http_method_ = bmx::HTTP_METHODS::GET_METHOD;
        break;

    case http_method::HTTP_POST: 
        http_request->http_method_ = bmx::HTTP_METHODS::POST_METHOD;
        break;

    case http_method::HTTP_PUT: 
        http_request->http_method_ = bmx::HTTP_METHODS::PUT_METHOD;
        break;

    case http_method::HTTP_DELETE: 
        http_request->http_method_ = bmx::HTTP_METHODS::DELETE_METHOD;
        break;

    case http_method::HTTP_HEAD: 
        http_request->http_method_ = bmx::HTTP_METHODS::HEAD_METHOD;
        break;

    case http_method::HTTP_OPTIONS: 
        http_request->http_method_ = bmx::HTTP_METHODS::OPTIONS_METHOD;
        break;

    case http_method::HTTP_TRACE: 
        http_request->http_method_ = bmx::HTTP_METHODS::TRACE_METHOD;
        break;

    case http_method::HTTP_PATCH: 
        http_request->http_method_ = bmx::HTTP_METHODS::PATCH_METHOD;
        break;

    default: 
        http_request->http_method_ = bmx::HTTP_METHODS::OTHER_METHOD;
        break;
    }

    // TODO: Check body length.

    // Setting request properties.
    http_request->request_offset_ = 0;
    http_request->request_len_bytes_ = request_size_bytes;

    return 0;
}

} // namespace network
} // namespace starcounter