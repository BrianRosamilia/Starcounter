// ***********************************************************************
// <copyright file="RequestHandler.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using Starcounter.Advanced;
using Starcounter.Internal;
using Starcounter.Internal.Uri;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Starcounter.Rest
{
    /// <summary>
    /// For fast execution, Starcounter will generate code to match incoming HTTP requests.
    /// By matching and parsing the verb and URI, the correct user handler delegate will be called.
    /// This class is responsible to accept registration of user handlers and also to
    /// generate code for and instantiate the top level and sub level RequestProcessors
    /// needed to perform this task. The code generation and instantiation is performed as late
    /// as possible. If additional handlers are registered after code has been generated, a new
    /// version is generated and replaces the old version.
    /// </summary>
    public class RestRegistrationProxy : IREST
    {

        /// <summary>
        /// Registers a handler with no parameters
        /// </summary>
        /// <param name="verbAndUri">The verb and uri of the request. For example GET /things/123</param>
        /// <param name="handler">The code to call when receiving the request</param>
        public void RegisterHandler(ushort port, string verbAndUri, Func<Response> handler)
        {
            UserHandlerCodegen.NewNativeUriCodegen.GenerateParsingDelegate(port, verbAndUri, handler);
        }

        /// <summary>
        /// Registers a handler with one parameter
        /// </summary>
        /// <typeparam name="T">The type of the parameter.</typeparam>
        /// <param name="verbAndUri">The verb and uri of the request. For example GET /things/123</param>
        /// <param name="handler">The code to call when receiving the request</param>
        public void RegisterHandler<T>(ushort port, string verbAndUri, Func<T, Response> handler)
        {
            UserHandlerCodegen.NewNativeUriCodegen.GenerateParsingDelegate(port, verbAndUri, handler);
        }

        /// <summary>
        /// Registers a handler with two parameters
        /// </summary>
        /// <typeparam name="T1">The type of the first parameter.</typeparam>
        /// <typeparam name="T2">The type of the second parameter.</typeparam>
        /// <param name="verbAndUri">The verb and uri of the request. For example GET /things/123</param>
        /// <param name="handler">The code to call when receiving the request</param>
        public void RegisterHandler<T1, T2>(ushort port, string verbAndUri, Func<T1, T2, Response> handler)
        {
            UserHandlerCodegen.NewNativeUriCodegen.GenerateParsingDelegate(port, verbAndUri, handler);
        }

        /// <summary>
        /// Registers a handler with three parameters
        /// </summary>
        /// <typeparam name="T1">The type of the first parameter.</typeparam>
        /// <typeparam name="T2">The type of the second parameter.</typeparam>
        /// <typeparam name="T3">The type of the third parameter.</typeparam>
        /// <param name="verbAndUri">The verb and uri of the request. For example GET /things/123</param>
        /// <param name="handler">The code to call when receiving the request</param>
        public void RegisterHandler<T1, T2, T3>(ushort port, string verbAndUri, Func<T1, T2, T3, Response> handler)
        {
            UserHandlerCodegen.NewNativeUriCodegen.GenerateParsingDelegate(port, verbAndUri, handler);
        }

        /// <summary>
        /// Registers a handler with four parameters
        /// </summary>
        /// <typeparam name="T1">The type of the first parameter.</typeparam>
        /// <typeparam name="T2">The type of the second parameter.</typeparam>
        /// <typeparam name="T3">The type of the third parameter.</typeparam>
        /// <typeparam name="T4">The type of the fourth parameter.</typeparam>
        /// <param name="verbAndUri">The verb and uri of the request. For example GET /things/123</param>
        /// <param name="handler">The code to call when receiving the request</param>
        public void RegisterHandler<T1, T2, T3, T4>(ushort port, string verbAndUri, Func<T1, T2, T3, T4, Response> handler)
        {
            UserHandlerCodegen.NewNativeUriCodegen.GenerateParsingDelegate(port, verbAndUri, handler);
        }

        /// <summary>
        /// Registers a handler with five parameters
        /// </summary>
        /// <typeparam name="T1">The type of the first parameter.</typeparam>
        /// <typeparam name="T2">The type of the second parameter.</typeparam>
        /// <typeparam name="T3">The type of the third parameter.</typeparam>
        /// <typeparam name="T4">The type of the fourth parameter.</typeparam>
        /// <typeparam name="T5">The type of the fifth parameter.</typeparam>
        /// <param name="verbAndUri">The verb and uri of the request. For example GET /things/123</param>
        /// <param name="handler">The code to call when receiving the request</param>
        public void RegisterHandler<T1, T2, T3, T4, T5>(ushort port, string verbAndUri, Func<T1, T2, T3, T4, T5, Response> handler)
        {
            UserHandlerCodegen.NewNativeUriCodegen.GenerateParsingDelegate(port, verbAndUri, handler);
        }
    }

    /// <summary>
    /// Accepts registrations of Rest style user handlers. These handlers
    /// allows the user to catch restful calls using http verbs such as
    /// for example GET, POST, PATCH and DELETE using uri templates with parameters
    /// such as GET /persons/{name}.
    /// </summary>
    /// <remarks>Incomplete, needs some love. TODO!</remarks>
    public class RequestHandler
    {
        /// <summary>
        /// Object used for locking.
        /// </summary>
        static Object lockObject_ = new Object();

        /// <summary>
        /// Indicates if REST has been initialized.
        /// </summary>
        internal static Boolean IsRESTInitialized
        {
            get
            {
                return (null != StarcounterBase._REST);
            }
        }

        /// <summary>
        /// Initializes Starcounter REST settings.
        /// </summary>
        /// <param name="databaseTempDir">Path to database temp directory.</param>
        internal static void InitREST()
        {
            if (IsRESTInitialized)
                return;

            lock (lockObject_)
            {
                if (IsRESTInitialized)
                    return;

                // Initializes HTTP parser.
                Request.sc_init_http_parser();
                StarcounterBase._REST = new RestRegistrationProxy();

                // Resetting URI match builder.
                Reset();
            }
        }

        /// <summary>
        /// Make internal with friends
        /// </summary>
        internal static void Reset() {
            UriMatcherBuilder = new UriMatcherBuilder();
        }

        /// <summary>
        /// The URI matcher builder
        /// </summary>
        internal static UriMatcherBuilder UriMatcherBuilder;  
    }
}

