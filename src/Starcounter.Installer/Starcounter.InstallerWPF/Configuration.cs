//#define SIMULATE_INSTALLATION // REMOVE!
using System;
using System.Text;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.IO;
using Starcounter.InstallerWPF.Properties;
using System.Reflection;
using System.Collections.Generic;
using System.Windows.Data;
using System.Windows;
using System.Collections.ObjectModel;
using System.Collections;
using Starcounter.InstallerWPF.Pages;
using Starcounter.InstallerWPF.Components;
using System.Threading;
using Starcounter.Controls;
using Starcounter.InstallerEngine;
using Starcounter.Internal;
using System.IO.Compression;
using System.Xml;
using System.Diagnostics;

namespace Starcounter.InstallerWPF {
    public class Configuration : INotifyPropertyChanged {
        #region Properties

        /// <summary>
        /// Identifies common path for installation: product name + version.
        /// </summary>
        public static string StarcounterCommonPath;

        private Hashtable _Components = new Hashtable();
        public Hashtable Components {
            get { return this._Components; }
        }

        /// <summary>
        /// Gets a value indicating whether this instance can execute.
        /// </summary>
        /// <value>
        /// <c>true</c> if there is something to execute; otherwise, <c>false</c>.
        /// </value>
        public bool CanExecute {
            get {
                bool bSomethingToExecute = false;
                IDictionaryEnumerator _enumerator = this.Components.GetEnumerator();

                while (_enumerator.MoveNext()) {
                    BaseComponent component = _enumerator.Value as BaseComponent;

                    if (component != null && component.ExecuteCommand && component.IsExecuteCommandEnabled) {
                        bSomethingToExecute = true;
                        break;
                    }
                }

                return bSomethingToExecute;
            }

        }
        #endregion


        /// <summary>
        /// 
        /// </summary>
        private SetupOptions _SetupOptions;
        /// <summary>
        /// 
        /// </summary>
        public SetupOptions SetupOptions {
            get {

                return this._SetupOptions;
            }
            private set {
                this._SetupOptions = value;
            }
        }


        public Configuration() {
            StarcounterCommonPath = System.IO.Path.Combine(ConstantsBank.SCProductName, CurrentVersion.Version);
        }

        /// <summary>
        /// Sets the default values.
        /// </summary>
        /// <param name="setupOptions">The setup options.</param>
        public void SetDefaultValues(SetupOptions setupOptions) {

            this.SetupOptions = setupOptions;

            Samples samples = this.GetComponent(Samples.Identifier) as Samples;

            switch (setupOptions) {
                case SetupOptions.Ask:
                    break;

                case SetupOptions.None:
                    break;

                case SetupOptions.Install:

                    if (samples != null) {
                        samples.SetCanBeInstalled(true);
                        samples.SetCanBeUnInstalled(true);
                    }

                    break;

                case SetupOptions.AddComponents:

                    if (samples != null) {
                        samples.SetCanBeInstalled(true);
                    }

                    break;

                case SetupOptions.RemoveComponents:

                    if (samples != null) {
                        samples.SetCanBeUnInstalled(false);
                    }

                    break;

                case SetupOptions.Uninstall:

                    if (samples != null) {
                        samples.SetCanBeInstalled(true);
                        samples.SetCanBeUnInstalled(true);
                    }

                    break;

            }

        }

        /// <summary>
        /// Checking if we are either installing first time or
        /// adding new components to existing installation.
        /// </summary>
        /// <returns>True if yes :)</returns>
        Boolean InstallingOrAddingComponents() {
            bool installing = false;

            // Check if we are installing or uninstalling
            IDictionaryEnumerator item = this.Components.GetEnumerator();
            while (item.MoveNext()) {
                BaseComponent component = item.Value as BaseComponent;

                if (component.Command == ComponentCommand.None || component.Command == ComponentCommand.Update) {
                    // TODO: We do not support "Updates" yet.
                    continue;
                }

                if (component.ExecuteCommand == false) {
                    // Ignore commands that is not to be "Executed"
                    continue;
                }
                if (component.Command == ComponentCommand.Install) {
                    installing = true;
                    break;
                }

                if (component.Command == ComponentCommand.Uninstall) {
                    installing = false;
                    break;
                }
            }

            return installing;
        }

