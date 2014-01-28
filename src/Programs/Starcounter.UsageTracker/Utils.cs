using System;
using Codeplex.Data;
using Starcounter.Advanced;
using StarcounterApplicationWebSocket.VersionHandler;
using StarcounterApplicationWebSocket.VersionHandler.Model;

namespace Starcounter.Applications.UsageTrackerApp {
    /// <summary>
    /// General utils
    /// </summary>
    internal class Utils {

        /// <summary>
        /// Create an Error Json response from an exception
        /// </summary>
        /// <param name="e"></param>
        /// <returns>Response</returns>
        public static Response CreateErrorResponse(Exception e) {

            dynamic response = new DynamicJson();

            // Create error response
            response.exception = new { };
            response.exception.message = e.Message;
            response.exception.stackTrace = e.StackTrace;
            response.exception.helpLink = e.HelpLink;

            return new Response() { Body = response.ToString(), StatusCode = (ushort)System.Net.HttpStatusCode.InternalServerError };
        }

        /// <summary>
        /// Parse out the protocol version number from the header
        /// </summary>
        /// <param name="request"></param>
        /// <returns>Protocol number</returns>
        public static int GetRequestProtocolVersion(Request request) {

            // Accept: application/starcounter.tracker.usage-v2+json\r\n

            string headers = request.Headers;

            try {

                if (!string.IsNullOrEmpty(headers)) {

                    string[] lines = headers.Split(new string[] { "\r\n" }, StringSplitOptions.None);
                    for (int i = 0; i < lines.Length; i++) {

                        string line = lines[i];

                        // Check if the line is field values
                        if (line.StartsWith(" ") || line.StartsWith("\t")) continue;

                        if (line.StartsWith("accept:", StringComparison.CurrentCultureIgnoreCase)) {
                            // Found our accept header.
                            string match = "application/starcounter.tracker.usage-v";

                            int vStartIndex = line.IndexOf(match, 7, StringComparison.CurrentCultureIgnoreCase);
                            if (vStartIndex == -1) continue;
                            vStartIndex += match.Length;
                            int vStopIndex = line.IndexOf('+', vStartIndex);
                            if (vStopIndex == -1) continue;

                            string str = line.Substring(vStartIndex, vStopIndex - vStartIndex);
                            int num;

                            if (int.TryParse(str, out num) == false) continue;

                            // TODO: For the moment we will ignore values that spans over multiple lines
                            // In the future i think this code will be replaced by logic in the Request
                            return num;
                        }

                    }

                }
            }
            catch (Exception) {
            }

            return 1;
        }


