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

// Running all registered handlers.
uint32_t PortHandlers::RunHandlers(GatewayWorker *gw, SocketDataChunkRef sd, bool* is_handled)
{
    uint32_t err_code;

    // Going through all handler list.
    for (int32_t i = 0; i < handler_lists_.get_num_entries(); ++i)
    {
        err_code = handler_lists_[i]->RunHandlers(gw, sd, is_handled);

        // Checking if information was handled and no errors occurred.
        if (*is_handled || err_code)
            return err_code;
    }

    return SCERRGWPORTNOTHANDLED;
}

// Should be called when whole handlers list should be unregistered.
uint32_t HandlersList::UnregisterGlobally(db_index_type db_index)
{
    // Checking the type of handler.
    switch(type_)
    {
        case bmx::HANDLER_TYPE::PORT_HANDLER:
        {
            break;
        }

        case bmx::HANDLER_TYPE::SUBPORT_HANDLER:
        {
            // Unregister globally.

            break;
        }

        case bmx::HANDLER_TYPE::URI_HANDLER:
        {
            // Unregister globally.
            RegisteredUris* port_uris = g_gateway.FindServerPort(port_)->get_registered_uris();
            port_uris->RemoveEntry(db_index, processed_uri_info_);

            // Collecting empty ports.
            g_gateway.CleanUpEmptyPorts();

            break;
        }

        default:
        {
            return SCERRGWWRONGHANDLERTYPE;
        }
    }

    return 0;
}

uint32_t HandlersTable::RegisterPortHandler(
    GatewayWorker *gw,
    uint16_t port_num,
    BMX_HANDLER_TYPE handler_info,
    GENERIC_HANDLER_CALLBACK port_handler,
    db_index_type db_index,
    BMX_HANDLER_INDEX_TYPE& out_handler_index)
{
    // Checking number of handlers.
    if (max_num_entries_ >= bmx::MAX_TOTAL_NUMBER_OF_HANDLERS)
        return SCERRGWMAXHANDLERSREACHED;

    uint32_t err_code = 0;

    BMX_HANDLER_INDEX_TYPE i;
    out_handler_index = max_num_entries_;

    // Running throw existing handlers.
    for (i = 0; i < max_num_entries_; i++)
    {
        // Checking if empty slot.
        if (registered_handlers_[i].IsEmpty())
        {
            out_handler_index = i;
        }
        // Checking current handler type to be port handler.
        else if (bmx::PORT_HANDLER == registered_handlers_[i].get_type())
        {
            // Checking if port is the same.
            if (port_num == registered_handlers_[i].get_port())
            {
                GW_COUT << "Attempt to register handler duplicate: port " << port_num << GW_ENDL;

                // Disallowing handler duplicates.
                return SCERRHANDLERALREADYREGISTERED;

                /*
                // Checking same handler id.
                if (GetBmxHandlerIndex(handler_info) == registered_handlers_[i].get_handler_index())
                {
                    // Search if handler is already in the list.
                    if (!registered_handlers_[i].HandlerAlreadyExists(port_handler))
                    {
                        // Adding new handler to list.
                        err_code = registered_handlers_[i].AddHandler(port_handler);
                        GW_ERR_CHECK(err_code);
                    }

                    // Same handler already exists, checking server port.
                    goto PROCESS_SERVER_PORT;
                }
                else
                {
                    return SCERRGWWRONGHANDLERINSLOT;
                }
                */
            }
        }
    }

    // Initializing new handlers list.
    err_code = registered_handlers_[out_handler_index].Init(
        bmx::HANDLER_TYPE::PORT_HANDLER,
        handler_info,
        port_num,
        0,
        NULL,
        0,
        NULL,
        0,
        NULL,
        0,
        db_index,
        0,
        NULL);

    if (err_code)
        goto ERROR_HANDLING;

    // Adding handler to the list.
    err_code = registered_handlers_[out_handler_index].AddHandler(port_handler);
    if (err_code)
        goto ERROR_HANDLING;

    // New handler added.
    if (out_handler_index == max_num_entries_)
        max_num_entries_++;

    // Checking if there is a corresponding server port created.
    ServerPort* server_port = g_gateway.FindServerPort(port_num);

    // Checking if port exists.
    if (!server_port)
    {
        SOCKET listening_sock = INVALID_SOCKET;

        // Creating socket and binding to port for all workers.
        err_code = g_gateway.CreateListeningSocketAndBindToPort(gw, port_num, listening_sock);
        if (err_code)
            goto ERROR_HANDLING;

        // Adding new server port.
        server_port = g_gateway.AddServerPort(port_num, listening_sock);

        // Checking if its an aggregation port.
        if (port_num == g_gateway.setting_aggregation_port())
            server_port->set_aggregating_flag();
    }

    // Checking if port contains handlers from this database.
    if (INVALID_INDEX == server_port->get_port_handlers()->GetEntryIndex(db_index))
    {
        // Determining how many connections to create.
        int32_t how_many = ACCEPT_ROOF_STEP_SIZE;

#ifdef GW_TESTING_MODE

        // On the test client we immediately creating all needed connections.
        if (!g_gateway.setting_is_master())
            how_many = g_gateway.setting_num_connections_to_master_per_worker();

#endif

        // Creating new connections if needed for this database.
        // NOTE: Creating connections only on database 0.
        err_code = g_gateway.CreateNewConnectionsAllWorkers(how_many, port_num, 0);
        if (err_code)
            goto ERROR_HANDLING;
    }

    // Adding port handler if does not exist.
    server_port->get_port_handlers()->Add(db_index, registered_handlers_ + out_handler_index);

    return 0;

    // Handling error.
ERROR_HANDLING:

    registered_handlers_[out_handler_index].Unregister();
    return err_code;
}

