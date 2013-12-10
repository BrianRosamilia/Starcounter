
using HttpStructs;
using Starcounter.Internal;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Net;
using Starcounter.Advanced;

namespace Starcounter
{
    internal class ResponseSchedulerStore
    {
        LinkedList<Response> cachedResponses_ = new LinkedList<Response>();

        public Response GetNewInstance()
        {
            if (cachedResponses_.Count > 0)
            {
                LinkedListNode<Response> respNode = cachedResponses_.Last;
                cachedResponses_.RemoveLast();
                return respNode.Value;
            }

            Response resp = new Response();
            resp.AttachToCache(cachedResponses_);
            return resp;
        }
    }

    /// <summary>
    /// Handler response status.
    /// </summary>
    public enum HandlerStatus
    {
        /// <summary>
        /// The response is handled explicitly by some other means.
        /// </summary>
        Handled = 1,

        /// <summary>
        /// The handler didn't handle the request.
        /// Request is going to be handled by outer handler, for example, a static files resolver.
        /// </summary>
        NotHandled = 2
    }

    /// <summary>
    /// Handler response status.
    /// </summary>
    internal enum HandlerStatusInternal
    {
        /// <summary>
        /// The response is already sent.
        /// </summary>
        Done,

        /// <summary>
        /// The response was handled by some explicit means.
        /// </summary>
        Handled,

        /// <summary>
        /// The handler didn't handle the request.
        /// Request is going to be handled by outer handler, for example, a static files resolver.
        /// </summary>
        NotHandled
    }

    /// <summary>
    /// Response exception class which encapsulates the response thrown.
    /// </summary>
    public class ResponseException : Exception
    {
        Response responseObject_;

        Object userObject_;

        /// <summary>
        /// Returns encapsulated Response object.
        /// </summary>
        public Response ResponseObject
        {
            get { return responseObject_; }
            set { responseObject_ = value; }
        }

        /// <summary>
        /// Returns encapsulated User object.
        /// </summary>
        public Object UserObject
        {
            get { return userObject_; }
            set { userObject_ = value; }
        }

        /// <summary>
        /// Response exception constructor.
        /// </summary>
        /// <param name="respObject"></param>
        public ResponseException(Response responseObject) {
            responseObject_ = responseObject;
        }

        /// <summary>
        /// Response exception constructor.
        /// </summary>
        /// <param name="respObject"></param>
        /// <param name="userObject"></param>
        public ResponseException(Response responseObject, Object userObject) {
            responseObject_ = responseObject;
            userObject_ = userObject;
        }
    }

    /// <summary>
    /// Represents an HTTP response.
    /// </summary>
    /// <remarks>
    /// The Starcounter Web Server caches resources as complete http responses.
    /// As the exact same response can often not be used, the cashed response also
    /// include useful offsets and injection points to facilitate fast transitions
    /// to individual http responses. The cached response is also used to cache resources
    /// (compressed or uncompressed content) even if the consumer wants to embed the content
    /// in a new http response.
    /// </remarks>
    public sealed partial class Response
    {
        /*      
        /// <summary>
        /// If true, the Uncompressed and/or compressed byte arrays contains content AND header.
        /// If false, the Uncompressed and/or compressed byte arrays contain only content.
        /// </summary>
        /// <remarks>
        /// This is used when constructing responses from texts where the implicit conversion from
        /// string to response does not know what content type to use by default. The default content
        /// type will depend on the request. I.e. if the user agent makes a request with an Accept header
        /// telling the server that it wants "text/html" and the handler return a string, the default
        /// content type will be "text/html". As the implicit conversion from string to Response does
        /// not have fast and easy access to the request context, the response is created without a
        /// header. The header is constructed by Starcounter just 
        /// </remarks>
        private bool _ByteArrayContainsHeader = true;
        */

        // From which cache list this response came from.
        LinkedList<Response> responseCacheListFrom_ = null;

        // Node with this response.
        LinkedListNode<Response> responseListNode_ = null;

        /// <summary>
        /// Returns the enumerator back to the cache.
        /// </summary>
        internal void ReturnToCache()
        {
            // Returning this enumerator back to the cache.
            responseCacheListFrom_.AddLast(responseListNode_);
        }

        /// <summary>
        /// Should be called when attached to a cache.
        /// </summary>
        /// <param name="fromCache">Cache where this enumerator should be returned.</param>
        internal void AttachToCache(LinkedList<Response> fromCache)
        {
            // Attaching to the specified cache.
            responseCacheListFrom_ = fromCache;

            // Creating cache node from this response.
            responseListNode_ = new LinkedListNode<Response>(this);
        }

        /// <summary>
        /// The _ uncompressed
        /// </summary>
        private byte[] uncompressed_response_ = null;

		/// <summary>
		/// 
		/// </summary>
		private int uncompressedResponseLength_ = 0;

        /// <summary>
        /// The _ compressed
        /// </summary>
        private byte[] compressed_response_ = null;

        /// <summary>
        /// UncompressedBodyOffset_
        /// </summary>
        public int UncompressedBodyOffset_ = 0;

        /// <summary>
        /// CompressedBodyOffset_
        /// </summary>
        public int CompressedBodyOffset_ = 0;

        /// <summary>
        /// UncompressedBodyLength_
        /// </summary>
        public int UncompressedBodyLength_ = 0;

        /// <summary>
        /// CompressedBodyLength_
        /// </summary>
        public int CompressedBodyLength_ = 0;

        /// <summary>
        /// The URIs.
        /// </summary>
        public List<string> Uris = new List<string>();

        /// <summary>
        /// The file path
        /// </summary>
        public string FilePath;

        /// <summary>
        /// The file directory
        /// </summary>
        public string FileDirectory;

        /// <summary>
        /// The file name
        /// </summary>
        public string FileName;

        /// <summary>
        /// The file exists
        /// </summary>
        public bool FileExists;

        /// <summary>
        /// The file modified
        /// </summary>
        public DateTime FileModified;

        /// <summary>
        /// As the session id is a fixed size field, the session id of a cached
        /// response can easily be replaced with a current session id.
        /// </summary>
        /// <value>The session id offset.</value>
        /// <remarks>The offset is only valid in the uncompressed response.</remarks>
        public int SessionIdOffset { get; set; }

        #region BodyInjection
        /// <summary>
        /// Used for content injection.
        /// Where to insert the View Model assignment into the html document.
        /// </summary>
        /// <remarks>
        /// The injection offset (injection point) is only valid in the uncompressed
        /// response.
        /// 
        /// Insertion is made at one of these points (in order of priority).
        /// ======================================
        /// 1. The point after the &lt;head&gt; tag.
        /// 2. The point after the &lt;!doctype&gt; tag.
        /// 3. The beginning of the html document.
        /// </remarks>
        /// <value>The script injection point.</value>
        public int ScriptInjectionPoint { get; set; }

        /// <summary>
        /// Used for content injection.
        /// When injecting content into the response, the content length header
        /// needs to be altered. Used together with the ContentLengthLength property.
        /// </summary>
        /// <value>The content length injection point.</value>
        public int ContentLengthInjectionPoint { get; set; } // Used for injection

