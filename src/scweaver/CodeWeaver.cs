
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Xml;
using PostSharp;
using PostSharp.Extensibility;
using PostSharp.Hosting;
using PostSharp.Sdk.CodeModel;
using PostSharp.Sdk.Extensibility;
using Sc.Server.Weaver;
using Starcounter;
using Starcounter.Internal;
using Starcounter.Internal.Weaver;
using System.Text.RegularExpressions;
using Error = Starcounter.Internal.Error;

namespace Weaver {

    /// <summary>
    /// Exposes the facade of the code weaver engine.
    /// </summary>
    [Serializable]
    internal class CodeWeaver : MarshalByRefObject, IPostSharpHost {
        const string AnalyzerProjectFileName = "ScAnalyzeOnly.psproj";
        const string WeaverProjectFileName = "ScTransform.psproj";
        const string BootstrapWeaverProjectFileName = "ScWeaveBootstrap.psproj";

        private readonly List<Regex> weaverExcludes = new List<Regex>();

        /// <summary>
        /// The name of the default output directory, utilized by the weaver
        /// when no output directory is explicitly given.
        /// </summary>
        /// <remarks>
        /// If no output directory is given, the output directory will be a
        /// subdirectory of the input directory, with this name.
        /// </remarks>
        public const string DefaultOutputDirectoryName = ".starcounter";

        /// <summary>
        /// The name of the default cache directory, if no cache directory
        /// is given.
        /// </summary>
        /// <remarks>
        /// If no cache directory is given, the cache directory will be a
        /// subdirectory of the output directory, with this name.
        /// </remarks>
        public const string DefaultCacheDirectoryName = "cache";

        /// <summary>
        /// The directory where the weaver looks for input.
        /// </summary>
        public readonly string InputDirectory;

        /// <summary>
        /// The cache directory used by the weaver.
        /// </summary>
        public readonly string CacheDirectory;

        /// <summary>
        /// The output directory, mirroring the binaries in the input directory
        /// with relevant binaries weaved.
        /// </summary>
        public string OutputDirectory;

        /// <summary>
        /// Gets or sets the assembly file the weaver should act upon. The
        /// file is expected to be found in the <see cref="InputDirectory"/>.
        /// </summary>
        public string AssemblyFile { get; set; }

        /// <summary>
        /// Gets or sets a value dictating if outdated assemblies should be
        /// weaved/transformed. Defaults to true. If this is set to false,
        /// only analysis will be performed.
        /// </summary>
        public bool RunWeaver { get; set; }

        public bool UseStateRedirect { get; set; }

        /// <summary>
        /// Gets or sets a value that adapts the code weaver to perform
        /// weaving only to the cache directory and never touch the input
        /// binaries.
        /// </summary>
        public bool WeaveToCacheOnly { get; set; }

        /// <summary>
        /// Gets or sets a value that instructs the weaver to invoke the
        /// functionality involved when doing weaving to support bootstrapping
        /// of executables.
        /// </summary>
        public bool WeaveBootstrapperCode { get; set; }

        /// <summary>
        /// Gets or sets a value indicating if the weaver cache should be
        /// disabled. If the cache is disabled, cached assemblies will not
        /// be considered and all input will always be analyzed and/or
        /// transformed on every run.
        /// </summary>
        public bool DisableWeaverCache { get; set; }

        /// <summary>
        /// Gets or sets the weaver runtime directory path. This path will be
        /// consulted when the weaver needs to locate neccessary runtime files,
        /// such as the PostSharp-related project- and plugin files.
        /// </summary>
        /// <remarks>
        /// The construtor will try to assign this to the Starcounter binary
        /// directory and, as a fallback, use the current directory if that
        /// fails. Tools can override this funcationality by explicitly setting
        /// it after the weaver component is created (and before executed).
        /// </remarks>
        public string WeaverRuntimeDirectory { get; set; }

        /// <summary>
        /// Temporary directory used by the transformation engine.
        /// </summary>
        private string TempDirectoryPath;

