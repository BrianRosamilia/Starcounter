
using System;
using System.Diagnostics;
using System.IO;

namespace Starcounter.InstallerEngine.VsSetup {
    /// <summary>
    /// Provides the principal entrypoints for the installing and
    /// uninstalling of the Starcounter extension.
    /// </summary>
    public static class VSInstaller {
        /// <summary>
        /// Installs the Starcounter VS extension in VS 2012.
        /// </summary>
        /// <param name="binDirectory">Full path to the Starcounter
        /// installation folder.</param>
        public static void InstallVs2012(string binDirectory) {
            InstallUsingVSIXInstaller(
                Path.Combine(ConstantsBank.VS2012IDEDirectory, ConstantsBank.VSIXInstallerEngineExecutable),
                Path.Combine(binDirectory, VSIXPackageInfo.VS2012.FileName),
                VisualStudioVersion.VS2012.BuildNumber,
                "Pro"
                );
        }

        /// <summary>
        /// Uninstalls the Starcounter VS extension from VS 2012.
        /// </summary>
        /// <param name="binDirectory">Full path to the Starcounter
        /// installation folder.</param>
        public static void UninstallVs2012(string binDirectory) {
            UninstallUsingVSIXInstaller(
                Path.Combine(ConstantsBank.VS2012IDEDirectory, ConstantsBank.VSIXInstallerEngineExecutable),
                VSIXPackageInfo.VS2012.ExtensionIdentity,
                VisualStudioVersion.VS2012.BuildNumber,
                "Pro"
                );
        }

        /// <summary>
        /// Installs the Starcounter VS extension in VS 2013.
        /// </summary>
        /// <param name="binDirectory">Full path to the Starcounter
        /// installation folder.</param>
        public static void InstallVs2013(string binDirectory) {
            InstallUsingVSIXInstaller(
                Path.Combine(ConstantsBank.VS2013IDEDirectory, ConstantsBank.VSIXInstallerEngineExecutable),
                Path.Combine(binDirectory, VSIXPackageInfo.VS2013.FileName),
                VisualStudioVersion.VS2013.BuildNumber,
                "Pro"
                );
        }

        /// <summary>
        /// Uninstalls the Starcounter VS extension from VS 2013.
        /// </summary>
        /// <param name="binDirectory">Full path to the Starcounter
        /// installation folder.</param>
        public static void UninstallVs2013(string binDirectory) {
            UninstallUsingVSIXInstaller(
                Path.Combine(ConstantsBank.VS2013IDEDirectory, ConstantsBank.VSIXInstallerEngineExecutable),
                VSIXPackageInfo.VS2013.ExtensionIdentity,
                VisualStudioVersion.VS2013.BuildNumber,
                "Pro"
                );
        }

        /// <summary>
        /// Installs a VSIX package using the Visual Studio VSIX installer engine.
        /// </summary>
        /// <param name="installerEnginePath">
        /// Path to the engine to use.</param>
        /// <param name="vsixPackageFile">
        /// Path to the VSIX package to install.</param>
        /// <param name="targetEdition">
        /// The version (build number) of Visual Studio to install into, e.g. "11.0"
        /// for Visual Studio 2012.</param>
        /// <param name="targetVersion">
        /// The edition of Visual Studio to install into, normally "Pro" (for
        /// "Professional").
        /// </param>
        static void InstallUsingVSIXInstaller(
            string installerEnginePath,
            string vsixPackageFile,
            string targetVersion,
            string targetEdition
            ) {
            Process process;
            string arguments;
            int timeoutInMilliseconds;
            int result;
            bool completed;

            if (!File.Exists(installerEnginePath)) {
                throw ErrorCode.ToException(
                    Error.SCERRVSIXENGINENOTFOUND,
                    string.Format("Engine path={0}, Target version={1}, Target edition={2}",
                    installerEnginePath,
                    targetVersion,
                    targetEdition));
            }

            if (!File.Exists(vsixPackageFile)) {
                throw ErrorCode.ToException(
                    Error.SCERRVSIXPACKAGENOTFOUND,
                    string.Format("Package path={0}, Target version={1}, Target edition={2}",
                    vsixPackageFile,
                    targetVersion,
                    targetEdition));
            }

            // Example command-line:
            // VSIXInstaller.exe /quiet /skuName:Pro /skuVersion:11.0 "C:\Program Files\Starcounter\Starcounter.VisualStudio.11.0.vsix"

            arguments = string.Format(
                "/quiet /skuName:{0} /skuVersion:{1} \"{2}\"",
                targetEdition,
                targetVersion,
                vsixPackageFile
                );

            try {
                process = Process.Start(installerEnginePath, arguments);
            } catch (Exception startupException) {
                throw ErrorCode.ToException(Error.SCERRVSIXENGINECOULDNOTSTART,
                    startupException,
                    string.Format(
                    "Exception message={0}, Engine path={1}, Arguments={2}",
                    startupException.Message,
                    installerEnginePath,
                    arguments)
                    );
            }

            timeoutInMilliseconds = 120 * 1000;
            completed = process.WaitForExit(timeoutInMilliseconds);
            if (!completed) {
                throw ErrorCode.ToException(Error.SCERRVSIXENGINETIMEDOUT,
                    string.Format(
                    "Timeout time={0} ms, Engine path={1}, Arguments={2}",
                    timeoutInMilliseconds,
                    installerEnginePath,
                    arguments)
                    );
            }

            result = process.ExitCode;
            if (result != 0) {
                // Installing
                // 2001: When it can't find the VSIX file.
                // 1001: When we are installing and the extension is already installed.
                if (result == 1001) {
                    Utilities.LogMessage(
                        ErrorCode.ToMessage(Error.SCERRVSIXENGINEFAILED,
                        string.Format(
                        "The VSIX installer indicated the extension was already installed. Ignoring this. Process exit code={0}, Engine path={1}, Arguments={2}",
                        result,
                        installerEnginePath,
                        arguments)));
                } else {
                    throw ErrorCode.ToException(Error.SCERRVSIXENGINEFAILED,
                        string.Format(
                        "Process exit code={0}, Engine path={1}, Arguments={2}",
                        result,
                        installerEnginePath,
                        arguments)
                        );
                }
            }
        }

