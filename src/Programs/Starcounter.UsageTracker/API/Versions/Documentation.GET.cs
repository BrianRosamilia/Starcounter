
using System;
using System.IO;
using Starcounter.Advanced;
using StarcounterApplicationWebSocket.VersionHandler.Model;

namespace Starcounter.Applications.UsageTrackerApp.API.Versions {


    internal class Documentation_Get {

        public static void BootStrap(ushort port) {


            // Documentation
            // Redirect to the latest documentation version
            Handle.GET(port, "/doc", (Request request) => {

                try {
                    string channel = "NightlyBuilds";

                    VersionSource versionSource = Db.SlowSQL<VersionSource>("SELECT o FROM VersionSource o WHERE o.Channel=? AND o.IsAvailable=? AND o.DocumentationFolder IS NOT NULL ORDER BY o.VersionDate DESC", channel, true).First;
                    if (versionSource == null) {
                        string message = string.Format("At the moment there is no documentation available. Please try again later.");
                        return new Response() { StatusCode = (ushort)System.Net.HttpStatusCode.NotFound, Body = message };
                    }
                    else {
                        if (!Directory.Exists(versionSource.DocumentationFolder)) {
                            string message = string.Format("The documentation is not available.");
                            return new Response() { StatusCode = (ushort)System.Net.HttpStatusCode.InternalServerError, Body = message };
                        }
                    }

                    string documentationPath = string.Format("/{0}/{1}/{2}", versionSource.Channel, versionSource.Version, "webframe.html");

                    Response response = new Response() { StatusCode = (ushort)System.Net.HttpStatusCode.Redirect };
                    response["Location"] = documentationPath;
                    return response;
                }
                catch (Exception e) {
                    return new Response() { StatusCode = (ushort)System.Net.HttpStatusCode.InternalServerError, Body = e.ToString() };
                }
            });


        }
    }

}
