using System;
using Microsoft.Win32;
using System.IO;
using System.ServiceProcess;
using System.DirectoryServices;
using Starcounter;
using System.Diagnostics;
using Starcounter.Internal;
using Starcounter.Server.Setup;
using System.Xml;
using Starcounter.Advanced.Configuration;
using Starcounter.Server.Service;
using System.Threading;
using Starcounter.Server.Windows;

namespace Starcounter.InstallerEngine
{
public class CPersonalServer : CComponentBase
{
    /// <summary>
    /// Component initialization.
    /// </summary>
    public CPersonalServer()
    {
    }

    /// <summary>
    /// Provides descriptive name of the components.
    /// </summary>
    public override String DescriptiveName
    {
        get
        {
            return "Starcounter Personal Server Component";
        }
    }

    /// <summary>
    /// Provides name of the component setting.
    /// </summary>
    public override String SettingName
    {
        get
        {
            return ConstantsBank.Setting_InstallPersonalServer;
        }
    }

    /// <summary>
    /// Provides installation path of the component.
    /// </summary>
    public override String ComponentPath
    {
        get
        {
            return InstallerMain.GetInstallationSettingValue(ConstantsBank.Setting_PersonalServerPath);
        }
    }

    // Desktop shortcut.
    readonly String PersonalServerDesktopShortcutPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                     ConstantsBank.SCProductName + " " + StarcounterEnvironment.ServerNames.PersonalServer + " Server.lnk");

