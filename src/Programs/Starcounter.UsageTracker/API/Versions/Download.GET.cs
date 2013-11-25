using System;
using Starcounter;
using Starcounter.Advanced;
using StarcounterApplicationWebSocket.VersionHandler.Model;
using Codeplex.Data;
using System.Net;
using StarcounterApplicationWebSocket.VersionHandler;
using System.IO;
using Starcounter.Applications.UsageTrackerApp.VersionHandler;

namespace Starcounter.Applications.UsageTrackerApp.API.Versions {


    internal class Download {

        public static void BootStrap(ushort port) {


            // This is use to let the user navigate to /download and then be presented by downloads.html
            Handle.GET(port, "/download", (Request request) => {
                Response response = new Response() { StatusCode = (ushort)System.Net.HttpStatusCode.Redirect };
                response["Location"] = "/download/";
                return response;
            });

            // Show download page
            Handle.GET(port, "/download/{?}", (string res, Request request) => {
                Node node = new Node("127.0.0.1", port);
                return node.GET("/downloads.html", null);
            });


            // Download latest version from channel 'NightlyBuilds'
            // This link is used by www.starcounter.com
            Handle.GET(port, "/beta", (Request request) => {

                try {

                    string ipAddress = request.GetClientIpAddress().ToString();
                    if (Utils.IsBlacklisted(ipAddress)) {
                        LogWriter.WriteLine(string.Format("WARNING: IP Address {1} is blacklisted", ipAddress));
                        return new Response() { StatusCode = (ushort)System.Net.HttpStatusCode.Forbidden };
                    }

                    VersionBuild versionBuild = VersionBuild.GetLatestAvailableBuild("NightlyBuilds");
                    if (versionBuild == null) {
                        string message = string.Format("The download is not available at the moment. Please try again later.");
                        return new Response() { StatusCode = (ushort)System.Net.HttpStatusCode.ServiceUnavailable, Body = message };
                    }

                    byte[] fileBytes = File.ReadAllBytes(versionBuild.File);


                    Db.Transaction(() => {
                        versionBuild.DownloadDate = DateTime.UtcNow;
                        versionBuild.IPAdress = ipAddress;
                    });

                    VersionHandlerApp.BuildkWorker.Trigger();

                    LogWriter.WriteLine(string.Format("NOTICE: Sending version {0} to ip {1}", versionBuild.Version, request.GetClientIpAddress().ToString()));

                    string fileName = Path.GetFileName(versionBuild.File);

                    // Delete version build file
                    VersionBuild.DeleteVersionBuildFile(versionBuild);

                    // Assure IP Location
                    Utils.AssureIPLocation(versionBuild.IPAdress);

                    Response response = new Response() { BodyBytes = fileBytes, StatusCode = (ushort)System.Net.HttpStatusCode.OK };
                    response["Content-Disposition"] = "attachment; filename=\"" + fileName + "\"";
                    return response;

                }
                catch (Exception e) {
                    return new Response() { StatusCode = (ushort)System.Net.HttpStatusCode.InternalServerError, Body = e.ToString() };
                }

            });


            // Download latest version from channel 'NightlyBuilds' with key
            // This is used by users how got an email with a personal download link
            Handle.GET(port, "/beta/{?}", (string downloadkey, Request request) => {

                try {

                    string ipAddress = request.GetClientIpAddress().ToString();
                    if (Utils.IsBlacklisted(ipAddress)) {
                        LogWriter.WriteLine(string.Format("WARNING: IP Address {1} is blacklisted", ipAddress));
                        return new Response() { StatusCode = (ushort)System.Net.HttpStatusCode.Forbidden };
                    }

                    // Find a valid sombody
                    Somebody somebody = Db.SlowSQL<Somebody>("SELECT o FROM Somebody o WHERE o.DownloadKey=?", downloadkey).First;
                    if (somebody == null) {
                        return new Response() { StatusCode = (ushort)System.Net.HttpStatusCode.NotFound };
                    }

                    VersionBuild versionBuild = VersionBuild.GetLatestAvailableBuild("NightlyBuilds");
                    if (versionBuild == null) {
                        string message = string.Format("The download is not available at the moment. Please try again later.");
                        return new Response() { StatusCode = (ushort)System.Net.HttpStatusCode.ServiceUnavailable, Body = message };
                    }


                    byte[] fileBytes = File.ReadAllBytes(versionBuild.File);

                    Db.Transaction(() => {
                        versionBuild.DownloadDate = DateTime.UtcNow;
                        versionBuild.DownloadKey = downloadkey;
                        versionBuild.IPAdress = ipAddress;
                    });

                    VersionHandlerApp.BuildkWorker.Trigger();

                    LogWriter.WriteLine(string.Format("NOTICE: Sending version {0} to ip {1} with Key {2}", versionBuild.Version, request.GetClientIpAddress().ToString(), downloadkey));

                    string fileName = Path.GetFileName(versionBuild.File);

                    // Delete version build file
                    VersionBuild.DeleteVersionBuildFile(versionBuild);

                    // Assure IP Location
                    Utils.AssureIPLocation(versionBuild.IPAdress);

                    Response response = new Response() { BodyBytes = fileBytes, StatusCode = (ushort)System.Net.HttpStatusCode.OK };
                    response["Content-Disposition"] = "attachment; filename=\"" + fileName + "\"";
                    return response;

                }
                catch (Exception e) {
                    return new Response() { StatusCode = (ushort)System.Net.HttpStatusCode.InternalServerError, Body = e.ToString() };
                }

            });


            // Download a version from a channel
            Handle.GET(port, "/archive/{?}/{?}", (string channel, string version, Request request) => {

                try {

                    string ipAddress = request.GetClientIpAddress().ToString();
                    if (Utils.IsBlacklisted(ipAddress)) {
                        LogWriter.WriteLine(string.Format("WARNING: IP Address {1} is blacklisted", ipAddress));
                        return new Response() { StatusCode = (ushort)System.Net.HttpStatusCode.Forbidden };
                    }

                    // Check if source exist for specified channel and version.
                    VersionSource versionSource = Db.SlowSQL<VersionSource>("SELECT o FROM VersionSource o WHERE o.Channel=? AND o.Version=? AND o.IsAvailable=?", channel, version, true).First;
                    if (versionSource == null) {
                        string message = string.Format("The version {0} in channel {1} is invalid or not available.", version, channel);
                        return new Response() { StatusCode = (ushort)System.Net.HttpStatusCode.NotFound, Body = message };
                    }

                    VersionBuild versionBuild = VersionBuild.GetAvailableBuild(channel, version);
                    if (versionBuild == null) {
                        string message = string.Format("The download is not available at the moment. Please try again later.");
                        return new Response() { StatusCode = (ushort)System.Net.HttpStatusCode.ServiceUnavailable, Body = message };
                    }

                    byte[] fileBytes = File.ReadAllBytes(versionBuild.File);

                    Db.Transaction(() => {
                        versionBuild.DownloadDate = DateTime.UtcNow;
                        versionBuild.IPAdress = ipAddress;
                    });

                    VersionHandlerApp.BuildkWorker.Trigger();

                    LogWriter.WriteLine(string.Format("NOTICE: Sending version {0} to ip {1}", versionBuild.Version, request.GetClientIpAddress().ToString()));

                    string fileName = Path.GetFileName(versionBuild.File);

                    // Delete version build file
                    VersionBuild.DeleteVersionBuildFile(versionBuild);

                    // Assure IP Location
                    Utils.AssureIPLocation(versionBuild.IPAdress);

                    Response response = new Response() { BodyBytes = fileBytes, StatusCode = (ushort)System.Net.HttpStatusCode.OK };
                    response["Content-Disposition"] = "attachment; filename=\"" + fileName + "\"";
                    return response;

                }
                catch (Exception e) {
                    return new Response() { StatusCode = (ushort)System.Net.HttpStatusCode.InternalServerError, Body = e.ToString() };
                }

            });


            // Download latest version from channel 'NightlyBuilds'
            // This link is given to ppl that wanted a direct download link wihtout a key.
            // NOTE: deprecated, it's replaces by the /beta link
            Handle.GET(port, "/hiddenarea/latest", (Request request) => {
                Response response = new Response() { StatusCode = (ushort)System.Net.HttpStatusCode.MovedPermanently };
                response["Location"] = "/beta";
                return response;
            });

        }


    }
}
