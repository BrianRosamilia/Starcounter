// ***********************************************************************
// <copyright file="Control.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using Starcounter.CommandLine;
using Starcounter; // TODO:
using Starcounter.ABCIPC;
using Starcounter.ABCIPC.Internal;
using Starcounter.Advanced;
using Starcounter.Hosting;
using Starcounter.Internal; // TODO:
using Starcounter.Logging;
using StarcounterInternal.Hosting;
using HttpStructs;
using System.Text.RegularExpressions;
using System.IO;
using Starcounter.Internal.JsonPatch;
using Starcounter.Bootstrap.Management;

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
                //Debugger.Launch();

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
                if (!StarcounterInternal.Hosting.ExceptionManager.HandleUnhandledException(ex)) throw;
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
            ProgramCommandLine.TryGetProgramArguments(args, out arguments);
            OnCommandLineParsed();

            if (arguments.ContainsFlag("attachdebugger")) {
                Debugger.Launch();
            }

            configuration = Configuration.Load(arguments);
            OnConfigurationLoaded();

            withdb_ = !configuration.NoDb;
            StarcounterEnvironment.Gateway.NumberOfWorkers = configuration.GatewayNumberOfWorkers;

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

            ulong hlogs = ConfigureLogging(configuration, hmenv);
            if (arguments.ContainsFlag(StarcounterConstants.BootstrapOptionNames.EnableTraceLogging)) {
                System.Diagnostics.Trace.Listeners.Add(new LogTraceListener());
            }
            OnLoggingConfigured();

            ManagementService.Init(configuration.Name);

            // Initializing the BMX manager if network gateway is used.
            if (!configuration.NoNetworkGateway)
            {
                bmx.sc_init_bmx_manager(
                    HttpStructs.GlobalSessions.g_destroy_apps_session_callback,
                    HttpStructs.GlobalSessions.g_create_new_apps_session_callback,
                    Diagnostics.g_error_handling_callback);

                OnBmxManagerInitialized();

                // Initializing package loader.
                Package.InitPackage(() => {

                    SqlRestHandler.Register(
                    configuration.DefaultUserHttpPort,
                    configuration.DefaultSystemHttpPort);

                    PuppetRestHandler.Register(
                    configuration.DefaultUserHttpPort);
                });
            }

            // Configuring host environment.
            ConfigureHost(configuration, hlogs);
            OnHostConfigured();

            // Configuring schedulers.
            hsched_ = ConfigureScheduler(configuration, mem, hmenv, schedulerCount);
            mem += (1024 + (schedulerCount * 512));
            OnSchedulerConfigured();

            // Initialize the Db environment (database name)
            Db.SetEnvironment(new DbEnvironment(configuration.Name, withdb_));

            // Initializing AppsBootstrapper.
            AppsBootstrapper.InitAppsBootstrapper(
                (Byte)configuration.SchedulerCount,
                configuration.DefaultUserHttpPort,
                configuration.DefaultSystemHttpPort,
                configuration.DefaultSessionTimeoutMinutes,
                configuration.Name);

            OnAppsBoostraperInitialized();

            // Configuring database related settings.
            if (withdb_)
            {
                ConfigureDatabase(configuration);
                OnDatabaseConfigured();

                ConnectDatabase(configuration, hsched_, hmenv, hlogs);
                OnDatabaseConnected();
            }

            // Query module.
            Scheduler.Setup((Byte)schedulerCount);
            if (withdb_)
            {
                Starcounter.Query.QueryModule.Initiate(
                    configuration.SQLProcessPort,
                    Path.Combine(configuration.TempDirectory, "sqlschemas"));

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

            // TODO: Fix the proper BMX push channel registration with gateway.
            // Waiting until BMX component is ready.
            if (!configuration.NoNetworkGateway)
            {
                int ir = sccorelib.fix_wait_for_gateway_available(10000);
                if (ir == 0)
                    throw ErrorCode.ToException(Starcounter.Error.SCERRUNSPECIFIED, "fix_wait_for_gateway_available didn't finish within given time interval.");

                OnNetworkGatewayConnected();
            }

            var appDomain = AppDomain.CurrentDomain;
            appDomain.AssemblyResolve += new ResolveEventHandler(Loader.ResolveAssembly);

            OnAppDomainConfigured();

            ManagementService.Setup(configuration.DefaultSystemHttpPort, hsched_, !configuration.NoNetworkGateway);
            OnServerCommandHandlersRegistered();

            if (withdb_)
            {
                Loader.AddBasePackage(hsched_, stopwatch_);
                OnBasePackageLoaded();
            }
           
            } finally { OnEndStart(); }
        }

        /// <summary>
        /// Simple parser for user arguments.
        /// </summary>
        String[] ParseUserArguments(String userArgs)
        {
            char[] parmChars = userArgs.ToCharArray();
            bool inQuote = false;

            for (int i = 0; i < parmChars.Length; i++)
            {
                if (parmChars[i] == '"')
                {
                    parmChars[i] = '\n';
                    inQuote = !inQuote;
                }

                if (!inQuote && parmChars[i] == ' ')
                    parmChars[i] = '\n';
            }

            return (new string(parmChars)).Split(new Char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
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
                // Trying to get user arguments if any.
                String userArgs = null;
                String[] userArgsArray = null;
                configuration.ProgramArguments.TryGetProperty(StarcounterConstants.BootstrapOptionNames.UserArguments, out userArgs);
                if (userArgs != null)
                {
                    // Parsing user arguments.
                    String[] parsedUserArgs = ParseUserArguments(userArgs);

                    // Checking if any parameters determined.
                    if (parsedUserArgs.Length > 0)
                        userArgsArray = parsedUserArgs;
                }

                // Trying to get explicit working directory.
                String workingDir = null;
                configuration.ProgramArguments.TryGetProperty(StarcounterConstants.BootstrapOptionNames.WorkingDir, out workingDir);

                OnArgumentsParsed();

                // Loading the given application.
                Loader.ExecApp(hsched_, configuration.AutoStartExePath, configuration.AutoStartExePath, stopwatch_, workingDir, userArgsArray, true);

                OnAutoStartModuleExecuted();
            }

            OnCodeHostBootCompleted();
                
            // Receive until we are told to shutdown.

            ManagementService.RunUntilShutdown();

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

                if (withdb_)
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

            throw ErrorCode.ToException(Starcounter.Error.SCERRAPPALREADYSTARTED);
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
            Kernel32.MEMORYSTATUSEX m;
            m.dwLength = (uint)sizeof(Kernel32.MEMORYSTATUSEX);
            Kernel32.GlobalMemoryStatusEx(&m);
            uint slabs = (uint)(m.ullTotalPhys / 8192);
            if (slabs > sccorelib.MH4_MENV_MAX_SLABS)
                slabs = sccorelib.MH4_MENV_MAX_SLABS;
            ulong hmenv = sccorelib.mh4_menv_create(mem128, slabs);
            if (hmenv != 0) return hmenv;
            throw ErrorCode.ToException(Starcounter.Error.SCERROUTOFMEMORY);
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

            e = sccorelog.sccorelog_init(hmenv);
            if (e != 0) throw ErrorCode.ToException(e);

            ulong hlogs;
            e = sccorelog.sccorelog_connect_to_logs(
                ScUri.MakeDatabaseUri(ScUri.GetMachineName(), c.ServerName, c.Name),
                c.OutputDirectory,
                null,
                &hlogs
                );
            if (e != 0) throw ErrorCode.ToException(e);

            LogManager.Setup(hlogs);
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
            setup.gateway_num_workers = c.GatewayNumberOfWorkers;
            setup.mem = mem;
            setup.mem_size = space_needed_for_scheduler;
            setup.hmenv = hmenv;
            setup.cpuc = (byte)schedulerCount;
            if (withdb_) orange.orange_configure_scheduler_callbacks(ref setup);
            else orange_nodb.orange_configure_scheduler_callbacks(ref setup);

            void* hsched;
            uint r = sccorelib.cm2_setup(&setup, &hsched);

            Marshal.FreeHGlobal((IntPtr)setup.name);
            Marshal.FreeHGlobal((IntPtr)setup.server_name);
            Marshal.FreeHGlobal((IntPtr)setup.db_data_dir_path);

            if (r == 0) {
                Processor.Setup(hsched);
                return hsched;
            }

            throw ErrorCode.ToException(r);
        }

        /// <summary>
        /// Configures the database.
        /// </summary>
        /// <param name="c">The c.</param>
        private unsafe void ConfigureDatabase(Configuration c)
        {
            uint e;

            e = sccoredb.sccoredb_set_system_variable("NAME", c.Name);
            if (e != 0) throw ErrorCode.ToException(e);

            e = sccoredb.sccoredb_set_system_variable("IMAGEDIR", c.DatabaseDirectory);
            if (e != 0) throw ErrorCode.ToException(e);

            e = sccoredb.sccoredb_set_system_variable("OLOGDIR", c.DatabaseDirectory);
            if (e != 0) throw ErrorCode.ToException(e);

            e = sccoredb.sccoredb_set_system_variable("TLOGDIR", c.DatabaseDirectory);
            if (e != 0) throw ErrorCode.ToException(e);

            e = sccoredb.sccoredb_set_system_variable("TEMPDIR", c.TempDirectory);
            if (e != 0) throw ErrorCode.ToException(e);

            e = sccoredb.sccoredb_set_system_variable("OUTDIR", c.OutputDirectory);
            if (e != 0) throw ErrorCode.ToException(e);

            var callbacks = new sccoredb.sccoredb_callbacks();
            orange.orange_configure_database_callbacks(ref callbacks);
            e = sccoredb.sccoredb_set_system_callbacks(&callbacks);
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
            flags |= sccoredb.SCCOREDB_USE_BUFFERED_IO;
            flags |= sccoredb.SCCOREDB_ENABLE_CHECK_FILE_ON_LOAD;
            //flags |= sccoredb.SCCOREDB_ENABLE_CHECK_FILE_ON_CHECKP;
            flags |= sccoredb.SCCOREDB_ENABLE_CHECK_FILE_ON_BACKUP;
            flags |= sccoredb.SCCOREDB_ENABLE_CHECK_MEMORY_ON_CHECKP;

            int empty;
            e = sccoredb.sccoredb_connect(flags, hsched, hmenv, hlogs, &empty);
            if (e != 0) throw ErrorCode.ToException(e);

            e = filter.init_filter_lib(hmenv);
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
        private Stopwatch stopwatch_;
        
        [Conditional("TRACE")]
        private void Trace(string message, bool restartWatch = false)
        {
            if (restartWatch) {
                stopwatch_.Restart();
                ticksElapsedBetweenProcessStartAndMain_ = 0;
            }
            long elapsedTicks = stopwatch_.ElapsedTicks + ticksElapsedBetweenProcessStartAndMain_;
            Diagnostics.WriteTrace("control", elapsedTicks, message);

            Diagnostics.WriteTimeStamp("CONTROL", message);
        }

        private void OnProcessInitialized()
        {
            ticksElapsedBetweenProcessStartAndMain_ = (DateTime.Now - Process.GetCurrentProcess().StartTime).Ticks;
            stopwatch_ = Stopwatch.StartNew();

            Trace("Bootstrap Main() started.");
        }

        private void OnExceptionFactoryInstalled() { Trace("Exception factory installed."); }
        private void OnCommandLineParsed() { Trace("Command line parsed."); }
        private void OnConfigurationLoaded() { Trace("Configuration loaded."); }
        private void OnAssuredNoOtherProcessWithTheSameName() { Trace("Assured no other process with the same name."); }
        private void OnGlobalMemoryAllocated() { Trace("Global memory allocated."); }
        private void OnKernelMemoryConfigured() { Trace("Kernel memory configured."); }
        private void OnBmxManagerInitialized() { Trace("BMX manager initialized."); }
        private void OnLoggingConfigured() { Trace("Logging configured."); }
        private void OnHostConfigured() { Trace("Host configured."); }
        private void OnSchedulerConfigured() { Trace("Scheduler configured."); }
        private void OnDatabaseConnected() { Trace("Database connected."); }
        private void OnAppsBoostraperInitialized() { Trace("Apps bootstraper initialized."); }
        private void OnDatabaseConfigured() { Trace("Database configured."); }
        private void OnQueryModuleInitiated() { Trace("Query module initiated."); }

        private void OnEndSetup() { Trace("Setup completed."); }

        private void OnSchedulerStarted() { Trace("Scheduler started."); }
        private void OnAppDomainConfigured() { Trace("App domain configured."); }
        private void OnServerCommandHandlersRegistered() { Trace("Server command handlers registered."); }
        private void OnBasePackageLoaded() { Trace("Base package loaded."); }
        private void OnNetworkGatewayConnected() { Trace("Network gateway connected."); }
        private void OnArgumentsParsed() { Trace("Command line arguments parsed."); }
        private void OnAutoStartModuleExecuted() { Trace("Auto start module executed."); }
        private void OnCodeHostBootCompleted() { Trace("Booting completed."); }

        private void OnEndStart() { Trace("Start completed."); }

        private void OnEndRun() { Trace("Run completed.", true); }
        private void OnEndStop() { Trace("Stop completed."); }
        private void OnEndCleanup() { Trace("Cleanup completed."); }
    }
}
