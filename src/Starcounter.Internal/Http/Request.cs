
using Starcounter.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;

namespace Starcounter {
    /// <summary>
    /// Class Request
    /// </summary>
    public sealed class Request {
        /// <summary>
        /// Creates a minimalistic Http 1.0 GET request with the given uri without any headers or even protocol version specifier.
        /// </summary>
        /// <remarks>
        /// Calling RawGET("/test") will return the Http request "GET /test" in Ascii/UTF8 encoding.
        /// </remarks>
        /// <param name="uri">The URI.</param>
        /// <returns>System.Object.</returns>
        internal static byte[] RawGET(string uri) {
            var length = uri.Length + 3 + 1 + 4;// GET + space + URI + CRLFCRLF
            byte[] vu = new byte[length];
            vu[0] = (byte)'G';
            vu[1] = (byte)'E';
            vu[2] = (byte)'T';
            vu[3] = (byte)' ';
            vu[length - 4] = (byte)'\r';
            vu[length - 3] = (byte)'\n';
            vu[length - 2] = (byte)'\r';
            vu[length - 1] = (byte)'\n';
            Encoding.ASCII.GetBytes(uri, 0, uri.Length, vu, 4);
            return vu;
        }

        public static Request GET(string uri) {
            Byte[] reqBytes = RawGET(uri);

            return new Request(reqBytes, reqBytes.Length);
        }

        /// <summary>
        /// Request constructor.
        /// </summary>
        /// <param name="protocol_type">Type of network protocol. Default HTTP v1.</param>
        public Request(MixedCodeConstants.NetworkProtocolType protocol_type = MixedCodeConstants.NetworkProtocolType.PROTOCOL_HTTP1)
        {
            protocol_type_ = protocol_type;
        }

        /// <summary>
        /// Internal structure with HTTP request information.
        /// </summary>
        unsafe HttpRequestInternal* http_request_struct_;

        /// <summary>
        /// Direct pointer to session data.
        /// </summary>
        unsafe ScSessionStruct* session_;

        /// <summary>
        /// Internal network data stream.
        /// </summary>
        NetworkDataStream data_stream_;

        /// <summary>
        /// Network port number.
        /// </summary>
        UInt16 port_number_ = 0;

        /// <summary>
        /// Network port number.
        /// </summary>
        public UInt16 PortNumber
        {
            get { return port_number_; }
            set { port_number_ = value; }
        }

        /// Returns the single most preferred mime type according to the Accept header of the request amongst a 
        /// set of common mime types. If the mime type is not in the enum of known common mime types, the
        /// value MimeType.Other is returned. If there is no Accept header or if the Accept header is empty,
        /// the value MimeType.Unspecified is returned.
        /// <remarks>
        /// TODO! Implement proper fast method! Include all mime types in xml file and speed up using
        /// similar code generation as the URI matcher.
        /// </remarks>
        public MimeType PreferredMimeType {
            get {
                EnsureHttpV1IsUsed();

                var a = this[HttpHeadersUtf8.GetAcceptHeader];
                if (a != null) {
                    a = a.ToUpper();
                    if (a.StartsWith("APPLICATION/JSON-PATCH+JSON")) {
                        return MimeType.Application_JsonPatch__Json;
                    }
                    else if (a.StartsWith("TEXT/HTML")) {
                        return MimeType.Text_Html;
                    }
                    else if (a.StartsWith("APPLICATION/JSON")) {
                        return MimeType.Application_Json;
                    }
                    else if (a.StartsWith("TEXT/PLAIN")) {
                        return MimeType.Text_Plain;
                    }
                    else if (a.StartsWith("*/*")) {
                        return MimeType.Unspecified;
                    }
                    return MimeType.Other;
                }
                return MimeType.Unspecified;
            }
        }

        /// <summary>
        /// Returns a list of requested mime types in preference order as discovered in the Accept header
        /// of the request.
        /// </summary>
        /// <remarks>
        /// TODO! Implement! Does currently only return a single mime type and supports only a few mime types.
        /// </remarks>
        public IEnumerator<MimeType> PreferredMimeTypes {
            get {
                EnsureHttpV1IsUsed();

                var l = new List<MimeType>();
                l.Add( PreferredMimeType );
                return l.GetEnumerator();
            }
        }

        /// <summary>
        /// Indicates if this Request is internally constructed from Apps.
        /// </summary>
        Boolean is_internal_request_ = false;

        /// <summary>
        /// Returns True if request is internal.
        /// </summary>
        internal Boolean IsInternal
        {
            get { return is_internal_request_; }
        }

        /// <summary>
        /// Just using Request as holder for user Message instance type.
        /// </summary>
        Type message_object_type_ = null;

        // Type of network protocol.
        MixedCodeConstants.NetworkProtocolType protocol_type_;

        /// <summary>
        /// Returns protocol type.
        /// </summary>
        public MixedCodeConstants.NetworkProtocolType ProtocolType
        {
            get { return protocol_type_; }
        }

        /// <summary>
        /// Setting message object type.
        /// </summary>
        internal Type ArgMessageObjectType
        {
            get { return message_object_type_; }
            set { message_object_type_ = value; }
        }

