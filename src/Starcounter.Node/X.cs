// ***********************************************************************
// <copyright file="ScUri.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using System;
using System.Net;
using Starcounter.Internal;
using Starcounter.Advanced;
using System.Net.Sockets;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;

namespace Starcounter
{
    public class X
    {
        /// <summary>
        /// Retrieves endpoint and relative URI information from URI.
        /// </summary>
        /// <param name="uri">Absolute or relative resource URI.</param>
        /// <param name="endpoint">Calculated endpoint.</param>
        /// <param name="relativeUri">Calculated relative URI.</param>
        internal static void GetEndpointFromUri(String uri, out String endpoint, out String relativeUri)
        {
            // Checking if its a localhost communication.
            if ('/' == uri[0])
            {
                endpoint = "127.0.0.1";
                relativeUri = uri;
                
                return;
            }

            endpoint = "";
            relativeUri = "/";

            Int32 startEndpoint = -1, endEndpoint = uri.Length - 1;
            
            for (Int32 i = 0; i < uri.Length; i++)
            {
                Char s = uri[i];
                if ('/' == uri[i])
                {
                    if (startEndpoint < 0)
                    {
                        if (':' == uri[i - 1])
                        {
                            startEndpoint = i + 2;
                            i += 2;
                        }
                        else
                        {
                            startEndpoint = 0;
                            endEndpoint = i - 1;
                            break;
                        }
                    }
                    else
                    {
                        endEndpoint = i - 1;
                        break;
                    }
                }
            }

            if (startEndpoint < 0)
            {
                endpoint = uri;
                return;
            }

            Int32 endpointLen = endEndpoint - startEndpoint + 1;

            endpoint = uri.Substring(startEndpoint, endpointLen);
            
            if (endEndpoint < (uri.Length - 1))
                relativeUri = uri.Substring(endEndpoint + 1);
        }

        // Nodes cache when used for Starcounter client.
        [ThreadStatic]
        static Dictionary<String, Node> ThreadStaticNodeDict;

        // Default node for 127.0.0.1 and default user port.
        [ThreadStatic]
        static Node ThreadStaticThisNode;

        // Nodes cache when used for Starcounter hosted code.
        static Dictionary<String, Node>[] StaticNodeDictArray = new Dictionary<String, Node>[StarcounterConstants.MaximumSchedulersNumber];

        // Default node for 127.0.0.1 and default user port.
        static Node[] StaticThisNodeArray = new Node[StarcounterConstants.MaximumSchedulersNumber];

        // Determines if we are inside hosted Starcounter code.
        static Boolean IsInSccode = false;

        // Static constructor.
        static X()
        {
            if (StarcounterEnvironment.IsCodeHosted)
                IsInSccode = true;
        }

        /// <summary>
        /// Gets node instance from given URI.
        /// </summary>
        /// <param name="uri">Absolute or relative resource URI.</param>
        /// <param name="node">Obtained node instance.</param>
        /// <param name="relativeUri">Calculated relative URI.</param>
        internal static void GetNodeFromUri(String uri, out Node node, out String relativeUri)
        {
            // NOTE: Checking specifically for default localhost endpoint.
            // Just a performance optimization.
            if ('/' == uri[0])
            {
                relativeUri = uri;

                if (IsInSccode)
                {
                    Byte curSchedulerId = StarcounterEnvironment.GetCurrentSchedulerId();

                    // Getting Node dictionary from array by current scheduler index.
                    if (null == StaticThisNodeArray[curSchedulerId])
                        StaticThisNodeArray[curSchedulerId] = new Node("127.0.0.1");

                    node = StaticThisNodeArray[curSchedulerId];
                }
                else
                {
                    // Checking if static object is initialized.
                    if (null == ThreadStaticThisNode)
                        ThreadStaticThisNode = new Node("127.0.0.1");

                    // Getting thread static instance.
                    node = ThreadStaticThisNode;
                }

                return;
            }

            Dictionary<String, Node> nodesDict;

            if (IsInSccode)
            {
                Byte curSchedulerId = StarcounterEnvironment.GetCurrentSchedulerId();

                // Checking if node dictionary is already created.
                if (null == StaticNodeDictArray[curSchedulerId])
                    StaticNodeDictArray[curSchedulerId] = new Dictionary<String, Node>();

                // Getting Node dictionary from array by current scheduler index.
                nodesDict = StaticNodeDictArray[curSchedulerId];
            }
            else
            {
                // Checking if static object is initialized.
                if (null == ThreadStaticNodeDict)
                    ThreadStaticNodeDict = new Dictionary<String, Node>();

                // Getting thread static instance.
                nodesDict = ThreadStaticNodeDict;
            }

            // Calculating endpoint name from given URI.
            String endpoint;
            GetEndpointFromUri(uri, out endpoint, out relativeUri);

            // Trying to get node instance from node cache.
            if (!nodesDict.TryGetValue(endpoint, out node))
            {
                String endpointWithoutPort = endpoint;
                UInt16 port = 0;

                Int32 i = endpoint.Length - 2, colonPos = -1;

                // Checking if port is defined.
                while (i >= 0)
                {
                    if (endpoint[i] == ':')
                    {
                        colonPos = i;
                        break;
                    }
                    i--;
                }

                // Checking if port is specified within URI.
                if (colonPos > 0) {
                    port = UInt16.Parse(endpoint.Substring(colonPos + 1));
                    endpointWithoutPort = endpoint.Substring(0, colonPos);
                }

                node = new Node(endpointWithoutPort, port);

                // Adding node to dictionary.
                nodesDict.Add(endpoint, node);
            }
        }

