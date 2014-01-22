using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Reflection;
using BuildSystemHelper;

namespace CheckBuildSystem
{
    class Program
    {
        static String FindIncorrectFiles(
            String searchDirectory,
            String[] fileTypes,
            String[] fileExceptions,
            String[] incorrectPatterns,
            String[] requiredPatterns)
        {
            String incorrectFileList = null;
            if (fileTypes == null)
            {
                throw new ArgumentException("fileTypes argument is null");
            }

            // Working with each file type.
            foreach (String fileType in fileTypes)
            {
                // Retrieving all matching files recursively.
                String[] allMatchingFiles = Directory.GetFiles(searchDirectory, fileType, SearchOption.AllDirectories);

                // Checking if there are any matching files.
                if (allMatchingFiles.Length > 0)
                {
                    // Running through each incorrect pattern.
                    if (incorrectPatterns != null)
                    {
                        foreach (String incorrectPattern in incorrectPatterns)
                        {
                            // Running through every incorrect pattern.
                            Regex rgx = new Regex(incorrectPattern, RegexOptions.IgnoreCase);
                            foreach (String file in allMatchingFiles)
                            {
                                Boolean ignoreFile = false;

                                // Checking if the file is an exception.
                                if (fileExceptions != null)
                                {
                                    foreach (String fileExc in fileExceptions)
                                    {
                                        if (file.EndsWith(fileExc, StringComparison.InvariantCultureIgnoreCase))
                                        {
                                            // Ignoring this file from exemption.
                                            ignoreFile = true;
                                            break;
                                        }
                                    }
                                }

                                // Find matches in file if its not skipped.
                                if (!ignoreFile)
                                {
                                    String fileText = File.ReadAllText(file);

                                    // Find matches in text.
                                    MatchCollection matches = rgx.Matches(fileText);
                                    if (matches.Count > 0)
                                    {
                                        incorrectFileList += "File \"" + file + "\"" +
                                            Environment.NewLine + "Does not satisfy the build system policy:" +
                                            Environment.NewLine + "File type: " + fileType +
                                            Environment.NewLine + "Found incorrect pattern: " + incorrectPattern +
                                            Environment.NewLine +
                                            Environment.NewLine;
                                    }
                                }
                            }
                        }
                    }
                    // Checking required patterns.
                    else if (requiredPatterns != null)
                    {
                        foreach (String requiredPattern in requiredPatterns)
                        {
                            // Running through every required pattern.
                            Regex rgx = new Regex(requiredPattern, RegexOptions.IgnoreCase);
                            foreach (String file in allMatchingFiles)
                            {
                                Boolean ignoreFile = false;

                                // Checking if the file is an exception.
                                if (fileExceptions != null)
                                {
                                    foreach (String fileExc in fileExceptions)
                                    {
                                        if (file.EndsWith(fileExc, StringComparison.InvariantCultureIgnoreCase))
                                        {
                                            // Ignoring this file from exemption.
                                            ignoreFile = true;
                                            break;
                                        }
                                    }
                                }

                                // Find matches in file if its not skipped.
                                if (!ignoreFile)
                                {
                                    String fileText = File.ReadAllText(file);

                                    // Find matches in text.
                                    MatchCollection matches = rgx.Matches(fileText);
                                    if (matches.Count <= 0)
                                    {
                                        incorrectFileList += "File \"" + file + "\"" +
                                            Environment.NewLine + "Does not satisfy the build system policy:" +
                                            Environment.NewLine + "File type: " + fileType +
                                            Environment.NewLine + "Not found required pattern: " + requiredPattern +
                                            Environment.NewLine +
                                            Environment.NewLine;
                                    }
                                }
                            }
                        }
                    }
                    // Just checking all matched files.
                    else
                    {
                        String badFileList = null;

                        // Checking if there is an exception file.
                        if (fileExceptions != null)
                        {
                            foreach (String matchedFile in allMatchingFiles)
                            {
                                Boolean fileIgnored = false;
                                foreach (String fileExc in fileExceptions)
                                {
                                    if (matchedFile.EndsWith(fileExc, StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        // Excluding this file.
                                        fileIgnored = true;
                                        break;
                                    }
                                }

                                // Checking if file was an exception.
                                if (!fileIgnored)
                                {
                                    badFileList += matchedFile + Environment.NewLine;
                                }
                            }
                        }
                        else
                        {
                            foreach (String matchedFile in allMatchingFiles)
                            {
                                badFileList += matchedFile + Environment.NewLine;
                            }
                        }

                        if (badFileList != null)
                        {
                            // File type is banned even without a string pattern.
                            incorrectFileList += "File type \"" + fileType + "\"" +
                                Environment.NewLine + "Must not be submitted into source repository:" +
                                Environment.NewLine + "File list:" + Environment.NewLine +
                                badFileList +
                                Environment.NewLine;
                        }
                    }
                }
            }

            return incorrectFileList;
        }

        // Class describing one build system policy.
        class BuildSystemPolicy
        {
            // File types, e.g. "*.sln" 
            public String[] FileTypes;

            // Incorrect string pattern in file.
            public String[] IncorrectPatterns;

            // Required string pattern in file.
            public String[] RequiredPatterns;

            // File exceptions, e.g. "HtmlAgilityPack.fx.4.0-CP.csproj"
            public String[] FileExceptions;

            // Description of this build system policy.
            public String PolicyDescription;
        }

        // All build system policies are defined here.
        static BuildSystemPolicy[] Policies = new BuildSystemPolicy[]
        {
            new BuildSystemPolicy
            {
                FileTypes = new String[] { "*.sln" },

                IncorrectPatterns = new String[] { "Mixed Platforms" },

                RequiredPatterns = null,

                FileExceptions = null, 

                PolicyDescription = "Solution files should NOT have any Mixed Platforms configurations."
            },

            new BuildSystemPolicy
            {
                FileTypes = new String[] { "*.ilk", "*.force", "*.pdb", "*.suo", "*.ncb", "*.generated.cs", "*.cache", "*.cs.dll" },

                IncorrectPatterns = null,

                RequiredPatterns = null,

                FileExceptions = new String[] { "P4API_x64.pdb" },

                PolicyDescription = "Only binaries that are added to exceptions list can be submitted to source control system."
            },

            new BuildSystemPolicy
            {
                FileTypes = new String[] { "*.csproj" },

                IncorrectPatterns = new String[] { @"\<TargetFrameworkVersion\>v4\.0\<\/TargetFrameworkVersion\>",
                                                   @"\<TargetFrameworkVersion\>v3\.5\<\/TargetFrameworkVersion\>" },

                RequiredPatterns = null,

                FileExceptions = new String[] { "HtmlAgilityPack.fx.4.0-CP.csproj", "Starcounter.InstallerWrapper.csproj" },

                PolicyDescription = "Only .NET v4.5 is allowed for managed projects."
            },

            new BuildSystemPolicy
            {
                FileTypes = new String[] { "*.csproj" },

                IncorrectPatterns = new String[] { @"\<Prefer32Bit\>true\<\/Prefer32Bit\>" },

                RequiredPatterns = null,

                FileExceptions = null,

                PolicyDescription = "No true Prefer32Bit flag is allowed in managed projects."
            },

            new BuildSystemPolicy
            {
                FileTypes = new String[] { "*.vcxproj" },

                IncorrectPatterns = new String[] { @"\<PlatformToolset\>v100\<\/PlatformToolset\>",
                                                   @"\<PlatformToolset\>v90\<\/PlatformToolset\>" },

                RequiredPatterns = null,

                FileExceptions = null,

                PolicyDescription = "Visual Studio 2012 Build Toolset(v110) should be used for all native projects."
            },

            new BuildSystemPolicy
            {
                FileTypes = new String[] { "*.csproj" },

                IncorrectPatterns = null,

                RequiredPatterns = new String[] { @"\<TreatWarningsAsErrors\>true\<\/TreatWarningsAsErrors\>" },

                FileExceptions = new String[] { "HtmlAgilityPack VS2008.csproj",
                                                "HtmlAgilityPack.csproj",
                                                "HtmlAgilityPack.fx.4.0-CP.csproj",
                                                "HtmlAgilityPack.fx.4.5-CP.csproj",
                                                "HtmlAgilityPack.fx.4.5.csproj", 
                                                "ApplicationProjectTemplate.csproj",
                                                "ClassLibraryProjectTemplate.csproj" },

                PolicyDescription = "All managed projects must treat Warnings As Errors."
            },

            new BuildSystemPolicy
            {
                FileTypes = new String[] { "*.vcxproj" },

                IncorrectPatterns = null,

                RequiredPatterns = new String[] { @"\<TreatWarningAsError\>true\<\/TreatWarningAsError\>" },

                FileExceptions = new String[] { "scerrres.vcxproj", "scerrres32.vcxproj" },

                PolicyDescription = "All native projects must treat Warnings As Errors."
            },

            new BuildSystemPolicy
            {
                FileTypes = new String[] { "*.csproj" },

                IncorrectPatterns = null,

                RequiredPatterns = new String[] { @"<DocumentationFile>.*\.XML</DocumentationFile>" },

                FileExceptions = new String[] {
                "TestApp.csproj", "CopyDependenciesBuildServer.csproj", "CopyDependenciesLocal.csproj",
                "CopyDependenciesRemote.csproj", "AdoptProjectToBuildSystem.csproj", "BuildLevel0.csproj",
                "BuildsFillupDaemon.csproj", "BuildSystemHelper.csproj", "CleanUpSourceDirectory.csproj",
                "DaemonObserver.csproj", "EnforceBuildSystemPolicy.csproj", "GenerateInstaller.csproj",
                "InstallshieldMod.csproj", "PackArtifactsAndUpload.csproj", "BuildAndDeploy.csproj",
                "CommonBuildTools.csproj", "Plugin_DragTabControl.csproj", "Plugin_SpeedGrid.csproj",
                "SetAssemblyVersion.csproj", "BranchBuild.csproj", "TestLauncher.csproj",
                "TestsSplitter.csproj", "WikiErrorCodes.csproj", "AppsProjectTemplate.csproj",
                "Starcounter.VS.CSAppsProjectTemplate.csproj", "ExeProjectTemplate.csproj",
                "Starcounter.VS.CSExeProjectTemplate.csproj", "Starcounter.VS.VSIX.csproj",
                "FasterThanJson.Tests.csproj", "HttpParser.Tests.csproj", "hello.csproj",
                "HelloGateway.csproj", "IndexQueryTest.csproj", "MySampleApp.csproj",
                "NetworkIoTest.csproj", "QueryProcessingTest.csproj", "SQLTest.csproj",
                "sccode.csproj", "scweaver.csproj", "star.csproj", "Starcounter.Administrator.csproj",
                "Starcounter.Apps.CodeGeneration.Tests.csproj", "Starcounter.Apps.HtmlReader.Tests.csproj",
                "Starcounter.Apps.JsonReader.Tests.csproj", "Starcounter.Apps.Tests.csproj",
                "BitsAndBytes.Test.csproj", "scerrcc.csproj", "Starcounter.Errors.csproj",
                "Starcounter.InstallerWPF.csproj", "Starcounter.Internal.Tests.csproj",
                "Starcounter.SqlParser.Tests.csproj", "Starcounter.Web.Tests.csproj",
                "DynamoIoc.csproj", "HtmlAgilityPack VS2008.csproj", "HtmlAgilityPack.Tests.csproj",
                "Newtonsoft.Json.csproj", "ServerLogTail.csproj", "LoadAndLatency.csproj",
                "SqlCacheTrasher.csproj", "PolePosition.csproj", "ApplicationProjectTemplate.csproj",
                "ClassLibraryProjectTemplate.csproj", "scweaver.Test.csproj", "Build32BitComponents.csproj",
                "Starcounter.Server.Rest.csproj", "Starcounter.InstallerWrapper.csproj",
                "ErrorHelpPages.csproj", "SPA.csproj", "NodeTest.csproj", "PostBuildTasks.csproj",
                "PreBuildTasks.csproj", "RunUnitTests.csproj", "Starcounter.XSON.JsonByExample.csproj",
                "Starcounter.XSON.PartialClassGenerator.csproj", "Starcounter.XSON.DeserializerCompiler.csproj",
                "Starcounter.XSON.Tests.csproj", "PMail.csproj", "Starcounter.Tracking.csproj", "staradmin.csproj",
				"scadminserver.csproj", "scservice.csproj", "BuildHelpers.csproj", "Starcounter.VS.VSIX.2013.csproj"
                },

                PolicyDescription = "All managed projects must have an XML documentation file."
            },

            new BuildSystemPolicy
            {
                FileTypes = new String[] { "*.csproj", "*.vcxproj" },

                IncorrectPatterns = new String[] { "A1EFDB162FD7" },

                RequiredPatterns = null,

                FileExceptions = new String[] { "ApplicationProjectTemplate.csproj", "ClassLibraryProjectTemplate.csproj" },

                PolicyDescription = "Starcounter Application projects are not allowed in Level1."
            },
        };

        /// <summary>
        /// Strings that should be disallowed in build log.
        /// </summary>
        readonly static String[] BuildLogDisallowedStrings =
        {
            ": warning ",
            ": error "
        };

        /// <summary>
        /// Checks build output file for warnings and errors.
        /// </summary>
        static Int32 CheckBuildLog(String pathToBuildLogFile)
        {
            String s = File.ReadAllText(pathToBuildLogFile);

            s = s.ToLowerInvariant();

            s = s.Replace("warning msb3270", "");

            // Going throw all disallowed strings.
            foreach (String ss in BuildLogDisallowedStrings)
            {
                Int32 i = s.IndexOf(ss);

                if (i < 0)
                {
                    Console.WriteLine("Build log contains a disallowed string: \"" + ss + "\" in: " + s.Substring(i, 100));
                    return 1;
                }
            }

            Console.WriteLine("Build log conforms to build policy!");
            return 0;
        }

        static Int32 Main(String[] args)
        {
            // Printing tool welcome message.
            BuildSystem.PrintToolWelcome("Build System Policy Enforcement Tool");

            String searchDirectory = BuildSystem.GetAssemblyDir();
            if (args.Length > 0)
            {
                searchDirectory = args[0];
                if ("--CheckBuildLog" == args[0])
                    return CheckBuildLog(args[1]);
            }

            TextWriter errorOut = Console.Error;

            String errorStr = null;
            Boolean errorFound = false;
            for (Int32 i = 0; i < Policies.Length; i++)
            {
                // Checking that file types are defined.
                if (Policies[i].FileTypes == null)
                {
                    Console.Error.WriteLine("File types for policy " + i + " is not defined!");
                    return 1;
                }

                // Checking that descriptions are defined.
                if (Policies[i].PolicyDescription == null)
                {
                    Console.Error.WriteLine("Policy description for policy " + i + " is not defined!");
                    return 1;
                }

                // Searching for incorrect files.
                errorStr = FindIncorrectFiles(searchDirectory, Policies[i].FileTypes, Policies[i].FileExceptions, Policies[i].IncorrectPatterns, Policies[i].RequiredPatterns);
                if (errorStr != null)
                {
                    errorFound = true;
                    errorOut.Write(errorStr);
                    errorOut.WriteLine("Error description: " + Policies[i].PolicyDescription);
                    errorOut.WriteLine();
                    errorOut.WriteLine();
                }
            }

            // Checking if any error has been found.
            if (errorFound)
                return 1;

            Console.WriteLine("Build system policy enforcement tool finished successfully.");
            Console.WriteLine("---------------------------------------------------------------");
            return 0;
        }
    }
}
