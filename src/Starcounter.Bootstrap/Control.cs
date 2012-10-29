﻿// ***********************************************************************
// <copyright file="Control.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using Starcounter.CommandLine;
using Starcounter; // TODO:
using Starcounter.ABCIPC;
using Starcounter.ABCIPC.Internal;
using Starcounter.Internal; // TODO:
using Starcounter.Logging;
using StarcounterInternal.Hosting;
using Error = Starcounter.Internal.Error;

namespace StarcounterInternal.Bootstrap
{
    /// <summary>
    /// Class Control
    /// </summary>
    public class Control // TODO: Make internal.
    {

        /// <summary>
        /// Defines the entry point of the application.
        /// </summary>
        /// <param name="args">The args.</param>
        public static void Main(string[] args)
        {
            try
            {
                Control c = new Control();
                c.OnProcessInitialized();
                bool b = c.Setup(args);
                if (b)
                {
                    c.Start();
                    c.Run();
                    c.Stop();
                    c.Cleanup();
                }
            }
            catch (Exception ex)
            {
                if (!ExceptionManager.HandleUnhandledException(ex)) throw;
            }
        }

        /// <summary>
        /// Loaded configuration info.
        /// </summary>
        private Configuration configuration;

        /// <summary>
        /// The withdb_
        /// </summary>
        private bool withdb_;

        /// <summary>
        /// The hsched_
        /// </summary>
        private unsafe void* hsched_;

        /// <summary>
        /// The <see cref="Server"/> used as the interface to support local
        /// requests such as the hosting/exeuting of executables and to handle
        /// our servers management demands.
        /// </summary>
        /// <see cref="Control.ConfigureHost"/>
        private Server server;

        /// <summary>
        /// Setups the specified args.
        /// </summary>
        /// <param name="args">The args.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        private unsafe bool Setup(string[] args)
        {
            try {

#if false
            // Disables priority boost for all the threads in the process.
            // Often a good idea when using spin-locks. Not sure it worth
            // anything with the current setup however since most often no more
            // running threads then cores. So leaving this disabled for now.

            Kernel32.SetProcessPriorityBoost(Kernel32.GetCurrentProcess(), 1);
#endif

            DatabaseExceptionFactory.InstallInCurrentAppDomain();
            OnExceptionFactoryInstalled();

            ApplicationArguments arguments;
            if (!ProgramCommandLine.TryGetProgramArguments(args, out arguments))
                return false;
            OnCommandLineParsed();

            configuration = Configuration.Load(arguments);
            OnConfigurationLoaded();

            withdb_ = !configuration.NoDb;

            AssureNoOtherProcessWithTheSameName(configuration);
            OnAssuredNoOtherProcessWithTheSameName();

            uint schedulerCount = configuration.SchedulerCount;
            uint memSize = CalculateAmountOfMemoryNeededForRuntimeEnvironment(schedulerCount);
            byte* mem = (byte*)Kernel32.VirtualAlloc((void *)0, (IntPtr)memSize, Kernel32.MEM_COMMIT, Kernel32.PAGE_READWRITE);
            OnGlobalMemoryAllocated();

            // Note that we really only need 128 bytes. See method
            // CalculateAmountOfMemoryNeededForRuntimeEnvironment for details.

            ulong hmenv = ConfigureMemory(configuration, mem);
            mem += 512;
            OnKernelMemoryConfigured();

            // Initializing the BMX manager if network gateway is used.
            if (!configuration.NoNetworkGateway)
            {
                bmx.sc_init_bmx_manager();
                OnBmxManagerInitialized();
            }

            ulong hlogs = ConfigureLogging(configuration, hmenv);
            OnLoggingConfigured();

            ConfigureHost(configuration, hlogs);
            OnHostConfigured();

            hsched_ = ConfigureScheduler(configuration, mem, hmenv, schedulerCount);
            mem += (1024 + (schedulerCount * 512));
            OnSchedulerConfigured();

            if (withdb_)
            {
                ConfigureDatabase(configuration);
                ConnectDatabase(configuration, hsched_, hmenv, hlogs);
                OnDatabaseConnected();
            }

            // Query module.
            Scheduler.Setup((byte)schedulerCount);
            if (withdb_)
            {
                Starcounter.Query.QueryModule.Initiate(configuration.SQLProcessPort);
                OnQueryModuleInitiated();
            }

            return true;

            } finally { OnEndSetup(); }
        }

