// ***********************************************************************
// <copyright file="DatabaseHostingService.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Starcounter.Server {
    /// <summary>
    /// Implements the functionality used by the server to interact with
    /// the the database host.
    /// </summary>
    internal sealed class DatabaseHostingService {
        /// <summary>
        /// Gets the server that has instantiated this service.
        /// </summary>
        readonly ServerEngine engine;

        /// <summary>
        /// Initializes a new instance of the <see cref="DatabaseHostingService"/>
        /// class.
        /// </summary>
        /// <param name="engine">The <see cref="ServerEngine"/> in which the
        /// service will live.</param>
        internal DatabaseHostingService(ServerEngine engine) {
            this.engine = engine;
        }

        /// <summary>
        /// Executes setup of the current <see cref="DatabaseHostingService"/>.
        /// </summary>
        internal void Setup() {
        }
    }
}
