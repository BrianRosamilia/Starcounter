using HttpStructs;
using Starcounter;
using Starcounter.Internal.Application;
using Starcounter.Internal.JsonPatch;
using Starcounter.Internal.Web;
using System;
using System.IO;
using System.Collections.Generic;

partial class TestApp : App {
    static void Main(String[] args) {

        BootstrapApps();
        // TODO! Per. Please allow to specify directory as a command line parameter to your app
        var path = Path.GetDirectoryName(typeof(TestApp).Assembly.Location) + "\\..\\..";
        AddFileServingDirectory( path );

        GET("/", () => { return new TestApp() { View = "TestApp.html" }; });

    }



    private static HttpAppServer _appServer;
    private static StaticWebServer fileServer;

    /// <summary>
    /// Adds a directory to the list of directories used by the web server to
    /// resolve GET requests for static content.
    /// </summary>
    /// <param name="path">The directory to include</param>
    public static void AddFileServingDirectory( string path ) {
        fileServer.UserAddedLocalFileDirectoryWithStaticContent(path);
    }

    /// <summary>
    /// Function that registers a default handler in the gateway and handles incoming requests
    /// and dispatch them to Apps. Also registers internal handlers for jsonpatch.
    /// 
    /// All this should be done internally in Starcounter.
    /// </summary>
    internal static void BootstrapApps() {
        fileServer = new StaticWebServer();
    
        _appServer = new HttpAppServer(fileServer, new SessionDictionary());

        // Register the handlers required by the Apps system. These work as user code handlers, but
        // listens to the built in REST api serving view models to REST clients.
        InternalHandlers.Register();

        // Let the Network Gateway now when the user adds a handler (like GET("/")).
        App.UriMatcherBuilder.RegistrationListeners.Add((string verbAndUri) => {
            UInt16 handlerId;

            // TODO! Alexey. Please allow to register to Gateway with only port (i.e without Verb and URI)
            GatewayHandlers.RegisterUriHandler(8080, "GET /", OnHttpMessageRoot, out handlerId);
            // TODO! Alexey. Please allow to register to Gateway with only port (i.e without Verb and URI)
            GatewayHandlers.RegisterUriHandler(8080, "PATCH /", OnHttpMessageRoot, out handlerId);
        });
    }

    /// <summary>
    /// Entrypoint for all incoming http requests from the Network Gateway.
    /// </summary>
    /// <param name="request">The http request</param>
    /// <returns>Returns true if the request was handled</returns>
    private static Boolean OnHttpMessageRoot(HttpRequest request) {
        HttpResponse result = _appServer.Handle(request);
        request.WriteResponse(result.Uncompressed, 0, result.Uncompressed.Length);
        return true; // TODO! Return false if request was not handled.
    }
}