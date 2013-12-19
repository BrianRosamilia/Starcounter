// ***********************************************************************
// <copyright file="Package.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using Starcounter.Binding;
using Starcounter.Internal;
using Starcounter.Query;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;

namespace Starcounter.Hosting {

    /// <summary>
    /// Class Package
    /// </summary>
    public class Package {
        /// <summary>
        /// Initializes internal HTTP handlers.
        /// </summary>
        static Action InitInternalHttpHandlers_ = null;

        /// <summary>
        /// Indicates if package was already initialized for all executables.
        /// </summary>
        static Boolean packageInitialized_ = false;

        /// <summary>
        /// Initializes package with global settings.
        /// </summary>
        /// <param name="initInternalHttpHandlers">Initializes internal HTTP handlers.</param>
        public static void InitPackage(Action initInternalHttpHandlers)
        {
            InitInternalHttpHandlers_ = initInternalHttpHandlers;
        }

        /// <summary>
        /// Processes the specified h package.
        /// </summary>
        /// <param name="hPackage">The h package.</param>
        public static void Process(IntPtr hPackage) {
            GCHandle gcHandle = (GCHandle)hPackage;
            Package p = (Package)gcHandle.Target;
            gcHandle.Free();
            p.Process();
        }

        /// <summary>
        /// The unregistered type defs_
        /// </summary>
        private readonly TypeDef[] unregisteredTypeDefs_;

        /// <summary>
        /// The assembly_
        /// </summary>
        private readonly Assembly assembly_;

        private readonly Stopwatch stopwatch_;

        private readonly bool execEntryPointSynchronously_;

        /// <summary>
        /// The processed event_
        /// </summary>
        private readonly ManualResetEvent processedEvent_;
        private volatile uint processedResult;

