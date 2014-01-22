using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Starcounter;
using System.IO;
using Starcounter.Internal;
using Starcounter.InstallerEngine.VsSetup;
using Microsoft.Win32;

namespace Starcounter.InstallerEngine
{
    public static class ConstantsBank
    {
        public const String SettingsSection_Root = "StarcounterSetupSettings";

        // Sections in settings file.
        public const String SettingsSection_Install = "StarcounterInstallationSettings";

        // Setup settings constants.
        public const String Setting_InstallPersonalServer = "InstallPersonalServer";
        public const String Setting_InstallSystemServer = "InstallSystemServer";

        public const String Setting_InstallVS2010Integration = "InstallVS2010Integration";
        public const String Setting_InstallVS2012Integration = "InstallVS2012Integration";
        public const String Setting_InstallVS2013Integration = "InstallVS2013Integration";

        public const String Setting_SendUsageAndCrashReports = "SendUsageAndCrashReports";

        public const String Setting_CreatePersonalServerShortcuts = "CreatePersonalServerShortcuts";
        public const String Setting_AddStarcounterToStartMenu = "AddStarcounterToStartMenu";

        public const String Setting_PersonalServerPath = "PersonalServerPath";
        public const String Setting_DefaultPersonalServerUserHttpPort = StarcounterConstants.NetworkPorts.DefaultPersonalServerUserHttpPort_String;
        public const String Setting_DefaultPersonalServerSystemHttpPort = StarcounterConstants.NetworkPorts.DefaultPersonalServerSystemHttpPort_String;
        public const String Setting_DefaultPersonalPrologSqlProcessPort = StarcounterConstants.NetworkPorts.DefaultPersonalPrologSqlProcessPort_String;

        public const String Setting_SystemServerPath = "SystemServerPath";
        public const String Setting_DefaultSystemServerUserHttpPort = StarcounterConstants.NetworkPorts.DefaultSystemServerUserHttpPort_String;
        public const String Setting_DefaultSystemServerSystemHttpPort = StarcounterConstants.NetworkPorts.DefaultSystemServerSystemHttpPort_String;
        public const String Setting_DefaultSystemPrologSqlProcessPort = StarcounterConstants.NetworkPorts.DefaultSystemPrologSqlProcessPort_String;

        public const String GatewayXmlStatisticsPort = "GatewayStatisticsPort";

        public const String Setting_True = "True";

        // Uninstall settings constants.
        public const String SettingsSection_Uninstall = "StarcounterUninstallationSettings";
        public const String Setting_RemovePersonalServer = "RemovePersonalServer";
        public const String Setting_RemoveSystemServer = "RemoveSystemServer";
        public const String Setting_RemoveVS2010Integration = "RemoveVS2010Integration";
        public const String Setting_RemoveVS2012Integration = "RemoveVS2012Integration";
        public const String Setting_RemoveVS2013Integration = "RemoveVS2013Integration";

        // Other constants.
        public const String SCIconFilename = "sc.ico";
        public const String SCAdminIconFilename = "sc_logo.ico";
        
        public const String SilentArg = "Silent";
        public const String DontCheckOtherInstancesArg = "DontCheckOtherInstances";

        public const String ScGlobalSettingsXmlName = "SetupSettings.xml";
        public const String ScGUISetupXmlName = "GUISetupSettings.xml";
        public const String ScGatewayConfigName = "scnetworkgateway.xml";

        public static String ScPostSetupFilePath = Path.Combine(Environment.GetEnvironmentVariable("TEMP", EnvironmentVariableTarget.User), "ScPostSetupTemp.txt");
        public static String ScStartDemosTemp = Path.Combine(Environment.GetEnvironmentVariable("TEMP", EnvironmentVariableTarget.User), "ScStartDemosTemp.txt");
        public const String ScPostSetupFileArgsSeparator = "###";

        internal const String SCDemoName = "SimpleBenchmark";
        internal const String SCAdminDatabaseName = "Administrator";
        internal const String SCDemoDbName = "StarcounterBenchmarkDemoDb";
        internal const String ScSamplesDemosDirName = "SamplesAndDemos";
        internal const String ScLogFileName = "ScSetup.log";
        internal const String SCVersionFileName = StarcounterEnvironment.FileNames.VersionInfoFileName;
        internal const String SCSilentSetupParam = "silent";
        internal const String SCUninstallParam = "uninstall";
        internal const String SCCleanupParam = "cleanup";
        internal const String SCVSSafeImportsKey = "Starcounter.MsBuild";
        internal const String ScExceptionAssistantContentFileName = "StarcounterExceptionAssistantContent.xml";