        /// <summary>
        /// Gets or sets the set of assemblies that we need to load to consider
        /// if they need to be analyzed/weaved or not.
        /// </summary>
        /// <remarks>
        /// <para>
        /// In general, this means all binaries in the input directory except
        /// those that have been excluded by rule (configuration) or those we
        /// have found to be up to date in the cache.
        /// </para>
        /// </remarks>
        internal Dictionary<string, ModuleLoadStrategy> Assemblies {
            get;
            set;
        }

        /// <summary>
        /// Gets the path to the analyser project file, once resolved. This
        /// file is used when we are running analysis only (.i.e no transformation).
        /// </summary>
        /// <remarks>
        /// The value is the full path to the file.
        /// </remarks>
        private string AnalyzerProjectFile;

        /// <summary>
        /// Gets the path to the weaver project file, once resolved. This
        /// file is use when we need to transform assemblies.
        /// </summary>
        /// <remarks>
        /// The value is the full path to the file.
        /// </remarks>
        private string WeaverProjectFile;

        /// <summary>
        /// Gets the path to the bootstrap weaver project file, once resolved.
        /// This file is used to weave executables and make them "bootable" from
        /// the OS shell.
        /// </summary>
        /// <remarks>
        /// The value is the full path to the file.
        /// </remarks>
        private string BootstrapWeaverProjectFile;

        /// <summary>
        /// Holds a reference to the weaver cache we'll use when the weaver
        /// executes.
        /// </summary>
        private WeaverCache Cache;

        public CodeWeaver(string directory, string file, string outputDirectory, string cacheDirectory) {
            this.InputDirectory = directory;
            this.OutputDirectory = outputDirectory;
            this.CacheDirectory = cacheDirectory;
            this.RunWeaver = true;
            this.WeaveBootstrapperCode = false;
            this.DisableWeaverCache = false;
            this.AssemblyFile = file;

            try {
                this.WeaverRuntimeDirectory = Path.GetDirectoryName(typeof(CodeWeaver).Assembly.Location);
            } catch {
                this.WeaverRuntimeDirectory = Environment.CurrentDirectory;
            }

            AddStandardWeaverExcludes();
            AddWeaverExcludesFromFile();
        }

        public bool Execute() {
            PostSharpObjectSettings postSharpSettings;

            // Execute setup

            if (Setup() == false)
                return false;

            // If we found that no assemblies needed to be weaved, we write
            // out some information about it and consider this method a success.

            if (this.Assemblies.Count == 0) {
                Program.WriteInformation("No assemblies needed to be weaved.");
                return CompleteMirroringAfterWeaver();
            }

            // Prepare the underlying weaver and the execution of PostSharp.

            Messenger.Current.Message += this.OnWeaverMessage;

            postSharpSettings = new PostSharpObjectSettings(Messenger.Current);
            postSharpSettings.CreatePrivateAppDomain = false;
            postSharpSettings.ProjectExecutionOrder = ProjectExecutionOrder.Phased;
            postSharpSettings.OverwriteAssemblyNames = false;
            postSharpSettings.DisableReflection = true;
            postSharpSettings.SearchDirectories.Add(this.InputDirectory);
            postSharpSettings.LocalHostImplementation = typeof(CodeWeaverInsidePostSharpDomain).AssemblyQualifiedName;

            // Move all assemblies in the cached weaver schema to that of the
            // weaver engine.

            foreach (var cachedAssembly in this.Cache.Schema.Assemblies) {
                ScAnalysisTask.DatabaseSchema.Assemblies.Add(cachedAssembly);
            }

            using (IPostSharpObject postSharpObject = PostSharpObject.CreateInstance(postSharpSettings, this)) {
                ((PostSharpObject)postSharpObject).Domain.AssemblyLocator.DefaultOptions |= PostSharp.Sdk.CodeModel.AssemblyLocatorOptions.ForClrLoading;

                Stopwatch stopwatch = Stopwatch.StartNew();
                try {
                    // Have PostSharp run the weaver engine for us, analysing and/or
                    // transforming each assembly.

                    var strategies = new ModuleLoadStrategy[this.Assemblies.Count];
                    this.Assemblies.Values.CopyTo(strategies, 0);
                    postSharpObject.ProcessAssemblies(strategies);
                } catch (MessageException) {
                    // The message handler will have reported the error already.
                    // Just return 1 indicating something went wrong.

                    return false;

                } catch (Exception e) {
                    ErrorMessage error;
                    if (!ErrorCode.TryGetCodedMessage(e, out error)) {
                        // We have no clue what the exception may be. Don't
                        // try anything fancy. Just let it explode.
                        throw;
                    }

                    // Any exception we catch that orignates from us (i.e.
                    // is based on a Starcounter error code and error message),
                    // we report as a program error.
                    Program.ReportProgramError(error.Code, error.ToString());
                    return false;
                }

                stopwatch.Stop();
                Program.WriteDebug("Time weaving: {0:00.00} s.", stopwatch.ElapsedMilliseconds / 1000d);
            }

            return CompleteMirroringAfterWeaver();
        }

