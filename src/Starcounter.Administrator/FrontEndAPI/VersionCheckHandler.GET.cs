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
using System.Globalization;

namespace Starcounter.Administrator.FrontEndAPI {
    internal static partial class FrontEndAPI {

        public static void VersionCheck_GET(ushort port, IServerRuntime server) {

            // Start Database: POST /api/engines/{name}
            // Stop Database: DELETE <EngineUri>/{name}

            // Get a list of all databases with running status
            //{
            //  "VersionCheck"
            //      {
            //          "current":"2.0.0.0",
            //          "currentDate":"2012-04-23T18:25:43.511Z",
            //          "latest":"2.0.1167.3",
            //          "latestDate":"2012-04-23T18:25:43.511Z",
            //          "latestUri":"http://downloads.starcounter.com/beta",
            //          "showNotice":true
            //      }
            //}
            Handle.GET("/api/admin/versioncheck", (Request req) => {

                try {

                    string channel = "NightlyBuilds";

                    // Retrive the latest available version
                    Response response;

//#if ANDWAH
//                    X.GET("http://192.168.8.183:80/api/channels/" + channel, null, out response, 5000);
//#else
                    X.GET("http://downloads.starcounter.com:80/api/channels/" + channel, null, out response, 5000);
//#endif

                    if (!response.IsSuccessStatusCode) {
                        // TODO: Add "Retry-After" header
                        return new Response() { StatusCode = (ushort)System.Net.HttpStatusCode.ServiceUnavailable };
                    }

                    dynamic incomingJson = DynamicJson.Parse(response.Body);

                    var result = new versioncheck();

                    result.VersionCheck.currentVersion = CurrentVersion.Version;
                    result.VersionCheck.currentVersionDate = CurrentVersion.VersionDate.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");

                    result.VersionCheck.latestVersion = incomingJson.latestVersion;

                    DateTime latestVersionDate = DateTime.Parse(incomingJson.latestVersionDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                    result.VersionCheck.latestVersionDate = latestVersionDate.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");

                    result.VersionCheck.latestVersionDownloadUri = incomingJson.downloadUrl;

                    return new Response() { StatusCode = (ushort)System.Net.HttpStatusCode.OK, BodyBytes = result.ToJsonUtf8() };
                }
                catch (Exception e) {
                    return RestUtils.CreateErrorResponse(e);
                }

            });





        }



    }
}