        // Constants defined in and fetched from the shared framework assembly
        internal static String SCServiceExeName { get { return StarcounterConstants.ProgramNames.ScService + ".exe"; } }
        public static String SCEnvVariableName { get { return StarcounterEnvironment.VariableNames.InstallationDirectory; } }
        public static String SCEnvVariableDefaultServer { get { return StarcounterEnvironment.VariableNames.DefaultServer; } }
        public static String SCEnvVariableDefaultPersonalPort { get { return StarcounterEnvironment.VariableNames.DefaultServerPersonalPort; } }
        public static String SCEnvVariableDefaultSystemPort { get { return StarcounterEnvironment.VariableNames.DefaultServerSystemPort; } }
        public static String SCPersonalServerName { get { return StarcounterEnvironment.ServerNames.PersonalServer; } }
        public static String SCSystemServerName { get { return StarcounterEnvironment.ServerNames.SystemServer; } }

        public const String SCInstallerGUI = "Starcounter-Setup";
        public const String SCInstallerEngine = "Starcounter.InstallerEngine";
        public const String SCProductName = StarcounterConstants.ProgramNames.ProductName;

        public const String SCPublicAssembliesDir = "Public Assemblies";

        public static readonly String ProgramFilesPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles); //Environment.GetEnvironmentVariable("ProgramW6432");

        internal const String Registry32BitUninstallPath = @"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall";
        internal const String Registry64BitUninstallPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

        // Defines the path to the key holding the "installed product"
        // information in the registry representing an installed integration
        // under the current users VS 2012 config (and in this case, the
        // Starcounter key in particular).
        internal const String RegistryVS2012StarcounterInstalledProductKey = @"SOFTWARE\Microsoft\VisualStudio\11.0_Config\InstalledProducts\Starcounter";

        /// <summary>
        /// Gets the path to the Visual Studio 2012 (11.0) installation directory.
        /// </summary>
        internal static string VS2012InstallationDirectory { get { return VSIntegration.GetVisualStudioInstallationDirectory("11.0"); } }

        /// <summary>
        /// Gets the path to the Visual Studio 2013 (12.0) installation directory.
        /// </summary>
        internal static string VS2013InstallationDirectory { get { return VSIntegration.GetVisualStudioInstallationDirectory("12.0"); } }

        /// <summary>
        /// Gets the path to the Visual Studio 2012 (11.0) IDE directory.
        /// </summary>
        internal static string VS2012IDEDirectory { get { return Path.Combine(VS2012InstallationDirectory, @"Common7\IDE"); } }

        /// <summary>
        /// Gets the path to the Visual Studio 2013 (12.0) IDE directory.
        /// </summary>
        internal static string VS2013IDEDirectory { get { return Path.Combine(VS2013InstallationDirectory, @"Common7\IDE"); } }

        /// <summary>
        /// Gets the path to the Visual Studio 2012 (11.0) exception assistant content
        /// directory.
        /// </summary>
        /// <remarks>
        /// The path returned here is the path to the English, standard directory, i.e.
        /// the result is not localized.
        /// </remarks>
        internal static string VS2012ExceptionAssistantDirectory { get { return Path.Combine(VS2012IDEDirectory, @"ExceptionAssistantContent\1033"); } }

        /// <summary>
        /// Gets the path to the Visual Studio 2012 (11.0) IDE executable file (i.e. "devenv.exe")
        /// </summary>
        internal static string VS2012DevEnvPath { get { return Path.Combine(VS2012IDEDirectory, "devenv.exe"); } }

        /// <summary>
        /// Gets the path to the Visual Studio 2013 (12.0) IDE executable file (i.e. "devenv.exe")
        /// </summary>
        internal static string VS2013DevEnvPath { get { return Path.Combine(VS2013IDEDirectory, "devenv.exe"); } }

        /// <summary>
        /// Gets the name of the VSIX installer executable file (i.e. currently "VSIXInstaller.exe").
        /// </summary>
        internal const string VSIXInstallerEngineExecutable = "VSIXInstaller.exe";
    }
}
