using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Security.Cryptography;
using System.Web;
using System.Diagnostics;
using BuildSystemHelper;
using System.Threading;
using System.Reflection;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml;

namespace GenerateInstaller
{
    class GenerateInstaller
    {
        const String CompanyName = "Starcounter AB";
        const String ProductName = "Starcounter Components";
        public static readonly String LocalCertificateFilePath = BuildSystem.LocalToolsFolder + "\\StarcounterCertificate.cer";

        /// <summary>
        /// Generates unique build from given all-in-one directory.
        /// </summary>
        /// <param name="uniqueDownloadKey"></param>
        /// <param name="destinationDir"></param>
        /// <param name="pathToCertificateFile"></param>
        static void GenerateUniqueBuild(String uniqueDownloadKey, String destinationDir, String pathToCertificateFile)
        {
            Console.WriteLine("Starting generating unique setup EXE...");

            // Setting special environment variable for resolving project references.
            Environment.SetEnvironmentVariable("GenerateUniqueBuild", "True");

            Console.WriteLine("Replacing unique build information in sources, license, etc...");

            XmlDocument versionXML = new XmlDocument();
            versionXML.Load(BuildSystem.VersionXMLFileName);

            // NOTE: We are getting only first element.
            String version = (versionXML.GetElementsByTagName("Version"))[0].InnerText;
            String configuration = (versionXML.GetElementsByTagName("Configuration"))[0].InnerText;
            String platform = (versionXML.GetElementsByTagName("Platform"))[0].InnerText;

            // Adding required registration date.
            String requiredRegistrationDate = DateTime.Now.AddDays(60).ToString("yyyy-MM-dd").ToUpper();

            // Replacing unique build information.
            String trackerFilePath = @"Level1\src\Starcounter.Tracking\Environment.cs";
            BuildSystem.ReplaceStringInFile(trackerFilePath, @"String UniqueDownloadKey = ""[0-9, A-Z]+"";", "String UniqueDownloadKey = \"" + uniqueDownloadKey + "\";");
            BuildSystem.ReplaceStringInFile(trackerFilePath, @"DateTime RequiredRegistrationDate = DateTime\.Parse\(""[0-9\-]+""\);", "DateTime RequiredRegistrationDate = DateTime.Parse(\"" + requiredRegistrationDate + "\");");

            String level1OutputDir = @"Level1\Bin\" + configuration;
            BuildSystem.DirectoryContainsFilesRegex(level1OutputDir, new String[] { @"Starcounter.+Setup\.exe", @"Starcounter.+Setup\.pdb", @"Starcounter.+Setup\.ilk" }, true);

            // Checking if setup file already exists.
            String staticSetupFileName = "Starcounter-Setup.exe";
            String staticSetupFilePath = Path.Combine(level1OutputDir, staticSetupFileName);

            // Looking for an Installer WPF resources folder.
            String installerWpfFolder = @"Level1\src\Starcounter.Installer\Starcounter.InstallerWPF";

            // Saving the new license information.
            String licenseFilePath = Path.Combine(installerWpfFolder, "Resources\\LicenseAgreement.html");

            // Replacing license file with a new unique version.
            BuildSystem.ReplaceStringInFile(licenseFilePath, "package identified by the key \"(.*)\"",
                "package identified by the key \"" + uniqueDownloadKey + "\"");

            // Creating required registration date.
            String currentDatePlus60Days = DateTime.Now.AddDays(60).ToString("MMMM dd, yyyy").ToUpper();
            BuildSystem.ReplaceStringInFile(licenseFilePath, " no later than \"(.*)\"",
                " no later than \"" + currentDatePlus60Days + "\"");

            // Restoring fake archive file if needed.
            String archivePath = Path.Combine(installerWpfFolder, "Resources\\Archive.zip");
            String savedArchivePath = archivePath + ".saved";
            
            Console.WriteLine("Building empty managed setup EXE...");

            Console.WriteLine("Building unique Starcounter.Tracking DLL...");

            // Building tracking project.
            BuildMsbuildProject(
                @"Level1\src\Starcounter.Tracking\Starcounter.Tracking.csproj",
                configuration,
                platform);

            File.WriteAllText(archivePath, "This is an empty file...");

            Console.WriteLine("Building empty Starcounter.InstallerWPF...");

            // Building the Installer WPF project.
            BuildMsbuildProject(
                Path.Combine(installerWpfFolder, "Starcounter.InstallerWPF.csproj"),
                configuration,
                platform);

            // Signing the main Starcounter setup.
            String signingError = BuildSystem.SignFiles(new String[] { staticSetupFilePath }, CompanyName, ProductName, pathToCertificateFile);

            // Checking if there are any errors during signing process.
            if (signingError != null) {
                Console.WriteLine(signingError);
                throw new Exception("Failed to sign static Starcounter setup file...");
            }

            Console.WriteLine("Updating consolidated Archive.zip with new unique empty setup EXE and tracking DLL...");

            File.Delete(archivePath);

            // Updating newly built files in archive.
            using (FileStream zipToOpen = new FileStream(savedArchivePath, FileMode.Open))
            {
                using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Update))
                {
                    // Replacing Starcounter-Setup.exe.
                    ZipArchiveEntry e = archive.GetEntry(staticSetupFileName);
                    if (null != e)
                        e.Delete();

                    archive.CreateEntryFromFile(staticSetupFilePath, staticSetupFileName);

                    // Replacing Starcounter.Tracking.dll.
                    e = archive.GetEntry("Starcounter.Tracking.dll");
                    if (null != e)
                        e.Delete();

                    archive.CreateEntryFromFile(Path.Combine(level1OutputDir, "Starcounter.Tracking.dll"), "Starcounter.Tracking.dll");

                    // Replacing Starcounter.Tracking.pdb.
                    e = archive.GetEntry("Starcounter.Tracking.pdb");
                    if (null != e)
                        e.Delete();

                    archive.CreateEntryFromFile(Path.Combine(level1OutputDir, "Starcounter.Tracking.pdb"), "Starcounter.Tracking.pdb");
                }
            }

