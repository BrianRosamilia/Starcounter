//
// bmx.hpp
//
// 
//
// Copyright © 2006-2013 Starcounter AB. All rights reserved.
// Starcounter® is a registered trademark of Starcounter AB.
//

#ifndef BMX_HPP
#define BMX_HPP

#if defined(_MSC_VER) && (_MSC_VER >= 1200)
# pragma once
#endif // defined(_MSC_VER) && (_MSC_VER >= 1200)

#define WIN32_LEAN_AND_MEAN
#include <winsock2.h>
#undef WIN32_LEAN_AND_MEAN

#include <cstdint>
#include <list>
#include <vector>
#include <cassert>
#include "../../Starcounter.Internal/Constants/MixedCodeConstants.cs"
#include "coalmine.h"
#include "sccoredbg2.h"
#include "sccoredb.h"
#include "chunk_helper.h"
#include "..\common\chunk.hpp"

// BMX task information.
struct TASK_INFO_TYPE
{
    uint8_t flags;
    uint8_t scheduler_number;
    BMX_HANDLER_INDEX_TYPE handler_index;
    uint8_t client_worker_id;
    starcounter::core::chunk_index the_chunk_index;
};

// User handler callback.
typedef uint32_t (__stdcall *GENERIC_HANDLER_CALLBACK)(
    uint64_t session_id,
    shared_memory_chunk* smc, 
    TASK_INFO_TYPE* task_info,
    bool* is_handled
);

namespace starcounter
{
namespace bmx
{
    typedef uint32_t BMX_SUBPORT_TYPE;

    // NOTE: Excluding original chunk since its for extra linked.
    const uint32_t MAX_EXTRA_LINKED_WSABUFS = MixedCodeConstants::CHUNK_MAX_DATA_BYTES / sizeof(WSABUF) - 1;
    const uint32_t MAX_BYTES_EXTRA_LINKED_WSABUFS = (MAX_EXTRA_LINKED_WSABUFS - 1) * MixedCodeConstants::CHUNK_MAX_DATA_BYTES;

    // Invalid BMX handler info.
    const BMX_HANDLER_TYPE BMX_INVALID_HANDLER_INFO = ~((BMX_HANDLER_TYPE) 0);

    // Invalid BMX handler index.
    const BMX_HANDLER_INDEX_TYPE BMX_INVALID_HANDLER_INDEX = ~((BMX_HANDLER_INDEX_TYPE) 0);

    // Predefined BMX management handler.
    const BMX_HANDLER_TYPE BMX_MANAGEMENT_HANDLER_ID = 0;

    inline BMX_HANDLER_TYPE MakeHandlerInfo(BMX_HANDLER_INDEX_TYPE handler_index, BMX_HANDLER_UNIQUE_NUM_TYPE unique_num)
    {
        return (((uint64_t)unique_num) << 16) | handler_index;
    }

    const BMX_HANDLER_TYPE BMX_MANAGEMENT_HANDLER_INFO = MakeHandlerInfo(bmx::BMX_MANAGEMENT_HANDLER_ID, 1);

    // Maximum total number of registered handlers.
    const uint32_t MAX_TOTAL_NUMBER_OF_HANDLERS = 256;

    // Maximum number of the same handlers in a list.
    const uint32_t MAX_NUMBER_OF_HANDLERS_IN_LIST = 8;

    // BMX message types.
    const uint8_t BMX_REGISTER_PORT = 0;
    const uint8_t BMX_REGISTER_PORT_SUBPORT = 1;
    const uint8_t BMX_REGISTER_URI = 2;
    const uint8_t BMX_UNREGISTER = 3;
    const uint8_t BMX_ERROR = 4;
    const uint8_t BMX_SESSION_DESTROY = 5;
    const uint8_t BMX_PING = 254;
    const uint8_t BMX_PONG = 255;

    // Supported HTTP methods.
    enum HTTP_METHODS
    {
        GET_METHOD,
        POST_METHOD,
        PUT_METHOD,
        DELETE_METHOD,
        HEAD_METHOD,
        OPTIONS_METHOD,
        TRACE_METHOD,
        PATCH_METHOD,    
        OTHER_METHOD
    };