        /// <summary>
        /// Accessors to HTTP method.
        /// </summary>
        public HTTP_METHODS MethodEnum { get; set; }

        Response response_;

        /// <summary>
        /// Set or get the Response object attached to this request.
        /// Used to declare response object that should be returned to the original request.
        /// </summary>
        public Response Response
        {
            get { return response_; }
            set
            {
                response_ = value;
                response_.Request = this;
            }
        }

        /// <summary>
        /// Returns True if method is idempotent.
        /// </summary>
        public Boolean IsIdempotent()
        {
            switch (MethodEnum)
            {
                case HTTP_METHODS.GET:
                case HTTP_METHODS.PUT:
                case HTTP_METHODS.DELETE:
                case HTTP_METHODS.HEAD:
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Parses internal HTTP request.
        /// </summary>
        [DllImport("schttpparser.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public unsafe extern static UInt32 sc_parse_http_request(
            Byte* request_buf,
            UInt32 request_size_bytes,
            Byte* out_http_request);

        /// <summary>
        /// Initializes the Apps HTTP parser.
        /// </summary>
        [DllImport("schttpparser.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public extern static UInt32 sc_init_http_parser();

        /// <summary>
        /// Initializes a new instance of the <see cref="Request" /> class.
        /// </summary>
        /// <param name="buf">The buf.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        public Request(Byte[] buf, Int32 buf_len)
        {
            unsafe
            {
                InternalInit(buf, buf_len, null);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Request" /> class.
        /// </summary>
        /// <param name="buf"></param>
        /// <param name="params_info"></param>
        public unsafe Request(Byte[] buf, Int32 buf_len, Byte* params_info_ptr)
        {
            InternalInit(buf, buf_len, params_info_ptr);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Request" /> class.
        /// </summary>
        /// <param name="chunk_data">The chunk_data.</param>
        /// <param name="single_chunk">The single_chunk.</param>
        /// <param name="chunk_index">The chunk_index.</param>
        /// <param name="handler_id">The handler id.</param>
        /// <param name="http_request_begin">The http_request_begin.</param>
        /// <param name="socket_data">The socket_data.</param>
        /// <param name="data_stream">The data_stream.</param>
        /// <param name="protocol_type">Type of network protocol.</param>
        internal unsafe Request(
            Byte* chunk_data,
            Boolean single_chunk,
            UInt32 chunk_index,
            UInt16 handler_id,
            Byte* http_request_begin,
            Byte* socket_data,
            NetworkDataStream data_stream,
            MixedCodeConstants.NetworkProtocolType protocol_type)
        {
            http_request_struct_ = (HttpRequestInternal*)http_request_begin;
            session_ = (ScSessionStruct*)(socket_data + MixedCodeConstants.SOCKET_DATA_OFFSET_SESSION);
            http_request_struct_->socket_data_ = socket_data;
            data_stream_ = data_stream;
            handler_id_ = handler_id;
            protocol_type_ = protocol_type;
        }

        /// <summary>
        /// Initializes request structure.
        /// </summary>
        /// <param name="buf"></param>
        unsafe void InternalInit(Byte[] buf, Int32 buf_len, Byte* params_info_ptr)
        {
            unsafe
            {
                // Indicating that request is internal.
                is_internal_request_ = true;

                // Allocating space for Request contents and structure.
                Int32 alloc_size = buf_len + sizeof(HttpRequestInternal);
                if (params_info_ptr != null)
                    alloc_size += MixedCodeConstants.PARAMS_INFO_MAX_SIZE_BYTES;

                Byte* request_native_buf = (Byte*) BitsAndBytes.Alloc(alloc_size);
                fixed (Byte* fixed_buf = buf)
                {
                    // Copying HTTP request data.
                    BitsAndBytes.MemCpy(request_native_buf, fixed_buf, (UInt32)buf_len);
                }

                // Pointing to HTTP request structure.
                http_request_struct_ = (HttpRequestInternal*)(request_native_buf + buf_len);

                // Setting the request data pointer.
                http_request_struct_->socket_data_ = request_native_buf;

                // Checking if we have parameters info supplied.
                if (params_info_ptr != null)
                {
                    http_request_struct_->params_info_ptr_ = request_native_buf + buf_len + sizeof(HttpRequestInternal);
                    BitsAndBytes.MemCpy(http_request_struct_->params_info_ptr_, params_info_ptr, MixedCodeConstants.PARAMS_INFO_MAX_SIZE_BYTES);
                }
                
                // Indicating that we internally constructing Request.
                protocol_type_ = MixedCodeConstants.NetworkProtocolType.PROTOCOL_HTTP1;

                // NOTE: No internal sessions support.
                session_ = null;

                // NOTE: No internal data stream support:
                // Simply on which socket to send this "request"?

                // Executing HTTP request parser and getting Request structure as result.
                UInt32 err_code = sc_parse_http_request(request_native_buf, (UInt32)buf_len, (Byte*)http_request_struct_);

                // Checking if any error occurred.
                if (err_code != 0)
                {
                    // Freeing memory etc.
                    Destroy();

                    throw ErrorCode.ToException(err_code);
                }
            }
        }

        /// <summary>
        /// Destroys the instance of Request.
        /// </summary>
        internal void Destroy(Boolean isStarcounterThread = true)
        {
            unsafe
            {
                // Checking if already destroyed.
                if (http_request_struct_ == null)
                    return;

                // Checking if we have constructed this Request
                // internally in Apps or externally in Gateway.
                if (is_internal_request_)
                {
                    // Releasing internal resources here.
                    // NOTE: Socket_data_ points to complete memory including http_request_struct_,
                    // so freeing it frees everything.
                    BitsAndBytes.Free((IntPtr)http_request_struct_->socket_data_);
                }
                else
                {
                    // Releasing data stream resources like chunks, etc.
                    data_stream_.Destroy(isStarcounterThread);
                }

                http_request_struct_ = null;
                session_ = null;
            }
        }

        /// <summary>
        /// Checks if request is destroyed already.
        /// </summary>
        /// <returns>True if destroyed.</returns>
        internal bool IsDestroyed()
        {
            unsafe
            {
                return (http_request_struct_ == null) && (session_ == null);
            }
        }
        
        /// <summary>
        /// Releases resources.
        /// </summary>
        ~Request()
        {
            // Not on Starcounter thread.
            Destroy(false);
        }

        // TODO
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
        /// Linear index for this handler.
        /// </summary>
        UInt16 handler_id_;
        internal UInt16 HandlerId
        {
            get { return handler_id_; }
            set { handler_id_ = value; } 
        }

        /// <summary>
        /// Gets the raw parameters structure.
        /// </summary>
        /// <param name="ptr">The PTR.</param>
        /// <param name="sizeBytes">The size bytes.</param>
        public IntPtr GetRawParametersInfo()
        {
            unsafe
            {
                if (null == http_request_struct_)
                    throw new ArgumentException("HTTP request not initialized.");

                if (!is_internal_request_)
                    return (IntPtr)(http_request_struct_->socket_data_ + MixedCodeConstants.SOCKET_DATA_OFFSET_PARAMS_INFO);

                return (IntPtr)http_request_struct_->params_info_ptr_;
            }
        }

        /// <summary>
        /// Gets the raw method and URI.
        /// </summary>
        /// <param name="ptr">The PTR.</param>
        /// <param name="sizeBytes">The size bytes.</param>
        public IntPtr GetRawMethodAndUri()
        {
            EnsureHttpV1IsUsed();

            unsafe
            {
                if (null == http_request_struct_)
                    throw new ArgumentException("HTTP request not initialized.");

                return http_request_struct_->GetRawMethodAndUri();
            }
        }

        /*
        /// <summary>
        /// The is app view_
        /// </summary>
        bool is_app_view_ = false;

        /// <summary>
        /// Gets or sets a value indicating whether this instance is app view.
        /// </summary>
        /// <value><c>true</c> if this instance is app view; otherwise, <c>false</c>.</value>
        public bool IsAppView 
        {
            get { return is_app_view_; }
            set { is_app_view_ = value; }
        }
         */

        /// <summary>
        /// The gzip advisable_
        /// </summary>
        bool gzip_advisable_ = false;

        /// <summary>
        /// Gets or sets the gzip advisable.
        /// </summary>
        /// <value>The gzip advisable.</value>
        public Boolean GzipAdvisable 
        {
            get { return gzip_advisable_; }
            set { gzip_advisable_ = value; }
        }

        /// <summary>
        /// Gets or sets the view model.
        /// </summary>
        /// <value>The view model.</value>
        public byte[] ViewModel { get; set; }

        /// <summary>
        /// Indicates if user wants to send custom request.
        /// </summary>
        Boolean customFields_;

        /// <summary>
        /// Bytes that represent custom request.
        /// </summary>
        Byte[] customBytes_;

        /// <summary>
        /// Length in bytes for custom bytes array.
        /// </summary>
        Int32 customBytesLen_;

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
                if (null == bodyString_)
                {
                    if (null != bodyBytes_)
                    {
                        bodyString_ = Encoding.UTF8.GetString(bodyBytes_);
                        return bodyString_;
                    }
                }
                else
                {
                    return bodyString_;
                }

                unsafe
                {
                    if (null == http_request_struct_)
                        throw new ArgumentException("HTTP request not initialized.");

                    return http_request_struct_->GetBodyStringUtf8_Slow();
                }
            }

            set
            {
                customFields_ = true;
                bodyString_ = value;
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
                if (null == bodyBytes_)
                {
                    if (bodyString_ != null)
                        bodyBytes_ = Encoding.UTF8.GetBytes(bodyString_);
                    else
                        bodyBytes_ = GetBodyBytes_Slow();
                }

                return bodyBytes_;
            }

            set
            {
                customFields_ = true;
                bodyBytes_ = value;
            }
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

                    if (null == http_request_struct_)
                        throw new ArgumentException("HTTP request not initialized.");

                    headersString_ = http_request_struct_->GetHeadersStringUtf8_Slow();

                    return headersString_;
                }
            }
        }

        /// <summary>
        /// Cookie string.
        /// </summary>
        public String Cookie
        {
            get
            {
                EnsureHttpV1IsUsed();

                return this[HttpHeadersUtf8.GetCookieHeader];
            }

            set
            {
                EnsureHttpV1IsUsed();

                customFields_ = true;
                this[HttpHeadersUtf8.GetCookieHeader] = value;
            }
        }

        String methodString_;

        /// <summary>
        /// Method string.
        /// </summary>
        public String Method
        {
            get
            {
                EnsureHttpV1IsUsed();

                if (null == methodString_)
                    methodString_ = MethodEnum.ToString();

                return methodString_;
            }

            set
            {
                EnsureHttpV1IsUsed();

                customFields_ = true;
                methodString_ = value;
            }
        }

        String uriString_;

        /// <summary>
        /// Uri string.
        /// </summary>
        public String Uri
        {
            get
            {
                EnsureHttpV1IsUsed();

                if (null == uriString_)
                {
                    unsafe
                    {
                        if (null == http_request_struct_)
                            throw new ArgumentException("HTTP request not initialized.");

                        uriString_ = http_request_struct_->Uri;
                    }
                }

                return uriString_;
            }

            set
            {
                EnsureHttpV1IsUsed();

                customFields_ = true;
                uriString_ = value;
            }
        }

        String hostNameString_;

        /// <summary>
        /// HostName string.
        /// </summary>
        public String HostName
        {
            get
            {
                EnsureHttpV1IsUsed();

                return hostNameString_;
            }

            set
            {
                EnsureHttpV1IsUsed();

                customFields_ = true;
                hostNameString_ = value;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        internal Int32 CustomBytesLength
        {
            get { return customBytesLen_; }
        }

		/// <summary>
		/// 
		/// </summary>
		internal byte[] CustomBytes {
			get { return customBytes_; }
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

            headersString_ = null;
            bodyString_ = null;
            bodyBytes_ = null;
            hostNameString_ = null;
            uriString_ = null;
            methodString_ = null;
        }

		private int EstimateNeededSize() {
			int size = HttpHeadersUtf8.TotalByteSize;

			size += methodString_.Length;
			size += uriString_.Length;
			size += hostNameString_.Length;
			
			if (null != headersString_)
				size += headersString_.Length;

            if (null != customHeaderFields_) {
                foreach (KeyValuePair<string, string> h in customHeaderFields_) {
                    size += (h.Key.Length + h.Value.Length + 4);
                }
            }

			if (null != bodyString_)
				size += bodyString_.Length << 1;
			else if (null != bodyBytes_) 
				size += bodyBytes_.Length;

			return size;
		}

		/// <summary>
		/// Constructs Response from fields that are set.
		/// </summary>
		public void ConstructFromFields() {
			// Checking if we have a custom response.
			if (!customFields_)
				return;

			if (null == uriString_)
				throw new ArgumentException("Relative URI should be set when creating custom Request.");

			if (null == hostNameString_)
				throw new ArgumentException("Host name should be set when creating custom Request.");

			if (null == methodString_)
				methodString_ = "GET";

            Utf8Writer writer;

			byte[] buf = new byte[EstimateNeededSize()];
			unsafe {
				fixed (byte* p = buf) {
					writer = new Utf8Writer(p);

					writer.Write(methodString_);
					writer.Write(' ');
					writer.Write(uriString_);
					writer.Write(" HTTP/1.1"); // TODO: Change to static bytearray header.
					writer.Write(HttpHeadersUtf8.CRLF);

					writer.Write("Host: "); // TODO: Change to static bytearray header.
					writer.Write(hostNameString_);
					writer.Write(HttpHeadersUtf8.CRLF);

					if (null != headersString_)
                    {
						writer.Write(headersString_);
                    }
                    else
                    {
                        if (null != customHeaderFields_)
                        {
                            foreach (KeyValuePair<string, string> h in customHeaderFields_)
                            {
                                writer.Write(h.Key);
                                writer.Write(": ");
                                writer.Write(h.Value);
                                writer.Write(HttpHeadersUtf8.CRLF);
                            }
                        }
                    }

					if (null != bodyString_) {
						writer.Write(HttpHeadersUtf8.ContentLengthStart);
						writer.Write(writer.GetByteCount(bodyString_));
						writer.Write(HttpHeadersUtf8.CRLFCRLF);
						writer.Write(bodyString_);
					} else if (null != bodyBytes_) {
						writer.Write(HttpHeadersUtf8.ContentLengthStart);
						writer.Write(bodyBytes_.Length);
						writer.Write(HttpHeadersUtf8.CRLFCRLF);
						writer.Write(bodyBytes_);
					} else {
						writer.Write(HttpHeadersUtf8.ContentLengthStart);
						writer.Write('0');
						writer.Write(HttpHeadersUtf8.CRLFCRLF);
					}

					// Finally setting the request bytes.
					customBytes_ = buf;
                    customBytesLen_ = writer.Written;
				}
			}

			customFields_ = false;
		}

        /// <summary>
        /// Gets a value indicating whether this instance can use static response.
        /// </summary>
        /// <value><c>true</c> if this instance can use static response; otherwise, <c>false</c>.</value>
        public bool CanUseStaticResponse 
        {
            get { return ViewModel == null; }
        }

        /// <summary>
        /// Gets the content raw pointer.
        /// </summary>
        /// <param name="ptr">The PTR.</param>
        /// <param name="sizeBytes">The size bytes.</param>
        public void GetBodyRaw(out IntPtr ptr, out UInt32 sizeBytes)
        {
            unsafe
            {
                if (null == http_request_struct_)
                    throw new ArgumentException("HTTP request not initialized.");
                
                http_request_struct_->GetBodyRaw(out ptr, out sizeBytes);
            }
        }

        /// <summary>
        /// Gets the content as byte array.
        /// </summary>
        /// <returns></returns>
        Byte[] GetBodyByteArray_Slow()
        {
            // TODO: Provide a more efficient interface with existing Byte[] and offset.

            unsafe
            {
                if (null == http_request_struct_)
                    throw new ArgumentException("HTTP request not initialized.");

                return http_request_struct_->GetBodyByteArray_Slow();
            }
        }

        /// <summary>
        /// Gets the request as byte array.
        /// </summary>
        /// <returns>Request bytes.</returns>
        Byte[] GetRequestByteArray_Slow()
        {
            // TODO: Provide a more efficient interface with existing Byte[] and offset.

            unsafe
            {
                if (null == http_request_struct_)
                    throw new ArgumentException("HTTP request not initialized.");

                return http_request_struct_->GetRequestByteArray_Slow();
            }
        }

        /// <summary>
        /// Gets the client IP address.
        /// </summary>
        public IPAddress GetClientIpAddress()
        {
            unsafe
            {
                if (null == http_request_struct_)
                    throw new ArgumentException("HTTP request not initialized.");

                if (!is_internal_request_)
                    return new IPAddress(*(Int64*)(http_request_struct_->socket_data_ + MixedCodeConstants.SOCKET_DATA_OFFSET_CLIENT_IP));

                return IPAddress.Loopback;
            }
        }

        /// <summary>
        /// Gets the whole request size.
        /// </summary>
        public UInt32 GetRequestLength()
        {
            unsafe
            {
                if (null == http_request_struct_)
                    throw new ArgumentException("HTTP request not initialized.");

                return http_request_struct_->request_len_bytes_;
            }
        }

        /// <summary>
        /// Byte array of the request.
        /// </summary>
        /// <param name="r"></param>
        /// <returns></returns>
        public static implicit operator Byte[](Request r)
        {
            return r.GetRequestByteArray_Slow();
        }

        /// <summary>
        /// Gets body bytes.
        /// </summary>
        Byte[] GetBodyBytes_Slow()
        {
            unsafe
            {
                if (null == http_request_struct_)
                    throw new ArgumentException("HTTP request not initialized.");

                return http_request_struct_->GetBodyByteArray_Slow();
            }
        }

        /// <summary>
        /// Gets the length of the content in bytes.
        /// </summary>
        /// <value>The length of the content.</value>
        public UInt32 ContentLength
        {
            get
            {
                unsafe
                {
                    if (null == http_request_struct_)
                        throw new ArgumentException("HTTP request not initialized.");

                    return http_request_struct_->content_len_bytes_;
                }
            }
        }

        /// <summary>
        /// Sends the response.
        /// </summary>
        /// <param name="buffer">The buffer to send.</param>
        /// <param name="offset">The offset within buffer.</param>
        /// <param name="length">The length of the data to send.</param>
        /// <param name="connFlags">The connection flags.</param>
        public void SendResponse(Byte[] buffer, Int32 offset, Int32 length, Response.ConnectionFlags connFlags)
        {
            unsafe { data_stream_.SendResponse(buffer, offset, length, connFlags); }
        }

        /// <summary>
        /// Sends response on this request.
        /// </summary>
        /// <param name="resp">Response object to send.</param>
        public void SendResponse(Response resp)
        {
            resp.ConstructFromFields();
            SendResponse(resp.Uncompressed, 0, resp.UncompressedLength, resp.ConnFlags);
        }

        /// <summary>
        /// Gets the raw request.
        /// </summary>
        /// <param name="ptr">The PTR.</param>
        /// <param name="sizeBytes">The size bytes.</param>
        public void GetRequestRaw(out IntPtr ptr, out UInt32 sizeBytes) 
        {
            unsafe
            {
                if (null == http_request_struct_)
                    throw new ArgumentException("HTTP request not initialized.");

                http_request_struct_->GetRequestRaw(out ptr, out sizeBytes);
            }
        }

        /// <summary>
        /// Gets request as UTF8 string.
        /// </summary>
        /// <returns>UTF8 string.</returns>
        String GetRequestStringUtf8_Slow() 
        {
            unsafe
            {
                if (null == http_request_struct_)
                    throw new ArgumentException("HTTP request not initialized.");

                return http_request_struct_->GetRequestStringUtf8_Slow();
            }
        }

        /// <summary>
        /// Gets the raw method and URI.
        /// </summary>
        /// <param name="ptr">The PTR.</param>
        /// <param name="sizeBytes">The size bytes.</param>
        public void GetRawMethodAndUri(out IntPtr ptr, out UInt32 sizeBytes) 
        {
            EnsureHttpV1IsUsed();

            unsafe
            {
                if (null == http_request_struct_)
                    throw new ArgumentException("HTTP request not initialized.");

                http_request_struct_->GetRawMethodAndUri(out ptr, out sizeBytes);
            }
        }

        /// <summary>
        /// Gets the raw method and URI plus extra character.
        /// </summary>
        /// <param name="ptr">The PTR.</param>
        /// <param name="sizeBytes">The size bytes.</param>
        public void GetRawMethodAndUriPlusAnExtraCharacter(out IntPtr ptr, out UInt32 sizeBytes) 
        {
            EnsureHttpV1IsUsed();

            unsafe
            {
                if (null == http_request_struct_)
                    throw new ArgumentException("HTTP request not initialized.");

                http_request_struct_->GetRawMethodAndUri(out ptr, out sizeBytes);
            }

            sizeBytes += 1;
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
                if (null == http_request_struct_)
                    throw new ArgumentException("HTTP request not initialized.");

                http_request_struct_->GetRawHeaders(out ptr, out sizeBytes);
            }
        }

        /// <summary>
        /// Gets the raw session string.
        /// </summary>
        /// <param name="ptr">The PTR.</param>
        /// <param name="sizeBytes">The size bytes.</param>
        public void GetRawSessionString(out IntPtr ptr) 
        {
            unsafe
            {
                if (null == http_request_struct_)
                    throw new ArgumentException("HTTP request not initialized.");

                http_request_struct_->GetRawSessionString(out ptr);
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
            EnsureHttpV1IsUsed();

            unsafe
            {
                if (null == http_request_struct_)
                    throw new ArgumentException("HTTP request not initialized.");

                http_request_struct_->GetHeaderValue(key, out ptr, out sizeBytes);
            }
        }

        /// <summary>
        /// String containing all headers.
        /// </summary>
        String headersString_;

        /// <summary>
        /// Dictionary of simple user custom headers.
        /// </summary>
        Dictionary<String, String> customHeaderFields_;

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
                            if (null != http_request_struct_)
                                headersString = http_request_struct_->GetHeadersStringUtf8_Slow();
                        }
                    }

                    customHeaderFields_ = Response.CreateHeadersDictionaryFromHeadersString(headersString);
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
                    if (null == http_request_struct_)
                        return null;

                    return http_request_struct_->GetHeaderValue(name, ref headersString_);
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
                            if (null != http_request_struct_)
                                headers = http_request_struct_->GetHeadersStringUtf8_Slow();
                        }
                    }

                    customHeaderFields_ = Response.CreateHeadersDictionaryFromHeadersString(headers);
                }

                customHeaderFields_[name] = value;
            }
        }

