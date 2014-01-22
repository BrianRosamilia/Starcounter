using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Starcounter.InstallerEngine.VsSetup
{
    /// <summary>
    /// Represent metadata about a VSIX extension package file (.vsix),
    /// consumed by the installer.
    /// </summary>
    internal class VSIXPackageInfo
    {
        /// <summary>
        /// Gets the extension metadata for the VS 2010 package file.
        /// </summary>
        internal static readonly VSIXPackageInfo VS2010 = new VSIXPackageInfo("Starcounter.VS10.vsix", "C8636CE1-46B5-4E7C-A558-4925EA1ED3AC");

        /// <summary>
        /// Gets the extension metadata for the VS 2012 package file.
        /// </summary>
        internal static readonly VSIXPackageInfo VS2012 = new VSIXPackageInfo("Starcounter.VS11.vsix", "Starcounter.VS11.DCCF9B11-E0CD-4D4F-BCE6-55EEA5AA1325");

        /// <summary>
        /// Gets the extension metadata for the VS 2013 package file.
        /// </summary>
        internal static readonly VSIXPackageInfo VS2013 = new VSIXPackageInfo("Starcounter.VS12.vsix", "Starcounter.VS12.0C4CE454-5AE6-49D5-ACD5-90FF0A8AA61A");

        /// <summary>
        /// Private constructor initializing a <see cref="VSIXPackageInfo"/>.
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="extensionIdentity"></param>
        private VSIXPackageInfo(string fileName, string extensionIdentity)
        {
            this.FileName = fileName;
            this.ExtensionIdentity = extensionIdentity;
        }

        /// <summary>
        /// The simple file name of the extension package file (expected to
        /// include it's ".vsixmanifest" file extension).
        /// </summary>
        public readonly string FileName;

        /// <summary>
        /// The unique identity used to represent the extension.
        /// </summary>
        /// <remarks>
        /// The master copy of this value is in each respective .vsixmanifest file, so
        /// make sure to keep the in sync if they ever have to change.
        /// </remarks>
        public readonly string ExtensionIdentity;
    }
}
