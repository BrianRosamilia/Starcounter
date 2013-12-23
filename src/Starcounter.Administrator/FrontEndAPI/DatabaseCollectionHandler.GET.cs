using System;
using Codeplex.Data;
using Starcounter;
using Starcounter.Advanced;
using Starcounter.Server.PublicModel;
using System.Net;
using System.Diagnostics;
using Starcounter.Internal;
using Starcounter.Internal.Web;
using Starcounter.Administrator.API.Utilities;
using Starcounter.Administrator.API.Handlers;
using Starcounter.Server.Rest.Representations.JSON;
using Starcounter.Server.Rest;
using Starcounter.CommandLine;
using System.IO;
using Starcounter.Rest.ExtensionMethods;

namespace Starcounter.Administrator.FrontEndAPI {
    internal static partial class FrontEndAPI {

        public static void Database_GET(ushort port, IServerRuntime server) {


            // Start Database: POST /api/engines/{name}
            // Stop Database: DELETE <EngineUri>/{name}

            // Get a list of all databases with running status
            //{
            //  "Databases":[
            //      {
            //          "Name":"tracker",
            //          "Uri":"http://headsutv19:8181/api/databases/tracker",
            //          "HostUri":"http://headsutv19:8181/api/engines/tracker/db",
            //          "Running":true
            //      }
            //  ]
            //}
            Handle.GET("/api/admin/databases", (Request req) => {

                try {

                    var serverRuntime = RootHandler.Host.Runtime;
                    var applicationDatabases = serverRuntime.GetDatabases();
                    var admin = RootHandler.API;

                    var result = new databases();

                    //var result = new DatabaseCollection();
                    foreach (DatabaseInfo databaseInfo in applicationDatabases) {
                        var db = result.Databases.Add();
                        db.name = databaseInfo.Name;
                        db.uri = admin.Uris.Database.ToAbsoluteUri(databaseInfo.Name);
                        db.engineUri = admin.Uris.Engine.ToAbsoluteUri(databaseInfo.Name);

                        string uriTemplateDbProcess = RootHandler.API.Uris.Engine + "/db";
                        db.databaseProcessUri = uriTemplateDbProcess.ToAbsoluteUri(databaseInfo.Name);

                        string uriTemplateHostProcess = RootHandler.API.Uris.Engine + "/host";
                        db.codeHostProcessUri = uriTemplateHostProcess.ToAbsoluteUri(databaseInfo.Name);

                        // Get Database status
                        EngineInfo engineInfo = databaseInfo.Engine;
                        if (engineInfo != null && engineInfo.DatabaseProcessRunning) {
                            db.running = true;
                        }


                    }

                    return new Response() { StatusCode = (ushort)System.Net.HttpStatusCode.OK, BodyBytes = result.ToJsonUtf8() };
                }
                catch (Exception e) {
                    return RestUtils.CreateErrorResponse(e);
                }

            });

            // Start Executable: POST /api/engines/{enginename}/executables   [Bodydata]
            // Stop All Executables: DELETE /api/engines/{enginename}/host
            // Stop Executable: DELETE 

            // Get a list of all running Executables
            //{
            //  "Executables":[
            //      {
            //          "path":"C:\\path\to\\the\\exe\\foo.exe",
            //          "uri":"http://example.com/api/engines/foo/executables/foo.exe-123456789",
            //          "applicationFilePath":"",
            //          "databaseName":"default"
            //      }
            //  ]
            //}
            Handle.GET("/api/admin/executables", (Request req) => {

                try {

                    IServerRuntime serverRuntime = RootHandler.Host.Runtime;
                    DatabaseInfo[] applicationDatabases = serverRuntime.GetDatabases();
                    var admin = RootHandler.API;

                    var result = new executables();

                    foreach (DatabaseInfo databaseInfo in applicationDatabases) {

                        EngineInfo engineInfo = databaseInfo.Engine;

                        if (engineInfo != null && engineInfo.HostProcessId != 0) {

                            if (engineInfo.HostedApps != null) {
                                foreach (AppInfo appInfo in engineInfo.HostedApps) {
                                    var executable = result.Executables.Add();
                                    executable.path = appInfo.ExecutablePath;
                                    executable.applicationFilePath = appInfo.ApplicationFilePath;
                                    executable.uri = admin.Uris.Executable.ToAbsoluteUri(databaseInfo.Name, appInfo.Key);
                                    executable.databaseName = databaseInfo.Name;
                                    if (appInfo.Arguments != null) {
                                        foreach (string arg in appInfo.Arguments) {
                                            var item = executable.arguments.Add();
                                            item.dummy = arg;
                                        }
                                    }
                                }
                            }
                        }

                    }

                    return new Response() { StatusCode = (ushort)System.Net.HttpStatusCode.OK, BodyBytes = result.ToJsonUtf8() };
                }
                catch (Exception e) {
                    return RestUtils.CreateErrorResponse(e);
                }

            });


            Handle.GET("/api/admin/databases/{?}/settings", (string name, Request req) => {

                lock (LOCK) {

                    try {

                        DatabaseInfo database = Master.ServerInterface.GetDatabaseByName(name);

                        if (database == null) {
                            // Database not found

                            dynamic errorJson = new DynamicJson();

                            errorJson.message = string.Format("Could not find the {0} database", name);
                            errorJson.code = (int)HttpStatusCode.NotFound;
                            errorJson.helpLink = "http://en.wikipedia.org/wiki/HTTP_404"; // TODO

                            return RESTUtility.JSON.CreateResponse(errorJson.ToString(), (int)HttpStatusCode.NotFound);
                        }
                        else {
                        }

                        dynamic resultJson = new DynamicJson();

                        // Return the database
                        resultJson.settings = new {
                            name = database.Name,
                            hostProcessId = database.Engine == null ? 0 : database.Engine.HostProcessId,
                            httpPort = database.Configuration.Runtime.DefaultUserHttpPort,
                            schedulerCount = database.Configuration.Runtime.SchedulerCount ?? Environment.ProcessorCount,
                            chunksNumber = database.Configuration.Runtime.ChunksNumber,
                            sqlAggregationSupport = database.Configuration.Runtime.SqlAggregationSupport,

                            //dumpDirectory = database.Configuration.Runtime.DumpDirectory,
                            tempDirectory = database.Configuration.Runtime.TempDirectory,
                            imageDirectory = database.Configuration.Runtime.ImageDirectory,
                            transactionLogDirectory = database.Configuration.Runtime.TransactionLogDirectory,

                        };

                        // collationFile
                        return RESTUtility.JSON.CreateResponse(resultJson.ToString());
                    }
                    catch (Exception e) {
                        return RestUtils.CreateErrorResponse(e);
                    }
                }

            });





        }



    }
}
