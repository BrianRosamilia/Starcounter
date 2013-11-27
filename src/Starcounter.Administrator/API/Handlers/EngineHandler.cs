using Starcounter.Advanced;
using Starcounter.Server.PublicModel;
using Starcounter.Server.Rest.Representations.JSON;
using Starcounter.Administrator.API.Utilities;
using System.Collections.Generic;
using System.Diagnostics;

namespace Starcounter.Administrator.API.Handlers {
    /// <summary>
    /// Provides the handlers for the admin server database
    /// engine resource.
    /// </summary>
    internal static partial class EngineHandler {
        static string uriTemplate;
        static string uriTemplateHostProcess;
        static string uriTemplateDbProcess;
        /// <summary>
        /// Provides a set of utility methods for working on strongly typed
        /// Engine JSON representations.
        /// </summary>
        internal static class JSON {
            /// <summary>
            /// Creates a JSON representation of the given application level
            /// database state object.
            /// </summary>
            /// <param name="state">The database whose semantics the JSON
            /// representation represents.</param>
            /// <param name="headers">Dictionary with headers that possibly
            /// will be added to if given and the state indicates we should
            /// do so.</param>
            /// <returns>A strongly types JSON <see cref="Engine"/> instance.
            /// </returns>
            internal static Engine CreateRepresentation(DatabaseInfo state, Dictionary<string, string> headers = null) {
                var admin = RootHandler.API;
                var engine = new Engine();
                var name = state.Name;
                var engineState = state.Engine;
                Trace.Assert(
                    engineState != null, "We should never hand out engine instances with no backing state.");

                engine.Uri = uriTemplate.ToAbsoluteUri(name);
                engine.Database.Name = name;
                engine.Database.Uri = admin.Uris.Database.ToAbsoluteUri(name);
                engine.CodeHostProcess.Uri = uriTemplateHostProcess.ToAbsoluteUri(name);
                engine.DatabaseProcess.Uri = uriTemplateDbProcess.ToAbsoluteUri(name);
                engine.CodeHostProcess.PID = engineState.HostProcessId;
                engine.DatabaseProcess.Running = engineState.DatabaseProcessRunning;
                engine.NoDb = engineState.HasNoDbSwitch();
                engine.LogSteps = engineState.HasLogStepsSwitch();
                engine.Executables.Uri = admin.Uris.Executables.ToAbsoluteUri(name);
                if (engineState.HostedApps != null) {
                    foreach (var executable in engineState.HostedApps) {
                        var exe = engine.Executables.Executing.Add();
                        exe.Path = executable.ApplicationFilePath;
                        exe.Uri = admin.Uris.Executable.ToAbsoluteUri(name, executable.Key);
                    }
                }

                if (headers != null) {
                    headers.Add("ETag", engineState.Fingerprint);
                }

                return engine;
            }

            internal static Response CreateConditionBasedResponse(Request request, EngineInfo engineInfo, bool isGETorHEAD) {
                var status = 0;

                // From RFC2616:
                //
                // "If the request would, without the If-Match header field, result
                // in anything other than a 2xx or 412 status, then the If-Match
                // header MUST be ignored".
                //
                // and
                //
                // The meaning of "If-Match: *" is that the method SHOULD be performed
                // if the representation selected by the origin server [...] exists,
                // and MUST NOT be performed if the representation does not exist".

                if (request["If-Range"] != null)
                    return RESTUtility.JSON.CreateResponse(null, 501);

                var etag = request["If-Match"];
                if (etag != null) {    
                    if (etag.Equals("*")) {
                        status = engineInfo == null ? 412 : 0;
                    }
                    else if (engineInfo == null || !engineInfo.Fingerprint.Equals(etag)) {
                        status = 412;
                    }
                }

                etag = request["If-None-Match"];
                if (etag != null) {
                    // From RFC2616:
                    // "If any of the entity tags match the entity tag of the entity that would
                    // have been returned in the response to a similar GET request (without the
                    // If-None-Match header) on that resource, or if "*" is given and any current
                    // entity exists for that resource, then the server MUST NOT perform the
                    // requested method [...]"
                    //
                    // and
                    //
                    // "Instead, if the request method was GET or HEAD, the server SHOULD
                    // respond with a 304 (Not Modified) response, including the cache- related
                    // header fields (particularly ETag) of one of the entities that matched.
                    // For all other request methods, the server MUST respond with a status of
                    // 412 (Precondition Failed)."

                    if (etag.Equals("*")) {
                        // From RFC2616:
                        // "The meaning of "If-None-Match: *" is that the method MUST NOT be
                        // performed if the representation selected by the origin server
                        // [...] exists, and SHOULD be performed if the representation does
                        // not exist".
                        if (engineInfo != null) {
                            status = isGETorHEAD ? 304 : 412;
                        }
                    }
                    else if (engineInfo != null && engineInfo.Fingerprint.Equals(etag)) {
                        status = isGETorHEAD ? 304 : 412;
                    }
                }

                if (status == 0) {
                    return null;
                } else if (status == 412) {
                    var errDetail = RESTUtility.JSON.CreateError(Error.SCERRCOMMANDPRECONDITIONFAILED);
                    return RESTUtility.JSON.CreateResponse(errDetail.ToJson(), 412);
                } else {
                    Trace.Assert(status == 304);
                    return RESTUtility.JSON.CreateResponse(null, 304);
                }
            }
        }

        /// <summary>
        /// Install handlers for the resource represented by this class and
        /// performs custom setup.
        /// </summary>
        internal static void Setup() {
            uriTemplate = RootHandler.API.Uris.Engine;
            uriTemplateHostProcess = uriTemplate + "/host";
            uriTemplateDbProcess = uriTemplate + "/db";

            // Handlers for the larger engine resource/abstraction
            Handle.GET<string, Request>(uriTemplate, OnGET);
            Handle.DELETE<string, Request>(uriTemplate, OnDELETE);
            RootHandler.Register405OnAllUnsupported(uriTemplate, new string[] { "GET", "DELETE" });

            // Handlers for referenced primary processes
            Handle.GET<string, Request>(uriTemplateHostProcess, OnHostGET);
            Handle.DELETE<string, Request>(uriTemplateHostProcess, OnHostDELETE);
            RootHandler.Register405OnAllUnsupported(uriTemplateHostProcess, new string[] { "GET", "DELETE" });
            Handle.GET<string, Request>(uriTemplateDbProcess, OnDatabaseProcessGET);
            RootHandler.Register405OnAllUnsupported(uriTemplateDbProcess, new string[] { "GET" });
        }
    }
}