        public static void AssureIndexes() {

            #region Installation

            if (Starcounter.Db.SQL("SELECT i FROM MATERIALIZED_INDEX i WHERE Name=?", "InstallationNoIndex").First == null) {
                Starcounter.Db.SQL("CREATE INDEX InstallationNoIndex ON Installation (InstallationNo)");
            }

            if (Starcounter.Db.SQL("SELECT i FROM MATERIALIZED_INDEX i WHERE Name=?", "InstallationPrevNoIndex").First == null) {
                Starcounter.Db.SQL("CREATE INDEX InstallationPrevNoIndex ON Installation (PreviousInstallationNo)");
            }

            //if (Starcounter.Db.SQL("SELECT i FROM MATERIALIZED_INDEX i WHERE Name=?", "InstallationDateIndex").First == null) {
            //    Starcounter.Db.SQL("CREATE INDEX InstallationDateIndex ON Installation (\"'Date'\")");
            //}

            #endregion

            #region InstallerAbort
            if (Starcounter.Db.SQL("SELECT i FROM MATERIALIZED_INDEX i WHERE Name=?", "InstallerAbortIndex").First == null) {
                Starcounter.Db.SQL("CREATE INDEX InstallerAbortIndex ON InstallerAbort (Installation)");
            }

            //if (Starcounter.Db.SQL("SELECT i FROM MATERIALIZED_INDEX i WHERE Name=?", "InstallerAbortDateIndex").First == null) {
            //    Starcounter.Db.SQL("CREATE INDEX InstallerAbortDateIndex ON InstallerAbort (\"Date\")");
            //}
            #endregion

            #region InstallerEnd
            if (Starcounter.Db.SQL("SELECT i FROM MATERIALIZED_INDEX i WHERE Name=?", "InstallerEndIndex").First == null) {
                Starcounter.Db.SQL("CREATE INDEX InstallerEndIndex ON InstallerEnd (Installation)");
            }

            //if (Starcounter.Db.SQL("SELECT i FROM MATERIALIZED_INDEX i WHERE Name=?", "InstallerEndDateIndex").First == null) {
            //    Starcounter.Db.SQL("CREATE INDEX InstallerEndDateIndex ON InstallerEnd (\"Date\")");
            //}
            #endregion

            #region InstallerExecuting
            if (Starcounter.Db.SQL("SELECT i FROM MATERIALIZED_INDEX i WHERE Name=?", "InstallerExecutingIndex").First == null) {
                Starcounter.Db.SQL("CREATE INDEX InstallerExecutingIndex ON InstallerExecuting (Installation)");
            }

            //if (Starcounter.Db.SQL("SELECT i FROM MATERIALIZED_INDEX i WHERE Name=?", "InstallerExecutingDateIndex").First == null) {
            //    Starcounter.Db.SQL("CREATE INDEX InstallerExecutingDateIndex ON InstallerExecuting (\"Date\")");
            //}
            #endregion

            #region InstallerFinish
            if (Starcounter.Db.SQL("SELECT i FROM MATERIALIZED_INDEX i WHERE Name=?", "InstallerFinishIndex").First == null) {
                Starcounter.Db.SQL("CREATE INDEX InstallerFinishIndex ON InstallerFinish (Installation)");
            }

            //if (Starcounter.Db.SQL("SELECT i FROM MATERIALIZED_INDEX i WHERE Name=?", "InstallerFinishDateIndex").First == null) {
            //    Starcounter.Db.SQL("CREATE INDEX InstallerFinishDateIndex ON InstallerFinish (\"Date\")");
            //}
            #endregion

            #region InstallerStart
            if (Starcounter.Db.SQL("SELECT i FROM MATERIALIZED_INDEX i WHERE Name=?", "InstallerStartIndex").First == null) {
                Starcounter.Db.SQL("CREATE INDEX InstallerStartIndex ON InstallerStart (Installation)");
            }

            //if (Starcounter.Db.SQL("SELECT i FROM MATERIALIZED_INDEX i WHERE Name=?", "InstallerStartDateIndex").First == null) {
            //    Starcounter.Db.SQL("CREATE INDEX InstallerStartDateIndex ON InstallerStart (\"Date\")");
            //}
            #endregion

            #region InstallerException
            if (Starcounter.Db.SQL("SELECT i FROM MATERIALIZED_INDEX i WHERE Name=?", "InstallerExceptionIndex").First == null) {
                Starcounter.Db.SQL("CREATE INDEX InstallerExceptionIndex ON InstallerException (Installation)");
            }

            //if (Starcounter.Db.SQL("SELECT i FROM MATERIALIZED_INDEX i WHERE Name=?", "InstallerStartDateIndex").First == null) {
            //    Starcounter.Db.SQL("CREATE INDEX InstallerStartDateIndex ON InstallerStart (\"Date\")");
            //}
            #endregion

            #region StarcounterUsage
            if (Starcounter.Db.SQL("SELECT i FROM MATERIALIZED_INDEX i WHERE Name=?", "StarcounterUsageIndex").First == null) {
                Starcounter.Db.SQL("CREATE INDEX StarcounterUsageIndex ON StarcounterUsage (Installation)");
            }

            //if (Starcounter.Db.SQL("SELECT i FROM MATERIALIZED_INDEX i WHERE Name=?", "StarcounterUsageDateIndex").First == null) {
            //    Starcounter.Db.SQL("CREATE INDEX StarcounterUsageDateIndex ON StarcounterUsage (\"Date\")");
            //}
            #endregion

            #region StarcounterGeneral
            if (Starcounter.Db.SQL("SELECT i FROM MATERIALIZED_INDEX i WHERE Name=?", "StarcounterGeneralIndex").First == null) {
                Starcounter.Db.SQL("CREATE INDEX StarcounterGeneralIndex ON StarcounterGeneral (Installation)");
            }

            //if (Starcounter.Db.SQL("SELECT i FROM MATERIALIZED_INDEX i WHERE Name=?", "StarcounterGeneralDateIndex").First == null) {
            //    Starcounter.Db.SQL("CREATE INDEX StarcounterGeneralDateIndex ON StarcounterGeneral (\"Date\")");
            //}
            #endregion

            #region ErrorReport
            if (Starcounter.Db.SQL("SELECT i FROM MATERIALIZED_INDEX i WHERE Name=?", "ErrorReportIndex").First == null) {
                Starcounter.Db.SQL("CREATE INDEX ErrorReportIndex ON ErrorReport (Installation)");
            }

            #endregion

            #region VersionBuild
            if (Starcounter.Db.SQL("SELECT i FROM MATERIALIZED_INDEX i WHERE Name=?", "VersionBuildVersionIndex").First == null) {
                Starcounter.Db.SQL("CREATE INDEX VersionBuildVersionIndex ON VersionBuild (Version)");
            }
            if (Starcounter.Db.SQL("SELECT i FROM MATERIALIZED_INDEX i WHERE Name=?", "VersionBuildChannelIndex").First == null) {
                Starcounter.Db.SQL("CREATE INDEX VersionBuildChannelIndex ON VersionBuild (Channel)");
            }
            if (Starcounter.Db.SQL("SELECT i FROM MATERIALIZED_INDEX i WHERE Name=?", "VersionBuildChannelVersionIndex").First == null) {
                Starcounter.Db.SQL("CREATE INDEX VersionBuildChannelVersionIndex ON VersionBuild (Channel, Version)");
            }
            if (Starcounter.Db.SQL("SELECT i FROM MATERIALIZED_INDEX i WHERE Name=?", "VersionBuildEditionChannelVersionIndex").First == null) {
                Starcounter.Db.SQL("CREATE INDEX VersionBuildEditionChannelVersionIndex ON VersionBuild (Edition, Channel, Version)");
            }

            //if (Starcounter.Db.SQL("SELECT i FROM MATERIALIZED_INDEX i WHERE Name=?", "VersionBuildDLIndex").First == null) {
            //    Starcounter.Db.SQL("CREATE INDEX VersionBuildDLIndex ON VersionBuild (HasBeenDownloaded)");
            //}

            //if (Starcounter.Db.SQL("SELECT i FROM MATERIALIZED_INDEX i WHERE Name=?", "VersionBuildChannelVersionDLIndex").First == null) {
            //    Starcounter.Db.SQL("CREATE INDEX VersionBuildChannelVersionDLIndex ON VersionBuild (Channel, Version, HasBeenDownloaded)");
            //}

            if (Starcounter.Db.SQL("SELECT i FROM MATERIALIZED_INDEX i WHERE Name=?", "VersionBuildSerialIndex").First == null) {
                Starcounter.Db.SQL("CREATE INDEX VersionBuildSerialIndex ON VersionBuild (Serial)");
            }



            #endregion

            #region VersionSource
            if (Starcounter.Db.SQL("SELECT i FROM MATERIALIZED_INDEX i WHERE Name=?", "VersionSourceEditionChannelIsAvailableIndex").First == null) {
                Starcounter.Db.SQL("CREATE INDEX VersionSourceEditionChannelIsAvailableIndex ON VersionSource (Edition,Channel,IsAvailable)");
            }

            if (Starcounter.Db.SQL("SELECT i FROM MATERIALIZED_INDEX i WHERE Name=?", "VersionSourceBuildErrorChannelIndex").First == null) {
                Starcounter.Db.SQL("CREATE INDEX VersionSourceBuildErrorChannelIndex ON VersionSource (BuildError,Channel)");
            }

            if (Starcounter.Db.SQL("SELECT i FROM MATERIALIZED_INDEX i WHERE Name=?", "VersionSourceBuildErrorIndex").First == null) {
                Starcounter.Db.SQL("CREATE INDEX VersionSourceBuildErrorIndex ON VersionSource (BuildError)");
            }

            if (Starcounter.Db.SQL("SELECT i FROM MATERIALIZED_INDEX i WHERE Name=?", "VersionSourceVersionIndex").First == null) {
                Starcounter.Db.SQL("CREATE INDEX VersionSourceVersionIndex ON VersionSource (Version)");
            }

            #endregion

            #region IPLocation
            if (Starcounter.Db.SQL("SELECT i FROM MATERIALIZED_INDEX i WHERE Name=?", "IPLocationIPIndex").First == null) {
                Starcounter.Db.SQL("CREATE INDEX IPLocationIPIndex ON IPLocation (IPAdress)");
            }

            #endregion

        }

