// ***********************************************************************
// <copyright file="ExecCommandProcessor.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using Starcounter.Advanced;
using Starcounter.Bootstrap.Management;
using Starcounter.Internal;
using Starcounter.Rest.ExtensionMethods;
using Starcounter.Server.PublicModel.Commands;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Starcounter.Server.Commands {

    /// <summary>
    /// Executes a queued and dispatched <see cref="ExecCommand"/>.
    /// </summary>
    [CommandProcessor(typeof(ExecCommand))]
    internal sealed partial class ExecCommandProcessor : CommandProcessor {

        /// <summary>
        /// Initializes a new <see cref="ExecCommandProcessor"/>.
        /// </summary>
        /// <param name="server">The server in which the processor executes.</param>
        /// <param name="command">The <see cref="ExecCommand"/> the
        /// processor should exeucte.</param>
        public ExecCommandProcessor(ServerEngine server, ServerCommand command)
            : base(server, command) {
        }

        /// <inheritdoc />
        protected override void Execute() {
            ExecCommand command;
            WeaverService weaver;
            string appRuntimeDirectory;
            string weavedExecutable;
            Database database;
            DatabaseApp app;
            Process codeHostProcess;
            bool databaseExist;

            command = (ExecCommand)this.Command;
            databaseExist = false;
            weavedExecutable = null;
            database = null;
            codeHostProcess = null;

            if (!File.Exists(command.ExecutablePath)) {
                throw ErrorCode.ToException(
                    Error.SCERREXECUTABLENOTFOUND, string.Format("File: {0}", command.ExecutablePath));
            }

            databaseExist = Engine.Databases.TryGetValue(command.DatabaseName, out database);
            if (!databaseExist) {
                throw ErrorCode.ToException(
                    Error.SCERRDATABASENOTFOUND, string.Format("Database: {0}", command.DatabaseName)
                    );
            }

            app = database.Apps.Find(delegate(DatabaseApp candidate) {
                return candidate.OriginalExecutablePath.Equals(command.ExecutablePath, StringComparison.InvariantCultureIgnoreCase);
            });
            if (app != null) {
                throw ErrorCode.ToException(
                    Error.SCERREXECUTABLEALREADYRUNNING,
                    string.Format("Executable {0} is already running in engine {1}.", command.ExecutablePath, command.DatabaseName)
                    );
            }

            codeHostProcess = database.GetRunningCodeHostProcess();
            if (codeHostProcess == null) {
                throw ErrorCode.ToException(
                    Error.SCERRDATABASEENGINENOTRUNNING,
                    string.Format("Database {0}.", command.DatabaseName)
                    );
            }

            var exeKey = Engine.ExecutableService.CreateKey(command.ExecutablePath);
            WithinTask(Task.PrepareExecutable, (task) => {
                weaver = Engine.WeaverService;
                appRuntimeDirectory = Path.Combine(database.ExecutableBasePath, exeKey);

                if (command.NoDb) {
                    weavedExecutable = CopyAllFilesToRunNoDbApplication(command.ExecutablePath, appRuntimeDirectory);
                    OnAssembliesCopiedToRuntimeDirectory();
                } else {
                    weavedExecutable = weaver.Weave(command.ExecutablePath, appRuntimeDirectory);
                    OnWeavingCompleted();
                }
            });

            WithinTask(Task.Run, (task) => {
                Exception codeHostExited = null;
                try {

                    var node = Node.LocalhostSystemPortNode;
                    var serviceUris = CodeHostAPI.CreateServiceURIs(database.Name);
                    app = new DatabaseApp() {
                        OriginalExecutablePath = command.ExecutablePath,
                        ApplicationFilePath = command.ApplicationFilePath,
                        WorkingDirectory = command.WorkingDirectory,
                        Arguments = command.Arguments,
                        ExecutionPath = weavedExecutable,
                        Key = exeKey,
                        IsStartedWithAsyncEntrypoint = command.RunEntrypointAsynchronous
                    };
                    var exe = app.ToExecutable();

                    if (exe.RunEntrypointAsynchronous) {
                        // Just make the asynchronous call and be done with it
                        // We never check anything more.
                        node.POST(serviceUris.Executables, exe.ToJson(), null, null, (Response resp, Object userObject) => { });
                    } else {
                        // Make a asynchronous call, where we let the callback
                        // set the event whenever the code host is done. Until
                        // then, we wait for this, and check that the code host
                        // is running periodically.
                        var confirmed = new ManualResetEvent(false);
                        Response codeHostResponse = null;
                        node.POST(serviceUris.Executables, exe.ToJson(), null, confirmed, (Response resp, object userObject) => {
                            var done = (ManualResetEvent)userObject;
                            codeHostResponse = resp;
                            done.Set();
                        });

                        var timeout = 500;
                        while (!confirmed.WaitOne(timeout)) {
                            codeHostExited = CreateExceptionIfCodeHostTerminated(codeHostProcess, database);
                            if (codeHostExited != null) {
                                throw codeHostExited;
                            }

                            timeout = 20;
                        }
                        confirmed.Dispose();
                        codeHostResponse.FailIfNotSuccess();
                    }
                    OnCodeHostExecRequestProcessed();

                    // The app is successfully loaded in the worker process. We should
                    // keep it referenced in the server and consider the execution of this
                    // processor a success.
                    database.Apps.Add(app);
                    OnDatabaseAppRegistered();

                } catch (Exception ex) {
                    if (codeHostExited != null && ex.Equals(codeHostExited)) {
                        throw;
                    }
                    codeHostExited = CreateExceptionIfCodeHostTerminated(codeHostProcess, database, ex);
                    if (codeHostExited != null)
                        throw codeHostExited;
                    throw ex;
                }
            });

            var result = Engine.CurrentPublicModel.UpdateDatabase(database);
            SetResult(result);

            OnDatabaseStatusUpdated();
        }

        Exception CreateExceptionIfCodeHostTerminated(Process codeHostProcess, Database database, Exception ex = null) {
            Exception result = null;
            codeHostProcess.Refresh();

            if (codeHostProcess.HasExited) {

                // The code host has exited, most likely because something
                // in the bootstrap sequence or in the exec handler has gone
                // wrong. 
                // We count on the code host logging the exact reason,
                // but just in case it fails to do so, we log this from the
                // perspective of the server, including the exit code which 
                // should be a proper starcounter errorcode.
                // We also don't try to amend this right now, since we don't
                // have any good strategy figured out. We start with just
                // logging it and nothing else.

                result = DatabaseEngine.CreateCodeHostTerminated(codeHostProcess, database, ex);
            }

            return result;
        }

        /// <summary>
        /// Adapts to the (temporary) NoDb switch by copying all binary
        /// files possibly referenced by the starting assembly, as given
        /// by <paramref name="assemblyPath"/>, including the starting
        /// assembly itself.
        /// </summary>
        /// <param name="assemblyPath">Full path to the original assembly,
        /// i.e. the assembly we are told to execute.</param>
        /// <param name="runtimeDirectory">The runtime directory where the
        /// assembly will actually run from, when hosted in Starcounter.</param>
        /// <returns>Full path to the assembly that is about to be executed.
        /// </returns>
        string CopyAllFilesToRunNoDbApplication(string assemblyPath, string runtimeDirectory) {
            #region Copying of a single binary + it's symbol file (i.e. pdb)
            Action<string, string> copyBinary = (string sourceFile, string targetDirectory) => {
                string sourceDirectory;
                string fileNameNoExtension;
                string symbolFileName;
                string sourceSymbolFile;

                sourceDirectory = Path.GetDirectoryName(sourceFile);
                fileNameNoExtension = Path.GetFileNameWithoutExtension(sourceFile);
                symbolFileName = string.Concat(fileNameNoExtension, ".pdb");
                sourceSymbolFile = Path.Combine(sourceDirectory, symbolFileName);

                File.Copy(sourceFile, Path.Combine(targetDirectory, Path.GetFileName(sourceFile)), true);
                if (File.Exists(sourceSymbolFile)) {
                    File.Copy(sourceSymbolFile, Path.Combine(targetDirectory, symbolFileName), true);
                }
            };
            #endregion

            Directory.CreateDirectory(runtimeDirectory);

            var extensions = new string[] { ".dll", ".exe" };
            foreach (var extension in extensions) {
                foreach (var item in Directory.GetFiles(Path.GetDirectoryName(assemblyPath), "*" + extension, SearchOption.TopDirectoryOnly)) {
                    if (item.EndsWith(".vshost.exe"))
                        continue;

                    copyBinary(item, runtimeDirectory);
                }
            }

            return Path.Combine(runtimeDirectory, Path.GetFileName(assemblyPath));
        }

        void OnExistingWorkerProcessStopped() { Trace("Existing worker process stopped."); }
        void OnAssembliesCopiedToRuntimeDirectory() { Trace("Assemblies copied to runtime directory."); }
        void OnWeavingCompleted() { Trace("Weaving completed."); }
        void OnDatabaseCreated() { Trace("Database created."); }
        void OnDatabaseRegistered() { Trace("Database registered."); }
        void OnDatabaseProcessStarted() { Trace("Database process started."); }
        void OnWorkerProcessStarted() { Trace("Worker process started."); }
        void OnHostingInterfaceConnected() { Trace("Hosting interface connected."); }
        void OnPingRequestProcessed() { Trace("Ping request processed."); }
        void OnCodeHostExecRequestProcessed() { Trace("Code host Exec request processed."); }
        void OnDatabaseAppRegistered() { Trace("Database app registered."); }
        void OnDatabaseStatusUpdated() { Trace("Database status updated."); }
    }
}