            File.Copy(savedArchivePath, archivePath);

            Console.WriteLine("Building complete managed setup EXE...");

            // Building the Installer WPF project.
            BuildMsbuildProject(
                Path.Combine(installerWpfFolder, "Starcounter.InstallerWPF.csproj"),
                configuration,
                platform);

            // Signing the main Starcounter setup.
            signingError = BuildSystem.SignFiles(new String[] { staticSetupFilePath }, CompanyName, ProductName, pathToCertificateFile);

            // Checking if there are any errors during signing process.
            if (signingError != null)
                throw new Exception("Failed to sign static Starcounter setup file...");

            Console.WriteLine("Building final native installer wrapper...");

            String installerWrapperDir = @"Level1\src\Starcounter.Installer\Starcounter.InstallerNativeWrapper";

            File.Copy(staticSetupFilePath, Path.Combine(installerWrapperDir, "resources", staticSetupFileName), true);

            // Building the Installer WPF project.
            BuildMsbuildProject(
                @"Level1\src\Starcounter.Installer\Starcounter.InstallerNativeWrapper\Starcounter.InstallerNativeWrapper.vcxproj",
                configuration,
                "Win32",
                "/p:VisualStudioVersion=11.0");

            String specificSetupFileName = "Starcounter-" + version + "-Setup.exe";
            String specificDestSetupFilePath = Path.Combine(destinationDir, specificSetupFileName);

            if (File.Exists(specificDestSetupFilePath))
                File.Delete(specificDestSetupFilePath);

            // Moving produced setup to destination specific setup.
            File.Move(staticSetupFilePath, specificDestSetupFilePath);