        private void GenerateSetupXmlFile() {
            // Checking if we are installing or adding components.
            bool installingOrAdding = InstallingOrAddingComponents();

            InstallationBase installationBase = this.GetComponent(InstallationBase.Identifier) as InstallationBase;
            if (installationBase == null || string.IsNullOrEmpty(installationBase.Path)) {
                MessageBox.Show("Corrupt Installation", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Getting installation path.
            String installationPath = installationBase.Path;
            String configPath = Path.Combine(installationPath, ConstantsBank.ScGUISetupXmlName);

            // Checking if previous installation GUI settings file exists.
            if (File.Exists(configPath))
                File.Delete(configPath);

            XmlDocument xmlDoc = new XmlDocument();
            XmlElement rootElem = xmlDoc.CreateElement(ConstantsBank.SettingsSection_Root);

            if (installingOrAdding) {
                XmlElement subRootElem = xmlDoc.CreateElement(ConstantsBank.SettingsSection_Install);
                rootElem.AppendChild(subRootElem);

                XmlElement elem = xmlDoc.CreateElement(ConstantsBank.Setting_AddStarcounterToStartMenu);
                elem.InnerText = installationBase.AddToStartMenu.ToString();
                subRootElem.AppendChild(elem);

                // PersonalServer
                PersonalServer personalServer = this.GetComponent(PersonalServer.Identifier) as PersonalServer;
                elem = xmlDoc.CreateElement(ConstantsBank.Setting_InstallPersonalServer);
                elem.InnerText = personalServer.ExecuteCommand.ToString();
                subRootElem.AppendChild(elem);

                elem = xmlDoc.CreateElement(ConstantsBank.Setting_PersonalServerPath);
                elem.InnerText = personalServer.Path;
                subRootElem.AppendChild(elem);

                elem = xmlDoc.CreateElement(ConstantsBank.Setting_DefaultPersonalServerUserHttpPort);
                elem.InnerText = personalServer.DefaultUserHttpPort.ToString();
                subRootElem.AppendChild(elem);

                elem = xmlDoc.CreateElement(ConstantsBank.Setting_DefaultPersonalServerSystemHttpPort);
                elem.InnerText = personalServer.DefaultSystemHttpPort.ToString();
                subRootElem.AppendChild(elem);

                elem = xmlDoc.CreateElement(ConstantsBank.Setting_DefaultPersonalPrologSqlProcessPort);
                elem.InnerText = personalServer.DefaultPrologSqlProcessPort.ToString();
                subRootElem.AppendChild(elem);

                // Send usage statistics and crash reports
                elem = xmlDoc.CreateElement(ConstantsBank.Setting_SendUsageAndCrashReports);
                elem.InnerText = personalServer.SendUsageAndCrashReports.ToString();
                subRootElem.AppendChild(elem);

                // Personal server Desktop shortcuts.
                elem = xmlDoc.CreateElement(ConstantsBank.Setting_CreatePersonalServerShortcuts);
                elem.InnerText = "True";
                subRootElem.AppendChild(elem);


                // SystemServer
                SystemServer systemServer = this.GetComponent(SystemServer.Identifier) as SystemServer;
                elem = xmlDoc.CreateElement(ConstantsBank.Setting_InstallSystemServer);
                elem.InnerText = systemServer.ExecuteCommand.ToString();
                subRootElem.AppendChild(elem);

                elem = xmlDoc.CreateElement(ConstantsBank.Setting_SystemServerPath);
                elem.InnerText = systemServer.Path;
                subRootElem.AppendChild(elem);

                elem = xmlDoc.CreateElement(ConstantsBank.Setting_DefaultSystemServerUserHttpPort);
                elem.InnerText = systemServer.DefaultUserHttpPort.ToString();
                subRootElem.AppendChild(elem);

                elem = xmlDoc.CreateElement(ConstantsBank.Setting_DefaultSystemServerSystemHttpPort);
                elem.InnerText = systemServer.DefaultSystemHttpPort.ToString();
                subRootElem.AppendChild(elem);

                elem = xmlDoc.CreateElement(ConstantsBank.Setting_DefaultSystemPrologSqlProcessPort);
                elem.InnerText = systemServer.DefaultPrologSqlProcessPort.ToString();
                subRootElem.AppendChild(elem);

                // VisualStudio2012
                VisualStudio2012Integration visualStudio2012Integration = this.GetComponent(VisualStudio2012Integration.Identifier) as VisualStudio2012Integration;
                elem = xmlDoc.CreateElement(ConstantsBank.Setting_InstallVS2012Integration);
                elem.InnerText = visualStudio2012Integration.ExecuteCommand.ToString();
                subRootElem.AppendChild(elem);

                // VisualStudio2013
                VisualStudio2013Integration visualStudio2013Integration = this.GetComponent(VisualStudio2013Integration.Identifier) as VisualStudio2013Integration;
                elem = xmlDoc.CreateElement(ConstantsBank.Setting_InstallVS2013Integration);
                elem.InnerText = visualStudio2013Integration.ExecuteCommand.ToString();
                subRootElem.AppendChild(elem);

            }
            else {
                XmlElement subRootElem = xmlDoc.CreateElement(ConstantsBank.SettingsSection_Uninstall);
                rootElem.AppendChild(subRootElem);

                // PersonalServer
                PersonalServer personalServer = this.GetComponent(PersonalServer.Identifier) as PersonalServer;
                XmlElement elem = xmlDoc.CreateElement(ConstantsBank.Setting_RemovePersonalServer);
                elem.InnerText = personalServer.ExecuteCommand.ToString();
                subRootElem.AppendChild(elem);

                // SystemServer
                SystemServer systemServer = this.GetComponent(SystemServer.Identifier) as SystemServer;
                elem = xmlDoc.CreateElement(ConstantsBank.Setting_RemoveSystemServer);
                elem.InnerText = systemServer.ExecuteCommand.ToString();
                subRootElem.AppendChild(elem);

                // VisualStudio2012Integration
                VisualStudio2012Integration visualStudio2012Integration = this.GetComponent(VisualStudio2012Integration.Identifier) as VisualStudio2012Integration;
                elem = xmlDoc.CreateElement(ConstantsBank.Setting_RemoveVS2012Integration);
                elem.InnerText = visualStudio2012Integration.ExecuteCommand.ToString();
                subRootElem.AppendChild(elem);


                // VisualStudio2013Integration
                VisualStudio2013Integration visualStudio2013Integration = this.GetComponent(VisualStudio2013Integration.Identifier) as VisualStudio2013Integration;
                elem = xmlDoc.CreateElement(ConstantsBank.Setting_RemoveVS2013Integration);
                elem.InnerText = visualStudio2013Integration.ExecuteCommand.ToString();
                subRootElem.AppendChild(elem);

            }

            // Saving setup setting to file.
            xmlDoc.AppendChild(xmlDoc.CreateXmlDeclaration("1.0", "UTF-8", null));
            xmlDoc.AppendChild(rootElem);
            xmlDoc.Save(configPath);
        }

        private BaseComponent GetComponent(string identifier) {
            IDictionaryEnumerator item = this.Components.GetEnumerator();
            while (item.MoveNext()) {
                BaseComponent component = item.Value as BaseComponent;
                if (string.Equals(component.ComponentIdentifier, identifier)) {
                    return component;
                }

            }
            return null;
        }

        // Data stream of the embedded ZIP archive.
        static Stream archiveZipStream = null;

        // Reads once Zip archive from embedded resources and returns its data stream.
        public static Stream ArchiveZipStream {
            get {
                // Checking if we have already loaded the archive.
                if (archiveZipStream != null)
                    return archiveZipStream;

                // Loading Zip archive from embedded resources from scratch.
                String resourceName = "Starcounter.InstallerWPF.resources.Archive.zip";
                Stream memStream = Assembly.GetEntryAssembly().GetManifestResourceStream(resourceName);
                if (memStream == null)
                    throw new FileNotFoundException("Archive.zip package can't be found as an embedded resource.");

                return memStream;
            }
        }

        /// <summary>
        /// Calls installer engine for core installation functionality.
        /// </summary>
        public void RunInstallerEngine(
            EventHandler<Utilities.InstallerProgressEventArgs> progressCallback,
            EventHandler<Utilities.MessageBoxEventArgs> messageboxCallback) {
            bool installingOrAdding = InstallingOrAddingComponents();
            String[] args = null;

            // Obtaining installation path.
            InstallationBase installationBase = this.GetComponent(InstallationBase.Identifier) as InstallationBase;
            String installationPath = installationBase.Path;

            // Checking installation directory.
            if (!Directory.Exists(installationPath))
                Directory.CreateDirectory(installationPath);

            // Setting current working directory.
            Environment.CurrentDirectory = installationPath;

            // Checking if we are installing.
            if (installingOrAdding) {
                // Checking if its first time installation so we copy binaries.
                if (!installationBase.IsInstalled) {
                    // Getting directory from where installer EXE is running.
                    String currentDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);

                    // Checking if we are in developer mode.
                    if (File.Exists(Path.Combine(currentDirectory, ConstantsBank.SCInstallerEngine + ".dll"))) {
                        // Checking if we are not installing in the same directory as we run this installer from.
                        if (!Utilities.EqualDirectories(installationPath, currentDirectory)) {
                            // Copying all files to destination.
                            Utilities.CopyFilesRecursively(new DirectoryInfo(currentDirectory), new DirectoryInfo(installationPath));
                        }
                    }
                    else {
                        // Checking if there are any files in the target installation directory.
                        if (Utilities.DirectoryIsNotEmpty(new DirectoryInfo(installationPath))) {
                            // Setting normal attributes for all files and folders in installation directory.
                            Utilities.SetNormalDirectoryAttributes(new DirectoryInfo(installationPath));

                            // Asking if user wants to delete installation path before proceeding.
                            Utilities.MessageBoxEventArgs messageBoxEventArgs = new Utilities.MessageBoxEventArgs(
                                "Installation path '" + installationPath + "' is occupied. Do you want to delete existing directory? ALL DATA IN THIS DIRECTORY WILL BE LOST!", "Installation path is occupied...",
                                WpfMessageBoxButton.YesNo, WpfMessageBoxImage.Exclamation, WpfMessageBoxResult.No);
                            messageboxCallback(this, messageBoxEventArgs);

                            // Obtaining user's decision.
                            WpfMessageBoxResult result = messageBoxEventArgs.MessageBoxResult;

                            // User confirmed to delete previous installation directory.
                            if (result != WpfMessageBoxResult.Yes)
                                throw ErrorCode.ToException(Error.SCERRINSTALLERABORTED, "User has canceled file copy process.");

                            // Removing directory.
                            Utilities.ForceDeleteDirectory(new DirectoryInfo(installationPath));

                            // Looping until directory is empty.
                            Int32 cleaningAttempts = 10;
                            while (Utilities.DirectoryIsNotEmpty(new DirectoryInfo(installationPath)) && (cleaningAttempts > 0)) {
                                Thread.Sleep(1000);
                                cleaningAttempts--;
                            }

                            // Checking if directory is still not empty.
                            if (cleaningAttempts <= 0)
                                throw ErrorCode.ToException(Error.SCERRINSTALLERABORTED, "Installation path '" + installationPath + "' is occupied and can not be cleaned. Please check any locking processes.");
                        }

                        // Extracting all files to installation directory, and overwriting old files.
                        using (ZipArchive zipArchive = new ZipArchive(ArchiveZipStream, ZipArchiveMode.Read)) {
                            zipArchive.ExtractToDirectory(installationPath);
                            /*foreach (ZipArchiveEntry entry in zipArchive.Entries)
                            {
                                entry.ExtractToFile(Path.Combine(installationPath, entry.FullName), true);
                            }*/
                        }
                    }
                }
            }
            else {
                // Creating new uninstall file.
                args = new String[] { "--uninstall" };
            }

            // Creating new installation file.
            GenerateSetupXmlFile();

            String targetSetupXmlFile = Path.Combine(installationPath, ConstantsBank.ScGUISetupXmlName);

            // Running core setup function.
            InstallerMain.StarcounterSetup(args, installationPath, targetSetupXmlFile, progressCallback, messageboxCallback);
        }

        /// <summary>
        /// Executes the settings.
        /// Note, This is not done in the Main thread!
        /// </summary>
        public void ExecuteSettings(
            EventHandler<Utilities.InstallerProgressEventArgs> progressCallback,
            EventHandler<Utilities.MessageBoxEventArgs> messageboxCallback) {
            Utilities.InstallerProgressEventArgs args = new Utilities.InstallerProgressEventArgs();
            try {

                // Simulate installation....
#if SIMULATE_INSTALLATION
                for (int i = 0; i < 100; i++)
                {
                    // Simulate progress
                    args.Progress++;
                    args.Text = "Installing " + i + Environment.NewLine + "item";
                    if (progressCallback != null)
                    {
                        progressCallback(this, args);
                    }

                    // Simulate question
                    if (i == 50)
                    {
                        if (messageboxCallback != null)
                        {

                            string message = "Test link in message box http://www.starcounter.com " + Environment.NewLine + "Do you want to cancel installation?";

                            InstallerEngine.Utilities.MessageBoxEventArgs messageBoxEventArgs = new InstallerEngine.Utilities.MessageBoxEventArgs(message, "Installer", WpfMessageBoxButton.YesNo, WpfMessageBoxImage.Question, WpfMessageBoxResult.No);
                            //MessageBoxEventArgs messageBoxEventArgs = new MessageBoxEventArgs("Do you want to cancel?", "Installer", WpfMessageBoxButton.YesNo, WpfMessageBoxImage.Question, WpfMessageBoxResult.No);
                            messageboxCallback(this, messageBoxEventArgs);

                            WpfMessageBoxResult result = messageBoxEventArgs.MessageBoxResult;

                            if (result == WpfMessageBoxResult.Yes)
                            {
                                throw new InstallerEngine.InstallerAbortedException("User canceled");
                            }
                        }
                    }

                    // Simulate question
                    if (i == 80)
                    {
                        if (messageboxCallback != null)
                        {
                            InstallerEngine.Utilities.MessageBoxEventArgs messageBoxEventArgs = new InstallerEngine.Utilities.MessageBoxEventArgs("Some feedback information", "Installer", WpfMessageBoxButton.OK, WpfMessageBoxImage.Information);
                            messageboxCallback(this, messageBoxEventArgs);
                        }
                    }

                    Thread.Sleep(10);   // TODO: Remove delay simulation
                }
#else
                // Simply running installation engine with provided callbacks.
                RunInstallerEngine(progressCallback, messageboxCallback);

#endif

            }
            catch (Exception e) {
                throw e;
            }

        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string fieldName) {
            if (PropertyChanged != null) {
                PropertyChanged(this, new PropertyChangedEventArgs(fieldName));
            }
        }

        #endregion

    }

    /// <summary>
    /// Component action command
    /// </summary>
    public enum ComponentCommand {
        /// <summary>
        /// No action is taken
        /// </summary>
        None,
        /// <summary>
        /// Installation command
        /// </summary>
        Install,
        /// <summary>
        /// Uninstallation command
        /// </summary>
        Uninstall,
        /// <summary>
        /// Update command
        /// </summary>
        Update
    }
}
