﻿
using HttpStructs;
using Starcounter.Advanced;
using Starcounter.Apps.Bootstrap;
using Starcounter.Internal.JsonPatch;
using Starcounter.Internal.Web;
using System;
using System.IO;
namespace Starcounter.Internal {

    /// <summary>
    /// Sets up the REST, Hypermedia and Apps modules
    /// </summary>
    /// <remarks>
    /// A common dependency injection pattern for all bootstrapping in Starcounter should be
    /// considered for a future version. This includes Apps, SQL and other modules.
    /// </remarks>
    public static class AppsBootstrapper {
        private static HttpAppServer appServer;
        // private static StaticWebServer fileServer;
        private static UInt16 defaultUserHttpPort_ = StarcounterConstants.NetworkPorts.DefaultPersonalServerUserHttpPort;
        private static Boolean isAdministratorApp = false;

        /// <summary>
        /// Initializes AppsBootstrapper.
        /// </summary>
        /// <param name="defaultPort"></param>
        public static void InitAppsBootstrapper(UInt16 defaultUserHttpPort, String dbName)
        {
            defaultUserHttpPort_ = defaultUserHttpPort;
            isAdministratorApp = (0 == String.Compare(dbName, MixedCodeConstants.AdministratorAppName, true));
            Node.ThisStarcounterNode = new Node("127.0.0.1", 8181);
        }
        
        /// <summary>
        /// 
        /// </summary>
        static AppsBootstrapper() {
            var fileServer = new StaticWebServer();
            appServer = new HttpAppServer(fileServer);
            StarcounterBase.Fileserver = fileServer;

            // Checking if we are inside the database worker process.
            AppProcess.AssertInDatabaseOrSendStartRequest();
        }

        /// <summary>
        /// Adds a directory to the list of directories used by the web server to
        /// resolve GET requests for static content.
        /// </summary>
        /// <param name="path">The directory to include</param>
        public static void AddFileServingDirectory(string path) {
            StarcounterBase.Fileserver.UserAddedLocalFileDirectoryWithStaticContent(path);
        }

        /// <summary>
        /// Function that registers a default handler in the gateway and handles incoming requests
        /// and dispatch them to Apps. Also registers internal handlers for jsonpatch.
        /// </summary>
        /// <param name="port">Listens for http traffic on the given port. </param>
        /// <param name="resourceResolvePath">Adds a directory path to the list of paths used when resolving a request for a static REST (web) resource</param>
        public static void Bootstrap(
            UInt16 port = StarcounterConstants.NetworkPorts.DefaultUnspecifiedPort,
            string resourceResolvePath = null)
        {
            // Checking for the port.
            if (port == StarcounterConstants.NetworkPorts.DefaultUnspecifiedPort)
                port = defaultUserHttpPort_;

            if (resourceResolvePath != null)
            {
                AddFileServingDirectory(resourceResolvePath);

                // Registering static content directory with Administrator.

                // Getting bytes from string.
                //byte[] staticContentDirBytes = System.Text.ASCIIEncoding.ASCII.GetBytes(resourceResolvePath);

                // Converting path string to base64 string.
                //string staticContentDirBase64 = System.Convert.ToBase64String(staticContentDirBytes);

                // Checking if this is not administrator.
                if (!isAdministratorApp)
                {
                    // Putting port and full path to resources directory.
                    String content = port + StarcounterConstants.NetworkConstants.CRLF + Path.GetFullPath(resourceResolvePath);

                    // Sending REST POST request to Administrator to register static resources directory.
                    Node.ThisStarcounterNode.POST("/addstaticcontentdir", content, null, (HttpResponse resp) =>
                    {
                        String respString = resp.GetContentStringUtf8_Slow();

                        if ("Success!" != respString)
                            throw new Exception("Could not register static resources directory with administrator!");

                        return "Success!";
                    });
                }
            }

            // Setting the response handler.
            Node.SetHandleResponse(appServer.HandleResponse);

            // Giving REST needed delegates.
            UserHandlerCodegen.Setup(
                port,
                GatewayHandlers.RegisterUriHandler,
                OnHttpMessageRoot);
            
            // Register the handlers required by the Apps system. These work as user code handlers, but
            // listens to the built in REST API serving view models to REST clients.
            InternalHandlers.Register();
        }

        /// <summary>
        /// Entry-point for all incoming http requests from the Network Gateway.
        /// </summary>
        /// <param name="request">The http request</param>
        /// <returns>Returns true if the request was handled</returns>
        private static Boolean OnHttpMessageRoot(HttpRequest request) {
            var result = (HttpResponse)appServer.Handle(request);
            request.SendResponse(result.Uncompressed, 0, result.Uncompressed.Length);
            return true;
        }
    }
}
