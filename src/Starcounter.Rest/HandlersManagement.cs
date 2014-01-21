using Starcounter;
using Starcounter.Internal;
using Starcounter.Rest;
using System;
using System.Collections.Generic;
using System.Text;

namespace Starcounter.Rest
{
    /// <summary>
    /// Information about registered URI.
    /// </summary>
    internal class RegisteredUriInfo
    {
        public String original_uri_info_ = null;
        public IntPtr original_uri_info_ascii_bytes_;
        public String processed_uri_info_ = null;
        public IntPtr processed_uri_info_ascii_bytes_;
        public Type param_message_type_ = null;
        public Byte[] native_param_types_ = null;
        public Byte num_params_ = 0;
        public UInt16 handler_id_ = UInt16.MaxValue;
        public UInt16 port_ = 0;
        public MixedCodeConstants.NetworkProtocolType proto_type_ = MixedCodeConstants.NetworkProtocolType.PROTOCOL_HTTP1;
        public HTTP_METHODS http_method_ = HTTP_METHODS.GET;

        public void Destroy()
        {
            original_uri_info_ = null;
            processed_uri_info_ = null;
            handler_id_ = UInt16.MaxValue;

            if (original_uri_info_ascii_bytes_ != IntPtr.Zero)
            {
                // Releasing internal resources here.
                BitsAndBytes.Free(original_uri_info_ascii_bytes_);
                original_uri_info_ascii_bytes_ = IntPtr.Zero;
            }

            if (processed_uri_info_ascii_bytes_ != IntPtr.Zero)
            {
                // Releasing internal resources here.
                BitsAndBytes.Free(processed_uri_info_ascii_bytes_);
                processed_uri_info_ascii_bytes_ = IntPtr.Zero;
            }
        }

        public void InitUriPointers()
        {
            unsafe
            {
                original_uri_info_ascii_bytes_ = BitsAndBytes.Alloc(original_uri_info_.Length);
                Byte[] temp = Encoding.ASCII.GetBytes(original_uri_info_);
                fixed (Byte* t = temp)
                {
                    BitsAndBytes.MemCpy((Byte*)original_uri_info_ascii_bytes_, t, (uint)original_uri_info_.Length);
                }

                processed_uri_info_ascii_bytes_ = BitsAndBytes.Alloc(processed_uri_info_.Length);
                temp = Encoding.ASCII.GetBytes(processed_uri_info_);
                fixed (Byte* t = temp)
                {
                    BitsAndBytes.MemCpy((Byte*)processed_uri_info_ascii_bytes_, t, (uint)processed_uri_info_.Length);
                }
            }
        }

        public unsafe MixedCodeConstants.RegisteredUriManaged GetRegisteredUriManaged()
        {
            MixedCodeConstants.RegisteredUriManaged r = new MixedCodeConstants.RegisteredUriManaged();

            r.original_uri_info_len_chars = (UInt32)original_uri_info_.Length;
            r.original_uri_info_string = original_uri_info_ascii_bytes_;

            r.processed_uri_info_len_chars = (UInt32)processed_uri_info_.Length;
            r.processed_uri_info_string = processed_uri_info_ascii_bytes_;

            r.num_params = num_params_;

            // TODO: Resolve this hack with only positive handler ids in generated code.
            r.handler_id = handler_id_ + 1;

            for (Int32 i = 0; i < native_param_types_.Length; i++)
                r.param_types[i] = native_param_types_[i];

            return r;
        }
    }

    /// <summary>
    /// User handler information.
    /// </summary>
    internal class UserHandlerInfo
    {
        Func<Request, IntPtr, IntPtr, Response> user_delegate_ = null;

        RegisteredUriInfo uri_info_ = new RegisteredUriInfo();

        public RegisteredUriInfo UriInfo
        {
            get { return uri_info_; }
        }

        public Type ArgMessageType
        {
            get { return uri_info_.param_message_type_; }
            set { uri_info_.param_message_type_ = value; }
        }

        public String OriginalUriInfo
        {
            get { return uri_info_.original_uri_info_; }
        }

        public String ProcessedUriInfo
        {
            get { return uri_info_.processed_uri_info_; }
        }

        public UInt16 Port
        {
            get { return uri_info_.port_; }
        }

        public Func<Request, IntPtr, IntPtr, Response> UserDelegate
        {
            get { return user_delegate_; }
        }

        public bool IsEmpty()
        {
            return uri_info_.processed_uri_info_ == null;
        }