        /// <summary>
        /// Gets or sets the logical working directory the entrypoint
        /// assembly runs in.
        /// </summary>
        public string WorkingDirectory {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the entrypoint arguments to be given to the
        /// entrypoint when invoked.
        /// </summary>
        public string[] EntrypointArguments {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the full path to the primary file, used to
        /// trigger this package to load. Note that this is normally not
        /// the same file as the one being loaded.
        /// </summary>
        public string PrimaryFilePath { 
            get; 
            set; 
        }

		/// <summary>
        /// Initializes a new instance of the <see cref="Package" /> class.
        /// </summary>
        /// <param name="unregisteredTypeDefs">The unregistered type defs.</param>
        /// <param name="assembly">The assembly.</param>
        /// <param name="stopwatch"></param>
        /// <param name="execEntryPointSynchronously">
        /// If true the event for processing complete will be set after the entrypoint returns, 
        /// if set to false the event will be set before the entrypoint executes.
        /// </param>
        public Package(
            TypeDef[] unregisteredTypeDefs, // Previously unregistered type definitions.
            Assembly assembly,              // Entry point assembly.
            Stopwatch stopwatch,             // Stopwatch used to measure package load times.
            bool execEntryPointSynchronously
            ) {
            unregisteredTypeDefs_ = unregisteredTypeDefs;
            assembly_ = assembly;
            stopwatch_ = stopwatch;
            processedEvent_ = new ManualResetEvent(false);
            processedResult = uint.MaxValue;
            execEntryPointSynchronously_ = execEntryPointSynchronously;
        }

        /// <summary>
        /// Processes this instance.
        /// </summary>
        internal void Process()
        {
            Application application = null;
            if (this.assembly_ != null) {
                // The assembly can be null for internal packages, like
                // the Starcounter assembly/package.
                if (this.EntrypointArguments == null) {
                    this.EntrypointArguments = new string[0];
                }
                application = new Application() {
                    FileName = this.PrimaryFilePath,
                    LoadPath = this.assembly_.Location,
                    WorkingDirectory = this.WorkingDirectory,
                    Arguments = this.EntrypointArguments
                };
                Application.Index(application);
            }

            try
            {
                OnProcessingStarted();

                UpdateDatabaseSchemaAndRegisterTypes();

				if (this.WorkingDirectory != null && !StarcounterEnvironment.IsAdministratorApp)
					AppsBootstrapper.Bootstrap(this.WorkingDirectory); 

                // Initializing package for all executables.
                if ((InitInternalHttpHandlers_ != null) && (!packageInitialized_))
                {
                    // Registering internal HTTP handlers.
                    InitInternalHttpHandlers_();

                    // Indicating that package is now initialized.
                    packageInitialized_ = true;

                    OnInternalHandlersRegistered();
                }

                // Starting user Main() here.
                if (execEntryPointSynchronously_)
                    ExecuteEntryPoint(application);

            } catch (Exception e) {
                uint code = 0;
                if (!ErrorCode.TryGetCode(e, out code)) {
                    code = Error.SCERRUNSPECIFIED;
                }
                processedResult = code;
                throw;
            } finally {
                if (processedResult == uint.MaxValue) {
                    processedResult = 0;
                }

                OnProcessingCompleted();
                processedEvent_.Set();
            }

            if (!execEntryPointSynchronously_)
                ExecuteEntryPoint(application);
        }

        /// <summary>
        /// Waits the until processed.
        /// </summary>
        public uint WaitUntilProcessed() {
            processedEvent_.WaitOne();
            return processedResult;
        }

        /// <summary>
        /// Disposes this instance.
        /// </summary>
        public void Dispose() {
            processedEvent_.Dispose();
        }

        /// <summary>
        /// Updates the database schema and register types.
        /// </summary>
        private void UpdateDatabaseSchemaAndRegisterTypes() {
            var typeDefs = unregisteredTypeDefs_;

            if (typeDefs[0].Name == "Starcounter.Metadata.MaterializedTable") {
                Starcounter.SqlProcessor.SqlProcessor.PopulateRuntimeMetadata();
                OnRuntimeMetadataPopulated();
            }

            if (typeDefs.Length != 0)
            {
                for (int i = 0; i < typeDefs.Length; i++)
                {
                    var typeDef = typeDefs[i];
                    var tableDef = typeDef.TableDef;

                    tableDef = CreateOrUpdateDatabaseTable(tableDef);
                    typeDef.TableDef = tableDef;

                    // Remap properties representing columns in case the column
                    // order has changed.

                    LoaderHelper.MapPropertyDefsToColumnDefs(
                        tableDef.ColumnDefs, typeDef.PropertyDefs, out typeDef.ColumnRuntimeTypes
                        );
                }

                OnDatabaseSchemaCheckedAndUpdated();

                Bindings.RegisterTypeDefs(typeDefs);

                OnTypeDefsRegistered();

                QueryModule.UpdateSchemaInfo(typeDefs);

                OnQueryModuleSchemaInfoUpdated();
            }
        }

        /// <summary>
        /// Creates the or update database table.
        /// </summary>
        /// <param name="tableDef">The table def.</param>
        /// <returns>TableDef.</returns>
        private TableDef CreateOrUpdateDatabaseTable(TableDef tableDef) {
            string tableName = tableDef.Name;
            TableDef storedTableDef = null;
            TableDef pendingUpgradeTableDef = null;

            Db.Transaction(() => {
                storedTableDef = Db.LookupTable(tableName);
                pendingUpgradeTableDef = Db.LookupTable(TableUpgrade.CreatePendingUpdateTableName(tableName));
            });

            if (pendingUpgradeTableDef != null) {
                var continueTableUpgrade = new TableUpgrade(tableName, storedTableDef, pendingUpgradeTableDef);
                storedTableDef = continueTableUpgrade.ContinueEval();
            }

            if (storedTableDef == null) {
                var tableCreate = new TableCreate(tableDef);
                storedTableDef = tableCreate.Eval();
            } else if (!storedTableDef.Equals(tableDef)) {
                var tableUpgrade = new TableUpgrade(tableName, storedTableDef, tableDef);
                storedTableDef = tableUpgrade.Eval();
            }

            return storedTableDef;
        }

        /// <summary>
        /// Executes the entry point.
        /// </summary>
        private void ExecuteEntryPoint(Application application) {
            if (assembly_ != null) {
                var entrypoint = assembly_.EntryPoint;

                try {
                    Application.CurrentAssigned = application;
                    if (entrypoint.GetParameters().Length == 0) {
                        entrypoint.Invoke(null, null);
                    } else {
                        var arguments = this.EntrypointArguments;
                        entrypoint.Invoke(null, new object[] { arguments });
                    }
                } catch (TargetInvocationException te) {
                    var entrypointException = te.InnerException;
                    if (entrypointException == null) throw;

                    var detail = entrypointException.Message;
                    if (!ErrorCode.IsFromErrorCode(entrypointException)) {
                        detail = entrypointException.ToString();
                    }

                    throw ErrorCode.ToException(Error.SCERRFAILINGENTRYPOINT, te, detail);
                } finally {
                    Application.CurrentAssigned = null;
                }

                OnEntryPointExecuted();
            }
        }

        private void OnProcessingStarted() { Trace("Package started."); }
        private void OnInternalHandlersRegistered() { Trace("Internal handlers were registered."); }
        private void OnDatabaseSchemaCheckedAndUpdated() { Trace("Database schema checked and updated."); }
        private void OnTypeDefsRegistered() { Trace("Type definitions registered."); }
        private void OnQueryModuleSchemaInfoUpdated() { Trace("Query module schema information updated."); }
        private void OnEntryPointExecuted() { Trace("Entry point executed."); }
        private void OnProcessingCompleted() { Trace("Processing completed."); }
        private void OnRuntimeMetadataPopulated() { Trace("Runtime meta-data tables were created and populated with initial data."); }

        [Conditional("TRACE")]
        private void Trace(string message)
        {
            Diagnostics.WriteTrace("loader", stopwatch_.ElapsedTicks, message);

            Diagnostics.WriteTimeStamp("PACKAGE", message);
        }
    }
}
