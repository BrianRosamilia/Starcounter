﻿using BuildSystemHelper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PostBuildTasks
{
    class PostBuildTasks
    {
        /// <summary>
        /// Directories in output that should be deleted before packaging into one installer.
        /// </summary>
        static readonly String[] OutputDirsToDelete =
        {
            ".db",
            ".db.output",
            ".srv",
            "s",
            "NetworkIoTest",
            "Programs"
        };

        /// <summary>
        /// Files in output that should be deleted before packaging into one installer.
        /// </summary>
        static readonly String[] OutputFilesToDelete =
        {
            "personal.xml",
            "facit.cpp.txt"
        };

        /// <summary>
        /// Files in output that should be deleted before packaging into one installer.
        /// </summary>
        static readonly String[] OutputFilePatternsToDelete =
        {
            "*.json",
            "*.tests.dll",
            "*.tests.exe"
        };

        /// <summary>
        /// Clean output directory.
        /// </summary>
        /// <param name="outputFolder"></param>
        static void CleanOutputDirectory(String outputFolder)
        {
            // Checking if environment variable is set.
            if ("True" != Environment.GetEnvironmentVariable("SC_CLEAN_OUTPUT"))
                return;

            Console.WriteLine("Deleting selected directories and files...");

            // Removing selected directories from output.
            foreach (String delDir in OutputDirsToDelete)
            {
                String delDirPath = Path.Combine(outputFolder, delDir);

                if (Directory.Exists(delDirPath))
                {
                    Directory.Delete(delDirPath, true);

                    Console.WriteLine("  Deleted directory: " + delDirPath);
                }
            }

            // Removing selected files from output.
            foreach (String filesPattern in OutputFilesToDelete)
            {
                String delFilePath = Path.Combine(outputFolder, filesPattern);

                if (File.Exists(delFilePath))
                {
                    File.Delete(delFilePath);

                    Console.WriteLine("  Deleted file: " + delFilePath);
                }
            }

            // Removing selected file patterns from output.
            foreach (String filesPattern in OutputFilePatternsToDelete)
            {
                String[] filesToDelete = Directory.GetFiles(outputFolder, filesPattern);
                foreach (String filePath in filesToDelete)
                {
                    File.Delete(filePath);

                    Console.WriteLine("  Deleted file: " + filePath);
                }
            }
        }

        /// <summary>
        /// Writing all contents from the build statistics to public.
        /// </summary>
        /// <param name="buildTempStatisticsFilePath"></param>
        /// <param name="publicStatisticsFilePath"></param>
        public static void WriteStatisticsFromBuildToPublic(String buildTempStatisticsFilePath, String publicStatisticsFilePath)
        {
            if (!File.Exists(buildTempStatisticsFilePath))
                return;

            Console.WriteLine("Writing build statistics to public statistics...");

            String[] tempLines = File.ReadAllLines(buildTempStatisticsFilePath);
            String tempString = "";
            foreach (String line in tempLines)
                tempString += "\\n\\" + Environment.NewLine + line;

            Byte[] tempBytes = Encoding.ASCII.GetBytes(tempString + "\";");

            FileStream fs = null;

            while (true)
            {
                try
                {
                    fs = new FileStream(publicStatisticsFilePath, FileMode.Open, FileAccess.Write, FileShare.None);
                    break;
                }
                catch
                {
                    Thread.Sleep(300);
                }
            }

            fs.Seek(-2, SeekOrigin.End);
            fs.Write(tempBytes, 0, tempBytes.Length);

            fs.Close();
        }

        // Path to final statistics file.
        static readonly String PublicStatisticsFilePath = BuildSystem.PublicLogDir + "\\BuildsStatistics.js";

        static Int32 Main(string[] args)
        {
            try
            {
                // Getting current build configuration.
                String configuration = Environment.GetEnvironmentVariable("Configuration");
                if (configuration == null)
                {
                    throw new Exception("Environment variable 'Configuration' does not exist.");
                }

                // Getting sources directory.
                String sourcesDir = Environment.GetEnvironmentVariable(BuildSystem.CheckOutDirEnvVar);
                if (sourcesDir == null)
                {
                    throw new Exception("Environment variable " + BuildSystem.CheckOutDirEnvVar + " does not exist.");
                }

                // Getting the path to current build consolidated folder.
                String outputFolder = Path.Combine(sourcesDir, "Level1\\Bin\\" + configuration);

                // Killing interrupting processes.
                Console.WriteLine("Killing disturbing processes...");
                BuildSystem.KillAll();
                Thread.Sleep(10000);

                // Checking if output should be cleaned.
                CleanOutputDirectory(outputFolder);

                // Copying build statistics file into public statistics.
                WriteStatisticsFromBuildToPublic(BuildSystem.BuildStatisticsFilePath, PublicStatisticsFilePath);

                return 0;
            }
            catch (Exception generalException)
            {
                return BuildSystem.LogException(generalException);
            }
        }
    }
}