    // Entrance to process any BMX message.
    extern uint32_t OnIncomingBmxMessage(
        uint64_t session_id,
        shared_memory_chunk* smc,
        TASK_INFO_TYPE* task_info,
        bool* is_handled
        );

    // Type of handler.
    enum HANDLER_TYPE
    {
        UNUSED_HANDLER,
        PORT_HANDLER,
        SUBPORT_HANDLER,
        URI_HANDLER
    };

    class BmxData;

    // Handler list.
    class HandlersList
    {
        // Type of the underlying handler.
        bmx::HANDLER_TYPE type_;

        // Assigned handler info.
        BMX_HANDLER_TYPE handler_info_;

        // Current number of handlers.
        uint8_t num_entries_;

        // User handler callbacks.
        GENERIC_HANDLER_CALLBACK handlers_[bmx::MAX_NUMBER_OF_HANDLERS_IN_LIST];

        // Port number.
        uint16_t port_;

        // Sub-port number.
        BMX_SUBPORT_TYPE subport_;

        // URI string.
        char* original_uri_info_;
        char* processed_uri_info_;

        uint32_t original_uri_info_len_chars_;
        uint32_t processed_uri_info_len_chars_;

        uint8_t num_params_;
        uint8_t param_types_[MixedCodeConstants::MAX_URI_CALLBACK_PARAMS];

        starcounter::MixedCodeConstants::NetworkProtocolType proto_type_;

        HandlersList(const HandlersList&);
        HandlersList& operator=(const HandlersList&);

    public:

        // Constructor.
        explicit HandlersList()
        {
            original_uri_info_ = NULL;
            processed_uri_info_ = NULL;

            Unregister();
        }

        // Getting handler index.
        BMX_HANDLER_INDEX_TYPE get_handler_index()
        {
            return GetBmxHandlerIndex(handler_info_);
        }

        // Getting handler id.
        BMX_HANDLER_TYPE get_handler_info()
        {
            return handler_info_;
        }

        // Getting number of entries.
        uint8_t get_num_entries()
        {
            return num_entries_;
        }

        // Getting all handlers.
        GENERIC_HANDLER_CALLBACK* get_handlers()
        {
            return handlers_;
        }

        // Gets sub-port number.
        BMX_SUBPORT_TYPE get_subport()
        {
            return subport_;
        }

        // Gets port number.
        uint16_t get_port()
        {
            return port_;
        }

        // Gets URI.
        char* get_original_uri_info()
        {
            return original_uri_info_;
        }

        // Gets URI.
        char* get_processed_uri_info()
        {
            return processed_uri_info_;
        }

        // Get URI length.
        uint32_t get_original_uri_info_len_chars()
        {
            return original_uri_info_len_chars_;
        }

        // Get URI length.
        uint32_t get_processed_uri_info_len_chars()
        {
            return processed_uri_info_len_chars_;
        }

        // Get number of URI callback parameters.
        int32_t get_num_params()
        {
            return num_params_;
        }

        // Gets handler type.
        bmx::HANDLER_TYPE get_type()
        {
            return type_;
        }

        // Find existing handler.
        bool HandlerAlreadyExists(GENERIC_HANDLER_CALLBACK handler_callback)
        {
            // Going through all registered handlers.
            for (uint8_t i = 0; i < num_entries_; ++i)
            {
                if (handler_callback == handlers_[i])
                    return true;
            }

            return false;
        }

        // Adds user handler.
        uint32_t AddUserHandler(GENERIC_HANDLER_CALLBACK handler_callback)
        {
            // Reached maximum amount of handlers.
            if (num_entries_ >= bmx::MAX_NUMBER_OF_HANDLERS_IN_LIST)
                return SCERRMAXHANDLERSREACHED;

            // Checking if handler already exists.
            if (HandlerAlreadyExists(handler_callback))
                return SCERRHANDLERALREADYREGISTERED;

            // Adding handler to array.
            handlers_[num_entries_] = handler_callback;
            num_entries_++;

            return 0;
        }

