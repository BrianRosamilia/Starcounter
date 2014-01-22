// ***********************************************************************
// <copyright file="StaticWebServer.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Starcounter.Advanced;

namespace Starcounter.Internal.Web {
    /// <summary>
    /// Manages the loading and caching of web resources such as .html files and images. Also keeps track 
    /// of non static web resources such as apps (having dynamic content). Each resource is represented 
    /// by a WebResource instance.
    /// <para><img src="http://www.rebelslounge.com/res/scweb/WebResource.png" /></para>
    /// </summary>
    public partial class StaticWebServer : IRestServer {
        /// <summary>
        /// The web server can accept multiple root catalogues/directories to resolve static
        /// file resources. If the same file can be found in the same relative path in multiple
        /// root directories, the first match is used. For this reason, directories should
        /// be added in priority order with the most prioritised path first.
        /// </summary>
        /// <param name="path">The file path for the directory to add</param>
        public void UserAddedLocalFileDirectoryWithStaticContent(UInt16 port, String path) {
            if (path.EndsWith("\\")) {
                path = path.Substring(0, path.Length - 1);
            }
            if (!Directory.Exists(path)) {
                throw new Exception(
                    String.Format("The directory {0} given as the root file server directory does not exist",
                    path));
            }

            Debug("Adding path to static web server \"" + path + "\"");

            // Always clearing cache when adding new directory on this port.
            ClearCache();

            // Adding only if does not contain this path already.
            if (!workingDirectories.Contains(path))
                workingDirectories.Add(path);
        }

        /// <summary>
        /// Http response cache keyed on URI
        /// </summary>
        /// <remarks>
        /// Http responses are cached in memory. The cache can store both compressed and
        /// uncompressed versions. Compressed and uncompressed items are cached
        /// the first time they are retrieved.
        /// </remarks>
        private Dictionary<string, Response> cacheOnUri;

        /// <summary>
        /// Http response cache keyed on file path on disk
        /// </summary>
        /// <remarks>
        /// Http responses are cached in memory. The cache can store both compressed and
        /// uncompressed versions. Compressed and uncompressed items are cached
        /// the first time they are retrieved.
        /// </remarks>
        private Dictionary<string, Response> cacheOnFilePath;

        /// <summary>
        /// Creates a new web server with an empty cache.
        /// </summary>
        public StaticWebServer() {
            ClearCache();
        }

        /// <summary>
        /// Empties the cache.
        /// </summary>
        public void ClearCache() {
            cacheOnUri = new Dictionary<string, Response>();
            cacheOnFilePath = new Dictionary<string, Response>();
            ClearWatchedParts();
        }

        /// <summary>
        /// As a HttpRestServer, the static file server needs to implement the
        /// Handle method to provide a response to an http request.
        /// </summary>
        /// <param name="request">The http request</param>
        /// <returns>The http response</returns>
        public Response HandleRequest(Request request) {
            return GetStatic(request.Uri, request);
        }

        /// <summary>
        /// Handling the http GET method (verb).
        /// </summary>
        /// <param name="relativeUri">The URI of the resource</param>
        /// <param name="request">The http request as defined by Starcounter</param>
        /// <returns>The UTF8 encoded response</returns>
        public Response GetStatic(string relativeUri, Request request) {
            Response resource;

            relativeUri = relativeUri.ToLower();
            if (cacheOnUri.TryGetValue(relativeUri, out resource)) {
                return resource;
            }

            if (!Configuration.Current.FileServer.DisableAllCaching) {
                // Locking because of possible multi-threaded calls.
                lock (lockObject) {
                    // Checking again if already processed the file..
                    if (cacheOnUri.TryGetValue(relativeUri, out resource)) {
                        Debug("(found cache2) " + relativeUri);
                        return resource;
                    }
                }
            }

            resource = GetFileResource(resource, relativeUri, request);
            return resource;
        }

        /// <summary>
        /// Converts an uncompressed resource to a compressed resource.
        /// </summary>
        /// <param name="input">The uncompressed resource</param>
        /// <returns>The compressed resource</returns>
        private static byte[] Compress(byte[] input) {
            byte[] output;
            using (MemoryStream ms = new MemoryStream()) {
                using (GZipStream gs = new GZipStream(ms, CompressionMode.Compress)) {
                    gs.Write(input, 0, input.Length);
                    gs.Close();
                    output = ms.ToArray();
                }
                ms.Close();
            }
            return output;
        }

        /// <summary>
        /// Housekeeps this instance.
        /// </summary>
        /// <returns>System.Int32.</returns>
        public int Housekeep() {
            //ClearCache(); // TODO! Only invalidate individual items
            var invalidated = new List<Response>(cacheOnFilePath.Count);
            foreach (var cached in this.cacheOnFilePath) {
                var path = cached.Value.FilePath;
                bool was = cached.Value.FileExists;
                bool exists = File.Exists(path);
                if (was != exists || exists && File.GetLastWriteTime(path) != cached.Value.FileModified) {
                    invalidated.Add(cached.Value);
                }
            }
            foreach (var cached in invalidated) {
                var path = cached.FilePath;
                FileSystemEventArgs e = new FileSystemEventArgs(WatcherChangeTypes.Changed, cached.FileDirectory, cached.FileName);
                FileHasChanged(null, e);
            }
            return 1000;
        }
    }
}
