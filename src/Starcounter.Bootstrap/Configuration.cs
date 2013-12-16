// ***********************************************************************
// <copyright file="Configuration.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using Starcounter;
using Starcounter.CommandLine;
using Starcounter.Internal;
using System;
using System.IO;

namespace StarcounterInternal.Bootstrap
{
    /// <summary>
    /// Basic host configuration.
    /// </summary>
    public class Configuration
    {
        /// <summary>
        /// Loads the specified program arguments.
        /// </summary>
        /// <param name="programArguments">The program arguments.</param>
        /// <returns>Configuration.</returns>
        public static Configuration Load(ApplicationArguments programArguments)
        {
            return new Configuration(programArguments);
        }

        /// <summary>
        /// Gets or sets the program arguments.
        /// </summary>
        /// <value>The program arguments.</value>
        public ApplicationArguments ProgramArguments { get; set; }

        /// <summary>
        /// Gets the scheduler count.
        /// </summary>
        /// <value>The scheduler count.</value>
        public uint SchedulerCount { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Configuration" /> class.
        /// </summary>
        /// <param name="programArguments">The program arguments.</param>
        private Configuration(ApplicationArguments programArguments)
        {
            ProgramArguments = programArguments;

            string prop;
            if (ProgramArguments.TryGetProperty(StarcounterConstants.BootstrapOptionNames.SchedulerCount, out prop))
            {
                try
                {
                    SchedulerCount = uint.Parse(prop);
                }
                catch (Exception e)
                {
                    throw ErrorCode.ToException(Starcounter.Error.SCERRBADSCHEDCOUNTCONFIG, e);
                }
            }
            else
            {
                SchedulerCount = (uint)Environment.ProcessorCount;
            }
        }

        /// <summary>
        /// Gets the name.
        /// </summary>
        /// <value>The name.</value>
        public string Name
        {
            get 
            {
                return this.ProgramArguments.CommandParameters[0];
            }
        }

        /// <summary>
        /// Gets the database directory.
        /// </summary>
        /// <value>The database directory.</value>
        public string DatabaseDirectory
        {
            get {
                string prop;

                if (!this.ProgramArguments.TryGetProperty(StarcounterConstants.BootstrapOptionNames.DatabaseDir, out prop))
                    prop = @"C:/Test";

                return prop;
            }
        }

        /// <summary>
        /// Gets the output directory.
        /// </summary>
        /// <value>The output directory.</value>
        public string OutputDirectory
        {
            get {
                string prop;

                if (!this.ProgramArguments.TryGetProperty(StarcounterConstants.BootstrapOptionNames.OutputDir, out prop))
                    prop = @"C:/Test";

                return prop;
            }
        }

        /// <summary>
        /// Gets the temp directory.
        /// </summary>
        /// <value>The temp directory.</value>
        public string TempDirectory
        {
            get {
                string prop;

                if (!this.ProgramArguments.TryGetProperty(StarcounterConstants.BootstrapOptionNames.TempDir, out prop))
                    prop = @"C:/Test/Temp";

                return prop;
            }
        }

        /// <summary>
        /// Gets the auto start exe path.
        /// </summary>
        /// <value>The auto start exe path.</value>
        public string AutoStartExePath
        {
            get
            {
                string autoStartExePath;

                if (!this.ProgramArguments.TryGetProperty(StarcounterConstants.BootstrapOptionNames.AutoStartExePath, out autoStartExePath))
                    autoStartExePath = null;

                return autoStartExePath;
            }
        }

        /// <summary>
        /// Gets the name of the server.
        /// </summary>
        /// <value>The name of the server.</value>
        public string ServerName
        {
            get
            {
                string serverName;

                if (!this.ProgramArguments.TryGetProperty(StarcounterConstants.BootstrapOptionNames.ServerName, out serverName))
                    serverName = StarcounterEnvironment.ServerNames.PersonalServer.ToUpper();

                // Making server name upper case.
                serverName = serverName.ToUpper();

                return serverName;
            }
        }

        /// <summary>
        /// Number of workers in gateway.
        /// </summary>
        public Byte GatewayNumberOfWorkers
        {
            get
            {
                if (NoNetworkGateway)
                    return 0;

                String gatewayWorkersNumber;
                if (this.ProgramArguments.TryGetProperty(StarcounterConstants.BootstrapOptionNames.GatewayWorkersNumber, out gatewayWorkersNumber))
                {
                    return Byte.Parse(gatewayWorkersNumber);
                }

                throw ErrorCode.ToException(Starcounter.Error.SCERRBADGATEWAYWORKERSNUMBERCONFIG);
            }
        }

        /// <summary>
        /// Default session timeout.
        /// </summary>
        public UInt32 DefaultSessionTimeoutMinutes
        {
            get
            {
                UInt32 defaultSessionsTimeout = StarcounterConstants.NetworkPorts.DefaultSessionTimeoutMinutes;
                String s;
                if (this.ProgramArguments.TryGetProperty(StarcounterConstants.BootstrapOptionNames.DefaultSessionTimeoutMinutes, out s))
                {
                    defaultSessionsTimeout = UInt32.Parse(s);
                    if (defaultSessionsTimeout <= 0)
                        throw ErrorCode.ToException(Starcounter.Error.SCERRBADSESSIONSDEFAULTTIMEOUT);
                }

                return defaultSessionsTimeout;
            }
        }

        /// <summary>
        /// Gets the chunks number.
        /// </summary>
        /// <value>The chunks number.</value>
        public uint ChunksNumber
        {
            get
            {
                // Default communication shared chunks number.
                uint chunksNumber = MixedCodeConstants.SHM_CHUNKS_DEFAULT_NUMBER;

                string chunksNumberStr;
                if (this.ProgramArguments.TryGetProperty(StarcounterConstants.BootstrapOptionNames.ChunksNumber, out chunksNumberStr))
                {
                    chunksNumber = uint.Parse(chunksNumberStr);

                    // Checking if number of chunks is correct.
                    if ((chunksNumber < 128) || (chunksNumber > 4096 * 128))
                    {
                        throw ErrorCode.ToException(Starcounter.Error.SCERRBADCHUNKSNUMBERCONFIG);
                    }
                }

                return chunksNumber;
            }
        }

        /// <summary>
        /// Gets the SQL process port.
        /// </summary>
        /// <value>The SQL process port.</value>
        public UInt16 SQLProcessPort
        {
            get
            {
                UInt16 v = 0;
                string str;
                if (this.ProgramArguments.TryGetProperty(StarcounterConstants.BootstrapOptionNames.SQLProcessPort, out str))
                {
                    v = UInt16.Parse(str);
                }
                return v;
            }
        }

        /// <summary>
        /// Gets the default user HTTP port.
        /// </summary>
        /// <value>The default user HTTP port.</value>
        public UInt16 DefaultUserHttpPort
        {
            get
            {
                UInt16 v = StarcounterConstants.NetworkPorts.DefaultPersonalServerUserHttpPort;
                string str;
                if (this.ProgramArguments.TryGetProperty(StarcounterConstants.BootstrapOptionNames.DefaultUserHttpPort, out str))
                {
                    v = UInt16.Parse(str);
                }
                return v;
            }
        }

        /// <summary>
        /// Gets the default user HTTP port.
        /// </summary>
        /// <value>The default user HTTP port.</value>
        public UInt16 DefaultSystemHttpPort
        {
            get
            {
                UInt16 v = StarcounterConstants.NetworkPorts.DefaultPersonalServerSystemHttpPort;
                string str;
                if (this.ProgramArguments.TryGetProperty(StarcounterConstants.BootstrapOptionNames.DefaultSystemHttpPort, out str))
                {
                    v = UInt16.Parse(str);
                }
                return v;
            }
        }

        /// <summary>
        /// Gets a value indicating whether [no db].
        /// </summary>
        /// <value><c>true</c> if [no db]; otherwise, <c>false</c>.</value>
        public bool NoDb
        {
            get
            {
                return this.ProgramArguments.ContainsFlag(StarcounterConstants.BootstrapOptionNames.NoDb);
            }
        }

        /// <summary>
        /// Gets a value indicating whether network gateway should not be used.
        /// </summary>
        /// <value><c>true</c> if network gateway should not be used; otherwise, <c>false</c>.</value>
        public bool NoNetworkGateway
        {
            get
            {
                return this.ProgramArguments.ContainsFlag(StarcounterConstants.BootstrapOptionNames.NoNetworkGateway);
            }
        }
    }
}