        // Init.
        uint32_t Init(
            bmx::HANDLER_TYPE type,
            BMX_HANDLER_TYPE handler_info,
            uint16_t port,
            BMX_SUBPORT_TYPE subport,
            char* original_uri_info,
            uint32_t original_uri_len_chars,
            char* processed_uri_info,
            uint32_t processed_uri_len_chars,
            uint8_t* param_types,
            int32_t num_params,
            starcounter::MixedCodeConstants::NetworkProtocolType proto_type)
        {
            _SC_ASSERT(original_uri_len_chars < MixedCodeConstants::MAX_URI_STRING_LEN);
            _SC_ASSERT(processed_uri_len_chars < MixedCodeConstants::MAX_URI_STRING_LEN);

            num_entries_ = 0;

            type_ = type;
            port_ = port;

            subport_ = subport;
            handler_info_ = handler_info;

            original_uri_info_len_chars_ = original_uri_len_chars;
            processed_uri_info_len_chars_ = processed_uri_len_chars;

            // Deleting previous allocations if any.
            if (original_uri_info_)
                delete original_uri_info_;
            if (processed_uri_info_)
                delete processed_uri_info_;

            // Allocating space for new URI infos.
            original_uri_info_ = new char[original_uri_info_len_chars_ + 1];
            processed_uri_info_ = new char[processed_uri_info_len_chars_ + 1];

            num_params_ = num_params;
            if (num_params_ > 0)
                memcpy(param_types_, param_types, MixedCodeConstants::MAX_URI_CALLBACK_PARAMS);

            proto_type_ = proto_type;

            // Checking the type of handler.
            switch(type)
            {
                case bmx::HANDLER_TYPE::PORT_HANDLER:
                {
                    break;
                }

                case bmx::HANDLER_TYPE::SUBPORT_HANDLER:
                {
                    break;
                }

                case bmx::HANDLER_TYPE::URI_HANDLER:
                {
                    // Copying the URI string.
                    if (original_uri_len_chars > 0)
                        strncpy_s(original_uri_info_, original_uri_info_len_chars_ + 1, original_uri_info, original_uri_len_chars);

                    if (processed_uri_len_chars > 0)
                        strncpy_s(processed_uri_info_, processed_uri_info_len_chars_ + 1, processed_uri_info, processed_uri_len_chars);

                    break;
                }

                default:
                {
                    _SC_ASSERT(false);
                }
            }

            return 0;
        }

        // Writes needed port handler data into chunk.
        uint32_t WriteRegisteredPortHandler(response_chunk_part *resp_chunk)
        {
            // Checking if message fits the chunk.
            if ((starcounter::core::chunk_size - resp_chunk->get_offset() - shared_memory_chunk::link_size) <=
                sizeof(BMX_REGISTER_PORT) + sizeof(handler_info_) + sizeof(port_))
            {
                return 0;
            }

            resp_chunk->write(BMX_REGISTER_PORT);
            resp_chunk->write(handler_info_);
            resp_chunk->write(port_);

            return resp_chunk->get_offset();
        }

        // Writes needed subport handler data into chunk.
        uint32_t WriteRegisteredSubPortHandler(response_chunk_part *resp_chunk)
        {
            // Checking if message fits the chunk.
            if ((starcounter::core::chunk_size - resp_chunk->get_offset() - shared_memory_chunk::link_size) <=
                sizeof(BMX_REGISTER_PORT_SUBPORT) + sizeof(handler_info_) + sizeof(port_) + sizeof(subport_))
            {
                return 0;
            }

            resp_chunk->write(BMX_REGISTER_PORT_SUBPORT);
            resp_chunk->write(handler_info_);
            resp_chunk->write(port_);
            resp_chunk->write(subport_);

            return resp_chunk->get_offset();
        }

        // Writes needed URI handler data into chunk.
        uint32_t WriteRegisteredUriHandler(response_chunk_part *resp_chunk)
        {
            // Checking if message fits the chunk.
            if ((starcounter::core::chunk_size - resp_chunk->get_offset() - shared_memory_chunk::link_size) <=
                sizeof(BMX_REGISTER_URI) + sizeof(handler_info_) + sizeof(port_) + original_uri_info_len_chars_ + processed_uri_info_len_chars_ + 1)
            {
                return 0;
            }

            resp_chunk->write(BMX_REGISTER_URI);
            resp_chunk->write(handler_info_);
            resp_chunk->write(port_);
            resp_chunk->write_string(original_uri_info_, original_uri_info_len_chars_);
            resp_chunk->write_string(processed_uri_info_, processed_uri_info_len_chars_);
            resp_chunk->write(num_params_);
            resp_chunk->write_data_only(param_types_, MixedCodeConstants::MAX_URI_CALLBACK_PARAMS);
            resp_chunk->write((uint8_t)proto_type_);

            return resp_chunk->get_offset();
        }

