﻿
using System;
using Starcounter.Server.PublicModel.Commands;

namespace Starcounter.Server.Commands.Processors {
    
    [CommandProcessor(typeof(StopDatabaseCommand))]
    internal sealed class StopDatabaseCommandProcessor : CommandProcessor {
        /// <summary>
        /// Initializes a new <see cref="StopDatabaseCommandProcessor"/>.
        /// </summary>
        /// <param name="server">The server in which the processor executes.</param>
        /// <param name="command">The <see cref="StopDatabaseCommand"/> the
        /// processor should exeucte.</param>
        public StopDatabaseCommandProcessor(ServerEngine server, ServerCommand command)
            : base(server, command) {
        }

        /// </inheritdoc>
        protected override void Execute() {
            StopDatabaseCommand command = (StopDatabaseCommand)this.Command;
            Database database;

            if (!this.Engine.Databases.TryGetValue(command.Name, out database)) {
                throw ErrorCode.ToException(Error.SCERRDATABASENOTFOUND, string.Format("Database: '{0}'.", command.DatabaseUri));
            }

            Engine.DatabaseEngine.StopWorkerProcess(database);
            if (command.StopDatabaseProcess) {
                Engine.DatabaseEngine.StopDatabaseProcess(database);
            }
        }
    }
}