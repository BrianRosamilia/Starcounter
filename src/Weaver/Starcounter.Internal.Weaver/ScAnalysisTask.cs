// ***********************************************************************
// <copyright file="ScAnalysisTask.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using PostSharp;
using PostSharp.Extensibility;
using PostSharp.Sdk.CodeModel;
using PostSharp.Sdk.CodeModel.Binding;
using PostSharp.Sdk.CodeModel.Helpers;
using PostSharp.Sdk.CodeModel.TypeSignatures;
using PostSharp.Sdk.CodeWeaver;
using PostSharp.Sdk.Collections;
using PostSharp.Sdk.Extensibility;
using PostSharp.Sdk.Extensibility.Tasks;
using Sc.Server.Weaver.Schema;
using Starcounter;

namespace Starcounter.Internal.Weaver {

    /// <summary>
    /// Analytic part of the weaver. Discovers database classes in data assemblies,
    /// build the database schema and validates it against many rules.
    /// </summary>
    public class ScAnalysisTask : Task {
        /// <summary>
        /// The _root society object type name
        /// </summary>
        private const String _rootSocietyObjectTypeName = "Concepts.Ring1.Something";

        /// <summary>
        /// The _schema
        /// </summary>
        private static readonly DatabaseSchema _schema = new DatabaseSchema();
        /// <summary>
        /// The _initialization blocks
        /// </summary>
        private static readonly Set<InstructionBlock> _initializationBlocks = new Set<InstructionBlock>();
        /// <summary>
        /// The _DB classes in current module
        /// </summary>
        private readonly Dictionary<TypeDefDeclaration, DatabaseClass> _dbClassesInCurrentModule = new Dictionary<TypeDefDeclaration, DatabaseClass>();
        /// <summary>
        /// The _writer
        /// </summary>
        private readonly InstructionWriter _writer = new InstructionWriter();

        /// <summary>
        /// The _forbidden assemblies
        /// </summary>
        private readonly String[] _forbiddenAssemblies = new[] {
            "starcounter.configuration",
            "starcounter.framework",
            "starcounter.management.agent",
            "starcounter.management.client",
            "starcounter.management.console",
            "starcounter.management.contracts",
            "starcounter.management.server",
            "starcounter.management.win32"
        };

        /// <summary>
        /// The _SC app assembly ref
        /// </summary>
        private AssemblyRefDeclaration _scAppAssemblyRef;
        /// <summary>
        /// The _database assembly
        /// </summary>
        private DatabaseAssembly _databaseAssembly;
        /// <summary>
        /// The _discover database class cache
        /// </summary>
        private Dictionary<ITypeSignature, DatabaseClass> _discoverDatabaseClassCache;
        /// <summary>
        /// The _default constructor signature
        /// </summary>
        private MethodSignature _defaultConstructorSignature;
        /// <summary>
        /// The _module
        /// </summary>
        private ModuleDeclaration _module;
        /// <summary>
        /// The _weaver directives
        /// </summary>
        private WeaverDirectives _weaverDirectives;
        /// <summary>
        /// The _weaving helper
        /// </summary>
        private WeavingHelper _weavingHelper;
        /// <summary>
        /// The _not persistent attribute type
        /// </summary>
        private IType _notPersistentAttributeType;
        /// <summary>
        /// The _entity type
        /// </summary>
        private IType _entityType;
        /// <summary>
        /// The _DB object type
        /// </summary>
        private ITypeSignature _dbObjectType;

        /// <summary>
        /// Gets the <see cref="DatabaseSchema" /> for the current application.
        /// </summary>
        /// <value>The database schema.</value>
        /// <remarks>The database schema is a static property shared among all
        /// instances of <see cref="ScAnalysisTask" />. There is one instance
        /// <see cref="ScAnalysisTask" /> for each module composing the application,
        /// but there is a single schema.</remarks>
        public static DatabaseSchema DatabaseSchema {
            get {
                return _schema;
            }
        }

        /// <summary>
        /// Finds the <see cref="ScAnalysisTask" /> instance in a PostSharp project.
        /// </summary>
        /// <param name="project">The PostSharp project.</param>
        /// <returns>The <see cref="ScAnalysisTask" />, or <b>null</b> if the project did not
        /// contain this task.</returns>
        public static ScAnalysisTask GetTask(Project project) {
            return (ScAnalysisTask)project.Tasks["ScAnalyze"];
        }

        /// <summary>
        /// Gets the collection of database classes defined in the current module.
        /// </summary>
        /// <value>The database classes in current module.</value>
        public ICollection<DatabaseClass> DatabaseClassesInCurrentModule {
            get {
                return _dbClassesInCurrentModule.Values;
            }
        }

