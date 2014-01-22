using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Starcounter.Tools;

namespace Starcounter.InstallerEngine
{
    public class UninstallEngine
    {
        // Map of uninstall settings and their values.
        static Dictionary<String, String> uninstallSettings;

        /// <summary>
        /// Loads uninstallation settings.
        /// </summary>
        /// <param name="configPath">Path to settings file.</param>
        internal static void LoadUninstallationSettings(String configPath)
        {
            // Overwriting settings if already been loaded.
            uninstallSettings = new Dictionary<String, String>();
            SettingsLoader.LoadConfigFile(configPath, ConstantsBank.SettingsSection_Uninstall, uninstallSettings);
        }

        /// <summary>
        /// Compares uninstall setting value with provided one.
        /// </summary>
        /// <param name="settingName">Name of the setting.</param>
        /// <param name="compareWith">String to compare with.</param>
        /// <returns>True if values are the same.</returns>
        internal static Boolean UninstallationSettingCompare(String settingName, String compareWith)
        {
            String setting = SettingsLoader.GetSettingValue(settingName, uninstallSettings);

            return setting.Equals(compareWith, StringComparison.CurrentCultureIgnoreCase);
        }

        /// <summary>
        /// Cached list of installed components and its number.
        /// </summary>
        static Int32 cachedRemainingComponentsNum = -1;

        /// <summary>
        /// Simply resets corresponding value so that
        /// components are obtained from scratch.
        /// </summary>
        internal static void ResetCachedRemainingComponentsNum()
        {
            cachedRemainingComponentsNum = -1;
        }

        /// <summary>
        /// Calculates how many components will be left after uninstalling
        /// those which are marked TRUE in parameters.
        /// </summary>
        public static Int32 RemainingComponents()
        {
            if (cachedRemainingComponentsNum >= 0)
                return cachedRemainingComponentsNum;

            // Obtaining the list of installed components.
            Boolean[] installedComponents = ComponentsCheck.GetListOfInstalledComponents();

            // Remember that installation base is not considered as an individual component.
            Boolean[] remainingComponents = new Boolean[ComponentsCheck.NumComponents];

            // Marking installed components.
            if (installedComponents[(Int32) ComponentsCheck.Components.PersonalServer])
                remainingComponents[(Int32)ComponentsCheck.Components.PersonalServer] = true;

            if (installedComponents[(Int32) ComponentsCheck.Components.SystemServer])
                remainingComponents[(Int32)ComponentsCheck.Components.SystemServer] = true;

            if (installedComponents[(Int32)ComponentsCheck.Components.VS2012Integration])
                remainingComponents[(Int32)ComponentsCheck.Components.VS2012Integration] = true;

            try
            {
                if (InstallerMain.PersonalServerComponent.ShouldBeRemoved())
                    remainingComponents[(Int32)ComponentsCheck.Components.PersonalServer] = false;

                if (InstallerMain.VS2012IntegrationComponent.ShouldBeRemoved())
                    remainingComponents[(Int32)ComponentsCheck.Components.VS2012Integration] = false;

                if (InstallerMain.VS2013IntegrationComponent.ShouldBeRemoved())
                    remainingComponents[(Int32)ComponentsCheck.Components.VS2013Integration] = false;
            }
            catch
            {
                // This means that uninstall settings were not loaded properly.
            }

            // Going through the whole list and checking what components remain.
            cachedRemainingComponentsNum = 0;
            for (Int32 i = 0; i < remainingComponents.Length; i++)
            {
                if (remainingComponents[i])
                    cachedRemainingComponentsNum++;
            }

            return cachedRemainingComponentsNum;
        }

        /// <summary>
        /// Delete installation folder if satisfying.
        /// </summary>
        public static void DeleteInstallationDir(Boolean dontDelete)
        {
            // If we need to postpone the deletion.
            if (dontDelete || InstallerMain.DontDeleteInstallDir)
                return;

            Utilities.ReportSetupEvent("Deleting installation directory...");

            // Checking that no remaining components left.
            if (!ComponentsCheck.AnyComponentsExist())
            {
                // Checking that no important files are inside.
                if (Utilities.DirectoryContainsFilesRegex(InstallerMain.InstallationDir, new String[] { @".+\.sci", @".+\.engine\.config", @".+\.server\.config" }))
                {
                    if (!InstallerMain.SilentFlag)
                    {
                        Utilities.MessageBoxInfo("Your installation directory \"" + InstallerMain.InstallationDir + "\" was not deleted since it contains probably important files." +
                            Environment.NewLine + "Please check that no Starcounter servers/databases are located under your installation directory.",
                            "Installation directory is left untouched.", true);
                    }

                    return; // If any important files exist - we are not deleting directory.
                }

                // Making all files deletable.
                Utilities.SetNormalDirectoryAttributes(new DirectoryInfo(InstallerMain.InstallationDir));

                // Checking if installer runs from the same directory as installation.
                if (Utilities.EqualDirectories(System.Windows.Forms.Application.StartupPath, InstallerMain.InstallationDir))
                {
                    // Remove the directory using scheduled remove directory task.
                    Process rmdirCmd = new Process();
                    try
                    {
                        rmdirCmd.StartInfo.FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
                        rmdirCmd.StartInfo.Arguments = "/C TIMEOUT 3 /NOBREAK & RMDIR \"" + InstallerMain.InstallationDir + "\" /S /Q";
                        rmdirCmd.StartInfo.UseShellExecute = false;
                        rmdirCmd.StartInfo.CreateNoWindow = true;
                        rmdirCmd.Start();
                    }
                    finally
                    {
                        rmdirCmd.Close();
                    }
                }
                else
                {
                    // Removing directory directly.
                    Utilities.ForceDeleteDirectory(new DirectoryInfo(InstallerMain.InstallationDir));
                }
            }
        }