        private bool CompleteMirroringAfterWeaver() {
            string currentBinary;
            string path1;
            bool fileNeedsMirroring;

            if (this.WeaveToCacheOnly)
                return true;

            var files = new List<string>();
            files.AddRange(Directory.GetFiles(this.InputDirectory, "*.dll"));
            files.AddRange(Directory.GetFiles(this.InputDirectory, "*.exe"));

            Program.WriteDebug("Assuring mirroring of {0} files.", files.Count);

            foreach (var file in files) {
                currentBinary = Path.GetFileName(file);
                path1 = Path.Combine(this.OutputDirectory, currentBinary);

                if (!File.Exists(path1)) {
                    Program.WriteInformation("Mirrored file {0} not found  - copying it.", currentBinary);
                    fileNeedsMirroring = true;
                } else if (File.GetLastWriteTime(path1) < File.GetLastWriteTime(file)) {
                    Program.WriteInformation("Mirrored file {0} outdated - copying it.", currentBinary);
                    fileNeedsMirroring = true;
                } else {
                    Program.WriteDebug("Mirrored file {0} up-to-date.", currentBinary);
                    fileNeedsMirroring = false;
                }

                if (fileNeedsMirroring) {
                    // If there is no corresponding file in the output
                    // directory, or if the file in the output directory is
                    // older, we perform a copy of the binary and an eventual
                    // debug database file.
                    try {
                        MirrorBinary(currentBinary);
                    } catch (Exception e) {
                        Program.ReportProgramError(Error.SCERRUNSPECIFIED, "Failed mirroring file {0}; exception: {1}", currentBinary, e.Message);
                        return false;
                    }
                }
            }
            
            return true;
        }

        private void MirrorBinary(string filename) {
            string filenameWithoutExtension;
            string sourcePath;
            string targetPath;

            sourcePath = Path.Combine(this.InputDirectory, filename);
            targetPath = Path.Combine(this.OutputDirectory, filename);
            File.Copy(sourcePath, targetPath, true);

            filenameWithoutExtension = Path.GetFileNameWithoutExtension(filename);
            filename = string.Concat(filenameWithoutExtension, ".pdb");
            sourcePath = Path.Combine(this.InputDirectory, filename);
            if (File.Exists(sourcePath)) {
                targetPath = Path.Combine(this.OutputDirectory, filename);
                File.Copy(sourcePath, targetPath, true);
            }
        }