        /// <summary>
        /// Gets or sets the path of the file to which the schema should be saved.
        /// </summary>
        /// <value>The save to.</value>
        /// <remarks>This property is configured from the PostSharp project file.</remarks>
        [ConfigurableProperty]
        public string SaveTo {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the timestamp with which the save file (see <see cref="SaveTo" />)
        /// should be touched.
        /// </summary>
        /// <value>The timestamp.</value>
        /// <remarks>This property is configured from the PostSharp project file.</remarks>
        [ConfigurableProperty]
        public DateTime Timestamp {
            get;
            set;
        }

        /// <summary>
        /// Gets a value describing the type of transformation we are to execute.
        /// </summary>
        /// <value>The kind of the transformation.</value>
        public WeaverTransformationKind TransformationKind {
            get;
            private set;
        }

        /// <summary>
        /// Locates a PostSharp model type defined in the Starcounter assembly
        /// based on a given .NET representation.
        /// </summary>
        /// <param name="type">The type to be found.</param>
        /// <returns>The PostSharp model type or null, if it was not found.</returns>
        /// <exception cref="System.InvalidOperationException"></exception>
        private IType FindStarcounterType(Type type) {
            if (_scAppAssemblyRef == null) {
                throw new InvalidOperationException();
            }

            return (IType)_scAppAssemblyRef.FindType(
                type.FullName,
                BindingOptions.RequireGenericDefinition
                );
        }

        /// <summary>
        /// Tries to find a reference to the Starcounter assembly from <c>module</c>, either
        /// a direct or indirect reference.
        /// </summary>
        /// <param name="module">The module whose reference set we are to check.</param>
        /// <returns>A reference to the Starcounter assembly or null, if no reference could
        /// be found.</returns>
        internal static AssemblyRefDeclaration FindStarcounterAssemblyReference(ModuleDeclaration module) {
            List<string> evaluated = new List<string>();
            StringComparison comparison;

            // Find the reference to Starcounter. We check the full graph of module,
            // including all it's references, until we find the reference to Starcounter
            // or the entire graph is exhausted.
            //   In earlier versions, there was an assert carried out that any given
            // assembly had only a single reference to an assembly named Starcounter.dll.
            // That has been removed, since it didn't really properly protect us against
            // the scenario with multiple, different Starcounter references since the
            // check must be done among ALL assemblies/modules to be accurate (checking
            // that they ALL reference the SAME Starcounter.dll, and also, the one that
            // is currently loaded, or at least one compatible).

            evaluated = new List<string>();
            comparison = StringComparison.InvariantCultureIgnoreCase;

            // Start recursive lookup
            return FindStarcounterAssemblyReferenceRecursive(
                module,
                comparison,
                evaluated);
        }

        /// <summary>
        /// Performs a recursive lookup of all references from module, trying to
        /// find a reference to the Starcounter assembly
        /// </summary>
        /// <param name="module">The module to search</param>
        /// <param name="comparison">The comparison method to use when comparing the
        /// name of a reference to that of the Starcounter assembly name.</param>
        /// <param name="evaluated">A list of modules already considered, to guard
        /// against infinate lookup.</param>
        /// <returns>A reference to the Starcounter assembly or null, if no reference
        /// could be found.</returns>
        private static AssemblyRefDeclaration FindStarcounterAssemblyReferenceRecursive(
            ModuleDeclaration module,
            StringComparison comparison,
            List<string> evaluated) {
            ModuleDeclaration referencedModule;
            string name;

            name = module.Name.ToLowerInvariant();
            if (evaluated.Contains(name))
                return null;

            evaluated.Add(name);

            foreach (AssemblyRefDeclaration assemblyRef in module.AssemblyRefs) {
                if (string.Equals(assemblyRef.Name, "Starcounter", comparison)) {
                    return assemblyRef;
                }
            }

            foreach (AssemblyRefDeclaration assemblyRef in module.AssemblyRefs) {
                // Don't look in referenced assemblies we KNOW don't reference
                // Starcounter.

                if (assemblyRef.IsMscorlib)
                    continue;

                // Try to get the envolope of the reference and the manifest
                // module from that. If we fail, we just ignore it by design.
                // Modules not reachable right now can't and shouldn't be
                // evaluated.

                try {
                    referencedModule = assemblyRef.GetAssemblyEnvelope().ManifestModule;
                } catch {
                    continue;
                }

                // Recursively consult the referenced assembly

                var found = FindStarcounterAssemblyReferenceRecursive(
                    referencedModule,
                    comparison,
                    evaluated);
                if (found != null) return found;
            }

            return null;
        }

        /// <summary>
        /// Adds a dependency to the <see cref="DatabaseAssembly" />
        /// representing the module currently being analyzed.
        /// </summary>
        /// <param name="moduleDependency">The module dependency.</param>
        private void AddModuleDependencyRecursive(ModuleDeclaration moduleDependency) {
            String name = moduleDependency.Name;

            // First make sure we don't add any recursive reference to
            // mscorelib.dll and Starcounter-related assemblies. We'll make
            // sure to reference the Starcounter-related ones later, but
            // explicitly and not in a recursive mode.

            if (moduleDependency.IsMscorlib)
                return;

            if (name.StartsWith("PostSharp", StringComparison.InvariantCultureIgnoreCase))
                return;

            if (name.Equals("Starcounter", StringComparison.InvariantCultureIgnoreCase))
                return;

            // Right now, we don't recursively add any dependencies to assemblies
            // other than those in the same directory as the assembly we are currently
            // analyzing.

            if (!Path.GetDirectoryName(moduleDependency.FileName).Equals(Path.GetDirectoryName(_module.FileName)))
                return;

            // Add the dependency and it's references if it's not allready
            // added.

            if (AddModuleDependency(name, moduleDependency.FileName)) {
                foreach (AssemblyRefDeclaration assemblyRef in moduleDependency.AssemblyRefs) {
                    AddModuleDependencyRecursive(assemblyRef.GetAssemblyEnvelope().ManifestModule);
                }
            }
        }

        /// <summary>
        /// Adds a dependency to the <see cref="DatabaseAssembly" />
        /// representing the module currently being analyzed.
        /// </summary>
        /// <param name="name">Name of the reference.</param>
        /// <param name="location">Path to the referenced binary. Used to
        /// calculate it's hash.</param>
        /// <returns>True if the reference was added; false if the reference
        /// was already established.</returns>
        private bool AddModuleDependency(string name, string location) {
            if (!_databaseAssembly.Dependencies.ContainsKey(name)) {
                ScAnalysisTrace.Instance.WriteLine("Adding the dependency {{{0}}}.", location);
                _databaseAssembly.Dependencies.Add(name, HashHelper.ComputeHash(location));
                return true;
            }
            return false;
        }

        /// <summary>
        /// Determines the kind of transformation needed for the underlying
        /// assembly/module, based on that settings in the project and on the
        /// state of the assembly itself.
        /// </summary>
        /// <returns>The kind of transformation needed.</returns>
        internal WeaverTransformationKind GetTransformationKind() {
            BindingOptions bindingOptions;
            Type type;
            IType weavedAssemblyAttributeType;
            bool specifiesIPCWeaving;
            bool assemblyIsWeavedForIPC;

            type = typeof(AssemblyWeavedForIPCAttribute);
            bindingOptions = BindingOptions.OnlyExisting | BindingOptions.DontThrowException;
            weavedAssemblyAttributeType = (IType)_module.FindType(typeof(AssemblyWeavedForIPCAttribute), bindingOptions);

            // Decide if the project specifies we should weave for IPC.

            if (string.IsNullOrEmpty(this.Project.Properties["WeaveForIPC"]))
                specifiesIPCWeaving = false;
            else
                specifiesIPCWeaving = this.Project.Properties["WeaveForIPC"].Equals(bool.TrueString);

            // Determine if the assembly is tagged as being weaved by the IPC
            // weaver already.

            assemblyIsWeavedForIPC = weavedAssemblyAttributeType == null
                ? false :
                _module.AssemblyManifest.CustomAttributes.GetOneByType(weavedAssemblyAttributeType) != null;

            // Based on the above checks, establish the appropriate kind
            // of transformation needed.

            if (assemblyIsWeavedForIPC) {
                // The assembly is weaved for IPC. If the project tells us we should
                // weave it for IPC, we can omit transformation (by returning None).
                // Otherwise, we assume we should transform the assembly from it's IPC
                // state to a database-ready assembly.

                if (specifiesIPCWeaving)
                    return WeaverTransformationKind.None;

                return WeaverTransformationKind.IPCToDatabase;
            }

            // The assembly is not weaved for IPC. We assume it is a user code
            // assembly (i.e. one that was never transformed at all). We should
            // either weave it for IPC or directly to a database-ready assembly.

            if (specifiesIPCWeaving)
                return WeaverTransformationKind.UserCodeToIPC;

            return WeaverTransformationKind.UserCodeToDatabase;
        }

        /// <summary>
        /// Inspects the given module and returns a set of weaver directives that
        /// describes how the module should be analysed and weaved.
        /// </summary>
        /// <param name="module">The module to inspect.</param>
        /// <returns>Directives for the module.</returns>
        public static WeaverDirectives GetWeaverDirectives(ModuleDeclaration module) {
            BindingOptions bindOpts;
            CustomAttributeDeclaration knownAssemblyAttribute;
            IType knownAssemblyAttributeType;
            KnownAssemblyAttribute kaa;
            SerializedValue value;
            String proof;
            Type type;
            WeaverDirectives weaverDirectives;

            bindOpts = BindingOptions.OnlyExisting | BindingOptions.DontThrowException;
            type = typeof(KnownAssemblyAttribute);
            knownAssemblyAttributeType = (IType)module.FindType(type, bindOpts);

            weaverDirectives = WeaverDirectives.None;

            if (knownAssemblyAttributeType == null) {
                return weaverDirectives;
            }

            knownAssemblyAttribute = module.AssemblyManifest.CustomAttributes.GetOneByType(knownAssemblyAttributeType);
            if (knownAssemblyAttribute == null) {
                return weaverDirectives;
            }

            value = knownAssemblyAttribute.ConstructorArguments[0].Value;
            weaverDirectives |= (WeaverDirectives)(Int32)value.Value;

            proof = (String)knownAssemblyAttribute.ConstructorArguments[1].Value.Value;
            kaa = new KnownAssemblyAttribute(weaverDirectives, proof);
            if (!kaa.CheckProof(module.AssemblyManifest.GetFullName())) {
#pragma warning disable 618
                ScMessageSource.Instance.Write(SeverityType.Fatal, "SCATV04",
                                               new Object[] { module.AssemblyManifest.GetFullName() });
#pragma warning restore 618
                return WeaverDirectives.None;
            }

            return weaverDirectives;
        }

        /// <summary>
        /// Principal entry point of the task. Invoked by PostSharp.
        /// </summary>
        /// <returns><b>true</b> in case of success, otherwise <b>false</b>.</returns>
        public override Boolean Execute() {
            DatabaseClass databaseClass;
            DatabaseExtensionClass databaseExtensionClass;
            DatabaseKindClass databaseKindClass;
            DatabaseEntityClass databaseEntityClass;
            DatabaseSocietyClass databaseSocietyClass;
            IEnumerator<MetadataDeclaration> typeDefEnumerator;
            IntrinsicTypeSignature voidTypeSign;
            String name;
            TypeDefDeclaration typeDef;

            // Initializes the current instance.

            _module = Project.Module;

            this.TransformationKind = GetTransformationKind();

            // Establish the weaver directives. First check the ones possibly decorated
            // on the assembly, then consider the kind of transformation.
            _weaverDirectives = GetWeaverDirectives(_module);
            if (!WeaverUtilities.IsFromUserCode(this.TransformationKind)) {
                // Any transformation not originating from user code, we don't
                // need to execute validation on.

                _weaverDirectives |=
                    WeaverDirectives.AllowForbiddenDeclarations | WeaverDirectives.ExcludeConstraintValidation;
            }

            _discoverDatabaseClassCache = new Dictionary<ITypeSignature, DatabaseClass>();

#pragma warning disable 618
            ScMessageSource.Instance.Write(
                SeverityType.ImportantInfo,
                "SCINF01",
                new Object[] { _module.Name });
#pragma warning restore 618

            // Create a DatabaseAssembly for the current module and add it to the schema.
            _databaseAssembly = new DatabaseAssembly(_module.AssemblyManifest.Name, _module.AssemblyManifest.GetFullName()) {
                IsTransformed = ((_weaverDirectives &
                                  WeaverDirectives.ExcludeAnyTransformation) == 0),
                HasDebuggingSymbols = _module.HasDebugInfo
            };
            DatabaseSchema.Assemblies.Add(_databaseAssembly);

            // Prepare cached stuff.
            _weavingHelper = new WeavingHelper(_module);
            voidTypeSign = _module.Cache.GetIntrinsic(IntrinsicType.Void);
            _defaultConstructorSignature = new MethodSignature(_module,
                                                               CallingConvention.Default,
                                                               voidTypeSign,
                                                               new IntrinsicTypeSignature[0],
                                                               0);

            // Ensure that the code does not reference forbidden constructs.

            if ((_weaverDirectives & (WeaverDirectives.ExcludeConstraintValidation
                                            | WeaverDirectives.AllowForbiddenDeclarations)) == 0) {
                if (_module.AssemblyManifest.GetPublicKey() != null) {
#pragma warning disable 618
                    ScMessageSource.Instance.Write(
                                        SeverityType.Error,
                                        "SCATV05",
                                        new Object[] { _module.AssemblyManifest.GetFullName() }
                                    );
#pragma warning restore 618
                }
                ValidateForbiddenReferences();
            }

            // Find the reference to Starcounter

            _scAppAssemblyRef = FindStarcounterAssemblyReference(_module);

            if (_scAppAssemblyRef == null) {
                // If there is no reference to Starcounter, we will not need to do any
                // processing of this assembly. We should omit a NOTICE about this, that
                // it would be better to configure this assembly not to be analyzed by
                // the weaver, to improve startup time.

#pragma warning disable 618
                ScMessageSource.Instance.Write(
                    SeverityType.ImportantInfo,
                    "SCATV06",
                    new Object[] { _module.AssemblyManifest.GetFullName() }
                    );
#pragma warning restore 618
            } else {
                // The module/assembly we are told to process references Starcounter,
                // either directly or indirectly. Prepare to run analyzis.

                _entityType = FindStarcounterType(typeof(Entity));
                _dbObjectType = FindStarcounterType(typeof(Entity));
                _notPersistentAttributeType = FindStarcounterType(typeof(NotPersistentAttribute));
                
                // Set up dependencies for this assembly.
                // First assure we add dependencies recursively, starting from the
                // module currently being analyzed. Then add references to the
                // Starcounter- and PostSharp assemblies currently loaded, to assure
                // we invalidate the to-be-cached target assembly if neither change.

                AddModuleDependencyRecursive(_module);
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                    name = assembly.GetName().Name;
                    if (name == "Starcounter" || name.StartsWith("PostSharp")) {
                        AddModuleDependency(name + ".dll", assembly.Location);
                    }
                }

                // Identify types and build the schema.

                typeDefEnumerator = _module.GetDeclarationEnumerator(TokenType.TypeDef);
                while (typeDefEnumerator.MoveNext()) {
                    typeDef = (TypeDefDeclaration)typeDefEnumerator.Current;
                    if (typeDef.Name != "<Module>") {
                        databaseClass = DiscoverDatabaseClass(
                                            (TypeDefDeclaration)typeDefEnumerator.Current
                                        );
                        if (databaseClass != null) {
                            _dbClassesInCurrentModule.Add(typeDef, databaseClass);
                        }
                    }
                }

                // Now that the schema is complete, validate it.

                if ((_weaverDirectives & WeaverDirectives.ExcludeConstraintValidation) == 0) {
                    // Inspect all instructions for the constraint PFV21 (related to passing 
                    // fields by reference). We should do it before constructors are inspected 
                    // so that we can read instruction from the original stream (performance).
                    InspectLoadFieldAddress();

                    // Now process constructors.
                    foreach (KeyValuePair<TypeDefDeclaration, DatabaseClass> pair
                                                            in _dbClassesInCurrentModule) {
                        InspectConstructors(pair.Value, pair.Key);
                    }

                    // Validate the database classes.
                    foreach (DatabaseClass dbc in _dbClassesInCurrentModule.Values) {
                        ScAnalysisTrace.Instance.WriteLine("Validating the database class {0}.",
                                                           dbc.Name);

                        databaseSocietyClass = dbc as DatabaseSocietyClass;
                        databaseKindClass = dbc as DatabaseKindClass;
                        databaseExtensionClass = dbc as DatabaseExtensionClass;
                        databaseEntityClass = dbc as DatabaseEntityClass;
                        ValidateDatabaseClass(dbc);

                        if (databaseSocietyClass != null) {
                            ValidateDatabaseSocietyClass(databaseSocietyClass);
                        }

                        if (databaseKindClass != null) {
                            ValidateDatabaseKindClass(databaseKindClass);
                        }

                        if (databaseEntityClass != null) {
                            ValidateEntityClass(databaseEntityClass);
                        }
                    }
                }

                // If there was some error, return at this point.
                if (Messenger.Current.ErrorCount > 0) {
                    return false;
                }
            }

            // Save the assembly to a file.

            if (!String.IsNullOrEmpty(SaveTo)) {
                _databaseAssembly.Serialize(SaveTo);
                if (Timestamp != DateTime.MinValue) {
                    File.SetLastWriteTime(SaveTo, Timestamp);
                }
            }

            return true;
        }

