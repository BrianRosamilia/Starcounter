// ***********************************************************************
// <copyright file="DatabaseApp.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using Starcounter.Bootstrap.Management.Representations.JSON;
using Starcounter.Server.PublicModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Starcounter.Server {
    /// <summary>
    /// Represents an "App" as it relates to a <see cref="Database"/> under
    /// a given server.
    /// </summary>
    internal sealed class DatabaseApp {
        /// <summary>
        /// Gets or sets the path to the original executable, i.e. the
        /// executable that caused the materialization of this instance.
        /// </summary>
        internal string OriginalExecutablePath {
            get;
            set;
        }

        /// <summary>
        /// Path to the application file that was used to invoke the
        /// starting of the current application.
        /// </summary>
        /// <remarks>
        /// <see cref="Starcounter.Server.PublicModel.AppInfo.ApplicationFilePath"/>
        /// </remarks>
        public string ApplicationFilePath {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the server key for this executable. A key must
        /// be assured to be unique within the scope of a single database.
        /// </summary>
        internal string Key { 
            get; 
            set;
        }

        /// <summary>
        /// Gets or sets the path to the executable file actually being
        /// loaded into the database, i.e. normally to the file after it
        /// has been weaved.
        /// </summary>
        internal string ExecutionPath {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the working directory of the App.
        /// </summary>
        internal string WorkingDirectory {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the full argument set passed to the executable when
        /// started, possibly including both arguments targeting Starcounter
        /// and/or the actual App Main.
        /// </summary>
        internal string[] Arguments {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets a value indicating if the current application was,
        /// or will be, started with its entrypoint being invoked asynchronously.
        /// </summary>
        /// <remarks>
        /// <para>The default is <c>false</c>. Normally, the entrypoint of any
        /// application is run in a synchronous fashion.</para>
        /// <para>If the application doesn't define an entrypoint - for example,
        /// it's represented by a library or just some code file with a few
        /// classes - this property is silently ignored.</para>
        /// </remarks>
        internal bool IsStartedWithAsyncEntrypoint {
            get;
            set;
        }

        /// <summary>
        /// Creates a snapshot of this <see cref="DatabaseApp"/> in the
        /// form of a public model <see cref="AppInfo"/>.
        /// </summary>
        /// <returns>An <see cref="AppInfo"/> representing the current state
        /// of this executable.</returns>
        internal AppInfo ToPublicModel() {
            return new AppInfo() {
                ExecutablePath = this.OriginalExecutablePath,
                ApplicationFilePath = this.ApplicationFilePath,
                ExecutionPath = this.ExecutionPath,
                Arguments = this.Arguments,
                Key = this.Key
            };
        }

        /// <summary>
        /// Creates an <see cref="Executable"/> instance based on the
        /// properties of the current <see cref="DatabaseApp"/>.
        /// </summary>
        /// <returns>An <see cref="Executable"/> representing the same
        /// application as the current instance.</returns>
        internal Executable ToExecutable() {
            var exe = new Executable();
            exe.Path = this.ExecutionPath;

            exe.PrimaryFile = this.OriginalExecutablePath;
            exe.ApplicationFilePath = this.ApplicationFilePath;
            exe.WorkingDirectory = this.WorkingDirectory;
            if (this.Arguments != null) {
                foreach (var argument in this.Arguments) {
                    exe.Arguments.Add().dummy = argument;
                }
            }
            exe.RunEntrypointAsynchronous = this.IsStartedWithAsyncEntrypoint;
            return exe;
        }
    }
}
