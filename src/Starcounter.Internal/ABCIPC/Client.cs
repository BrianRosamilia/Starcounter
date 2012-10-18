﻿
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Starcounter.ABCIPC {

    // Stuff in ABCIPC:
    // 1) We define the notion of requests with a BOOL result. For
    // failures, we allow an explaination.
    // 2) The implementation maps request/response. If a response
    // doesn't meet the current request, we report back to the installed
    // error handler.
    // 3) Implement stuff such as "shutdown" and "unknown request"?
    // 4) Add metadata, like time, unique ID, etc.
    // 5) Implement tracing, so that we can log what is sent/received.
    // 6) Additional built-in messages: pause (ignoring all incoming
    // messages except for shutdown and "resume); Ping? Echo?
    // 7) Ability to send/receive string[] and Dictionary<string, string>
    // using KeyValueBinary. Or make this extension methods?

    public class Client {
        Action<string> send;
        Func<string> receive;

        //private class OutgoingRequest {
        //    public const int Shutdown = 0;
        //    public const int RequestWithoutParameters = 10;
        //    public const int RequestWithNULLParameter = 11;
        //    public const int RequestWithParameters = 12;

        //    public static readonly OutgoingRequest ShutdownRequest = new OutgoingRequest(OutgoingRequest.Shutdown, null, null);

        //    public readonly int RequestType;
        //    public readonly string Message;
        //    public readonly string Parameters;
        //    public readonly string Value;

        //    public OutgoingRequest(int type, string message, string parameters) {
        //        this.RequestType = type;
        //        this.Message = message;
        //        this.Parameters = parameters;

        //        switch (type) {
        //            case 12:
        //                this.Value = string.Format("12{0}{1}{2}{3}", message.Length, message, parameters.Length, parameters);
        //                break;
        //            case 10:
        //                this.Value = string.Format("10{0}{1}", message.Length, message);
        //                break;
        //            case 11:
        //                this.Value = string.Format("11{0}{1}", message.Length, message);
        //                break;
        //            case 0:
        //                this.Value = "00";
        //                break;
        //        }
        //    }
        //}

        public Client(Action<string> send, Func<string> recieve) {
            Bind(send, receive);
        }

        protected Client() {
            this.send = (string request) => { throw new NotImplementedException("Request function has not yet been set."); };
            this.receive = () => { throw new NotImplementedException("Receiving function has not yet been set."); };
        }

        public bool SendShutdown() {
            return SendShutdown(null);
        }

        public bool SendShutdown(Action<Reply> responseHandler) {
            return SendRequest(Request.ShutdownMessage, Request.Protocol.ShutdownRequest, responseHandler);
        }

        // Client shutdown:00 (nothing else), the smallest messsage.
        // 01-09: reserved.
        // Client request w/o parameters: 10.
        // Client request w/ NULL parameter 11.
        // Client request w/ parameters: 12

        // 10-12, cont:
        // 00: length of message + message

        // 12: length of parameter data + parameter data (either string, string[] or dictionary).

        // Server response: 50, (request hash).

        // Simplest message: a message with no parameters. The returned value indicates
        // if the call was a success (i.e the response was "OK"). Returned information and/or
        // error details are ignored/swallowed.

        public bool Send(string message) {
            var protocolMessage = Request.Protocol.MakeRequestStringWithoutParameters(message);
            return SendRequest(message, protocolMessage, null);
        }

        public bool Send(string message, Action<Reply> responseHandler) {
            var protocolMessage = Request.Protocol.MakeRequestStringWithoutParameters(message);
            return SendRequest(message, protocolMessage, responseHandler);
        }

        public bool Send(string message, string parameter) {
            return Send(message, parameter, null);
        }

        public bool Send(string message, string parameter, Action<Reply> responseHandler) {
            string protocolMessage;

            if (parameter == null) {
                protocolMessage = Request.Protocol.MakeRequestStringWithStringNULL(message);
            } else {
                protocolMessage = Request.Protocol.MakeRequestStringWithStringParameter(message, parameter);
            }

            return SendRequest(message, protocolMessage, responseHandler);
        }

        public bool Send(string message, string[] arguments) {
            return Send(message, arguments, null);
        }

        public bool Send(string message, string[] arguments, Action<Reply> responseHandler) {
            string protocol = arguments == null ?
                Request.Protocol.MakeRequestStringWithStringArrayNULL(message) :
                Request.Protocol.MakeRequestStringWithStringArray(message, arguments);
            return SendRequest(message, protocol, responseHandler);
        }

        public bool Send(string message, Dictionary<string, string> arguments) {
            return Send(message, arguments, null);
        }

        public bool Send(string message, Dictionary<string, string> arguments, Action<Reply> responseHandler) {
            string protocol = arguments == null ?
                Request.Protocol.MakeRequestStringWithDictionaryNULL(message) :
                Request.Protocol.MakeRequestStringWithDictionary(message, arguments);
            return SendRequest(message, protocol, responseHandler);
        }

        protected void Bind(Action<string> send, Func<string> recieve) {
            this.send = send;
            this.receive = recieve;
        }

        bool SendRequest(string message, string protocolMessage, Action<Reply> responseHandler) {
            // int hash = protocolMessage.GetHashCode();
            send(protocolMessage);

            Reply reply;
            string stringReply;

            do {
                stringReply = receive();
                reply = Reply.Protocol.Parse(stringReply);

                // If there are protocol-level errors, raise exceptions, not invoking
                // the response handler. We only raise exceptions (on the client) for
                // errors that are actually considered caused by the client.
                RaiseIfMessageUnknown(reply);
                RaiseIfBadSignature(reply, message);

                // Invoke the handler if there is one.

                if (responseHandler != null)
                    responseHandler(reply);

            } while (!reply.IsResponse);

            // Return the result of the request.
            return reply.IsSuccess;
        }

        void RaiseIfMessageUnknown(Reply reply) {
            if (reply._type == Reply.ReplyType.UnknownMessage)
                throw new NotSupportedException(string.Format("The server didn't reconize the sent message. ({0}).", reply.ToString()));
        }

        void RaiseIfBadSignature(Reply reply, string message) {
            if (reply._type == Reply.ReplyType.BadSignature)
                throw new Exception(string.Format("The server side signature for message \"{0}\" did not match the call", message));
        }
    }
}