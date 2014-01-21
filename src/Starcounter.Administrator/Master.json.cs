
using Codeplex.Data;
using Starcounter;
using Starcounter.Administrator;
using Starcounter.Administrator.API;
using Starcounter.Administrator.API.Handlers;
using Starcounter.Advanced;
using Starcounter.Internal;
using Starcounter.Internal.JsonPatch;
using Starcounter.Internal.REST;
using Starcounter.Server;
using Starcounter.Server.PublicModel;
using Starcounter.Server.PublicModel.Commands;
using Starcounter.Server.Rest;
using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Web;

// http://msdn.microsoft.com/en-us/library/system.runtime.compilerservices.internalsvisibletoattribute.aspx

namespace Starcounter.Administrator {

    partial class Master : Json {

        public static IServerRuntime ServerInterface;
        public static ServerEngine ServerEngine;

        // Argument <path to server configuraton file> <portnumber>
        static void Main(string[] args) {

            if (args == null || args.Length < 1) {
                Console.WriteLine("Starcounter Administrator: Invalid arguments: Usage <path to server configuraton file>");
                return;
            }

            // Server configuration file.
            if (string.IsNullOrEmpty(args[0]) || !File.Exists(args[0])) {
                Console.WriteLine("Starcounter Administrator: Missing server configuration file {0}", args[0]);
            }

            // Administrator port.
            UInt16 adminPort = StarcounterEnvironment.Default.SystemHttpPort;
            Console.WriteLine("Starcounter Administrator started on port: " + adminPort);

#if ANDWAH
            AppsBootstrapper.Bootstrap(@"c:\github\Level1\src\Starcounter.Administrator", adminPort);   // TODO:REMOVE
#else
            AppsBootstrapper.Bootstrap("scadmin", adminPort);
#endif

            Master.ServerEngine = new ServerEngine(args[0]);      // .srv\Personal\Personal.server.config
            Master.ServerEngine.Setup();
            Master.ServerInterface = Master.ServerEngine.Start();

            // Start listening on log-events
            ServerInfo serverInfo = Master.ServerInterface.GetServerInfo();

            LogApp.Setup(serverInfo.Configuration.LogDirectory);

            // Register and setup the API subsystem handlers
            var admin = new AdminAPI();
            RestAPI.Bootstrap(admin, Dns.GetHostEntry(String.Empty).HostName, adminPort, Master.ServerEngine, Master.ServerInterface);

            FrontEndAPI.FrontEndAPI.Bootstrap(adminPort, Master.ServerEngine, Master.ServerInterface);

            // Registering Administrator handlers.
            RegisterHandlers();

            // Start User Tracking (Send data to tracking server each hour and crash reports)
            if (serverInfo.Configuration.SendUsageAndCrashReports) {
                Tracking.Client.Instance.StartTrackUsage(Master.ServerInterface, Master.ServerEngine.HostLog);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        static void RegisterHandlers() {

            // Registering default handler for ALL static resources on the server.
            GET("/{?}", (string res) => {
                return HandlerStatus.NotHandled;
            });

            // Redirecting root to index.html.
            GET("/", () => {
                // Returns this response to original request.
                return Node.LocalhostSystemPortNode.GET("/index.html", null);
            });

            POST("/addstaticcontentdir", (Request req) => {

                // Getting POST contents.
                String content = req.Body;

                // Splitting contents.
                String[] settings = content.Split(new String[] { StarcounterConstants.NetworkConstants.CRLF }, StringSplitOptions.RemoveEmptyEntries);

                // Getting port of the resource.
                UInt16 port = UInt16.Parse(settings[0]);

                // Adding static files serving directory.
                AppsBootstrapper.AddFileServingDirectory(port, settings[1]);

                try {
                    // Registering static handler on given port.
                    GET(port, "/{?}", (string res) => {
                        return HandlerStatus.NotHandled;
                    });
                }
                catch (Exception exc) {
                    UInt32 errCode;

                    // Checking if this handler is already registered.
                    if (ErrorCode.TryGetCode(exc, out errCode)) {
                        if (Starcounter.Error.SCERRHANDLERALREADYREGISTERED == errCode)
                            return "Success!";
                    }
                    throw exc;
                }

                return "Success!";
            });

            #region Debug/Test

            GET("/return/{?}", (int code) => {
                return code;
            });

            GET("/returnstatus/{?}", (int code) => {
                return (System.Net.HttpStatusCode)code;
            });

            GET("/returnwithreason/{?}", (string codeAndReason) => {
                // Example input: 404ThisIsMyCustomReason
                var code = int.Parse(codeAndReason.Substring(0, 3));
                var reason = codeAndReason.Substring(3);
                return new HttpStatusCodeAndReason(code, reason);
            });

            GET("/test", () => {
                return "hello";
            });

            POST("/echotest", (Request req) => {
                return new Response() { BodyBytes = req.BodyBytes };
            });

            GET("/echotestws", (Request req) =>
            {
                return new Response() { BodyBytes = req.BodyBytes };
            });

            #endregion

        }

        static public string EncodeTo64(string toEncode) {
            byte[] toEncodeAsBytes = System.Text.ASCIIEncoding.ASCII.GetBytes(toEncode);
            string returnValue = System.Convert.ToBase64String(toEncodeAsBytes);
            return returnValue;
        }

        static public string DecodeFrom64(string encodedData) {
            byte[] encodedDataAsBytes = System.Convert.FromBase64String(encodedData);
            string returnValue = System.Text.ASCIIEncoding.ASCII.GetString(encodedDataAsBytes);
            return returnValue;
        }
    }


}
