﻿// ***********************************************************************
// <copyright file="DatabaseEngine.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using Starcounter.ABCIPC;
using Starcounter.Server.Commands;
using Starcounter.Server.PublicModel;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using Starcounter.Internal;
using Starcounter.Server.PublicModel.Commands;
using Starcounter.Bootstrap.Management;

namespace Starcounter.Server {

    /// <summary>
    /// Encapsulates and abstracts the database engine, i.e. letting code
    /// using this class act on the database engine without having to know
    /// the exact underlying details about how to actually start it or
    /// what exact input to use.
    /// </summary>
    internal sealed class DatabaseEngine {
        private static class Win32 {
            internal static UInt32 EVENT_MODIFY_STATE = 0x0002;

            [DllImport("Kernel32.dll", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
            internal static extern IntPtr OpenEvent(UInt32 dwDesiredAccess, Int32 bInheritHandle, String lpName);

            [DllImport("Kernel32.dll", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
            internal static extern Int32 CloseHandle(IntPtr hObject);
        }

        internal const string DatabaseExeFileName = StarcounterConstants.ProgramNames.ScData + ".exe";
        internal const string CodeHostExeFileName = StarcounterConstants.ProgramNames.ScCode + ".exe";
        internal const string MinGWCompilerFileName = "x86_64-w64-mingw32-gcc.exe";

        /// <summary>
        /// Gets the server that has instantiated this engine.
        /// </summary>
        internal readonly ServerEngine Server;

        /// <summary>
        /// Gets the database engine monitor service
        /// </summary>
        internal readonly DatabaseEngineMonitor Monitor;

        /// <summary>
        /// Gets the full path to the database executable.
        /// </summary>
        internal string DatabaseExePath {
            get;
            private set;
        }

        /// <summary>
        /// Gets the full path to the code host executable.
        /// </summary>
        internal string CodeHostExePath {
            get;
            private set;
        }

        /// <summary>
        /// Gets the full path to the MinGW compiler executable.
        /// </summary>
        internal string MinGWCompilerPath {
            get;
            private set;
        }

        /// <summary>
        /// Initializes a <see cref="DatabaseEngine"/> for the given
        /// <see cref="ServerEngine"/>.
        /// </summary>
        /// <param name="server"></param>
        internal DatabaseEngine(ServerEngine server) {
            this.Server = server;
            this.Monitor = new DatabaseEngineMonitor(server);
        }

        /// <summary>
        /// Performs setup of the current <see cref="DatabaseEngine"/>.
        /// </summary>
        internal void Setup() {
            var databaseExe = Path.Combine(this.Server.InstallationDirectory, DatabaseExeFileName);
            if (!File.Exists(databaseExe)) {
                throw ErrorCode.ToException(Error.SCERRUNSPECIFIED, string.Format("Database engine executable not found: {0}", databaseExe));
            }
            var codeHostExe = Path.Combine(this.Server.InstallationDirectory, CodeHostExeFileName);
            if (!File.Exists(codeHostExe)) {
                throw ErrorCode.ToException(Error.SCERRUNSPECIFIED, string.Format("Code host executable not found: {0}", databaseExe));
            }
            var compilerPath = Path.Combine(this.Server.InstallationDirectory, @"MinGW\bin", MinGWCompilerFileName);
            if (!File.Exists(compilerPath)) {
                throw ErrorCode.ToException(Error.SCERRUNSPECIFIED, string.Format("MinGW compiler executable not found: {0}", compilerPath));
            }

            this.DatabaseExePath = databaseExe;
            this.CodeHostExePath = codeHostExe;
            this.MinGWCompilerPath = compilerPath;

            this.Monitor.Setup();
        }

        /// <summary>
        /// Starts the database process for the given <see cref="Database"/>.
        /// If it turns out already started, this method silently returns.
        /// </summary>
        /// <param name="database">The <see cref="Database"/> the starting
        /// process should run.</param>
        /// <returns>Returns true if the database was actually started, false
        /// if it was not (i.e. it was already running).</returns>
        internal bool StartDatabaseProcess(Database database) {
            bool databaseRunning;
            try {
                string processControlEventName;
                EventWaitHandle processControlEvent;

                processControlEventName = GetDatabaseControlEventName(database);
                processControlEvent = EventWaitHandle.OpenExisting(
                    processControlEventName,
                    System.Security.AccessControl.EventWaitHandleRights.Synchronize
                    );
                databaseRunning = !processControlEvent.WaitOne(0);
                processControlEvent.Close();

                if (!databaseRunning) {
                    // Process is shutting down. Wait for shutdown to complete and
                    // restart it.

                    WaitForDatabaseProcessToExit(processControlEventName);
                }
            } catch (WaitHandleCannotBeOpenedException) {
                databaseRunning = false;
            }

            if (!databaseRunning) {
                ProcessStartInfo startInfo;
                startInfo = GetDatabaseStartInfo(database);
                DoStartEngineProcess(startInfo, database);
            }

            return !databaseRunning;
        }

        /// <summary>
        /// Stops the database process for the given <see cref="Database"/>.
        /// If it turns out already stopped, this method silently returns.
        /// </summary>
        /// <param name="database">The <see cref="Database"/> the stopping
        /// process runs.</param>
        /// <returns>Returns true if the database was actually stopped, false
        /// if it was not (i.e. it was not running).</returns>
        internal bool StopDatabaseProcess(Database database) {
            string processControlEventName;
            EventWaitHandle processControlEvent;
            string errorReason;

            processControlEventName = GetDatabaseControlEventName(database);
            try {
                processControlEvent = EventWaitHandle.OpenExisting(processControlEventName, EventWaitHandleRights.Modify);
            } catch (WaitHandleCannotBeOpenedException) {
                processControlEvent = null;
            } catch (UnauthorizedAccessException) {
                errorReason =
                    string.Format("User '{0}' unauthorized to modify named control event \"{1}\".",
                    WindowsIdentity.GetCurrent().Name,
                    processControlEventName
                    );
                throw new ErrorInfoException(new ErrorInfo[] { new ErrorInfo("StopDatabaseFailed", database.Name, errorReason) });
            }

            if (processControlEvent == null) {
                return false;
            }

            processControlEvent.Set();
            processControlEvent.Close();
            processControlEvent = null;
            Thread.Yield();

            WaitForDatabaseProcessToExit(processControlEventName);
            return true;
        }

        internal bool IsDatabaseProcessRunning(Database database) {
            String processControlEventName = GetDatabaseControlEventName(database);
            return IsDatabaseProcessRunning(processControlEventName);
        }

        internal bool IsDatabaseProcessRunning(string processControlEventName) {
            IntPtr hProcessControlEvent;

            hProcessControlEvent = Win32.OpenEvent(Win32.EVENT_MODIFY_STATE, 0, processControlEventName);
            if (hProcessControlEvent != IntPtr.Zero) {
                Win32.CloseHandle(hProcessControlEvent);
                return true;
            } else {
                return false;
            }

#if false
            EventWaitHandle processControlEvent;
            try
            {
                processControlEvent = EventWaitHandle.OpenExisting(processControlEventName, EventWaitHandleRights.Modify);
                processControlEvent.Close();
                return true;
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                return false;
            }
#endif
        }

        internal bool IsCodeHostProcessRunning(Database database) {
            return database.GetRunningCodeHostProcess() != null;
        }

        internal bool StartCodeHostProcess(Database database, out Process process) {
            return StartCodeHostProcess(database, false, false, out process);
        }

        internal bool StartCodeHostProcess(Database database, bool startWithNoDb, bool applyLogSteps, out Process process) {
            process = database.GetRunningCodeHostProcess();
            if (process != null) 
                return false;

            // No process referenced, or the referenced process was not
            // alive. Start a code host process.

            var startInfo = GetCodeHostProcessStartInfo(database, startWithNoDb, applyLogSteps);
            process = DoStartEngineProcess(startInfo, database);
            database.CodeHostProcess = process;
            database.SupposedToBeStarted = true;
            database.CodeHostArguments = startInfo.Arguments;
            return true;
        }

        internal bool StopCodeHostProcess(Database database) {
            var process = database.CodeHostProcess;
            if (process == null)
                return false;

            process.Refresh();
            if (process.HasExited) {
                ResetToCodeHostNotRunning(database);
                SafeClose(process);
                return false;
            }

            // The process is alive; we should tell it to shut down and
            // release the reference.

            var node = new Node("127.0.0.1", NewConfig.Default.SystemHttpPort);
            node.InternalSetLocalNodeForUnitTests(false);
            var serviceUris = CodeHostAPI.CreateServiceURIs(database.Name);
            
            var response = node.DELETE(serviceUris.Host, null, null, null); 
            if (!response.IsSuccessStatusCode) {
                // If the host actively refused to shut down, we never try to
                // kill it by force. Instead, we raise an exception that will later
                // be logged, describing this scenario.
                throw ErrorCode.ToException(
                    Error.SCERRCODEHOSTPROCESSREFUSEDSTOP, FormatCodeHostProcessInfoString(database, process));
            }

            // Wait for the user code process to exit. First wait for a short while,
            // them write out a warning that it takes longer than expected. Then wait
            // a little longer, and finally emit an error in the log, before we
            // finally kill the process.
            if (!process.WaitForExit(1000 * 5)) {
                var log = ServerLogSources.Default;
                var infoString = FormatCodeHostProcessInfoString(database, process);
                log.LogWarning("User code process takes longer than expected to exit. ({0})", infoString);
                if (!process.WaitForExit(1000 * 15)) {
                    // Emit the error and kill it.
                    // Suffix the message with the information that we killed it, and
                    // append the info about the database/process.
                    log.LogError(ErrorCode.ToMessage(
                        Error.SCERRCODEHOSTPROCESSNOTEXITED,
                        string.Format("{0}. ({1})", "Killing it.", infoString)).ToString());
                    process.Kill();
                }
            }

            ResetToCodeHostNotRunning(database);
            SafeClose(process);
            return true;
        }

        internal void ResetToCodeHostNotRunning(Database database) {
            database.CodeHostProcess = null;
            database.CodeHostArguments = null;
            database.Apps.Clear();
            database.SupposedToBeStarted = false;
        }

        internal void SafeClose(Process p) {
            try { p.Close(); } catch { }
        }

        Process DoStartEngineProcess(ProcessStartInfo startInfo, Database database) {
            var p = Process.Start(startInfo);
            this.Monitor.BeginMonitoring(database, p);
            return p;
        }

        void WaitForDatabaseProcessToExit(string processControlEventName) {
            while (IsDatabaseProcessRunning(processControlEventName)) Thread.Sleep(1);
        }

        ProcessStartInfo GetDatabaseStartInfo(Database database) {
            var arguments = new StringBuilder();

            arguments.Append(database.Name.ToUpperInvariant());
            arguments.Append(' ');

            arguments.Append('\"');
            arguments.Append(database.Uri);
            //arguments.Append(database.Name);
            arguments.Append('\"');
            arguments.Append(' ');

            arguments.Append('\"');
            arguments.Append(database.Server.Configuration.LogDirectory.TrimEnd('\\'));
            arguments.Append('\"');

            return new ProcessStartInfo(this.DatabaseExePath, arguments.ToString());
        }

        ProcessStartInfo GetCodeHostProcessStartInfo(Database database, bool startWithNoDb = false, bool applyLogSteps = false) {
            ProcessStartInfo processStart;
            StringBuilder args;

            args = new StringBuilder();
            // args.Append("--FLAG:attachdebugger ");  // Apply to attach a debugger to the boot sequence.
            args.Append(database.Name.ToUpper());
            args.AppendFormat(" --" + StarcounterConstants.BootstrapOptionNames.DatabaseDir + "=\"{0}\"", database.Configuration.Runtime.ImageDirectory);
            args.AppendFormat(" --" + StarcounterConstants.BootstrapOptionNames.OutputDir + "=\"{0}\"", database.Server.Configuration.LogDirectory);
            args.AppendFormat(" --" + StarcounterConstants.BootstrapOptionNames.TempDir + "=\"{0}\"", database.Configuration.Runtime.TempDirectory);
            args.AppendFormat(" --" + StarcounterConstants.BootstrapOptionNames.CompilerPath + "=\"{0}\"", this.MinGWCompilerPath);
            args.AppendFormat(" --" + StarcounterConstants.BootstrapOptionNames.DefaultUserHttpPort + "={0}", database.Configuration.Runtime.DefaultUserHttpPort);
            args.AppendFormat(" --" + StarcounterConstants.BootstrapOptionNames.DefaultSystemHttpPort + "={0}", database.Server.Configuration.SystemHttpPort);
            args.AppendFormat(" --" + StarcounterConstants.BootstrapOptionNames.SQLProcessPort + "={0}", database.Configuration.Runtime.SQLProcessPort);
            
            if (startWithNoDb) {
                args.Append(" --FLAG:" + StarcounterConstants.BootstrapOptionNames.NoDb);
            }
            // args.Append(" --FLAG:" + ProgramCommandLine.OptionNames.NoNetworkGateway);
            if (applyLogSteps) {
                args.Append(" --FLAG:" + StarcounterConstants.BootstrapOptionNames.EnableTraceLogging);
            }

            if (database.Configuration.Runtime.SchedulerCount.HasValue) {
                args.AppendFormat(" --" + StarcounterConstants.BootstrapOptionNames.SchedulerCount + "={0}", database.Configuration.Runtime.SchedulerCount.Value);
            }
            
            processStart = new ProcessStartInfo(this.CodeHostExePath, args.ToString().Trim());
            processStart.CreateNoWindow = true;
            processStart.UseShellExecute = false;
            processStart.RedirectStandardInput = true;
            processStart.RedirectStandardOutput = true;
            processStart.RedirectStandardError = true;    
            
            return processStart;
        }

        string GetDatabaseControlEventName(Database database) {
#if false
            ScUri uri = ScUri.FromString(database.Uri);
            string processControlEventName = string.Format(
                "SCDATA_EXE_{0}_{1}",
                uri.ServerName.ToUpperInvariant(),
                uri.DatabaseName.ToUpperInvariant()
                );
            return processControlEventName;
#endif

            ScUri uri = ScUri.FromString(database.Uri);
            string processControlEventName = string.Format(
                "SCDATA_EXE_{0}",
                uri.DatabaseName.ToUpperInvariant()
                );
            return processControlEventName;
        }

        internal static string FormatCodeHostProcessInfoString(Database database, Process process, bool checkExited = false) {
            string pid;
            string info;

            try {
                pid = process.Id.ToString();
            } catch {
                pid = "N/A";
            }

            // Example: ScCode.exe, PID=123, Database=Foo
            info = string.Format("{0}, PID={1}, Database={2}", DatabaseEngine.CodeHostExeFileName, pid, database.Name);
            if (checkExited) {
                try {
                    if (process.HasExited) {
                        info += string.Format(", Exitcode={0}", process.ExitCode);
                    }
                } catch { }
            }

            return info;
        }

        internal static Exception CreateCodeHostTerminated(Process codeHostProcess, Database database, Exception serverException = null) {
            var exitCode = (uint)codeHostProcess.ExitCode;
            var errorPostfix = FormatCodeHostProcessInfoString(database, codeHostProcess, true);

            // If the exit code indicates anything greater than 1,
            // we construct an inner exception based on the exit code.
            // Exit code 1 indicates manual kiling of the process.
            var inner = exitCode > 1 ? 
                ErrorCode.ToException(exitCode, serverException) :
                serverException;
            
            return ErrorCode.ToException(Error.SCERRDATABASEENGINETERMINATED, inner, errorPostfix);
        }
    }
}