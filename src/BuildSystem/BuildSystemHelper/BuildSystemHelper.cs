using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Reflection;
using System.Text.RegularExpressions;

namespace BuildSystemHelper
{
    public class BuildSystem
    {
        // Important paths constants.
        public const String MappedBuildServerFTP = "Z:"; // Resembles a mapped drive to \\scbuildserver\ftp.
        public const String BuildServerFTP = @"\\scbuildserver\ftp";

        public static readonly String BuildSystemDir = Environment.GetEnvironmentVariable(BuildSystem.BuildSystemDirEnvVar);
        public static readonly String LocalBuildsFolder = BuildSystemDir + "\\StarcounterBuilds";
        public static readonly String LocalToolsFolder = BuildSystemDir + "\\ConfigsAndTools";
        public static readonly String FtpClientExePath = LocalToolsFolder + "\\WinScp.exe";
        public static readonly String ThirtyTwoBitsComponentsDir = "32BitComponents";
        public static readonly String PublicAssembliesDir = "Public Assemblies";

        public static readonly String PublicLogDir = MappedBuildServerFTP + "\\SCDev\\BuildSystem\\Logs\\";
        public static readonly String BuildAgentLogDir = PublicLogDir + "\\" + System.Environment.MachineName;
        public static readonly String ExceptionsLogFile = BuildAgentLogDir + "\\ScBuildSystemExceptions.txt";
        public static readonly String SpecialEventsLogFile = BuildAgentLogDir + "\\ScSpecialBuildEvents.txt";

        public const String VersionXMLFileName = "VersionInfo.xml";
        public const String StopDaemonFileName = "daemon.stop";
        public const String StopObserverFileName = "observer.stop";
        public const String BuildDaemonName = "BuildsFillupDaemon";

        public const String StableBuildsName = "StableBuilds";
        public const String NightlyBuildsName = "NightlyBuilds";
        public const String CustomBuildsName = "CustomBuilds";
        public const String TestBetaName = "TestBeta";

        public const String StarcounterBinVar = "StarcounterBin";

        // FTP configurations and tool path.
        public const String StarcounterFtpConfigName = "builds@starcounter.com";

        // Temporary directory path.
        public static readonly String TempDirectory = Environment.GetEnvironmentVariable("TEMP", EnvironmentVariableTarget.Machine);

        // Path to build statistics file.
        public static readonly String BuildStatisticsFilePath = Path.Combine(Environment.GetEnvironmentVariable("TEMP", EnvironmentVariableTarget.User), "ScBuildStatistics.txt");

        // Random numbers generator.
        static Random RandomGen = new Random();

        /// <summary>
        /// Represents the name of the checkout directory environment variable.
        /// </summary>
        public const String CheckOutDirEnvVar = "SC_CHECKOUT_DIR";

        /// <summary>
        /// Generate installer environment variable.
        /// </summary>
        public const String GenerateInstallerEnvVar = "SC_GENERATE_INSTALLER";

        /// <summary>
        /// Represents the name of the checkout directory environment variable.
        /// </summary>
        public const String BuildSystemDirEnvVar = "SC_BUILD_SYSTEM_DIR";

        /// <summary>
        /// Build number env var.
        /// </summary>
        public const String BuildNumberEnvVar = "BUILD_NUMBER";

        /// <summary>
        /// Common path to default build output.
        /// </summary>
        public const String CommonDefaultBuildOutputPath = @"Level1\src\..\bin";

        /// <summary>
        /// Common Level0 include directory.
        /// </summary>
        public static readonly String CommonLevel0IncludeDir = Path.Combine("Level0", "src", "include");

        /// <summary>
        /// Common path to default build tools output.
        /// </summary>
        public const String CommonDefaultBuildToolsOutputPath = @"Level1\src\..\bsbin\Debug";

        /// <summary>
        /// Path to MsBuild tool.
        /// </summary>
        public const String MsBuildExePath = @"C:\Windows\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe";

        /// <summary>
        /// Flag to upload to external FTP.
        /// </summary>
        public const String UploadToPublicServer = "SC_UPLOAD_TO_US_FTP";

        /// <summary>
        /// Flag to pack and upload artifacts to external FTP.
        /// </summary>
        public const String PackAndUploadArtifacts = "SC_PACK_AND_UPLOAD_ARTIFACTS";

