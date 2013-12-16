// ***********************************************************************
// <copyright file="RepositorySetup.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Starcounter.Advanced.Configuration;
using Starcounter.Server;
using Starcounter.Internal;

namespace Starcounter.Server.Setup {

    /// <summary>
    /// Represents the process of creating a new repository.
    /// </summary>
    /// <example>
    /// This sample shows the intended use of the <see cref="RepositorySetup"/> class.
    /// <code>
    /// class Program
    /// {
    ///     public static void Main(string[] args)
    ///     {
    ///         RepositorySetup setup;
    ///
    ///         // Normally, let the user choose some directory to create the
    ///         // new repository in. In this case, we assume the user has specified
    ///         // a directory MyServers\MyServer. We use that value to initialize
    ///         // the setup structure.
    ///
    ///         setup = RepositorySetup.NewDefault(@"C:\MyServers\MyServer", "Personal");
    ///
    ///         // Display some GUI/wizard that allows the user to modify
    ///         // all default values as suggested by the NewDefault method.
    ///         // No modification to the disk has yet occured.
    ///
    ///         CreateRepositoryWizard.Show(setup);
    ///
    ///         // Now create the disk structure and the configuration files
    ///         // after the wizard has terminated.
    ///
    ///         try {
    ///             setup.Execute();
    ///         }
    ///         catch (Exception) { ... }
    ///     }
    /// }
    /// </code>
    /// </example>
    public sealed class RepositorySetup {
        /// <summary>
        /// The <see cref="RepositoryStructure"/> to use.
        /// </summary>
        public readonly RepositoryStructure Structure;

        /// <summary>
        /// The <see cref="ServerConfiguration">server configuration</see> to use.
        /// </summary>
        public readonly ServerConfiguration ServerConfiguration;

        /// <summary>
        /// Creates a new default setup based on a directory and a name.
        /// </summary>
        /// <param name="repositoryParentPath">
        /// Directory in where the new repository should be created.</param>
        /// <param name="name">Name of the new repository.</param>
        /// <returns>A <see cref="RepositorySetup"/> representing the
        /// setup of the given values.</returns>
        public static RepositorySetup NewDefault(string repositoryParentPath, string name) {
            if (string.IsNullOrEmpty(repositoryParentPath)) {
                throw new ArgumentNullException("repositoryParentPath");
            }

            if (string.IsNullOrEmpty(name)) {
                throw new ArgumentNullException("name");
            }

            if (name.IndexOfAny(Path.GetInvalidPathChars()) >= 0) {
                throw new FormatException("name");
            }

            for (int i = 0; i < name.Length; i++) {
                if (Char.IsWhiteSpace(name[i])) {
                    throw new FormatException("name");
                }
            }

            return NewDefault(Path.Combine(repositoryParentPath, name), 0);
        }

        /// <summary>
        /// Creates a new default setup based on a directory repository directory.
        /// </summary>
        /// <param name="repositoryPath">The directory that makes up the root of the
        /// repository created. The name of the repository is fetched from the last
        /// part of the given path.</param>
        /// <param name="serverPortRange">The server port range.</param>
        /// <returns>A <see cref="RepositorySetup" /> representing the
        /// setup of the given value.</returns>
        internal static RepositorySetup NewDefault(string repositoryPath, int serverPortRange) {
            RepositoryStructure structure;
            ServerConfiguration serverConfig;
            MonitoringConfiguration monitoringConfig;
            DatabaseRuntimeConfiguration databaseRuntimeConfig;
            DatabaseStorageConfiguration databaseStorageConfig;
            DatabaseConfiguration databaseConfiguration;
            DatabaseDefaults databaseDefaults;

            structure = RepositoryStructure.NewDefault(repositoryPath);
            databaseDefaults = new DatabaseDefaults();

            monitoringConfig = new MonitoringConfiguration() {
                MonitoringType = MonitoringType.Disabled,
                StartupType = StartupType.Manual
            };

            databaseStorageConfig = new DatabaseStorageConfiguration() {
                TransactionLogSize = databaseDefaults.TransactionLogSize,
                SupportReplication = false,
                CollationFile = databaseDefaults.CollationFile
            };

            databaseRuntimeConfig = new DatabaseRuntimeConfiguration() {
                ImageDirectory = Path.Combine(structure.DataDirectory, "[DatabaseName]"),
                TransactionLogDirectory = Path.Combine(structure.DataDirectory, "[DatabaseName]"),
                TempDirectory = Path.Combine(structure.TempDirectory, "[DatabaseName]"),
                SQLProcessPort = (UInt16)(StarcounterConstants.NetworkPorts.DefaultPersonalPrologSqlProcessPort + serverPortRange),
                DefaultUserHttpPort = StarcounterConstants.NetworkPorts.DefaultPersonalServerUserHttpPort,
                ChunksNumber = MixedCodeConstants.SHM_CHUNKS_DEFAULT_NUMBER,
                DefaultSessionTimeoutMinutes = StarcounterConstants.NetworkPorts.DefaultSessionTimeoutMinutes
            };

            databaseConfiguration = new DatabaseConfiguration() {
                Runtime = databaseRuntimeConfig,
                Monitoring = monitoringConfig
            };

            serverConfig = new ServerConfiguration() {
                DatabaseDirectory = structure.DatabaseDirectory,
                TempDirectory = structure.TempDirectory,
                LogDirectory = structure.LogDirectory,
                EnginesDirectory = structure.RepositoryDirectory,
                SystemHttpPort = structure.SystemHttpPort,
                DefaultDatabaseStorageConfiguration = databaseStorageConfig,
                DefaultDatabaseConfiguration = databaseConfiguration,
                SendUsageAndCrashReports = true
            };

            return new RepositorySetup(structure, serverConfig);
        }

        /// <summary>
        /// Initializes a new <see cref="RepositorySetup"/> by specifying a
        /// structure, a server configuration and an engine configuration.
        /// </summary>
        /// <param name="structure">A <see cref="RepositoryStructure"/> to use.</param>
        /// <param name="serverConfiguration">The <see cref="ServerConfiguration"/> to
        /// use.</param>
        internal RepositorySetup(RepositoryStructure structure, ServerConfiguration serverConfiguration) {
            if (structure == null) {
                throw new ArgumentNullException("structure");
            }
            if (serverConfiguration == null) {
                throw new ArgumentNullException("serverConfiguration");
            }
            
            this.Structure = structure;
            this.ServerConfiguration = serverConfiguration;
        }

        /// <summary>
        /// Creates the repository on disk.
        /// </summary>
        public void Execute() {
            this.Structure.Create();
            this.ServerConfiguration.Save(this.Structure.ServerConfigurationPath);

            string samplePath = Path.Combine(Path.GetDirectoryName(this.Structure.ServerConfigurationPath), ".sample" + DatabaseConfiguration.FileExtension);
            var sample = ServerConfiguration.DefaultDatabaseConfiguration.Clone(samplePath);
            sample.Save();
        }
    }
}