        /// <summary>
        /// Assure that we have an location for an ipadress
        /// </summary>
        /// <param name="ipAddress"></param>
        public static void AssureIPLocation(string ipAddress) {
            // http://freegeoip.net/

            IPLocation ipLocation = Db.SlowSQL<IPLocation>("SELECT o FROM IPLocation o WHERE o.IPAdress=?", ipAddress).First;
            if (ipLocation != null) {
                // We already got an location
                return;
            }

            try {

                // Service: http://freegeoip.net/  
                // API usage is limited to 10,000 queries per hour. 
                Node node = new Node("freegeoip.net", 80);

                string uri = string.Format("/json/{0}", ipAddress);

                Response response = node.GET(uri, null);
                // Example call: http://freegeoip.net/json/85.89.70.180
                // Example response
                //{
                //  "ip":"85.89.70.180",
                //  "country_code":"SE",
                //  "country_name":"Sweden",
                //  "region_code":"10",
                //  "region_name":"Dalarnas Lan",
                //  "city":"Falun",
                //  "zipcode":"",
                //  "latitude":60.6,
                //  "longitude":15.6333,
                //  "metro_code":"",
                //  "areacode":""
                //}

                if (response.StatusCode == (ushort)System.Net.HttpStatusCode.NotFound) {
                    // TODO: Try another service?
                    LogWriter.WriteLine(string.Format("NOTICE: Failed to lookup ip {0}. {1}", ipAddress, response.Body));
                    return;
                }
                else if (response.StatusCode == (ushort)System.Net.HttpStatusCode.Forbidden) {
                    LogWriter.WriteLine(string.Format("WARNING: Reached the limit of ip lookups. {0}", response.Body));
                    return;
                }

                dynamic incomingJson = DynamicJson.Parse(response.Body);

                Db.Transaction(() => {
                    IPLocation locaction = new IPLocation();
                    if (incomingJson.IsDefined("ip")) {
                        locaction.IPAdress = incomingJson.ip;
                    }
                    if (incomingJson.IsDefined("country_code")) {
                        locaction.CountryCode = incomingJson.country_code;
                    }
                    if (incomingJson.IsDefined("country_name")) {
                        locaction.CountryName = incomingJson.country_name;
                    }
                    if (incomingJson.IsDefined("region_code")) {
                        locaction.RegionCode = incomingJson.region_code;
                    }
                    if (incomingJson.IsDefined("region_name")) {
                        locaction.RegionName = incomingJson.region_name;
                    }
                    if (incomingJson.IsDefined("city")) {
                        locaction.City = incomingJson.city;
                    }
                    if (incomingJson.IsDefined("zipcode")) {
                        locaction.ZipCode = incomingJson.zipcode;
                    }
                    if (incomingJson.IsDefined("latitude")) {
                        locaction.Latitude = new decimal(incomingJson.latitude);
                    }
                    if (incomingJson.IsDefined("longitude")) {
                        locaction.Longitude = new decimal(incomingJson.longitude);
                    }
                    if (incomingJson.IsDefined("metro_code")) {
                        locaction.MetroCode = incomingJson.metro_code;
                    }
                    if (incomingJson.IsDefined("areacode")) {
                        locaction.AreaCode = incomingJson.areacode;
                    }
                });
            }
            catch (Exception e) {
                LogWriter.WriteLine(string.Format("ERROR: IP Lookup {0}. {1}", ipAddress, e.Message));
            }

        }


        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// TODO: This was a quick fix solution that we needed immediately
        /// In the future we need a more flexible solution. 
        /// Maybe a auto blacklist with timeouts for ipadresses that is "hammering" our services
        /// </remarks>
        /// <param name="ipAdress"></param>
        /// <returns></returns>
        public static bool IsBlacklisted(string ipAdress) {

            // TODO: 
            //            if ("58.27.115.118" == ipAdress) return true;
            return false;

        }

    }
}