        /// <summary>
        /// Used for getting directory of currently running assembly.
        /// </summary>
        public static String GetAssemblyDir()
        {
            return Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        }

        // Partial path to temporary local FTP file.
        static String TempFTPFilePath()
        {
            Int64 randNum = RandomGen.Next() + DateTime.Now.Ticks;
            return Path.Combine(TempDirectory, "TempFTPFile_" + randNum);
        }

        /// <summary>
        /// Standard procedure to log any build process errors.
        /// </summary>
        /// <param name="exc">Triggered exception.</param>
        public static Int32 LogException(Exception generalException)
        {
            String exceptionText = DateTime.Now.ToString() + " (" + Assembly.GetEntryAssembly().Location + "): " +
                generalException.ToString() + Environment.NewLine;

            // Processing inner exception if any.
            Exception innerExc = generalException.InnerException;
            if (innerExc != null)
                exceptionText += "Inner exception:" + Environment.NewLine + innerExc.ToString() + Environment.NewLine;

            exceptionText += Environment.NewLine;

            // Outputting the exception in several places.
            Console.Write("Build utility exception:" + Environment.NewLine + exceptionText);

            // Attempting to write to log.
            for (Int32 i = 0; i < 10; i++)
            {
                try
                {
                    File.AppendAllText(ExceptionsLogFile, exceptionText);

                    // Successfully written.
                    break;
                }
                catch
                {
                    Thread.Sleep(300);
                }
            }

            return 123;
        }

        /// <summary>
        /// Standard procedure to log special build event.
        /// </summary>
        public static void LogSpecialBuildEvent(String eventString)
        {
            Console.Error.WriteLine(eventString);

            // Attempting to write to log.
            for (Int32 i = 0; i < 10; i++)
            {
                try
                {
                    File.AppendAllText(SpecialEventsLogFile, DateTime.Now.ToString() + " (" + Assembly.GetEntryAssembly().Location + "): " +
                        eventString + Environment.NewLine + Environment.NewLine);

                    // Successfully written.
                    break;
                }
                catch
                {
                    Thread.Sleep(300);
                }
            }
        }

        /// <summary>
        /// Returns True if its a personal build.
        /// </summary>
        static Nullable<Boolean> _personalBuild = null;
        public static Boolean IsPersonalBuild()
        {
            if (_personalBuild != null)
                return _personalBuild.Value;

            if (Environment.GetEnvironmentVariable("BUILD_IS_PERSONAL") != null)
            {
                _personalBuild = true;
                return true;
            }

            _personalBuild = false;
            return false;
        }

        /// <summary>
        /// Returns true if its a nightly build.
        /// </summary>
        static Nullable<Boolean> _nightlyBuild = null;
        public static Boolean IsNightlyBuild()
        {
            if (_nightlyBuild != null)
                return _nightlyBuild.Value;

            // Getting nightly build environment variable.
            Boolean nightlyEnvVar = (Environment.GetEnvironmentVariable("SC_NIGHTLY_BUILD") != null);

            // Checking if its already a personal build and not explicit nightly build.
            if (IsPersonalBuild() && (!nightlyEnvVar))
            {
                _nightlyBuild = false;
                return false;
            }

            // Checking if its a scheduled nightly build.
            if (nightlyEnvVar /*|| (DateTime.Now.Hour >= 1) && (DateTime.Now.Hour <= 6)*/)
            {
                _nightlyBuild = true;
                return true;
            }

            _nightlyBuild = false;
            return false;
        }

        /// <summary>
        /// Returns true if its a releasing build.
        /// </summary>
        static Nullable<Boolean> _releasingBuild = null;
        public static Boolean IsReleasingBuild()
        {
            if (_releasingBuild != null)
                return _releasingBuild.Value;

            // Checking if its already a personal build.
            if (IsPersonalBuild())
            {
                _releasingBuild = false;
                return false;
            }

            // Checking if its not customized Debug build.
            if (String.Compare(Environment.GetEnvironmentVariable("Configuration"), "Release", true) != 0)
            {
                _releasingBuild = false;
                return false;
            }

            // Checking if its a scheduled nightly build.
            if (Environment.GetEnvironmentVariable("SC_RELEASING_BUILD") != null)
            {
                _releasingBuild = true;
                return true;
            }

            _releasingBuild = false;
            return false;
        }

