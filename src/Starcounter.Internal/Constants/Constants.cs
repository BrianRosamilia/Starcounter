
using System;
using System.Diagnostics;

namespace Starcounter.Internal
{
    /// <summary>
    /// StarcounterConstants
    /// </summary>
    public static class StarcounterConstants
    {
        /// <summary>
        /// Defines the default name to use when no information about a
        /// named database is given in a certain context, for example when
        /// starting an executable.
        /// </summary>
        public const string DefaultDatabaseName = "Default";

        /// <summary>
        /// Maximum possible schedulers number.
        /// </summary>
        public const Byte MaximumSchedulersNumber = 128;

        /// <summary>
        /// Network related constants.
        /// </summary>
        public static class NetworkConstants
        {
            /// <summary>
            /// End of line for HTTP.
            /// </summary>
            public const string CRLF = "\r\n";

            /// <summary>
            /// Double end of line for HTTP.
            /// </summary>
            public const string CRLFCRLF = CRLF + CRLF;
        }

        /// <summary>
        /// Defines the program names.
        /// </summary>
        public static class ProgramNames
        {
            /// <summary>
            /// ScService
            /// </summary>
            public const string ScService = "scservice";

            /// <summary>
            /// ScCode
            /// </summary>
            public const string ScCode = "sccode";

            /// <summary>
            /// ScData
            /// </summary>
            public const string ScData = "scdata";

            /// <summary>
            /// </summary>
            public const string ScDbLog = "scdblog";

            /// <summary>
            /// ScNetworkGateway
            /// </summary>
            public const string ScNetworkGateway = "scnetworkgateway";

            /// <summary>
            /// ScIpcMonitor
            /// </summary>
            public const string ScIpcMonitor = "scipcmonitor";

            /// <summary>
            /// ScWeaver
            /// </summary>
            public const string ScWeaver = "scweaver";

            /// <summary>
            /// ScSql
            /// </summary>
            public const string ScSqlParser = "scsqlparser";

            /// <summary>
            /// Just represents the product name.
            /// </summary>
            public const string ProductName = "Starcounter";

            /// <summary>
            /// Gets the name of the code host process that runs the
            /// Starcounter admin server.
            /// </summary>
            public const string ScAdminServer = "scadminserver";

            /// <summary>
            /// ScTrayIcon
            /// </summary>
            public const string ScTrayIcon = "sctrayicon";

        }

        /// <summary>
        /// Defines the well known network ports.
        /// </summary>
        public class NetworkPorts
        {
            /// <summary>
            /// Default value for the port that is not specified by user.
            /// </summary>
            public const UInt16 DefaultUnspecifiedPort = 0;

            /// <summary>
            /// Default user port for Personal server.
            /// </summary>
            public const UInt16 DefaultPersonalServerUserHttpPort = 8080;

            /// <summary>
            /// Default user port for Personal server.
            /// </summary>
            public const String DefaultPersonalServerUserHttpPort_String = "DefaultPersonalServerUserHttpPort";

            /// <summary>
            /// Default user port for System server.
            /// </summary>
            public const UInt16 DefaultSystemServerUserHttpPort = 80;

            /// <summary>
            /// Default user port for System server.
            /// </summary>
            public const String DefaultSystemServerUserHttpPort_String = "DefaultSystemServerUserHttpPort";

            /// <summary>
            /// Default user port for Personal server.
            /// </summary>
            public const UInt16 DefaultPersonalServerSystemHttpPort = 8181;

            /// <summary>
            /// Default aggregation port for Personal server.
            /// </summary>
            public const UInt16 DefaultPersonalServerAggregationPort = 9090;

            /// <summary>
            /// Default user port for Personal server.
            /// </summary>
            public const String DefaultPersonalServerSystemHttpPort_String = "DefaultPersonalServerSystemHttpPort";

            /// <summary>
            /// Default system port for System server.
            /// </summary>
            public const UInt16 DefaultSystemServerSystemHttpPort = 81;