// Registers sub-port handler.
uint32_t HandlersTable::RegisterSubPortHandler(
    GatewayWorker *gw,
    uint16_t port_num,
    bmx::BMX_SUBPORT_TYPE subport,
    BMX_HANDLER_TYPE handler_info,
    GENERIC_HANDLER_CALLBACK subport_handler,
    db_index_type db_index,
    BMX_HANDLER_INDEX_TYPE& out_handler_index)
{
    // Checking number of handlers.
    if (max_num_entries_ >= bmx::MAX_TOTAL_NUMBER_OF_HANDLERS)
        return SCERRGWMAXHANDLERSREACHED;

    uint32_t err_code = 0;

    BMX_HANDLER_INDEX_TYPE i;
    out_handler_index = max_num_entries_;

    // Running throw existing handlers.
    for (i = 0; i < max_num_entries_; i++)
    {
        // Checking if empty slot.
        if (registered_handlers_[i].IsEmpty())
        {
            out_handler_index = i;
        }
        // Checking current handler type to be port handler.
        else if (bmx::HANDLER_TYPE::SUBPORT_HANDLER == registered_handlers_[i].get_type())
        {
            // Checking if port is the same.
            if (port_num == registered_handlers_[i].get_port())
            {
                if (subport == registered_handlers_[i].get_subport())
                {
                    GW_COUT << "Attempt to register handler duplicate: port " << port_num << ", sub-port " << subport << GW_ENDL;

                    // Disallowing handler duplicates.
                    return SCERRHANDLERALREADYREGISTERED;

                    /*
                    // Checking same handler id.
                    if (GetBmxHandlerIndex(handler_info) == registered_handlers_[i].get_handler_index())
                    {
                        // Search if handler is already in the list.
                        if (!registered_handlers_[i].HandlerAlreadyExists(subport_handler))
                        {
                            // Adding new handler to list.
                            err_code = registered_handlers_[i].AddHandler(subport_handler);
                            GW_ERR_CHECK(err_code);
                        }

                        // Same handler already exists, checking server port.
                        goto PROCESS_SERVER_PORT;
                    }
                    else
                    {
                        return SCERRGWWRONGHANDLERINSLOT;
                    }
                    */
                }
            }
        }
    }

    // Initializing new handlers list.
    err_code = registered_handlers_[out_handler_index].Init(
        bmx::HANDLER_TYPE::SUBPORT_HANDLER,
        handler_info,
        port_num,
        0,
        NULL,
        0,
        NULL,
        0,
        NULL,
        0,
        db_index,
        0,
        NULL);

    if (err_code)
        goto ERROR_HANDLING;

    // Adding handler to the list.
    err_code = registered_handlers_[out_handler_index].AddHandler(subport_handler);
    if (err_code)
        goto ERROR_HANDLING;

    // New handler added.
    if (out_handler_index == max_num_entries_)
        max_num_entries_++;

    // Checking if there is a corresponding server port created.
    ServerPort* server_port = g_gateway.FindServerPort(port_num);

    // Checking if port exists.
    if (!server_port)
    {
        /*
        SOCKET listening_sock = INVALID_SOCKET;

        // Creating socket and binding to port for all workers.
        err_code = g_gateway.CreateListeningSocketAndBindToPort(gw, port_num, listening_sock);
        if (err_code)
            goto ERROR_HANDLING;

        // Adding new server port.
        server_port = g_gateway.AddServerPort(port_num, listening_sock, SUBPORT_BLOB_USER_DATA_OFFSET);

        // Adding new port outer handler.
        BMX_HANDLER_INDEX_TYPE new_handler_index;
        err_code = RegisterPortHandler(gw, port_num, 0, OuterSubportProcessData, db_index, new_handler_index);
        */

        // Registering handler on active database.
        err_code = g_gateway.AddPortHandler(
            gw,
            g_gateway.get_gw_handlers(),
            port_num,
            handler_info,
            0,
            OuterSubportProcessData);

        if (err_code)
            goto ERROR_HANDLING;
    }

    // NOTE: Registering a port handler for each database since each database
    // when deleted, deletes its own port.

    // Checking if port already contains handlers from this database.
    /*if (INVALID_INDEX == server_port->get_port_handlers()->GetEntryIndex(db_index))
    {
        // Adding new port outer handler.
        BMX_HANDLER_INDEX_TYPE new_handler_index;
        err_code = RegisterPortHandler(gw, port_num, 0, OuterSubportProcessData, db_index, new_handler_index);
        if (err_code)
            goto ERROR_HANDLING;
    }*/

    return 0;

    // Handling error.
ERROR_HANDLING:

    registered_handlers_[out_handler_index].Unregister();
    return err_code;
}

