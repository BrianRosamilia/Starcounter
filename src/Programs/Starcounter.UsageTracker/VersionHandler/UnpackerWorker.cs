using Starcounter;
using StarcounterApplicationWebSocket.VersionHandler;
using StarcounterApplicationWebSocket.VersionHandler.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Starcounter.Applications.UsageTrackerApp.VersionHandler {

    /// <summary>
    /// Unpacker worker takes car of unpacking uploaded packages
    /// </summary>
    internal class UnpackerWorker {

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
        /// Start unpacker worker
        /// </summary>
        public void Start() {

            if (this.IsRunning == true) throw new InvalidOperationException("Worker is already running");
            this.bAbort = false;
            ThreadPool.QueueUserWorkItem(WorkerThread);

        }


        /// <summary>
        /// Stop unpacker worker
        /// </summary>
        public void Stop() {
            this.bAbort = true;
        }


        /// <summary>
        /// Trigger unpacker worker to check for tasks 
        /// </summary>
        public void Trigger() {
            this.waitEvent.Set();
        }


        /// <summary>
        /// Unpacker thread
        /// </summary>
        /// <param name="state"></param>
        private void WorkerThread(object state) {

            LogWriter.WriteLine("NOTICE: Unpacker worker started.");

            this.IsRunning = true;

            DbSession dbSession = new DbSession();

            while (true) {

                waitEvent.WaitOne();

                if (this.bAbort == true) {
                    break;
                }

                try {
                    dbSession.RunSync(() => {
                        this.UnpackUnpackedPackages();
                    });
                }
                catch (Exception e) {
                    LogWriter.WriteLine(string.Format("ERROR: {0}.", e.ToString()));
                }

            }

            this.IsRunning = false;
            LogWriter.WriteLine("NOTICE: Unpacker worker ended.");

        }


        /// <summary>
        /// Unpack unpacked uploaded packages
        /// </summary>
        private void UnpackUnpackedPackages() {

            bool bUnpacked = false;
            LogWriter.WriteLine("NOTICE: Checking for new packages to unpack.");

            QueryResultRows<VersionSource> versionSources = Db.SlowSQL<VersionSource>("SELECT o FROM VersionSource o"); // TODO: Only select items where 'PackageFile' is not empty

            foreach (VersionSource item in versionSources) {

                if (string.IsNullOrEmpty(item.PackageFile)) {
                    // Already unpacked
                    continue;
                }

                // Validate the existans of the package file
                if (!File.Exists(item.PackageFile)) {
                    LogWriter.WriteLine(string.Format("ERROR: Package file {0} is missing. Please restart to trigger the cleanup process.", item.PackageFile));
                    continue;
                }

                // Add edition to destination path
                string destination = Path.Combine(VersionHandlerApp.Settings.SourceFolder, item.Edition);

                // Add channel to destination path
                destination = Path.Combine(destination, item.Channel);

                // Add version to destination path
                destination = Path.Combine(destination, item.Version);

                // Validate destination folder (it should not exist)
                if (Directory.Exists(destination)) {
                    LogWriter.WriteLine(string.Format("ERROR: Package file {0} destination folder {1} already exists. Please restart to trigger the cleanup process.", item.PackageFile, destination));
                    continue;
                }

                // Unpack
                bool result = PackageHandler.Unpack(item.PackageFile, destination);

                if (result == true) {
                    // Success

                    try {
                        // Delete package
                        File.Delete(item.PackageFile);
                    }
                    catch (Exception e) {
                        LogWriter.WriteLine(string.Format("ERROR: Failed to delete the uploaded package file {0}, {1}.", item.PackageFile, e.Message));
                    }

                    Db.Transaction(() => {
                        // Clear packagefile
                        item.PackageFile = null;
                        item.SourceFolder = destination;
                    });

                    // Move docs
                    PackageHandler.MoveDocumentation(item);

                    bUnpacked = true;

                }
                else {
                    // Error
                    // The package file can be invalid or the sourcefolder can be blocked or similar
                }
            }

            if (bUnpacked) {

                // Cleanup Obsolete Version
                CleanUpObsoleteVersions();

                // Trigger the buildworker
                VersionHandlerApp.BuildkWorker.Trigger();
            }
        }


        /// <summary>
        /// Clean up Obsolete Versions, we only want to keep a number of versions available
        /// to save diskspace
        /// </summary>
        private void CleanUpObsoleteVersions() {

            var editionsResult = Db.SlowSQL<string>("SELECT o.Edition FROM VersionSource o WHERE o.IsAvailable=? GROUP BY o.Edition", true);
            foreach (string edition in editionsResult) {

                var channelResult = Db.SlowSQL<string>("SELECT o.Channel FROM VersionSource o WHERE o.Edition=? AND o.IsAvailable=? GROUP BY o.Channel", edition, true);
                foreach (string channel in channelResult) {
                    this.CleanUpObsoleteVersion(edition, channel);
                }
            }
        }


        /// <summary>
        /// Cleanup obsolete versions from a specific edition and channel
        /// </summary>
        /// <param name="edition"></param>
        /// <param name="channel"></param>
        private void CleanUpObsoleteVersion(string edition, string channel) {

            int sourceCount = VersionHandlerSettings.GetSettings().MaximumSourceCount;

            Int64 numVersion = Db.SlowSQL<Int64>("SELECT count(*) FROM VersionSource o WHERE o.Edition=? AND o.Channel=? AND o.IsAvailable=?", edition, channel, true).First;

            if (numVersion > sourceCount) {

                Int64 numDelete = numVersion - sourceCount;

                // Start deleting versions and syncdata
                Db.Transaction(() => {

                    // Retrive versions to delete
                    QueryResultRows<VersionSource> versionSources = Db.SlowSQL<VersionSource>("SELECT o FROM VersionSource o WHERE e.Edition=? AND o.Channel=? AND o.IsAvailable=? ORDER BY o.VersionDate FETCH FIRST ? ROWS ONLY", edition, channel, true, numDelete);

                    foreach (VersionSource versionSource in versionSources) {

                        // Delete Obsolete Version Builds
                        VersionBuild.DeleteVersionBuild(versionSource.Edition, versionSource.Channel, versionSource.Version);

                        // Delete Obsolete Version Source (including with documentation and package file)
                        string version = versionSource.Version;
                        VersionSource.DeleteVersion(versionSource);
                        LogWriter.WriteLine(string.Format("NOTICE: Obsolete Version {0} deleted.", version));
                    }
                });
            }

        }

    }
}
