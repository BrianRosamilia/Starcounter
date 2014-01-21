// ***********************************************************************
// <copyright file="WebResourcesTest.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using System;
using Starcounter.Internal.Web;
using NUnit.Framework;

namespace Starcounter.Internal.Tests
{
    /// <summary>
    /// Class WebResourcesTest
    /// </summary>
   public class WebResourcesTest
   {
       /// <summary>
       /// Parses the file specifier.
       /// </summary>
      [Test]
      public static void ParseFileSpecifier()
      {
         string serverPath = "c:";
         string relativeUri = "/test.txt";
         string directory;
         string directoryExpected = "c:";
         string fileName;
         string fileNameExpected = "test";
         string fileExtension;
         string fileExtensionExpected = "txt";
         StaticWebServer target = new StaticWebServer();
         target.ParseFileSpecifier(serverPath,relativeUri, out directory, out fileName, out fileExtension);
         Assert.AreEqual(directoryExpected, directory);
         Assert.AreEqual(fileNameExpected, fileName);

         serverPath = "c:\\test1";
         relativeUri = "/test2/test3/test4.txt";
         directoryExpected = "c:\\test1\\test2\\test3";
         fileNameExpected = "test4";
         fileExtensionExpected = ".txt";
         target = new StaticWebServer();
         target.workingDirectories.Add(serverPath);
         target.ParseFileSpecifier(serverPath,relativeUri, out directory, out fileName, out fileExtension);
         Assert.AreEqual(directoryExpected, directory);
         Assert.AreEqual(fileNameExpected, fileName);
         Assert.AreEqual(fileExtensionExpected, fileExtension);
      }

      /// <summary>
      /// Parses the file specifier with query.
      /// </summary>
      [Test]
      public static void ParseFileSpecifierWithQuery() {
          string serverPath = "c:";
          string relativeUri = "/test.txt?this.is.weird";
          string directory;
          string directoryExpected = "c:";
          string fileName;
          string fileNameExpected = "test";
          string fileExtension;
//          string fileExtensionExpected = "txt";
          StaticWebServer target = new StaticWebServer();
          target.ParseFileSpecifier(serverPath, relativeUri, out directory, out fileName, out fileExtension);
          Assert.AreEqual(directoryExpected, directory);
          Assert.AreEqual(fileNameExpected, fileName);
      }

//      [Test]
//      public static void AlexeyHello() {
//          for (int i = 0 ; i < 10 ; i++ ) {
//          Console.WriteLine("Testsdadsds");
//          }
//      }
   }
}