        /// <summary>
        /// Starts this instance.
        /// </summary>
        private unsafe void Start()
        {
            try {

            uint e = sccorelib.cm2_start(hsched_);
            if (e != 0) throw ErrorCode.ToException(e);

            OnSchedulerStarted();

            var appDomain = AppDomain.CurrentDomain;
            appDomain.AssemblyResolve += new ResolveEventHandler(Loader.ResolveAssembly);

            OnAppDomainConfigured();

            // Install handlers for the type of requests we accept.

            // Handles execution requests for executables that support
            // lauching into Starcounter from the OS shell. This handler
            // requires only a single parameter - the path to the assembly
            // file - and will use the defaults based on that.
            server.Handle("Exec", delegate(Request r)
            {
                try
                {
                    Loader.ExecApp(hsched_, r.GetParameter<string>());
                }
                catch (LoaderException ex)
                {
                    r.Respond(false, ex.Message);
                }
            });

            server.Handle("Exec2", delegate(Request r)
            {
                try
                {
                    var properties = r.GetParameter<Dictionary<string, string>>();
                    string assemblyPath = properties["AssemblyPath"];
                    string workingDirectory = null;
                    string argsString = null;
                    string[] args = null;

                    properties.TryGetValue("WorkingDir", out workingDirectory);
                    if (properties.TryGetValue("Args", out argsString))
                    {
                        args = KeyValueBinary.ToArray(argsString);
                    }

                    Loader.ExecApp(hsched_, assemblyPath, workingDirectory, args);

                }
                catch (LoaderException ex)
                {
                    r.Respond(false, ex.Message);
                }
            });

            // Ping, allowing clients to check the responsiveness of the
            // code host.

            server.Handle("Ping", delegate(Request request)
            {
                request.Respond(true);
            });

            OnServerCommandHandlersRegistered();

            if (withdb_)
            {
                Loader.AddBasePackage(hsched_);
                OnBasePackageLoaded();
            }

            // TODO: Fix the proper BMX push channel registration with gateway.
            // Waiting until BMX component is ready.
            if (!configuration.NoNetworkGateway)
            {
                bmx.sc_wait_for_bmx_ready();
                OnNetworkGatewayConnected();
            }
            
            } finally { OnEndStart(); }
        }

        /// <summary>
        /// Runs this instance.
        /// </summary>
        private unsafe void Run()
        {
            try {

            // Executing auto-start task if any.
            if (configuration.AutoStartExePath != null)
            {
                // Loading the given application.
                Loader.ExecApp(hsched_, configuration.AutoStartExePath);
                OnAutoStartModuleExecuted();
            }
                
            // Receive until we are told to shutdown.

            server.Receive();

            } finally { OnEndRun(); }
        }

        /// <summary>
        /// Stops this instance.
        /// </summary>
        private unsafe void Stop()
        {
            try {

            uint e = sccorelib.cm2_stop(hsched_, 1);
            if (e == 0) return;
            throw ErrorCode.ToException(e);

            } finally { OnEndStop(); }
        }

        /// <summary>
        /// Cleanups this instance.
        /// </summary>
        private void Cleanup()
        {
            try {

            DisconnectDatabase();

            } finally { OnEndCleanup(); }
        }

        /// <summary>
        /// The process control_
        /// </summary>
        private System.Threading.EventWaitHandle processControl_;