        /// <summary>
        /// Kills all disturbing processes.
        /// </summary>
        /// <param name="procNames">Names of processes.</param>
        public static void KillDisturbingProcesses(String[] procNames)
        {
            foreach (String procName in procNames)
            {
                Process[] procs = Process.GetProcessesByName(procName);
                foreach (Process proc in procs)
                {
                    // Checking that its not a current process.
                    if (proc.Id != Process.GetCurrentProcess().Id)
                    {
                        proc.Kill();
                        proc.WaitForExit();

                        Console.WriteLine("Process " + proc.ProcessName + " has been killed.");
                    }
                }
            }
        }

        /// <summary>
        /// Simulates kill_all.bat.
        /// </summary>
        public static void KillAll()
        {
            BuildSystem.KillDisturbingProcesses(
                new String[]
                    {
                        "sccode",
                        "scdata",
                        "scdblog",
                        "scnetworkgateway",
                        "scnetworkgatewayloopedtest",
                        "scipcmonitor",
						"scadminserver",
                        "scweaver",
                        "scsqlparser"
                    });
        }

        /// <summary>
        /// Prints tool welcome.
        /// </summary>
        /// <param name="toolName"></param>
        public static void PrintToolWelcome(String toolName)
        {
            Console.WriteLine("---------------------------------------------------------------");
            Console.WriteLine("Starting tool: '" + toolName + "'");
        }

