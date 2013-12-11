using System;
using Codeplex.Data;
using Starcounter;
using Starcounter.Advanced;
using Starcounter.Server.PublicModel;
using System.Net;
using System.Diagnostics;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.IO;
using System.Timers;

namespace Starcounter.Administrator.FrontEndAPI {
    internal static partial class FrontEndAPI {

        /// <summary>
        /// List of WebSocket sessions
        /// </summary>
        static List<Session>[] WebSocketSessions = new List<Session>[Db.Environment.SchedulerCount];

        /// <summary>
        /// The Timer is used to eliminat multiple events from the FileSystemWatcher
        /// </summary>
        private static System.Timers.Timer delaySendTimer;

        public static void ServerLog_GET(ushort port) {

            #region Log (/adminapi/v1/server/log)

            Handle.GET("/api/admin/log?{?}", (string parameters, Request req) => {

                lock (LOCK) {
                    try {

                        NameValueCollection collection = System.Web.HttpUtility.ParseQueryString(parameters);

                        LogApp logApp = new LogApp();

                        #region Set Filter
                        Boolean filter_debug;
                        Boolean.TryParse(collection["debug"], out filter_debug);
                        logApp.FilterDebug = filter_debug;

                        Boolean filter_notice;
                        Boolean.TryParse(collection["notice"], out filter_notice);
                        logApp.FilterNotice = filter_notice;

                        Boolean filter_warning;
                        Boolean.TryParse(collection["warning"], out filter_warning);
                        logApp.FilterWarning = filter_warning;

                        Boolean filter_error;
                        Boolean.TryParse(collection["error"], out filter_error);
                        logApp.FilterError = filter_error;

                        string filter_sourceList = collection["source"];
                        if (!string.IsNullOrEmpty(filter_sourceList)) {
                            logApp.FilterSource = filter_sourceList;
                        }

                        #endregion

                        logApp.RefreshLogEntriesList();
                        return logApp;
                    }
                    catch (Exception e) {
                        return RestUtils.CreateErrorResponse(e);
                    }
                }
            });

            // Returns the log
            Handle.GET("/api/admin/log", (Request req) => {
                lock (LOCK) {

                    try {

                        LogApp logApp = new LogApp() { FilterDebug = false, FilterNotice = false, FilterWarning = true, FilterError = true };
                        logApp.RefreshLogEntriesList();
                        return logApp;
                    }
                    catch (Exception e) {
                        return RestUtils.CreateErrorResponse(e);
                    }
                }
            });

            #endregion

            // Handle WebSocket connections for listening on log changes
            Handle.GET("/api/admin/log/event/ws", (Request req, Session session) => {

                Byte schedId = ThreadData.Current.Scheduler.Id;
                if (!WebSocketSessions[schedId].Contains(session)) {
                    WebSocketSessions[schedId].Add(session);
                    session.SetSessionDestroyCallback((Session s) => {
                        WebSocketSessions[schedId].Remove(s);
                    });
                }

                return "0"; // 0 = no change
            });

            SetupLogListener(WebSocketSessions);
        }

        /// <summary>
        /// Setup a listener on the log file(s)
        /// </summary>
        /// <param name="WebSocketSessions"></param>
        private static void SetupLogListener(List<Session>[] WebSocketSessions) {

            DbSession dbSession = new DbSession();
            for (Byte i = 0; i < Db.Environment.SchedulerCount; i++) {
                WebSocketSessions[i] = new List<Session>();
            }

            ServerInfo serverInfo = Master.ServerInterface.GetServerInfo();

            FileSystemWatcher watcher = new FileSystemWatcher();
            watcher.Path = serverInfo.Configuration.LogDirectory;
            watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size;
            watcher.Filter = "starcounter.*.log";
            watcher.Changed += watcher_Changed;

            // Begin watching.
            watcher.EnableRaisingEvents = true;

        }

        /// <summary>
        /// Event when a log file has changed
        /// </summary>
        /// <param name="sender">FileSystemWatcher</param>
        /// <param name="e"></param>
        static void watcher_Changed(object sender, FileSystemEventArgs e) {

            // The Timer is used to eliminat multiple events from the FileSystemWatcher
            if (delaySendTimer == null) {
                delaySendTimer = new System.Timers.Timer(100);
                delaySendTimer.Elapsed += delaySendTimer_Elapsed;
            }

            if (delaySendTimer.Enabled) return;
            delaySendTimer.Enabled = true;
        }

        /// <summary>
        /// Event when the delaySendTimer has elapsed
        /// </summary>
        /// <param name="sender">Timer</param>
        /// <param name="e"></param>
        static void delaySendTimer_Elapsed(object sender, ElapsedEventArgs e) {
            sendLogChangedEvent();
            delaySendTimer.Enabled = false;
        }

        /// <summary>
        /// Send log changed event to all websocket connections
        /// </summary>
        static void sendLogChangedEvent() {

            lock (LOCK) {

                DbSession dbSession = new DbSession();

                for (Byte i = 0; i < Db.Environment.SchedulerCount; i++) {
                    Byte k = i;

                    // TODO: Avoid calling RunAsync when there is no "listeners"

                    dbSession.RunAsync(() => {

                        Byte sched = k;

                        for (Int32 m = 0; m < WebSocketSessions[sched].Count; m++) {
                            Session s = WebSocketSessions[sched][m];

                            // Checking if session is not yet dead.
                            if (s.IsAlive()) {
                                s.Push("1"); // Log has changed
                            }
                            else {
                                // Removing dead session from broadcast.
                                WebSocketSessions[sched].Remove(s);
                            }
                        }
                    }, i);
                }
            }
        }


    }
}
