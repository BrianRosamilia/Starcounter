using Codeplex.Data;
using Starcounter;
using Starcounter.Advanced;
using StarcounterApplicationWebSocket.VersionHandler;
using StarcounterApplicationWebSocket.VersionHandler.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Starcounter.Applications.UsageTrackerApp.VersionHandler {
    /// <summary>
    /// Sync data between filsystem and database
    /// This is used as a recovery option if files
    /// where moved/deleted and added manually
    /// </summary>
    internal class SyncData {


        internal static void Start() {

            // Check if Database Packages exist in 'Settings.UploadFolder'
            // NOTE: It's important that this is called before .FixUnhandledPackages();
            RemoveInvalidSourceItems();

            // Check if uploaded packages in 'Settings.UploadFolder' exist as Database.VersionPackage instances
            FixUnhandledPackages();

            // Check if unpacked package source 'Settings.SourceFolder' exist in Database.Source class
            FixMissingSources();

            // Connect VersionBuilds with missing Source property.
            ConnectVersionBuildWithMissingSource();

            // Check if there is any documentation that is not referenced in the database
            FixMissingDocumentation();

            // Move unmoved documentations
            MoveMissingDocumentations();

            // Check if versions in 'Settings.VersionFolder' exist in Database.Version class
            CleanupBuildFolder();

            // Check if Database.Version exist in 'Settings.VersionFolder'
            CleanupBuilds();

            // Previously failed builds will be rebuild if necessary
            UnmarkFailedBuilds();

            // Assure that we have an location for ipadresses
            AssureDownloadsIPLocation();

        }

        /// <summary>
        /// Remove invalid source items
        /// There is no package to unpack and there is no source folder specified
        /// </summary>
        private static void RemoveInvalidSourceItems() {

            Db.Transaction(() => {

                var result = Db.SlowSQL<VersionSource>("SELECT o FROM VersionSource o");
                foreach (VersionSource source in result) {

                    if (!string.IsNullOrEmpty(source.PackageFile) && !File.Exists(source.PackageFile)) {
                        // Package file is missing
                        source.PackageFile = null;
                        LogWriter.WriteLine(string.Format("NOTICE: Removing missing package {0} from database.", source.Version));
                    }

                    if (!string.IsNullOrEmpty(source.DocumentationFolder) && !Directory.Exists(source.DocumentationFolder)) {
                        // Documentation folder is missing
                        source.DocumentationFolder = null;
                        LogWriter.WriteLine(string.Format("NOTICE: Removing missing documentation {0} from database.", source.DocumentationFolder));
                    }

                    if (!string.IsNullOrEmpty(source.SourceFolder) && !Directory.Exists(source.SourceFolder)) {
                        // Source folder is missing
                        source.SourceFolder = null;
                        LogWriter.WriteLine(string.Format("NOTICE: Removing missing source {0} from database.", source.Version));
                    }

                    if (string.IsNullOrEmpty(source.PackageFile) && string.IsNullOrEmpty(source.SourceFolder)) {
                        // No Package file specifed and there is no sourcefolder specified
                        string version = source.Version;
                        source.Delete();

                        LogWriter.WriteLine(string.Format("WARNING: Source item {0} was removed from database, package and sourcefolder was not specified.", version));
                    }
                }
            });

            // TODO: Remove duplicates

        }


        /// <summary>
        /// Check if packages in the filesystem is registered in the database
        /// If package is missing it will be added to the database if it's a vaild uploaded package
        /// and not a duplicate
        /// </summary>
        /// <remarks>
        /// A packages is a zipped file.
        /// </remarks>
        private static void FixUnhandledPackages() {

            VersionHandlerSettings settings = VersionHandlerApp.Settings;

            if (!Directory.Exists(settings.UploadFolder)) {
                return;
            }

            string[] files = Directory.GetFiles(settings.UploadFolder);
            foreach (string file in files) {

                VersionSource versionSource = Db.SlowSQL<VersionSource>("SELECT o FROM VersionSource o WHERE PackageFile=?", file).First;
                if (versionSource == null) {
                    // Package file is not registred in database
                    string message;
                    try {
                        // Register package
                        bool result = PackageHandler.AddFileEntryToDatabase(file, out message);
                        if (result == false) {
                            LogWriter.WriteLine(string.Format("NOTICE: Failed to register package {0}. {1}.", file, message));
                        }
                    }
                    catch (Exception e) {
                        LogWriter.WriteLine(string.Format("ERROR: Failed to handle package {0}. {1}.", file, e.Message));
                    }
                }

            }
        }


        /// <summary>
        /// Check for missing source items.
        /// </summary>
        private static void FixMissingSources() {

            VersionHandlerSettings settings = VersionHandlerApp.Settings;

            if (!Directory.Exists(settings.SourceFolder)) {
                return;
            }

            string[] channels = Directory.GetDirectories(settings.SourceFolder);

            foreach (string channel in channels) {

                string[] versionSources = Directory.GetDirectories(channel);

                foreach (string versionSourceFolder in versionSources) {

                    string versionInfo_version;
                    string versionInfo_channel;
                    string versionInfo_edition;
                    string versionInfo_builderTool = string.Empty;
                    DateTime versionInfo_versionDate;

                    String versionInfoFile = Path.Combine(versionSourceFolder, "VersionInfo.xml");
                    if (!File.Exists(versionInfoFile)) {
                        LogWriter.WriteLine(string.Format("ERROR: Invalid source {0} folder. Missing VersionInfo.xml.", versionSourceFolder));
                        continue;
                    }

                    using (FileStream fs = File.OpenRead(versionInfoFile)) {
                        // Read version info
                        PackageHandler.ReadInfoVersionInfo(fs, out versionInfo_version, out versionInfo_channel, out versionInfo_edition, out versionInfo_versionDate);
                        if (versionInfo_versionDate == DateTime.MinValue) {
                            versionInfo_versionDate = File.GetCreationTimeUtc(versionInfoFile);
                            LogWriter.WriteLine(string.Format("WARNING: The VersionDate tag is missing from VersionInfo.xml in source folder {0}. The File date will be used instead.", versionSourceFolder));
                        }

                        if (File.Exists(Path.Combine(versionSourceFolder, "GenerateInstaller.exe"))) {
                            versionInfo_builderTool = "GenerateInstaller.exe";
                        }

                        // Validate version info
                        bool valid = versionInfo_version != string.Empty && versionInfo_channel != string.Empty && versionInfo_edition != string.Empty && versionInfo_builderTool != string.Empty && versionInfo_versionDate != DateTime.MinValue;

                        if (valid == false) {
                            LogWriter.WriteLine(string.Format("ERROR: Invalid VersionInfo.xml content and/or missing building tool in source {0}", versionSourceFolder));
                            continue;
                        }
                    }

                    VersionSource source = Db.SlowSQL<VersionSource>("SELECT o FROM VersionSource o WHERE o.Version=?", versionInfo_version).First;
                    if (source == null) {

                        Db.Transaction(() => {
                            DirectoryInfo channelFolder = new DirectoryInfo(channel);
                            VersionSource versionSource = new VersionSource();
                            versionSource.SourceFolder = versionSourceFolder;
                            versionSource.PackageFile = null;
                            versionSource.Channel = versionInfo_channel;
                            versionSource.Version = versionInfo_version;
                            versionSource.Edition = versionInfo_edition;
                            versionSource.BuildError = false;
                            versionSource.VersionDate = versionInfo_versionDate;
                        });

                        LogWriter.WriteLine(string.Format("NOTICE: Missing source {0} was added to database.", versionInfo_version));
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private static void ConnectVersionBuildWithMissingSource() {

            var result = Db.SlowSQL<VersionBuild>("SELECT o FROM VersionBuild o WHERE o.Source IS NULL");

            foreach (VersionBuild versionBuild in result) {

                // Find Source
                VersionSource versionSource = Db.SlowSQL<VersionSource>("SELECT o FROM VersionSource o WHERE o.Edition=? AND o.Channel=? AND o.Version=?", versionBuild.Edition, versionBuild.Channel, versionBuild.Version).First;
                if (versionSource == null) {
                    // Version source has been removed.
                    // The build will still exist if it has been downloaded.
                    if (versionBuild.HasBeenDownloaded == false) {
                        LogWriter.WriteLine(string.Format("WARNING: The build {0} has not been downloaded and the source is missing. Please restart to trigger the cleanup process.", versionBuild.File));
                    }
                    continue;
                }

                Db.Transaction(() => {
                    versionBuild.Source = versionSource;
                });


            }


        }


        /// <summary>
        /// Search for moved documentations that is not registered in the database
        /// </summary>
        private static void FixMissingDocumentation() {

            VersionHandlerSettings settings = VersionHandlerApp.Settings;

            string[] docs = Directory.GetDirectories(settings.DocumentationFolder);
            // docs = "public" "internal"
            foreach (string doc in docs) {

                string[] editions = Directory.GetDirectories(doc);

                foreach (string editionFolder in editions) {
                    string[] channels = Directory.GetDirectories(editionFolder);

                    foreach (string channelFolder in channels) {

                        string[] versions = Directory.GetDirectories(channelFolder);
                        foreach (string versionFolder in versions) {

                            // Check and see if its referenced by the database.
                            VersionSource versionSource = Db.SlowSQL<VersionSource>("SELECT o FROM VersionSource o WHERE o.DocumentationFolder=?", versionFolder).First;
                            if (versionSource != null) {
                                continue;
                            }

                            LogWriter.WriteLine(string.Format("WARNING: Documentation folder {0} is missing in the database.", versionFolder));

                            string edition = new DirectoryInfo(editionFolder).Name;
                            string channel = new DirectoryInfo(channelFolder).Name;
                            string version = new DirectoryInfo(versionFolder).Name;

                            versionSource = Db.SlowSQL<VersionSource>("SELECT o FROM VersionSource o WHERE o.Edition=? AND o.Channel=? AND o.Version=?", edition, channel, version).First;
                            if (versionSource == null) {

                                try {
                                    Directory.Delete(versionFolder, true);
                                    LogWriter.WriteLine(string.Format("NOTICE: Documentation folder {0} was deleted.", versionFolder));
                                }
                                catch (Exception e) {
                                    LogWriter.WriteLine(string.Format("ERROR: Failed to delete unreferenced documentation folder {0}. {1}", versionFolder, e.Message));
                                }
                            }
                            else {

                                Db.Transaction(() => {
                                    versionSource.DocumentationFolder = versionFolder;
                                    LogWriter.WriteLine(string.Format("NOTICE: Missing documentation folder {0} was added to database.", versionFolder));
                                });

                            }
                        }  // End versions loop

                    } // End channels loop

                } // end editions loop

            }

        }


        /// <summary>
        /// 
        /// </summary>
        private static void MoveMissingDocumentations() {

            var result = Db.SlowSQL<VersionSource>("SELECT o FROM VersionSource o");
            foreach (VersionSource versionSource in result) {

                if (!string.IsNullOrEmpty(versionSource.DocumentationFolder) && Directory.Exists(versionSource.DocumentationFolder)) {
                    // Documentation already moved
                    continue;
                }
                PackageHandler.MoveDocumentation(versionSource);
            }
        }


        /// <summary>
        /// Cleanup build folder
        /// Downloaded files and files with no reference to database is removed
        /// </summary>
        private static void CleanupBuildFolder() {

            VersionHandlerSettings settings = VersionHandlerApp.Settings;
            bool bRemoveBuildFolder = false;
            if (!Directory.Exists(settings.VersionFolder)) {
                return;
            }

            string[] editions = Directory.GetDirectories(settings.VersionFolder);
            foreach (string edition in editions) {

                string[] channels = Directory.GetDirectories(edition);

                foreach (string channel in channels) {

                    string[] versions = Directory.GetDirectories(channel);
                    DirectoryInfo channelFolder = new DirectoryInfo(channel);

                    foreach (string version in versions) {

                        DirectoryInfo versionFolder = new DirectoryInfo(version);

                        string[] builds = Directory.GetDirectories(version);

                        foreach (string buildFolder in builds) {

                            bRemoveBuildFolder = false;
                            string[] files = Directory.GetFiles(buildFolder);
                            if (files == null || files.Length == 0) {
                                bRemoveBuildFolder = true;
                            }
                            else {
                                string file = files[0];
                                VersionBuild versionBuild = Db.SlowSQL<VersionBuild>("SELECT o FROM VersionBuild o WHERE o.File=?", file).First;
                                if (versionBuild == null) {
                                    bRemoveBuildFolder = true;
                                }
                                else {

                                    VersionSource versionSource = Db.SlowSQL<VersionSource>("SELECT o FROM VersionSource o WHERE o.Edition=? AND o.Channel=? AND o.Version=?", versionBuild.Edition, versionBuild.Channel, versionBuild.Version).First;
                                    if (versionSource == null) {
                                        // No source
                                        bRemoveBuildFolder = true;
                                    }
                                    else if (versionBuild.HasBeenDownloaded == true) {
                                        // Also remove downloaded builds to cleanup
                                        bRemoveBuildFolder = true;
                                    }
                                }
                            }

                            // Delete folder
                            if (bRemoveBuildFolder) {
                                try {
                                    // Cleanup
                                    if (Directory.Exists(buildFolder)) {
                                        Directory.Delete(buildFolder, true);
                                        LogWriter.WriteLine(string.Format("NOTICE: Build {0} was removed, File was downloaded or there was no reference to it.", buildFolder));
                                    }
                                }
                                catch (Exception e) {
                                    LogWriter.WriteLine(string.Format("ERROR: Failed to cleanup build folder {0}. {1}.", buildFolder, e.Message));
                                }
                            }
                        } // end builds loop

                    }  // end versions loop

                } // end channels loop

            } // end editions loop


            // Cleanup empty folders
            foreach (string edition in editions) {

                string[] channels = Directory.GetDirectories(edition);

                foreach (string channel in channels) {

                    string[] versions = Directory.GetDirectories(channel);
                    DirectoryInfo channelFolder = new DirectoryInfo(channel);

                    foreach (string versionFolder in versions) {

                        try {
                            if (Directory.Exists(versionFolder)) {
                                if (StarcounterApplicationWebSocket.API.Versions.Utils.IsDirectoryEmpty(versionFolder)) {
                                    Directory.Delete(versionFolder);
                                }
                            }
                        }
                        catch (Exception e) {
                            LogWriter.WriteLine(string.Format("ERROR: Failed to cleanup empty folder {0}. {1}.", versionFolder, e.Message));
                        }
                    }
                } // end channels loop

            } // end editions loop

        }


        /// <summary>
        /// Remove build's that dosent have a source (we can not build more)
        /// and remove build where there is no reference to an existing file
        /// </summary>
        private static void CleanupBuilds() {

            // Remove all builds that dosent have a source and has not been downloaded
            // TODO: o.Source is always null
            Db.Transaction(() => {
                var result = Db.SlowSQL("SELECT o FROM VersionBuild o WHERE o.Source is null AND o.HasBeenDownloaded=?", false);
                foreach (VersionBuild build in result) {
                    string file = build.File;
                    build.Delete();
                    LogWriter.WriteLine(string.Format("NOTICE: Removed build {0} from database, Build have not a source reference.", file));
                }
            });

            Db.Transaction(() => {

                var result = Db.SlowSQL("SELECT o FROM VersionBuild o");
                foreach (VersionBuild build in result) {

                    if (!File.Exists(build.File)) {

                        // Do not delete downloaded build, we like to track them
                        if (!build.HasBeenDownloaded) {
                            string file = build.File;
                            build.Delete();
                            LogWriter.WriteLine(string.Format("NOTICE: Removed build {0} from database, File was missing.", file));
                        }
                    }
                }
            });

        }


        /// <summary>
        /// Reset builds that faild so it will try to build again
        /// </summary>
        private static void UnmarkFailedBuilds() {

            // TODO: Have a build retry counter. 

            var result = Db.SlowSQL("SELECT o FROM VersionSource o WHERE o.BuildError=?", true);
            Db.Transaction(() => {
                foreach (VersionSource source in result) {
                    source.BuildError = false;
                    LogWriter.WriteLine(string.Format("NOTICE: Resetted source {0} build error flag.", source.Version));
                }
            });

        }


        /// <summary>
        /// Assure that we have an location for ipadresses
        /// </summary>
        private static void AssureDownloadsIPLocation() {

            var result = Db.SlowSQL<VersionBuild>("SELECT o FROM VersionBuild o WHERE o.IPAdress IS NOT NULL");

            foreach (VersionBuild versionBuild in result) {
                Utils.AssureIPLocation(versionBuild.IPAdress);
            }
        }


    }
}