        /// <summary>
        /// Invalid session index.
        /// </summary>
        public const UInt32 INVALID_GW_SESSION_INDEX = UInt32.MaxValue;

        /// <summary>
        /// Invalid Apps session unique number.
        /// </summary>
        public const UInt32 INVALID_APPS_UNIQUE_SESSION_INDEX = UInt32.MaxValue;

        /// <summary>
        /// Invalid Apps session salt.
        /// </summary>
        public const UInt64 INVALID_APPS_SESSION_SALT = 0;

        /// <summary>
        /// Invalid Apps session index.
        /// </summary>
        public const UInt32 INVALID_APPS_SESSION_INDEX = UInt32.MaxValue;

        /// <summary>
        /// Invalid View model index.
        /// </summary>
        public const UInt32 INVALID_VIEW_MODEL_INDEX = UInt32.MaxValue;

#if DEBUG

        /// <summary>
        /// Used for forcing no session.
        /// </summary>
        Boolean forceNoSession = false;

#endif

        /// <summary>
        /// Checks if HTTP request already has session.
        /// </summary>
        public Boolean HasSession 
        {
            get
            {
#if DEBUG
                // First checking if no session is enforced.
                if (forceNoSession)
                    return false;
#endif

                unsafe
                {
                    if (session_ != null)
                        return INVALID_APPS_UNIQUE_SESSION_INDEX != (session_->linear_index_);

                    return false;
                }
            }
        }