        #region Validate references to declarations decorated with [HideFromApplications]

        /// <summary>
        /// Validates that user code does not reference declarations decorated with
        /// the [HideFromApplications] custom attribute.
        /// </summary>
        private void ValidateForbiddenReferences() {
            String name;
            StringComparison strComp = StringComparison.InvariantCultureIgnoreCase;

            ScAnalysisTrace.Instance.WriteLine("Validating forbidden references.");

            // Validate assembly references.
            foreach (AssemblyRefDeclaration assemblyRef in _module.AssemblyRefs) {
                name = assemblyRef.Name;
                if (Array.Exists(_forbiddenAssemblies, s => String.Equals(name, s, strComp))) {
#pragma warning disable 618
                    ScMessageSource.Instance.Write(SeverityType.Error, "SCATV01",
                                                   new Object[] { name });
#pragma warning restore 618
                }
            }
        }

        #endregion

        #region Validate custom attribute usage

        /// <summary>
        /// Gets the name of the type reflection.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>String.</returns>
        public static String GetTypeReflectionName(IType type) {
            StringBuilder builder = new StringBuilder(100);
            type.WriteReflectionName(builder, ReflectionNameOptions.None);
            return builder.ToString();
        }

        /// <summary>
        /// Emit errors if Starcounter custom attributes have been used on unexpected
        /// declaration.
        /// </summary>
        /// <remarks>This method is called after the discovery, so we apply the rule: if the
        /// custom attribute was not discovered by the discovery process, it's because
        /// it is used improperly.</remarks>
        private void ValidateCustomAttributeUsage() {
            AnnotationRepositoryTask annotationRepositoryTask;
//            DatabaseAttribute databaseAttribute;
//            DatabaseClass databaseClass;
//            FieldDefDeclaration field;
            
            // Inspect that custom attributes were used were it makes sense.
#pragma warning disable 612
            annotationRepositoryTask = AnnotationRepositoryTask.GetTask(Project);
#pragma warning restore 612
        }

        #endregion

        #region Validate database classes and database attributes

        /// <summary>
        /// Validates a society class (which may be also a kind class).
        /// </summary>
        /// <param name="databaseClass">The society class.</param>
        private static void ValidateDatabaseSocietyClass(DatabaseSocietyClass databaseClass) {
            DatabaseSocietyClass parent;

            if (!(databaseClass is DatabaseKindClass)) {
                if (databaseClass.Name != _rootSocietyObjectTypeName &&
                    databaseClass.KindClass != null) {
                    parent = (DatabaseSocietyClass)databaseClass.BaseClass;
                    if (parent != null
                            && databaseClass.KindClass.BaseClass != parent.InheritedKindClass) {
#pragma warning disable 618
                        ScMessageSource.Instance.Write(SeverityType.Error,
                                                       "SCKCV04",
                                                       new object[] {
														databaseClass.Name,
														parent.InheritedKindClass.Name
												   }
                                                    );
#pragma warning restore 618
                    }
                }

                // If a class is nested in a society object is named Kind, it should be a kind class.
                foreach (TypeDefDeclaration nestedType in databaseClass.GetTypeDefinition().Types) {
                    if (nestedType.Name == "Kind"
                         && (databaseClass.KindClass == null
                                || nestedType != databaseClass.KindClass.GetTypeDefinition())
                       ) {
#pragma warning disable 618
                        ScMessageSource.Instance.Write(SeverityType.Error,
                                                       "SCKCV06",
                                                       new object[] { nestedType.Name });
#pragma warning restore 618
                    }
                }
            }
        }

        /// <summary>
        /// Validates a kind class.
        /// </summary>
        /// <param name="databaseClass">The kind class.</param>
        private void ValidateDatabaseKindClass(DatabaseKindClass databaseClass) {
            BindingOptions bindOpts = BindingOptions.OnlyExisting
                                        | BindingOptions.DontThrowException;
            TypeDefDeclaration typeDef = databaseClass.GetTypeDefinition();

            // A kind class must be named �Kind�.
            if (!databaseClass.Name.EndsWith("+Kind")) {
#pragma warning disable 618
                ScMessageSource.Instance.Write(SeverityType.Error,
                                               "SCKCV02",
                                               new Object[] { databaseClass.Name });
#pragma warning restore 618
            }

            // A kind class must have a default constructor (unless it is abstract).
            if (!typeDef.IsAbstract
                    && typeDef.Methods.GetMethod(".ctor",
                                                 _defaultConstructorSignature,
                                                 bindOpts) == null) {
#pragma warning disable 618
                ScMessageSource.Instance.Write(SeverityType.Error,
                                               "SCKCV05",
                                               new Object[] { databaseClass.Name });
#pragma warning restore 618
            }

            // A kind class cannot be sealed.
            if (typeDef.IsSealed) {
#pragma warning disable 618
                ScMessageSource.Instance.Write(SeverityType.Error,
                                               "SCKCV08",
                                               new Object[] { databaseClass.Name });
#pragma warning restore 618
            }

            // A kind class should have at least protected visibility.
            TypeAttributes visibility = typeDef.Attributes & TypeAttributes.VisibilityMask;
            if (visibility != TypeAttributes.NestedFamily
                  && visibility != TypeAttributes.NestedFamORAssem
                  && visibility != TypeAttributes.NestedPublic) {
#pragma warning disable 618
                ScMessageSource.Instance.Write(SeverityType.Error,
                                               "SCKCV09",
                                               new Object[] { databaseClass.Name });
#pragma warning restore 618
            }
        }

        /// <summary>
        /// Validates an extension class.
        /// </summary>
        /// <param name="databaseClass">An extension class.</param>
        private static void ValidateDatabaseExtensionClass(DatabaseExtensionClass databaseClass) {
            Boolean forbiddenConstructorErrorEmitted = false;
            MethodDefDeclaration defaultConstructor = null;
            TypeDefDeclaration typeDef = databaseClass.GetTypeDefinition();

            // An extension class should not contain any other constructor 
            // than the default (parameterless one).
            foreach (MethodDefDeclaration constructorDef in typeDef.Methods.GetByName(".ctor")) {
                if (constructorDef.Parameters.Count == 0) {
                    defaultConstructor = constructorDef;
                } else if (!forbiddenConstructorErrorEmitted) {
#pragma warning disable 618
                    ScMessageSource.Instance.Write(SeverityType.Error,
                                                   "SCECV01",
                                                   new Object[] { databaseClass.Name });
#pragma warning restore 618
                    forbiddenConstructorErrorEmitted = true;
                }
            }

            if (defaultConstructor == null) {
                if (!forbiddenConstructorErrorEmitted) {
#pragma warning disable 618
                    ScMessageSource.Instance.Write(SeverityType.Error,
                                                   "SCECV01",
                                                   new Object[] { databaseClass.Name });
#pragma warning restore 618
                }
            }

            // An extension class should be sealed.
            if (!typeDef.IsSealed) {
#pragma warning disable 618
                ScMessageSource.Instance.Write(SeverityType.Error,
                                               "SCECV04",
                                               new Object[] { databaseClass.Name });
#pragma warning restore 618
            }
        }