        bool SetupEngine() {
            bool boolResult;
            BufferedStream buff;
            Byte[] strArr;
            Stream stream;
            String licenseKey;
            uint errorCode;

            // Initialize the PostSharp license manager

            stream = GetType().Assembly.GetManifestResourceStream("Sc.Postsharp.license");
            buff = new BufferedStream(stream);
            strArr = new Byte[buff.Length];
            buff.Read(strArr, 0, strArr.Length);
            licenseKey = System.Text.Encoding.UTF8.GetString(strArr);
            boolResult = PostSharp.Sdk.Extensibility.Licensing.LicenseManager.Initialize(licenseKey);
            if (boolResult == false) {
                errorCode = Error.SCERRBADPOSTSHARPLICENSE;
                Program.ReportProgramError(errorCode);
                return false;
            }

            // Assure the system project files exist and are reachable for this
            // utility. Raise an error if not.

            var analyzerProjectFile = Path.GetFullPath(Path.Combine(this.WeaverRuntimeDirectory, CodeWeaver.AnalyzerProjectFileName));
            if (!File.Exists(analyzerProjectFile)) {
                errorCode = Error.SCERRWEAVERPROJECTFILENOTFOUND;
                Program.ReportProgramError(
                    errorCode,
                    ErrorCode.ToMessage(errorCode, string.Format("Path: {0}", analyzerProjectFile))
                    );
                return false;
            }
            this.AnalyzerProjectFile = analyzerProjectFile;

            var weaverProjectFile = Path.GetFullPath(Path.Combine(this.WeaverRuntimeDirectory, CodeWeaver.WeaverProjectFileName));
            if (!File.Exists(weaverProjectFile)) {
                errorCode = Error.SCERRWEAVERPROJECTFILENOTFOUND;
                Program.ReportProgramError(
                    errorCode,
                    ErrorCode.ToMessage(errorCode, string.Format("Path: {0}", weaverProjectFile))
                    );
                return false;
            }
            this.WeaverProjectFile = weaverProjectFile;

            var bootstrapWeaverProjectFile = Path.GetFullPath(Path.Combine(this.WeaverRuntimeDirectory, CodeWeaver.BootstrapWeaverProjectFileName));
            if (!File.Exists(bootstrapWeaverProjectFile)) {
                errorCode = Error.SCERRWEAVERPROJECTFILENOTFOUND;
                Program.ReportProgramError(
                    errorCode,
                    ErrorCode.ToMessage(errorCode, string.Format("Path: {0}", bootstrapWeaverProjectFile))
                    );
                return false;
            }
            this.BootstrapWeaverProjectFile = bootstrapWeaverProjectFile;

            // Decide all finalized directory paths to use and make sure all
            // directories we might need is actually in place.

            this.TempDirectoryPath = Path.Combine(this.CacheDirectory, "WeaverTemp");
            if (!Directory.Exists(this.TempDirectoryPath)) Directory.CreateDirectory(this.TempDirectoryPath);

            // Create the cache

            this.Cache = new WeaverCache(this.CacheDirectory);
            this.Cache.Disabled = this.DisableWeaverCache;
            this.Cache.AssemblySearchDirectories.Add(this.InputDirectory);
            this.Cache.AssemblySearchDirectories.Add(this.WeaverRuntimeDirectory);

            return true;
        }