        /// <summary>
        /// Gets certain Apps session.
        /// </summary>
        public IAppsSession GetAppsSessionInterface()
        {
            unsafe 
            {
                // Obtaining corresponding Apps session.
                IAppsSession apps_session =
                    GlobalSessions.AllGlobalSessions.GetAppsSessionInterface(ref *session_);

                // Destroying the session if Apps session was destroyed.
                if (apps_session == null) {

#if DEBUG
                    // Checking if we have a session predefined.
                    if (IsSessionPredefined()) {
                        forceNoSession = true;
                    } else {
                        session_->Destroy();
                    }

#else
                    session_->Destroy();
#endif

                }

                return apps_session;
            }
        }

        /// <summary>
        /// Generates new session.
        /// </summary>
        internal UInt32 GenerateNewSession(IAppsSession apps_session)
        {
            unsafe
            {
                // Simply generating new session.
                return GlobalSessions.AllGlobalSessions.CreateNewSession(
                    ref *session_,
                    apps_session);
            }
        }

#if DEBUG
        /// <summary>
        /// Generates forced session.
        /// </summary>
        internal UInt32 GenerateForcedSession(IAppsSession apps_session)
        {
            unsafe
            {
                // Simply generating new session.
                return GlobalSessions.AllGlobalSessions.CreateForcedSession(
                    ref *session_,
                    apps_session);
            }
        }
#endif

