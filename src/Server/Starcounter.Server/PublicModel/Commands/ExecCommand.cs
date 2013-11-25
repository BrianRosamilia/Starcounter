// ***********************************************************************
// <copyright file="ExecAppCommand.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using System;
using System.IO;
using System.Linq;
using Starcounter.Internal;
using Starcounter.Server.Commands;

namespace Starcounter.Server.PublicModel.Commands {

    /// <summary>
    /// A command representing the request to start an executable.
    /// </summary>
    public sealed class ExecCommand : DatabaseCommand {
        string databaseName;

        public static class DefaultProcessor {
            public static int Token {
                get { return ExecCommandProcessor.ProcessorToken; }
            }

            public static class Tasks {
                public const int PrepareExecutableAndFiles = 1;
                public const int RunInCodeHost = 2;
            }
        }

        /// <summary>
        /// Gets the path to the executable file requesting to start.
        /// </summary>
        public readonly string ExecutablePath;

        /// <summary>
        /// Path to the application file that was used to invoke the
        /// starting of the about-to-be-started application.
        /// </summary>
        /// <remarks>
        /// <see cref="Starcounter.Server.PublicModel.AppInfo.ApplicationFilePath"/>
        /// </remarks>
        public string ApplicationFilePath {
            get;
            set;
        }

        /// <summary>
        /// Gets the path to the directory the requesting executable
        /// has given as it's working directory;
        /// </summary>
        public readonly string WorkingDirectory;

        /// <summary>
        /// Gets the arguments with which the requesting executable
        /// was started with.
        /// </summary>
        public readonly string[] Arguments;

        /// <summary>
        /// Gets or sets the name of the database the App should load
        /// into.
        /// </summary>
        public string DatabaseName {
            get { return databaseName; }
            set {
                databaseName = value;
                DatabaseUri = ScUri.MakeDatabaseUri(ScUri.GetMachineName(), this.Engine.Name, this.databaseName);
            }
        }

        /// <summary>
        /// Gets or sets a value dictating if the App being executed should
        /// be considered not containing anything that needs the database services
        /// of Starcounter (i.e. weaving, SQL, etc).
        /// </summary>
        /// <remarks>
        /// This switch will likely be made obsolete in the near future, so
        /// use it only if you are very certain of what you do and why.
        /// </remarks>
        public bool NoDb {
            get;
            set;
        }

        /// <summary>
        /// Instructs the processor of this command to run the
        /// entrypoint of the executable in an asynchronous fashion.
        /// </summary>
        public bool RunEntrypointAsynchronous {
            get;
            set;
        }

        /// <summary>
        /// Initializes an instance of <see cref="ExecCommand"/>.
        /// </summary>
        /// <param name="engine">The <see cref="ServerEngine"/> where this command
        /// are to execute.</param>
        /// <param name="assemblyPath">Path to the assembly requesting to start.</param>
        /// <param name="workingDirectory">Working directory the executable has requested to run in.</param>
        /// <param name="arguments">Arguments as passed to the requesting executable.</param>
        public ExecCommand(ServerEngine engine, string assemblyPath, string workingDirectory, string[] arguments)
            : base(engine, null, "Starting {0}", Path.GetFileName(assemblyPath)) {
            if (string.IsNullOrEmpty(assemblyPath)) {
                throw new ArgumentNullException("assemblyPath");
            }
            this.ExecutablePath = assemblyPath;
            if (string.IsNullOrEmpty(workingDirectory)) {
                workingDirectory = Path.GetDirectoryName(this.ExecutablePath);
            }
            this.WorkingDirectory = workingDirectory;
            this.Arguments = arguments;
        }

        /// <inheritdoc />
        internal override void GetReadyToEnqueue() {
            base.GetReadyToEnqueue();
        }
    }
}