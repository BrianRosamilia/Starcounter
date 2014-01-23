using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Microsoft.Win32;
using System.Xml;
using System.DirectoryServices;
using System.ServiceProcess;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Net;
using System.Runtime.InteropServices;
using Starcounter.InstallerEngine;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Management;
using System.Reflection;
using System.Globalization;
using Starcounter;
using Starcounter.Internal;
using Starcounter.Server;
using Starcounter.Advanced.Configuration;
using Starcounter.Server.PublicModel;
using Starcounter.Server.PublicModel.Commands;

namespace Starcounter.InstallerEngine
{
    public class InstallerMain
    {
        // Map of installation settings and their values.
        static Dictionary<String, String> installSettings;

        /// <summary>
        /// Loads installer settings.
        /// </summary>
        /// <param name="configPath"></param>
        internal static void LoadInstallationSettings(String configPath)
        {
            // Overwriting settings if already been loaded.
            installSettings = new Dictionary<String, String>();
            SettingsLoader.LoadConfigFile(configPath, ConstantsBank.SettingsSection_Install, installSettings);
        }

        /// <summary>
        /// Compares install setting value with provided one.
        /// </summary>
        /// <param name="settingName">Name of the setting.</param>
        /// <returns>True if values are the same.</returns>
        /// <param name="compareWith"></param>
        internal static Boolean InstallationSettingCompare(String settingName, String compareWith)
        {
            String setting = SettingsLoader.GetSettingValue(settingName, installSettings);

            return setting.Equals(compareWith, StringComparison.CurrentCultureIgnoreCase);
        }

        /// <summary>
        /// Gets specific installation setting.
        /// </summary>
        /// <param name="settingName">Name of the setting.</param>
        /// <returns>Setting value.</returns>
        internal static String GetInstallationSettingValue(String settingName)
        {
            return SettingsLoader.GetSettingValue(settingName, installSettings);
        }
        
        // Creates server config XML.
        public static void CreateServerConfig(
            String serverName,
            String pathToServerDir,
            String whereToSaveConfig)
        {
            XmlDocument xmlDoc = new XmlDocument();
            XmlElement rootElem = xmlDoc.CreateElement("service");

            XmlElement elem = xmlDoc.CreateElement("server-dir");
            elem.InnerText = pathToServerDir;
            rootElem.AppendChild(elem);

            // Saving setup setting to file.
            xmlDoc.AppendChild(xmlDoc.CreateXmlDeclaration("1.0", "UTF-8", null));
            xmlDoc.AppendChild(rootElem);
            xmlDoc.Save(whereToSaveConfig);
        }

        // Copy gateway config XML.
        public static void CopyGatewayConfig(
            String serverDir,
            String gatewayStatsPort)
        {
            // Copying gateway configuration.
            File.Copy(
                Path.Combine(InstallerMain.InstallationDir, ConstantsBank.ScGatewayConfigName),
                Path.Combine(serverDir, ConstantsBank.ScGatewayConfigName),
                true);

            // Overwriting server config values.
            if (!Utilities.ReplaceXMLParameterInFile(
                Path.Combine(serverDir, ConstantsBank.ScGatewayConfigName),
                ConstantsBank.GatewayXmlStatisticsPort,
                gatewayStatsPort))
            {
                throw ErrorCode.ToException(Error.SCERRINSTALLERINTERNALPROBLEM, "Can't replace gateway statistics port.");
            }
        }

        /// <summary>
        /// Compares installed and current running Starcounter versions.
        /// Returns installed version string if versions are different.
        /// If versions are the same returns NULL.
        /// </summary>
        public static String CompareScVersions()
        {
            // Setting version to default value.
            scVersion = "unknown";

            // Reading INSTALLED Starcounter version XML file.
            String installedVersion = null;
            String installDir = CInstallationBase.GetInstalledDirFromEnv();
            if (installDir != null)
            {
                XmlDocument versionXML = new XmlDocument();
                String versionInfoFilePath = Path.Combine(installDir, ConstantsBank.SCVersionFileName);

                // Checking that version file exists and loading it.
                try
                {
                    versionXML.Load(versionInfoFilePath);

                    // NOTE: We are getting only first element.
                    installedVersion = (versionXML.GetElementsByTagName("Version"))[0].InnerText;
                }
                catch {}
            }

            // Reading CURRENT embedded Starcounter version XML file.
            String currentVersion = CurrentVersion.Version;

            // Checking if versions are not the same.
            if ((installedVersion != null) && (installedVersion != currentVersion))
                return installedVersion;

            // Setting version value to embedded version value.
            scVersion = currentVersion;
            return null;
        }