        /// <summary>
        /// Update session details.
        /// </summary>
        internal void UpdateSessionDetails()
        {
            // Don't do anything on internal requests.
            if (is_internal_request_)
                return;

            unsafe
            {
                // Fetching session information.
                ScSessionClass s = GlobalSessions.AllGlobalSessions.GetSessionClass(
                    session_->scheduler_id_,
                    session_->linear_index_,
                    session_->random_salt_);

                // Checking that session exists.
                if (null != s)
                {
                    s.socket_index_num_ = *(UInt32*)(http_request_struct_->socket_data_ + MixedCodeConstants.SOCKET_DATA_OFFSET_SOCKET_INDEX_NUMBER);
                    s.socket_unique_id_ = *(UInt64*)(http_request_struct_->socket_data_ + MixedCodeConstants.SOCKET_DATA_OFFSET_SOCKET_UNIQUE_ID);
                }
            }
        }

        /// <summary>
        /// Attaches existing session on this HTTP request.
        /// </summary>
        /// <param name="apps_session"></param>
        public void AttachSession(IAppsSession apps_session)
        {
            unsafe
            {
                *session_ = apps_session.InternalSession.session_struct_;
            }
        }

        /// <summary>
        /// Kills existing session.
        /// </summary>
        public UInt32 DestroySession() 
        {
            UInt32 err_code;

            unsafe 
            {
                // Simply generating new session.
                err_code = GlobalSessions.AllGlobalSessions.DestroySession(
                    session_->scheduler_id_,
                    session_->linear_index_,
                    session_->random_salt_);

                // Killing this session by setting invalid unique number and salt.
                session_->Destroy();
            }

            return err_code;
        }