        public void Destroy()
        {
            uri_info_.Destroy();
            user_delegate_ = null;
        }

        public void Init(
            UInt16 port,
            String original_uri_info,
            String processed_uri_info,
            Func<Request, IntPtr, IntPtr, Response> user_delegate,
            Byte[] native_param_types,
            Type param_message_type,
            UInt16 handler_id,
            MixedCodeConstants.NetworkProtocolType protoType)
        {
            uri_info_.original_uri_info_ = original_uri_info;
            uri_info_.processed_uri_info_ = processed_uri_info;
            uri_info_.param_message_type_ = param_message_type;
            uri_info_.handler_id_ = handler_id;
            uri_info_.port_ = port;
            uri_info_.native_param_types_ = native_param_types;
            uri_info_.num_params_ = (Byte)native_param_types.Length;
            uri_info_.http_method_ = UriHelper.GetMethodFromString(original_uri_info);

            user_delegate_ = user_delegate;

            uri_info_.InitUriPointers();
        }
    }

    /// <summary>
    /// All registered handlers manager.
    /// </summary>
    internal class HandlersManagement
    {
        UserHandlerInfo[] allUserHandlers_ = null;

        List<PortUris> portUris_ = new List<PortUris>();

        Int32 maxNumHandlersEntries_ = 0;

        public HandleInternalRequestDelegate HandleInternalRequest_;

        public delegate Boolean UriCallbackDelegate(Request req);

        public delegate Response HandleInternalRequestDelegate(Request request);

        // Checking if this Node supports local resting.
        internal Boolean IsSupportingLocalNodeResting()
        {
            return HandleInternalRequest_ != null;
        }

        public delegate void RegisterUriHandlerNative(
            UInt16 port,
            String originalUriInfo,
            String processedUriInfo,
            Byte[] paramTypes,
            UriCallbackDelegate uriCallback,
            MixedCodeConstants.NetworkProtocolType protoType,
            out UInt16 handlerId,
            out Int32 maxNumEntries);

        RegisterUriHandlerNative RegisterUriHandlerNative_;
        public UriCallbackDelegate OnHttpMessageRoot_;
        static Action<string, ushort> OnHandlerRegistered_;

        public static void SetHandlerRegisteredCallback(Action<string, ushort> callback)
        {
            OnHandlerRegistered_ = callback;
        }

        public void SetRegisterUriHandlerNew(
            RegisterUriHandlerNative registerUriHandlerNew,
            UriCallbackDelegate onHttpMessageRoot,
            HandleInternalRequestDelegate handleInternalRequest)
        {
            RegisterUriHandlerNative_ = registerUriHandlerNew;
            OnHttpMessageRoot_ = onHttpMessageRoot;
            HandleInternalRequest_ = handleInternalRequest;
        }

        public List<PortUris> PortUrisList
        {
            get { return portUris_; }
        }

        public PortUris SearchPort(UInt16 port)
        {
            foreach (PortUris pu in PortUrisList)
            {
                if (pu.Port == port)
                    return pu;
            }
            return null;
        }

        public PortUris AddPort(UInt16 port)
        {
            lock (HandleInternalRequest_)
            {
                // Searching for existing port if any.
                PortUris portUris = SearchPort(port);
                if (portUris != null)
                    return portUris;

                // Adding new port entry.
                portUris = new PortUris(port);
                portUris_.Add(portUris);
                return portUris;
            }
        }

        public UserHandlerInfo[] AllUserHandlerInfos
        {
            get { return allUserHandlers_; }
        }

        public Int32 NumRegisteredHandlers
        {
            get { return maxNumHandlersEntries_; }
        }

        public const Int32 MAX_USER_HANDLERS = 256;

        internal void Reset()
        {
            maxNumHandlersEntries_ = 0;

            portUris_ = new List<PortUris>();

            allUserHandlers_ = new UserHandlerInfo[MAX_USER_HANDLERS];
            for (Int32 i = 0; i < MAX_USER_HANDLERS; i++)
                allUserHandlers_[i] = new UserHandlerInfo();
        }

        public HandlersManagement()
        {
            // Initializing port uris.
            PortUris.GlobalInit();

            allUserHandlers_ = new UserHandlerInfo[MAX_USER_HANDLERS];
            for (Int32 i = 0; i < MAX_USER_HANDLERS; i++)
                allUserHandlers_[i] = new UserHandlerInfo();
        }

