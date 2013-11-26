
using Starcounter;
using Starcounter.CLI;
using Starcounter.CommandLine;
using Starcounter.CommandLine.Syntax;
using Starcounter.Internal;
using Starcounter.Server.Setup;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace star {

    class Program {
       
        static void Main(string[] args) {
            ApplicationArguments appArgs;
            int serverPort;
            string serverName;
            string serverHost;
            
            if (args.Length == 0) {
                Usage(null);
                return;
            }

            if (args.Length == 1) {
                uint code;
                if (uint.TryParse(args[0], out code)) {
                    var helpPageUri = ErrorCode.ToHelpLink(code);
                    ConsoleUtil.ToConsoleWithColor(string.Format("Opening help page \"{0}\"", helpPageUri), ConsoleColor.DarkGray);
                    Process.Start(helpPageUri);
                    return;
                }
            }

            var syntax = DefineCommandLineSyntax();
            if (!SharedCLI.TryParse(args, syntax, out appArgs))
                return;

            // Process global options that has precedence

            if (appArgs.ContainsFlag(SharedCLI.UnofficialOptions.Debug)) {
                Debugger.Launch();
            }

            if (appArgs.ContainsFlag(StarOption.NoColor)) {
                ConsoleUtil.DisableColors = true;
            }

            var syntaxTests = appArgs.ContainsFlag(StarOption.Syntax);
            if (syntaxTests) {
                ConsoleUtil.ToConsoleWithColor(() => { SyntaxTreeToConsole(syntax); }, ConsoleColor.DarkGray);
                ConsoleUtil.ToConsoleWithColor(() => { ParsedArgumentsToConsole(appArgs, syntax); }, ConsoleColor.Green);
                // Include how we resolve the admin server port / server, if applicable.
                // By design, silently ignore any error.
                try {
                    SharedCLI.ResolveAdminServer(appArgs, out serverHost, out serverPort, out serverName);
                    ConsoleUtil.ToConsoleWithColor(string.Format("Server \"{0}\" on @ {1}:{2}.", serverName, serverHost, serverPort), ConsoleColor.Yellow);
                } catch { }

                // Exiting, because we were asked to test syntax only.
                return;
            }

            if (appArgs.ContainsFlag(StarOption.Help, CommandLineSection.GlobalOptions)) {
                Usage(syntax);
                return;
            }

            if (appArgs.ContainsFlag(StarOption.HelpEx, CommandLineSection.GlobalOptions)) {
                Usage(syntax, true);
                return;
            }

            if (appArgs.ContainsFlag(StarOption.HelpUnofficial, CommandLineSection.GlobalOptions)) {
                Usage(syntax, true, true);
                return;
            }

            if (appArgs.ContainsFlag(StarOption.Info, CommandLineSection.GlobalOptions)) {
                ShowInfoAboutStarcounter();
                return;
            }

            if (appArgs.ContainsFlag(StarOption.Version, CommandLineSection.GlobalOptions)) {
                ShowVersionInfo();
                return;
            }

            if (appArgs.CommandParameters.Count == 0) {
                ConsoleUtil.ToConsoleWithColor("No file specified. Aborting.", ConsoleColor.Yellow);
                return;
            }

            // We are told to execute. We got at least one parameter. Check if it's
            // the temporary @@CreateRepo.
            if (appArgs.CommandParameters[0].Equals("@@CreateRepo", StringComparison.InvariantCultureIgnoreCase)) {
                CreateServerRepository(appArgs);
                return;
            }

            // We got an application to host.
            // Get the only required parameter - the application - and send it
            // to the server.
            // We utilize a strategy where we do minimal client side validation,
            // because everything we validate here (on the client) actually
            // might not hold true on the server. For example, what if the
            // server employs some cool fallback for sending a file that doesn't
            // exist? We shouldnt take away the ability for the server to do so
            // by validating if the file exist here.
            // So bottomline: a client with "full" transparency.
            //
            // Aware of the client transparency guideline stated previously,
            // we still do resolve the path of the given file based on
            // the location of the client. It's most likely what the user
            // intended.
            // On top of that, we check if we can find the file once resolved
            // and if we can't, we do a try on the given name + some of our
            // supported extensions If we find such file, we assume the user
            // meant that to be the file.
            //   After this resolving has taken place, if we still can't find
            // the file, the correct thing to do is to pass it to the server,
            // at least in theory. We have no way of knowing how the server do
            // handle a file that does not exist. Maybe it can create something
            // on the fly, or using some default?
            //  However, in practice, it's probably a decent thing to fail
            // upfront, right here and now, to offload the server and dont have
            // a lot of processing being done when the file is missing. So that
            // is what we do. If we find we must be more strict to theory later
            // on, we should implement a swich that allows this to be turned of.
            var filePath = appArgs.CommandParameters[0];
            try {
                filePath = Path.GetFullPath(filePath);
            } catch {
                SharedCLI.ShowErrorAndSetExitCode(
                    ErrorCode.ToMessage(Error.SCERREXECUTABLENOTFOUND, string.Format("File: \"{0}\"", filePath)), true);

            }
            if (!File.Exists(filePath)) {
                var extensionsSupported = new string[] { ".exe", ".cs" };
                foreach (var fileExtension in extensionsSupported) {
                    var filePathCandidate = appArgs.CommandParameters[0] + fileExtension;
                    filePathCandidate = Path.GetFullPath(filePathCandidate);
                    if (File.Exists(filePathCandidate)) {
                        filePath = filePathCandidate;
                        break;
                    }
                }   
            }
            if (!File.Exists(filePath)) {
                SharedCLI.ShowErrorAndSetExitCode(
                    ErrorCode.ToMessage(Error.SCERREXECUTABLENOTFOUND, string.Format("File: \"{0}\"", filePath)), true);
            }

            // The file exist. Check what kind of file we are dealing 
            // with here.
            var applicationFilePath = filePath;
            bool sourceCodeInput = false;

            if (Path.GetExtension(filePath).Equals(".cs", StringComparison.InvariantCultureIgnoreCase)) {
                try {
                    var sourceCode = filePath;
                    SourceCodeCompiler.CompileSingleFileToExecutable(sourceCode, out filePath);
                    applicationFilePath = sourceCode;
                    sourceCodeInput = true;
                } catch (Exception experimental) {
                    SharedCLI.ShowErrorAndSetExitCode(
                        ErrorCode.ToMessage(Error.SCERRUNSPECIFIED, experimental.ToString()), true);
                }
            }

            string[] userArgs = null;
            if (appArgs.CommandParameters != null) {
                int userArgsCount = appArgs.CommandParameters.Count;

                // Check if we have more arguments than one. Remember that we
                // reserve the first argument as the name/path of the executable
                // and that we are really hiding a general "Exec exe [others]"
                // scenario.

                if (userArgsCount > 1) {
                    userArgsCount--;
                    userArgs = new string[userArgsCount];
                    appArgs.CommandParameters.CopyTo(1, userArgs, 0, userArgsCount);
                }
            }

            // Turn to the shared CLI library to do the bulk of the
            // work executing.

            try {
                ExeCLI.StartOrStop(filePath, appArgs, applicationFilePath, userArgs);
            } finally {
                // Delete the temporary executable if we have executed
                // from a script being given.
                if (sourceCodeInput) {
                    try {
                        File.Delete(filePath);
                        var directory = Path.GetDirectoryName(filePath);
                        Directory.Delete(directory);
                    } catch (Exception e) {
                        if (SharedCLI.Verbose) {
                            Console.WriteLine("Failed deleting temporary content: {0}.", e.Message);
                        }
                    }
                }
            }
        }

        static void ShowVersionInfo() {
            Console.WriteLine("Version=", CurrentVersion.Version);
        }

        static void ShowInfoAboutStarcounter() {
            Console.WriteLine("Installation directory={0}", Environment.GetEnvironmentVariable(StarcounterEnvironment.VariableNames.InstallationDirectory));
            Console.WriteLine("Default server={0}", StarcounterEnvironment.ServerNames.PersonalServer.ToLower());
            Console.WriteLine("Version=", CurrentVersion.Version);
        }

        static void Usage(IApplicationSyntax syntax, bool extended = false, bool unofficial = false, uint? exitCode = null) {
            string formatting;
            Console.WriteLine("Usage: star [options] executable [parameters]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            formatting = "  {0,-22}{1,25}";
            Console.WriteLine(formatting, string.Format("-h, --{0}", StarOption.Help), "Shows help about star.exe.");
            Console.WriteLine(formatting, string.Format("-hx, --{0}", StarOption.HelpEx), "Shows extended help about star.exe.");
            Console.WriteLine(formatting, string.Format("-v, --{0}", StarOption.Version), "Prints the version of Starcounter.");
            Console.WriteLine(formatting, string.Format("-i, --{0}", StarOption.Info), "Prints information about the Starcounter installation.");
            Console.WriteLine(formatting, string.Format("-p=,--{0}=port", StarOption.Serverport), "Port to the admin server.");
            Console.WriteLine(formatting, string.Format("--{0}=name", StarOption.Server), "Specifies the name of the server. If no port is");
            Console.WriteLine(formatting, "", "specified, star.exe use the known port of server.");
            Console.WriteLine(formatting, string.Format("--{0}=host", StarOption.ServerHost), "Specifies the identity of the server host. If no");
            Console.WriteLine(formatting, "", "host is specified, 'localhost' is used.");
            Console.WriteLine(formatting, string.Format("-d=,--{0}=name", StarOption.Db), "The database to use. 'Default' is used if not given.");
            Console.WriteLine(formatting, "", "Example \"star d=foo bar.exe\"");
            Console.WriteLine(formatting, string.Format("--{0}", StarOption.Stop), "Stops the given application.");
            Console.WriteLine(formatting, string.Format("--{0}", StarOption.Restart), "Allow the application to be restarted if running.");
            if (extended) {
                Console.WriteLine(formatting, string.Format("--{0}", StarOption.LogSteps), "Enables diagnostic logging.");
                Console.WriteLine(formatting, string.Format("--{0}", StarOption.NoDb), "Tells the host to load and run the executable");
                Console.WriteLine(formatting, "", "without loading any database into the process.");
                Console.WriteLine(formatting, string.Format("--{0}", StarOption.NoAutoCreateDb), "Prevents automatic creation of database.");
                Console.WriteLine(formatting, string.Format("--{0}=path", StarOption.ResourceDirectory), "Sets the default directory for static resources.");
                Console.WriteLine(formatting, string.Format("--{0}", StarOption.Async), "Returns before the entrypoint has finished.");
                Console.WriteLine(formatting, string.Format("--{0}", StarOption.Verbose), "Instructs star.exe to show verbose output.");
                Console.WriteLine(formatting, string.Format("--{0}", StarOption.Syntax), "Shows the parsing of the command-line, then exits.");
                Console.WriteLine(formatting, string.Format("--{0}", StarOption.NoColor), "Instructs star.exe to turn off colorizing output.");
                Console.WriteLine(formatting, string.Format("-hxx, -{0}", StarOption.HelpUnofficial), "Shows unofficial help about star.exe.");
            }
            if (unofficial) {
                Console.WriteLine(formatting, string.Format("--{0}", SharedCLI.UnofficialOptions.Debug), "Attaches a debugger to the star.exe process.");
                Console.WriteLine(formatting, string.Format("--{0}", SharedCLI.UnofficialOptions.CodeHostCommandLineOptions), "Allows for the passing of custom code host parameters");
            }
            Console.WriteLine();
            if (extended) {
                Console.WriteLine("TEMPORARY: Creating a repository.");
                Console.WriteLine("Run star.exe @@CreateRepo path [servername] to create a repository.");
                Console.WriteLine();
            }
            Console.WriteLine("Environment variables:");
            formatting = "{0,-30}{1,25}";
            Console.WriteLine(formatting, StarcounterEnvironment.VariableNames.DefaultServer, "Holds the server to use by default.");
            Console.WriteLine(formatting, StarcounterEnvironment.VariableNames.DefaultServerPersonalPort, "Personal server port used by default.");
            Console.WriteLine(formatting, StarcounterEnvironment.VariableNames.DefaultServerSystemPort, "System server port used by default.");
            Console.WriteLine();
            Console.WriteLine("For complete help, see {0}/{1}.", StarcounterEnvironment.InternetAddresses.StarcounterWiki, "star.exe");

            if (exitCode.HasValue) {
                Environment.Exit((int)exitCode);
            }
        }

        static IApplicationSyntax DefineCommandLineSyntax() {
            ApplicationSyntaxDefinition appSyntax;
            CommandSyntaxDefinition commandSyntax;

            appSyntax = new ApplicationSyntaxDefinition();
            appSyntax.ProgramDescription = "star.exe";
            appSyntax.DefaultCommand = "exec";
            SharedCLI.DefineWellKnownOptions(appSyntax, true);

            appSyntax.DefineFlag(
                StarOption.Help,
                "Prints the star.exe help message.",
                OptionAttributes.Default,
                new string[] { "h" }
                );
            appSyntax.DefineFlag(
                StarOption.HelpEx,
                "Prints the star.exe extended help message.",
                OptionAttributes.Default,
                new string[] { "hx" }
                );
            appSyntax.DefineFlag(
                StarOption.HelpUnofficial,
                "Prints the star.exe unofficial help message.",
                OptionAttributes.Default,
                new string[] { "hxx" }
                );
            appSyntax.DefineFlag(
                StarOption.Version,
                "Prints the version of Starcounter.",
                OptionAttributes.Default,
                new string[] { "v" }
                );
            appSyntax.DefineFlag(
                StarOption.Info,
                "Prints information about Starcounter and star.exe",
                OptionAttributes.Default,
                new string[] { "i" }
                );

            // Extended, advanced functionality

            appSyntax.DefineFlag(
                StarOption.Syntax,
                "Instructs star.exe to just parse the command-line and show the result of that."
                );
            appSyntax.DefineFlag(
                StarOption.NoColor,
                "Instructs star.exe to turn off colorizing output."
                );

            // NOTE:
            // Although we will refuse to execute any EXEC command without at least one parameter,
            // we specify a minimum of 0. The reason is that the exec command is the default, and it
            // will be applied whenever a command is not explicitly given. This in turn means that
            // if we invoke star.exe with a single global option, like --help, the parser will fail
            // since it will apply exec as the default command and force it to have parameters.
            commandSyntax = appSyntax.DefineCommand("exec", "Executes an application", 0, int.MaxValue);

            return appSyntax.CreateSyntax();
        }

        static void SyntaxTreeToConsole(IApplicationSyntax syntax) {
            Console.WriteLine(syntax.ProgramDescription);
            Console.WriteLine("Default command: {0}, required: {1}", syntax.DefaultCommand, syntax.RequiresCommand ? bool.TrueString : bool.FalseString);

            foreach (var prop in syntax.Properties) {
                Console.WriteLine("  {0}  = value", string.Join(",", prop.AllNames));
            }

            foreach (var flag in syntax.Flags) {
                Console.WriteLine("  {0}", string.Join(",", flag.AllNames));
            }

            foreach (var command in syntax.Commands) {
                var supportOptions = command.Properties.Length > 0 || command.Flags.Length > 0;
                string parameterString = "[parameters]";    // TODO:
                Console.WriteLine("    {0}", command.Name);
                foreach (var prop in command.Properties) { 
                    Console.WriteLine("      {0}  = value", string.Join(",", prop.AllNames));
                }
                foreach (var flag in command.Flags) {
                    Console.WriteLine("      {0}", string.Join(",", flag.AllNames));
                }
                Console.WriteLine("      {0}", parameterString);
            }
        }

        // a = \"value\"
        // b = \"value\"
        // x = TRUE
        // exec
        //   d = value
        //   y = TRUE
        //   parameter1, parameter2, parameter3
        static void ParsedArgumentsToConsole(ApplicationArguments args, IApplicationSyntax syntax) {
            var section = CommandLineSection.GlobalOptions;
            string value;

            foreach (var prop in syntax.Properties) {
                if (args.TryGetProperty(prop.Name, section, out value)) {
                    Console.WriteLine("{0}=\"{1}\"", prop.Name, value);
                }
            }

            foreach (var flag in syntax.Flags) {
                if (args.ContainsFlag(flag.Name, section)) {
                    Console.WriteLine("{0}=TRUE", flag.Name);
                }
            }

            if (!args.HasCommmand)
                return;
            
            Console.WriteLine(args.Command);
            section = CommandLineSection.CommandParametersAndOptions;
            var commandSyntax = syntax.Commands.First<ICommandSyntax>((candidate) => {
                return candidate.Name.Equals(args.Command, StringComparison.InvariantCultureIgnoreCase);
            });

            foreach (var prop in commandSyntax.Properties) {
                if (args.TryGetProperty(prop.Name, out value)) {
                    Console.WriteLine("  {0}=\"{1}\"", prop.Name, value);
                }
            }

            foreach (var flag in commandSyntax.Flags) {
                if (args.ContainsFlag(flag.Name)) {
                    Console.WriteLine("  {0}=TRUE", flag.Name);
                }
            }

            Console.Write("  ");
            foreach (var param in args.CommandParameters) {
                Console.Write(param + " ");
            }

            Console.WriteLine();
        }

        static void CreateServerRepository(ApplicationArguments args) {
            if (args.CommandParameters.Count < 2) {
                ConsoleUtil.ToConsoleWithColor("Missing required path argument to @@CreateRepo.", ConsoleColor.Red);
                Console.WriteLine();
                Usage(null);
                return;
            }

            var repositoryPath = args.CommandParameters[1];
            string serverName;

            if (args.CommandParameters.Count > 2) {
                serverName = args.CommandParameters[2];
            } else {
                serverName = StarcounterEnvironment.ServerNames.PersonalServer;
            }

            var setup = RepositorySetup.NewDefault(repositoryPath, serverName);
            setup.Execute();

            ConsoleUtil.ToConsoleWithColor(
                string.Format("New repository \"{0}\" created at {1}", serverName, repositoryPath), ConsoleColor.Green);
        }
    }
}