        /// <summary>
        /// Returns unique session number.
        /// </summary>
        public UInt64 UniqueSessionIndex 
        {
            get 
            {
                unsafe { return session_->linear_index_; }
            }
        }

        /// <summary>
        /// Returns session salt.
        /// </summary>
        public UInt64 SessionSalt
        {
            get
            {
                unsafe { return session_->random_salt_; }
            }
        }

#if DEBUG
        /// <summary>
        /// Returns true if its a predefined session.
        /// </summary>
        /// <returns></returns>
        internal Boolean IsSessionPredefined() {
            unsafe { return 1 == session_->random_salt_; }
        }
#endif

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
        /// Gets the is gzip accepted.
        /// </summary>
        /// <value>The is gzip accepted.</value>
        public Boolean IsGzipAccepted
        {
            get
            {
                EnsureHttpV1IsUsed();

                unsafe
                {
                    if (null == http_request_struct_)
                        throw new ArgumentException("HTTP request not initialized.");

                    return (http_request_struct_->gzip_accepted_ != 0);
                }
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
                if (null == http_request_struct_)
                    throw new ArgumentException("HTTP request not initialized.");

                return http_request_struct_->ToString();
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct HttpRequestInternal {

        internal UInt32 request_len_bytes_;
        internal UInt32 content_len_bytes_;

        internal UInt16 request_offset_;
        internal UInt16 content_offset_;
        internal UInt16 uri_len_bytes_;
        internal UInt16 headers_offset_;
        internal UInt16 headers_len_bytes_;
        internal UInt16 session_string_offset_;
        internal UInt16 uri_offset_;

        internal Byte http_method_;
        internal Byte gzip_accepted_;

        // Socket data pointer.
        public unsafe Byte* socket_data_;

        // Pointer to parameters structure.
        public unsafe Byte* params_info_ptr_;

        /// <summary>
        /// Gets the raw request.
        /// </summary>
        /// <param name="ptr">The PTR.</param>
        /// <param name="sizeBytes">The size bytes.</param>
        public void GetRequestRaw(out IntPtr ptr, out UInt32 sizeBytes) {
            ptr = new IntPtr(socket_data_ + request_offset_);

            sizeBytes = request_len_bytes_;
        }

        /// <summary>
        /// Gets the content raw pointer.
        /// </summary>
        /// <param name="ptr">The PTR.</param>
        /// <param name="sizeBytes">The size bytes.</param>
        public void GetBodyRaw(out IntPtr ptr, out UInt32 sizeBytes) {
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

            Byte[] content_bytes = new Byte[(Int32)content_len_bytes_];
            Marshal.Copy((IntPtr)(socket_data_ + content_offset_), content_bytes, 0, (Int32)content_len_bytes_);

            return content_bytes;
        }

        /// <summary>
        /// Gets the request as byte array.
        /// </summary>
        /// <returns>Request bytes.</returns>
        internal Byte[] GetRequestByteArray_Slow()
        {
            // Checking if there is a request.
            if (request_len_bytes_ <= 0)
                return null;

            // TODO: Provide a more efficient interface with existing Byte[] and offset.

            Byte[] request_bytes = new Byte[(Int32)request_len_bytes_];
            Marshal.Copy((IntPtr)(socket_data_ + request_offset_), request_bytes, 0, (Int32)request_len_bytes_);

            return request_bytes;
        }

        /// <summary>
        /// Gets the request as UTF8 string.
        /// </summary>
        /// <returns>Request string.</returns>
        internal String GetRequestStringUtf8_Slow()
        {
            return new String((SByte*)(socket_data_ + request_offset_), 0, (Int32)request_len_bytes_, Encoding.UTF8);
        }

        /// <summary>
        /// Gets body as UTF8 string.
        /// </summary>
        /// <returns>UTF8 string.</returns>
        internal String GetBodyStringUtf8_Slow() {
            // Checking if there is a body.
            if (content_len_bytes_ <= 0)
                return null;

            return new String((SByte*)(socket_data_ + content_offset_), 0, (Int32)content_len_bytes_, Encoding.UTF8);
        }

        /// <summary>
        /// Gets the raw method and URI.
        /// </summary>
        /// <param name="ptr">The PTR.</param>
        /// <param name="sizeBytes">The size bytes.</param>
        public void GetRawMethodAndUri(out IntPtr ptr, out UInt32 sizeBytes)
        {
            // NOTE: Method and URI must always exist.

            ptr = new IntPtr(socket_data_ + request_offset_);
            sizeBytes = (UInt32) (uri_offset_ - request_offset_ + uri_len_bytes_);
        }

        /// <summary>
        /// Gets the raw method and URI.
        /// </summary>
        /// <param name="ptr">The PTR.</param>
        /// <param name="sizeBytes">The size bytes.</param>
        public IntPtr GetRawMethodAndUri() {
            // NOTE: Method and URI must always exist.
            return (IntPtr)(socket_data_ + request_offset_);
        }

        /// <summary>
        /// Gets the raw headers.
        /// </summary>
        /// <param name="ptr">The PTR.</param>
        /// <param name="sizeBytes">The size bytes.</param>
        public void GetRawHeaders(out IntPtr ptr, out UInt32 sizeBytes) {
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
            // Checking if there are cookies.
            if (headers_len_bytes_ <= 0)
                return null;

            return new String((SByte*)(socket_data_ + headers_offset_), 0, (Int32)headers_len_bytes_, Encoding.ASCII);
        }

        /// <summary>
        /// Gets the raw session string.
        /// </summary>
        /// <param name="ptr">The PTR.</param>
        /// <param name="sizeBytes">The size bytes.</param>
        public void GetRawSessionString(out IntPtr ptr) {
            ptr = new IntPtr(socket_data_ + session_string_offset_);
        }

        /// <summary>
        /// Gets the session string.
        /// </summary>
        /// <returns>String.</returns>
        public String GetSessionString()
        {
            IntPtr raw_session_string;
            GetRawSessionString(out raw_session_string);

            return Marshal.PtrToStringAnsi(raw_session_string, MixedCodeConstants.SESSION_STRING_LEN_CHARS);
        }

        /// <summary>
        /// Gets the header value.
        /// </summary>
        /// <param name="headerName">Name of the header.</param>
        /// <param name="ptr">The PTR.</param>
        /// <param name="sizeBytes">The size bytes.</param>
        public void GetHeaderValue(byte[] headerName, out IntPtr ptr, out UInt32 sizeBytes) {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the header value.
        /// </summary>
        /// <param name="headerName">Name of the header.</param>
        /// <param name="headersString">Reference of the header string.</param>
        /// <returns>String.</returns>
        public String GetHeaderValue(String headerName, ref String headersString) {

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

        /// <summary>
        /// Gets the URI.
        /// </summary>
        /// <value>The URI.</value>
        public String Uri {
            get {
                if (uri_len_bytes_ > 0)
                    return Marshal.PtrToStringAnsi((IntPtr)(socket_data_ + uri_offset_), (Int32)uri_len_bytes_);

                return null;
            }
        }

        /// <summary>
        /// Gets the HTTP method.
        /// </summary>
        /// <value>The method.</value>
        public String Method
        {
            get
            {
                // TODO: Pre-calculate the method length!
                unsafe
                {
                    // Calculate number of characters in method.
                    Int32 method_len = 0;
                    Byte* begin = socket_data_ + request_offset_;
                    while (*begin != ' ')
                    {
                        method_len++;
                        begin++;

                        // TODO: Security check.
                        if (method_len > 16)
                        {
                            method_len = 0;
                            break;
                        }
                    }

                    if (method_len > 0)
                        return Marshal.PtrToStringAnsi((IntPtr)(socket_data_ + request_offset_), (Int32)method_len);

                }

                return null;
            }
        }
    }
}