        /// <summary>
        /// Assures the name of the no other process with the same.
        /// </summary>
        /// <param name="c">The c.</param>
        private void AssureNoOtherProcessWithTheSameName(Configuration c)
        {
            try
            {
                bool createdNew;
                processControl_ = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.ManualReset, c.Name, out createdNew);
                if (createdNew) return;
                processControl_.Dispose();
                processControl_ = null;
            }
            catch (UnauthorizedAccessException)
            {
                // Event exists but we can't access it. We treat it the same as
                // if the event exists and we can access it.
            }

            throw ErrorCode.ToException(Error.SCERRAPPALREADYSTARTED);
        }

        /// <summary>
        /// Calculates the amount of memory needed for runtime environment.
        /// </summary>
        /// <param name="schedulerCount">The scheduler count.</param>
        /// <returns>System.UInt32.</returns>
        private uint CalculateAmountOfMemoryNeededForRuntimeEnvironment(uint schedulerCount)
        {
            uint s =
                // Kernel memory setup. We actually only need 128 bytes but in
                // order for per scheduler memory to be aligned to page
                // boundary we allocate 512 bytes for this.

                512 + // 128

                // Scheduler: 1024 shared + 512 per scheduler.

                1024 +
                (schedulerCount * 512) +

                0;
            return s;
        }

        /// <summary>
        /// Configures the memory.
        /// </summary>
        /// <param name="c">The c.</param>
        /// <param name="mem128">The mem128.</param>
        /// <returns>System.UInt64.</returns>
        private unsafe ulong ConfigureMemory(Configuration c, void* mem128)
        {
            uint slabs = (0xFFFFF000 - 4096) / 4096;  // 4 GB - 4 KB
            ulong hmenv = sccorelib.mh4_menv_create(mem128, slabs);
            if (hmenv != 0) return hmenv;
            throw ErrorCode.ToException(Error.SCERROUTOFMEMORY);
        }

        /// <summary>
        /// Configures the logging.
        /// </summary>
        /// <param name="c">The c.</param>
        /// <param name="hmenv">The hmenv.</param>
        /// <returns>System.UInt64.</returns>
        private unsafe ulong ConfigureLogging(Configuration c, ulong hmenv)
        {
            uint e;

            e = sccorelog.SCInitModule_LOG(hmenv);
            if (e != 0) throw ErrorCode.ToException(e);

            ulong hlogs;
            e = sccorelog.SCConnectToLogs(c.Name, null, null, &hlogs);
            if (e != 0) throw ErrorCode.ToException(e);

            e = sccorelog.SCBindLogsToDir(hlogs, c.OutputDirectory);
            if (e != 0) throw ErrorCode.ToException(e);

            return hlogs;
        }

        /// <summary>
        /// Configures the host.
        /// </summary>
        /// <param name="configuration">The <see cref="Configuration"/> to use when
        /// configuring the host.</param>
        /// <param name="hlogs">The hlogs.</param>
        private unsafe void ConfigureHost(Configuration configuration, ulong hlogs)
        {
            uint e = sccoreapp.sccoreapp_init((void*)hlogs);
            if (e != 0) throw ErrorCode.ToException(e);

            LogManager.Setup(hlogs);

            // Decide what interface to expose locally, to handle requests
            // from the server and from executables being loaded from the
            // shell.
            //   Currently, named pipes is the standard means.

            if (!configuration.UseConsole) {
                var pipeName = ScUriExtensions.MakeLocalDatabasePipeString(configuration.ServerName, configuration.Name);
                server = ClientServerFactory.CreateServerUsingNamedPipes(pipeName);

            } else {
                // Expose services via standard streams.
                //
                // If input has not been redirected, we let the server accept
                // requests in a simple text format from the console.
                // 
                // If the input has been redirected, we force the parent process
                // to use the "real" API's (i.e. the Client) and expose our
                // services "raw" on the standard streams.

                if (!Console.IsInputRedirected) {
                    server = ClientServerFactory.CreateServerUsingConsole();
                } else {
                    server = new Server(Console.In.ReadLine, (string reply, bool endsRequest) => {
                        Console.Out.WriteLine(reply);
                    });
                }
            }
        }

