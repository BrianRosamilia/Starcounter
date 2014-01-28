
using Starcounter.Internal;
using Starcounter.Server;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace scservice {

    class Program {

        static void Main(string[] args) {
            var startedAsService = IsStartedAsService(args);
            string serverName = "Personal";
            bool logSteps = false;
            var debug = false;

            if (args.Length > 2) {
                if (!startedAsService) {
                    Usage();
                }
                return;
            }

            var ignoreCase = StringComparison.InvariantCultureIgnoreCase;
            for (int i = 0; i < args.Length; i++) {
                var arg = args[i];
                if (arg.Equals("--logsteps", ignoreCase)) {
                    logSteps = true;
                }
                else if (arg.Equals("?") || arg.Equals("-h") || arg.Equals("--help")) {
                    if (!startedAsService) {
                        Usage();
                    }
                    return;
                }
                else if (arg.Equals("--sc-debug", ignoreCase)) {
                    debug = true;
                }
                else {
                    Console.WriteLine("Ignoring parameter \"{0}\"", arg);
                }
            }

            if (debug) {
                int seconds = 20;
                Console.WriteLine("Waiting {0} seconds, or until a debugger is attached...", seconds);
                for (int i = 0; i < seconds; i++) {
                    Thread.Sleep(1000);
                    if (Debugger.IsAttached) {
                        Debugger.Break();
                        break;
                    }
                }
            }

            if (!startedAsService) {
                StartTrayIcon();
            }

            var serviceProcess = new ServerServiceProcess(serverName) {
                LogSteps = logSteps
            };
            serviceProcess.Launch(startedAsService);
        }

        static bool IsStartedAsService(string[] args) {
            return Process.GetCurrentProcess().SessionId == 0;
        }

        static void Usage() {
            Console.WriteLine("scservice.exe [--logsteps]");
            Console.WriteLine();
            Console.WriteLine("How it works:");
            Console.WriteLine("scservice will load XML-file called personal.xml");
            Console.WriteLine("from the same directory as scservice.exe and");
            Console.WriteLine("will fetch corresponding server directory from it.");
            Console.WriteLine("From obtained directory it will load personal.config.xml");
            Console.WriteLine("to read server-related settings.");
            Console.WriteLine("scservice will then start and monitor all required");
            Console.WriteLine("Starcounter components, like scnetworkgateway, scipcmonitor, etc.");
        }

        /// <summary>
        /// Start the TrayIcon program
        /// </summary>
        /// <remarks>
        /// The TrayIcon program is a Singelton so we dont need to check for a existing running one
        /// </remarks>
        static void StartTrayIcon() {
            try {
                string scBin = Environment.GetEnvironmentVariable(StarcounterEnvironment.VariableNames.InstallationDirectory);

                // Need to use full path to EXE because of no shell execute.
                var startInfo = new ProcessStartInfo(Path.Combine(Environment.CurrentDirectory, StarcounterConstants.ProgramNames.ScTrayIcon+".exe"));
                startInfo.WorkingDirectory = scBin;

                Process.Start(startInfo);
            }
            catch (Exception e) {
                Console.WriteLine("Failed to start the TrayIcon program \"{0}\"", e.ToString());
            }
        }
    }
}