using Starcounter;
using StarcounterApplicationWebSocket.VersionHandler;
using StarcounterApplicationWebSocket.VersionHandler.Model;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Starcounter.Applications.UsageTrackerApp.VersionHandler {

    /// <summary>
    /// Buildworker take care of building missing builds for all versions
    /// </summary>
    internal class BuildWorker {

        #region Properties
        private bool _IsRunning;
        /// <summary>
        /// True if worker thread is running, otherwise false
        /// </summary>
        public bool IsRunning {
            get {
                return this._IsRunning;
            }
            private set {
                this._IsRunning = value;
            }
        }
        private bool bAbort = false;
        private AutoResetEvent waitEvent = new AutoResetEvent(false);
        #endregion


        /// <summary>
        /// Start Build worker
        /// </summary>
        public void Start() {

            if (this.IsRunning == true) throw new InvalidOperationException("Worker is already running");
            this.bAbort = false;
            ThreadPool.QueueUserWorkItem(WorkerThread);

        }


        /// <summary>
        /// Stop Build worker
        /// </summary>
        public void Stop() {
            this.bAbort = true;
        }


        /// <summary>
        /// Trigger Build worker to check for tasks 
        /// </summary>
        public void Trigger() {
            this.waitEvent.Set();
        }


        /// <summary>
        /// Build worker thread
        /// </summary>
        private void WorkerThread(object state) {

            LogWriter.WriteLine("NOTICE: Build worker started.");

            this.IsRunning = true;

            DbSession dbSession = new DbSession();

            while (true) {

                waitEvent.WaitOne();

                if (this.bAbort == true) {
                    break;
                }

                try {
                    lock (this) {
                        dbSession.RunSync(() => {
                            this.BuildMissingBuilds();
                        });
                    }
                }
                catch (Exception e) {

                    LogWriter.WriteLine(string.Format("ERROR: {0}", e.ToString()));

                }
            }

            this.IsRunning = false;
            LogWriter.WriteLine("NOTICE: Build worker ended.");

        }


        /// <summary>
        /// Build missing unique builds
        /// </summary>
        private void BuildMissingBuilds() {

            LogWriter.WriteLine("NOTICE: Checking for new versions to build.");

            var sources = Db.SlowSQL<VersionSource>("SELECT o FROM VersionSource o WHERE o.IsAvailable=?", true);

            VersionHandlerSettings settings = VersionHandlerApp.Settings;

            // Check needed builds for each versions source
            foreach (VersionSource source in sources) {

                // Validate the source folder
                if (string.IsNullOrEmpty(source.SourceFolder)) {
                    // Not yet unpacked
                    continue;
                }

                // Validate the source folder
                if (!Directory.Exists(source.SourceFolder)) {
                    LogWriter.WriteLine(string.Format("ERROR: Source folder {0} is missing. Please restart to trigger the cleanup process.", source.SourceFolder));
                    continue;
                }

                while (source.BuildError == false) {

                    // Get number of available builds
                    Int64 num = Db.SlowSQL<Int64>("SELECT count(o) FROM VersionBuild o WHERE o.HasBeenDownloaded=? AND o.Version=?", false, source.Version).First;

                    Int64 neededVersions = settings.MaximumBuilds - num;
                    if (neededVersions <= 0) {
                        // No new builds is needed
                        break;
                    }

                    // Build destination folder path (unique per build) an assure it dosent exits yet
                    // Example path: ..\stable\2.0.2345.2\xdfe4lvrlkmv
                    string destinationFolder = string.Empty;
                    for (int i = 0; i < 50; i++) {
                        destinationFolder = settings.VersionFolder;
                        destinationFolder = System.IO.Path.Combine(destinationFolder, source.Channel);
                        destinationFolder = System.IO.Path.Combine(destinationFolder, source.Version);
                        destinationFolder = System.IO.Path.Combine(destinationFolder, System.IO.Path.GetRandomFileName());
                        if (!Directory.Exists(destinationFolder)) {
                            break;
                        }
                    }

                    // Check that the folder dosent exits
                    if (Directory.Exists(destinationFolder)) {
                        LogWriter.WriteLine(string.Format("ERROR: Failed to generate a new unique destination folder {0}.", destinationFolder));
                        Db.Transaction(() => {
                            // Mark build as a faild to build
                            source.BuildError = true;
                        });
                        break;
                    }

                    string file;

                    // Get serial id
                    string serialId = GetUniqueSerialId();
                    if (serialId == null) {
                        LogWriter.WriteLine("ERROR: Failed to generate a unique serial id.");
                        Db.Transaction(() => {
                            // Mark build as a faild to build
                            source.BuildError = true;
                        });
                        break;
                    }

                    // Build
                    bool result = this.Build(source.SourceFolder, destinationFolder, serialId, settings.CertificationFile, out file);
                    if (result == false) {

                        // Build failed, mark the source as a fail to build
                        Db.Transaction(() => {
                            source.BuildError = true;
                        });

                        try {
                            // Cleanup
                            if (Directory.Exists(destinationFolder)) {
                                Directory.Delete(destinationFolder, true);
                            }
                        }
                        catch (Exception e) {
                            LogWriter.WriteLine(string.Format("ERROR: Failed to cleanup destination folder {0}. {1}.", destinationFolder, e.Message));
                        }

                    }
                    else {

                        // Successfull build
                        Db.Transaction(() => {

                            // Create VersionBuild instance
                            VersionBuild build = new VersionBuild();
                            build.BuildDate = DateTime.UtcNow;
                            build.File = file;
                            build.Serial = serialId;
                            build.DownloadDate = DateTime.MinValue;
                            build.Version = source.Version;
                            build.Channel = source.Channel;
                            build.Source = source;
                        });
                    }
                }
            }

            LogWriter.WriteLine("NOTICE: Waiting to build new versions.");

        }

        /// <summary>
        /// Generate unique serial id
        /// </summary>
        /// <returns>Serial otherwise null</returns>
        private static string GetUniqueSerialId() {
            string serial;

            for (int i = 0; i < 50; i++) {
                serial = DownloadID.GenerateNewUniqueDownloadKey();
                VersionBuild result = Db.SlowSQL<VersionBuild>("SELECT o FROM VersionBuild o WHERE o.Serial=?", serial).First;
                if (result == null) {
                    return serial;
                }
            }
            return null;
        }


        /// <summary>
        /// Build version
        /// </summary>
        /// <param name="sourceFolder">Source folder</param>
        /// <param name="destinationFolder">Destination Folder</param>
        /// <param name="serialId">Unique serialid</param>
        /// <param name="certFile">Fullpath to a certificat file</param>
        /// <param name="file">Output file</param>
        /// <returns>True if successfull, otherwise false</returns>
        private bool Build(string sourceFolder, string destinationFolder, string serialId, string certFile, out string file) {

            file = string.Empty;

            // Assure destination folder
            if (!Directory.Exists(destinationFolder)) {
                Directory.CreateDirectory(destinationFolder);
            }

            string builderTool = Path.Combine(sourceFolder, "GenerateInstaller.exe");

            // Start the Building tool process
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.FileName = builderTool;
            startInfo.WindowStyle = ProcessWindowStyle.Normal;
            startInfo.RedirectStandardOutput = true;
            startInfo.Arguments = "UniqueDownloadKey=" + serialId + " " +
                "DestinationDir=" + "\"" + destinationFolder + "\"" + " " +
                "PathToCertificateFile=" + "\"" + certFile + "\"";
            startInfo.WorkingDirectory = sourceFolder;

            LogWriter.WriteLine(string.Format("NOTICE: Building {0} to {1}", serialId, destinationFolder));

            int retries = 5;
            int retryPause = 3000;
            int timeout_min = 5;

            for (int i = 0; i < retries; i++) {

                try {
                    using (Process exeProcess = Process.Start(startInfo)) {

                        string output = exeProcess.StandardOutput.ReadToEnd();
                        exeProcess.WaitForExit(timeout_min * 60 * 1000);

                        if (exeProcess.HasExited == false) {
                            LogWriter.WriteLine(output);
                            LogWriter.WriteLine(string.Format("ERROR: Failed to build within reasonable time ({0} min) (retry {1}).", timeout_min, i));
                            exeProcess.Kill();
                            Thread.Sleep(retryPause);
                            continue;
                        }

                        if (exeProcess.ExitCode != 0) {
                            LogWriter.WriteLine(output);
                            LogWriter.WriteLine(string.Format("ERROR: Failed to build, error code {0} (retry {1}).", exeProcess.ExitCode, i));
                            Thread.Sleep(retryPause);
                            continue;
                        }

                        string[] files = Directory.GetFiles(destinationFolder);
                        if (files == null || files.Length == 0) {
                            LogWriter.WriteLine(output);
                            LogWriter.WriteLine(string.Format("ERROR: Building tool did not generate an output file in destination folder {0} (retry {1}).", destinationFolder, i));
                            Thread.Sleep(retryPause);
                            continue;
                        }
                        file = files[0];

                        LogWriter.WriteLine(string.Format("NOTICE: Successfully built {0} to {1}.", serialId, destinationFolder));
                        return true;
                    }
                }
                catch (Exception e) {
                    LogWriter.WriteLine(string.Format("ERROR: Failed to start the building tool {0}. {1} (retry {2}).", builderTool, e.Message, i));
                    return false;
                }


            }

            LogWriter.WriteLine(string.Format("ERROR: Failed to build, Max retries reached (retry {0}).", retries));
            return false;
        }


    }
}