        // Pushes registered URI handler.
        uint32_t PushRegisteredUriHandler(BmxData* bmx_data);

        // Pushes registered port handler.
        uint32_t PushRegisteredPortHandler(BmxData* bmx_data);

        // Pushes registered subport handler.
        uint32_t PushRegisteredSubportHandler(BmxData* bmx_data);

        // Should be called when whole handlers list should be unregistered.
        uint32_t Unregister()
        {
            type_ = bmx::HANDLER_TYPE::UNUSED_HANDLER;
            handler_info_ = BMX_INVALID_HANDLER_INFO;
            num_entries_ = 0;

            return 0;
        }

        // Checking if slot is empty.
        bool IsEmpty()
        {
            return (bmx::HANDLER_TYPE::UNUSED_HANDLER == type_);
        }

        // Unregistering specific user handler.
        uint32_t Unregister(GENERIC_HANDLER_CALLBACK handler_callback)
        {
            // Comparing all handlers.
            for (uint8_t i = 0; i < num_entries_; ++i)
            {
                // Checking if handler is the same.
                if (handler_callback == handlers_[i])
                {
                    // Checking if it was not the last handler in the array.
                    if (i < (num_entries_ - 1))
                    {
                        // Shifting all forward handlers.
                        for (uint8_t k = i; k < (num_entries_ - 1); ++k)
                            handlers_[k] = handlers_[k + 1];
                    }
                    
                    // Number of entries decreased by one.
                    num_entries_--;

                    break;
                }
            }

            // Checking if it was the last handler.
            if (num_entries_ <= 0)
                return Unregister();

            return SCERRHANDLERNOTFOUND;
        }

        // Runs user handlers.
        uint32_t RunHandlers(
            uint64_t session_id,
            shared_memory_chunk* smc, 
            TASK_INFO_TYPE* task_info)
        {
            uint32_t err_code;
            bool is_handled = false;

            // Going through all registered handlers.
            for (uint8_t i = 0; i < num_entries_; ++i)
            {
                // Running the handler.
                err_code = handlers_[i](session_id, smc, task_info, &is_handled);

                // Checking if information was handled and no errors occurred.
                if (is_handled || err_code)
                    return err_code;
            }

            // All handlers should be called at this point.
            _SC_ASSERT(false);

            return 0;
        }
    };

    // Global BMX data, including handlers, memory, etc.
    class BmxData
    {
        // Current maximum number of handlers.
        int32_t max_num_entries_;

        // All registered handlers.
        HandlersList* registered_handlers_;

        // Current unique number.
        BMX_HANDLER_UNIQUE_NUM_TYPE unique_handler_num_;

    public:

        // Gets specific registered handler.
        HandlersList* GetRegisteredHandler(BMX_HANDLER_TYPE handler_id)
        {
            return registered_handlers_ + handler_id;
        }

        int32_t get_max_num_entries()
        {
            return max_num_entries_;
        }

        // Number of remaining push channels.
        int32_t get_num_remaining_push_channels();

        // Clones current BMX data.
        BmxData* Clone()
        {
            // Note: creating with one more entry.
            BmxData* new_copy = new BmxData(max_num_entries_ + 1);

            new_copy->max_num_entries_ = max_num_entries_;
            new_copy->unique_handler_num_ = unique_handler_num_;

            // Note: for non-linear HandlersList structure, need to copy element by element.
            memcpy(new_copy->registered_handlers_, registered_handlers_, sizeof(registered_handlers_[0]) * max_num_entries_);

            return new_copy;
        }

        // Pushes unregistered handler.
        uint32_t PushHandlerUnregistration(BMX_HANDLER_TYPE handler_info);
        uint32_t HandleSessionDestruction(request_chunk_part* request, TASK_INFO_TYPE* task_info);
        uint32_t HandleErrorFromGateway(request_chunk_part* request, TASK_INFO_TYPE* task_info);

