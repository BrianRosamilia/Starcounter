
using Starcounter.Administrator.API.Utilities;
using Starcounter.Advanced;
using Starcounter.Internal;
using Starcounter.Server.PublicModel;
using Starcounter.Server.PublicModel.Commands;
using Starcounter.Server.Rest.Representations.JSON;
using System.Collections.Generic;
using System.Diagnostics;

namespace Starcounter.Administrator.API.Handlers {

    internal static partial class ExecutableCollectionHandler {
        /// <summary>
        /// Handles a POST to this resource.
        /// </summary>
        /// <param name="name">
        /// The name of the engine hosting the executable collection
        /// represented by this resource.</param>
        /// <param name="request">
        /// The REST request.</param>
        /// <returns>The response to be sent back to the client.</returns>
        static Response OnPOST(string name, Request request) {
            Executable exe;
            ErrorDetail errDetail;
            var engine = RootHandler.Host.Engine;
            var runtime = RootHandler.Host.Runtime;
            var async = false;

            var response = RESTUtility.JSON.CreateFromRequest<Executable>(request, out exe);
            if (response != null) return response;
            response = RESTUtility.JSON.ExpectAsynchronousOrNone(request, out async);
            if (response != null) return response;

			int i = 0;
            string[] userArgs = exe.Arguments.Count == 0 ? null : new string[exe.Arguments.Count];
//            for (int i = 0; i < exe.Arguments.Count; i++) {
			foreach (Executable.ArgumentsElementJson arg in exe.Arguments) {
                userArgs[i++] = arg.dummy;
            }

            var cmd = new ExecCommand(engine, exe.Path, exe.WorkingDirectory, userArgs);
            cmd.ApplicationFilePath = exe.ApplicationFilePath;
            cmd.DatabaseName = name;
            cmd.EnableWaiting = !async;
            cmd.RunEntrypointAsynchronous = !exe.IsTool;

            var commandInfo = runtime.Execute(cmd);
            Trace.Assert(commandInfo.ProcessorToken == ExecCommand.DefaultProcessor.Token);
            if (!async) {
                commandInfo = runtime.Wait(commandInfo);
            } else if (!commandInfo.HasError) {
                // Asked to do this asynchronously. We should return a
                // monitor, pointing to the now-running command. Right
                // now we are fine just returning 202 for our own needs.
                // We should design a monitor resource to allow polling
                // and end the last poll with either an error or the
                // Executable reprsenting the result.
                return 202;
            }
            Trace.Assert(commandInfo.IsCompleted);
            
            if (commandInfo.HasError) {
                ErrorInfo single;
                ErrorMessage msg;

                single = null;
                if (ErrorInfoExtensions.TryGetSingleReasonErrorBasedOnServerConvention(commandInfo.Errors, out single)) {
                    var code = single.GetErrorCode();
                    switch (code) {
                        case Error.SCERRDATABASENOTFOUND:
                            msg = single.ToErrorMessage();
                            errDetail = RESTUtility.JSON.CreateError(msg.Code, msg.Body, msg.Helplink);
                            return RESTUtility.JSON.CreateResponse(errDetail.ToJson(), 404);
                        case Error.SCERREXECUTABLENOTFOUND:
                        case Error.SCERRWEAVERFAILEDLOADFILE:
                            msg = single.ToErrorMessage();
                            errDetail = RESTUtility.JSON.CreateError(msg.Code, msg.Body, msg.Helplink);
                            return RESTUtility.JSON.CreateResponse(errDetail.ToJson(), 422);
                        case Error.SCERREXECUTABLEALREADYRUNNING:
                        case Error.SCERRDATABASEENGINENOTRUNNING:
                            // Conflicts with the state expected; we map to HTTP 409.
                            msg = single.ToErrorMessage();
                            errDetail = RESTUtility.JSON.CreateError(msg.Code, msg.Body, msg.Helplink);
                            return RESTUtility.JSON.CreateResponse(errDetail.ToJson(), 409);
                        default:
                            break;
                    };
                }

                if (single == null) {
                    single = commandInfo.Errors[0];
                }
                msg = single.ToErrorMessage();

                errDetail = RESTUtility.JSON.CreateError(msg.Code, msg.ToString(), msg.Helplink);
                return RESTUtility.JSON.CreateResponse(errDetail.ToJson(), 500);
            }

            // The command succeeded, indicating the executable was accepted and
            // is now running inside the engine. We hand out an entity from the
            // state given to us by the processor, and return 201.

            var result = (DatabaseInfo) commandInfo.Result;
            var headers = new Dictionary<string, string>(2);
            var exeCreated = ExecutableHandler.JSON.CreateRepresentation(result, cmd.ExecutablePath, headers);
            exeCreated.StartedBy = exe.StartedBy;
            exeCreated.Arguments = exe.Arguments;
            exeCreated.IsTool = exe.IsTool;
            headers.Add("Location", exeCreated.Uri);

            return RESTUtility.JSON.CreateResponse(exeCreated.ToJson(), 201, headers);
        }
    }
}