        /// <summary>
        /// Rollback the installation when it fails.
        /// </summary>
        public static void RollbackInstallation()
        {
            Utilities.ReportSetupEvent("Running installation rollback...");

            // Setting rollback flag.
            rollbackSetting = true;

            // Killing processes that can remain after installation.
            Utilities.KillDisturbingProcesses(InstallerMain.ScProcessesList, true);

            // Reseting progress.
            InstallerMain.ResetProgressStep();

            // Looking for components that were tried to be installed.
            if (InstallerMain.PersonalServerComponent.ShouldBeInstalled()) InstallerMain.AddComponentToProgress();
            if (InstallerMain.VS2012IntegrationComponent.ShouldBeInstalled()) InstallerMain.AddComponentToProgress();
            if (InstallerMain.VS2013IntegrationComponent.ShouldBeInstalled()) InstallerMain.AddComponentToProgress();

            // Getting percentage step value.
            InstallerMain.CalculateProgressStep();

            // Running uninstall core.
            UninstallEngine.UninstallAllComponents();

            // Adding last progress update.
            InstallerMain.ProgressFinished();

            // Logging event.
            Utilities.ReportSetupEvent("Rollback process finished...");

            // Resetting rollback flag.
            rollbackSetting = false;
        }

        /// <summary>
        /// Core Starcounter uninstall function.
        /// </summary>
        internal static void UninstallAllComponents()
        {
            // Removing each component one by one.
            foreach (CComponentBase comp in InstallerMain.AllComponentsToUninstall)
            {
                try
                {
                    comp.Uninstall();
                }
                catch (Exception generalException)
                {
                    // Re-throwing the exception if not cleanup.
                    if (!completeCleanupSetting)
                        throw generalException;

                    // Silently logging the exception.
                    Utilities.LogMessage(generalException.ToString());
                }
            }
        }

        static Boolean completeCleanupSetting = false;
        public static Boolean CompleteCleanupSetting
        {
            get
            {
                return completeCleanupSetting;
            }
        }

        static Boolean rollbackSetting = false;
        public static Boolean RollbackSetting
        {
            get
            {
                return rollbackSetting;
            }
        }

        /// <summary>
        /// Entry function for uninstalling Starcounter (which calls the core function).
        /// </summary>
        internal static void UninstallStarcounter(Boolean cleanup)
        {
            completeCleanupSetting = false;

            // Installation part of the settings file should already be loaded.
            if (cleanup)
            {
                // We are in cleanup mode.
                completeCleanupSetting = true;

                // Reseting progress steps.
                InstallerMain.ResetProgressStep();

                // Uninstalling all five components.
                InstallerMain.AddComponentsToProgress(6);

                // Getting percentage step value.
                InstallerMain.CalculateProgressStep();
            }
            else
            {
                // Reseting progress steps.
                InstallerMain.ResetProgressStep();

                // Loading settings.
                if (InstallerMain.PersonalServerComponent.ShouldBeRemoved()) InstallerMain.AddComponentToProgress();
                if (InstallerMain.VS2012IntegrationComponent.ShouldBeRemoved()) InstallerMain.AddComponentToProgress();
                if (InstallerMain.VS2013IntegrationComponent.ShouldBeRemoved()) InstallerMain.AddComponentToProgress();

                // Getting percentage step value.
                InstallerMain.CalculateProgressStep();
            }

            // Calling the core uninstall function.
            UninstallAllComponents();

            // Adding last progress update.
            InstallerMain.ProgressFinished();

            // Logging event.
            Utilities.ReportSetupEvent("Uninstall process finished...");

            // Resetting cleanup flag.
            completeCleanupSetting = false;
        }
    }
}
