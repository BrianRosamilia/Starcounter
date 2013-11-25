using System;
using Starcounter;
using Starcounter.Advanced;
using StarcounterApplicationWebSocket.VersionHandler.Model;
using Codeplex.Data;
using System.Net;
using StarcounterApplicationWebSocket.VersionHandler;
using System.IO;

namespace Starcounter.Applications.UsageTrackerApp.API.Versions {


    internal class Versions_Get {

        public static void BootStrap(ushort port) {


            // Get a list of all available versions grouped by channel
            // JSON Format: 
            // {
            //      "channels":[{
            //          "name":"DailyBuilds",
            //          "latestVersion":"2.0.5901.2",
            //          "downloadUrl":"http://downloads.starcounter.com/archive/DailyBuilds/2.0.5901.2",
            //          "versions":[{
            //              "channel":"DailyBuilds",
            //              "version":"2.0.5901.2",
            //              "versionDate":"2007-04-05T14:30:00.000Z",
            //              "downloadUrl":"http://downloads.starcounter.com/archive/DailyBuilds/2.0.5901.2"
            //          }]
            //      }]
            // }
            Handle.GET(port, "/api/versions", (Request request) => {

                try {

                    // TODO: Also check if the versionSource has been unpacked and ready to build
                    var result = Db.SlowSQL<VersionSource>("SELECT o FROM VersionSource o WHERE o.IsAvailable=? ORDER BY o.Channel, o.VersionDate DESC", true);

                    AllVersions allVersionItems = new AllVersions();

                    AllVersions.channelsElementJson currentChannelItem = null;
                    foreach (VersionSource versionSource in result) {

                        if (currentChannelItem == null || !String.Equals(currentChannelItem.name, versionSource.Channel)) {
                            // Add channel
                            currentChannelItem = allVersionItems.channels.Add();
                            currentChannelItem.name = versionSource.Channel;

                            VersionSource latestVersion = VersionSource.GetLatestVersion(versionSource.Channel);
                            if (latestVersion != null) {
                                currentChannelItem.latestVersion = latestVersion.Version;
                                currentChannelItem.latestVersionDate = latestVersion.VersionDate.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
                                currentChannelItem.downloadUrl = string.Format("http://{0}/archive/{1}/{2}", VersionHandlerApp.StarcounterTrackerUrl, latestVersion.Channel, latestVersion.Version);
                            }

                        }

                        var versionItem = currentChannelItem.versions.Add();

                        versionItem.version = versionSource.Version;
                        versionItem.channel = versionSource.Channel;

                        // Send the UTC DateTime
                        versionItem.versionDate = versionSource.VersionDate.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
                        versionItem.downloadUrl = string.Format("http://{0}/archive/{1}/{2}", VersionHandlerApp.StarcounterTrackerUrl, versionSource.Channel, versionSource.Version);
                    }

                    return new Response() { StatusCode = (ushort)System.Net.HttpStatusCode.OK, BodyBytes = allVersionItems.ToJsonUtf8() };

                }
                catch (Exception e) {
                    return new Response() { StatusCode = (ushort)System.Net.HttpStatusCode.InternalServerError, Body = e.ToString() };
                }

            });


            // Get a list of all available versions in a specific channel
            // JSON Format: 
            // {
            //      "versions":[{
            //          "channel":"NightlyBuilds",
            //          "version":"2.0.801.3",
            //          "buildDate":"",
            //          "downloadUrl":"http://downloads.starcounter.com/archive/nightlybuilds/2.0.801.3"
            //      }]
            // }
            Handle.GET(port, "/api/versions/{?}", (string channel, Request request) => {

                try {

                    var result = Db.SlowSQL<VersionSource>("SELECT o FROM VersionSource o WHERE o.IsAvailable=? AND o.Channel=?", true, channel);

                    if (result == null) {
                        return new Response() { StatusCode = (ushort)System.Net.HttpStatusCode.NotFound, Body = string.Format("Channel {0} not found", channel) };
                    }

                    Versions versions = new Versions();

                    foreach (VersionSource versionSource in result) {
                        var item = versions.versions.Add();
                        item.channel = versionSource.Channel;
                        item.version = versionSource.Version;
                        item.versionDate = versionSource.VersionDate.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
                        item.downloadUrl = string.Format("http://{0}/archive/{1}/{2}", VersionHandlerApp.StarcounterTrackerUrl, channel, versionSource.Version);
                        //item.buildDate =
                    }

                    return new Response() { StatusCode = (ushort)System.Net.HttpStatusCode.OK, BodyBytes = versions.ToJsonUtf8() };

                }
                catch (Exception e) {
                    return new Response() { StatusCode = (ushort)System.Net.HttpStatusCode.InternalServerError, Body = e.ToString() };
                }

            });


            // Version information in JSON 
            // JSON Format: 
            // {
            //      "version":"2.0.801.3",
            //      "downloadUrl":"http://downloads.starcounter.com/archive/NightlyBuilds/2.0.801.3"
            // }
            Handle.GET(port, "/api/versions/{?}/{?}", (string channel, string version, Request request) => {

                try {

                    VersionSource versionSource = Db.SlowSQL<VersionSource>("SELECT o FROM VersionSource o WHERE o.IsAvailable=? AND o.Channel=? AND o.Version=?", true, channel, version).First;
                    if (versionSource == null) {
                        return new Response() { StatusCode = (ushort)System.Net.HttpStatusCode.NotFound, Body = string.Format("Version {0} in channel {1} was not found", version, channel) };
                    }

                    Version versionItem = new Version();
                    versionItem.version = versionSource.Version;
                    versionItem.versionDate = versionSource.VersionDate.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
                    versionItem.downloadUrl = string.Format("http://{0}/archive/{1}/{2}", VersionHandlerApp.StarcounterTrackerUrl, versionSource.Channel, versionSource.Version);

                    return new Response() { StatusCode = (ushort)System.Net.HttpStatusCode.OK, BodyBytes = versionItem.ToJsonUtf8() };

                }
                catch (Exception e) {
                    return new Response() { StatusCode = (ushort)System.Net.HttpStatusCode.InternalServerError, Body = e.ToString() };
                }

            });

        }
    }

}