            /// <summary>
            /// Default aggregation port for System server.
            /// </summary>
            public const UInt16 DefaultSystemServerAggregationPort = 90;

            /// <summary>
            /// Default system port for System server.
            /// </summary>
            public const String DefaultSystemServerSystemHttpPort_String = "DefaultSystemServerSystemHttpPort";

            /// <summary>
            /// Default Prolog SQL TCP port.
            /// </summary>
            public const UInt16 DefaultPersonalPrologSqlProcessPort = 8066;

            /// <summary>
            /// Default Prolog SQL TCP port.
            /// </summary>
            public const String DefaultPersonalPrologSqlProcessPort_String = "DefaultPersonalPrologSqlProcessPort";

            /// <summary>
            /// Default Prolog SQL TCP port.
            /// </summary>
            public const UInt16 DefaultSystemPrologSqlProcessPort = 8067;

            /// <summary>
            /// Default Prolog SQL TCP port.
            /// </summary>
            public const String DefaultSystemPrologSqlProcessPort_String = "DefaultSystemPrologSqlProcessPort";
        }

        /// <summary>
        /// Defines the commands this program accepts.
        /// </summary>
        public static class BootstrapCommandNames
        {
            /// <summary>
            /// Defines the name of the Start command.
            /// </summary>
            /// <remarks>
            /// The Start command is the default command and can hence
            /// be omitted on the command line.
            /// </remarks>
            public const string Start = "Start";
        }

        /// <summary>
        /// Defines the options the "Start" command accepts.
        /// </summary>
        public static class BootstrapOptionNames
        {
            /// <summary>
            /// Specifies the database directory to use.
            /// </summary>
            public const string DatabaseDir = "DatabaseDir";

            /// <summary>
            /// Specifies the output directory to use.
            /// </summary>
            public const string OutputDir = "OutputDir";

            /// <summary>
            /// Specifies the temporary directory to use.
            /// </summary>
            public const string TempDir = "TempDir";

            /// <summary>
            /// Specifies the name of Starcounter server which started the database.
            /// </summary>
            public const string ServerName = "ServerName";

            /// <summary>
            /// Specifies the total number of chunks used for shared memory communication.
            /// </summary>
            public const string ChunksNumber = "ChunksNumber";

            /// <summary>
            /// Number of workers used in gateway.
            /// </summary>
            public const string GatewayWorkersNumber = "GatewayWorkersNumber";

            /// <summary>
            /// Specifies TCP/IP port to be used by SQL parser.
            /// </summary>
            public const string SQLProcessPort = "SQLProcessPort";

            /// <summary>
            /// Default HTTP port for user code.
            /// </summary>
            public const string DefaultUserHttpPort = "DefaultUserHttpPort";

            /// <summary>
            /// Default HTTP port for system code.
            /// </summary>
            public const string DefaultSystemHttpPort = "DefaultSystemHttpPort";

            /// <summary>
            /// Specifies the number of schedulers.
            /// </summary>
            public const string SchedulerCount = "SchedulerCount";

            /// <summary>
            /// Gets the string to use to apply the switch that tells the host process
            /// not to connect to the database nor utilize the SQL engine.
            /// </summary>
            public const string NoDb = "NoDb";

            /// <summary>
            /// Indicates if this host Apps is not utilizing the network gateway.
            /// </summary>
            public const string NoNetworkGateway = "NoNetworkGateway";

            /// <summary>
            /// Specifies the path to executable that should be run on startup.
            /// </summary>
            public const string AutoStartExePath = "AutoStartExePath";

            /// <summary>
            /// User command line arguments.
            /// </summary>
            public const string UserArguments = "UserArguments";

            /// <summary>
            /// Explicit working directory.
            /// </summary>
            public const string WorkingDir = "WorkingDir";

            /// <summary>
            /// Holds the name of the command-line allowing a program to install
            /// a trace listener that writes every trace to the log.
            /// </summary>
            public const string EnableTraceLogging = "LogSteps";
        }
    }
}
