using Starcounter;
using StarcounterApplicationWebSocket.VersionHandler;
using StarcounterApplicationWebSocket.VersionHandler.Model;
using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Xml;

namespace Starcounter.Applications.UsageTrackerApp.VersionHandler {
    internal class PackageHandler {

        /// <summary>
        /// Unpack file
        /// </summary>
        /// <param name="file">Fullpath to package</param>
        /// <param name="destination">Destination where to unpack</param>
        /// <returns></returns>
        internal static bool Unpack(string file, string destination) {

            // Unpack package
            try {
                LogWriter.WriteLine(string.Format("NOTICE: Unpacking package {0} to {1}.", file, destination));
                ZipFile.ExtractToDirectory(file, destination);
                LogWriter.WriteLine(string.Format("NOTICE: Successfully unpacked package {0} to {1}.", file, destination));
                return true;
            }
            catch (Exception e) {
                LogWriter.WriteLine(string.Format("ERROR: Failed to unpack package {0} to folder {1}. {2}.", file, destination, e.Message));
                try {
                    // Cleanup destination folder
                    if (Directory.Exists(destination)) {
                        Directory.Delete(destination, true);
                    }
                    return false;
                }
                catch (Exception ee) {
                    LogWriter.WriteLine(string.Format("ERROR: Failed to cleanup destination folder {0}. {1}.", destination, ee.Message));
                    return false;
                }
            }

        }


        /// <summary>
        /// Read packaged information
        /// </summary>
        /// <param name="file"></param>
        /// <param name="version"></param>
        /// <param name="channel"></param>
        /// <param name="sourceUTCDate"></param>
        /// <param name="builderTool"></param>
        /// <returns></returns>
        internal static bool ReadPackageInfo(string file, out string version, out string channel, out string edition, out DateTime sourceUTCDate, out string builderTool) {

            version = string.Empty;
            channel = string.Empty;
            edition = string.Empty;
            sourceUTCDate = DateTime.MinValue;
            builderTool = string.Empty;

            using (ZipArchive archive = ZipFile.OpenRead(file)) {
                foreach (ZipArchiveEntry entry in archive.Entries) {
                    if (entry.FullName.Equals("versioninfo.xml", StringComparison.OrdinalIgnoreCase)) {
                        Stream s = entry.Open();

                        ReadInfoVersionInfo(s, out version, out channel, out edition, out sourceUTCDate);

                        if (sourceUTCDate == DateTime.MinValue) {
                            sourceUTCDate = entry.LastWriteTime.UtcDateTime;
                            LogWriter.WriteLine(string.Format("WARNING: VersionDate tag is missing from VersionInfo.xml in package {0}. Using the file date as version date.", file));
                        }

                        s.Close();
                        s.Dispose();
                    }
                    else if (entry.FullName.Equals("GenerateInstaller.exe", StringComparison.OrdinalIgnoreCase)) {
                        builderTool = "GenerateInstaller.exe";
                    }

                }
            }

            return version != string.Empty && channel != string.Empty && edition != string.Empty && builderTool != string.Empty && sourceUTCDate != DateTime.MinValue;
        }


        /// <summary>
        /// Read out version information
        /// </summary>
        /// <param name="s"></param>
        /// <param name="version"></param>
        /// <param name="channel"></param>
        /// <param name="edition"></param>
        /// <param name="sourceUTCDate"></param>
        internal static void ReadInfoVersionInfo(Stream s, out string version, out string channel, out string edition, out DateTime sourceUTCDate) {

            version = string.Empty;
            channel = string.Empty;
            edition = string.Empty;
            sourceUTCDate = DateTime.MinValue;

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(s);

            XmlNodeList versionInfo = xmlDoc.GetElementsByTagName("VersionInfo");
            if (versionInfo != null && versionInfo.Count > 0) {
                XmlNodeList nodeList = versionInfo[0].SelectNodes("Version");
                if (nodeList != null && nodeList.Count > 0) {
                    version = nodeList[0].InnerText;
                }
                nodeList = versionInfo[0].SelectNodes("Channel");
                if (nodeList != null && nodeList.Count > 0) {
                    channel = nodeList[0].InnerText;
                }
                nodeList = versionInfo[0].SelectNodes("Edition");
                if (nodeList != null && nodeList.Count > 0) {
                    edition = nodeList[0].InnerText;
                }

                nodeList = versionInfo[0].SelectNodes("VersionDate");
                if (nodeList != null && nodeList.Count > 0) {
                    //DateTime.TryParse(nodeList[0].InnerText, out sourceUTCDate);
                    DateTime.TryParse(nodeList[0].InnerText, CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal, out sourceUTCDate);
                }

            }

        }