            // Signing the main Starcounter setup.
            signingError = BuildSystem.SignFiles(new String[] { specificDestSetupFilePath }, CompanyName, ProductName, pathToCertificateFile);

            // Checking if there are any errors during signing process.
            if (signingError != null)
                throw new Exception("Failed to sign native installer setup...");
        }

        /// <summary>
        /// Builds given MSbuild project.
        /// </summary>
        /// <param name="pathToProject"></param>
        /// <param name="configuration"></param>
        /// <param name="platform"></param>
        /// <param name="arguments"></param>
        static void BuildMsbuildProject(String pathToProject, String configuration, String platform, String extraArguments = "")
        {
            ProcessStartInfo msbuildInfo = new ProcessStartInfo();
            msbuildInfo.FileName = BuildSystem.MsBuildExePath;
            String installerMsbuildArgs = "\"" + pathToProject + "\"" +
                " /maxcpucount /NodeReuse:false /target:Build /property:Configuration=" + configuration + ";Platform=\"" + platform + "\" " + extraArguments;

            msbuildInfo.Arguments = installerMsbuildArgs;
            msbuildInfo.UseShellExecute = false;

            Process msbuildProcess = Process.Start(msbuildInfo);
            msbuildProcess.WaitForExit();
            if (msbuildProcess.ExitCode != 0)
            {
                throw new Exception("Building " + pathToProject + " project failed with error code: " + msbuildProcess.ExitCode);
            }
            msbuildProcess.Close();
        }

        // Accepts builds pool FTP mapped directory as a path
        // ('StableBuilds' or 'NightlyBuilds' without version part).
        static int Main(string[] args)
        {
            try
            {
                if (3 == args.Length)
                {
                    BuildSystem.PrintToolWelcome("Starting Starcounter unique version builder...");

                    String UniqueDownloadKey = null,
                        DestinationDir = null,
                        PathToCertificateFile = null;

                    for (Int32 i = 0; i < args.Length; i++)
                    {
                        if (args[i].StartsWith("UniqueDownloadKey=")) UniqueDownloadKey = args[i].Substring("UniqueDownloadKey=".Length);
                        else if (args[i].StartsWith("DestinationDir=")) DestinationDir = args[i].Substring("DestinationDir=".Length);
                        else if (args[i].StartsWith("PathToCertificateFile=")) PathToCertificateFile = args[i].Substring("PathToCertificateFile=".Length);
                        else throw new Exception("Wrong argument supplied: " + args[i]);
                    }

                    // Generating unique build.
                    GenerateUniqueBuild(UniqueDownloadKey, DestinationDir, PathToCertificateFile);

                    return 0;
                }

                BuildSystem.PrintToolWelcome("Starting Starcounter installer generator...");

                // Checking if same executable is already running.
                if (BuildSystem.IsSameExecutableRunning())
                    return 0;

                // Checking if its a personal build.
                if (Environment.GetEnvironmentVariable(BuildSystem.GenerateInstallerEnvVar) != "True")
                {
                    Console.WriteLine("Skipping generation of a installer...");
                    return 0;
                }

                Console.WriteLine("Obtaining needed environment variables...");

                // Getting current build configuration.
                String configuration = Environment.GetEnvironmentVariable("Configuration");
                if (configuration == null)
                {
                    throw new Exception("Environment variable 'Configuration' does not exist.");
                }

                // Getting current build platform.
                String platform = "x64"; // Environment.GetEnvironmentVariable("Platform");
                if (platform == null)
                {
                    throw new Exception("Environment variable 'Platform' does not exist.");
                }

                // Getting current build version.
                String version = Environment.GetEnvironmentVariable(BuildSystem.BuildNumberEnvVar);
                if (version == null)
                {
                    throw new Exception("Environment variable 'BUILD_NUMBER' does not exist.");
                }

                // Getting sources directory.
                String checkoutDir = Environment.GetEnvironmentVariable(BuildSystem.CheckOutDirEnvVar);
                if (checkoutDir == null)
                {
                    throw new Exception("Environment variable " + BuildSystem.CheckOutDirEnvVar + " does not exist.");
                }

                // Getting the path to current build consolidated folder.
                String outputDir = Path.Combine(checkoutDir, "Level1\\Bin\\" + configuration);

                if (!Directory.Exists(outputDir))
                {
                    throw new Exception("Consolidated directory is empty or does not exist: " + outputDir);
                }

                Console.WriteLine("Replacing version information in configuration and source files...");

                String channel = args[0];
                String versionFileContents = "<?xml version=\"1.0\" encoding=\"UTF-8\" ?>" + Environment.NewLine;
                versionFileContents += "<VersionInfo>" + Environment.NewLine;
                versionFileContents += "  <Configuration>" + configuration + "</Configuration>" + Environment.NewLine;
                versionFileContents += "  <Platform>" + platform + "</Platform>" + Environment.NewLine;
                versionFileContents += "  <Version>" + version + "</Version>" + Environment.NewLine;

                String versionDate = BuildSystem.FindStringInFile(Path.Combine(checkoutDir, @"Level1\src\Starcounter.Internal\Constants\CurrentVersion.cs"), @"VersionDate \= DateTime\.Parse\(""[0-9Z\:\-\. ]+"",").Substring("VersionDate = DateTime.Parse(".Length + 1, 20);
                versionFileContents += "  <VersionDate>" + versionDate + "</VersionDate>" + Environment.NewLine;
                versionFileContents += "  <Channel>" + channel + "</Channel>" + Environment.NewLine;
                versionFileContents += "</VersionInfo>" + Environment.NewLine;

                // Looking for an Installer WPF resources folder.
                String installerWpfFolder = Path.Combine(checkoutDir, @"Level1\src\Starcounter.Installer\Starcounter.InstallerWPF");

                // Updating the version information.
                File.WriteAllText(Path.Combine(installerWpfFolder, BuildSystem.VersionXMLFileName), versionFileContents);
                File.WriteAllText(Path.Combine(outputDir, BuildSystem.VersionXMLFileName), versionFileContents);

                // Copying necessary embedded files.
                String installerWrapperDir = Path.Combine(checkoutDir, @"Level1\src\Starcounter.Installer\Starcounter.InstallerNativeWrapper");

                File.Copy(Path.Combine(BuildSystem.BuildServerFTP, @"SCDev\ThirdParty\dotNET\dotnetfx45_full_x86_x64.exe"),
                    Path.Combine(installerWrapperDir, "resources", "dotnetfx45_full_x86_x64.exe"), true);

                // Collecting the list of executables and libraries in order to sign them.
                String[] allFilesToSign = Directory.GetFiles(outputDir, "*.exe");
                String signingError = "error";

                // Trying to sign several times.
                signingError = BuildSystem.SignFiles(allFilesToSign, CompanyName, ProductName, LocalCertificateFilePath);

                // Checking if there are any errors during signing process.
                if (signingError != null)
                {
                    throw new Exception("Failed to sign files:" + Environment.NewLine + signingError);
                }

                Console.WriteLine("Creating installer Archive.zip.saved...");

                // Removing Starcounter-Setup.
                BuildSystem.DirectoryContainsFilesRegex(outputDir, new String[] { @"Starcounter.+Setup\.exe", @"Starcounter.+Setup\.pdb", @"Starcounter.+Setup\.ilk" }, true);

                // Packing output directory into archive.
                if (File.Exists(installerWpfFolder + "\\Resources\\Archive.zip"))
                    File.Delete(installerWpfFolder + "\\Resources\\Archive.zip");

                ZipFile.CreateFromDirectory(outputDir, installerWpfFolder + "\\Resources\\Archive.zip.saved", CompressionLevel.Optimal, false);

                // Removing old standalone package.
                String tempBuildDir = Path.Combine(checkoutDir, "TempBuild");
                if (Directory.Exists(tempBuildDir))
                    Directory.Delete(tempBuildDir, true);

                Directory.CreateDirectory(tempBuildDir);

                Console.WriteLine("Copying sources and binaries to destination package...");

                BuildSystem.CopyFilesRecursively(
                    new DirectoryInfo(outputDir),
                    new DirectoryInfo(tempBuildDir + @"\Level1\Bin\" + configuration));

                BuildSystem.CopyFilesRecursively(
                    new DirectoryInfo(installerWpfFolder),
                    new DirectoryInfo(tempBuildDir + @"\Level1\src\Starcounter.Installer\Starcounter.InstallerWPF"));

                BuildSystem.CopyFilesRecursively(
                    new DirectoryInfo(checkoutDir + @"\Level1\src\Starcounter.Tracking"),
                    new DirectoryInfo(tempBuildDir + @"\Level1\src\Starcounter.Tracking"));

                BuildSystem.CopyFilesRecursively(
                    new DirectoryInfo(checkoutDir + @"\Level1\src\Starcounter.Installer\Starcounter.InstallerNativeWrapper"),
                    new DirectoryInfo(tempBuildDir + @"\Level1\src\Starcounter.Installer\Starcounter.InstallerNativeWrapper"));

                // Copying documentation if generated.
                if (Directory.Exists(checkoutDir + @"\Level1\docs"))
                {
                    BuildSystem.CopyFilesRecursively(
                        new DirectoryInfo(checkoutDir + @"\Level1\docs\public"),
                        new DirectoryInfo(tempBuildDir + @"\docs\public"));

                    BuildSystem.CopyFilesRecursively(
                        new DirectoryInfo(checkoutDir + @"\Level1\docs\internal"),
                        new DirectoryInfo(tempBuildDir + @"\docs\internal"));
                }

                // Copy all needed build tools to target directory.
                String buildToolsBinDir = Path.Combine(checkoutDir, BuildSystem.CommonDefaultBuildToolsOutputPath);

                // Copying shared build system library.
                File.Copy(Path.Combine(buildToolsBinDir, "BuildSystemHelper.dll"), Path.Combine(tempBuildDir, "BuildSystemHelper.dll"), true);
                File.Copy(Path.Combine(buildToolsBinDir, "BuildSystemHelper.pdb"), Path.Combine(tempBuildDir, "BuildSystemHelper.pdb"), true);

                File.Copy(Path.Combine(buildToolsBinDir, "GenerateInstaller.exe"), Path.Combine(tempBuildDir, "GenerateInstaller.exe"), true);
                File.Copy(Path.Combine(buildToolsBinDir, "GenerateInstaller.pdb"), Path.Combine(tempBuildDir, "GenerateInstaller.pdb"), true);

                Directory.CreateDirectory(tempBuildDir + @"\Level1\src\Starcounter.MsBuild");
                File.Copy(checkoutDir + @"\Level1\src\Starcounter.MsBuild\Starcounter.MsBuild.Develop.targets",
                    tempBuildDir + @"\Level1\src\Starcounter.MsBuild\Starcounter.MsBuild.Develop.targets", true);

                if (File.Exists(tempBuildDir + @"\Level1\src\Starcounter.Installer\Starcounter.InstallerNativeWrapper\resources\Starcounter-Setup.exe"))
                    File.Delete(tempBuildDir + @"\Level1\src\Starcounter.Installer\Starcounter.InstallerNativeWrapper\resources\Starcounter-Setup.exe");

                File.Copy(Path.Combine(installerWpfFolder, BuildSystem.VersionXMLFileName),
                    Path.Combine(tempBuildDir, BuildSystem.VersionXMLFileName), true);

                Directory.SetCurrentDirectory(tempBuildDir);

                // Generating unique build now.
                GenerateUniqueBuild(
                    "000000000000000000000000"/*DownloadID.GenerateNewUniqueDownloadKey()*/,
                    checkoutDir,
                    LocalCertificateFilePath);

                // Deleting the existing archive since we have a copy in .saved file.
                if (File.Exists(tempBuildDir + @"\Level1\src\Starcounter.Installer\Starcounter.InstallerWPF\resources\Archive.zip"))
                    File.Delete(tempBuildDir + @"\Level1\src\Starcounter.Installer\Starcounter.InstallerWPF\resources\Archive.zip");

                if (!File.Exists(tempBuildDir + @"\Level1\src\Starcounter.Installer\Starcounter.InstallerWPF\resources\Archive.zip.saved"))
                    throw new Exception("Archive.zip.saved is not found after build is created!");

                Console.WriteLine("Cleaning temporary build directories...");

                BuildSystem.DeleteSubDirectories(tempBuildDir + @"\Level1\src\Starcounter.Installer\Starcounter.InstallerNativeWrapper",
                    new String[] { "obj", "release", "debug" });

                if (File.Exists(tempBuildDir + @"\Level1\src\Starcounter.Installer\Starcounter.InstallerNativeWrapper\Starcounter.InstallerNativeWrapper.sdf"))
                    File.Delete(tempBuildDir + @"\Level1\src\Starcounter.Installer\Starcounter.InstallerNativeWrapper\Starcounter.InstallerNativeWrapper.sdf");

                if (File.Exists(tempBuildDir + @"\Level1\src\Starcounter.Installer\Starcounter.InstallerNativeWrapper\resources\Starcounter-Setup.exe"))
                    File.Delete(tempBuildDir + @"\Level1\src\Starcounter.Installer\Starcounter.InstallerNativeWrapper\resources\Starcounter-Setup.exe");

                BuildSystem.DeleteSubDirectories(tempBuildDir + @"\Level1\src\Starcounter.Installer\Starcounter.InstallerWPF",
                    new String[] { "obj", "release", "debug" });

                BuildSystem.DeleteSubDirectories(tempBuildDir + @"\Level1\src\Starcounter.Tracking",
                    new String[] { "obj", "release", "debug" });

                Console.WriteLine("Creating all-in-one build package...");

                String pathToAllInOneArchive = tempBuildDir + "\\..\\" + channel + "-" + version + ".zip";
                if (File.Exists(pathToAllInOneArchive))
                    File.Delete(pathToAllInOneArchive);

                ZipFile.CreateFromDirectory(tempBuildDir, pathToAllInOneArchive, CompressionLevel.Optimal, false);

                // Uploading all-in-one package to public server.
                if ("True" == Environment.GetEnvironmentVariable(BuildSystem.UploadToPublicServer))
                {
                    Console.WriteLine("Uploading all-in-one build package to public server...");

                    // Calling external tool to upload build package to public server.
                    ProcessStartInfo uploadProcessInfo = new ProcessStartInfo();
                    uploadProcessInfo.FileName = outputDir + "\\BuildHelpers.exe";
                    uploadProcessInfo.Arguments = "-Mode=UploadBigFile -UploadUri=\"tracker.starcounter.com:8585/upload\" -PathToFile=\"" + pathToAllInOneArchive + "\"";
                    uploadProcessInfo.UseShellExecute = false;

                    // Start the upload and wait for exit.
                    Process uploadProcess = Process.Start(uploadProcessInfo);
                    uploadProcess.WaitForExit();
                    if (uploadProcess.ExitCode != 0)
                        throw new Exception("Uploading build package failed with error code: " + uploadProcess.ExitCode);

                    uploadProcess.Close();

                    Console.WriteLine("Uploading done...");
                }

                Console.WriteLine("Succeeded generating unique installer!");
                Console.WriteLine("---------------------------------------------------------------");

                return 0;
            }
            catch (Exception generalException)
            {
                return BuildSystem.LogException(generalException);
            }
        }
    }
}