        /// <summary>
        /// Used for content injection.
        /// When injecting content into the response, the content length header
        /// needs to be altered. The existing previous number of bytes used for the text
        /// integer length value starting at ContentLengthInjectionPoint is stored here.
        /// </summary>
        /// <value>The length of the content length.</value>
        public int ContentLengthLength { get; set; } // Used for injection

        /// <summary>
        /// Used for injecting headers. Specifies where to insert additional
        /// headers that might be needed.
        /// </summary>
        public int HeaderInjectionPoint { get; set; }

        #endregion

        /// <summary>
        /// Indicates if user wants to send custom response.
        /// </summary>
        Boolean customFields_;

        /// <summary>
        /// Status code.
        /// </summary>
        UInt16 statusCode_;

        /// <summary>
        /// Handling status for this response.
        /// </summary>
        internal HandlerStatusInternal HandlingStatus
        {
            get { return handlingStatus_; }
            set { handlingStatus_ = value; }
        }

        /// <summary>
        /// Handling status.
        /// </summary>
        HandlerStatusInternal handlingStatus_;

        /// <summary>
        /// WebSocket close codes.
        /// </summary>
        public enum WebSocketCloseCodes
        {
            WS_CLOSE_NORMAL = 1000,
            WS_CLOSE_GOING_DOWN = 1001,
            WS_CLOSE_PROTOCOL_ERROR = 1002,
            WS_CLOSE_CANT_ACCEPT_DATA = 1003,
            WS_CLOSE_WRONG_DATA_TYPE = 1007,
            WS_CLOSE_POLICY_VIOLATED = 1008,
            WS_CLOSE_MESSAGE_TOO_BIG = 1009,
            WS_CLOSE_UNEXPECTED_CONDITION = 1011
        }

        /// <summary>
        /// Special connection flags.
        /// </summary>
        public enum ConnectionFlags
        {
            NoSpecialFlags = 0,
            DisconnectAfterSend = MixedCodeConstants.SOCKET_DATA_FLAGS_DISCONNECT_AFTER_SEND,
            DisconnectImmediately = MixedCodeConstants.SOCKET_DATA_FLAGS_DISCONNECT,
            GracefullyCloseConnection = MixedCodeConstants.HTTP_WS_FLAGS_GRACEFULLY_CLOSE
        }

        /// <summary>
        /// Connection flags.
        /// </summary>
        ConnectionFlags connectionFlags_ = ConnectionFlags.NoSpecialFlags;

        /// <summary>
        /// Indicates if corresponding connection should be shut down.
        /// </summary>
        public ConnectionFlags ConnFlags
        {
            get
            {
                return connectionFlags_;
            }

            set
            {
                customFields_ = true;
                connectionFlags_ = value;
            }
        }

        /// <summary>
        /// HTTP response status code.
        /// </summary>
        public UInt16 StatusCode
        {
            get
            {
                EnsureHttpV1IsUsed();

                if (0 == statusCode_)
                {
                    unsafe
                    {
                        if (null == http_response_struct_)
                            throw new ArgumentException("HTTP response not initialized.");

                        return http_response_struct_->status_code_;
                    }
                }

                return statusCode_;
            }

            set
            {
                EnsureHttpV1IsUsed();

                customFields_ = true;
                statusCode_ = value;
            }
        }

        String statusDescription_;

        /// <summary>
        /// Status description.
        /// </summary>
        public String StatusDescription
        {
            get
            {
                EnsureHttpV1IsUsed();

                if (null == statusDescription_)
                {
                    unsafe
                    {
                        if (null == http_response_struct_)
                            throw new ArgumentException("HTTP response not initialized.");

                        return http_response_struct_->GetStatusDescription();
                    }
                }

                return statusDescription_;
            }

            set
            {
                EnsureHttpV1IsUsed();

                customFields_ = true;
                statusDescription_ = value;
            }
        }

        /// <summary>
        /// Content type.
        /// </summary>
        public String ContentType
        {
            get
            {
                EnsureHttpV1IsUsed();

                return this[HttpHeadersUtf8.ContentTypeHeader];
            }

            set
            {
                EnsureHttpV1IsUsed();

                customFields_ = true;
                this[HttpHeadersUtf8.ContentTypeHeader] = value;
            }
        }

		String cacheControl_;

		/// <summary>
		/// 
		/// </summary>
		public String CacheControl
        {
			get
            {
                EnsureHttpV1IsUsed();

                return cacheControl_;
            }

			set
            {
                EnsureHttpV1IsUsed();

				customFields_ = true;
				cacheControl_ = value;
			}
		}

        /// <summary>
        /// Content encoding.
        /// </summary>
        public String ContentEncoding
        {
            get
            {
                EnsureHttpV1IsUsed();

                return this[HttpHeadersUtf8.ContentEncodingHeader];
            }

            set
            {
                EnsureHttpV1IsUsed();

                customFields_ = true;
                this[HttpHeadersUtf8.ContentEncodingHeader] = value;
            }
        }

        String bodyString_;

