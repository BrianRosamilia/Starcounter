// ***********************************************************************
// <copyright file="AppServer.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using System;
using System.Text;
using Starcounter.Internal.REST;
using Starcounter.Advanced;
using System.Net;
using Codeplex.Data;
using Starcounter.Internal.JsonPatch;
using System.Collections.Generic;
using Starcounter.Rest;

namespace Starcounter.Internal.Web {
    /// <summary>
    /// Wraps the file based http web resource resolver and the App view model resolver.
    /// </summary>
    /// <remarks>Supports Http as well as proprietary protocols.
    /// If the URI does not point to a App view model or a user implemented
    /// handler, the request is routed to a standard file based static resource
    /// web serving implementation that will serve html, png, jpg etc. using the file system.
    /// This file based resolver will be injected into the constructor of this class.</remarks>
    public partial class AppRestServer : IRestServer {

        /// <summary>
        /// A standard file based static resource web serving implementation.
        /// Will serve html, png, jpg etc. using the file system.
        /// If the URI does not point to a App view model or a user implemented
        /// handler, this is where the request will go.
        /// </summary>
        private Dictionary<UInt16, StaticWebServer> StaticFileServers;

        /// <summary>
        /// Initializes a new instance of the <see cref="AppRestServer" /> class.
        /// </summary>
        /// <param name="staticFileServer">The static file server.</param>
        public AppRestServer(Dictionary<UInt16, StaticWebServer> staticFileServer) {
            StaticFileServers = staticFileServer;
        }

        /// <summary>
        /// Handles the response returned from the user handler.
        /// </summary>
        /// <param name="request">Incomming HTTP request.</param>
        /// <param name="response">Result of calling user handler (i.e. the delegate).</param>
        /// <returns>The same object as provide in the response parameter</returns>
        public Response OnResponseHttp(Request request, Response response) {
            try {
                // Checking if we need to resolve static resource.
                if (response.HandlingStatus == HandlerStatusInternal.NotHandled) {
                    response = ResolveAndPrepareFile(request.Uri, request);
                    response.HandlingStatus = HandlerStatusInternal.Done;
                } else {
                    // NOTE: Checking if its internal request then just returning response without modification.
                    if (request.IsInternal)
                        return response;

                    // Checking if JSON object is attached.
                    if (response.Hypermedia is Json) {
                        Json r = (Json)response.Hypermedia;

                        while (r.Parent != null)
                            r = r.Parent;

                        response.Hypermedia = (Json)r;
                    }
                }

                response.Request = request;
                response.ConstructFromFields();

                return response;
            }
            catch (Exception ex) {
				// Logging the exception to server log.
				LogSources.Hosting.LogException(ex);
				var errResp = Response.FromStatusCode(500);
				errResp.Body = GetExceptionString(ex);
				errResp.ContentType = "text/plain";
				errResp.ConstructFromFields();
				return errResp;
            }
        }

        /// <summary>
        /// Handles the response returned from the user handler.
        /// </summary>
        /// <param name="request">Incomming HTTP request.</param>
        /// <param name="response">Result of calling user handler (i.e. the delegate).</param>
        /// <returns>The same object as provide in the response parameter</returns>
        public Response OnResponseWebSockets(Request request, Response response)
        {
            try
            {
                // NOTE: Checking if its internal request then just returning response without modification.
                if (request.IsInternal)
                    return response;

                // Checking if JSON object is attached.
                if (response.Hypermedia is Json)
                {

                    Json r = (Json)response.Hypermedia;

                    while (r.Parent != null)
                        r = r.Parent;

                    response.Hypermedia = (Json)r;
                }

                response.Request = request;
                response.ConstructFromFields();

                return response;
            }
            catch (Exception ex)
            {
                // Logging the exception to server log.
                LogSources.Hosting.LogException(ex);
                var errResp = Response.FromStatusCode(500);
                errResp.Body = GetExceptionString(ex);
                errResp.ContentType = "text/plain";
                errResp.ConstructFromFields();
                return errResp;
            }
        }

