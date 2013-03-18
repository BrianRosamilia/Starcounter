﻿
using Starcounter;
using Starcounter.ABCIPC.Internal;
using Starcounter.Administrator;
using Starcounter.Advanced;
using Starcounter.Internal;
using Starcounter.Internal.REST;
using Starcounter.Server;
using Starcounter.Server.PublicModel;
using StarcounterAppsLogTester;
using System;
using System.IO;
using System.Net;

// http://msdn.microsoft.com/en-us/library/system.runtime.compilerservices.internalsvisibletoattribute.aspx

namespace StarcounterApps3 {

    partial class Master : Puppet {

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
            UInt16 adminPort = StarcounterConstants.NetworkPorts.DefaultPersonalServerSystemHttpPort;
            if (UInt16.TryParse(args[1], out adminPort) == false) {
                Console.WriteLine("Starcounter Administrator: Invalid port number {0}", args[1]);
                return;
            };

            Console.WriteLine("Starcounter Administrator started on port: " + adminPort);

            AppsBootstrapper.Bootstrap(adminPort, "scadmin");

            Master.ServerEngine = new ServerEngine(args[0]);      // .srv\Personal\Personal.server.config
            Master.ServerEngine.Setup();
            Master.ServerInterface = Master.ServerEngine.Start();

            // Start Engine services
            StartListeningService();

            // Start listening on log-events
            ServerInfo serverInfo = Master.ServerInterface.GetServerInfo();

            //SetupLogListener(serverInfo.Configuration.LogDirectory);

            LogApp.Setup(serverInfo.Configuration.LogDirectory);

            HostedExecutables.Setup(
                Dns.GetHostEntry(String.Empty).HostName,
                adminPort,
                Master.ServerEngine, 
                Master.ServerInterface
            );

            RegisterGETS();
        }

        static void RegisterGETS() {

            // Registering default handler for ALL static resources on the server.
            GET("/{?}", (string res) => {
                return null;
            });

            POST("/addstaticcontentdir", (HttpRequest req) => {

                // Getting POST contents.
                String content = req.GetContentStringUtf8_Slow();

                // Splitting contents.
                String[] settings = content.Split(
                    new String[] { StarcounterConstants.NetworkConstants.CRLF },
                    StringSplitOptions.RemoveEmptyEntries);

                try
                {
                    // Registering static handler on given port.
                    GET(UInt16.Parse(settings[0]), "/{?}", (string res) => {
                        return null;
                    });
                }
                catch (Exception exc)
                {
                    UInt32 errCode;

                    // Checking if this handler is already registered.
                    if (ErrorCode.TryGetCode(exc, out errCode))
                    {
                        if (Starcounter.Error.SCERRHANDLERALREADYREGISTERED == errCode)
                            return "Success!";
                    }
                    throw exc;
                }

                // Adding static files serving directory.
                AppsBootstrapper.AddFileServingDirectory(settings[1]);

                return "Success!";
            });

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

            GET("/", () => {
                return new Master() { View = "index.html" };
            });

            // Accept "", text/html, OR application/json. Otherwise, 406.
            GET("/server", () => {

                ServerInfo serverInfo = Master.ServerInterface.GetServerInfo();

                ServerApp serverApp = new ServerApp();
                serverApp.View = "server.html";
                serverApp.DatabaseDirectory = serverInfo.Configuration.DatabaseDirectory;
                serverApp.LogDirectory = serverInfo.Configuration.LogDirectory;
                serverApp.TempDirectory = serverInfo.Configuration.TempDirectory;
                serverApp.ServerName = serverInfo.Configuration.Name;

                return serverApp;
            });

            GET("/databases", () => {

                DatabaseInfo[] databases = Master.ServerInterface.GetDatabases();

                DatabasesApp databaseList = new DatabasesApp();

                databaseList.View = "databases.html";
                foreach (var database in databases) {
                    DatabaseApp databaseApp = new DatabaseApp();
                    databaseApp.SetDatabaseInfo(database);

                    databaseList.DatabaseList.Add(databaseApp);

                }

                return databaseList;

            });

            GET("/databases/{?}", (string uri) => {

                DatabaseInfo database = Master.ServerInterface.GetDatabase(uri);

                DatabaseApp databaseApp = new DatabaseApp();
                databaseApp.View = "database.html";

                databaseApp.SetDatabaseInfo(database);

                return databaseApp;
            });


            GET("/apps", () => {
                AppsApp appsApp = new AppsApp();
                appsApp.View = "apps.html";

                appsApp.Setup();

                return appsApp;
            });


            GET("/query", () => {

                SqlApp sqlApp = new SqlApp();
                sqlApp.View = "sql.html";
                sqlApp.DatabaseName = "default";
                sqlApp.Query = "SELECT m FROM systable m";
                sqlApp.Port = 8181;

                return sqlApp;

                //return new SqlApp() { View = "sql.html", DatabaseName = "default", Query = "SELECT m FROM systable m", Port = 8181 };
            });

            GET("/log", () => {
                LogApp logApp = new LogApp() { FilterNotice = true, FilterWarning = true, FilterError = true };
                logApp.View = "log.html";
                logApp.RefreshLogEntriesList();
                return logApp;
            });
        }

        #region ServerServices

        static void StartListeningService() {
            System.Threading.ThreadPool.QueueUserWorkItem(ServerServicesThread);
        }

        static private void ServerServicesThread(object state) {
            ServerServices services;
            string pipeName;
            pipeName = ScUriExtensions.MakeLocalServerPipeString(Master.ServerEngine.Name);
            var ipcServer = ClientServerFactory.CreateServerUsingNamedPipes(pipeName);
            ipcServer.ReceivedRequest += OnIPCServerReceivedRequest;
            services = new ServerServices(Master.ServerEngine, ipcServer);
            ToConsoleWithColor(string.Format("Accepting service calls on pipe '{0}'...", pipeName), ConsoleColor.DarkGray);

            services.Setup();
            // Start the engine and run the configured services.
            Master.ServerEngine.Run(services);

        }

        static void OnIPCServerReceivedRequest(object sender, string e) {
            ToConsoleWithColor(string.Format("Request: {0}", e), ConsoleColor.Yellow);
        }

        #endregion

        static void ToConsoleWithColor(string text, ConsoleColor color) {
            try {
                Console.ForegroundColor = color;
                Console.WriteLine(text);
            } finally {
                Console.ResetColor();
            }
        }
    }

}
