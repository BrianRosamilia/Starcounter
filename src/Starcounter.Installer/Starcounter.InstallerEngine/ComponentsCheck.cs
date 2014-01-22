using System;
using Microsoft.Win32;
using System.IO;
using System.ServiceProcess;
using System.DirectoryServices;

namespace Starcounter.InstallerEngine
{
    public class ComponentsCheck
    {
        /// <summary>
        /// Checks if Starcounter environment variables exist in the system.
        /// </summary>
        internal static String CheckServerEnvVars(Boolean currentUser,
                                                  Boolean systemWide)
        {
            // Checking for Starcounter environment variables existence.
            if (currentUser)
            {
                String scEnvVar = Environment.GetEnvironmentVariable(ConstantsBank.SCEnvVariableDefaultServer,
                    EnvironmentVariableTarget.User);

                if (scEnvVar != null) return scEnvVar;
            }

            if (systemWide)
            {
                String scEnvVar = Environment.GetEnvironmentVariable(ConstantsBank.SCEnvVariableName,
                    EnvironmentVariableTarget.Machine);

                if (scEnvVar != null) return scEnvVar;

                scEnvVar = Environment.GetEnvironmentVariable(ConstantsBank.SCEnvVariableDefaultServer,
                    EnvironmentVariableTarget.Machine);

                if (scEnvVar != null) return scEnvVar;
            }

            return null;
        }

        // List of all distinguished Starcounter components.
        public enum Components
        {
            InstallationBase,
            PersonalServer,
            SystemServer,
            VS2012Integration,
            VS2013Integration
        };

        public static readonly Int32 NumComponents = Enum.GetValues(typeof(Components)).Length;

        /// <summary>
        /// Cached list of installed components.
        /// </summary>
        static Boolean[] cachedInstalledComponents = null;

        /// <summary>
        /// Simply resets corresponding value so that
        /// components are obtained from scratch.
        /// </summary>
        internal static void ResetCachedInstalledComponents()
        {
            cachedInstalledComponents = null;
        }

        /// <summary>
        /// Get list of components that has already been installed in the system.
        /// </summary>
        public static Boolean[] GetListOfInstalledComponents()
        {
            if (cachedInstalledComponents != null)
                return cachedInstalledComponents;

            // Creating empty list of installed components.
            cachedInstalledComponents = new Boolean[NumComponents];
            for (Int32 i = 0; i < NumComponents; i++)
                cachedInstalledComponents[i] = false;

            if (InstallerMain.InstallationBaseComponent.IsInstalled())
                cachedInstalledComponents[(Int32)Components.InstallationBase] = true;

            if (InstallerMain.PersonalServerComponent.IsInstalled())
                cachedInstalledComponents[(Int32)Components.PersonalServer] = true;

            if (InstallerMain.VS2012IntegrationComponent.IsInstalled())
                cachedInstalledComponents[(Int32)Components.VS2012Integration] = true;

            if (InstallerMain.VS2013IntegrationComponent.IsInstalled())
                cachedInstalledComponents[(Int32)Components.VS2013Integration] = true;

            // In case if there are no components installed, returns 'false' array.
            return cachedInstalledComponents;
        }

        /// <summary>
        /// Returns TRUE if any of Starcounter components still remain in the system.
        /// </summary>
        public static Boolean AnyComponentsExist()
        {
            // Resetting cached values for components search
            // (so its done from scratch again).
            ComponentsCheck.ResetCachedInstalledComponents();

            // Retrieving 'fresh' components list.
            Boolean[] installedComponents = ComponentsCheck.GetListOfInstalledComponents();

            // Remember that installation base is not considered as an individual component.
            if (installedComponents[(Int32)ComponentsCheck.Components.PersonalServer]) return true;
            if (installedComponents[(Int32)ComponentsCheck.Components.SystemServer]) return true;
            if (installedComponents[(Int32)ComponentsCheck.Components.VS2012Integration]) return true;

            return false;
        }
    }
}