        static void UninstallUsingVSIXInstaller(
            string installerEnginePath,
            string vsixExtensionIdentity,
            string targetVersion,
            string targetEdition) {
            Process process;
            string arguments;
            int result;
            
            if (!File.Exists(installerEnginePath)) {
                throw ErrorCode.ToException(
                    Error.SCERRVSIXENGINENOTFOUND,
                    string.Format("Engine path={0}, Target version={1}, Target edition={2}",
                    installerEnginePath,
                    targetVersion,
                    targetEdition));
            }

            // Example command-line:
            // VSIXInstaller.exe /quiet /skuName:Pro /skuVersion:11.0 /uninstall:Starcounter.VS11.DCCF9B11-E0CD-4D4F-BCE6-55EEA5AA1325

            arguments = string.Format(
                "/quiet /skuName:{0} /skuVersion:{1} /uninstall:{2}",
                targetEdition,
                targetVersion,
                vsixExtensionIdentity
                );

            process = RunVSIXInstaller(installerEnginePath, arguments);
            result = process.ExitCode;
            if (result == 0) {
                // The VSIX installer uninstalls using a strategy that first
                // marks the extension as installed, returning 0. The marking
                // is done in the registry, at:
                // HKCU\Software\Microsoft\VisualStudio\11.0\ExtensionManager\PendingDeletions
                // Then, on a second run (or when starting VS), the extension
                // is acutally removed from the system.
                // We employ a second run instantly, to force the removal of
                // the extension. This makes sure our IsInstalled algorithm works
                // as we expect.
                process = RunVSIXInstaller(installerEnginePath, arguments);
                result = process.ExitCode;
                if (result != 2003) {
                    throw ErrorCode.ToException(Error.SCERRVSIXENGINEFAILED,
                        string.Format(
                        "Process exit code={0}, Engine path={1}, Arguments={2}",
                        result,
                        installerEnginePath,
                        arguments)
                        );
                }
            }
            else {
                // 2003: The extension with that ID was not installed in the given version
                // 2003: When specifiyng no particular SKU/version, the same code indicates it's not installed.

                if (result == 2003) {
                    // 2003 is the exit code indicating that a package was not installed.
                    // If we execute in the context of a full cleanup, we have no certain
                    // state (i.e. the installation can be corrupt) and so we let this
                    // one slide without reporting an exception.

                    Utilities.LogMessage(
                        ErrorCode.ToMessage(Error.SCERRVSIXENGINEFAILED,
                        string.Format(
                        "The VSIX installer indicated the extension was already uninstalled. Ignoring this. Process exit code={0}, Engine path={1}, Arguments={2}",
                        result,
                        installerEnginePath,
                        arguments)));
                } else {
                    throw ErrorCode.ToException(Error.SCERRVSIXENGINEFAILED,
                        string.Format(
                        "Process exit code={0}, Engine path={1}, Arguments={2}",
                        result,
                        installerEnginePath,
                        arguments)
                        );
                }
            }
        }

        static Process RunVSIXInstaller(string installerEnginePath, string arguments, bool wait = true) {
            Process process;
            try {
                process = Process.Start(installerEnginePath, arguments);
            } catch (Exception startupException) {
                throw ErrorCode.ToException(Error.SCERRVSIXENGINECOULDNOTSTART,
                    startupException,
                    string.Format(
                    "Exception message={0}, Engine path={1}, Arguments={2}",
                    startupException.Message,
                    installerEnginePath,
                    arguments)
                    );
            }

            if (wait) {
                var completed = process.WaitForExit(60 * 1000);
                if (!completed) {
                    throw ErrorCode.ToException(Error.SCERRVSIXENGINETIMEDOUT,
                        string.Format(
                        "Timeout time={0} ms, Engine path={1}, Arguments={2}",
                        60 * 1000,
                        installerEnginePath,
                        arguments)
                        );
                }
            }

            return process;
        }
    }
}