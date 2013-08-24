﻿using System;
using Starcounter;
using Starcounter.Advanced;
using StarcounterApplicationWebSocket.VersionHandler.Model;
using Codeplex.Data;
using Starcounter.Internal.Web;
using System.Net;
using StarcounterApplicationWebSocket.VersionHandler;
using System.IO;

namespace Starcounter.Applications.UsageTrackerApp.API.Versions {


    internal class Download {

        public static void BootStrap(ushort port) {

            // downloads.starcounter.com/beta/<downloadkey>

            Handle.GET(port, "/beta/{?}", (string downloadkey, Request request) => {

                try {

                    // Find a valid sombody
                    Somebody somebody = Db.SlowSQL("SELECT o FROM Somebody o WHERE o.DownloadKey=?", downloadkey).First;
                    if (somebody == null) {
                        return new Response() { StatusCode = (ushort)System.Net.HttpStatusCode.NotFound };
                    }

                    VersionBuild latestBuild = VersionBuild.GetLatestAvailableBuild("NightlyBuilds");
                    if (latestBuild == null) {
                        // TODO: Redirect to a information page?
                        string message = string.Format("The download is not available at the moment. Please try again later.");
                        return new Response() { Uncompressed = HttpResponseBuilder.Slow.FromStatusHeadersAndStringContent((int)HttpStatusCode.ServiceUnavailable, null, message) };

                    }

                    byte[] fileBytes = File.ReadAllBytes(latestBuild.File);

                    Db.Transaction(() => {
                        latestBuild.DownloadDate = DateTime.UtcNow;
                        latestBuild.DownloadKey = downloadkey;
                        latestBuild.IPAdress = request.GetClientIpAddress().ToString();
                    });

                    VersionHandlerApp.BuildkWorker.Trigger();

                    LogWriter.WriteLine(string.Format("NOTICE: Sending version {0} to ip {1}", latestBuild.Version, request.GetClientIpAddress().ToString()));

                    string fileName = Path.GetFileName(latestBuild.File);

                    return new Response() { BodyBytes = fileBytes, Headers = "Content-Disposition: attachment; filename=\"" + fileName + "\"\r\n", StatusCode = (ushort)System.Net.HttpStatusCode.OK };

                }
                catch (Exception e) {
                    return new Response() { StatusCode = (ushort)System.Net.HttpStatusCode.InternalServerError, Body = e.ToString() };
                }

            });

        }
    }
}