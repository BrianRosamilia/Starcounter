// ***********************************************************************
// <copyright file="ProgramCommandLine.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Starcounter.CommandLine;
using Starcounter.CommandLine.Syntax;
using Starcounter.Internal;
using Starcounter;

namespace StarcounterInternal.Bootstrap {
    using Error = Starcounter.Internal.Error;

    /// <summary>
    /// Contains a set of utility methods responsible for defining, parsing
    /// and handling errors for the command line parameters given to this
    /// program.
    /// </summary>
    public static class ProgramCommandLine {
        static IApplicationSyntax syntax;

        /// <summary>
        /// Gets the command-line syntax of the code host bootstrapping
        /// program
        /// </summary>
        public static IApplicationSyntax Syntax {
            get {
                if (syntax == null) {
                    syntax = CreateSyntax();
                }
                return syntax;
            }
        }

        /// <summary>
        /// Tries the get program arguments.
        /// </summary>
        /// <param name="args">The args.</param>
        /// <param name="arguments">The arguments.</param>
        internal static void TryGetProgramArguments(string[] args, out ApplicationArguments arguments) {
            Parser parser;

            try {
                // If no arguments are given, use the syntax to create a Usage
                // message, just as is expected when giving /help or /? to a program.
                // Only do this when the console hasn't been redirected though.
                // And exit instantly thereafter.
                if (args.Length == 0 && !Console.IsInputRedirected) {
                    Usage(syntax, null);
                    Environment.Exit((int)Error.SCERRBADCOMMANDLINESYNTAX);
                }

                // Parse and evaluate the given input.
                // If parsing fails, an exception will be raised that pinpoints
                // the error and has an error code indicating what is wrong. Let
                // that exception slip through to the top-level handler.
                parser = new Parser(args);
                arguments = parser.Parse(Syntax);

            } catch (Exception e) {
                // Since this method is normally called by the code host
                // even before the logging system is bound, we need to be
                // extra careful here, trying our best to at least provide
                // some kind of information that could be useful.
                uint error;
                if (ErrorCode.TryGetCode(e, out error)) {
                    throw;
                }

                error = Error.SCERRBADCOMMANDLINESYNTAX;
                throw ErrorCode.ToException(error, e);
            }
        }

        /// <summary>
        /// Usages the specified syntax.
        /// </summary>
        /// <param name="syntax">The syntax.</param>
        /// <param name="argumentException">The argument exception.</param>
        internal static void Usage(
            IApplicationSyntax syntax,
            InvalidCommandLineException argumentException) {
            // Print a usage message, based on the syntax.
            // If the argument exception given is not null, print a header with
            // the message.

            if (argumentException != null) {
                Console.WriteLine(argumentException.Message);
                Console.WriteLine();
            } else {
                Console.WriteLine(syntax.ProgramDescription);
                Console.WriteLine();
            }

            Console.WriteLine("Usage:");
            Console.WriteLine();
            Console.Write(StarcounterConstants.ProgramNames.ScCode + ".exe");
            foreach (var globalFlag in syntax.Flags) {
                Console.Write(" --FLAG:{0}", globalFlag.Name);
            }

            foreach (var globalProperty in syntax.Properties) {
                Console.Write(" --{0}=(value)", globalProperty.Name);
            }

            for (int i = 0; i < syntax.Commands.Length; i++) {
                var currentCommand = syntax.Commands[i];
                if (i > 0)
                    Console.Write(" or");
                Console.Write(" {0}", currentCommand.Name);
                if (currentCommand.MinParameterCount == 0 && currentCommand.MaxParameterCount == 0) {
                    // No parameters
                } else if (currentCommand.MinParameterCount == currentCommand.MaxParameterCount) {
                    Console.Write(" ({0} parameter(s))", currentCommand.MinParameterCount);
                } else {
                    Console.Write(" ({0}-{1} parameters)", currentCommand.MinParameterCount, currentCommand.MaxParameterCount);
                }

                foreach (var flag in currentCommand.Flags) {
                    Console.Write(" --FLAG:{0}", flag.Name);
                }

                foreach (var property in currentCommand.Properties) {
                    Console.Write(" --{0}=(value)", property.Name);
                }
            }

            Console.WriteLine();
        }