        /// <summary>
        /// Validate and Add the file entry to database
        /// </summary>
        /// <remarks>This adds a VersionSource instance to the database</remarks>
        /// <param name="file">Zipped package source file</param>
        /// <param name="message"></param>
        /// <returns>True if succesfull otherwise false</returns>
        internal static bool AddFileEntryToDatabase(string file, out string message) {

            message = string.Empty;

            try {

                string version = string.Empty;
                string channel = string.Empty;
                string edition = string.Empty;
                string builderTool = string.Empty;
                DateTime sourceUTCDate = DateTime.MinValue;

                if (!File.Exists(file)) {
                    throw new FileNotFoundException("Can not find the file", file);
                }

                // Validate the file.
                bool result = PackageHandler.ReadPackageInfo(file, out version, out channel, out edition, out sourceUTCDate, out builderTool);

                if (result == false) {
                    message = "Invalid package content.";

                    if (version == string.Empty) {
                        message += message + " Missing <Version> tag information.";
                    }
                    if (channel == string.Empty) {
                        message += message + " Missing <Channel> tag information.";
                    }
                    if (edition == string.Empty) {
                        message += message + " Missing <Edition> tag information.";
                    }
                    if (sourceUTCDate == DateTime.MinValue) {
                        message += message + " Failed to read source datetime on VersionInfo.xml.";
                    }
                    if (builderTool == string.Empty) {
                        message += message + " Missing builder tool GenerateInstaller.exe";
                    }

                    // Invalid package
                    if (File.Exists(file)) {
                        try {
                            File.Delete(file);
                            LogWriter.WriteLine(string.Format("NOTICE: The invalid package {0} was deleted.", file));
                        }
                        catch (Exception e) {
                            LogWriter.WriteLine(string.Format("ERROR: Failed to delete the invalid package {0}. {1}.", file, e.Message));
                        }
                    }
                    return false;
                }

                // Check if version already exist in database
                // Note, the version number is unique between editions and channels.
                VersionSource dupCheckResult = Db.SlowSQL<VersionSource>("SELECT o FROM VersionSource o WHERE o.Version=?", version).First;
                if (dupCheckResult != null) {
                    message = string.Format("Source version {0} already exist in database", version);

                    // Already unpacked
                    if (File.Exists(file)) {
                        try {
                            File.Delete(file);
                            LogWriter.WriteLine(string.Format("NOTICE: Duplicated source, Already unpacked package {0} was deleted.", file));
                        }
                        catch (Exception e) {
                            LogWriter.WriteLine(string.Format("ERROR: Failed to delete the invalid package {0}. {1}.", file, e.Message));
                        }
                    }

                    return false;
                }

                // Save info to database
                Db.Transaction(() => {
                    VersionSource versionSource = new VersionSource();
                    versionSource.SourceFolder = null;
                    versionSource.PackageFile = file;
                    versionSource.Edition = edition;
                    versionSource.Channel = channel;
                    versionSource.Version = version;
                    versionSource.BuildError = false;
                    versionSource.VersionDate = sourceUTCDate;
                });

                LogWriter.WriteLine(string.Format("NOTICE: Package {0} was added to database.", file));

                return true;
            }
            catch (Exception e) {

                LogWriter.WriteLine(string.Format("ERROR: Unpacking {0}. {1}.", file, e.Message));

                if (File.Exists(file)) {
                    File.Delete(file);
                    LogWriter.WriteLine(string.Format("NOTICE: Package {0} was delete.", file));
                }
                throw e;
            }

        }

        /// <summary>
        /// Move Source documentation folder to configured destination
        /// </summary>
        /// <param name="item"></param>
        /// <returns>True if documentation folder was moved</returns>
        internal static bool MoveDocumentation(VersionSource item) {

            if (string.IsNullOrEmpty(item.SourceFolder)) {
                LogWriter.WriteLine(string.Format("WARNING: The package {0} has not yet been unpacked.", item.PackageFile));
                return false;
            }

            // Add '\docs\public' to source folder path
            string sourceDocsFolder = Path.Combine(item.SourceFolder, "docs");
            sourceDocsFolder = Path.Combine(sourceDocsFolder, "public");

            // Source folder path example: <usersettings>\oem\nightlybuilds\2.0.904.3

            if (!Directory.Exists(sourceDocsFolder)) {
                LogWriter.WriteLine(string.Format("WARNING: The package {0} in {1} ({2}) doesn't contain a documentation folder.", item.Version, item.Channel, item.Edition));
                return false;
            }


            // Destination documentation path

            // Add 'public' 
            string outputDocumentationFolder = Path.Combine(VersionHandlerApp.Settings.DocumentationFolder, "public");

            // Add edition name
            outputDocumentationFolder = Path.Combine(outputDocumentationFolder, item.Edition);

            // Add channel name
            outputDocumentationFolder = Path.Combine(outputDocumentationFolder, item.Channel);

            // Create directory Without the final folder otherwise Directory.Move() will complain.
            if (!Directory.Exists(outputDocumentationFolder)) {
                Directory.CreateDirectory(outputDocumentationFolder);
            }

            outputDocumentationFolder = Path.Combine(outputDocumentationFolder, item.Version);
            // Example path: <usersettings>\public\oem\nightlybuilds\2.0.904.3

            if (!Directory.Exists(sourceDocsFolder) && Directory.Exists(outputDocumentationFolder)) {
                // Doc's already moved?
                return false;
            }


            if (Directory.Exists(outputDocumentationFolder)) {
                LogWriter.WriteLine(string.Format("WARNING: Documentation folder {0} already exists.", outputDocumentationFolder));
                return false;
            }

            try {
                // Move documentation folder
                Directory.Move(sourceDocsFolder, outputDocumentationFolder);

                Db.Transaction(() => {
                    item.DocumentationFolder = outputDocumentationFolder;
                });

                LogWriter.WriteLine(string.Format("NOTICE: Documentation version {0} moved to {1}.", item.Version, outputDocumentationFolder));
                return true;
            }
            catch (Exception e) {
                LogWriter.WriteLine(string.Format("ERROR: Failed to move the documentation folder {0} to {1}. {2}", sourceDocsFolder, outputDocumentationFolder, e.Message));
                return false;
            }

        }

    }
}