        public void RunDelegate(Request r, out Response resource)
        {
            unsafe
            {
                UserHandlerInfo uhi = allUserHandlers_[r.HandlerId];

                // Checking if we had custom type user Message argument.
                if (uhi.ArgMessageType != null)
                    r.ArgMessageObjectType = uhi.ArgMessageType;

                // Setting some request parameters.
                r.PortNumber = uhi.UriInfo.port_;
                r.MethodEnum = uhi.UriInfo.http_method_;

                IntPtr methodAndUri;

                // Checking what underlying protocol we have.
                switch (r.ProtocolType)
                {
                    case MixedCodeConstants.NetworkProtocolType.PROTOCOL_HTTP1:
                    methodAndUri = r.GetRawMethodAndUri();
                    break;

                    case MixedCodeConstants.NetworkProtocolType.PROTOCOL_WEBSOCKETS:
                    methodAndUri = IntPtr.Zero;
                    break;

                    default:
                    throw new ArgumentOutOfRangeException("Trying to call handler for unsupported protocol: " + r.ProtocolType);
                }

                // Calling user delegate.
                resource = uhi.UserDelegate(
                    r,
                    methodAndUri,
                    r.GetRawParametersInfo());
            }
        }

        /// <summary>
        /// Registers URI handler on a specific port.
        /// </summary>
        /// <param name="port"></param>
        /// <param name="originalUriInfo"></param>
        /// <param name="processedUriInfo"></param>
        /// <param name="nativeParamTypes"></param>
        /// <param name="messageType"></param>
        /// <param name="wrappedDelegate"></param>
        /// <param name="protoType"></param>
        public void RegisterUriHandler(
            UInt16 port,
            String originalUriInfo,
            String processedUriInfo,
            Byte[] nativeParamTypes,
            Type messageType,
            Func<Request, IntPtr, IntPtr, Response> wrappedDelegate,
            MixedCodeConstants.NetworkProtocolType protoType)
        {
            lock (allUserHandlers_)
            {
                UInt16 handlerId = 0;

                // Checking if URI already registered.
                for (Int32 i = 0; i < maxNumHandlersEntries_; i++)
                {
                    if ((0 == String.Compare(allUserHandlers_[i].ProcessedUriInfo, processedUriInfo, true)) &&
                        (port == allUserHandlers_[i].Port))
                    {
                        throw ErrorCode.ToException(Error.SCERRHANDLERALREADYREGISTERED, "Processed URI: " + processedUriInfo);
                    }
                }

                // Registering the outer native handler (if any).
                if (RegisterUriHandlerNative_ != null)
                {
                    RegisterUriHandlerNative_(
                        port,
                        originalUriInfo,
                        processedUriInfo,
                        nativeParamTypes,
                        OnHttpMessageRoot_,
                        protoType,
                        out handlerId,
                        out maxNumHandlersEntries_);
                }
                else
                {
                    handlerId = (UInt16)maxNumHandlersEntries_;
                    maxNumHandlersEntries_++;
                }

                if (handlerId >= MAX_USER_HANDLERS)
                    throw new ArgumentOutOfRangeException("Too many user handlers registered!");

                allUserHandlers_[handlerId].Init(
                    port,
                    originalUriInfo,
                    processedUriInfo,
                    wrappedDelegate,
                    nativeParamTypes,
                    messageType,
                    handlerId,
                    protoType);

                if (OnHandlerRegistered_ != null)
                    OnHandlerRegistered_(originalUriInfo, port);

                // Resetting port URIs matcher.
                PortUris portUris = UserHandlerCodegen.HandlersManager.SearchPort(port);
                if (portUris != null)
                    portUris.MatchUriAndGetHandlerId = null;
            }
        }

        /// <summary>
        /// Unregisteres existing URI handler.
        /// </summary>
        /// <param name="methodAndUri"></param>
        public void UnregisterUriHandler(String methodAndUri)
        {
            lock (allUserHandlers_)
            {
                for (Int32 i = 0; i < MAX_USER_HANDLERS; i++)
                {
                    if (allUserHandlers_[i].ProcessedUriInfo == methodAndUri)
                    {
                        // TODO: Call underlying BMX handler destructor.

                        allUserHandlers_[i].Destroy();
                        maxNumHandlersEntries_--;

                        throw new NotImplementedException();
                    }
                }
            }
        }

        /// <summary>
        /// Incorrect session exception.
        /// </summary>
        public class IncorrectSessionException : Exception { }
    }
}