        /// <summary>
        /// Handles request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The bytes according to the appropriate protocol</returns>
        public Response HandleRequest(Request request) {
            Response response = null;
            UInt32 errCode;
            Boolean cameWithSession = request.HasSession;

            switch (request.ProtocolType)
            {
                case MixedCodeConstants.NetworkProtocolType.PROTOCOL_HTTP1:
                {
                    try
                    {
                        // Checking if we are in session already.
                        if (!request.IsInternal) {

                            // Setting the original request.
                            Session.InitialRequest = request;

                            // Obtaining session.
                            Session s = (Session) request.GetAppsSessionInterface();

                            if (cameWithSession && (null != s)) {

                                // Starting session.
                                Session.Start(s);

                                // Checking if we can reuse the cache.
                                if (X.CheckLocalCache(request.Uri, request.IsInternal, null, null, out response)) {
                                    // Setting the session again.
                                    response.AppsSession = Session.Current.InternalSession;

                                    // Handling and returning the HTTP response.
                                    response = OnResponseHttp(request, response);

                                    return response;
                                }
                            }
                        }

                        // Invoking original user delegate with parameters here.
                        UserHandlerCodegen.HandlersManager.RunDelegate(request, out response);

                        // In case of returned JSON object within current session we need to save it
                        // for later reuse.
                        
                        Json rootJsonObj = null;
                        if (null != Session.Current)
                            rootJsonObj = Session.Current.Data;

                        Json curJsonObj = null;
                        if (null != response) {

                            // Checking if response is processed later.
                            if (response.HandlingStatus == HandlerStatusInternal.Handled)
                                return response;

                            // Setting session on result only if its original request.
                            if ((null != Session.Current) && (!request.IsInternal) && (!cameWithSession))
                                response.AppsSession = Session.Current.InternalSession;

                            // Converting response to JSON.
                            curJsonObj = response;

                            if ((null != curJsonObj) &&
                                (null != rootJsonObj) &&
                                (request.IsIdempotent()) &&
                                (curJsonObj.HasThisRoot(rootJsonObj)))
                            {
                                Session.Current.AddJsonNodeToCache(request.Uri, curJsonObj);
                            }
                        } else {
                            // Null equals 404.
                            response = Response.FromStatusCode(404);
                            response["Connection"] = "close";
                            response.ConstructFromFields();
                            return response;
                        }

                        // Handling and returning the HTTP response.
                        response = OnResponseHttp(request, response);

                        return response;
                    }
                    catch (ResponseException exc)
                    {
                        // NOTE: if internal request then throw the exception up.
                        if (request.IsInternal)
                            throw exc;

                        response = exc.ResponseObject;
                        response.ConnFlags = Response.ConnectionFlags.DisconnectAfterSend;
                        response.ConstructFromFields();
                        return response;
                    }
                    catch (HandlersManagement.IncorrectSessionException)
                    {
                        response = Response.FromStatusCode(400);
                        response["Connection"] = "close";
                        response.ConstructFromFields();
                        return response;
                    }
                    catch (Exception exc)
                    {
						// Logging the exception to server log.
						LogSources.Hosting.LogException(exc);
						response = Response.FromStatusCode(500);
						response.Body = GetExceptionString(exc);
						response.ContentType = "text/plain";
						response.ConstructFromFields();
						return response;
                    }
                    finally
                    {
                        // Checking if a new session was created during handler call.
                        if ((null != Session.Current) && (!request.IsInternal))
                            Session.End();
                    }
                }

                case MixedCodeConstants.NetworkProtocolType.PROTOCOL_WEBSOCKETS:
                {
                    try
                    {
                        // Setting the original request.
                        Session.InitialRequest = request;

                        // Checking if we are in session already.
                        if (!cameWithSession)
                        {
                            // Creating new current session.
                            Session.Current = new Session();

                            // Creating session on Request as well.
                            errCode = request.GenerateNewSession(Session.Current);
                            if (errCode != 0)
                                throw ErrorCode.ToException(errCode);
                        }
                        else
                        {
                            // Start using specific session.
                            Session.Start((Session)request.GetAppsSessionInterface());
                        }

                        // Updating session information (sockets info, WebSockets, etc).
                        request.UpdateSessionDetails();

                        // Invoking original user delegate with parameters here.
                        UserHandlerCodegen.HandlersManager.RunDelegate(request, out response);

                        // Handling result.
                        if (null == response)
                        {
                            // Simply disconnecting if response is null.
                            response = new Response()
                            {
                                ConnFlags = Response.ConnectionFlags.DisconnectImmediately,
                                HandlingStatus = HandlerStatusInternal.Done
                            };
                        }
                        else
                        {
                            // Checking if WebSockets response status is unhandled.
                            if (response.HandlingStatus == HandlerStatusInternal.NotHandled)
                            {
                                response.ConnFlags = Response.ConnectionFlags.DisconnectImmediately;
                                response.HandlingStatus = HandlerStatusInternal.Done;
                            }
                        }

                        response.ProtocolType = MixedCodeConstants.NetworkProtocolType.PROTOCOL_WEBSOCKETS;

                        // Checking if response is processed later.
                        if (response.HandlingStatus == HandlerStatusInternal.Done)
                            response = OnResponseWebSockets(request, response);

                        return response;
                    }
                    catch (ResponseException exc)
                    {
                        response = exc.ResponseObject;
                        response.ConnFlags = Response.ConnectionFlags.DisconnectAfterSend;
                        response.ConstructFromFields();
                        return response;
                    }
                    finally
                    {
                        Session.End();
                    }
                }

                default:
                {
                    throw ErrorCode.ToException(Error.SCERRUNKNOWNNETWORKPROTOCOL);
                }
            }
        }