        /// <summary>
        /// Validates a entity class (which may be also a society class).
        /// </summary>
        /// <param name="databaseClass">An entity class.</param>
        private static void ValidateEntityClass(DatabaseEntityClass databaseClass) {
        }

        /// <summary>
        /// Validates a database class (which may be an entity class or an extension class).
        /// </summary>
        /// <param name="databaseClass">The database class.</param>
        private void ValidateDatabaseClass(DatabaseClass databaseClass) {
            BindingOptions bindOpts = BindingOptions.DontThrowException
                                            | BindingOptions.OnlyExisting;
            String assFail;
            TypeDefDeclaration typeDef = databaseClass.GetTypeDefinition();

            if (typeDef == null) {
                assFail = "databaseClass.TypeDefintiion was not set for type {0}.";
                throw ErrorCode.ToException(Error.SCERRUNSPECIFIED, String.Format(assFail, databaseClass.Name));
            }

            // A database class cannot have generic parameters.
            if (typeDef.IsGenericDefinition) {
#pragma warning disable 618
                ScMessageSource.Instance.Write(SeverityType.Error,
                                               "SCDCV01",
                                               new Object[] { databaseClass.Name });
#pragma warning restore 618
            }

            // A database class cannot have a destructor.
            MethodDefDeclaration finalizeMethod =
                (MethodDefDeclaration)typeDef.Methods.GetMethod("Finalize",
                                                                _defaultConstructorSignature,
                                                                bindOpts);
            if (finalizeMethod != null) {
#pragma warning disable 618
                ScMessageSource.Instance.Write(SeverityType.Error,
                                               "SCDCV02",
                                               new Object[] { databaseClass.Name });
#pragma warning restore 618
            }
        }

        #endregion

        #region Helper methods

        /// <summary>
        /// Determines whether a <see cref="Type" /> inherits a parent type, given
        /// the name of the parent type.
        /// </summary>
        /// <param name="child">Child <see cref="Type" />.</param>
        /// <param name="parentName">Name of the parent type.</param>
        /// <returns><b>true</b> if <paramref name="child" /> derives from a type named <paramref name="parentName" />,
        /// otherwise <b>false</b>.</returns>
        private static bool Inherits(IType child, string parentName) {
            TypeDefDeclaration cursor = child.GetTypeDefinition();
            while (true) {
                if (cursor.GetReflectionName() == parentName) {
                    return true;
                }
                if (cursor.BaseType != null) {
                    cursor = cursor.BaseType.GetTypeDefinition();
                } else {
                    break;
                }
            }
            return false;
        }

        /// <summary>
        /// Determines whether a <see cref="Type" /> is a society object / society class.
        /// </summary>
        /// <param name="type">A <see cref="Type" />.</param>
        /// <returns><b>true</b> if <paramref name="type" /> is a society object, otherwise <b>false</b>.</returns>
        private static bool IsSocietyObject(IType type) {
            return Inherits(type, _rootSocietyObjectTypeName);
        }

        /// <summary>
        /// Determines whether a <see cref="Type" /> is a kind class.
        /// </summary>
        /// <param name="type">A <see cref="Type" />.</param>
        /// <returns><b>true</b> if <paramref name="type" /> is a kind class, otherwise <b>false</b>.</returns>
        private static bool IsSocietyObjectKind(IType type) {
            return Inherits(type, _rootSocietyObjectTypeName + "+Kind");
        }

        #endregion

        #region Parse attribute types

        /// <summary>
        /// Gets the <see cref="DatabasePrimitive" /> corresponding to a reflection <paramref name="type" />.
        /// </summary>
        /// <param name="type">A reflection <see cref="Type" />.</param>
        /// <returns>A <see cref="DatabasePrimitive" />, or <see cref="DatabasePrimitive.None" /> if
        /// <paramref name="type" /> is not a database primitive.</returns>
        private static DatabasePrimitive GetPrimitive(ITypeSignature type) {
            IntrinsicTypeSignature intrinsicType = type as IntrinsicTypeSignature;
            if (intrinsicType != null) {
                switch (intrinsicType.IntrinsicType) {
                    case IntrinsicType.Boolean:
                        return DatabasePrimitive.Boolean;
                    case IntrinsicType.Byte:
                        return DatabasePrimitive.Byte;
                    case IntrinsicType.SByte:
                        return DatabasePrimitive.SByte;
                    case IntrinsicType.Int16:
                        return DatabasePrimitive.Int16;
                    case IntrinsicType.UInt16:
                        return DatabasePrimitive.UInt16;
                    case IntrinsicType.Int32:
                        return DatabasePrimitive.Int32;
                    case IntrinsicType.UInt32:
                        return DatabasePrimitive.UInt32;
                    case IntrinsicType.Int64:
                        return DatabasePrimitive.Int64;
                    case IntrinsicType.UInt64:
                        return DatabasePrimitive.UInt64;
                    case IntrinsicType.Single:
                        return DatabasePrimitive.Single;
                    case IntrinsicType.Double:
                        return DatabasePrimitive.Double;
                    case IntrinsicType.String:
                        return DatabasePrimitive.String;
                    default:
                        return DatabasePrimitive.None;
                }
            }
            INamedType namedType = type as INamedType;
            if (namedType != null) {
                switch (namedType.Name) {
                    case "System.Decimal":
                        return DatabasePrimitive.Decimal;
                    case "System.DateTime":
                        return DatabasePrimitive.DateTime;
                    case "System.TimeSpan":
                        return DatabasePrimitive.TimeSpan;
                    case "Starcounter.Binary":
                        return DatabasePrimitive.Binary;
                    case "Starcounter.LargeBinary":
                        return DatabasePrimitive.LargeBinary;
                    default:
                        return DatabasePrimitive.None;
                }
            }
            return DatabasePrimitive.None;
        }

        /// <summary>
        /// Gets the <see cref="IDatabaseAttributeType" /> corresponding to a reflection <see cref="Type" />,
        /// when the <see cref="Type" /> is 'simple': <see cref="DatabasePrimitiveType" />,
        /// <see cref="DatabaseEnumType" /> or <see cref="DiscoverDatabaseClass" />.
        /// </summary>
        /// <param name="type">The <see cref="Type" /> to parse.</param>
        /// <returns>IDatabaseAttributeType.</returns>
        /// <remarks>Enumerable and arrays are considered as composed types, since they take a simple type as
        /// argument.</remarks>
        private IDatabaseAttributeType GetSimpleType(ITypeSignature type) {
            // Test enumerations.
            if (type.BelongsToClassification(TypeClassifications.Enum)) {
                INamedType namedType = (INamedType)type.GetNakedType(TypeNakingOptions.None);
                return
                    DatabaseEnumType.GetInstance(namedType.Name,
                                                 GetPrimitive(EnumHelper.GetUnderlyingType(namedType)));
            }
            // Test primitives.
            DatabasePrimitive primitive = GetPrimitive(type);
            if (primitive != DatabasePrimitive.None) {
                return DatabasePrimitiveType.GetInstance(primitive);
            } else {
                // Not a primitive. Last chance: it's a database class.
                return DiscoverDatabaseClass(type);
            }
        }

        /// <summary>
        /// Set the type of a <see cref="DatabaseAttribute" /> given reflection information
        /// (<see cref="FieldInfo" /> or <see cref="PropertyInfo" />).
        /// </summary>
        /// <param name="member">Member (<see cref="FieldInfo" /> or <see cref="PropertyInfo" />)
        /// from which type information has to be read.</param>
        /// <param name="requireSupportedType"><b>true</b> if an error should be emitted
        /// if the type is not supported by the database kernel, otherwise <b>false</b>.</param>
        /// <param name="databaseAttribute">Database attribute whose type should be
        /// updated.</param>
        private void SetDatabaseAttributeType(NamedMetadataDeclaration member,
                                              bool requireSupportedType,
                                              DatabaseAttribute databaseAttribute) {
            databaseAttribute.AttributeType = null;
            // Get the field or property type.
            ITypeSignature type;
            FieldDefDeclaration field = member as FieldDefDeclaration;
            if (field != null) {
                type = field.FieldType;
            } else {
                type = ((PropertyDeclaration)member).PropertyType;
            }
            type = type.GetNakedType(TypeNakingOptions.None);
            ScAnalysisTrace.Instance.WriteLine("SetDatabaseAttributeType: getting attribute type {0} (persistent={1})",
                                               type, requireSupportedType);
            ArrayTypeSignature arrayTypeSignature;

            if (type.IsGenericInstance) {
                GenericTypeInstanceTypeSignature genericTypeInstance =
                    (GenericTypeInstanceTypeSignature)type.GetNakedType(TypeNakingOptions.None);
                INamedType genericTypeDef = genericTypeInstance.GenericDefinition;

                if (
                    genericTypeDef.Equals(this._module.Cache.GetType(typeof(Nullable<>),
                                                                    BindingOptions.RequireGenericDefinition))) {
                    // We have a nullable.
                    ITypeSignature nullableType = genericTypeInstance.GenericArguments[0];
                    IDatabaseAttributeType nullableDatabaseAttributeType = GetSimpleType(nullableType);
                    if (nullableDatabaseAttributeType == null) {
                        // Unsupported field.
                        databaseAttribute.AttributeType = null;
                        databaseAttribute.IsNullable = false;
                    } else {
                        if (databaseAttribute.AttributeType == null) {
                            databaseAttribute.AttributeType = nullableDatabaseAttributeType;
                        }
                        databaseAttribute.IsNullable = true;
                    }
                } else {
                    // Unsupported field.
                    databaseAttribute.IsNullable = false;
                }
            } else if ((arrayTypeSignature = type as ArrayTypeSignature) != null && arrayTypeSignature.Rank == 1) {
                // This is an array. The array type should be supported.
                ScAnalysisTrace.Instance.WriteLine(
                    "SetDatabaseAttributeType: {0}: array, looking for the element type.",
                    type);
                DatabaseClass elementType = DiscoverDatabaseClass(arrayTypeSignature.ElementType);
                if (elementType != null) {
                    databaseAttribute.AttributeType = new DatabaseArrayType(databaseAttribute, elementType);
                    databaseAttribute.IsNullable = true;
                }
            } else {
                // It should be a 'simple' type (primitive or database class).
                databaseAttribute.AttributeType = GetSimpleType(type);
            }
            // At this point, if the field type is unsupported, write an error.
            if (databaseAttribute.AttributeType == null) {
                if (requireSupportedType) {
                    ScMessageSource.WriteError(
                        MessageLocation.Of(databaseAttribute),
                        Error.SCERRUNSUPPORTEDATTRIBUTETYPE,
                        string.Format("Attribute: {0}.{1} ({2})", member.Parent, member.Name, type.GetReflectionName())
                        );
                }
                databaseAttribute.AttributeType = new DatabaseUnsupportedType(type.ToString());
            }
            ScAnalysisTrace.Instance.WriteLine("SetDatabaseAttributeType: {0} -> {1} (nullable={2})",
                                               type, databaseAttribute.AttributeType, databaseAttribute.IsNullable);
        }