        // Unregisters certain handler.
        uint32_t UnregisterHandler(BMX_HANDLER_INDEX_TYPE handler_index, bool* is_empty_handler);
        uint32_t UnregisterHandler(BMX_HANDLER_INDEX_TYPE handler_index, GENERIC_HANDLER_CALLBACK user_handler, bool* is_empty_handler);
        uint32_t FindUriHandler(
            uint16_t port_num,
            char* processed_uri_info,
            BMX_HANDLER_INDEX_TYPE* handler_index);
        uint32_t FindPortHandler(
            uint16_t port_num,
            BMX_HANDLER_INDEX_TYPE* handler_index);
        uint32_t FindSubportHandler(
            uint16_t port_num,
            BMX_SUBPORT_TYPE subport_num,
            BMX_HANDLER_INDEX_TYPE* handler_index);

        // Registers port handler.
        uint32_t RegisterPortHandler(
            uint16_t port_num,
            GENERIC_HANDLER_CALLBACK port_handler,
            BMX_HANDLER_TYPE* handler_id);

        // Registers sub-port handler.
        uint32_t RegisterSubPortHandler(
            uint16_t port,
            BMX_SUBPORT_TYPE subport,
            GENERIC_HANDLER_CALLBACK subport_handler,
            BMX_HANDLER_TYPE* handler_id);

        // Finds certain handler.
        bool IsHandlerExist(BMX_HANDLER_INDEX_TYPE handler_index);

        // Registers URI handler.
        uint32_t RegisterUriHandler(
            uint16_t port,
            char* original_uri_info,
            char* processed_uri_info,
            uint8_t* param_types,
            int32_t num_params,
            GENERIC_HANDLER_CALLBACK uri_handler, 
            BMX_HANDLER_TYPE* handler_id,
            starcounter::MixedCodeConstants::NetworkProtocolType proto_type);

        // Constructor.
        BmxData(uint32_t max_total_handlers)
        {
            registered_handlers_ = new HandlersList[max_total_handlers];
            max_num_entries_ = 0;
            unique_handler_num_ = 0;
        }

        // Destructor.
        ~BmxData()
        {
            delete [] registered_handlers_;
            registered_handlers_ = NULL;
        }

        // Main message loop for incoming requests. Handles the 
        // dispatching of the message to the correct handler as 
        // well as sending any responses back.
        uint32_t HandleBmxChunk(CM2_TASK_DATA* task_data);
    };

    // Global BMX data.
    extern BmxData* g_bmx_data;

    // Managed callback to destroy Apps session.
    typedef void (*DestroyAppsSessionCallback)(
        uint8_t scheduler_id,
        uint32_t linear_index,
        uint64_t random_salt);

    extern DestroyAppsSessionCallback g_destroy_apps_session_callback;

    // Managed callback to create a new Apps session.
    typedef void (*CreateNewAppsSessionCallback)(
        uint8_t scheduler_id,
        uint32_t* linear_index,
        uint64_t* random_salt,
        uint32_t* reserved);

    extern CreateNewAppsSessionCallback g_create_new_apps_session_callback;

    // Managed callback to handle errors from gateway.
    typedef void (*ErrorHandlingCallback)(
        uint32_t err_code,
        wchar_t* err_string,
        int32_t err_string_len);

    extern ErrorHandlingCallback g_error_handling_callback;

}  // namespace bmx
}; // namespace starcounter

#if 0
// Waits for BMX manager to be ready.
EXTERN_C int32_t __stdcall sc_wait_for_bmx_ready(uint32_t max_time_to_wait_ms);
#endif

// Handles all incoming chunks.
EXTERN_C uint32_t __stdcall sc_handle_incoming_chunks(CM2_TASK_DATA* task_data);

// Construct BMX Ping message.
EXTERN_C uint32_t __stdcall sc_bmx_construct_ping(
    uint64_t ping_data, 
    shared_memory_chunk* smc
    );

// Parse BMX Pong message.
EXTERN_C uint32_t __stdcall sc_bmx_parse_pong(
    shared_memory_chunk* smc,
    uint64_t* pong_data
    );

#endif
