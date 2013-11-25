// ***********************************************************************
// <copyright file="AppInfo.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Starcounter.Server.PublicModel {
    /// <summary>
    /// Exposes the properties of a Starcounter App.
    /// </summary>
    public sealed class AppInfo {
        /// <summary>
        /// Gets the principal path of the executable originally
        /// starting the App.
        /// </summary>
        /// <remarks>
        /// This path is not neccessary (and even most likely not)
        /// the path to the executable really loaded, since Starcounter
        /// will process App executables in between them being launched
        /// and when they are actually becoming hosted, and hosting is
        /// normally done from a copy, running in another directory.
        /// </remarks>
        public string ExecutablePath {
            get;
            set;
        }

        /// <summary>
        /// Path to the application file that was used to invoke the
        /// starting of the current application.
        /// </summary>
        /// <remarks>
        /// In the simplest scenario, this path will be equal to 
        /// <c>ExecutablePath</c>, but in a scenario where there is a
        /// transform between the input and the actual executable
        /// (e.g when the input is a source code file), this property
        /// will return the path of the source code file while the
        /// <c>ExecutablePath</c> will return the path to the assembly
        /// compiled on the fly.
        /// </remarks>
        public string ApplicationFilePath {
            get;
            set;
        }

        /// <summary>
        /// Gets the working directory of the App.
        /// </summary>
        public string WorkingDirectory {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the path from which the represented executable
        /// actually runs (governed by the server).
        /// </summary>
        public string ExecutionPath {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the full argument set passed to the executable when
        /// started, possibly including both arguments targeting Starcounter
        /// and/or the actual App Main.
        /// </summary>
        public string[] Arguments {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the server key for this executable. A key must
        /// be assured to be unique within the scope of a single database.
        /// </summary>
        public string Key {
            get;
            set;
        }
    }
}