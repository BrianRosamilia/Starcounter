using System;
using Starcounter;
using System.IO;


namespace StarcounterApplicationWebSocket.VersionHandler.Model {

    /// <summary>
    /// Available sources
    /// </summary>
    [Database]
    public class VersionSource {

        /// <summary>
        /// Version of the source
        /// </summary>
        public string Version;


        /// <summary>
        /// Channel of the source, example 'Stable', 'NightlyBuilds'
        /// </summary>
        public string Channel;


        /// <summary>
        /// UTC Date of the Version
        /// </summary>
        public DateTime VersionDate;


        /// <summary>
        /// Folder where the source is located
        /// </summary>
        public string SourceFolder;


        /// <summary>
        /// Folder where the documentation is located
        /// </summary>
        public string DocumentationFolder;


        /// <summary>
        /// Full path to the Version Package file
        /// </summary>
        /// <remarks>This is empty if the package has been unpacked</remarks>
        public string PackageFile;


        /// <summary>
        /// If source can not be built
        /// </summary>
        public bool BuildError;


        /// <summary>
        /// If a source is available for build
        /// </summary>
        public bool IsAvailable {
            get {
                return this.BuildError == false && string.IsNullOrEmpty(this.SourceFolder) == false;
            }
        }


        /// <summary>
        /// Get the latest version in a specific channel
        /// </summary>
        /// <param name="channel">Channel name</param>
        /// <returns>Version or null if no valid versions was found in the specified channel</returns>
        internal static VersionSource GetLatestVersion(string channel) {

            // Get latest version source
            VersionSource versionSource = Db.SlowSQL<VersionSource>("SELECT o FROM VersionSource o WHERE o.Channel=? AND o.IsAvailable=? ORDER BY o.VersionDate DESC", channel, true).First;
            if (versionSource == null) return null;

            return versionSource;
        }


        /// <summary>
        /// This will delete the Version source from filesystem and it's database reference
        /// </summary>
        /// <remarks>
        /// The documentation and package file will also be removed.
        /// </remarks>
        /// <param name="versionSource"></param>
        internal static void DeleteVersion(VersionSource versionSource) {

            VersionSource.DeleteDocumentation(versionSource);
            VersionSource.DeleteSource(versionSource);
            VersionSource.DeletePackageFile(versionSource);

            Db.Transaction(() => {
                versionSource.Delete();
            });

        }


        /// <summary>
        /// Delete documentation folder and remove it's database reference
        /// </summary>
        /// <param name="versionSource"></param>
        /// <returns>True if successfull otherwise false</returns>
        private static bool DeleteDocumentation(VersionSource versionSource) {

            // Remove documentation
            if (!string.IsNullOrEmpty(versionSource.DocumentationFolder)) {

                try {
                    if (Directory.Exists(versionSource.DocumentationFolder)) {
                        Directory.Delete(versionSource.DocumentationFolder, true);
                        //LogWriter.WriteLine(string.Format("NOTICE: Documentation folder {0} was deleted.", versionSource.DocumentationFolder));
                    }

                    Db.Transaction(() => {
                        versionSource.DocumentationFolder = null;
                    });

                    return true;
                }
                catch (Exception e) {
                    LogWriter.WriteLine(string.Format("ERROR: Failed to delete documentation folder {0}. {1}", versionSource.DocumentationFolder, e.Message));
                }

            }
            return false;
        }


        /// <summary>
        /// Delete source folder and removes it's database reference
        /// </summary>
        /// <param name="versionSource"></param>
        /// <returns>True if successfull otherwise false</returns>
        private static bool DeleteSource(VersionSource versionSource) {

            // Remove documentation
            if (!string.IsNullOrEmpty(versionSource.SourceFolder)) {

                try {
                    if (Directory.Exists(versionSource.SourceFolder)) {
                        Directory.Delete(versionSource.SourceFolder, true);
                        //LogWriter.WriteLine(string.Format("NOTICE: Source folder {0} was deleted.", versionSource.SourceFolder));
                    }

                    Db.Transaction(() => {
                        versionSource.SourceFolder = null;
                    });

                    return true;
                }
                catch (Exception e) {
                    LogWriter.WriteLine(string.Format("ERROR: Failed to delete source folder {0}. {1}", versionSource.SourceFolder, e.Message));
                }

            }
            return false;
        }


        /// <summary>
        /// Delete packagefile and remove it's database reference
        /// </summary>
        /// <returns>True if successfull otherwise false</returns>
        private static bool DeletePackageFile(VersionSource versionSource) {

            // Remove documentation
            if (!string.IsNullOrEmpty(versionSource.PackageFile)) {
                try {
                    if (File.Exists(versionSource.PackageFile)) {
                        File.Delete(versionSource.PackageFile);
                    }

                    Db.Transaction(() => {
                        versionSource.PackageFile = null;
                    });

                    return true;
                }
                catch (Exception e) {
                    LogWriter.WriteLine(string.Format("ERROR: Failed to delete package file {0}. {1}", versionSource.PackageFile, e.Message));
                }

            }
            return false;
        }

    }
}