        #endregion

        #region Process database classes

        /// <summary>
        /// Inspects a <see cref="Type" />, detects whether it is a database class, and if yet
        /// build the proper schema object (<see cref="DatabaseClass" />) and insert it in the schema.
        /// </summary>
        /// <param name="type">Type to be discovered.</param>
        /// <returns>The <see cref="DatabaseClass" /> corresponding to <paramref name="type" />,
        /// otherwise <b>false</b>.</returns>
        /// <remarks>This method may be safely called many times with the same argument.</remarks>
        private DatabaseClass DiscoverDatabaseClass(ITypeSignature type) {
            // First of all look in the cache of the current module.
            DatabaseClass databaseClass;
            DatabaseKindClass databaseKindClass;
            TypeDefDeclaration extendedType = null;
            if (this._discoverDatabaseClassCache.TryGetValue(type, out databaseClass)) {
                ScAnalysisTrace.Instance.WriteLine("DiscoverDatabaseClass: {0} already processed.", type);
                return databaseClass;
            }
            // Get the type definition.
            TypeDefDeclaration typeDef = type.GetTypeDefinition(BindingOptions.DontThrowException);
            if (typeDef == null) {
                this._discoverDatabaseClassCache.Add(type, null);
                return null;
            }
            // Look for the type definition in the cache.
            if (typeDef != type) {
                if (this._discoverDatabaseClassCache.TryGetValue(typeDef, out databaseClass)) {
                    ScAnalysisTrace.Instance.WriteLine("DiscoverDatabaseClass: {0} already processed.", type);
                    return databaseClass;
                }
            }
            // If the type is defined in another module, return it.
            if (typeDef.Module != this._module) {
                databaseClass = DatabaseSchema.FindDatabaseClass(typeDef.GetReflectionName());
                this._discoverDatabaseClassCache.Add(typeDef, databaseClass);
                return databaseClass;
            }
            #region Check directives for this class
            // Not in the cache and not in an external assembly. We can continue.
            WeaverDirectives typeWeaverDirectives;
            List<object> knownTypeAttribute = new List<object>();
            typeDef.CustomAttributes.ConstructRuntimeObjects(
                (IType)this._module.Cache.GetType(typeof(KnownTypeAttribute)), knownTypeAttribute);
            if (knownTypeAttribute.Count > 0) {
                typeWeaverDirectives = ((KnownTypeAttribute)knownTypeAttribute[0]).WeaverDirectives;
            } else {
                typeWeaverDirectives = WeaverDirectives.None;
            }
            if ((typeWeaverDirectives & WeaverDirectives.ExcludeSchemaDiscovery) != 0) {
                ScAnalysisTrace.Instance.WriteLine(
                    "DiscoverDatabaseClass: {0} has the ExcludeSchemaDiscovery directive.", typeDef);
                return null;
            }
            #endregion
            #region Verify if its a database class of some kind
            if (!typeDef.IsAssignableTo(this._dbObjectType)) {
                //  ScAnalysisTrace.Instance.WriteLine("DiscoverDatabaseClass: {0} is not a DbObject.", type);
                // Emit an error if the type is nested in a society object and is named 'Kind'.
                if (typeDef.DeclaringType != null && typeDef.Name == "Kind" && IsSocietyObject(typeDef.DeclaringType)) {
#pragma warning disable 618
                    ScMessageSource.Instance.Write(SeverityType.Error,
                                                   "SCKCV06", new object[] { typeDef });
#pragma warning restore 618
                }
                return null;
            }
            #endregion
            ScAnalysisTrace.Instance.WriteLine("DiscoverDatabaseClass: processing {0}.", typeDef);
            #region Detect and instantiate the proper kind of database class.
            #region Kind Classes
            if (IsSocietyObjectKind(typeDef)) {
                ScAnalysisTrace.Instance.WriteLine("DiscoverDatabaseClass: {0} is a kind class.", typeDef);
                databaseKindClass = new DatabaseKindClass(this._databaseAssembly, typeDef.GetReflectionName());
                databaseClass = databaseKindClass;
            }
            #endregion
            #region Society Object
 else if (IsSocietyObject(typeDef)) {
                ScAnalysisTrace.Instance.WriteLine("DiscoverDatabaseClass: {0} is a society object.", typeDef);
                DatabaseSocietyClass databaseSocietyClass =
                    new DatabaseSocietyClass(this._databaseAssembly, typeDef.GetReflectionName());
                databaseClass = databaseSocietyClass;
            }
            #endregion
            #region Entity
 else if (typeDef.IsAssignableTo(this._entityType)) {
                ScAnalysisTrace.Instance.WriteLine("DiscoverDatabaseClass: {0} is a regular entity object.",
                                                   typeDef);
                databaseClass = new DatabaseEntityClass(this._databaseAssembly, typeDef.GetReflectionName());
            }
            #endregion

            #endregion
            // Do things that are common to all database classes.
            databaseClass.WeaverDirectives = (int)typeWeaverDirectives;
            databaseClass.SetTypeDefinition(typeDef);
            // Check we don't have a class with the same name.
            DatabaseClass existingDatabaseClass =
                this._databaseAssembly.Schema.FindDatabaseClass(typeDef.GetReflectionName());

            if (existingDatabaseClass != null) {
                StringBuilder existingClassName = new StringBuilder();
                existingDatabaseClass.GetTypeDefinition().WriteReflectionName(existingClassName,
                        ReflectionNameOptions.UseAssemblyName);
                StringBuilder newClassName = new StringBuilder();
                typeDef.WriteReflectionName(newClassName, ReflectionNameOptions.UseAssemblyName);
#pragma warning disable 618
                ScMessageSource.Instance.Write(SeverityType.Error,
                                               "SCDCV07",
                                               new object[] { typeDef.GetReflectionName(), newClassName, existingClassName }
                                              );
#pragma warning restore 618
                return null;
            }

            this._databaseAssembly.DatabaseClasses.Add(databaseClass);
            this._discoverDatabaseClassCache.Add(typeDef, databaseClass);

            // Not until here its safe to invoke other discoveries, since these
            // can potentially get back to a discovery of the class being processed
            // here and the entry guard check depends on the class being cached,
            // else it will fail when the recursive calls are unwinded again.
            //   Now, lets do some further lookups.

            databaseKindClass = databaseClass as DatabaseKindClass;
            if (databaseKindClass != null) {
                // If the current class is a kind class, relate it to its corresponding society object.
                DatabaseSocietyClass parentClass =
                typeDef.DeclaringType == null
                ? null
                : DiscoverDatabaseClass(typeDef.DeclaringType)
                as DatabaseSocietyClass;
                if (parentClass == null) {
                    // The parent class is not a society object.
                    // This is an error.

#pragma warning disable 618
                    ScMessageSource.Instance.Write(SeverityType.Error, "SCKCV03", new object[] { typeDef });
#pragma warning restore 618
                    return null;
                } else {
                    parentClass.KindClass = databaseClass as DatabaseKindClass;
                    databaseKindClass.KindOf = parentClass;
                }
            } else if (databaseClass is DatabaseExtensionClass) {
                DatabaseExtensionClass databaseExtensionClass = databaseClass as DatabaseExtensionClass;
                // Relate the extension to its extented type.
                DatabaseEntityClass extendedEntityClass =
                (DatabaseEntityClass)DiscoverDatabaseClass(extendedType);
                databaseExtensionClass.Extends = extendedEntityClass;
            } else if (databaseClass is DatabaseEntityClass) {
                // Check that all entity classes are public. If they are not,
                // refuse them.
                //
                // This is just a prototype. Add a proper error message for this
                // case and document it.
                //
                // TODO:

                if (!typeDef.IsPublic()) {
                    ScMessageSource.WriteError(
                        MessageLocation.Of(typeDef),
                        Error.SCERRENTITYCLASSNOTPUBLIC,
                        string.Format("Class: {0}", typeDef)
                    );
                    return null;
                }
            }

            // Make sure the base is discovered allready.
            databaseClass.BaseClass = DiscoverDatabaseClass(typeDef.BaseType);

            // If it is a regular type, process the fields.
            // This has to be done when the type itself has been processed.

            foreach (FieldDefDeclaration field in typeDef.Fields) {
                if (this.TransformationKind == WeaverTransformationKind.IPCToDatabase) {
                    // We identifiy persistent fields by studying attribute index fields
                    // emitted earlier by the IPC-weaver.

                    PropertyDeclaration preGeneratedProperty;
                    string fieldName;

                    if (!field.IsStatic) continue;

                    if (WeaverNamingConventions.TryGetNakedAttributeName(field.Name, out fieldName)) {
                        // Locate the generated property and create our attribute from that.

                        preGeneratedProperty = typeDef.Properties.GetOneByName(fieldName);
                        DiscoverIPCWeavedProperty(databaseClass, preGeneratedProperty);
                    }
                } else if (!field.IsStatic) {
                    DiscoverDatabaseField(databaseClass, field);
                }
            }

            // Process the properties if the source is undecorated user code,
            // independent of the target (lucent access or database)

            if (this.TransformationKind != WeaverTransformationKind.IPCToDatabase) {
                foreach (PropertyDeclaration property in typeDef.Properties) {
                    if (property.Getter != null && !property.Getter.IsStatic) {
                        DiscoverDatabaseProperty(databaseClass, property);
                    }
                }
            }

            return databaseClass;
        }