    readonly String PersonalServerAdminDesktopShortcutPath =
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                 ConstantsBank.SCProductName + " " + StarcounterEnvironment.ServerNames.PersonalServer + " Administrator.lnk");

    // TrayIcon Shortcut, Placed in the Startup folder for all users (requires admin rights)
    readonly String TrayIconShortcutPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "Starcounter TrayIcon.lnk");

    // Start Menu shortcut.
    readonly String PersonalServerStartMenuPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                     "Programs\\" + ConstantsBank.SCProductName + "\\" + ConstantsBank.SCProductName + " " + StarcounterEnvironment.ServerNames.PersonalServer + " Server.lnk");

    /// <summary>
    /// Creates a Desktop and Menu shortcuts for personal server.
    /// </summary>
    void CreatePersonalServerShortcuts()
    {
        // Checking if shortcuts should be installed.
        Boolean createShortcuts = InstallerMain.InstallationSettingCompare(
            ConstantsBank.Setting_CreatePersonalServerShortcuts,
            ConstantsBank.Setting_True);

        // Checking if user wants to create desktop shortcuts.
        if (!createShortcuts)
            return;

        // Logging event.
        Utilities.ReportSetupEvent("Creating Personal Server shortcuts...");

        // Shortcut to installation directory.
        String installPath = InstallerMain.InstallationBaseComponent.ComponentPath;

        // Calling external tool to create server shortcut.
        Utilities.CreateShortcut(
            Path.Combine(installPath, ConstantsBank.SCServiceExeName),
            PersonalServerDesktopShortcutPath,
            StarcounterEnvironment.ServerNames.PersonalServer,
            installPath,
            "Starts " + ConstantsBank.SCProductName  + " Personal Server.",
            Path.Combine(InstallerMain.InstallationDir, ConstantsBank.SCIconFilename));

        // Calling external tool to create Administrator shortcut.
        Utilities.CreateShortcut(
            "http://127.0.0.1:" + InstallerMain.GetInstallationSettingValue(ConstantsBank.Setting_DefaultPersonalServerSystemHttpPort),
            PersonalServerAdminDesktopShortcutPath,
            "",
            installPath,
            "Starts " + ConstantsBank.SCProductName + " Personal Administrator.",
            Path.Combine(InstallerMain.InstallationDir, ConstantsBank.SCAdminIconFilename));

        // Obtaining path to Start Menu for a current user.
        String startMenuDir = Path.GetDirectoryName(PersonalServerStartMenuPath);

        // Creating Start Menu directory if needed.
        if (!Directory.Exists(startMenuDir))
            Directory.CreateDirectory(startMenuDir);

        // Calling external tool to create shortcut.
        Utilities.CreateShortcut(
            Path.Combine(installPath, ConstantsBank.SCServiceExeName),
            PersonalServerStartMenuPath,
            StarcounterEnvironment.ServerNames.PersonalServer,
            installPath,
            "Starts " + ConstantsBank.SCProductName + " " + StarcounterEnvironment.ServerNames.PersonalServer + " Administrator.",
            Path.Combine(InstallerMain.InstallationDir, ConstantsBank.SCIconFilename));

        // Updating progress.
        InstallerMain.ProgressIncrement();
    }

    /// <summary>
    /// Create TrayIcon shortcut in Windows StartUp folder
    /// </summary>
    void CreateAutoStartTrayIconShortcut() {

        // Logging event.
        Utilities.ReportSetupEvent("Creating TrayIcon shortcut");

        // Shortcut to installation directory.
        String installPath = InstallerMain.InstallationBaseComponent.ComponentPath;

        // Create shortcut for the TrayIcon
        Utilities.CreateShortcut(
            Path.Combine(installPath, Starcounter.Internal.StarcounterConstants.ProgramNames.ScTrayIcon + ".exe"), // Target file
            TrayIconShortcutPath, // Shortcut lnk file
            "-autostarted", // CommandArgs ('-autostarted' Indicating that it will be auto started when user logs in)
            installPath, // Workingdir
            "Starcounter TrayIcon", // Description
            Path.Combine(InstallerMain.InstallationDir, ConstantsBank.SCAdminIconFilename) // Icon
            );
    }

    /// <summary>
    /// Creates the windows service that can optionally run the server.
    /// </summary>
    /// <returns>The setup used when the server was created.</returns>
    ServerServiceSetup CreateWindowsService() {
        var setup = new ServerServiceSetup();
        setup.StartupType = Server.Windows.StartupType.Automatic;

        var vsComponent = InstallerMain.VS2012IntegrationComponent;
        if (vsComponent.IsInstalled() || vsComponent.ShouldBeInstalled()) {
            setup.StartupType = Server.Windows.StartupType.Manual;
        }

        var vsComponent13 = InstallerMain.VS2013IntegrationComponent;
        if (vsComponent13.IsInstalled() || vsComponent13.ShouldBeInstalled()) {
            setup.StartupType = Server.Windows.StartupType.Manual;
        }
        
        Utilities.ReportSetupEvent("Adding Starcounter server service...");
        setup.Execute();

        return setup;
    }

    internal static void StartServiceIfAutomatic() {
        var service = ServerService.Find();
        if (service != null) {
            // Only start it if it's automatic start type.
            // Otherwise, start the personal server?
            // TODO:

            var config = WindowsServiceHelper.GetServiceConfig(service.ServiceName);
            if (config.dwStartType == (uint) Win32Service.SERVICE_START.SERVICE_AUTO_START) {
                Utilities.ReportSetupEvent("Starting Starcounter service...");
                ServerService.Start(service.ServiceName);
            }
        }
    }

    void DeleteWindowsService() {
        Utilities.ReportSetupEvent("Removing Starcounter server service...");

        ServerService.Stop(ServerService.Name, 60 * 1000);
        ServerService.Delete();

        // Checking for Starcounter service existence.
        Boolean serviceStillFound = false;
        for (Int32 i = 0; i < 5; i++) {
            var allServices = ServiceController.GetServices();
            foreach (ServiceController someService in allServices) {
                if (ServerService.IsServerService(someService)) {
                    serviceStillFound = true;
                    break;
                }
            }

            // If service does not exist we are stopping the search procedure.
            if (!serviceStillFound)
                break;

            Thread.Sleep(1000);
        }

        // If service is still found - panic!
        var servicePanicMsg = 
            "Starcounter system service still exists in the system and can not be completely removed." + Environment.NewLine +
            "Please check that Starcounter service is not blocked by any application or restart the system.";

        if (serviceStillFound) {
            if (InstallerMain.SilentFlag) {
                // Printing a console message.
                Utilities.ConsoleMessage(servicePanicMsg);
            } else {
                Utilities.MessageBoxInfo(servicePanicMsg, "Starcounter service still exists in the system...");
            }
        }

        // Disabling service start at the end.
        InstallerMain.StartService = false;
    }

    /// <summary>
    /// Starts personal server if demanded.
    /// </summary>
    void StartPersonalServer()
    {
        // Adding process to post-setup start.
        InstallerMain.AddProcessToPostStart(
            Path.Combine(InstallerMain.InstallationDir, ConstantsBank.SCServiceExeName),
            StarcounterEnvironment.ServerNames.PersonalServer
            );
    }

    /// <summary>
    /// Checks if server directory exists and asks user for its removal.
    /// </summary>
    /// <param name="serverDir"></param>
    public static void CheckExistingServerDirectory(String serverDir)
    {
        if (Directory.Exists(serverDir))
        {
            if (!Utilities.AskUserForDecision(
                "Server directory already exists: " + serverDir + Environment.NewLine +
                "Would you like to remove it? ALL DATA IN THIS DIRECTORY WILL BE LOST!",
                "Server directory already exists..."))
            {
                // TODO: Talk with Anders about catching this type of exception in GUI.
                //throw new InstallerAbortedException("User rejected removal of server directory: " + serverDir);
            }
            else
            {
                // Removing directory.
                Utilities.ForceDeleteDirectory(new DirectoryInfo(serverDir));
            }
        }
    }

    /// <summary>
    /// Installs component.
    /// </summary>
    public override void Install()
    {
        // Checking if component should be installed in this session.
        if (!ShouldBeInstalled())
            return;

        // Checking that component is not already installed.
        if (!CanBeInstalled())
            return;

        // Server related directories.
        String serverOuterDir = ComponentPath;

        // Checking for existing server directory.
        CheckExistingServerDirectory(serverOuterDir);

        // Logging event.
        Utilities.ReportSetupEvent("Creating environment variables for personal database engine...");

        // Obsolete: there is no longer need for such a variable.
        // TODO:
        // Setting the default server environment variable.
        Environment.SetEnvironmentVariable(
            ConstantsBank.SCEnvVariableDefaultServer,
            ConstantsBank.SCPersonalServerName,
            EnvironmentVariableTarget.User);

        // Logging event.
        Utilities.ReportSetupEvent("Installing server...");

        // Checking that server path is in user's personal directory.
        if (!Utilities.ParentChildDirectory(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\..", serverOuterDir))
        {
            if (!Utilities.RunningOnBuildServer())
            {
                Utilities.MessageBoxWarning("You are installing Personal Server not in user directory."
                    + " Make sure you have read/write access rights to the directory: " +
                    serverOuterDir, "Personal server installation in non-user directory...");
            }
        }

        // Logging event.
        Utilities.ReportSetupEvent("Creating structure for the server...");

        // Creating new server repository.
        RepositorySetup setup = RepositorySetup.NewDefault(serverOuterDir, StarcounterEnvironment.ServerNames.PersonalServer);

        setup.Execute();

        String serverInnerDir = setup.Structure.RepositoryDirectory;
        String serverConfigPath = setup.ServerConfiguration.ConfigurationFilePath;

        // Replacing default server parameters.
        if (!Utilities.ReplaceXMLParameterInFile(
            serverConfigPath,
            StarcounterConstants.BootstrapOptionNames.DefaultUserHttpPort,
            InstallerMain.GetInstallationSettingValue(ConstantsBank.Setting_DefaultPersonalServerUserHttpPort)))
        {
            throw ErrorCode.ToException(Error.SCERRINSTALLERINTERNALPROBLEM,
                "Can't replace default Apps TCP port for " + StarcounterEnvironment.ServerNames.PersonalServer + " server.");
        }

        // Replacing default server parameters.
        if (!Utilities.ReplaceXMLParameterInFile(
            serverConfigPath,
            ServerConfiguration.SystemHttpPortString,
            InstallerMain.GetInstallationSettingValue(ConstantsBank.Setting_DefaultPersonalServerSystemHttpPort)))
        {
            throw ErrorCode.ToException(Error.SCERRINSTALLERINTERNALPROBLEM,
                "Can't replace Administrator TCP port for " + StarcounterEnvironment.ServerNames.PersonalServer + " server.");
        }

        // Replacing default server parameters.
        if (!Utilities.ReplaceXMLParameterInFile(
            serverConfigPath,
            StarcounterConstants.BootstrapOptionNames.SQLProcessPort,
            InstallerMain.GetInstallationSettingValue(ConstantsBank.Setting_DefaultPersonalPrologSqlProcessPort)))
        {
            throw ErrorCode.ToException(Error.SCERRINSTALLERINTERNALPROBLEM,
                "Can't replace Prolog SQL TCP port for " + StarcounterEnvironment.ServerNames.PersonalServer + " server.");
        }

        // Setting the given personal server system HTTP port on the current user
        int serverSystemPort;
        var serverSystemPortString = InstallerMain.GetInstallationSettingValue(ConstantsBank.Setting_DefaultPersonalServerSystemHttpPort);
        if (string.IsNullOrEmpty(serverSystemPortString) || !int.TryParse(serverSystemPortString, out serverSystemPort)) {
            throw ErrorCode.ToException(
                Error.SCERRINSTALLERINTERNALPROBLEM,
                "Unable to properly access given server HTTP port value: " + serverSystemPortString ?? "NULL");
        }

        // Send usage statistics and crash reports to the tracker
        bool sendUsageAndCrashReports;
        var sendUsageAndCrashReportsString = InstallerMain.GetInstallationSettingValue(ConstantsBank.Setting_SendUsageAndCrashReports);
        if (string.IsNullOrEmpty(sendUsageAndCrashReportsString) || !bool.TryParse(sendUsageAndCrashReportsString, out sendUsageAndCrashReports)) {
            throw ErrorCode.ToException(
                Error.SCERRINSTALLERINTERNALPROBLEM,
                "Unable to properly access given property for send user stats and crash report: " + sendUsageAndCrashReportsString ?? "NULL");
        }

        // Save SendUsageAndCrashReports setting
        if (setup.ServerConfiguration.SendUsageAndCrashReports != sendUsageAndCrashReports) {
            setup.ServerConfiguration.SendUsageAndCrashReports = sendUsageAndCrashReports;
            setup.ServerConfiguration.Save();
        }


        Environment.SetEnvironmentVariable(
            ConstantsBank.SCEnvVariableDefaultPersonalPort,
            serverSystemPortString,
            EnvironmentVariableTarget.User);

        // Creating server config.
        InstallerMain.CreateServerConfig(
            StarcounterEnvironment.ServerNames.PersonalServer,
            serverInnerDir,
            PersonalServerConfigPath);

        // Copying gateway configuration.
        InstallerMain.CopyGatewayConfig(
            serverInnerDir,
            InstallerMain.GetInstallationSettingValue(ConstantsBank.Setting_DefaultPersonalServerSystemHttpPort));

        // Creating service
        CreateWindowsService();

        // Creating shortcuts.
        CreatePersonalServerShortcuts();

        // Create TrayIcon auto start Shortcut
        CreateAutoStartTrayIconShortcut();

        // Creating Administrator database.
        // TODO: Recover if in need of a database for Administrator.
        /*
        InstallerMain.CreateDatabaseSynchronous(
            StarcounterEnvironment.ServerNames.PersonalServer,
            serverInnerDir,
            ConstantsBank.SCAdminDatabaseName);
        */

        // Starts personal server if demanded.
        StartPersonalServer();

        // Updating progress.
        InstallerMain.ProgressIncrement();

    }

    // Path to server configuration file.
    String personalServerConfigPath_ = null;
    String PersonalServerConfigPath
    {
        get
        {
            if (personalServerConfigPath_ != null)
                return personalServerConfigPath_;

            if (InstallerMain.InstallationDir == null)
                return null;

            personalServerConfigPath_ = Path.Combine(InstallerMain.InstallationDir, StarcounterEnvironment.ServerNames.PersonalServer + ".xml");

            return personalServerConfigPath_;
        }
    }

    /// <summary>
    /// Removes component.
    /// </summary>
    public override void Uninstall()
    {
        if (!UninstallEngine.CompleteCleanupSetting)
        {
            if (UninstallEngine.RollbackSetting)
            {
                // Checking if component was installed in this session.
                if (!ShouldBeInstalled())
                    return;
            }
            else // Standard removal.
            {
                // Checking if component is selected to be removed.
                if (!ShouldBeRemoved())
                    return;

                // Checking if component can be removed.
                if (!CanBeRemoved())
                    return;
            }
        }

        DeleteWindowsService();

        // Logging event.
        Utilities.ReportSetupEvent("Removing Personal Server environment variables...");

        // Removing default server environment variable.
        Environment.SetEnvironmentVariable(
            ConstantsBank.SCEnvVariableDefaultServer,
            null,
            EnvironmentVariableTarget.User);

        Environment.SetEnvironmentVariable(
            ConstantsBank.SCEnvVariableDefaultPersonalPort,
            null,
            EnvironmentVariableTarget.User);


        // Logging event.
        Utilities.ReportSetupEvent("Deleting personal database server registry entries...");

        // Removing registry entries.
        RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("SOFTWARE", true);

        // Looking if 'Starcounter' key exists and trying to delete it.
        if ((registryKey != null) && (registryKey.OpenSubKey(ConstantsBank.SCProductName, true) != null))
            registryKey.DeleteSubKeyTree(ConstantsBank.SCProductName);

        // Deleting personal server configuration folder if demanded.
        Utilities.ReportSetupEvent("Deleting personal database server configuration...");

        // Getting the server installation path.
        try
        {
            String personalServerPath = ComponentPath;

            if (Utilities.DirectoryIsNotEmpty(new DirectoryInfo(personalServerPath)))
            {
                InstallerMain.FinalSetupMessage += "You can find and manually delete the Personal Server databases directory in: '" +
                    personalServerPath + "'" + Environment.NewLine;
            }
        }
        catch { }

        // Deleting the config file.
        if (File.Exists(PersonalServerConfigPath))
            File.Delete(PersonalServerConfigPath);

        // Removing desktop shortcut.
        if (File.Exists(PersonalServerDesktopShortcutPath))
            File.Delete(PersonalServerDesktopShortcutPath);

        // Removing desktop shortcut.
        if (File.Exists(PersonalServerAdminDesktopShortcutPath))
            File.Delete(PersonalServerAdminDesktopShortcutPath);

        // Removing Start Menu shortcut.
        if (File.Exists(PersonalServerStartMenuPath))
            File.Delete(PersonalServerStartMenuPath);

        // Removing TrayIcon shortcut.
        if (File.Exists(TrayIconShortcutPath))
            File.Delete(TrayIconShortcutPath);

        // Updating progress.
        InstallerMain.ProgressIncrement();
    }

    /// <summary>
    /// Checks if components is already installed.
    /// </summary>
    /// <returns>True if already installed.</returns>
    public override Boolean IsInstalled()
    {
        // Checking for Starcounter environment variables existence.
        String envVar = ComponentsCheck.CheckServerEnvVars(true, false);
        if (envVar != null)
            return true;

        // Getting list of installed programs.
        RegistryKey rkSettings = Registry.CurrentUser.OpenSubKey("SOFTWARE");

        // Checking if Starcounter settings (personal installation) exist in registry.
        if ((rkSettings != null) && (rkSettings.OpenSubKey(ConstantsBank.SCProductName) != null))
            return true;

        // Checking for Starcounter server configuration file.
        String serverDir = Utilities.ReadServerInstallationPath(PersonalServerConfigPath);
        if (Directory.Exists(serverDir))
            return true;

        // Check if the Windows service is installed and configured.
        foreach (var service in ServiceController.GetServices()) {
            if (ServerService.IsServerService(service)) {
                return true;
            }
        }

        // None of evidence found.
        return false;
    }

    /// <summary>
    /// Checks if component can be installed.
    /// </summary>
    /// <returns>True if can.</returns>
    public override Boolean CanBeInstalled()
    {
        return (!IsInstalled());
    }

    /// <summary>
    /// Checks if component can be installed.
    /// </summary>
    /// <returns>True if can.</returns>
    public override Boolean CanBeRemoved()
    {
        return IsInstalled();
    }

    /// <summary>
    /// Determines if this component should be installed
    /// in this session.
    /// </summary>
    /// <returns>True if component should be installed.</returns>
    public override Boolean ShouldBeInstalled()
    {
        return InstallerMain.InstallationSettingCompare(ConstantsBank.Setting_InstallPersonalServer, ConstantsBank.Setting_True);
    }

    /// <summary>
    /// Determines if this component should be uninstalled
    /// in this session.
    /// </summary>
    /// <returns>True if component should be uninstalled.</returns>
    public override Boolean ShouldBeRemoved()
    {
        return UninstallEngine.UninstallationSettingCompare(ConstantsBank.Setting_RemovePersonalServer, ConstantsBank.Setting_True);
    }
}
}