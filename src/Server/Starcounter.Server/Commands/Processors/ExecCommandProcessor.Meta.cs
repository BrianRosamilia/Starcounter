// ***********************************************************************
// <copyright file="ExecAppCommandProcessor.Meta.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using Starcounter.Internal;
using Starcounter.Server.PublicModel;
using Starcounter.Server.PublicModel.Commands;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Starcounter.Server.Commands {

    internal sealed partial class ExecCommandProcessor : CommandProcessor {

        public static int ProcessorToken = CreateToken(typeof(ExecCommandProcessor));
        
        public static CommandDescriptor MakeDescriptor() {
            return new CommandDescriptor() {
                ProcessorToken = ProcessorToken,
                CommandDescription = "Executes an executable inside a Starcounter host",
                Tasks = new TaskInfo[] { 
                    Task.PrepareExecutable.ToPublicModel(), 
                    Task.Run.ToPublicModel() 
                }
            };
        }

        internal static class Task {

            internal static readonly CommandTask PrepareExecutable = new CommandTask(
                ExecCommand.DefaultProcessor.Tasks.PrepareExecutableAndFiles,
                "Preparing user executables and files",
                TaskDuration.NormalIndeterminate,
                "Prepares the user code to be hosted in the code host, weaving and/or copying it."
                );

            internal static readonly CommandTask Run = new CommandTask(
                ExecCommand.DefaultProcessor.Tasks.RunInCodeHost,
                "Loading executable in code host",
                TaskDuration.NormalIndeterminate,
                "Makes a request to the code host to load and execute the prepared executable."
                );
        }
    }
}