// Registers URI handler.
uint32_t HandlersTable::RegisterUriHandler(
    GatewayWorker *gw,
    uint16_t port_num,
    const char* original_uri_string,
    uint32_t original_uri_str_len,
    const char* processed_uri_string,
    uint32_t processed_uri_str_len,
    uint8_t* param_types,
    int32_t num_params,
    BMX_HANDLER_TYPE handler_info,
    GENERIC_HANDLER_CALLBACK uri_handler,
    db_index_type db_index,
    BMX_HANDLER_INDEX_TYPE& out_handler_index,
    ReverseProxyInfo* reverse_proxy_info)
{
    // Checking number of handlers.
    if (max_num_entries_ >= bmx::MAX_TOTAL_NUMBER_OF_HANDLERS)
        return SCERRGWMAXHANDLERSREACHED;

    uint32_t err_code = 0;

    BMX_HANDLER_INDEX_TYPE i;
    out_handler_index = max_num_entries_;

    // Running throw existing handlers.
    for (i = 0; i < max_num_entries_; i++)
    {
        // Checking if empty slot.
        if (registered_handlers_[i].IsEmpty())
        {
            out_handler_index = i;
        }
        // Checking current handler type to be port handler.
        else if (bmx::URI_HANDLER == registered_handlers_[i].get_type())
        {
            // Checking if port is the same.
            if (port_num == registered_handlers_[i].get_port())
            {
                // Checking the same URI.
                if (!strcmp(processed_uri_string, registered_handlers_[i].get_processed_uri_info()))
                {
                    GW_COUT << "Attempt to register handler duplicate: port " << port_num << ", URI " << processed_uri_string << GW_ENDL;

                    // Disallowing handler duplicates.
                    return SCERRHANDLERALREADYREGISTERED;

                    /*
                    // Checking same handler id.
                    if (GetBmxHandlerIndex(handler_info) == registered_handlers_[i].get_handler_index())
                    {
                        // Search if handler is already in the list.
                        if (!registered_handlers_[i].HandlerAlreadyExists(uri_handler))
                        {
                            // Adding new handler to list.
                            err_code = registered_handlers_[i].AddHandler(uri_handler);
                            GW_ERR_CHECK(err_code);
                        }

                        // Same handler already exists, checking server port.
                        goto PROCESS_SERVER_PORT;
                    }
                    else
                    {
                        return SCERRGWWRONGHANDLERINSLOT;
                    }
                    */
                }
            }
        }
    }

    // Initializing new handlers list.
    err_code = registered_handlers_[out_handler_index].Init(
        bmx::HANDLER_TYPE::URI_HANDLER,
        handler_info,
        port_num,
        0,
        original_uri_string,
        original_uri_str_len,
        processed_uri_string,
        processed_uri_str_len,
        param_types,
        num_params,
        db_index,
        0,
        reverse_proxy_info);

    if (err_code)
        goto ERROR_HANDLING;

    // Adding handler to the list.
    err_code = registered_handlers_[out_handler_index].AddHandler(uri_handler);
    if (err_code)
        goto ERROR_HANDLING;

    // New handler added.
    if (out_handler_index == max_num_entries_)
        max_num_entries_++;

    // Checking if there is a corresponding server port created.
    ServerPort* server_port = g_gateway.FindServerPort(port_num);

    // Checking if port exists.
    if (!server_port)
    {
        /*
        SOCKET listening_sock = INVALID_SOCKET;

        // Creating socket and binding to port for all workers.
        err_code = g_gateway.CreateListeningSocketAndBindToPort(gw, port_num, listening_sock);
        if (err_code)
            goto ERROR_HANDLING;

        // Adding new server port.
        server_port = g_gateway.AddServerPort(port_num, listening_sock, HTTP_BLOB_USER_DATA_OFFSET);

        // Adding new port outer handler.
        BMX_HANDLER_INDEX_TYPE new_handler_index;
        err_code = RegisterPortHandler(gw, port_num, 0, OuterUriProcessData, db_index, new_handler_index);
        */

        // Registering handler on active database.
        err_code = g_gateway.AddPortHandler(
            gw,
            g_gateway.get_gw_handlers(),
            port_num,
            handler_info,
            0,
            OuterUriProcessData);

        if (err_code)
            goto ERROR_HANDLING;
    }

    // NOTE: Registering a port handler for each database since each database
    // when deleted, deletes its own port.

    // Checking if port already contains handlers from this database.
    /*if (INVALID_INDEX == server_port->get_port_handlers()->GetEntryIndex(db_index))
    {
        // Adding new port outer handler.
        BMX_HANDLER_INDEX_TYPE new_handler_index;
        err_code = RegisterPortHandler(gw, port_num, 0, OuterUriProcessData, db_index, new_handler_index);
        if (err_code)
            goto ERROR_HANDLING;
    }*/

    return 0;

    // Handling error.
ERROR_HANDLING:

    registered_handlers_[out_handler_index].Unregister();
    return err_code;
}