        /// <summary>
        /// Checks for local cache.
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="customHeaders"></param>
        /// <param name="userObject"></param>
        /// <param name="userDelegate"></param>
        internal static Boolean CheckLocalCache(String uri, 
                                                bool internalRequest,
                                                Object userObject, 
                                                Action<Response, Object> userDelegate, 
                                                out Response resp)
        {
            resp = null;

            // Checking if we can reuse the cache.
            if (uri[0] == '/')
            {
                if (null != Session.Current)
                {
                    Json cachedObj = Session.Current.GetCachedJsonNode(uri);
                    if (null != cachedObj)
                    {
                        if (internalRequest || !cachedObj.HasBeenSent) {
                            // Calling user delegate directly.
                            if (null != userDelegate)
                                userDelegate.Invoke(cachedObj, userObject);
                            else
                                resp = cachedObj;

                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Performs asynchronous HTTP GET.
        /// </summary>
        /// <param name="uri">Resource URI, e.g.: "/hello", "index.html", "/"</param>
        /// <param name="receiveTimeoutMs">Timeout for receive in milliseconds.</param>
        public static object GET(String uri, Int32 receiveTimeoutMs = 0)
        {
            Response r;
            GET(uri, out r, null, receiveTimeoutMs);
            return r.Content;
        }

        /// <summary>
        /// Performs asynchronous HTTP GET.
        /// </summary>
        /// <param name="uri">Resource URI, e.g.: "/hello", "index.html", "/"</param>
        /// <param name="receiveTimeoutMs">Timeout for receive in milliseconds.</param>
        public static T GET<T>(String uri, Int32 receiveTimeoutMs = 0)
        {
            Response r;
            GET(uri, out r, null, receiveTimeoutMs);
            return r.GetContent<T>();
        }

        /// <summary>
        /// Performs asynchronous HTTP GET.
        /// </summary>
        /// <param name="uri">Resource URI, e.g.: "/hello", "index.html", "/"</param>
        /// <param name="customHeaders">Custom HTTP headers or null, e.g.: "MyNewHeader: value123\r\n"</param>
        /// <param name="response">Generated response.</param>
        /// <param name="receiveTimeoutMs">Timeout for receive in milliseconds.</param>
        public static void GET(String uri, out Response response, String customHeaders = null, Int32 receiveTimeoutMs = 0)
        {
            // Checking if we can reuse the cache.
            if (IsInSccode && CheckLocalCache(uri, true, null, null, out response))
                return;

            Node node;
            String relativeUri;

            GetNodeFromUri(uri, out node, out relativeUri);

            response = node.GET(relativeUri, customHeaders, receiveTimeoutMs);
        }

        /// <summary>
        /// Performs asynchronous HTTP GET.
        /// </summary>
        /// <param name="uri">Resource URI, e.g.: "/hello", "index.html", "/"</param>
        /// <param name="customHeaders">Custom HTTP headers or null, e.g.: "MyNewHeader: value123\r\n"</param>
        /// <param name="userObject">User object to be passed on response.</param>
        /// <param name="userDelegate">User delegate to be called on response.</param>
        /// <param name="receiveTimeoutMs">Timeout for receive in milliseconds.</param>
        public static void GET(String uri, String customHeaders, Object userObject, Action<Response, Object> userDelegate, Int32 receiveTimeoutMs = 0)
        {
            Response resp;

            // Checking if we can reuse the cache.
            if (IsInSccode && CheckLocalCache(uri, true, userObject, userDelegate, out resp))
                return;

            Node node;
            String relativeUri;

            GetNodeFromUri(uri, out node, out relativeUri);

            node.GET(relativeUri, customHeaders, userObject, userDelegate, receiveTimeoutMs);
        }

        /// <summary>
        /// Performs asynchronous HTTP POST.
        /// </summary>
        /// <param name="uri">Resource URI, e.g.: "/hello", "index.html", "/"</param>
        /// <param name="body">HTTP body or null.</param>
        /// <param name="customHeaders">Custom HTTP headers or null, e.g.: "MyNewHeader: value123\r\n"</param>
        /// <param name="userObject">User object to be passed on response.</param>
        /// <param name="userDelegate">User delegate to be called on response.</param>
        /// <param name="receiveTimeoutMs">Timeout for receive in milliseconds.</param>
        public static void POST(String uri, String body, String customHeaders, Object userObject, Action<Response, Object> userDelegate, Int32 receiveTimeoutMs = 0)
        {
            Node node;
            String relativeUri;

            GetNodeFromUri(uri, out node, out relativeUri);

            node.POST(relativeUri, body, customHeaders, userObject, userDelegate, receiveTimeoutMs);
        }

        /// <summary>
        /// Performs asynchronous HTTP POST.
        /// </summary>
        /// <param name="uri">Resource URI, e.g.: "/hello", "index.html", "/"</param>
        /// <param name="body">HTTP body or null.</param>
        /// <param name="customHeaders">Custom HTTP headers or null, e.g.: "MyNewHeader: value123\r\n"</param>
        /// <param name="userObject">User object to be passed on response.</param>
        /// <param name="userDelegate">User delegate to be called on response.</param>
        /// <param name="receiveTimeoutMs">Timeout for receive in milliseconds.</param>
        public static void POST(String uri, Byte[] bodyBytes, String customHeaders, Object userObject, Action<Response, Object> userDelegate, Int32 receiveTimeoutMs = 0)
        {
            Node node;
            String relativeUri;

            GetNodeFromUri(uri, out node, out relativeUri);

            node.POST(relativeUri, bodyBytes, customHeaders, userObject, userDelegate, receiveTimeoutMs);
        }

        /// <summary>
        /// Performs synchronous HTTP POST.
        /// </summary>
        /// <param name="uri">Resource URI, e.g.: "/hello", "index.html", "/"</param>
        /// <param name="body">HTTP body or null.</param>
        /// <param name="customHeaders">Custom HTTP headers or null, e.g.: "MyNewHeader: value123\r\n"</param>
        /// <param name="receiveTimeoutMs">Timeout for receive in milliseconds.</param>
        /// <returns>HTTP response.</returns>
        public static Response POST(String uri, String body, String customHeaders, Int32 receiveTimeoutMs = 0)
        {
            Node node;
            String relativeUri;

            GetNodeFromUri(uri, out node, out relativeUri);

            return node.POST(relativeUri, body, customHeaders, receiveTimeoutMs);
        }

        /// <summary>
        /// Performs synchronous HTTP POST.
        /// </summary>
        /// <param name="uri">Resource URI, e.g.: "/hello", "index.html", "/"</param>
        /// <param name="body">HTTP body or null.</param>
        /// <param name="customHeaders">Custom HTTP headers or null, e.g.: "MyNewHeader: value123\r\n"</param>
        /// <param name="receiveTimeoutMs">Timeout for receive in milliseconds.</param>
        /// <returns>HTTP response.</returns>
        public static Response POST(String uri, Byte[] bodyBytes, String customHeaders, Int32 receiveTimeoutMs = 0)
        {
            Node node;
            String relativeUri;

            GetNodeFromUri(uri, out node, out relativeUri);

            return node.POST(relativeUri, bodyBytes, customHeaders, receiveTimeoutMs);
        }

        /// <summary>
        /// Performs asynchronous HTTP PUT.
        /// </summary>
        /// <param name="uri">Resource URI, e.g.: "/hello", "index.html", "/"</param>
        /// <param name="body">HTTP body or null.</param>
        /// <param name="customHeaders">Custom HTTP headers or null, e.g.: "MyNewHeader: value123\r\n"</param>
        /// <param name="userObject">User object to be passed on response.</param>
        /// <param name="userDelegate">User delegate to be called on response.</param>
        /// <param name="receiveTimeoutMs">Timeout for receive in milliseconds.</param>
        public static void PUT(String uri, String body, String customHeaders, Object userObject, Action<Response, Object> userDelegate, Int32 receiveTimeoutMs = 0)
        {
            Node node;
            String relativeUri;

            GetNodeFromUri(uri, out node, out relativeUri);

            node.PUT(relativeUri, body, customHeaders, userObject, userDelegate, receiveTimeoutMs);
        }

        /// <summary>
        /// Performs asynchronous HTTP PUT.
        /// </summary>
        /// <param name="uri">Resource URI, e.g.: "/hello", "index.html", "/"</param>
        /// <param name="body">HTTP body or null.</param>
        /// <param name="customHeaders">Custom HTTP headers or null, e.g.: "MyNewHeader: value123\r\n"</param>
        /// <param name="userObject">User object to be passed on response.</param>
        /// <param name="userDelegate">User delegate to be called on response.</param>
        /// <param name="receiveTimeoutMs">Timeout for receive in milliseconds.</param>
        public static void PUT(String uri, Byte[] bodyBytes, String customHeaders, Object userObject, Action<Response, Object> userDelegate, Int32 receiveTimeoutMs = 0)
        {
            Node node;
            String relativeUri;

            GetNodeFromUri(uri, out node, out relativeUri);

            node.PUT(relativeUri, bodyBytes, customHeaders, userObject, userDelegate, receiveTimeoutMs);
        }

        /// <summary>
        /// Performs synchronous HTTP PUT.
        /// </summary>
        /// <param name="uri">Resource URI, e.g.: "/hello", "index.html", "/"</param>
        /// <param name="body">HTTP body or null.</param>
        /// <param name="customHeaders">Custom HTTP headers or null, e.g.: "MyNewHeader: value123\r\n"</param>
        /// <param name="receiveTimeoutMs">Timeout for receive in milliseconds.</param>
        /// <returns>HTTP response.</returns>
        public static Response PUT(String uri, String body, String customHeaders, Int32 receiveTimeoutMs = 0)
        {
            Node node;
            String relativeUri;

            GetNodeFromUri(uri, out node, out relativeUri);

            return node.PUT(relativeUri, body, customHeaders, receiveTimeoutMs);
        }

        /// <summary>
        /// Performs synchronous HTTP PUT.
        /// </summary>
        /// <param name="uri">Resource URI, e.g.: "/hello", "index.html", "/"</param>
        /// <param name="body">HTTP body or null.</param>
        /// <param name="customHeaders">Custom HTTP headers or null, e.g.: "MyNewHeader: value123\r\n"</param>
        /// <param name="receiveTimeoutMs">Timeout for receive in milliseconds.</param>
        /// <returns>HTTP response.</returns>
        public static Response PUT(String uri, Byte[] bodyBytes, String customHeaders, Int32 receiveTimeoutMs = 0)
        {
            Node node;
            String relativeUri;

            GetNodeFromUri(uri, out node, out relativeUri);

            return node.PUT(relativeUri, bodyBytes, customHeaders, receiveTimeoutMs);
        }

        /// <summary>
        /// Performs asynchronous HTTP PATCH.
        /// </summary>
        /// <param name="uri">Resource URI, e.g.: "/hello", "index.html", "/"</param>
        /// <param name="body">HTTP body or null.</param>
        /// <param name="customHeaders">Custom HTTP headers or null, e.g.: "MyNewHeader: value123\r\n"</param>
        /// <param name="userObject">User object to be passed on response.</param>
        /// <param name="userDelegate">User delegate to be called on response.</param>
        /// <param name="receiveTimeoutMs">Timeout for receive in milliseconds.</param>
        public static void PATCH(String uri, String body, String customHeaders, Object userObject, Action<Response, Object> userDelegate, Int32 receiveTimeoutMs = 0)
        {
            Node node;
            String relativeUri;

            GetNodeFromUri(uri, out node, out relativeUri);

            node.PATCH(relativeUri, body, customHeaders, userObject, userDelegate, receiveTimeoutMs);
        }

        /// <summary>
        /// Performs asynchronous HTTP PATCH.
        /// </summary>
        /// <param name="uri">Resource URI, e.g.: "/hello", "index.html", "/"</param>
        /// <param name="body">HTTP body or null.</param>
        /// <param name="customHeaders">Custom HTTP headers or null, e.g.: "MyNewHeader: value123\r\n"</param>
        /// <param name="userObject">User object to be passed on response.</param>
        /// <param name="userDelegate">User delegate to be called on response.</param>
        /// <param name="receiveTimeoutMs">Timeout for receive in milliseconds.</param>
        public static void PATCH(String uri, Byte[] bodyBytes, String customHeaders, Object userObject, Action<Response, Object> userDelegate, Int32 receiveTimeoutMs = 0)
        {
            Node node;
            String relativeUri;

            GetNodeFromUri(uri, out node, out relativeUri);

            node.PATCH(relativeUri, bodyBytes, customHeaders, userObject, userDelegate, receiveTimeoutMs);
        }

        /// <summary>
        /// Performs synchronous HTTP PATCH.
        /// </summary>
        /// <param name="uri">Resource URI, e.g.: "/hello", "index.html", "/"</param>
        /// <param name="body">HTTP body or null.</param>
        /// <param name="customHeaders">Custom HTTP headers or null, e.g.: "MyNewHeader: value123\r\n"</param>
        /// <param name="receiveTimeoutMs">Timeout for receive in milliseconds.</param>
        /// <returns>HTTP response.</returns>
        public static Response PATCH(String uri, String body, String customHeaders, Int32 receiveTimeoutMs = 0)
        {
            Node node;
            String relativeUri;

            GetNodeFromUri(uri, out node, out relativeUri);

            return node.PATCH(relativeUri, body, customHeaders, receiveTimeoutMs);
        }

        /// <summary>
        /// Performs synchronous HTTP PATCH.
        /// </summary>
        /// <param name="uri">Resource URI, e.g.: "/hello", "index.html", "/"</param>
        /// <param name="body">HTTP body or null.</param>
        /// <param name="customHeaders">Custom HTTP headers or null, e.g.: "MyNewHeader: value123\r\n"</param>
        /// <param name="receiveTimeoutMs">Timeout for receive in milliseconds.</param>
        /// <returns>HTTP response.</returns>
        public static Response PATCH(String uri, Byte[] bodyBytes, String customHeaders, Int32 receiveTimeoutMs = 0)
        {
            Node node;
            String relativeUri;

            GetNodeFromUri(uri, out node, out relativeUri);

            return node.PATCH(relativeUri, bodyBytes, customHeaders, receiveTimeoutMs);
        }

        /// <summary>
        /// Performs asynchronous HTTP DELETE.
        /// </summary>
        /// <param name="uri">Resource URI, e.g.: "/hello", "index.html", "/"</param>
        /// <param name="body">HTTP body or null.</param>
        /// <param name="customHeaders">Custom HTTP headers or null, e.g.: "MyNewHeader: value123\r\n"</param>
        /// <param name="userObject">User object to be passed on response.</param>
        /// <param name="userDelegate">User delegate to be called on response.</param>
        /// <param name="receiveTimeoutMs">Timeout for receive in milliseconds.</param>
        public static void DELETE(String uri, String body, String customHeaders, Object userObject, Action<Response, Object> userDelegate, Int32 receiveTimeoutMs = 0)
        {
            Node node;
            String relativeUri;

            GetNodeFromUri(uri, out node, out relativeUri);

            node.DELETE(relativeUri, body, customHeaders, userObject, userDelegate, receiveTimeoutMs);
        }

        /// <summary>
        /// Performs asynchronous HTTP DELETE.
        /// </summary>
        /// <param name="uri">Resource URI, e.g.: "/hello", "index.html", "/"</param>
        /// <param name="body">HTTP body or null.</param>
        /// <param name="customHeaders">Custom HTTP headers or null, e.g.: "MyNewHeader: value123\r\n"</param>
        /// <param name="userObject">User object to be passed on response.</param>
        /// <param name="userDelegate">User delegate to be called on response.</param>
        /// <param name="receiveTimeoutMs">Timeout for receive in milliseconds.</param>
        public static void DELETE(String uri, Byte[] bodyBytes, String customHeaders, Object userObject, Action<Response, Object> userDelegate, Int32 receiveTimeoutMs = 0)
        {
            Node node;
            String relativeUri;

            GetNodeFromUri(uri, out node, out relativeUri);

            node.DELETE(relativeUri, bodyBytes, customHeaders, userObject, userDelegate, receiveTimeoutMs);
        }

        /// <summary>
        /// Performs synchronous HTTP DELETE.
        /// </summary>
        /// <param name="uri">Resource URI, e.g.: "/hello", "index.html", "/"</param>
        /// <param name="body">HTTP body or null.</param>
        /// <param name="customHeaders">Custom HTTP headers or null, e.g.: "MyNewHeader: value123\r\n"</param>
        /// <param name="receiveTimeoutMs">Timeout for receive in milliseconds.</param>
        /// <returns>HTTP response.</returns>
        public static Response DELETE(String uri, String body, String customHeaders, Int32 receiveTimeoutMs = 0)
        {
            Node node;
            String relativeUri;

            GetNodeFromUri(uri, out node, out relativeUri);

            return node.DELETE(relativeUri, body, customHeaders, receiveTimeoutMs);
        }

        /// <summary>
        /// Performs synchronous HTTP DELETE.
        /// </summary>
        /// <param name="uri">Resource URI, e.g.: "/hello", "index.html", "/"</param>
        /// <param name="body">HTTP body or null.</param>
        /// <param name="customHeaders">Custom HTTP headers or null, e.g.: "MyNewHeader: value123\r\n"</param>
        /// <param name="receiveTimeoutMs">Timeout for receive in milliseconds.</param>
        /// <returns>HTTP response.</returns>
        public static Response DELETE(String uri, Byte[] bodyBytes, String customHeaders, Int32 receiveTimeoutMs = 0)
        {
            Node node;
            String relativeUri;

            GetNodeFromUri(uri, out node, out relativeUri);

            return node.DELETE(relativeUri, bodyBytes, customHeaders, receiveTimeoutMs);
        }

        /// <summary>
        /// Performs asynchronous HTTP request with given HTTP method.
        /// </summary>
        /// <param name="method">HTTP method, e.g.: "OPTIONS", "HEAD".</param>
        /// <param name="uri">Resource URI, e.g.: "/hello", "index.html", "/"</param>
        /// <param name="body">HTTP body or null.</param>
        /// <param name="customHeaders">Custom HTTP headers or null, e.g.: "MyNewHeader: value123\r\n"</param>
        /// <param name="userObject">User object to be passed on response.</param>
        /// <param name="userDelegate">User delegate to be called on response.</param>
        /// <param name="receiveTimeoutMs">Timeout for receive in milliseconds.</param>
        public static void CustomRESTRequest(String method, String uri, String body, String customHeaders, Object userObject, Action<Response, Object> userDelegate, Int32 receiveTimeoutMs = 0)
        {
            Node node;
            String relativeUri;

            GetNodeFromUri(uri, out node, out relativeUri);

            node.CustomRESTRequest(method, relativeUri, body, customHeaders, userObject, userDelegate, receiveTimeoutMs);
        }

        /// <summary>
        /// Performs asynchronous HTTP request with given HTTP method.
        /// </summary>
        /// <param name="method">HTTP method, e.g.: "OPTIONS", "HEAD".</param>
        /// <param name="uri">Resource URI, e.g.: "/hello", "index.html", "/"</param>
        /// <param name="body">HTTP body or null.</param>
        /// <param name="customHeaders">Custom HTTP headers or null, e.g.: "MyNewHeader: value123\r\n"</param>
        /// <param name="userObject">User object to be passed on response.</param>
        /// <param name="userDelegate">User delegate to be called on response.</param>
        /// <param name="receiveTimeoutMs">Timeout for receive in milliseconds.</param>
        public static void CustomRESTRequest(String method, String uri, Byte[] bodyBytes, String customHeaders, Object userObject, Action<Response, Object> userDelegate, Int32 receiveTimeoutMs = 0)
        {
            Node node;
            String relativeUri;

            GetNodeFromUri(uri, out node, out relativeUri);

            node.CustomRESTRequest(method, relativeUri, bodyBytes, customHeaders, userObject, userDelegate, receiveTimeoutMs);
        }

        /// <summary>
        /// Performs synchronous HTTP request with given HTTP method.
        /// </summary>
        /// <param name="method">HTTP method, e.g.: "OPTIONS", "HEAD".</param>
        /// <param name="uri">Resource URI, e.g.: "/hello", "index.html", "/"</param>
        /// <param name="body">HTTP body or null.</param>
        /// <param name="customHeaders">Custom HTTP headers or null, e.g.: "MyNewHeader: value123\r\n"</param>
        /// <param name="receiveTimeoutMs">Timeout for receive in milliseconds.</param>
        /// <returns>HTTP response.</returns>
        public static Response CustomRESTRequest(String method, String uri, String body, String customHeaders, Int32 receiveTimeoutMs = 0)
        {
            Node node;
            String relativeUri;

            GetNodeFromUri(uri, out node, out relativeUri);

            return node.CustomRESTRequest(method, relativeUri, body, customHeaders, receiveTimeoutMs);
        }

        /// <summary>
        /// Performs synchronous HTTP request with given HTTP method.
        /// </summary>
        /// <param name="method">HTTP method, e.g.: "OPTIONS", "HEAD".</param>
        /// <param name="uri">Resource URI, e.g.: "/hello", "index.html", "/"</param>
        /// <param name="body">HTTP body or null.</param>
        /// <param name="customHeaders">Custom HTTP headers or null, e.g.: "MyNewHeader: value123\r\n"</param>
        /// <param name="receiveTimeoutMs">Timeout for receive in milliseconds.</param>
        /// <returns>HTTP response.</returns>
        public static Response CustomRESTRequest(String method, String uri, Byte[] bodyBytes, String customHeaders, Int32 receiveTimeoutMs = 0)
        {
            Node node;
            String relativeUri;

            GetNodeFromUri(uri, out node, out relativeUri);

            return node.CustomRESTRequest(method, relativeUri, bodyBytes, customHeaders, receiveTimeoutMs);
        }

        /// <summary>
        /// Performs asynchronous HTTP request with given HTTP method.
        /// </summary>
        /// <param name="method">HTTP method, e.g.: "OPTIONS", "HEAD".</param>
        /// <param name="uri">Resource URI, e.g.: "/hello", "index.html", "/"</param>
        /// <param name="body">HTTP body or null.</param>
        /// <param name="customHeaders">Custom HTTP headers or null, e.g.: "MyNewHeader: value123\r\n"</param>
        /// <param name="req">Original HTTP request.</param>
        /// <param name="userObject">User object to be passed on response.</param>
        /// <param name="userDelegate">User delegate to be called on response.</param>
        /// <param name="receiveTimeoutMs">Timeout for receive in milliseconds.</param>
        public static void CustomRESTRequest(Request req, Object userObject, Action<Response, Object> userDelegate, Int32 receiveTimeoutMs = 0)
        {
            Node node;
            String relativeUri;

            GetNodeFromUri(req.Uri, out node, out relativeUri);

            node.CustomRESTRequest(req, userObject, userDelegate, receiveTimeoutMs);
        }

        /// <summary>
        /// Performs synchronous HTTP request with given HTTP method.
        /// </summary>
        /// <param name="method">HTTP method, e.g.: "OPTIONS", "HEAD".</param>
        /// <param name="uri">Resource URI, e.g.: "/hello", "index.html", "/"</param>
        /// <param name="body">HTTP body or null.</param>
        /// <param name="customHeaders">Custom HTTP headers or null, e.g.: "MyNewHeader: value123\r\n"</param>
        /// <param name="req">Original HTTP request.</param>
        /// <param name="receiveTimeoutMs">Timeout for receive in milliseconds.</param>
        /// <returns>HTTP response.</returns>
        public static Response CustomRESTRequest(Request req, Int32 receiveTimeoutMs = 0)
        {
            Node node;
            String relativeUri;

            GetNodeFromUri(req.Uri, out node, out relativeUri);

            return node.CustomRESTRequest(req, receiveTimeoutMs);
        }
    }
}