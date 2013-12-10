#pragma once
#ifndef HTTP_COMMON_HPP
#define HTTP_COMMON_HPP

#pragma warning(push)
#pragma warning(disable: 4163)
#include "../../../Chunks/bmx/bmx.hpp"
#pragma warning(pop)

#include "../../../Starcounter.Internal/Constants/MixedCodeConstants.cs"

namespace starcounter {
namespace network {

// HTTP/WebSockets fields.
enum HttpWsFields
{
    SCSESSIONID_FIELD,
    REFERRER_FIELD,
    XREFERRER_FIELD,
    COOKIE_FIELD,
    CONTENT_LENGTH_FIELD,
    ACCEPT_FIELD,
    ACCEPT_ENCODING_FIELD,
    UPGRADE_FIELD,
    WS_KEY_FIELD,
    WS_VERSION_FIELD,
    WS_PROTOCOL_FIELD,
    WS_EXTENSIONS_FIELD,
    WS_ACCEPT_FIELD,
    UNKNOWN_FIELD
};

const int64_t ACCEPT_HEADER_VALUE_8BYTES = 2322296583949083457;
const int64_t ACCEPT_ENCODING_HEADER_VALUE_8BYTES = 4984768388655178561;
const int64_t REFERER_HEADER_VALUE_8BYTES = 4211540143546721618;
const int64_t XREFERER_HEADER_VALUE_8BYTES = 7310016635636690264;
const int64_t CONTENT_LENGTH_HEADER_VALUE_8BYTES = 3275364211029339971;
const int64_t UPGRADE_HEADER_VALUE_8BYTES = 4207879796541583445;
const int64_t WEBSOCKET_HEADER_VALUE_8BYTES = 6008476277963711827;
const int64_t COOKIE_HEADER_VALUE_8BYTES = 2322280061311348547;

// Fast way to determine field type.
inline HttpWsFields DetermineField(const char *at, size_t length)
{
    int64_t header_8bytes = *(int64_t*)at;
    switch(header_8bytes)
    {
        case REFERER_HEADER_VALUE_8BYTES:
        {
            return REFERRER_FIELD; // Referer
        }

        case XREFERER_HEADER_VALUE_8BYTES:
        {
            if (*(int64_t*)(at + 2) == *(int64_t*)"Referer:")
                return XREFERRER_FIELD; // X-Referer

            break;
        }

        case COOKIE_HEADER_VALUE_8BYTES:
        {
            return COOKIE_FIELD; // Cookie
        }

        case ACCEPT_ENCODING_HEADER_VALUE_8BYTES:
        {
            if (*(int64_t*)(at + 7) == *(int64_t*)"Encoding")
                return ACCEPT_ENCODING_FIELD; // Accept-Encoding

            break;
        }

        case CONTENT_LENGTH_HEADER_VALUE_8BYTES:
        {
            if (*(int64_t*)(at + 6) == *(int64_t*)"t-Length")
                return CONTENT_LENGTH_FIELD; // Content-Length

            break;
        }

        case UPGRADE_HEADER_VALUE_8BYTES:
        {
            return UPGRADE_FIELD; // Upgrade
        }
        
        case WEBSOCKET_HEADER_VALUE_8BYTES:
        {
            switch(length)
            {
                case 17: return WS_KEY_FIELD; // Sec-WebSocket-Key
                case 21: return WS_VERSION_FIELD; // Sec-WebSocket-Version
                case 22: return WS_PROTOCOL_FIELD; // Sec-WebSocket-Protocol
                case 24: return WS_EXTENSIONS_FIELD; // Sec-WebSocket-Extensions
                case 20: return WS_ACCEPT_FIELD; // Sec-WebSocket-Accept
            }

            break;
        }
    }

    return UNKNOWN_FIELD;
}

// Parses decimal string into unsigned number.
inline uint32_t ParseStringToUint(const char *at, size_t length)
{
    uint32_t result = 0;
    int32_t mult = 1, i = (int32_t)length - 1;
    while (i >= 0)
    {
        // Checking for white space character.
        switch (at[i])
        {
            case ' ':
            case '\n':
            case '\t':
            case '\r':
            {
                i--;
                continue;
            }
        }

        // Adding to result.
        result += (at[i] - '0') * mult;

        --i;
        mult *= 10;
    }

    return result;
}

// Searching for the Starcounter session cookie among other cookies.
// Returns pointer to Starcounter session cookie value.
/*inline const char* GetSessionIdValueString(const char *at, size_t length)
{
    // Immediately excluding wrong cookie.
    if (length < kScFullSessionIdStringLength)
        return NULL;

    // Null terminating the string.
    char* atnull = (char*)at;
    char c = atnull[length];
    atnull[length] = '\0';

    // Searching using optimized strstr.
    const char* session_string = strstr(atnull, kScSessionIdStringPlus);

    // Restoring back the old character.
    atnull[length] = c;

    // Checking if session was found.
    if (session_string)
        return session_string + kScSessionIdStringPlusLength;

    return NULL;
}*/

} // namespace network
} // namespace starcounter

#endif // HTTP_COMMON_HPP