// Finds certain handler.
HandlersList* HandlersTable::FindHandler(BMX_HANDLER_TYPE handler_info)
{
    // Checking all registered handlers.
    for (BMX_HANDLER_TYPE i = 0; i < max_num_entries_; i++)
    {
        // Checking if the same handler.
        if (GetBmxHandlerIndex(handler_info) == registered_handlers_[i].get_handler_index())
            return registered_handlers_ + i;
    }

    return NULL;
}

// Unregisters certain handler.
uint32_t HandlersTable::UnregisterHandler(BMX_HANDLER_TYPE handler_info, GENERIC_HANDLER_CALLBACK handler_callback)
{
    // Checking all registered handlers.
    uint32_t err_code = SCERRUNSPECIFIED;
    for (BMX_HANDLER_TYPE i = 0; i < max_num_entries_; i++)
    {
        // Checking if the same handler.
        if (GetBmxHandlerIndex(handler_info) == registered_handlers_[i].get_handler_index())
        {
            // Unregistering certain handler.
            if (!handler_callback)
                err_code = registered_handlers_[i].Unregister();
            else
                err_code = registered_handlers_[i].Unregister(handler_callback);

            return err_code;
        }
    }

    // If not removed.
    return SCERRGWHANDLERNOTFOUND;
}

// Unregisters certain handler.
uint32_t HandlersTable::UnregisterHandler(BMX_HANDLER_TYPE handler_id)
{
    return UnregisterHandler(handler_id, NULL);
}

// Outer port handler.
uint32_t OuterPortProcessData(
    HandlersList* hl,
    GatewayWorker *gw,
    SocketDataChunkRef sd,
    BMX_HANDLER_TYPE handler_index,
    bool* is_handled)
{
    // First searching in database handlers table.
    HandlersTable* handlers_table = g_gateway.GetDatabase(sd->get_db_index())->get_user_handlers();

    // Getting the corresponding port number.
    uint16_t port_num = g_gateway.get_server_port(sd->GetPortIndex())->get_port_number();

    // Searching for the user code handler id.
    handler_index = g_gateway.get_gw_handlers()->FindPortHandlerIndex(port_num);

    // Checking if handler was not found in database handlers.
    if (bmx::BMX_INVALID_HANDLER_INDEX == handler_index)
    {
        // Switching to gateway handlers.
        handlers_table = g_gateway.get_gw_handlers();

        // Searching for the user code handler id.
        handler_index = handlers_table->FindPortHandlerIndex(port_num);
    }

    // Making sure that handler index is obtained.
    GW_ASSERT(bmx::BMX_INVALID_HANDLER_INDEX != handler_index);

    // Now running specific handler.
    return handlers_table->get_handler_list(handler_index)->RunHandlers(gw, sd, is_handled);
}