        /// <summary>
        /// Body string.
        /// </summary>
        public String Body
        {
            get
            {
                if (customFields_)
                {
                    if (null == bodyString_)
                    {
                        if (null != bodyBytes_)
                            return Encoding.UTF8.GetString(bodyBytes_);
                    }

                    return bodyString_;
                }

                return GetBodyStringUtf8_Slow();
            }

            set
            {
                customFields_ = true;
                bodyString_ = value;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetContent<T>() {
            // TODO Ask Jocke!
            if (typeof(T) == typeof(String)) {
                return (T)(object)GetContentString();
            }
            if (typeof(T) == typeof(byte[])) {
                return (T)(object)GetContentBytes();
            } 
            return (T)Content;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string GetContentString() {
            if (bodyString_ != null)
                return bodyString_;
            var bytes = this.GetContentBytes();
            return Encoding.UTF8.GetString(bytes);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private byte[] GetContentBytes() {
            if (bodyBytes_ != null)
                return bodyBytes_;
            if (bodyString_ != null)
                return Encoding.UTF8.GetBytes(bodyString_);
            if (_Hypermedia != null) {
                MimeType discard;
                return _Hypermedia.AsMimeType(MimeType.Unspecified, out discard);
            }
            if (Uncompressed != null) {
                return ExtractBodyFromUncompressedHttpResponse();
            }

            bodyBytes_ = GetBodyBytes_Slow();
            return bodyBytes_;
        }

        /// <summary>
        /// Should be made faster using pointers copying direcly to the output buffer
        /// </summary>
        /// <returns></returns>
        private byte[] ExtractBodyFromUncompressedHttpResponse() {
            var bytes = new byte[this.UncompressedBodyLength_];
            Array.Copy(Uncompressed, UncompressedBodyOffset_, bytes, 0, UncompressedBodyLength_);
            return bytes;
        }

        /// <summary>
        /// Body string.
        /// </summary>
        internal Object Content
        {
            get
            {
                if (null != _Hypermedia)
                    return _Hypermedia;

                if (null != bodyBytes_)
                    return bodyBytes_;

                if (null != bodyString_)
                    return bodyString_;

                return GetBodyStringUtf8_Slow();
            }

            set
            {
                if (value is String) {
                    bodyString_ = (String) value;
                } else if (value is Byte[]) {
                    bodyBytes_ = (Byte[]) value;
                } else if (value is IHypermedia) {
                    _Hypermedia = (IHypermedia) value;
                } else {
                    throw new ArgumentException("Wrong content type assigned!");
                }

                customFields_ = true;
            }
        }

        Byte[] bodyBytes_;

        /// <summary>
        /// Body bytes.
        /// </summary>
        public Byte[] BodyBytes
        {
            get
            {
                if (customFields_)
                {
                    if (null == bodyBytes_)
                    {
                        if (null != bodyString_)
                            return Encoding.UTF8.GetBytes(bodyString_);
                    }

                    return bodyBytes_;
                }

                return GetBodyBytes_Slow();
            }

            set
            {
                customFields_ = true;
                bodyBytes_ = value;
            }
        }

		/// <summary>
		/// 
		/// </summary>
		internal void SetCustomFieldsFlag() {
			customFields_ = true;
		}

        /// <summary>
        /// Resets all custom fields.
        /// </summary>
        internal void ResetAllCustomFields()
        {
            customFields_ = false;

            customHeaderFields_ = null;
            bodyString_ = null;
            statusDescription_ = null;
            statusCode_ = 0;
            AppsSession = null;
            _Hypermedia = null;
        }

        private IHypermedia _Hypermedia;

        /// <summary>
        /// The response can be contructed in one of the following ways:
        /// 
        /// 1) using a complete binary HTTP response
        /// in the Uncompressed or Compressed propery (this includes the header)
        /// 
        /// 2) using a IHypermedia object in the Hypermedia property
        /// 
        /// 3) using the BodyString property (does not include the header)
        /// 
        /// 4) using the BodyBytes property (does not include the header)
        /// </summary>
        public IHypermedia Hypermedia {
            get {
                return _Hypermedia;
            }
            set {
                customFields_ = true;
                _Hypermedia = value;
            }
        }

        /// <summary>
        /// Set-Cookie string.
        /// </summary>
        public String SetCookie
        {
            get
            {
                EnsureHttpV1IsUsed();

                return this[HttpHeadersUtf8.SetCookieHeader];
            }

            set
            {
                EnsureHttpV1IsUsed();

                customFields_ = true;
                this[HttpHeadersUtf8.SetCookieHeader] = value;
            }
        }

        // Type of network protocol.
        MixedCodeConstants.NetworkProtocolType protocol_type_ = MixedCodeConstants.NetworkProtocolType.PROTOCOL_HTTP1;

        /// <summary>
        /// Returns protocol type.
        /// </summary>
        public MixedCodeConstants.NetworkProtocolType ProtocolType
        {
            get { return protocol_type_; }

            internal set { protocol_type_ = value; }
        }

        /// <summary>
        /// Ensures that only HTTP v1 protocol is allowed.
        /// </summary>
        void EnsureHttpV1IsUsed()
        {
            switch (protocol_type_)
            {
                case MixedCodeConstants.NetworkProtocolType.PROTOCOL_HTTP1:
                return;
            }

            throw new NotSupportedException("Network protocol does not support this method call.");
        }

        /// <summary>
        /// Ensures that only HTTP v1 or WebSockets protocol is allowed.
        /// </summary>
        void EnsureHttpV1OrWebSocketsIsUsed()
        {
            switch (protocol_type_)
            {
                case MixedCodeConstants.NetworkProtocolType.PROTOCOL_HTTP1:
                case MixedCodeConstants.NetworkProtocolType.PROTOCOL_WEBSOCKETS:
                return;
            }

            throw new NotSupportedException("Network protocol does not support this method call.");
        }

		/// <summary>
		/// Constructs Response from fields that are set.
		/// </summary>
		public void ConstructFromFields() {

			// Checking if we have a custom response.
			if (!customFields_)
				return;

            byte[] buf;
            Utf8Writer writer;

            EnsureHttpV1OrWebSocketsIsUsed();

            switch (protocol_type_) {

                case MixedCodeConstants.NetworkProtocolType.PROTOCOL_HTTP1: {

			        byte[] bytes = bodyBytes_;
			        if (_Hypermedia != null) {
				        var mimetype = request_.PreferredMimeType;
				        try {
					        bytes = _Hypermedia.AsMimeType(mimetype, out mimetype);
					        this[HttpHeadersUtf8.ContentTypeHeader] = MimeTypeHelper.MimeTypeAsString(mimetype);
				        } catch (UnsupportedMimeTypeException exc) {
					        throw new Exception(
						        String.Format("Unsupported mime-type {0} in request Accept header. Exception: {1}", request_[HttpHeadersUtf8.GetAcceptHeader], exc.ToString()));
				        }

				        if (bytes == null) {
					        // The preferred requested mime type was not supported, try to see if there are
					        // other options.
					        IEnumerator<MimeType> secondaryChoices = request_.PreferredMimeTypes;
					        secondaryChoices.MoveNext(); // The first one is already accounted for
					        while (bytes == null && secondaryChoices.MoveNext()) {
						        mimetype = secondaryChoices.Current;
						        bytes = _Hypermedia.AsMimeType(mimetype, out mimetype);
					        }
					        if (bytes == null) {
						        // None of the requested mime types were supported.
						        // We will have to respond with a "Not Acceptable" message.
						        statusCode_ = 406;
					        } else {
						        this[HttpHeadersUtf8.ContentTypeHeader] = MimeTypeHelper.MimeTypeAsString(mimetype);
					        }
				        }
				        // We have our precious bytes. Let's wrap them up in a response.
			        }

			        buf = new byte[EstimateNeededSize(bytes)];
			
			        unsafe {
				        fixed (byte* p = buf) {
                            writer = new Utf8Writer(p);
					        writer.Write(HttpHeadersUtf8.Http11);

					        if (statusCode_ > 0) {
						        writer.Write(statusCode_);
						        writer.Write(' ');
						
						        // Checking if Status Description is set.
						        if (null != statusDescription_)
							        writer.Write(statusDescription_);
						        else 
							        writer.Write("OK");

						        writer.Write(HttpHeadersUtf8.CRLF);
					        } else {
						        // Checking if Status Description is set.
						        if (null != statusDescription_) {
							        writer.Write(200);
							        writer.Write(' ');
							        writer.Write(statusDescription_);
						        } else
							        writer.Write("200 OK");
						        writer.Write(HttpHeadersUtf8.CRLF);
					        }

					        writer.Write(HttpHeadersUtf8.ServerSc);

					        // TODO:
					        // What should the default cache control be?
					        if (null != cacheControl_) {
						        writer.Write(HttpHeadersUtf8.CacheControlStart);
						        writer.Write(cacheControl_);
						        writer.Write(HttpHeadersUtf8.CRLF);
					        } else
						        writer.Write(HttpHeadersUtf8.CacheControlNoCache);

                            if (null != customHeaderFields_) {

                                foreach (KeyValuePair<string, string> h in customHeaderFields_) {
                                    writer.Write(h.Key);
                                    writer.Write(": ");
                                    writer.Write(h.Value);
                                    if (h.Key == HttpHeadersUtf8.SetCookieHeader)
                                    {
                                        if (null != AppsSession)
                                        {
                                            if (AppsSession.use_session_cookie_)
                                            {
                                                writer.Write(HttpHeadersUtf8.SetSessionCookieMiddle);
                                                writer.Write(AppsSession.ToAsciiString());
                                                writer.Write(HttpHeadersUtf8.SetCookiePathEnd);
                                            }
                                            else
                                            {
                                                writer.Write(HttpHeadersUtf8.SetCookieLocationMiddle);
                                                writer.Write(ScSessionClass.DataLocationUriPrefixEscaped);
                                                writer.Write(AppsSession.ToAsciiString());
                                                writer.Write(HttpHeadersUtf8.SetCookiePathEnd);
                                            }
                                        }
                                    }
                                    writer.Write(HttpHeadersUtf8.CRLF);
                                }
                            }

                            // Checking if session is in place.
                            if (null != AppsSession)
                            {
                                if (this[HttpHeadersUtf8.SetCookieHeader] == null)
                                {
                                    if (AppsSession.use_session_cookie_)
                                    {
                                        writer.Write(HttpHeadersUtf8.SetSessionCookieHeader);
                                        writer.Write(AppsSession.ToAsciiString());
                                        writer.Write(HttpHeadersUtf8.SetCookiePathEnd);
                                        writer.Write(HttpHeadersUtf8.CRLF);
                                    }
                                    else
                                    {
						                writer.Write(HttpHeadersUtf8.SetLocationHeader);
							            writer.Write(ScSessionClass.DataLocationUriPrefixEscaped);
							            writer.Write(AppsSession.ToAsciiString());
							            writer.Write(HttpHeadersUtf8.SetCookiePathEnd);
							            writer.Write(HttpHeadersUtf8.CRLF);	
                                    }
                                }
					        }

					        if (null != bodyString_) {
						        if (null != bytes)
							        throw new ArgumentException("Either body string, body bytes or hypermedia can be set for Response.");

						        writer.Write(HttpHeadersUtf8.ContentLengthStart);
						        writer.Write(writer.GetByteCount(bodyString_));
						        writer.Write(HttpHeadersUtf8.CRLFCRLF);	

						        writer.Write(bodyString_);
					        } else if (null != bytes) {
						        writer.Write(HttpHeadersUtf8.ContentLengthStart);
						        writer.Write(bytes.Length);
						        writer.Write(HttpHeadersUtf8.CRLFCRLF);
						        writer.Write(bytes);
					        } else {
						        writer.Write(HttpHeadersUtf8.ContentLengthStart);
						        writer.Write('0');
						        writer.Write(HttpHeadersUtf8.CRLFCRLF);
					        }
				        }
			        }

                    // Finally setting the uncompressed bytes.
                    uncompressed_response_ = buf;
                    uncompressedResponseLength_ = writer.Written;

                    break;
                }

                case MixedCodeConstants.NetworkProtocolType.PROTOCOL_WEBSOCKETS: {

                    if (statusCode_ > 0) {
                        unsafe {
                            buf = new byte[EstimateNeededSize(bodyBytes_)];

                            Int32 written = 0;

                            fixed (byte* p = buf) {
                                // Setting close flag.
                                ConnFlags = ConnectionFlags.GracefullyCloseConnection;

                                // Writing error code in network order.
                                *(Int16*)p = IPAddress.HostToNetworkOrder((Int16)statusCode_);
                                written += 2;

                                // Writing status description if any.
                                if (null != statusDescription_) {
                                    writer = new Utf8Writer(p + 2);

                                    writer.Write(statusDescription_);
                                    written += writer.Written;
                                }
                            }

                            // Finally setting the uncompressed bytes.
                            uncompressed_response_ = buf;
                            uncompressedResponseLength_ = written;
                        }
                    } else {

                        if (null != bodyBytes_) {
                            uncompressed_response_ = bodyBytes_;
                            uncompressedResponseLength_ = uncompressed_response_.Length;
                        } else if (null != bodyString_) {
                            uncompressed_response_ = UTF8Encoding.UTF8.GetBytes(bodyString_);
                            uncompressedResponseLength_ = uncompressed_response_.Length;
                        }
                    }
                    
                    break;
                }

                default:
                    throw ErrorCode.ToException(Error.SCERRUNKNOWNNETWORKPROTOCOL);
            }

			customFields_ = false;
		}

		private int EstimateNeededSize(byte[] bytes) {
			// The sizes of the strings here is not accurate. We are mainly interested in making sure
			// that we will never have a buffer overrun so we take the length of the strings * 2.
			int size = HttpHeadersUtf8.TotalByteSize;

			if (statusDescription_ != null)
				size += statusDescription_.Length;

            if (null != customHeaderFields_) {
                foreach (KeyValuePair<string, string> h in customHeaderFields_) {
                    size += (h.Key.Length + h.Value.Length + 4);
                }
            }

			if (null != cacheControl_)
				size += cacheControl_.Length;
			if (null != AppsSession) {
				size += ScSessionClass.DataLocationUriPrefixEscaped.Length;
				size += AppsSession.ToAsciiString().Length;
			}

			if (null != bodyString_)
				size += (bodyString_.Length << 1); // Multiplying by 2 for possible UTF8.
			else if (null != bytes)
				size += bytes.Length;

			return size;
		}

        /// <summary>
        /// Checks if status code is resembling success.
        /// </summary>
        public Boolean IsSuccessStatusCode
        {
            get
            {
                EnsureHttpV1IsUsed();

                UInt16 statusCode = StatusCode;
                return (statusCode >= 200) && (statusCode <= 226);
            }
        }

        /// <summary>
        /// The number of bytes containing the http header in the uncompressed response. This is also
        /// the offset of the first byte of the content.
        /// </summary>
        /// <value>The length of the header.</value>
        public Int32 HeadersLength { get; set; }

        /// <summary>
        /// The number of bytes of the content (i.e. the resource) of the uncompressed http response.
        /// </summary>
        /// <value>The length of the content.</value>
        public Int32 ContentLength
        {
            get
            {
                if (customFields_)
                {
                    if (UncompressedBodyLength_ > 0)
                        return UncompressedBodyLength_;

                    if (null != bodyBytes_)
                        return bodyBytes_.Length;

                    if (null != bodyString_)
                        return bodyString_.Length;

                    // By default returning no content.
                    return 0;
                }

                unsafe
                {
                    if (null == http_response_struct_)
                        throw new ArgumentException("HTTP response not initialized.");

                    return http_response_struct_->content_len_bytes_;
                }
            }
            set
            {
                EnsureHttpV1IsUsed();

                UncompressedBodyLength_ = value;
            }
        }

        /// <summary>
        /// The uncompressed cached response
        /// </summary>
        /// <value>The uncompressed.</value>
        internal Byte[] Uncompressed
        {
            get
            {
                return uncompressed_response_;
            }
            set
            {
                uncompressed_response_ = value;
				if (value != null)
					uncompressedResponseLength_ = value.Length;
				else
					uncompressedResponseLength_ = 0;
            }
        }

		/// <summary>
		/// 
		/// </summary>
		internal Int32 UncompressedLength {
			get { return uncompressedResponseLength_; }
			set {
				if (uncompressed_response_ == null)
					throw new ArgumentException("No response defined!");

				if (value > uncompressed_response_.Length) {
					throw new ArgumentOutOfRangeException(
						"value", 
						"Cannot set the length of the response to be larger than the actual response.");
				}
				uncompressedResponseLength_ = value;
			}
		}

        /// <summary>
        /// Getting full response length.
        /// </summary>
        public Int32 ResponseLength
        {
            get
            {
                if (customFields_)
                {
                    if (uncompressed_response_ != null)
                        return uncompressedResponseLength_;

                    throw new ArgumentException("No response defined!");
                }

                unsafe
                {
                    if (null == http_response_struct_)
                        throw new ArgumentException("HTTP response not initialized.");

                    return (Int32)http_response_struct_->response_len_bytes_;
                }
            }
        }

        /// <summary>
        /// Gets the bytes.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>System.Byte[][].</returns>
        internal byte[] GetBytes(Request request)
        {
            // TODO: Re-enable once uncompressed resources are fixed.
            if (/*request.IsGzipAccepted && */Compressed != null)
                return Compressed;

            return Uncompressed;
        }

        /// <summary>
        /// The compressed (gzip) cached resource
        /// </summary>
        /// <value>The compressed.</value>
        internal byte[] Compressed
        {
            get
            {
                if (!WorthWhileCompressing)
                    return uncompressed_response_;
                else
                    return compressed_response_;
            }
            set
            {
                compressed_response_ = value;
            }
        }

        /// <summary>
        /// The _ worth while compressing
        /// </summary>
        private bool _WorthWhileCompressing = true;

        /// <summary>
        /// If false, it was found that the compressed version of the response was
        /// insignificantly smaller, equally large or even larger than the original version.
        /// </summary>
        /// <value><c>true</c> if [worth while compressing]; otherwise, <c>false</c>.</value>
        public bool WorthWhileCompressing
        {
            get
            {
                return _WorthWhileCompressing;
            }
            set
            {
                _WorthWhileCompressing = value;
            }
        }

        /// <summary>
        /// Internal structure with HTTP response information.
        /// </summary>
        unsafe HttpResponseInternal* http_response_struct_ = null;

        /// <summary>
        /// Direct pointer to session data.
        /// </summary>
        unsafe ScSessionStruct* session_;

        /// <summary>
        /// Indicates if this Response is internally constructed from Apps.
        /// </summary>
        Boolean is_internal_response_ = false;

        /// <summary>
        /// Parses internal HTTP response.
        /// </summary>
        [DllImport("schttpparser.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public unsafe extern static UInt32 sc_parse_http_response(
            Byte* response_buf,
            UInt32 response_size_bytes,
            Byte* out_http_response);

        /// <summary>
        /// Initializes a new instance of the <see cref="Response" /> class.
        /// </summary>
        public Response(MixedCodeConstants.NetworkProtocolType protocol_type = MixedCodeConstants.NetworkProtocolType.PROTOCOL_HTTP1)
        {
//            Retrieved = DateTime.Now.Ticks;
            HeaderInjectionPoint = 0;
            protocol_type_ = protocol_type;
        }

//        public long Retrieved { get; set; }

        /// <summary>
        /// Reference to corresponding request.
        /// </summary>
        Request request_ = null;

        /// <summary>
        /// A response may be associated with a request. If a response is created without a content type,
        /// the primary content type of the Accept header in the request will be assumed.
        /// </summary>
        public Request Request {
            get {
                return request_;
            }
            set {
                request_ = value;
            }
        }

        /// <summary>
        /// Underlying memory stream.
        /// </summary>
        MemoryStream mem_stream_ = null;

        /// <summary>
        /// Setting the response buffer.
        /// </summary>
        /// <param name="response_buf"></param>
        /// <param name="response_len_bytes"></param>
        internal void SetResponseBuffer(Byte[] response_buf, MemoryStream mem_stream, Int32 response_len_bytes)
        {
            uncompressed_response_ = response_buf;
			uncompressedResponseLength_ = response_len_bytes;

            mem_stream_ = mem_stream;

            unsafe
            {
                // Checking if have not allocated anything yet.
                if (null != http_response_struct_->socket_data_)
                {
                    // Releasing internal resources here.
                    BitsAndBytes.Free((IntPtr)http_response_struct_->socket_data_);
                    http_response_struct_->socket_data_ = null;
                }

                // Setting the response data pointer.
                http_response_struct_->socket_data_ = (Byte*) BitsAndBytes.Alloc(uncompressedResponseLength_);

                // Copying HTTP response data.
                fixed (Byte* fixed_response_buf = response_buf)
                    BitsAndBytes.MemCpy(http_response_struct_->socket_data_, fixed_response_buf, (UInt32)uncompressedResponseLength_);
            }
        }

        /// <summary>
        /// Parses the HTTP response from uncompressed buffer.
        /// </summary>
        internal void ParseResponseFromUncompressed()
        {
            if (uncompressed_response_ != null)
                TryParseResponse(uncompressed_response_, 0, uncompressedResponseLength_, true);
        }

        /// <summary>
        /// Parses HTTP response from buffer.
        /// </summary>
        /// <param name="buf"></param>
        /// <param name="bufLenBytes"></param>
        /// <param name="complete"></param>
        internal void TryParseResponse(Byte[] buf, Int32 offsetBytes, Int32 bufLenBytes, Boolean complete)
        {
            UInt32 err_code;
            unsafe
            {
                // First destroying.
                Destroy();

                // Indicating that we internally constructing Response.
                is_internal_response_ = true;

                // Allocating space just for response structure.
                http_response_struct_ = (HttpResponseInternal*) BitsAndBytes.Alloc(sizeof(HttpResponseInternal));
                http_response_struct_->socket_data_ = null;

                // Checking if we have a complete response.
                if (complete)
                {
                    // Setting the internal buffer.
                    SetResponseBuffer(buf, null, bufLenBytes);

                    // Executing HTTP response parser and getting Response structure as result.
                    err_code = sc_parse_http_response(http_response_struct_->socket_data_, (UInt32)bufLenBytes, (Byte*)http_response_struct_);
                }
                else
                {
                    fixed (Byte* pbuf = buf)
                    {
                        // Executing HTTP response parser and getting Response structure as result.
                        err_code = sc_parse_http_response(pbuf + offsetBytes, (UInt32)bufLenBytes, (Byte*)http_response_struct_);
                    }
                }

                // Checking if any error occurred.
                if (err_code != 0)
                {
                    // Freeing memory etc.
                    Destroy();

                    // Throwing the concrete error code exception.
                    throw ErrorCode.ToException(err_code);
                }

                UncompressedBodyLength_ = http_response_struct_->content_len_bytes_;
                UncompressedBodyOffset_ = (int)http_response_struct_->content_offset_;

                // NOTE: No internal sessions support.
                session_ = null;

                protocol_type_ = MixedCodeConstants.NetworkProtocolType.PROTOCOL_HTTP1;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Response" /> class.
        /// </summary>
        /// <param name="buf">The buf.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        public Response(Byte[] buf, Int32 offset, Int32 lenBytes, Request httpRequest = null, Boolean complete = true)
        {
            unsafe
            {
                // Parsing given buffer.
                TryParseResponse(buf, offset, lenBytes, complete);

                // Setting corresponding HTTP request.
                request_ = httpRequest;

                if (request_ != null)
                    protocol_type_ = request_.ProtocolType;
            }
        }

        /// <summary>
        /// Destroys the instance of Response.
        /// </summary>
        internal void Destroy()
        {
            unsafe
            {
                // Checking if already destroyed.
                if (http_response_struct_ == null)
                    return;

                // Closing the memory stream if any.
                if (null != mem_stream_)
                {
                    mem_stream_.Close();
                    mem_stream_ = null;
                }

                // Checking if we have constructed this Response
                // internally in Apps or externally in Gateway.
                if (is_internal_response_)
                {
                    // Checking if have not allocated anything yet.
                    if (null != http_response_struct_->socket_data_)
                    {
                        // Releasing response data.
                        BitsAndBytes.Free((IntPtr)http_response_struct_->socket_data_);
                        http_response_struct_->socket_data_ = null;
                    }

                    // Releasing internal resources here.
                    BitsAndBytes.Free((IntPtr)http_response_struct_);
                }

                http_response_struct_ = null;
                session_ = null;
            }
        }

        /// <summary>
        /// Checks if HttpStructs is destroyed already.
        /// </summary>
        /// <returns>True if destroyed.</returns>
        public bool IsDestroyed()
        {
            unsafe
            {
                return (http_response_struct_ == null) && (session_ == null);
            }
        }

        /// <summary>
        /// Releases resources.
        /// </summary>
        ~Response()
        {
            Destroy();
        }

        /// <summary>
        /// Debugs the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="ex">The ex.</param>
        public void Debug(string message, Exception ex = null)
        {
            Console.WriteLine(message);
        }

        /// <summary>
        /// The needs script injection_
        /// </summary>
        bool needsScriptInjection_ = false;

        /// <summary>
        /// Gets or sets a value indicating whether [needs script injection].
        /// </summary>
        /// <value><c>true</c> if [needs script injection]; otherwise, <c>false</c>.</value>
        public bool NeedsScriptInjection
        {
            get { return needsScriptInjection_; }
            set { needsScriptInjection_ = value; }
        }

        /// <summary>
        /// The is app view_
        /// </summary>
        bool isAppView_ = false;
        /// <summary>
        /// Gets or sets a value indicating whether this instance is app view.
        /// </summary>
        /// <value><c>true</c> if this instance is app view; otherwise, <c>false</c>.</value>
        internal bool IsAppView
        {
            get { return isAppView_; }
            set { isAppView_ = value; }
        }

        /// <summary>
        /// The gzip advisable_
        /// </summary>
        bool gzipAdvisable_ = false;

        /// <summary>
        /// Gets or sets the gzip advisable.
        /// </summary>
        /// <value>The gzip advisable.</value>
        public Boolean GzipAdvisable
        {
            get { return gzipAdvisable_; }
            set { gzipAdvisable_ = value; }
        }

        /// <summary>
        /// Gets or sets the view model.
        /// </summary>
        /// <value>The view model.</value>
        public byte[] ViewModel { get; set; }

        /// <summary>
        /// Gets a value indicating whether this instance can use static response.
        /// </summary>
        /// <value><c>true</c> if this instance can use static response; otherwise, <c>false</c>.</value>
        public bool CanUseStaticResponse
        {
            get
            {
                return ViewModel == null;
            }
        }

        /// <summary>
        /// Splits headers string into dictionary.
        /// </summary>
        internal static Dictionary<String, String> CreateHeadersDictionaryFromHeadersString(String headersString)
        {
            Dictionary<String, String> customHeaderFields = new Dictionary<String, String>();

            if (headersString == null)
                return customHeaderFields;

            String[] headersAndValues = headersString.Split(new String[] { StarcounterConstants.NetworkConstants.CRLF }, StringSplitOptions.RemoveEmptyEntries);
            foreach (String headerAndValue in headersAndValues)
            {
                for (Int32 i = 0; i < headerAndValue.Length; i++)
                {
                    if (headerAndValue[i] == ':')
                    {
                        String headerName = headerAndValue.Substring(0, i);
                        Int32 k = i + 1;
                        while (Char.IsWhiteSpace(headerAndValue[k]))
                            k++;

                        customHeaderFields[headerName] = headerAndValue.Substring(k);
                    }
                }
            }

            return customHeaderFields;
        }

        /// <summary>
        /// Headers string.
        /// </summary>
        public String Headers
        {
            get
            {
                EnsureHttpV1IsUsed();

                unsafe
                {
                    // Concatenating headers from dictionary.
                    if (null != customHeaderFields_)
                    {
                        headersString_ = "";

                        foreach (KeyValuePair<string, string> h in customHeaderFields_)
                        {
                            headersString_ += h.Key + ": " + h.Value + StarcounterConstants.NetworkConstants.CRLF;
                        }

                        return headersString_;
                    }

                    if (null == http_response_struct_)
                        throw new ArgumentException("HTTP response not initialized.");
                    
                    headersString_ = http_response_struct_->GetHeadersStringUtf8_Slow();
                    return headersString_;
                }
            }
        }

        /// <summary>
        /// Gets the raw headers length.
        /// </summary>
        /// <param name="ptr">The PTR.</param>
        /// <param name="sizeBytes">The size bytes.</param>
        public UInt32 GetHeadersLength()
        {
            EnsureHttpV1IsUsed();

            unsafe
            {
                if (null == http_response_struct_)
                    throw new ArgumentException("HTTP response not initialized.");

                return http_response_struct_->GetHeadersLength();
            }
        }

        /// <summary>
        /// Gets the content raw pointer.
        /// </summary>
        /// <param name="ptr">The PTR.</param>
        /// <param name="sizeBytes">The size bytes.</param>
        public void GetBodyRaw(out IntPtr ptr, out Int32 sizeBytes)
        {
            unsafe
            {
                if (null == http_response_struct_)
                    throw new ArgumentException("HTTP response not initialized.");

                http_response_struct_->GetBodyRaw(out ptr, out sizeBytes);
            }
        }

        /// <summary>
        /// Gets the content as byte array.
        /// </summary>
        /// <returns>content bytes.</returns>
        Byte[] GetBodyByteArray_Slow()
        {
            // TODO: Provide a more efficient interface with existing Byte[] and offset.

            unsafe
            {
                if (null == http_response_struct_)
                    throw new ArgumentException("HTTP response not initialized.");

                return http_response_struct_->GetBodyByteArray_Slow();
            }
        }

        /// <summary>
        /// Gets the response as byte array.
        /// </summary>
        /// <returns>Response bytes.</returns>
        internal Byte[] ResponseBytes
        {
            get { return uncompressed_response_; }
        }

        /// <summary>
        /// Byte array of the response.
        /// </summary>
        /// <param name="r"></param>
        /// <returns></returns>
        public static implicit operator Byte[](Response r)
        {
            return r.ResponseBytes;
        }

        /// <summary>
        /// Gets body as UTF8 string.
        /// </summary>
        /// <returns>UTF8 string.</returns>
        String GetBodyStringUtf8_Slow()
        {
            unsafe
            {
                if (null == http_response_struct_)
                    throw new ArgumentException("HTTP response not initialized.");

                return http_response_struct_->GetBodyStringUtf8_Slow();
            }
        }

        /// <summary>
        /// Gets body bytes.
        /// </summary>
        Byte[] GetBodyBytes_Slow()
        {
            unsafe
            {
                if (null == http_response_struct_)
                    throw new ArgumentException("HTTP request not initialized.");

                return http_response_struct_->GetBodyByteArray_Slow();
            }
        }

        /// <summary>
        /// Gets headers as UTF8 string.
        /// </summary>
        /// <returns>UTF8 string.</returns>
        String GetHeadersStringUtf8_Slow()
        {
            unsafe
            {
                if (null == http_response_struct_)
                    throw new ArgumentException("HTTP response not initialized.");

                return http_response_struct_->GetHeadersStringUtf8_Slow();
            }
        }

        /// <summary>
        /// Gets the raw response.
        /// </summary>
        /// <param name="ptr">The PTR.</param>
        /// <param name="sizeBytes">The size bytes.</param>
        public void GetResponseRaw(out IntPtr ptr, out UInt32 sizeBytes)
        {
            unsafe
            {
                if (null == http_response_struct_)
                    throw new ArgumentException("HTTP response not initialized.");

                http_response_struct_->GetResponseRaw(out ptr, out sizeBytes);
            }
        }

        /// <summary>
        /// Gets response as UTF8 string.
        /// </summary>
        /// <returns>UTF8 string.</returns>
        internal String GetResponseStringUtf8_Slow()
        {
            unsafe
            {
                if (null == http_response_struct_)
                    throw new ArgumentException("HTTP response not initialized.");

                return http_response_struct_->GetResponseStringUtf8_Slow();
            }
        }

        /// <summary>
        /// Gets the raw headers.
        /// </summary>
        /// <param name="ptr">The PTR.</param>
        /// <param name="sizeBytes">The size bytes.</param>
        public void GetRawHeaders(out IntPtr ptr, out UInt32 sizeBytes)
        {
            EnsureHttpV1IsUsed();

            unsafe
            {
                if (null == http_response_struct_)
                    throw new ArgumentException("HTTP response not initialized.");

                http_response_struct_->GetRawHeaders(out ptr, out sizeBytes);
            }
        }

        /// <summary>
        /// Gets the raw session string.
        /// </summary>
        /// <param name="ptr">The PTR.</param>
        /// <param name="sizeBytes">The size bytes.</param>
        public void GetRawSessionString(out IntPtr ptr, out UInt32 sizeBytes)
        {
            unsafe
            {
                if (null == http_response_struct_)
                    throw new ArgumentException("HTTP response not initialized.");

                http_response_struct_->GetRawSessionString(out ptr, out sizeBytes);
            }
        }

        /// <summary>
        /// Gets the raw header.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="ptr">The PTR.</param>
        /// <param name="sizeBytes">The size bytes.</param>
        public void GetRawHeader(byte[] key, out IntPtr ptr, out UInt32 sizeBytes)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Dictionary of simple user custom headers.
        /// </summary>
        Dictionary<String, String> customHeaderFields_;

        /// <summary>
        /// String containing all headers.
        /// </summary>
        String headersString_;

        /// <summary>
        /// Headers dictionary accessors.
        /// </summary>
        public Dictionary<String, String> HeadersDictionary
        {
            get 
            {
                EnsureHttpV1IsUsed();

                if (null == customHeaderFields_)
                {
                    String headersString = headersString_;
                    if (headersString == null)
                    {
                        unsafe
                        {
                            if (null != http_response_struct_)
                                headersString = http_response_struct_->GetHeadersStringUtf8_Slow();
                        }
                    }

                    customHeaderFields_ = CreateHeadersDictionaryFromHeadersString(headersString);
                }

                return customHeaderFields_;
            }

            set
            {
                EnsureHttpV1IsUsed();

                customHeaderFields_ = value;
            }
        }

        /// <summary>
        /// Gets the <see cref="String" /> with the specified name.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>String.</returns>
        public String this[String name]
        {
            get
            {
                EnsureHttpV1IsUsed();

                if (null != customHeaderFields_)
                {
                    if (customHeaderFields_.ContainsKey(name))
                        return customHeaderFields_[name];

                    return null;
                }

                unsafe
                {
                    if (null == http_response_struct_)
                        return null;

                    return http_response_struct_->GetHeaderValue(name, ref headersString_);
                }
            }

            set
            {
                EnsureHttpV1IsUsed();

                customFields_ = true;

                if (null == customHeaderFields_)
                {
                    String headers = headersString_;
                    if (headers == null)
                    {
                        unsafe
                        {
                            if (null != http_response_struct_)
                                headers = http_response_struct_->GetHeadersStringUtf8_Slow();
                        }
                    }

                    customHeaderFields_ = CreateHeadersDictionaryFromHeadersString(headers);
                }

                customHeaderFields_[name] = value;
            }
        }

        /// <summary>
        /// Checks if HTTP response already has session.
        /// </summary>
        public Boolean HasSession
        {
            get
            {
                unsafe
                {
                    return Request.INVALID_APPS_UNIQUE_SESSION_INDEX != (session_->linear_index_);
                }
            }
        }

        /// <summary>
        /// Returns unique session number.
        /// </summary>
        public UInt64 UniqueSessionIndex
        {
            get
            {
                unsafe
                {
                    return session_->linear_index_;
                }
            }
        }

        /// <summary>
        /// Returns session salt.
        /// </summary>
        public UInt64 SessionSalt
        {
            get
            {
                unsafe
                {
                    return session_->random_salt_;
                }
            }
        }

        /// <summary>
        /// Getting session for apps.
        /// </summary>
        internal ScSessionClass AppsSession { get; set; }

        /// <summary>
        /// Gets the session struct.
        /// </summary>
        /// <value>The session struct.</value>
        public ScSessionStruct SessionStruct
        {
            get
            {
                unsafe { return *session_; }
            }
        }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>A string that represents the current object.</returns>
        public override String ToString()
        {
            unsafe
            {
                if (null == http_response_struct_)
                    throw new ArgumentException("HTTP response not initialized.");

                return http_response_struct_->ToString();
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct HttpResponseInternal
    {
        internal UInt32 response_len_bytes_;
        internal Int32 content_len_bytes_;

        internal UInt16 response_offset_;
        internal UInt16 content_offset_;
        internal UInt16 headers_offset_;
        internal UInt16 headers_len_bytes_;
        internal UInt16 session_string_offset_;
        internal UInt16 status_code_;

        internal Byte session_string_len_bytes_;

        // Socket data pointer.
        public unsafe Byte* socket_data_;

        /// <summary>
        /// Get status description.
        /// </summary>
        public String GetStatusDescription()
        {
            SByte* cur = (SByte*) socket_data_ + response_offset_;
            cur += 12; // Skipping "HTTP/1.1 XXX"

            // Skipping until first space after StatusCode.
            while (*cur != (Byte)' ') cur++;
            cur++;
            SByte* status_descr_start = cur;

            // Skipping until first character return.
            while (*cur != (Byte)'\r') cur++;

            // Calculating length of the status string.
            Int32 len = (Int32)(cur - status_descr_start);

            return new String(status_descr_start, 0, len, Encoding.ASCII);
        }

        /// <summary>
        /// Gets the raw response.
        /// </summary>
        /// <param name="ptr">The PTR.</param>
        /// <param name="sizeBytes">The size bytes.</param>
        public void GetResponseRaw(out IntPtr ptr, out UInt32 sizeBytes)
        {
            ptr = new IntPtr(socket_data_ + response_offset_);

            sizeBytes = response_len_bytes_;
        }

        /// <summary>
        /// Gets the response as UTF8 string.
        /// </summary>
        /// <returns>UTF8 string.</returns>
        internal String GetResponseStringUtf8_Slow()
        {
            return new String((SByte*)(socket_data_ + response_offset_), 0, (Int32)response_len_bytes_, Encoding.UTF8);
        }

        /// <summary>
        /// Gets the raw parameters structure.
        /// </summary>
        /// <param name="ptr">The PTR.</param>
        /// <param name="sizeBytes">The size bytes.</param>
        public IntPtr GetRawParametersInfo()
        {
            return (IntPtr)(socket_data_ + MixedCodeConstants.SOCKET_DATA_OFFSET_PARAMS_INFO);
        }

        /// <summary>
        /// Gets the content raw pointer.
        /// </summary>
        /// <param name="ptr">The PTR.</param>
        /// <param name="sizeBytes">The size bytes.</param>
        public void GetBodyRaw(out IntPtr ptr, out Int32 sizeBytes)
        {
            if (content_len_bytes_ <= 0)
                ptr = IntPtr.Zero;
            else
                ptr = new IntPtr(socket_data_ + content_offset_);

            sizeBytes = content_len_bytes_;
        }

        /// <summary>
        /// Gets the content as byte array.
        /// </summary>
        /// <returns>Content bytes.</returns>
        internal Byte[] GetBodyByteArray_Slow()
        {
            // Checking if there is a content.
            if (content_len_bytes_ <= 0)
                return null;

            // TODO: Provide a more efficient interface with existing Byte[] and offset.

            Byte[] content_bytes = new Byte[content_len_bytes_];
            Marshal.Copy((IntPtr)(socket_data_ + content_offset_), content_bytes, 0, content_len_bytes_);

            return content_bytes;
        }

        /// <summary>
        /// Gets body as UTF8 string.
        /// </summary>
        /// <returns>UTF8 string.</returns>
        internal String GetBodyStringUtf8_Slow()
        {
            // Checking if there is a content.
            if (content_len_bytes_ <= 0)
                return null;

            return new String((SByte*)(socket_data_ + content_offset_), 0, content_len_bytes_, Encoding.UTF8);
        }

        /// <summary>
        /// Gets the raw headers.
        /// </summary>
        /// <param name="ptr">The PTR.</param>
        /// <param name="sizeBytes">The size bytes.</param>
        public void GetRawHeaders(out IntPtr ptr, out UInt32 sizeBytes)
        {
            if (headers_len_bytes_ <= 0)
                ptr = IntPtr.Zero;
            else
                ptr = new IntPtr(socket_data_ + headers_offset_);

            sizeBytes = headers_len_bytes_;
        }

        /// <summary>
        /// Gets headers as ASCII string.
        /// </summary>
        /// <returns>ASCII string.</returns>
        internal String GetHeadersStringUtf8_Slow()
        {
            if (headers_len_bytes_ <= 0)
                return null;

            return new String((SByte*)(socket_data_ + headers_offset_), 0, (Int32)headers_len_bytes_, Encoding.ASCII);
        }

        /// <summary>
        /// Gets the raw headers length.
        /// </summary>
        /// <param name="ptr">The PTR.</param>
        /// <param name="sizeBytes">The size bytes.</param>
        public UInt32 GetHeadersLength()
        {
            return headers_len_bytes_;
        }

        /// <summary>
        /// Gets the raw session string.
        /// </summary>
        /// <param name="ptr">The PTR.</param>
        /// <param name="sizeBytes">The size bytes.</param>
        public void GetRawSessionString(out IntPtr ptr, out UInt32 sizeBytes)
        {
            if (session_string_len_bytes_ <= 0)
                ptr = IntPtr.Zero;
            else
                ptr = new IntPtr(socket_data_ + session_string_offset_);

            sizeBytes = session_string_len_bytes_;
        }

        /// <summary>
        /// Gets the session string.
        /// </summary>
        /// <returns>String.</returns>
        public String GetSessionString()
        {
            // Checking if there is any session.
            if (session_string_len_bytes_ <= 0)
                return null;

            IntPtr raw_session_string;
            UInt32 len_bytes;
            GetRawSessionString(out raw_session_string, out len_bytes);

            return Marshal.PtrToStringAnsi(raw_session_string, (Int32)len_bytes);
        }

        /// <summary>
        /// Gets the header value.
        /// </summary>
        /// <param name="headerName">Name of the header.</param>
        /// <param name="ptr">The PTR.</param>
        /// <param name="sizeBytes">The size bytes.</param>
        public void GetHeaderValue(byte[] headerName, out IntPtr ptr, out UInt32 sizeBytes)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the header value.
        /// </summary>
        /// <param name="headerName">Name of the header.</param>
        /// <param name="headersString">Reference of the header string.</param>
        /// <returns>String.</returns>
        public String GetHeaderValue(String headerName, ref String headersString)
        {
            // Constructing the string if its the first time.
            if (headersString == null)
                headersString = Marshal.PtrToStringAnsi((IntPtr)(socket_data_ + headers_offset_), (Int32)headers_len_bytes_);

            // Getting needed substring.
            Int32 hstart = headersString.IndexOf(headerName);
            if (hstart < 0)
                return null;

            // Skipping header name and colon.
            hstart += headerName.Length + 1;

            // Skipping header name.
            while (headersString[hstart] == ' ' || headersString[hstart] == ':')
                hstart++;

            // Going until end of line.
            Int32 hend = headersString.IndexOf(StarcounterConstants.NetworkConstants.CRLF, hstart);
            if (hend <= 0)
                throw new ArgumentException("HTTP header is corrupted!");

            return headersString.Substring(hstart, hend - hstart);
        }
    }
}