        /// <summary>
        /// Check if another instance of setup is running.
        /// </summary>
        public static Boolean AnotherSetupRunning(Int32 parentPID)
        {
            Utilities.ReportSetupEvent("Checking if another instance of setup is running...");

            // Trying to find through all processes.
            Process[] processeslist = Process.GetProcesses();
            foreach (Process process in processeslist)
            {
                if (process.ProcessName.StartsWith("Starcounter", StringComparison.CurrentCultureIgnoreCase) &&
                    process.ProcessName.EndsWith("Setup", StringComparison.CurrentCultureIgnoreCase))
                {
                    // Checking process IDs.
                    if (Process.GetCurrentProcess().Id != process.Id)
                    {
                        // Checking if its also not a parent process.
                        if (process.Id != parentPID)
                            return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// String containing info about post-setup processes.
        /// </summary>
        public static String PostProcessesInfo = "";
        public static void AddProcessToPostStart(String exePath, String args)
        {
            PostProcessesInfo += exePath + ConstantsBank.ScPostSetupFileArgsSeparator + args + Environment.NewLine;
        }

        /// <summary>
        /// Starts post-setup processes.
        /// </summary>
        public static void StartPostSetupProcesses(Boolean calledFromParent)
        {
            // Checking from where this is called.
            if (!calledFromParent)
            {
                // Saving post-setup file.
                if (File.Exists(ConstantsBank.ScPostSetupFilePath))
                    File.Delete(ConstantsBank.ScPostSetupFilePath);

                File.AppendAllText(ConstantsBank.ScPostSetupFilePath, PostProcessesInfo);

                // The outer installer will start everything.
                return;
            }

            // Checking if post-setup file does exist.
            if (File.Exists(ConstantsBank.ScPostSetupFilePath))
            {
                String[] procInfos = File.ReadAllLines(ConstantsBank.ScPostSetupFilePath);
                File.Delete(ConstantsBank.ScPostSetupFilePath);

                // Starting all processes.
                foreach (String procInfo in procInfos)
                {
                    // Extracting all process start parameters.
                    String[] procParts = procInfo.Split(new String[] { ConstantsBank.ScPostSetupFileArgsSeparator }, StringSplitOptions.RemoveEmptyEntries);
                    if ((procParts == null) || (procParts.Length > 2))
                        throw ErrorCode.ToException(Error.SCERRINSTALLERINTERNALPROBLEM, "Post-start setup file has wrong format.");

                    String exePath = procParts[0];
                    String exeArgs = "";

                    // Checking if there are some arguments.
                    if (procParts.Length > 1)
                        exeArgs = procParts[1];

                    // Starting needed process.
                    Process proc = new Process();
                    proc.StartInfo.FileName = exePath;
                    proc.StartInfo.Arguments = exeArgs;
                    proc.StartInfo.WorkingDirectory = Path.GetDirectoryName(exePath);
                    if (exePath.Contains(ConstantsBank.SCServiceExeName)) {
                        proc.StartInfo.UseShellExecute = false;
                        proc.StartInfo.CreateNoWindow = true;
                    }
                    proc.Start();
                    proc.Close();

                    // Waiting some seconds to emulate sequential calls.
                    // for (Int32 i = 0; i < 5; i++)
                    //     Thread.Sleep(1000);
                }
            }
        }

        /// <summary>
        /// Post-processes the startup setup file.
        /// </summary>
        public static void FilterStartupFile(
            Boolean startPersonalServer,
            Boolean startScDemo)
        {
            // Checking if all need to be started - then just not doing anything.
            if (startPersonalServer && startScDemo)
                return;

            // Checking if post-setup file does exist.
            if (File.Exists(ConstantsBank.ScPostSetupFilePath))
            {
                String[] procInfos = File.ReadAllLines(ConstantsBank.ScPostSetupFilePath);
                File.Delete(ConstantsBank.ScPostSetupFilePath);

                // Starting all processes.
                String processedPostStartFile = "";
                foreach (String procInfo in procInfos)
                {
                    // Ignoring certain application.
                    if ((!startPersonalServer) && (procInfo.Contains(ConstantsBank.SCServiceExeName)))
                        continue;

                    // Ignoring certain application.
                    if ((!startScDemo) && (procInfo.Contains(ConstantsBank.SCDemoName)))
                        continue;

                    processedPostStartFile += procInfo + Environment.NewLine;
                }

                // Writing filtered contents to file.
                File.WriteAllText(ConstantsBank.ScPostSetupFilePath, processedPostStartFile);
            }
        }

        // Creates database synchronously without creating separated thread.
        public static void CreateDatabaseSynchronous(
            String serverName,
            String serverDir,
            String databaseName)
        {
            Utilities.ReportSetupEvent("Creating database '" + databaseName + "' on server '" + serverName + "'...");

            // Creating server engine instance.
            String serverConfigPath = serverDir + "\\" + serverName + ServerConfiguration.FileExtension;
            ServerEngine serverEngine = new ServerEngine(serverConfigPath, InstallationDir);
            serverEngine.Setup();

            IServerRuntime iServerRuntime = serverEngine.Start();
            DatabaseInfo[] existingDbs = iServerRuntime.GetDatabases();
            foreach (DatabaseInfo dbInfo in existingDbs)
            {
                if (String.Compare(dbInfo.Name, databaseName, true) == 0)
                {
                    // Same database already exists!
                    if (!Utilities.AskUserForDecision("Database '" + databaseName + "' already exists on server '" + serverName+ "'." +
                        "Would you like to recreate/vanish it?",
                        "Database already exists..."))
                    {
                        return;
                    }
                }
            }

            // Sending create database command.
            CreateDatabaseCommand createDbCmd = new CreateDatabaseCommand(serverEngine, databaseName);
            CommandInfo cmdInfo = iServerRuntime.Execute(createDbCmd);

            // Waiting for the finish.
            iServerRuntime.Wait(cmdInfo);
            serverEngine.Stop();
        }

        /// <summary>
        /// Returns the directory path where Starcounter is installed,
        /// obtained from environment variables.
        /// </summary>
        public static String GetInstalledDirFromEnv()
        {
            // First checking the user-wide installation directory.
            String scInstDir = Environment.GetEnvironmentVariable(ConstantsBank.SCEnvVariableName,
                                                                  EnvironmentVariableTarget.User);
            if (scInstDir != null) return scInstDir;

            // Then checking the system-wide installation directory.
            scInstDir = Environment.GetEnvironmentVariable(ConstantsBank.SCEnvVariableName,
                                                           EnvironmentVariableTarget.Machine);
            return scInstDir;
        }

        // Publicly accessible Starcounter version.
        static String scVersion = "unknown";
        internal static String SCVersion
        {
            get
            {
                return scVersion;
            }
        }

        // Indicates if user wants only command line based setup.
        static Boolean silentFlag = false;
        internal static Boolean SilentFlag
        {
            get
            {
                return silentFlag;
            }
        }

        // Indicates if user wants to uninstall Starcounter.
        static Boolean uninstallFlag = false;
        internal static Boolean UninstallFlag
        {
            get
            {
                return uninstallFlag;
            }
        }

        // Indicates the folder where Starcounter should be installed.
        static String installationDir = null;
        internal static String InstallationDir
        {
            get
            {
                if (installationDir == null)
                    installationDir = CInstallationBase.GetInstalledDirFromEnv();  

                return installationDir;
            }
        }

        // Indicates if service should be started.
        static Boolean startService = true;
        internal static Boolean StartService
        {
            get
            {
                return startService;
            }

            set
            {
                startService = value;
            }
        }

        // When Starcounter installation is corrupted, complete cleanup can be used.
        static Boolean cleanupFlag = false;
        internal static Boolean CleanupFlag
        {
            get
            {
                return cleanupFlag;
            }
        }

        // Indicates that installation folder should not be deleted.
        static Boolean dontDeleteInstallDir = true;
        internal static Boolean DontDeleteInstallDir
        {
            get
            {
                return dontDeleteInstallDir;
            }
        }

        // Contains the message for displaying at the end of setup process.
        static String finalSetupMessage = null;
        internal static String FinalSetupMessage
        {
            get { return finalSetupMessage; }
            set { finalSetupMessage = value; }
        }

        // List of processes to be killed.
        internal static String[] ScProcessesList = new String[]
        {
            StarcounterConstants.ProgramNames.ScService,
            StarcounterConstants.ProgramNames.ScAdminServer,
            StarcounterConstants.ProgramNames.ScCode,
            StarcounterConstants.ProgramNames.ScData,
            StarcounterConstants.ProgramNames.ScDbLog,
            StarcounterConstants.ProgramNames.ScIpcMonitor,
            StarcounterConstants.ProgramNames.ScNetworkGateway,
            StarcounterConstants.ProgramNames.ScWeaver,
            StarcounterConstants.ProgramNames.ScSqlParser,
            StarcounterConstants.ProgramNames.ScTrayIcon,
            "ServerLogTail"
        };

        /// <summary>
        /// Indicates the progress of the
        /// tasks being done, in percents.
        /// </summary>
        static Int32 progressPercent = 0;
        static Int32 progressStep = 0;

        internal static Int32 ProgressPercent
        {
            get
            {
                return progressPercent;
            }
        }

        internal static void AddComponentToProgress()
        {
            progressStep++;
        }

        internal static void AddComponentsToProgress(int numOfComponents)
        {
            progressStep = numOfComponents;
        }

        internal static void ProgressFinished()
        {
            progressPercent = 0;
        }

        internal static void ProgressIncrement()
        {
            progressPercent += progressStep;
        }

        internal static void CalculateProgressStep()
        {
            // Getting percentage step value.
            if (progressStep != 0)
                progressStep = 100 / progressStep;
        }

        internal static void ResetProgressStep()
        {
            progressPercent = 0;
            progressStep = 0;
        }

        /// <summary>
        /// All components are declared here.
        /// </summary>
        public static CSamplesDemos SamplesDemosComponent = new CSamplesDemos();
        public static CInstallationBase InstallationBaseComponent = new CInstallationBase();
        public static CPersonalServer PersonalServerComponent = new CPersonalServer();
        public static VS2012Integration VS2012IntegrationComponent = new VS2012Integration();
        public static VS2013Integration VS2013IntegrationComponent = new VS2013Integration();

        /// <summary>
        /// Array of all Starcounter components ordered for uninstallation purpose.
        /// </summary>
        public static CComponentBase[] AllComponentsToUninstall =
        {
            PersonalServerComponent,
            VS2012IntegrationComponent,
            VS2013IntegrationComponent,
            SamplesDemosComponent,
            InstallationBaseComponent
        };

        /// <summary>
        /// Array of all Starcounter components ordered for installation purpose.
        /// </summary>
        public static CComponentBase[] AllComponentsToInstall =
        {
            InstallationBaseComponent,
            PersonalServerComponent,
            SamplesDemosComponent,
            VS2012IntegrationComponent,
            VS2013IntegrationComponent
        };

        /// <summary>
        /// Initializing components.
        /// </summary>
        internal static void InitAllComponents()
        {
            // Initializing each component one by one.
            foreach (CComponentBase comp in InstallerMain.AllComponentsToInstall)
            {
                try
                {
                    comp.Init();
                }
                catch (Exception generalException)
                {
                    // Re-throwing the exception if not cleanup.
                    if (!cleanupFlag)
                        throw generalException;

                    // Silently logging the exception.
                    Utilities.LogMessage(generalException.ToString());
                }
            }
        }

        // Installing each component one by one.
        static void InstallAllComponents()
        {
            foreach (CComponentBase comp in InstallerMain.AllComponentsToInstall)
            {
                comp.Install();
            }
        }

        /// <summary>
        /// Contains references to installer GUI callbacks.
        /// </summary>
        internal static EventHandler<Utilities.InstallerProgressEventArgs> GuiProgressCallback = null;
        internal static EventHandler<Utilities.MessageBoxEventArgs> GuiMessageboxCallback = null;

        /// <summary>
        /// Sets a nice WPF message box instead of standard one.
        /// </summary>
        /// <param name="guiMessageboxCallback"></param>
        public static void SetNiceWpfMessageBoxDelegate(EventHandler<Utilities.MessageBoxEventArgs> guiMessageboxCallback)
        {
            GuiMessageboxCallback = guiMessageboxCallback;
        }

        /// <summary>
        /// This is the main environment setup function
        /// which is exposed to be called from outside.
        /// </summary>
        public static void StarcounterSetup(
            String[] args,
            String binariesPath,
            String setupConfigFile,
            EventHandler<Utilities.InstallerProgressEventArgs> progressCallback,
            EventHandler<Utilities.MessageBoxEventArgs> messageboxCallback)
        {
            //System.Diagnostics.Debugger.Launch();

            try
            {
                // Installing special installer exception factory.
                InstallerExceptionFactory.InstallInCurrentAppDomain();

                // Assigning installer GUI callbacks (if calling engine from GUI).
                GuiProgressCallback = progressCallback;
                GuiMessageboxCallback = messageboxCallback;

                // The following log message indicates the start of new setup session.
                Utilities.LogMessage("--------- Starting new Starcounter setup session ---------");

                // Number of modes that are not compatible.
                Int32 numDistinctModes = 0;

                // Processing command-line options.
                if (args != null)
                {
                    foreach (String param in args)
                    {
                        if (param.EndsWith(ConstantsBank.SCSilentSetupParam, StringComparison.InvariantCultureIgnoreCase))
                        {
                            // User selected to run the Installer in command-line mode.
                            silentFlag = true;
                        }
                        else if (param.EndsWith(ConstantsBank.SCUninstallParam, StringComparison.InvariantCultureIgnoreCase))
                        {
                            // User selected to run the setup in uninstall mode.
                            uninstallFlag = true;
                            numDistinctModes++;
                        }
                        else if (param.EndsWith(ConstantsBank.SCCleanupParam, StringComparison.InvariantCultureIgnoreCase))
                        {
                            // User selected to run the setup in cleanup mode.
                            cleanupFlag = true;
                            numDistinctModes++;
                        }
                        else
                        {
                            // Displaying command help message.
                            String argsHelp = "Welcome to Starcounter installation engine!" + Environment.NewLine +
                                              "The following command-line flags are available:" + Environment.NewLine +
                                              "1. " + ConstantsBank.SCSilentSetupParam + " - run the utility in command-line console mode only, without GUI elements." + Environment.NewLine +
                                              "2. " + ConstantsBank.SCUninstallParam + " - run the utility in uninstallation mode (components removal), the Default mode is installation (components addition) mode." + Environment.NewLine +
                                              "3. " + ConstantsBank.SCCleanupParam + " - run the utility in cleanup mode in order to clean the system from Starcounter when installation is corrupted." + Environment.NewLine;

                            Utilities.ConsoleMessage(argsHelp);
                            Utilities.MessageBoxInfo(argsHelp, "Installer command-line flags...");

                            throw ErrorCode.ToException(Error.SCERRINSTALLERABORTED, "Incorrect installer command-line flags supplied.");
                        }
                    }
                }

                // Checking how many flags supplied by user.
                if (numDistinctModes > 1)
                    throw ErrorCode.ToException(Error.SCERRINSTALLERABORTED, "Several modes can not be activated simultaneously. Supply only one of those.");

                // Killing all disturbing processes.
                if (!Utilities.KillDisturbingProcesses(ScProcessesList, false))
                {
                    // User has rejected the choice to kill processes.
                    throw ErrorCode.ToException(Error.SCERRINSTALLERABORTED, "User has rejected the choice to kill running Starcounter processes.");
                }

                try // The following procedures can cause exceptions only if installation is corrupted.
                {
                    // Binaries path (aka installation directory) should be specified.
                    installationDir = binariesPath;

                    // Checking for installed Starcounter binaries folder.
                    String installedBinariesPath = CInstallationBase.GetInstalledDirFromEnv();  
                    if (installedBinariesPath != null)
                        installationDir = installedBinariesPath;

                    if (installationDir != null)
                    {
                        // Deleting folder only in case when installation path exists.
                        dontDeleteInstallDir = false;
                    }

                    // Checking if installation directory is determined, otherwise taking current executable path.
                    if (installationDir == null)
                        installationDir = System.Windows.Forms.Application.StartupPath;

                    // Trying to load configuration settings.
                    if (setupConfigFile == null)
                        setupConfigFile = Path.Combine(installationDir, ConstantsBank.ScGlobalSettingsXmlName);

                    if (!cleanupFlag)
                    {
                        if (uninstallFlag)
                        {
                            // Loading uninstallation settings from setup settings file.
                            UninstallEngine.LoadUninstallationSettings(setupConfigFile);

                            // Loading installation settings in order to get server paths, etc.
                            InstallerMain.LoadInstallationSettings(Path.Combine(installationDir, ConstantsBank.ScGlobalSettingsXmlName));
                        }
                        else
                        {
                            // Loading installation settings.
                            LoadInstallationSettings(setupConfigFile);
                        }
                    }
                    else
                    {
                        // Trying to load settings files - if it fails, it fails..

                        // Loading installation settings in order to get server paths, etc.
                        try { InstallerMain.LoadInstallationSettings(Path.Combine(installationDir, ConstantsBank.ScGlobalSettingsXmlName)); }
                        catch { }
                    }
                }
                catch (InstallerAbortedException userCanceled)
                {
                    // Simply re-throwing user all cancellations.
                    throw userCanceled;
                }
                catch (Exception generalException)
                {
                    // Logging the exception.
                    Utilities.LogMessage(generalException.ToString());

                    if (!silentFlag)
                    {
                        if (Utilities.AskUserForDecision("Starcounter installation seems corrupted." + Environment.NewLine +
                            "Corruption details:" + Environment.NewLine +
                            generalException.ToString() + Environment.NewLine + Environment.NewLine +
                            "Would you like to run complete installation clean-up?",
                            "Installation is corrupted..."))
                        {
                            UninstallEngine.UninstallStarcounter(true);

                            throw ErrorCode.ToException(
                                Error.SCERRINSTALLERABORTED,
                                generalException,
                                "Starcounter installation has been detected as corrupted. User proceeded with clean up. Clean up finished.");
                        }
                    }

                    // Throwing the abort exception.
                    throw ErrorCode.ToException(
                        Error.SCERRINSTALLERABORTED, 
                        generalException, 
                        "Starcounter installation has been detected as corrupted. User didn't proceed with clean up.");
                }

                try
                {
                    // Setting normal binary directory file attributes.
                    Utilities.SetNormalDirectoryAttributes(new DirectoryInfo(installationDir));

                    // Initializing each component one by one.
                    InitAllComponents();

                    // Checking if user wants to uninstall Starcounter.
                    if (uninstallFlag)
                    {
                        UninstallEngine.UninstallStarcounter(false);
                        return;
                    }

                    // Checking if user wants to cleanup system from Starcounter.
                    if (cleanupFlag)
                    {
                        UninstallEngine.UninstallStarcounter(true);
                        return;
                    }

                    // Reseting progress steps.
                    ResetProgressStep();

                    // Mapping boolean flags to settings.
                    if (VS2012IntegrationComponent.ShouldBeInstalled()) AddComponentToProgress();
                    if (VS2013IntegrationComponent.ShouldBeInstalled()) AddComponentToProgress();
                    if (PersonalServerComponent.ShouldBeInstalled())
                    {
                        AddComponentToProgress();

                        // Checking if shortcuts should be installed.
                        Boolean createShortcuts = InstallerMain.InstallationSettingCompare(ConstantsBank.Setting_CreatePersonalServerShortcuts, ConstantsBank.Setting_True);
                        if (createShortcuts)
                            AddComponentToProgress();
                    }
                    if (SamplesDemosComponent.ShouldBeInstalled()) AddComponentToProgress();
                    if (InstallationBaseComponent.ShouldBeInstalled()) AddComponentToProgress();

                    // Getting percentage step value.
                    CalculateProgressStep();

                    // Installing each component one by one.
                    InstallAllComponents();

                    // Starting post-setup processes if needed.
                    StartPostSetupProcesses(false);

                    // Restarting Starcounter service if needed.
                    CPersonalServer.StartServiceIfAutomatic();
                }
                catch (InstallerAbortedException userCanceled)
                {
                    // Simply re-throwing all user cancellations.
                    throw userCanceled;
                }
                catch (Exception generalException)
                {
                    // Logging the exception.
                    Utilities.LogMessage(generalException.ToString());

                    if (uninstallFlag)
                    {
                        // Printing message box about rollback.
                        if (!silentFlag)
                        {
                            if (!Utilities.AskUserForDecision("Error occurred during uninstall:" + Environment.NewLine +
                                generalException.ToString() + Environment.NewLine + Environment.NewLine +
                                "Do you want to completely cleanup your system from Starcounter?",
                                "Uninstall error occurred..."))
                            {
                                // Throwing the abort exception.
                                throw ErrorCode.ToException(
                                    Error.SCERRINSTALLERABORTED,
                                    generalException,
                                    "Starcounter uninstallation has failed. User didn't proceed with clean up.");
                            }
                        }

                        // User selected to clean up system from Starcounter.
                        UninstallEngine.UninstallStarcounter(true);

                        // Throwing the abort exception.
                        throw ErrorCode.ToException(
                            Error.SCERRINSTALLERABORTED,
                            generalException,
                            "Starcounter uninstallation has failed. User proceeded with clean up. Clean up finished.");
                    }
                    else if (!cleanupFlag)
                    {
                        // Printing message box about rollback.
                        if (!silentFlag)
                        {
                            Utilities.MessageBoxError("Error occurred during installation:" + Environment.NewLine + Environment.NewLine +
                                generalException.ToString() + Environment.NewLine + Environment.NewLine + "Running rollback...",
                                "Installation error...");
                        }

                        try
                        {
                            // Rolling back the installation.
                            UninstallEngine.RollbackInstallation();
                        }
                        catch (Exception rollbackException)
                        {
                            // Logging the exception.
                            Utilities.LogMessage(rollbackException.ToString());

                            if (!silentFlag)
                            {
                                if (!Utilities.AskUserForDecision("Error occurred during rollback:" + Environment.NewLine +
                                    generalException.ToString() + Environment.NewLine + Environment.NewLine +
                                    "Do you want to completely cleanup your system from Starcounter?",
                                    "Installation error occurred..."))
                                {
                                    // Throwing the abort exception.
                                    throw ErrorCode.ToException(
                                        Error.SCERRINSTALLERABORTED,
                                        rollbackException,
                                        "Starcounter rollback has failed. User didn't proceed with clean up.");
                                }
                            }

                            // User selected to clean up system from Starcounter.
                            UninstallEngine.UninstallStarcounter(true);

                            // Throwing abortion exception.
                            throw ErrorCode.ToException(
                                Error.SCERRINSTALLERABORTED,
                                rollbackException,
                                "Starcounter rollback has failed. User proceeded with clean up. Clean up finished.");
                        }
                    }

                    // Re-throwing the exception up.
                    throw generalException;
                }

                // Adding last progress update.
                ProgressFinished();
                Utilities.ReportSetupEvent("Installation process finished...");
            }
            catch (Exception generalException)
            {
                // Logging the exception.
                Utilities.ReportSetupEvent(generalException.ToString());

                // Explicitly logging error's stack trace.
                Utilities.LogMessage(generalException.StackTrace);

                // Re-throwing up..
                throw generalException;
            }
            finally // This step will always run at the end of any setup session.
            {
                if (finalSetupMessage != null)
                {
                    if (InstallerMain.SilentFlag)
                    {
                        // Printing a console message.
                        Utilities.ConsoleMessage(finalSetupMessage);
                    }
                    else
                    {
                        // Showing a message box to user.
                        Utilities.MessageBoxInfo(finalSetupMessage, "Starcounter Setup...");
                    }
                }
            }
        }
    }
}
