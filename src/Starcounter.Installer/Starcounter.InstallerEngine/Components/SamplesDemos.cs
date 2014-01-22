using System;
using Microsoft.Win32;
using System.IO;
using System.ServiceProcess;
using System.DirectoryServices;
using System.Configuration.Install;
using System.Threading;
using System.Windows.Forms;
using Starcounter;
using System.Diagnostics;
using Starcounter.Advanced.Configuration;
using Starcounter.Server;
using Starcounter.Server.PublicModel;
using Starcounter.Server.PublicModel.Commands;

namespace Starcounter.InstallerEngine
{
public class CSamplesDemos : CComponentBase
{
    /// <summary>
    /// Component initialization.
    /// </summary>
    public CSamplesDemos()
    {
    }

    /// <summary>
    /// Provides name of the component setting.
    /// </summary>
    public override String SettingName
    {
        get
        {
            return "Not exist";
        }
    }

    /// <summary>
    /// Provides descriptive name of the components.
    /// </summary>
    public override String DescriptiveName
    {
        get
        {
            return "Starcounter Samples and Demos Component";
        }
    }

    // Path to samples in setup distribution directory.
    String vsSamplesPathOrig_ = null;

    public String VsSamplesPathOrig
    {
        get { return vsSamplesPathOrig_; }
        set { vsSamplesPathOrig_ = value; }
    }


    // Path to samples destination directory.
    String vsSamplesPathDest_ = null;

    public String VsSamplesPathDest
    {
        get { return vsSamplesPathDest_; }
        set { vsSamplesPathDest_ = value; }
    }

    /// <summary>
    /// Initializes component data.
    /// </summary>
    public override void Init()
    {
        // Creating needed globals here.
        vsSamplesPathOrig_ = Path.Combine(InstallerMain.InstallationBaseComponent.ComponentPath, ConstantsBank.ScSamplesDemosDirName);
        vsSamplesPathDest_ = Path.Combine(InstallerMain.PersonalServerComponent.ComponentPath, ConstantsBank.ScSamplesDemosDirName);
    }

    /// <summary>
    /// Initializes samples directories and copies files.
    /// </summary>
    Boolean CopySamplesAndDemos()
    {
        // Checking if source directory exists.
        if (!Directory.Exists(vsSamplesPathOrig_))
            return false;

        // Checking if samples present in destination folder.
        if (Utilities.DirectoryIsNotEmpty(new DirectoryInfo(vsSamplesPathDest_)))
        {
            if (InstallerMain.SilentFlag || (!Utilities.AskUserForDecision("Starcounter samples seem already installed to: " + vsSamplesPathDest_ + Environment.NewLine + "Do you want to overwrite them?",
                "Starcounter samples found...")))
            {
                return false;
            }
        }

        // Copying files only when directories exist.
        Utilities.ReportSetupEvent("Copying Visual Studio samples and demos...");
        Utilities.SetNormalDirectoryAttributes(new DirectoryInfo(vsSamplesPathDest_));
        Utilities.CopyFilesRecursively(new DirectoryInfo(vsSamplesPathOrig_), new DirectoryInfo(vsSamplesPathDest_));
        Utilities.RemoveZoneIdentifier(vsSamplesPathDest_, new String[] { @".+\.sln$", @".+\.csproj$", @".+\.cs$" });

        return true;
    }