        /// <summary>
        /// Checks if same executable is already running.
        /// </summary>
        public static Boolean IsSameExecutableRunning()
        {
            String thisAssemblyName = Assembly.GetEntryAssembly().GetName().Name;
            String thisAssemblyPath = Assembly.GetEntryAssembly().Location;
            Process[] runningProcs = Process.GetProcessesByName(thisAssemblyName);

            // Checking each individual process.
            if (runningProcs.Length > 0)
            {
                foreach (Process proc in runningProcs)
                {
                    // Checking if path for corresponding executables is the same and its not current process.
                    if ((Process.GetCurrentProcess().Id != proc.Id) &&
                        (String.Compare(thisAssemblyPath, proc.MainModule.FileName, StringComparison.InvariantCultureIgnoreCase) == 0))
                    {
                        Console.Error.WriteLine("Same executable is already running: " + thisAssemblyPath);
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Deletes sub directories from given common dir.
        /// </summary>
        /// <param name="commonDir"></param>
        /// <param name="subDirs"></param>
        public static void DeleteSubDirectories(String commonDir, String[] subDirs)
        {
            foreach (String sd in subDirs)
            {
                String d = Path.Combine(commonDir, sd);

                if (Directory.Exists(d))
                    Directory.Delete(d, true);
            }
        }

        /// <summary>
        /// Kills same processes if any are running.
        /// </summary>
        public static void KillSameProcesses()
        {
            String thisAssemblyName = Assembly.GetEntryAssembly().GetName().Name;
            KillDisturbingProcesses(new String[] { thisAssemblyName });
        }

        // Replaces string in file.
        public static void ReplaceStringInFile(String filePath, String origStringRegex, String replaceString)
        {
            String fileContents = File.ReadAllText(filePath);

            Match match = Regex.Match(fileContents, origStringRegex, RegexOptions.IgnoreCase);

            // Trying to find this exact string in file.
            if (!match.Success)
                throw new Exception("Can't find matching string " + origStringRegex + " in file " + filePath);

            fileContents = fileContents.Replace(match.Value, replaceString);

            File.WriteAllText(filePath, fileContents);
        }

        // Finds string in file.
        public static String FindStringInFile(String filePath, String origStringRegex)
        {
            String fileContents = File.ReadAllText(filePath);

            Match match = Regex.Match(fileContents, origStringRegex, RegexOptions.IgnoreCase);

            // Trying to find this exact string in file.
            if (!match.Success)
                throw new Exception("Can't find matching string " + origStringRegex + " in file " + filePath);

            return match.Value;
        }

        /// <summary>
        /// Helping function to copy folders recursively.
        /// </summary>
        /// <param name="source">Source folder.</param>
        /// <param name="target">Destination folder.</param>
        public static void CopyFilesRecursively(DirectoryInfo source, DirectoryInfo target)
        {
            // Traverse through all directories.
            foreach (DirectoryInfo dir in source.GetDirectories())
                CopyFilesRecursively(dir, target.CreateSubdirectory(dir.Name));

            // Traverse through all files.
            foreach (FileInfo file in source.GetFiles())
                file.CopyTo(Path.Combine(target.FullName, file.Name), true);
        }

        /// <summary>
        /// Recursively checks if directory contains certain files (any of them).
        /// </summary>
        /// <param name="dirPath">Path to a directory to check.</param>
        /// <param name="filePatterns">List of file REGEX patterns to search for.</param>
        /// <returns>Number of matched/deleted files.</returns>
        public static Int32 DirectoryContainsFilesRegex(String dirPath, String[] filePatterns, Boolean deleteFiles)
        {
            if (!Directory.Exists(dirPath))
                return 0;

            // Getting all files recursively.
            String[] allFiles = Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories);

            // Looking if any file matches the pattern.
            Int32 foundSuchFiles = 0;
            foreach (String pattern in filePatterns)
            {
                Regex rgx = new Regex(pattern, RegexOptions.IgnoreCase);
                foreach (String filePath in allFiles)
                {
                    // Getting file name from path.
                    String fileName = Path.GetFileName(filePath);

                    // Comparing with Regex.
                    if (rgx.IsMatch(fileName))
                    {
                        if (deleteFiles)
                            File.Delete(filePath);

                        foundSuchFiles++;
                    }
                }
            }

            return foundSuchFiles;
        }

        /// <summary>
        /// Sets normal security attributes for files and folders (recursively).
        /// </summary>
        public static void SetNormalDirectoryAttributes(FileSystemInfo fsi)
        {
            if (fsi == null)
                return;

            // Trying to set file attributes.
            try { fsi.Attributes = FileAttributes.Normal; }
            catch { }

            // Checking if its a directory and go into recursion if yes.
            var di = fsi as DirectoryInfo;
            if (di != null)
            {
                // Setting the folder attribute.
                try { fsi.Attributes = FileAttributes.Directory; }
                catch { }

                // Trying to obtain sub-information for the folder.
                FileSystemInfo[] fsis = null;
                try
                {
                    // Checking if directory exists.
                    if (!Directory.Exists(fsi.FullName))
                        return;

                    fsis = di.GetFileSystemInfos();
                }
                catch { }

                // Iterating through each sub-folder.
                if (fsis != null)
                {
                    foreach (var dirInfo in fsis)
                    {
                        // Go into recursion for each sub-directory/file.
                        SetNormalDirectoryAttributes(dirInfo);
                    }
                }
            }
        }

        /// <summary>
        /// Recursively copy files from one folder to shared FTP folder.
        /// </summary>
        /// <param name="sourceDir">Source directory.</param>
        /// <param name="targetDir">Target FTP directory.</param>
        public static void CopyDirToSharedFtp(String sourceDir, String targetDir)
        {
            if (!Directory.Exists(targetDir))
                Directory.CreateDirectory(targetDir);

            // Setting normal file attributes.
            SetNormalDirectoryAttributes(new DirectoryInfo(targetDir));

            // Copying files recursively.
            CopyFilesRecursively(new DirectoryInfo(sourceDir), new DirectoryInfo(targetDir));
        }

        /// <summary>
        /// Checks that all specified environment variables exist.
        /// </summary>
        /// <returns>True if all exist.</returns>
        public static Boolean AllEnvVariablesExist(String[] envVarNames)
        {
            foreach (String varName in envVarNames)
            {
                if (Environment.GetEnvironmentVariable(varName) == null)
                {
                    Console.Error.WriteLine("'" + varName + "' environment variable is not set. Quiting...");
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Executes FTP operation using WinScp FTP client.
        /// </summary>
        /// <param name="arguments">Arguments for the program.</param>
        public static void ExecuteFTPOperation(String arguments, Boolean asyncMode = false)
        {
            Int32 errCode = 1;

            // Trying several times to perform FTP operation.
            for (Int32 i = 0; i < 10; i++)
            {
                // Filling up the FTP client process info.
                ProcessStartInfo ftpClientInfo = new ProcessStartInfo();
                ftpClientInfo.FileName = FtpClientExePath;
                ftpClientInfo.Arguments = arguments;

                // Options for hiding FTP client process window.
                ftpClientInfo.UseShellExecute = true;
                ftpClientInfo.CreateNoWindow = true;
                ftpClientInfo.WindowStyle = ProcessWindowStyle.Hidden;

                // Starting the FTP client...
                Process ftpClientProc = Process.Start(ftpClientInfo);
                if (asyncMode)
                {
                    errCode = 0;
                }
                else
                {
                    ftpClientProc.WaitForExit();
                    errCode = ftpClientProc.ExitCode;
                }
                ftpClientProc.Close();

                // Breaking if success.
                if (errCode == 0)
                    break;

                Console.Error.WriteLine("   Failed performing operation with FTP. Re-trying " + i + " in 3 seconds...");
                Thread.Sleep(3000);
            }

            // Checking the error code and reporting problems if any.
            if (errCode != 0)
            {
                Console.Error.WriteLine("   Failed performing operation with FTP!");
                throw new Exception("Can't perform the following FTP operation: " + arguments);
            }

            Console.WriteLine("   Successfully finished FTP operation.");
        }

        /// <summary>
        /// Uploads specified file to FTP.
        /// </summary>
        /// <param name="localFilePath">Path to a local file.</param>
        /// <param name="targetFTPPath">Path to a file or directory(must end with slash) on FTP.</param>
        public static void UploadFileToFTP(String ftpConfigName, String localFilePath, String remoteFTPPath, Boolean asyncMode = false)
        {
            String uploadFileToFtp = "/console /command \"option batch on\" \"option confirm off\" \"open " + ftpConfigName + "\" \"put \"" +
                                     localFilePath + "\" \"" + remoteFTPPath + "\"\" \"exit\"";

            Console.WriteLine("Uploading file to FTP: " + remoteFTPPath);

            ExecuteFTPOperation(uploadFileToFtp, asyncMode);
        }

        /// <summary>
        /// Downloads the file from FTP.
        /// </summary>
        /// <param name="remoteFilePath">File path on FTP server.</param>
        /// <param name="localFilePath">Path to a local file.</param>
        public static void GetFileFromFTP(String ftpConfigName, String remoteFilePath, String localFilePath)
        {
            String getFileFromFtp = "/console /command \"option batch on\" \"option confirm off\" \"open " + ftpConfigName + "\" \"get \"" +
                                     remoteFilePath + "\" \"" + localFilePath + "\"\" \"exit\"";

            Console.WriteLine("Downloading file from FTP: " + remoteFilePath);

            ExecuteFTPOperation(getFileFromFtp);
        }

        /// <summary>
        /// Removes the file from FTP.
        /// </summary>
        /// <param name="remoteFilePath">File path on FTP server.</param>
        public static void RemoveFileFromFTP(String ftpConfigName, String remoteFilePath)
        {
            String removeFileFromFtp = "/console /command \"option batch on\" \"option confirm off\" \"open " + ftpConfigName + "\" \"rm \"" +
                                     remoteFilePath + "\"\" \"exit\"";

            Console.WriteLine("Removing file from FTP: " + remoteFilePath);

            ExecuteFTPOperation(removeFileFromFtp);
        }

        /// <summary>
        /// Gets needed file from FTP and saves it in temporary file.
        /// Grabs all temporary file data.
        /// Deletes temporary file after operation.
        /// </summary>
        /// <param name="remoteFilePath">Path to the file on FTP.</param>
        public static Byte[] GetFTPFileData(String ftpConfigName, String remoteFilePath)
        {
            String tempFTPFile = TempFTPFilePath();

            GetFileFromFTP(ftpConfigName, remoteFilePath, tempFTPFile);

            // Reading data from the temporary local file.
            Byte[] allFileBytes = File.ReadAllBytes(tempFTPFile);

            File.Delete(tempFTPFile);

            return allFileBytes;
        }

        /// <summary>
        /// Gets file from FTP and saves it in temporary file.
        /// Grabs all temporary file text.
        /// Deletes temporary file after operation.
        /// </summary>
        /// <param name="remoteFilePath">Path to the file on FTP.</param>
        public static String GetFTPFileAllText(String ftpConfigName, String remoteFilePath)
        {
            String tempFTPFile = TempFTPFilePath();

            GetFileFromFTP(ftpConfigName, remoteFilePath, tempFTPFile);

            // Reading data from the temporary local file.
            String allFileText = File.ReadAllText(tempFTPFile);

            File.Delete(tempFTPFile);

            return allFileText;
        }

        /// <summary>
        /// Gets file from FTP and saves it in temporary file.
        /// Grabs all temporary file text lines.
        /// Deletes temporary file after operation.
        /// </summary>
        /// <param name="remoteFilePath">Path to the file on FTP.</param>
        public static String[] GetFTPFileAllLines(String ftpConfigName, String remoteFilePath)
        {
            String tempFTPFile = TempFTPFilePath();

            GetFileFromFTP(ftpConfigName, remoteFilePath, tempFTPFile);

            // Reading data from the temporary local file.
            String[] allFileLines = File.ReadAllLines(tempFTPFile);

            File.Delete(tempFTPFile);

            return allFileLines;
        }

        /// <summary>
        /// Creates temporary file with needed data and uploads it to FTP server.
        /// Deletes temporary file after operation.
        /// </summary>
        /// <param name="fileBytes">File data that needs to be uploaded.</param>
        /// <param name="targetFTPFile">Target FTP file path.</param>
        public static void UploadFileDataToFTP(String ftpConfigName, Byte[] fileBytes, String targetFTPFile)
        {
            String tempFTPFile = TempFTPFilePath();

            // Writing data to a temporary local file.
            File.WriteAllBytes(tempFTPFile, fileBytes);

            try
            {
                UploadFileToFTP(ftpConfigName, tempFTPFile, targetFTPFile);
            }
            finally
            {
                File.Delete(tempFTPFile);
            }
        }

        /// <summary>
        /// Creates temporary file with needed text and uploads it to FTP server.
        /// Deletes temporary file after operation.
        /// </summary>
        /// <param name="fileBytes">File data that needs to be uploaded.</param>
        /// <param name="targetFTPFile">Target FTP file path.</param>
        public static void UploadFileTextToFTP(String ftpConfigName, String allText, String targetFTPFile)
        {
            String tempFTPFile = TempFTPFilePath();

            // Writing data to a temporary local file.
            File.WriteAllText(tempFTPFile, allText);

            try
            {
                UploadFileToFTP(ftpConfigName, tempFTPFile, targetFTPFile);
            }
            finally
            {
                File.Delete(tempFTPFile);
            }
        }

        /// <summary>
        /// Signs list of files.
        /// </summary>
        public static String SignFiles(String[] allFilesToSign, String companyName, String productName, String pathToCertificate)
        {
            Console.WriteLine("Signing files...");

            // Calling sign utility...
            ProcessStartInfo signToolInfo = new ProcessStartInfo();
            signToolInfo.FileName = @"c:\Program Files (x86)\Microsoft SDKs\Windows\v7.1A\Bin\SignTool.exe";
            if (!File.Exists(signToolInfo.FileName))
                signToolInfo.FileName = @"c:\Program Files (x86)\Microsoft SDKs\Windows\v7.0A\Bin\SignTool.exe";

            signToolInfo.RedirectStandardError = true;
            signToolInfo.RedirectStandardOutput = true;
            signToolInfo.UseShellExecute = false;

            String allFilesSpaced = "";
            foreach (String fileToSign in allFilesToSign)
            {
                // Appending to the spaced file list.
                allFilesSpaced += "\"" + fileToSign + "\" ";
            }

            signToolInfo.Arguments = "sign /sm /s MY /n \"" + companyName + "\" /d \"" + productName + "\" /v /ac \"" + pathToCertificate +
                                     "\" /t http://timestamp.verisign.com/scripts/timstamp.dll " + allFilesSpaced;

            Console.WriteLine("Signing: \"" + signToolInfo.FileName + "\" " + signToolInfo.Arguments);

            // Launch signing for this individual file.
            Process signProcess = Process.Start(signToolInfo);
            signProcess.WaitForExit(30000);

            String output = signProcess.StandardOutput.ReadToEnd();
            Console.WriteLine(output);

            // Checking if the process exited within given time interval.
            if (!signProcess.HasExited)
                return "Singing operation hangs by some reason. Must be a problem with scheduled task in Windows..";

            // Checking the exit error code.
            if (signProcess.ExitCode != 0)
                return signProcess.StandardError.ReadToEnd();

            signProcess.Close();

            Console.WriteLine("Successfully signed files...");
            return null;
        }
    }
}