        /// <summary>
        /// Configures the scheduler.
        /// </summary>
        /// <param name="c">The c.</param>
        /// <param name="mem">The mem.</param>
        /// <param name="hmenv">The hmenv.</param>
        /// <param name="schedulerCount">The scheduler count.</param>
        private unsafe void* ConfigureScheduler(Configuration c, void* mem, ulong hmenv, uint schedulerCount)
        {
            if (withdb_) orange.orange_setup(hmenv);
            else orange_nodb.orange_setup(hmenv);

            uint space_needed_for_scheduler = 1024 + (schedulerCount * 512);
            sccorelib.CM2_SETUP setup = new sccorelib.CM2_SETUP();
            setup.name = (char*)Marshal.StringToHGlobalUni(c.ServerName + "_" + c.Name);
            setup.server_name = (char*)Marshal.StringToHGlobalUni(c.ServerName);
            setup.db_data_dir_path = (char*)Marshal.StringToHGlobalUni(c.OutputDirectory); // TODO: ?
            setup.is_system = 0;
            setup.num_shm_chunks = c.ChunksNumber;
            setup.mem = mem;
            setup.mem_size = space_needed_for_scheduler;
            setup.hmenv = hmenv;
            setup.cpuc = (byte)schedulerCount;
            if (withdb_) orange.orange_configure_scheduler_callbacks(ref setup);
            else orange_nodb.orange_configure_scheduler_callbacks(ref setup);

            void* hsched;
            uint e = sccorelib.cm2_setup(&setup, &hsched);

            Marshal.FreeHGlobal((IntPtr)setup.name);
            Marshal.FreeHGlobal((IntPtr)setup.server_name);
            Marshal.FreeHGlobal((IntPtr)setup.db_data_dir_path);

            if (e == 0) return hsched;
            throw ErrorCode.ToException(e);
        }

        /// <summary>
        /// Configures the database.
        /// </summary>
        /// <param name="c">The c.</param>
        private unsafe void ConfigureDatabase(Configuration c)
        {
            uint e;

            e = sccoredb.SCConfigSetValue("NAME", c.Name);
            if (e != 0) throw ErrorCode.ToException(e);

            e = sccoredb.SCConfigSetValue("IMAGEDIR", c.DatabaseDirectory);
            if (e != 0) throw ErrorCode.ToException(e);

            e = sccoredb.SCConfigSetValue("OLOGDIR", c.DatabaseDirectory);
            if (e != 0) throw ErrorCode.ToException(e);

            e = sccoredb.SCConfigSetValue("TLOGDIR", c.DatabaseDirectory);
            if (e != 0) throw ErrorCode.ToException(e);

            e = sccoredb.SCConfigSetValue("TEMPDIR", c.TempDirectory);
            if (e != 0) throw ErrorCode.ToException(e);

            e = sccoredb.SCConfigSetValue("COMPPATH", c.CompilerPath);
            if (e != 0) throw ErrorCode.ToException(e);

            e = sccoredb.SCConfigSetValue("OUTDIR", c.OutputDirectory);
            if (e != 0) throw ErrorCode.ToException(e);

            // TODO: What is this configuration for?
            e = sccoredb.SCConfigSetValue("ELOGDIR", c.OutputDirectory);
            if (e != 0) throw ErrorCode.ToException(e);

            sccoredb.sccoredb_config config = new sccoredb.sccoredb_config();
            orange.orange_configure_database_callbacks(ref config);
            e = sccoredb.sccoredb_configure(&config);
            if (e != 0) throw ErrorCode.ToException(e);
        }