        /// <summary>
        /// This is where the AppServer calls to get a resource from the file system.
        /// </summary>
        /// <param name="relativeUri">The uri to resolve</param>
        /// <param name="request">The http request</param>
        /// <returns>The http response</returns>
        private Response ResolveAndPrepareFile(string relativeUri, Request request) {
            StaticWebServer staticWebServer;

            // Trying to fetch resource for this port.
            if (StaticFileServers.TryGetValue(request.PortNumber, out staticWebServer))
            {
                return staticWebServer.GetStatic(relativeUri, request);
            }

			var badReq = Response.FromStatusCode(400);
			badReq["Connection"] = "close";
		    return badReq;
        }

        /// <summary>
        /// Gets the exception string.
        /// </summary>
        /// <param name="ex">The ex.</param>
        /// <returns>String.</returns>
        private String GetExceptionString(Exception ex) {
            Exception inner;
            StringBuilder sb = new StringBuilder();

            sb.AppendLine(ex.Message);
            sb.AppendLine(ex.StackTrace);

            inner = ex.InnerException;
            while (inner != null) {
                sb.Append("-->");
                sb.AppendLine(inner.Message);
                sb.AppendLine(inner.StackTrace);
                inner = inner.InnerException;
            }
            return sb.ToString();
        }

        /// <summary>
        /// Sent from the Node when the user runs a module (an .EXE).
        /// </summary>
        /// <param name="path">The path.</param>
        /// <remarks>There is no need to add the directory to the static resolver as the static resolver
        /// will already be bootstrapped as a lower priority handler for stuff that this
        /// AppServer does not handle.</remarks>
        public void UserAddedLocalFileDirectoryWithStaticContent(UInt16 port, String path)
        {
            lock (StaticFileServers)
            {
                StaticWebServer staticWebServer;

                // Try to fetch static web server.
                if (StaticFileServers.TryGetValue(port, out staticWebServer))
                {
                    staticWebServer.UserAddedLocalFileDirectoryWithStaticContent(port, path);
                }
                else
                {
                    staticWebServer = new StaticWebServer();
                    StaticFileServers.Add(port, staticWebServer);
                    staticWebServer.UserAddedLocalFileDirectoryWithStaticContent(port, path);
                }
            }
        }

        /// <summary>
        /// Housekeeps this instance.
        /// </summary>
        /// <returns>System.Int32.</returns>
        public int Housekeep()
        {
            lock (StaticFileServers)
            {
                // Doing house keeping for each port.
                foreach (KeyValuePair<UInt16, StaticWebServer> s in StaticFileServers)
                    s.Value.Housekeep();

                return 0;
            }
        }
    }

}