        #endregion

        #region Process database attribute

        /// <summary>
        /// Inspects a reflection <see cref="FieldInfo" /> and builds the corresponding schema object
        /// (<see cref="DatabaseAttribute" />), and inserts it in the schema.
        /// </summary>
        /// <param name="databaseClass">The <see cref="DatabaseClass" /> to which the field belong.</param>
        /// <param name="field">Field to be inspected.</param>
        private void DiscoverDatabaseField(DatabaseClass databaseClass, FieldDefDeclaration field) {
            ScAnalysisTrace.Instance.WriteLine("DiscoverDatabaseField: processing field {0}.{1} of type {2}.",
            databaseClass.Name, field.Name, field.FieldType);
            // A field in a database class cannot be of the same name as a field in any parent class.
            if (databaseClass.BaseClass != null &&
            databaseClass.BaseClass.FindAttributeInAncestors(field.Name) != null) {
                // SCDCV06
#pragma warning disable 618
                ScMessageSource.Instance.Write(SeverityType.Error,
                "SCDCV06",
                new object[] { field.DeclaringType, field.Name });
#pragma warning restore 618
            }
            DatabaseAttribute databaseAttribute = new DatabaseAttribute(databaseClass, field.Name);
            databaseClass.Attributes.Add(databaseAttribute);
            databaseAttribute.SetFieldDefinition(field);
            databaseAttribute.IsInitOnly = (field.Attributes & FieldAttributes.InitOnly) != 0;
            
            if (field.CustomAttributes.Contains(this._notPersistentAttributeType)) {
                databaseAttribute.AttributeKind = DatabaseAttributeKind.NonPersistentField;
            } else {
                databaseAttribute.AttributeKind = DatabaseAttributeKind.PersistentField;
            }
            if (!databaseAttribute.IsPersistent) {
                // When the field is not persistent, we don't care about its type.
                databaseAttribute.AttributeType = new DatabaseUnsupportedType(field.FieldType.ToString());
            } else {
                // Check the attribute type.
                SetDatabaseAttributeType(field, databaseAttribute.IsPersistent, databaseAttribute);
            }
            databaseAttribute.IsPublicRead = field.IsPublic();
        }

        /// <summary>
        /// Inspects a reflection <see cref="PropertyDeclaration" /> originating from the IPC weaver
        /// and hence representing a persistent field that was removed. From this, builds the corresponding
        /// schema object (<see cref="DatabaseAttribute" />), and inserts it in the schema.
        /// </summary>
        /// <param name="databaseClass">The <see cref="DatabaseClass" /> to which the property belong.</param>
        /// <param name="property">The property.</param>
        private void DiscoverIPCWeavedProperty(DatabaseClass databaseClass, PropertyDeclaration property) {
            DatabaseAttribute databaseAttribute;

            ScAnalysisTrace.Instance.WriteLine(
                "DiscoverIPCWeavedProperty: processing property {0}.{1} of type {2}.",
                databaseClass.Name, property.Name, property.PropertyType
                );

            // The field was already discovered in the IPC weaving phase, and hence we
            // can rule out some things needed to be checked/verified in that phase.

            databaseAttribute = new DatabaseAttribute(databaseClass, property.Name);
            databaseClass.Attributes.Add(databaseAttribute);
            databaseAttribute.SetPropertyDefinition(property);
            databaseAttribute.AttributeKind = DatabaseAttributeKind.PersistentField;

            SetDatabaseAttributeType(property, true, databaseAttribute);

            databaseAttribute.IsPublicRead = property.Getter != null ? property.Getter.IsPublic() : false;
        }

        //private void ProcessRelatesToAttributes()
        //{
        //    foreach (KeyValuePair<DatabaseAttribute, RelatesToAttribute> pair in relatesToAttributes)
        //    {
        //        DatabaseAttribute databaseAttribute = pair.Key;
        //        RelatesToAttribute relatesToAttribute = pair.Value;
        //        ScAnalysisTrace.Instance.WriteLine("ProcessRelatesToAttributes: processing [RelatesTo] for {0}.{1}.",
        //        databaseAttribute.DeclaringClass.Name, databaseAttribute.Name);
        //        DatabaseEnumerableType databaseEnumerableType = (DatabaseEnumerableType) databaseAttribute.AttributeType;
        //        databaseEnumerableType.RelaxTypeCheck = relatesToAttribute.RelaxTypeCheck;
        //        DatabaseAttribute parentReferenceAttribute =
        //        databaseEnumerableType.ItemType.FindAttributeInAncestors(relatesToAttribute.FieldName);
        //        if (parentReferenceAttribute == null)
        //        {
        //            // The field {0}.{1} is marked to relate to the field {2},
        //            // but this field does not exist in the type {3}.
        //            ScMessageSource.Instance.Write(SeverityType.Error, "SCPFV15",
        //            new object[]
        //            {
        //                databaseAttribute.DeclaringClass.Name,
        //                databaseAttribute.Name,
        //                relatesToAttribute.FieldName,
        //                databaseEnumerableType.ItemType.Name
        //            });
        //        }
        //        else
        //        {
        //            // All seems correct.
        //            databaseAttribute.EnumerableRelatesTo = parentReferenceAttribute;
        //        }
        //    }
        //}

        /// <summary>
        /// Inspects a reflection <see cref="PropertyInfo" /> and builds the corresponding schema object
        /// (<see cref="DatabaseAttribute" />), and inserts it in the schema.
        /// </summary>
        /// <param name="databaseClass">The <see cref="DatabaseClass" /> to which the property belong.</param>
        /// <param name="property">Property to be inspected.</param>
        private void DiscoverDatabaseProperty(DatabaseClass databaseClass, PropertyDeclaration property)
            // ReSharper restore SuggestBaseTypeForParameter
        {
            DatabaseAttribute databaseAttribute = new DatabaseAttribute(databaseClass, property.Name);
            databaseClass.Attributes.Add(databaseAttribute);
            databaseAttribute.SetPropertyDefinition(property);
            databaseAttribute.AttributeKind = DatabaseAttributeKind.NotPersistentProperty;
            databaseAttribute.BackingField = ScanPropertyGetter(databaseAttribute);
            SetDatabaseAttributeType(property, false, databaseAttribute);
            databaseAttribute.IsPublicRead = property.Getter != null ? property.Getter.IsPublic() : false;
        }