// General sockets handler.
uint32_t AppsPortProcessData(
    HandlersList* hl,
    GatewayWorker *gw,
    SocketDataChunkRef sd,
    BMX_HANDLER_TYPE user_handler_id,
    bool* is_handled)
{
    uint32_t err_code;

    // Checking if data goes to user code.
    if (sd->get_to_database_direction_flag())
    {
        // Resetting user data parameters.
        sd->set_user_data_written_bytes(sd->get_accum_buf()->get_accum_len_bytes());
        sd->ResetUserDataOffset();

        // Setting matched URI index.
        sd->SetDestDbIndex(hl->get_db_index());

        // Push chunk to corresponding channel/scheduler.
        err_code = gw->PushSocketDataToDb(sd, user_handler_id);
        if (err_code)
            return err_code;

        // Setting handled flag.
        *is_handled = true;

        return 0;
    }
    // Checking if data goes from user code.
    else
    {
        // Prepare buffer to send outside.
        sd->get_accum_buf()->PrepareForSend(sd->UserDataBuffer(), sd->get_user_data_written_bytes());

        // Sending data.
        err_code = gw->Send(sd);
        GW_ERR_CHECK(err_code);

        // Setting handled flag.
        *is_handled = true;

        return 0;
    }

    return SCERRGWPORTPROCESSFAILED;
}

#ifdef GW_TESTING_MODE

// Port echo handler.
uint32_t GatewayPortProcessEcho(
    HandlersList* hl,
    GatewayWorker *gw,
    SocketDataChunkRef sd,
    BMX_HANDLER_TYPE user_handler_id,
    bool* is_handled)
{
    uint32_t err_code;

    // Setting handled flag.
    *is_handled = true;

    if (g_gateway.setting_is_master())
    {
        AccumBuffer* accum_buffer = sd->get_accum_buf();

        GW_ASSERT(accum_buffer->get_accum_len_bytes() == 8);

        // Copying echo message.
        int64_t orig_echo = *(int64_t*)accum_buffer->get_chunk_orig_buf_ptr();

        // Duplicating this echo.
        *(int64_t*)(accum_buffer->get_chunk_orig_buf_ptr() + 8) = orig_echo;

        // Prepare buffer to send outside.
        accum_buffer->PrepareForSend(accum_buffer->get_chunk_orig_buf_ptr(), 16);

        // Sending data.
        err_code = gw->Send(sd);
        GW_ERR_CHECK(err_code);
    }
    else
    {
        // Asserting correct number of bytes received.
        GW_ASSERT(sd->get_accum_buf()->get_accum_len_bytes() == 16);

        // Obtaining original echo number.
        echo_id_type echo_id = *(int32_t*)(sd->get_data_blob() + 8);

#ifdef GW_ECHO_STATISTICS
        GW_COUT << "Received echo: " << echo_id << GW_ENDL;
#endif

#ifdef GW_LIMITED_ECHO_TEST
        // Confirming received echo.
        g_gateway.ConfirmEcho(echo_id);
#endif

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
            goto SEND_RAW_ECHO_TO_MASTER;
        }

SEND_RAW_ECHO_TO_MASTER:

        // Checking that not all echoes are sent.
        if (!g_gateway.AllEchoesSent())
        {
            // Generating echo number.
            echo_id_type new_echo_num = 0;

#ifdef GW_LIMITED_ECHO_TEST
            new_echo_num = g_gateway.GetNextEchoNumber();
#endif

            // Sending echo request to server.
            err_code = gw->SendRawEcho(sd, new_echo_num);
            if (err_code)
                return err_code;
        }
    }

    return 0;
}

#endif

// Outer port handler.
uint32_t OuterSubportProcessData(
    HandlersList* hl,
    GatewayWorker *gw,
    SocketDataChunkRef sd,
    BMX_HANDLER_TYPE handler_index,
    bool* is_handled)
{
    return OuterPortProcessData(hl, gw, sd, handler_index, is_handled);
}

// Subport handler.
uint32_t AppsSubportProcessData(
    HandlersList* hl,
    GatewayWorker *gw,
    SocketDataChunkRef sd,
    BMX_HANDLER_TYPE user_handler_id,
    bool* is_handled)
{
    return AppsPortProcessData(hl, gw, sd, user_handler_id, is_handled);
}

#ifdef GW_TESTING_MODE

// Subport echo handler.
uint32_t GatewaySubportProcessEcho(
    HandlersList* hl,
    GatewayWorker *gw,
    SocketDataChunkRef sd,
    BMX_HANDLER_TYPE user_handler_id,
    bool* is_handled)
{
    return GatewayPortProcessEcho(hl, gw, sd, user_handler_id, is_handled);
}

#endif

} // namespace network
} // namespace starcounter