        /// <summary>
        /// Connects the database.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="hsched">The hsched.</param>
        /// <param name="hmenv">The hmenv.</param>
        /// <param name="hlogs">The hlogs.</param>
        private unsafe void ConnectDatabase(Configuration configuration, void* hsched, ulong hmenv, ulong hlogs)
        {
            uint e;

            uint flags = 0;
            flags |= sccoredb.SCCOREDB_LOAD_DATABASE;
            flags |= sccoredb.SCCOREDB_ENABLE_CHECK_FILE_ON_LOAD;
            //flags |= sccoredb.SCCOREDB_ENABLE_CHECK_FILE_ON_CHECKP;
            flags |= sccoredb.SCCOREDB_ENABLE_CHECK_FILE_ON_BACKUP;
            flags |= sccoredb.SCCOREDB_ENABLE_CHECK_MEMORY_ON_CHECKP;

            // Temporary solution. See flag docs for details.
            flags |= sccoredb.SCCOREDB_COMPLETE_INIT;

            int empty;
            e = sccoredb.sccoredb_connect(flags, hsched, hmenv, hlogs, &empty);
            if (e != 0) throw ErrorCode.ToException(e);
        }

        /// <summary>
        /// Disconnects the database.
        /// </summary>
        private void DisconnectDatabase()
        {
            uint e = sccoredb.sccoredb_disconnect(0);
            if (e == 0) return;
            throw ErrorCode.ToException(e);
        }

        private long ticksElapsedBetweenProcessStartAndMain_;
        private System.Diagnostics.Stopwatch stopwatch_;
        
        private void OutputElapsedTime(string tag)
        {
            long elapsedTicks = stopwatch_.ElapsedTicks + ticksElapsedBetweenProcessStartAndMain_;
            Console.WriteLine(string.Concat(elapsedTicks / 10000, ".", elapsedTicks % 10000, ":", tag));
        }

        private void OnProcessInitialized()
        {
            ticksElapsedBetweenProcessStartAndMain_ = (DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime).Ticks;
            stopwatch_ = System.Diagnostics.Stopwatch.StartNew();

            OutputElapsedTime("Process initialized");
        }

        private void OnExceptionFactoryInstalled() { OutputElapsedTime("Exception factory installed"); }
        private void OnCommandLineParsed() { OutputElapsedTime("Command line parsed"); }
        private void OnConfigurationLoaded() { OutputElapsedTime("Configuration loaded"); }
        private void OnAssuredNoOtherProcessWithTheSameName() { OutputElapsedTime("Assured no other process with the same name"); }
        private void OnGlobalMemoryAllocated() { OutputElapsedTime("Global memory allocated"); }
        private void OnKernelMemoryConfigured() { OutputElapsedTime("Kernel memory configured"); }
        private void OnBmxManagerInitialized() { OutputElapsedTime("BMX manager initialized"); }
        private void OnLoggingConfigured() { OutputElapsedTime("Logging configured"); }
        private void OnHostConfigured() { OutputElapsedTime("Host configured"); }
        private void OnSchedulerConfigured() { OutputElapsedTime("Scheduler configured"); }
        private void OnDatabaseConnected() { OutputElapsedTime("Database connected"); }
        private void OnQueryModuleInitiated() { OutputElapsedTime("Query module initiated"); }

        private void OnEndSetup() { OutputElapsedTime("Setup completed"); }

        private void OnSchedulerStarted() { OutputElapsedTime("Scheduler started"); }
        private void OnAppDomainConfigured() { OutputElapsedTime("App domain configured"); }
        private void OnServerCommandHandlersRegistered() { OutputElapsedTime("Server command handlers registered"); }
        private void OnBasePackageLoaded() { OutputElapsedTime("Base package loaded"); }
        private void OnNetworkGatewayConnected() { OutputElapsedTime("Network gateway connected"); }
        private void OnAutoStartModuleExecuted() { OutputElapsedTime("Auto start module executed"); }

        private void OnEndStart() { OutputElapsedTime("Start completed"); }
        
        private void OnEndRun() { OutputElapsedTime("Run completed"); }
        private void OnEndStop() { OutputElapsedTime("Stop completed"); }
        private void OnEndCleanup() { OutputElapsedTime("Cleanup completed"); }
    }
}