        /// <summary>
        /// Reads the real instruction.
        /// </summary>
        /// <param name="reader">The reader.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        private static bool ReadRealInstruction(InstructionReader reader) {
            while (reader.ReadInstruction()) {
                if (reader.CurrentInstruction.OpCodeNumber != OpCodeNumber.Nop) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Scans the property getter.
        /// </summary>
        /// <param name="databaseAttribute">The database attribute.</param>
        /// <returns>DatabaseAttribute.</returns>
        private static DatabaseAttribute ScanPropertyGetter(DatabaseAttribute databaseAttribute) {
            MethodDefDeclaration methodDef = databaseAttribute.GetPropertyDefinition().Getter;
            if (!methodDef.MayHaveBody) {
                return null;
            }
            InstructionReader reader = methodDef.MethodBody.CreateOriginalInstructionReader();
            if (!ReadRealInstruction(reader)) {
                return null;
            }
            if (reader.CurrentInstruction.OpCodeNumber != OpCodeNumber.Ldarg_0 &&
                !(reader.CurrentInstruction.OpCodeNumber == OpCodeNumber.Ldarg &&
                  reader.CurrentInstruction.Int16Operand == 0)) {
                return null;
            }
            if (!ReadRealInstruction(reader)) {
                return null;
            }
            if (reader.CurrentInstruction.OpCodeNumber != OpCodeNumber.Ldfld) {
                return null;
            }
            IField field = reader.CurrentInstruction.FieldOperand;
            DatabaseAttribute backingAttribute = databaseAttribute.DeclaringClass.FindAttributeInAncestors(field.Name);
            if (backingAttribute == null) {
                return null;
            }
            if (!ReadRealInstruction(reader)) {
                return null;
            }
            if (reader.CurrentInstruction.OpCodeNumber == OpCodeNumber.Ret) {
                return backingAttribute;
            }
            if (reader.CurrentInstruction.OpCodeNumber != OpCodeNumber.Stloc_0) {
                return null;
            }
            if (!ReadRealInstruction(reader)) {
                return null;
            }
            if (reader.CurrentInstruction.OpCodeNumber == OpCodeNumber.Br_S) {
                if (!ReadRealInstruction(reader)) {
                    return null;
                }
            }
            if (reader.CurrentInstruction.OpCodeNumber != OpCodeNumber.Ldloc_0) {
                return null;
            }
            if (!ReadRealInstruction(reader)) {
                return null;
            }
            if (reader.CurrentInstruction.OpCodeNumber != OpCodeNumber.Ret) {
                return null;
            }
            return backingAttribute;
        }

        #endregion

        #region Process constructors

        /// <summary>
        /// Inspects all constructors of a database class, checks validation rules and discovers
        /// initial values.
        /// </summary>
        /// <param name="databaseClass"><see cref="DatabaseClass" /> whose constructors have
        /// to be inspected.</param>
        /// <param name="typeDef"><see cref="TypeDefDeclaration" /> corresponding to
        /// <paramref name="databaseClass" />.</param>
        private void InspectConstructors(DatabaseClass databaseClass, TypeDefDeclaration typeDef) {
            // Process constructors.
            foreach (MethodDefDeclaration methodDef in typeDef.Methods.GetByName(".ctor")) {
                this.InspectConstructor(methodDef, databaseClass);
            }
        }

        /// <summary>
        /// Determines whether [is initialization block] [the specified block].
        /// </summary>
        /// <param name="block">The block.</param>
        /// <returns><c>true</c> if [is initialization block] [the specified block]; otherwise, <c>false</c>.</returns>
        public static bool IsInitializationBlock(InstructionBlock block) {
            return _initializationBlocks.Contains(block);
        }

        /// <summary>
        /// Inspects the constructor.
        /// </summary>
        /// <param name="methodDef">The method def.</param>
        /// <param name="databaseClass">The database class.</param>
        private void InspectConstructor(MethodDefDeclaration methodDef, DatabaseClass databaseClass) {
            ScAnalysisTrace.Instance.WriteLine("Inspecting the constructor {{{0}}}.", methodDef);
            // Analyze the constructor. Separate the "this uninitialized" zone.
            MethodBodyRestructurer methodBodyRestructurer = new MethodBodyRestructurer(methodDef,
                    MethodBodyRestructurerOptions.
                    None, this._weavingHelper);
            methodBodyRestructurer.Restructure(_writer);
            // Remember that this is the initialization block.
            _initializationBlocks.AddIfAbsent(methodBodyRestructurer.InitializationBlock);
            InstructionSequence firstSequence =
                methodBodyRestructurer.InitializationBlock.FindFirstInstructionSequence();
            // We do not support too complex constructors.
            /*
            if (methodBodyRestructurer.InitializationBlock.HasChildrenBlocks ||
                methodBodyRestructurer.InitializationBlock.HasExceptionHandlers ||
                methodBodyRestructurer.InitializationBlock.FirstInstructionSequence.NextSiblingSequence != null)
            {
                ScMessageSource.Instance.Write(SeverityType.Error, "SCDCV04",
                                               new object[] { methodDef.DeclaringType.Name });
                return;
            }
             */
            // Read instructions sequentially and build a simple stack of instructions.
            // ReSharper disable ConvertToConstant
            object thisPointerStackContent = "this";
            object tooComplexStackContent = "too complex";
            // ReSharper restore ConvertToConstant
            Stack<object> stack = new Stack<object>(methodDef.MethodBody.MaxStack);
            InstructionReader reader = methodDef.MethodBody.CreateInstructionReader(false);
            reader.EnterInstructionSequence(firstSequence);
            while (reader.ReadInstruction()) {
                switch (reader.CurrentInstruction.OpCodeNumber) {

                    case OpCodeNumber.Stfld:
                        // We are storing a field. Is it one of OUR fields? {
                        object[] stackContent = stack.ToArray();
                        if (stackContent[1] == thisPointerStackContent && stackContent[0] != null) {
                            IField field = reader.CurrentInstruction.FieldOperand;
                            // Now determines if it is a persistent field.
                            DatabaseClass declaringClass =
                                DatabaseSchema.FindDatabaseClass(
                                    ((INamedType)field.DeclaringType).Name);
                            if (declaringClass != null) {
                                DatabaseAttribute databaseAttribute = declaringClass.Attributes[field.Name];
                                if (databaseAttribute.IsPersistent) {
                                    // This is a persistent field on the current instance.
                                    // Determine if we can guess the initial value, or
                                    // write an error.
                                    if (stackContent[0] != thisPointerStackContent &&
                                        stackContent[0] != tooComplexStackContent) {
                                        ScAnalysisTrace.Instance.WriteLine(
                                            "Found that the field {{{0}}} is initialized to {{{1}}}.",
                                            databaseAttribute, stackContent[0]);
                                        Type fieldSystemType = field.FieldType.GetSystemType(null, null);
                                        if (fieldSystemType.IsEnum) {
                                            databaseAttribute.InitialValue =
                                                Enum.ToObject(fieldSystemType, stackContent[0]);
                                        } else if (fieldSystemType.IsGenericType &&
                                                   fieldSystemType.GetGenericTypeDefinition().FullName ==
                                                   "System.Nullable`1") {
                                            databaseAttribute.InitialValue =
                                                Convert.ChangeType(stackContent[0],
                                                                   fieldSystemType.GetGenericArguments()[0]);
                                        } else {
                                            databaseAttribute.InitialValue =
                                                Convert.ChangeType(stackContent[0], fieldSystemType);
                                        }
                                    } else {
                                        // The field {0}.{1} is initialized outside the constructor but has a complex value.
                                        // Only literal intrinsic values are allowed.
#pragma warning disable 618
                                        ScMessageSource.Instance.Write(SeverityType.Error,
                                                                       "SCPFV02", new object[]
                                    {
                                        databaseAttribute
                                        .
                                        DeclaringClass
                                        .Name,
                                        databaseAttribute
                                        .Name
                                    });
#pragma warning restore 618
                                    }
                                }
                            }
                        }
                        stack.Pop();
                        stack.Pop();
                        break;
                    case OpCodeNumber.Ldarg_0:
                        stack.Push(thisPointerStackContent);
                        break;
                    case OpCodeNumber.Ldnull:
                        stack.Push(null);
                        break;
                    //PI090903.
                    //case OpCodeNumber.Ldc_I4:
                    //case OpCodeNumber.Ldc_I4_S:
                    //    stack.Push(reader.CurrentInstruction.Int32Operand);
                    //    break;
                    case OpCodeNumber.Ldc_I4:
                        stack.Push(reader.CurrentInstruction.Int16Operand);
                        break;
                    case OpCodeNumber.Ldc_I4_S:
                        stack.Push(reader.CurrentInstruction.ByteOperand);
                        break;
                    //
                    case OpCodeNumber.Ldc_I4_0:
                        stack.Push(0);
                        break;
                    case OpCodeNumber.Ldc_I4_1:
                        stack.Push(1);
                        break;
                    case OpCodeNumber.Ldc_I4_2:
                        stack.Push(2);
                        break;
                    case OpCodeNumber.Ldc_I4_3:
                        stack.Push(3);
                        break;
                    case OpCodeNumber.Ldc_I4_4:
                        stack.Push(4);
                        break;
                    case OpCodeNumber.Ldc_I4_5:
                        stack.Push(5);
                        break;
                    case OpCodeNumber.Ldc_I4_6:
                        stack.Push(6);
                        break;
                    case OpCodeNumber.Ldc_I4_7:
                        stack.Push(7);
                        break;
                    case OpCodeNumber.Ldc_I4_8:
                        stack.Push(8);
                        break;
                    case OpCodeNumber.Ldc_I4_M1:
                        stack.Push(-1);
                        break;
                    case OpCodeNumber.Ldc_I8:
                        stack.Push(reader.CurrentInstruction.Int64Operand);
                        break;
                    case OpCodeNumber.Ldc_R4:
                        stack.Push(reader.CurrentInstruction.SingleOperand);
                        break;
                    case OpCodeNumber.Ldc_R8:
                        stack.Push(reader.CurrentInstruction.DoubleOperand);
                        break;
                    case OpCodeNumber.Ldstr:
                        stack.Push(reader.CurrentInstruction.StringOperand.ToString());
                        break;
                    case OpCodeNumber.Conv_I1:
                    case OpCodeNumber.Conv_Ovf_I:
                        stack.Push(Convert.ToSByte(stack.Pop()));
                        break;
                    case OpCodeNumber.Conv_I2:
                    case OpCodeNumber.Conv_Ovf_I2:
                        stack.Push(Convert.ToInt16(stack.Pop()));
                        break;
                    case OpCodeNumber.Conv_I4:
                    case OpCodeNumber.Conv_Ovf_I4:
                        stack.Push(Convert.ToInt32(stack.Pop()));
                        break;
                    case OpCodeNumber.Conv_I8:
                    case OpCodeNumber.Conv_Ovf_I8:
                        stack.Push(Convert.ToInt64(stack.Pop()));
                        break;
                    case OpCodeNumber.Conv_U1:
                    case OpCodeNumber.Conv_Ovf_U1:
                        stack.Push(Convert.ToByte(stack.Pop()));
                        break;
                    case OpCodeNumber.Conv_U2:
                    case OpCodeNumber.Conv_Ovf_U2:
                        stack.Push(Convert.ToUInt16(stack.Pop()));
                        break;
                    case OpCodeNumber.Conv_U4:
                    case OpCodeNumber.Conv_Ovf_U4:
                        stack.Push(Convert.ToUInt32(stack.Pop()));
                        break;
                    case OpCodeNumber.Conv_U8:
                    case OpCodeNumber.Conv_Ovf_U8:
                        stack.Push(Convert.ToUInt64(stack.Pop()));
                        break;
                    case OpCodeNumber.Call:
                    case OpCodeNumber.Callvirt:
                    case OpCodeNumber.Newobj: {
                            bool isNewObj = reader.CurrentInstruction.OpCodeNumber == OpCodeNumber.Newobj;
                            IMethod method = reader.CurrentInstruction.MethodOperand;
                            if (isNewObj && method.Name == ".ctor" && method.DeclaringType.IsGenericInstance &&
                                method.DeclaringType.GetTypeDefinition().Name == "System.Nullable`1") {
                                // This is the construction of a nullable type. We support this (no stack transition for us).
                            } else {
                                int argNumber = method.ParameterCount;
                                // ReSharper disable BitwiseOperatorOnEnumWihtoutFlags
                                if (!isNewObj && (method.CallingConvention & CallingConvention.HasThis) != 0) {
                                    // ReSharper restore BitwiseOperatorOnEnumWihtoutFlags {
                                    argNumber++;
                                }
                                for (int i = 0; i < argNumber; i++) {
                                    stack.Pop();
                                }
                                if (isNewObj || !IntrinsicTypeSignature.Is(method.ReturnType, IntrinsicType.Void)) {
                                    stack.Push(tooComplexStackContent);
                                }
                            }
                        }
                        break;
                    default:
                        //PI090903.
                        //if (reader.CurrentInstruction.OpCodeNumber == OpCodeNumber.Ldarg &&
                        //    reader.CurrentInstruction.Int32Operand == 0)
                        if ((reader.CurrentInstruction.OpCodeNumber == OpCodeNumber.Ldarg &&
                             reader.CurrentInstruction.Int16Operand == 0) ||
                            (reader.CurrentInstruction.OpCodeNumber == OpCodeNumber.Ldarg_S &&
                             reader.CurrentInstruction.ByteOperand == 0))
                            // {
                            stack.Push(thisPointerStackContent);
                        break;
                }
#pragma warning disable 618
                switch (OpCodeMap.GetFlowControl(reader.CurrentInstruction.OpCodeNumber))
#pragma warning restore 618
                {
                    case FlowControl.Next:
                    case FlowControl.Call:
                        // We understand that.
                        break;
                    default:
#pragma warning disable 618
                        // This is too complex for our analysis.
                        ScMessageSource.Instance.Write(SeverityType.Error, "SCDCV04",
                                                       new object[] { methodDef.DeclaringType.Name });
#pragma warning restore 618
                        return;
                }
#pragma warning disable 618
                StackBehaviour popBehaviour =
                    OpCodeMap.GetStackBehaviourPop(reader.CurrentInstruction.OpCodeNumber);
#pragma warning restore 618
#pragma warning disable 618
                StackBehaviour pushBehaviour =
                    OpCodeMap.GetStackBehaviourPush(reader.CurrentInstruction.OpCodeNumber);
#pragma warning restore 618
                switch (popBehaviour)
                {
                    case StackBehaviour.Pop0:
                        break;
                    case StackBehaviour.Pop1:
                    case StackBehaviour.Popi:
                    case StackBehaviour.Popref:
                        stack.Pop();
                        break;
                    case StackBehaviour.Pop1_pop1:
                    case StackBehaviour.Popi_popi:
                    case StackBehaviour.Popi_popr4:
                    case StackBehaviour.Popi_popr8:
                    case StackBehaviour.Popref_pop1:
                    case StackBehaviour.Popi_pop1:
                    case StackBehaviour.Popi_popi8:
                    case StackBehaviour.Popref_popi:
                        stack.Pop();
                        stack.Pop();
                        break;
                    case StackBehaviour.Popref_popi_pop1:
                    case StackBehaviour.Popi_popi_popi:
                    case StackBehaviour.Popref_popi_popi8:
                    case StackBehaviour.Popref_popi_popr4:
                    case StackBehaviour.Popref_popi_popr8:
                    case StackBehaviour.Popref_popi_popref:
                    case StackBehaviour.Popref_popi_popi:
                        stack.Pop();
                        stack.Pop();
                        stack.Pop();
                        break;
                    default:
                        throw ErrorCode.ToException(Error.SCERRUNSPECIFIED, string.Format("Unexpected StackBehaviour for pop: {0}.", popBehaviour));
                }
                switch (pushBehaviour) {
                    case StackBehaviour.Push0:
                        break;
                    case StackBehaviour.Push1:
                    case StackBehaviour.Pushi:
                    case StackBehaviour.Pushr4:
                    case StackBehaviour.Pushi8:
                    case StackBehaviour.Pushr8:
                    case StackBehaviour.Pushref:
                        stack.Push(tooComplexStackContent);
                        break;
                    case StackBehaviour.Push1_push1:
                        stack.Push(tooComplexStackContent);
                        stack.Push(tooComplexStackContent);
                        break;
                    default:
                        throw ErrorCode.ToException(Error.SCERRUNSPECIFIED, string.Format("Unexpected StackBehaviour for push: {0}.", popBehaviour));
                }
                break;
            }

            // If we have an extension class, we have to check that the constructor
            // is empty.
            if (databaseClass is DatabaseExtensionClass) {
                bool constructorEmpty = true;
                if (!methodBodyRestructurer.PrincipalBlock.HasExceptionHandlers) {
                    InstructionSequence sequence = methodBodyRestructurer.PrincipalBlock.FindFirstInstructionSequence();
                    reader.EnterInstructionSequence(sequence);
                    reader.ReadInstruction();
                    while (reader.ReadInstruction()) {
                        switch (reader.CurrentInstruction.OpCodeNumber) {
                            case OpCodeNumber.Nop:
                            case OpCodeNumber.Ret:
                                // These are acceptable instructons.
                                break;
                            default:
                                constructorEmpty = false;
                                break;
                        }
                        if (!constructorEmpty) {
                            break;
                        }
                    }
                } else {
                    constructorEmpty = false;
                }
                if (!constructorEmpty) {
#pragma warning disable 618
                    ScMessageSource.Instance.Write(SeverityType.Error,
                                                   "SCECV03", new object[] { databaseClass.Name });
#pragma warning restore 618
                }
            }
        }

        #endregion

        /// <summary>
        /// Inspects the load field address.
        /// </summary>
        private void InspectLoadFieldAddress() {
            IEnumerator<MetadataDeclaration> methodsEnumerator =
                this._module.GetDeclarationEnumerator(TokenType.MethodDef);
            while (methodsEnumerator.MoveNext()) {
                MethodDefDeclaration methodDef = (MethodDefDeclaration)methodsEnumerator.Current;
                if (!methodDef.HasBody) {
                    continue;
                }
                ScAnalysisTrace.Instance.WriteLine("Inspecting the method {{{0}}} for safe use of field references.",
                                                   methodDef);
                MultiDictionary<ITypeSignature, IField> loadedFieldAddresses =
                    new MultiDictionary<ITypeSignature, IField>(4, TypeComparer.GetInstance());
                Set<ITypeSignature> refArgumentTypes = new Set<ITypeSignature>(4, TypeComparer.GetInstance());
                InstructionReader reader = methodDef.MethodBody.CreateOriginalInstructionReader();
                while (reader.ReadInstruction()) {
                    switch (reader.CurrentInstruction.OpCodeNumber) {
                        case OpCodeNumber.Ldflda: {
                                IField field = reader.CurrentInstruction.FieldOperand;
                                if (field.DeclaringType.IsGenericInstance) {
                                    continue;
                                }
                                DatabaseAttribute databaseAttribute =
                                    _schema.FindDatabaseAttribute(field.Name, field.DeclaringType.GetReflectionName());
                                if (databaseAttribute != null &&
                                    databaseAttribute.AttributeKind == DatabaseAttributeKind.PersistentField) {
                                    ScAnalysisTrace.Instance.WriteLine(
                                        "This method loads the address of the field {{{0}}} of type {{{1}}}.", field,
                                        field.FieldType);
                                    loadedFieldAddresses.Add(field.FieldType, field);
                                }
                            }
                            break;
                        case OpCodeNumber.Call:
                        case OpCodeNumber.Callvirt: {
                                IMethod method = reader.CurrentInstruction.MethodOperand;
                                for (int i = 0; i < method.ParameterCount; i++) {
                                    ITypeSignature parameterType =
                                        method.GetParameterType(i);
                                    if (parameterType.BelongsToClassification(TypeClassifications.Pointer)) {
#pragma warning disable 618
                                        PointerTypeSignature pointerTypeSignature =
                                            (PointerTypeSignature)
                                            parameterType.GetNakedType(TypeNakingOptions.IgnoreAll);
#pragma warning restore 618
                                        ITypeSignature elementType = pointerTypeSignature.ElementType;
                                        ScAnalysisTrace.Instance.WriteLine(
                                            "This method passes the reference of a {{{0}}}.", elementType);
                                        refArgumentTypes.AddIfAbsent(elementType);
                                    }
                                }
                            }
                            break;
                    }
                }
                Set<string> suspectFields = new Set<string>();
                // Look if both sets intersect.
                foreach (ITypeSignature type in refArgumentTypes) {
                    if (loadedFieldAddresses.ContainsKey(type)) {
                        foreach (IField field in loadedFieldAddresses[type]) {
                            suspectFields.Add(field.ToString());
                        }
                    }
                }
                if (suspectFields.Count > 0) {
                    StringBuilder fields = new StringBuilder();
                    foreach (string s in suspectFields) {
                        if (fields.Length > 0) {
                            fields.Append(", ");
                        }
                        fields.Append(s);
                    }
#pragma warning disable 618
                    ScMessageSource.Instance.Write(SeverityType.Error, "SCPFV21",
                                                   new object[] { methodDef.ToString(), fields.ToString() });
#pragma warning restore 618
                }
            }
        }
    }
}