    // Starts any database previously created.
    void StartCreatedDatabase(String serverConfigPath, String serverName, String createdDbName)
    {
        Process p = null;
        String serverDir = Path.GetDirectoryName(serverConfigPath);
        String dbConnString = ScUri.MakeDatabaseUri(ScUri.GetMachineName(), serverName.ToLowerInvariant(), createdDbName.ToLowerInvariant());

        // Starting database memory management process.
        ProcessStartInfo pInfo = new ProcessStartInfo();
        pInfo.FileName = "\"" + InstallerMain.InstallationDir + "\\scpmm.exe\"";
        pInfo.Arguments = serverName.ToUpper() + "_" + createdDbName.ToUpper() + " \"" + ScUri.FromDbConnectionString(dbConnString).ToString() + "\" \"" + serverDir + "\\Logs\"";
        pInfo.UseShellExecute = false;
        pInfo.WorkingDirectory = InstallerMain.InstallationDir;

        p = Process.Start(pInfo);
        p.Close();

        // Starting database instance process.
        pInfo = new ProcessStartInfo();
        pInfo.FileName = "\"" + InstallerMain.InstallationDir + "\\scdbsw.exe\"";
        pInfo.Arguments = "\"" + serverDir + "\\Databases\\" + createdDbName + "\\" + createdDbName + ".config\" \"" + serverConfigPath + "\" -f " + serverName;
        pInfo.UseShellExecute = false;
        pInfo.WorkingDirectory = InstallerMain.InstallationDir;

        p = Process.Start(pInfo);
        p.Close();
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

        // Copying samples if needed.
        if (!CopySamplesAndDemos())
        {
            InstallerMain.ProgressIncrement();
            return;
        }

        // Adding demo to post-start.
        StartDemoPreBuilt();

        // Logging event.
        Utilities.ReportSetupEvent("Creating sample databases...");

        // By default we are installing sample database with default image size.
        String[] sampleDbNames = { /*"MyMusic"*/ };

        // Checking installation of Visual Studio plugin.
        if ((!InstallerMain.VS2012IntegrationComponent.ShouldBeInstalled()) &&
            (!InstallerMain.VS2012IntegrationComponent.IsInstalled()) &&
            (!InstallerMain.VS2013IntegrationComponent.ShouldBeInstalled()) &&
            (!InstallerMain.VS2013IntegrationComponent.IsInstalled()) &&
            (!Utilities.RunningOnBuildServer()))
        {
            sampleDbNames = new String[] { ConstantsBank.SCDemoDbName };
        }

        // Checking what server type is installed.
        String serverPath = null;
        if (InstallerMain.PersonalServerComponent.ShouldBeInstalled())
            serverPath = InstallerMain.PersonalServerComponent.ComponentPath;

        if (serverPath == null)
            return;

        //a_m//
        /*
        try
        {
            // Initializing client host interface to work with servers.
            ClientHost clientHost = new ClientHost();
            clientHost.Initialize(ServerSet.LocalServers);

            // Checking that all servers are installed properly.
            if (clientHost.Servers.Length < numServersToExpect)
            {
                throw ErrorCode.ToException(Error.SCERRINSTALLERWRONGSERVERSNUMBER,
                    "Expected number of servers: " + numServersToExpect + Environment.NewLine + "Found servers: " + clientHost.Servers.Length);
            }

            // Iterating through all servers and creating a sample database in each one.
            for (Int32 k = 0; k < sampleDbNames.Length; k++)
            {
                // Removing leading and trailing whitespace.
                String sampleDbName = sampleDbNames[k];

                // Creating the path to database sample on hard drive.
                String clientLibraryPath = @"ClientLibraries\" + sampleDbName + ".lib.zip";

                // Iterating through all servers and creating a sample database in each one.
                for (int i = 0; i < clientHost.Servers.Length; i++)
                {
                    // Checking if its a server that can be started.
                    if (clientHost.Servers[i].CanStart)
                    {
                        // Starting the server so we can fetch the info about it.
                        clientHost.Servers[i].Start(InstallerMain.InstallationBaseComponent.ComponentPath);
                        clientHost.Servers[i].Refresh();
                    }

                    // Waiting for server to start and run.
                    Int32 waitIntervals = 10;
                    while ((waitIntervals > 0) &&
                        (clientHost.Servers[i].ProcessStatus != ServerProcessStatus.Running))
                    {
                        waitIntervals--;
                        Thread.Sleep(3000);
                    }

                    // Checking that server was able to start eventually.
                    if (waitIntervals <= 0)
                        throw ErrorCode.ToException(Error.SCERRINSTALLERINTERNALPROBLEM, "Server '" + clientHost.Servers[i].DisplayName + "' was not able to start...");

                    // Checking if sample database files already exist (and if yes, not creating the same new database).
                    if (!Utilities.DirectoryContainsFilesRegex(clientHost.Servers[i].QueryServerInfo().Configuration.EnginesDirectory,
                        new String[] { sampleDbName + @".+\.sci" }))
                    {
                        // Creating database sample synchronously.
                        CreateDatabaseSynchronous(
                            clientHost.Servers[i],
                            sampleDbName,
                            sampleDbSizes[k],
                            clientLibraryPath);
                    }

                    // Starting the benchmark database.
                    if (ConstantsBank.SCDemoDbName == sampleDbName)
                    {
                        LocalServer localServer = clientHost.Servers[i] as LocalServer;
                        if (localServer != null)
                        {
                            if (localServer.Uri.EndsWith(StarcounterEnvironment.ServerNames.PersonalUser, StringComparison.InvariantCultureIgnoreCase) &&
                                InstallerMain.PersonalServerComponent.ShouldBeInstalled())
                            {
                                StartCreatedDatabase(localServer.Repository.ServerConfigurationPath, StarcounterEnvironment.ServerNames.PersonalUser, sampleDbName);
                            }
                            else if (localServer.Uri.EndsWith(StarcounterEnvironment.ServerNames.System, StringComparison.InvariantCultureIgnoreCase) &&
                                InstallerMain.SystemServerComponent.ShouldBeInstalled())
                            {
                                // System server databases should only be started directly by system server.
                                // And system server should be instructed for that by installer using special IPC.
                                //SilentStartDatabase(localServer.Repository.ServerConfigurationPath, StarcounterEnvironment.ServerNames.System, sampleDbName);
                            }
                        }
                    }
                }
            }
        }
        catch (ServerNotRunningException)
        {
            Utilities.MessageBoxInfo("One of the Starcounter servers could not be started for some reason. Skipping installation of sample databases.",
                "Starcounter server could not be started...");
        }*/

        // Updating progress.
        InstallerMain.ProgressIncrement();
    }