        bool Setup() {
            string fileToLoad;
            string[] filesToConsider;
            WeaverCache.CachedAssembly cachedAssembly;

            // First setup engine specific stuff.

            if (!SetupEngine())
                return false;

            // Iterate all assemblies in the library (input) directory that was specified
            // and prepare them to be invoked by the weaver.

            this.Assemblies = new Dictionary<string, ModuleLoadStrategy>(StringComparer.InvariantCultureIgnoreCase);

            var specifiedAssemblyFullPath = Path.Combine(this.InputDirectory, this.AssemblyFile);
            if (!File.Exists(specifiedAssemblyFullPath)) {
                Program.ReportProgramError(Error.SCERRWEAVERFILENOTFOUND, "Path: {0}", specifiedAssemblyFullPath);
                return false;
            }

            // When given a single assembly file as the argument, the
            // preferable strategy would be only to consider this file and
            // then have it's possible dependencies that also contain code
            // that target Starcounter resolved on the fly, in the callback
            // from PostSharp, asking for project invocation parameters
            // (i.e. GetProjectInvocationParameters).
            //   However, as of today, the above approach doesn't seem to
            // work due to an error (NullReferenceException) happening inside
            // one of the default PostSharp tasks when we set it up like
            // this.
            //   Based on that, we currently give all companioning binaries
            // to the PostSharp engine too, which is not very good at all since
            // it will load unneccesary code and have it somewhat examined.
            //   We give the user the option to override this by specifying
            // in configuration files he/she want's to explicitly exclude.

            // 20121126
            // When we extend the weaver to support an alternative weaving,
            // weaving only the executables entrypoint to support bootstraping,
            // we consider nothing more than the executable itself.
            string[] dlls;
            if (this.WeaveBootstrapperCode) {
                dlls = new string[0];
            } else {
                dlls = Directory.GetFiles(this.InputDirectory, "*.dll");
            }
            filesToConsider = dlls;

            if (Path.GetExtension(this.AssemblyFile).Equals(".exe")) {
                filesToConsider = new string[dlls.Length + 1];
                filesToConsider[0] = Path.Combine(this.InputDirectory, this.AssemblyFile);
                Array.Copy(dlls, 0, filesToConsider, 1, dlls.Length);
            }

            // Now check every file we need to consider, evaluate if their in the
            // cache and usable and/or if they have been excluded by rule. For every
            // other file, we create a module load strategy for PostSharp to use and
            // will investigate them further using the weaver engine.

            foreach (string file in filesToConsider) {
                // If it's to be completly excluded, just ignore it. Let it be in
                // the input directory.

                if (FileIsToBeExcluded(file)) {
                    Program.WriteInformation("Assembly \"{0}\" not processed, since it was excluded by rule.",
                        Path.GetFileName(file));
                    continue;
                }

                // Check if its in the cache and is considered up-to-date.
                // Extract it or only "get" it if we are only compiling to
                // the cache.

                string cachedName = Path.GetFileNameWithoutExtension(file);

                if (this.WeaveToCacheOnly) {
                    cachedAssembly = this.Cache.Get(cachedName);
                } else {
                    cachedAssembly = this.Cache.Extract(cachedName, this.OutputDirectory);
                }

                if (cachedAssembly.Assembly != null) {
                    // We could use the assembly from the cache. No need to run it
                    // through the weaver.

                    Program.WriteDebug("Assembly \"{0}\" not processed, since it was cached and up-to-date.",
                        Path.GetFileName(file));
                    continue;
                }

                // It's not in the cache, or outdated, and it's not to be excluded.
                // We need to investigate it further (loading it's code model in using
                // PostSharp). Decide the file to load (depending on what we are told
                // to do) and decide the module load strategy.

                Program.WriteDebug(
                    "Unable to extract assembly \"{0}\" from the cache: {1}",
                    Path.GetFileName(file),
                    WeaverUtilities.GetExtractionFailureReason(cachedAssembly)
                    );

                fileToLoad = Path.Combine(this.InputDirectory, Path.GetFileName(file));
                fileToLoad = Path.GetFullPath(fileToLoad);
                this.Assemblies.Add(fileToLoad, new CodeWeaverModuleLoadStrategy(fileToLoad));
            }

            return true;
        }

