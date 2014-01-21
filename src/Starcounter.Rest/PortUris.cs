using Starcounter.Internal;
using Starcounter.Internal.Uri;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Starcounter.Rest
{
    /// <summary>
    /// All URIs registered per port.
    /// </summary>
    internal unsafe class PortUris
    {
        [DllImport("GatewayClang.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public extern static void* GwClangCompileCodeAndGetFuntion(
            void** clang_engine,
            Byte* code_str,
            Byte* func_name,
            Boolean accumulate_old_modules
        );

        [DllImport("GatewayClang.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public extern static void GwClangInit();

        [DllImport("GatewayClang.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public extern static void GwClangDestroyEngine(void* clang_engine);

        public delegate Int32 MatchUriDelegate(
            Byte* uri_info,
            UInt32 uri_info_len,
            MixedCodeConstants.UserDelegateParamInfo** native_params);

        public delegate void ClangDestroyEngineType(void* clang_engine);

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="port"></param>
        public PortUris(UInt16 port)
        {
            port_ = port;
        }

        /// <summary>
        /// This port number.
        /// </summary>
        UInt16 port_;

        /// <summary>
        /// This port number.
        /// </summary>
        public UInt16 Port
        {
            get { return port_; }
        }

        const Int32 GenCodeStringNumBytes = 1024 * 1024;

        /// <summary>
        /// Json for the generated code.
        /// </summary>
        Byte[] gen_code_string_container_ = new Byte[GenCodeStringNumBytes];

        /// <summary>
        /// Pointer to create Clang engine.
        /// </summary>
        void* clang_engine_ = null;

        /// <summary>
        /// Delegate to generated match URI function.
        /// </summary>
        public volatile MatchUriDelegate MatchUriAndGetHandlerId = null;

        /// <summary>
        /// Global init for Clang.
        /// </summary>
        public static void GlobalInit()
        {
            GwClangInit();
        }

        /// <summary>
        /// Destroying this port.
        /// </summary>
        public void Destroy()
        {
            GwClangDestroyEngine(clang_engine_);
        }

        /// <summary>
        /// Generates the URI matcher for this port.
        /// </summary>
        public unsafe Boolean GenerateUriMatcher(
            UInt16 port,
            UserHandlerInfo[] allUserHandlers,
            Int32 numRegisteredHandlers)
        {
            lock (allUserHandlers)
            {
                // Checking again in case if delegate was already constructed.
                if (MatchUriAndGetHandlerId != null)
                    return true;

                // Creating list of registered uri infos.
                List<MixedCodeConstants.RegisteredUriManaged> registered_uri_infos = new List<MixedCodeConstants.RegisteredUriManaged>();
                for (Int32 i = 0; i < numRegisteredHandlers; i++)
                {
                    if (!allUserHandlers[i].IsEmpty())
                    {
                        // Grabbing URIs for this port only.
                        if (port == allUserHandlers[i].UriInfo.port_)
                        {
                            registered_uri_infos.Add(allUserHandlers[i].UriInfo.GetRegisteredUriManaged());
                        }
                    }
                }

                // Checking if there are any URIs.
                if (registered_uri_infos.Count <= 0)
                    return false;

                MixedCodeConstants.RegisteredUriManaged[] registered_uri_infos_array = registered_uri_infos.ToArray();

                // Name of the root function.
                String root_function_name = "MatchUriForPort" + port;

                fixed (Byte* gen_code_string_container = gen_code_string_container_)
                {
                    fixed (MixedCodeConstants.RegisteredUriManaged* reg_uri_infos_array = registered_uri_infos_array)
                    {
                        UInt32 num_code_bytes = GenCodeStringNumBytes - 1;

                        UInt32 err_code = UriMatcherBuilder.GenerateNativeUriMatcherManaged(
                            (UInt64)MixedCodeConstants.INVALID_SERVER_LOG_HANDLE,
                            root_function_name,
                            (IntPtr)reg_uri_infos_array,
                            (UInt32)registered_uri_infos_array.Length,
                            (IntPtr)gen_code_string_container,
                            ref num_code_bytes);

                        if (err_code != 0)
                            throw ErrorCode.ToException(err_code, "Internal URI matcher code generation error!");
                    }

                    Byte[] root_function_name_bytes = Encoding.ASCII.GetBytes(root_function_name);

                    fixed (Byte* fnpb = root_function_name_bytes)
                    {
                        fixed (void** clang_engine = &clang_engine_)
                        {
                            // Compiling the given code and getting function pointer back.
                            IntPtr func_ptr = (IntPtr)GwClangCompileCodeAndGetFuntion(
                                clang_engine,
                                gen_code_string_container,
                                fnpb,
                                false);

                            // Ensuring that generated function is not null.
                            Debug.Assert(func_ptr != IntPtr.Zero);

                            // Getting the managed.
                            MatchUriAndGetHandlerId = (MatchUriDelegate)Marshal.GetDelegateForFunctionPointer(func_ptr, typeof(MatchUriDelegate));
                        }
                    }
                }
            }

            return true;
        }
    }
}