    // Starcounter Demo Start Menu shortcut.
    public static String GetDemoShortcutPath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            "Programs\\" + ConstantsBank.SCProductName + "\\" + ConstantsBank.SCProductName + " Demo Project.lnk");
    }

    /// <summary>
    /// Creates Starcounter Demo Start Menu shortcut.
    /// </summary>
    static void CreateDemoStartMenuShortcut(String targetPath)
    {
        // Obtaining the path to future shortcut.
        String shortcutPath = GetDemoShortcutPath();

        // Logging event..
        Utilities.ReportSetupEvent("Creating Starcounter Demo shortcut...");

        // Obtaining path to Start Menu for a current user.
        String startMenuDir = Path.GetDirectoryName(shortcutPath);

        // Creating Start Menu directory if needed.
        if (!Directory.Exists(startMenuDir))
            Directory.CreateDirectory(startMenuDir);

        // Installation directory.
        String installPath = InstallerMain.InstallationBaseComponent.ComponentPath;

        // Calling external tool to create shortcut.
        Utilities.CreateShortcut(
            targetPath,
            shortcutPath,
            " ",
            installPath,
            "Launches Starcounter Demo.",
            Path.Combine(InstallerMain.InstallationDir, ConstantsBank.SCIconFilename));
    }

    /// <summary>
    /// Deletes Start Menu shortcuts.
    /// </summary>
    public static void DeleteDemoMenuShortcut()
    {
        String shortcutPath = GetDemoShortcutPath();

        Utilities.ReportSetupEvent("Deleting Starcounter Demo shortcut...");

        if (File.Exists(shortcutPath))
            File.Delete(shortcutPath);
    }

    /// <summary>
    /// Starts Visual Studio Performance Demo.
    /// </summary>
    internal static void StartDemoInVs()
    {
        String vsNumberVersion = null,
               vsYearVersion = null;

        // Checking what VS extension was installed.
        if (InstallerMain.VS2012IntegrationComponent.ShouldBeInstalled() ||
            InstallerMain.VS2012IntegrationComponent.IsInstalled())
        {
            vsNumberVersion = "11.0";
            vsYearVersion = "2012";
        }
        else if (InstallerMain.VS2013IntegrationComponent.ShouldBeInstalled() || 
            InstallerMain.VS2013IntegrationComponent.IsInstalled()) {
            vsNumberVersion = "12.0";
            vsYearVersion = "2013";
        }
        else
        {
            return;
        }

        // Path to Visual Studio IDE executable.
        String devenvPath = Path.Combine(ConstantsBank.ProgramFilesPath,
            @"..\Program Files (x86)\Microsoft Visual Studio " + vsNumberVersion + @"\Common7\IDE\devenv.exe");

        //String slnPath = Path.Combine(InstallerMain.SamplesDemosComponent.VsSamplesPathDest, "VsSamples\\Vs" + vsYearVersion + @"\HelloWorld\HelloWorld.sln");
        String slnPath = Path.Combine(InstallerMain.SamplesDemosComponent.VsSamplesPathDest, ConstantsBank.SCDemoName + "\\Vs" + vsYearVersion + "\\" + ConstantsBank.SCDemoName + ".sln");

        // Checking if Visual Studio IDE executable exists.
        if (!File.Exists(devenvPath) || !File.Exists(slnPath))
            return;

        // Adding process to post-setup start.
        InstallerMain.AddProcessToPostStart(devenvPath, "\"" + slnPath + "\" /command \"File.OpenFile Main.cs\"");

        // Creating Start Menu shortcuts.
        CreateDemoStartMenuShortcut(slnPath);
    }

    /// <summary>
    /// Starts Pre-Built Starcounter Performance Demo.
    /// </summary>
    internal static void StartDemoPreBuilt()
    {
        // Checking Starcounter Visual Studio integration.
        if ((InstallerMain.VS2012IntegrationComponent.ShouldBeInstalled()) ||
            (InstallerMain.VS2012IntegrationComponent.IsInstalled()) ||
            (InstallerMain.VS2013IntegrationComponent.ShouldBeInstalled()) ||
            (InstallerMain.VS2013IntegrationComponent.IsInstalled()) ||
            (Utilities.RunningOnBuildServer()))
        {
            return;
        }

        // Path to demo executable.
        String preBuiltDemoPath = Path.Combine(InstallerMain.InstallationBaseComponent.ComponentPath,
            ConstantsBank.ScSamplesDemosDirName + "\\" + ConstantsBank.SCDemoName + "\\PreBuilt\\" + ConstantsBank.SCDemoName + ".exe");

        // Adding process to post-setup start.
        InstallerMain.AddProcessToPostStart(preBuiltDemoPath, "");

        // Creating Start Menu shortcuts.
        CreateDemoStartMenuShortcut(preBuiltDemoPath);
    }

    /// <summary>
    /// Cached number of remaining demo components.
    /// </summary>
    static Int32 cachedRemainingDemoComponentsNum = -1;

    /// <summary>
    /// Calculates how many demo components will be left after uninstalling
    /// those which are marked TRUE in parameters.
    /// </summary>
    static Int32 RemainingDemoComponents()
    {
        if (cachedRemainingDemoComponentsNum >= 0)
            return cachedRemainingDemoComponentsNum;

        // Obtaining the list of installed components.
        Boolean[] installedComponents = ComponentsCheck.GetListOfInstalledComponents();

        // Remember that installation base is not considered as an individual component.
        Boolean[] remainingComponents = new Boolean[ComponentsCheck.NumComponents];

        // Marking installed components.
        if (installedComponents[(Int32)ComponentsCheck.Components.PersonalServer])
            remainingComponents[(Int32)ComponentsCheck.Components.PersonalServer] = true;

        if (installedComponents[(Int32)ComponentsCheck.Components.SystemServer])
            remainingComponents[(Int32)ComponentsCheck.Components.SystemServer] = true;

        if (installedComponents[(Int32)ComponentsCheck.Components.VS2012Integration])
            remainingComponents[(Int32)ComponentsCheck.Components.VS2012Integration] = true;

        if (installedComponents[(Int32)ComponentsCheck.Components.VS2013Integration])
            remainingComponents[(Int32)ComponentsCheck.Components.VS2013Integration] = true;

        try
        {
            if (InstallerMain.PersonalServerComponent.ShouldBeRemoved())
                remainingComponents[(Int32)ComponentsCheck.Components.PersonalServer] = false;

            if (InstallerMain.VS2012IntegrationComponent.ShouldBeRemoved())
                remainingComponents[(Int32)ComponentsCheck.Components.VS2012Integration] = false;
        }
        catch
        {
            // This means that uninstall settings were not loaded properly.
        }

        // Going through the whole list and checking what components remain.
        cachedRemainingDemoComponentsNum = 0;
        for (Int32 i = 0; i < remainingComponents.Length; i++)
        {
            if (remainingComponents[i])
                cachedRemainingDemoComponentsNum++;
        }

        return cachedRemainingDemoComponentsNum;
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

        // Silently killing demo process.
        Utilities.KillDisturbingProcesses(new String[] { ConstantsBank.SCDemoName }, true);

        // Deleting the demo shortcut.
        CSamplesDemos.DeleteDemoMenuShortcut();
    }

    /// <summary>
    /// Checks if components is already installed.
    /// </summary>
    /// <returns>True if already installed.</returns>
    public override Boolean IsInstalled()
    {
        String shortcutPath = GetDemoShortcutPath();

        // Simply checking if shortcut file exists.
        if (File.Exists(shortcutPath))
            return true;

        return false;
    }

    /// <summary>
    /// Checks if component can be installed.
    /// </summary>
    /// <returns>True if can.</returns>
    public override Boolean CanBeInstalled()
    {
        return !IsInstalled();
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
        return (InstallerMain.PersonalServerComponent.ShouldBeInstalled() ||
            InstallerMain.VS2012IntegrationComponent.ShouldBeInstalled() ||
            InstallerMain.VS2013IntegrationComponent.ShouldBeInstalled());
    }

    /// <summary>
    /// Determines if this component should be uninstalled
    /// in this session.
    /// </summary>
    /// <returns>True if component should be uninstalled.</returns>
    public override Boolean ShouldBeRemoved()
    {
        return (RemainingDemoComponents() <= 0);
    }
}
}