        bool FileIsToBeExcluded(string file) {
            // Since we currently have defined no way to configure excludes in PeeDee/2.2,
            // no exclusion occurs, except for the standard excludes (PostSharp, Starcounter).
            // When we need it, add configuration possibility and populate the same set.

            string fileName = Path.GetFileName(file);
            foreach (Regex regex in weaverExcludes) {
                if (regex.IsMatch(fileName))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Called when a message is broadcasted from the weaver engine.
        /// The message can be either informal, a warning or report about
        /// an error.
        /// </summary>
        /// <param name="sender">Object sending the message.</param>
        /// <param name="e">Message event arguments, including a reference
        /// to the actual message.</param>
        void OnWeaverMessage(object sender, MessageEventArgs e) {
            Message message;
            string messageIdAndText;
            uint errorCode;

            message = e.Message;
            if (message.MessageId.Equals(message.MessageText)) {
                messageIdAndText = message.MessageText;
            } else {
                messageIdAndText = string.Format("{0} - {1}", message.MessageId, message.MessageText);
                // Semi-hack. There are a few places where multiple braces are used, and it will
                // make the stream writer fuck out. All output from the weaver should be gone through
                // later, so I don't really want to start tampering with it all right now.
                messageIdAndText = messageIdAndText.Replace("{", string.Empty).Replace("}", string.Empty);
            }

            switch (message.Severity) {
                case SeverityType.Debug:
                    Program.WriteDebug(messageIdAndText);
                    break;
                case SeverityType.Verbose:
                case SeverityType.CommandLine:
                case SeverityType.ImportantInfo:
                case SeverityType.Info:
                    Program.WriteInformation(messageIdAndText);
                    break;
                case SeverityType.Warning:
                    Program.WriteWarning(messageIdAndText);
                    break;
                case SeverityType.Error:
                case SeverityType.Fatal:
                    if (message.Source.Equals("Starcounter.Weaver")) {
                        // Errors coming from the weaver can carry location
                        // information. We should utilize it to provide even
                        // better feedback.
                        //
                        // If the message stems from an error code, we should
                        // also remove the ID from the message we write since
                        // the formatted error already contains the ID (as
                        // it's header). See ErrorMessage class.

                        if (ErrorCode.TryParseDecorated(message.MessageId, out errorCode)) {
                            // The message stems from an error code. Use that fact to
                            // produce a better output.

                            messageIdAndText = message.MessageText;
                        } else {
                            // Legacy weaver error output. We must convert it from the old
                            // for to the new.

                            errorCode = WeaverUtilities.WeaverMessageToErrorCode(message.MessageId);
                            if (errorCode == 0) {
                                errorCode = Error.SCERRUNSPECIFIED;
                            }
                        }

                        // Append location information at the end of the message if we
                        // are instructed to create error message parcels.

                        if (Program.IsCreatingParceledErrors) {
                            // Every parceled error message should contain a file location
                            // by protocol. If the information is lacking, we still create
                            // an empty location.

                            FileLocation fileLocation;

                            if (message.Location == null || message.Location.Equals(MessageLocation.Unknown))
                                fileLocation = FileLocation.Unknown;
                            else {
                                fileLocation = new FileLocation(
                                    message.Location.File,
                                    message.Location.Line,
                                    message.Location.Column
                                    );
                            }

                            // Send the actual message as a parameter and the location
                            // information as another.

                            Program.ReportProgramError(
                                errorCode,
                                "{0} {1}",
                                messageIdAndText,
                                fileLocation.ToString()
                                );
                        } else {
                            // Give no location information

                            Program.ReportProgramError(errorCode, messageIdAndText);
                        }
                    } else {
                        // PostSharp can report errors too. Write these as warnings.

                        Program.WriteWarning(messageIdAndText);
                    }
                    break;
            }
        }

        internal ProjectInvocationParameters GetProjectInvocationParametersForAssemblies(ModuleDeclaration module) {
            ProjectInvocationParameters parameters = null;
            string file = module.FileName;
            ModuleLoadStrategy loadStrategy;
            string weaverProjectFile;

            // Check if the module is part of the set of assemblies we have
            // established we must process.
            //
            // If we return NULL, the assembly will not be analyzed and/or
            // transformed, and neither will any of it's dependencies (i.e.
            // they will never even be considered). To ignore an assmbly,
            // but to keep follow it's dependencies, return a project with
            // no tasks instead (like "ScIgnore.psproj").

            if (!this.Assemblies.TryGetValue(file, out loadStrategy))
                return null;

            // Yes, we'll have it processed.

            if (RunWeaver) {
                weaverProjectFile = this.WeaveBootstrapperCode ? this.BootstrapWeaverProjectFile : this.WeaverProjectFile;
                parameters = new ProjectInvocationParameters(weaverProjectFile);
                parameters.PreventOverwriteAssemblyNames = false;
                parameters.Properties["TempDirectory"] = this.TempDirectoryPath;
                parameters.Properties["ScOutputDirectory"] = this.OutputDirectory;
                parameters.Properties["UseStateRedirect"] = this.UseStateRedirect ? bool.TrueString : bool.FalseString;

            } else {
                // We are only analyzing. Do this straight from the input directory.

                parameters = new ProjectInvocationParameters(this.AnalyzerProjectFile);
                parameters.PreventOverwriteAssemblyNames = true;
            }

            // Apply all general, shared parameters

            parameters.Properties["ScInputDirectory"] = this.InputDirectory;
            parameters.Properties["ScCacheDirectory"] = this.CacheDirectory;
            parameters.Properties["CacheTimestamp"] =
                XmlConvert.ToString(File.GetLastWriteTime(file),
                XmlDateTimeSerializationMode.RoundtripKind);
            parameters.ProcessDependenciesFirst = !this.WeaveBootstrapperCode;
            parameters.Properties["AssemblyName"] = Path.GetFileNameWithoutExtension(file);
            parameters.Properties["AssemblyExtension"] = Path.GetExtension(file);
            parameters.Properties["ResolvedReferences"] = "";
            parameters.Properties["DontCopyToOutput"] = this.WeaveToCacheOnly ? bool.TrueString : bool.FalseString;

            return parameters;
        }

        void AddStandardWeaverExcludes() {
            foreach (var exclude in new string[] {
                "scerrres.dll",
                "schttpparser.dll",
                "PostSharp*.dll",
                "Roslyn*.dll",
                "FasterThanJson.dll",
                "Newtonsoft.Json.dll",
                "BizArk.Core.dll",
                "HtmlAgilityPack.dll",
                "Starcounter.dll",
                "RGiesecke.DllExport.Metadata.dll",
                "Starcounter.UriMatcher.dll",
                "Starcounter.REST.dll",
                "Starcounter.Node.dll",
                "Starcounter.XSON.dll",
                "Starcounter.Logging.dll",
                "Starcounter.Hosting.dll",
                "Starcounter.Hypermedia.dll",
                "Starcounter.Bootstrap.dll",
                "Starcounter.Apps.JsonPatch.dll",
                "Starcounter.Internal.dll",
                "Starcounter.BitsAndBytes.Unsafe.dll",
                "Starcounter.XSON.JsTemplateParser.dll",
                "Mono.CSharp.dll",
                "NetworkIoTest.exe",
                "Starcounter.XSON.JsonByExample"
            }
                ) {
                AddExcludeExpression(exclude, weaverExcludes);
            }
        }

        void AddWeaverExcludesFromFile() {
            string[] excludes;
            string filepath = Path.Combine(this.InputDirectory, "weaver.ignore");

            if (File.Exists(filepath)) {
                excludes = File.ReadAllLines(filepath);
                foreach (var exclude in excludes) {
                    AddExcludeExpression(exclude, weaverExcludes);
                }
            }
        }

        void AddExcludeExpression(string specification, List<Regex> target) {
            target.Add(
                new Regex("^" + specification.Replace(".", "\\.").Replace("?", ".").Replace("*", ".*"),
                    RegexOptions.IgnoreCase
                    ));
        }

        #region IPostSharpHost Members (methods called back by PostSharp)

        /// <summary>
        /// Not supported (and never called).
        /// </summary>
        /// <param name="assemblyFileName"></param>
        /// <param name="moduleName"></param>
        /// <param name="assemblyName"></param>
        /// <returns></returns>
        ProjectInvocationParameters IPostSharpHost.GetProjectInvocationParameters(
            string assemblyFileName,
            string moduleName,
            AssemblyName assemblyName) {
            // This is managed by the local host.
            throw new NotSupportedException();
        }

        /// <summary>
        /// Method called when an assembly is renamed. We don't have anything to do.
        /// </summary>
        /// <param name="oldAssemblyName">Old  name.</param>
        /// <param name="newAssemblyName">New name.</param>
        void IPostSharpHost.RenameAssembly(AssemblyName oldAssemblyName, AssemblyName newAssemblyName) {
        }

        /// <summary>
        /// Method called when an assembly reference should be solved (that is, PostSharp knows the
        /// name of an assembly and wants to load it).
        /// </summary>
        /// <param name="assemblyName">Name of the assembly to load.</param>
        /// <returns>A <see cref="ModuleLoadStrategy"/>, or <b>null</b> to use the default mechanism.</returns>
        string IPostSharpHost.ResolveAssemblyReference(AssemblyName assemblyName) {
            return null;
        }

        #endregion
    }
}