        static IApplicationSyntax CreateSyntax() {
            ApplicationSyntaxDefinition syntaxDefinition;
            CommandSyntaxDefinition commandDefinition;
            
            // Define the general program syntax and specify the
            // default command.

            syntaxDefinition = new ApplicationSyntaxDefinition();
            syntaxDefinition.ProgramDescription = "Runs the database user code worker process";
            syntaxDefinition.DefaultCommand = StarcounterConstants.BootstrapCommandNames.Start;

            // Define the global flag allowing a debugger to be attached
            // to the process when starting. Undocumented, internal flag.

            syntaxDefinition.DefineFlag(
                "attachdebugger",
                "Attaches a debugger to the process during startup."
                );

            // Define the Start command. Exactly one parameter - the database identity - is
            // expected. From this, the minimum command line will be:
            // > prog.exe mydatabase
            // (where we have omitted Start, since its the default).

            commandDefinition = syntaxDefinition.DefineCommand(StarcounterConstants.BootstrapCommandNames.Start, "Starts the named database", 1);

            // Specifies the property set we accept.
            // A full command line could look like
            // > prog.exe mydatabase --DatabaseDir "C:\MyDatabase" --OutputDir "C:\Out" --TempDir "C:\Temp"
            // --AutoStartExePath "c:\github\Orange\bin\Debug\NetworkIoTest\NetworkIoTest.exe" --ServerName PERSONAL --ChunksNumber 1024

            commandDefinition.DefineProperty(StarcounterConstants.BootstrapOptionNames.DatabaseDir, "Specifies the database directory to use.");
            commandDefinition.DefineProperty(StarcounterConstants.BootstrapOptionNames.OutputDir, "Specifies the output directory to use.");
            commandDefinition.DefineProperty(StarcounterConstants.BootstrapOptionNames.TempDir, "Specifies the temporary directory to use.");
            commandDefinition.DefineProperty(StarcounterConstants.BootstrapOptionNames.ServerName, "Specifies the name of Starcounter server which started the database.");
            commandDefinition.DefineProperty(StarcounterConstants.BootstrapOptionNames.ChunksNumber, "Specifies the total number of chunks used for shared memory communication.");
            commandDefinition.DefineProperty(StarcounterConstants.BootstrapOptionNames.DefaultSessionTimeoutMinutes, "Specifies the default session timeout in minutes.");
            commandDefinition.DefineProperty(StarcounterConstants.BootstrapOptionNames.GatewayWorkersNumber, "Specifies the number of gateway workers.");
            commandDefinition.DefineProperty(StarcounterConstants.BootstrapOptionNames.AutoStartExePath, "Specifies the path to executable that should be run on startup.");
            commandDefinition.DefineProperty(StarcounterConstants.BootstrapOptionNames.SQLProcessPort, "Specifies TCP/IP port to be used by " + StarcounterConstants.ProgramNames.ScSqlParser + ".exe.");
            commandDefinition.DefineProperty(StarcounterConstants.BootstrapOptionNames.DefaultUserHttpPort, "Specifies default HTTP port for user code.");
            commandDefinition.DefineProperty(StarcounterConstants.BootstrapOptionNames.DefaultSystemHttpPort, "Specifies system HTTP port for user code.");
            commandDefinition.DefineProperty(StarcounterConstants.BootstrapOptionNames.SchedulerCount, "Specifies the number of schedulers.");
            commandDefinition.DefineProperty(StarcounterConstants.BootstrapOptionNames.UserArguments, "User command line arguments.");
            commandDefinition.DefineProperty(StarcounterConstants.BootstrapOptionNames.WorkingDir, "Working directory for applet.");

            commandDefinition.DefineFlag(StarcounterConstants.BootstrapOptionNames.NoDb, "Instructs the program not to connect to the database nor use the SQL engine.");
            commandDefinition.DefineFlag(StarcounterConstants.BootstrapOptionNames.NoNetworkGateway, "Indicates that the host does not need to utilize with network gateway.");
            commandDefinition.DefineFlag(StarcounterConstants.BootstrapOptionNames.EnableTraceLogging, "Instructs the host to write anything being traced to the log.");

            // Create the syntax, validating it
            return syntaxDefinition.CreateSyntax();
        }
    }
}
