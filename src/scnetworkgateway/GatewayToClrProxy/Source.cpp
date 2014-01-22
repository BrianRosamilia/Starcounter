#pragma once

using namespace System;
using namespace Starcounter::Internal::Uri;
using namespace System::Runtime::InteropServices;

extern "C" __declspec(dllexport) UInt32 __cdecl GenerateUriMatcher(
    UInt64 server_log_handle,
    char* root_function_name,
    UInt64* uri_infos_ptr,
    UInt32 num_uris,
    UInt64* gen_code_str,
    UInt32% gen_code_str_num_bytes)
{
    return UriMatcherBuilder::GenerateNativeUriMatcher(
        server_log_handle,
        gcnew System::String(root_function_name),
        (IntPtr)uri_infos_ptr,
        num_uris,
        (IntPtr)gen_code_str,
        gen_code_str_num_bytes);
}
