
using Starcounter.Internal;
using System;
using System.Diagnostics;

namespace staradmin {
    internal static class ProcessUtilities {
        /// <summary>
        /// List of Starcounter processes that should be killed.
        /// </summary>
        static String[] ScProcessesList = new String[]
        {
            StarcounterConstants.ProgramNames.ScService,
            StarcounterConstants.ProgramNames.ScIpcMonitor,
            StarcounterConstants.ProgramNames.ScNetworkGateway,
            StarcounterConstants.ProgramNames.ScAdminServer,
            StarcounterConstants.ProgramNames.ScCode,
            StarcounterConstants.ProgramNames.ScData,
            StarcounterConstants.ProgramNames.ScDbLog,
            "scnetworkgatewayloopedtest",
            StarcounterConstants.ProgramNames.ScWeaver,
            StarcounterConstants.ProgramNames.ScSqlParser,
            StarcounterConstants.ProgramNames.ScTrayIcon,
            "ServerLogTail"
        };

        /// <summary>
        /// Kills all Starcounter processes and waits for them to shutdown.
        /// </summary>
        internal static void KillAllScProcesses(Int32 msToWait = 20000) {
            foreach (String procName in ScProcessesList) {
                Process[] procs = Process.GetProcessesByName(procName);
                foreach (Process proc in procs) {
                    try {
                        proc.Kill();
                        proc.WaitForExit(msToWait);
                        if (!proc.HasExited) {
                            String processCantBeKilled = "Process " + proc.ProcessName + " can not be killed." + Environment.NewLine +
                                "Please shutdown the corresponding application explicitly.";

                            throw new Exception(processCantBeKilled);
                        } else {
                            Console.WriteLine(DateTime.Now.TimeOfDay + ": process '" + procName + "' successfully killed!");
                        }
                    } finally { proc.Close(); }
                }
            }
        }
    }
}
