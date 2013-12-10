#pragma once
#ifndef URIMATCH_CODEGEN_HPP
#define URIMATCH_CODEGEN_HPP

namespace starcounter {
namespace network {

// Type of compiler to use for URI matching Codegen.
enum UriMatchCodegenCompilerType
{
    COMPILER_GCC,
    COMPILER_MSVC,
    COMPILER_CLANG
};

class CodegenUriMatcher
{
    static const uint32_t MAX_URI_MATCHING_CODE_BYTES = 1024 * 1024;
    uint32_t uri_code_size_bytes_;
    char* uri_matching_code_;

    MixedCodeConstants::GenerateNativeUriMatcherType generate_uri_matcher_;

public:

    CodegenUriMatcher()
    {
        generate_uri_matcher_ = NULL;
        uri_matching_code_ = NULL;
        uri_code_size_bytes_ = 0;
    }

    // Getting URI matching code.
    char* get_uri_matching_code()
    {
        return uri_matching_code_;
    }

    // Initializes managed codegen loader.
    void Init();

    // Generate the code using managed generator.
    uint32_t GenerateUriMatcher(
        const char* const root_function_name,
        MixedCodeConstants::RegisteredUriManaged* uri_infos,
        uint32_t num_uris)
    {
        uri_code_size_bytes_ = MAX_URI_MATCHING_CODE_BYTES;

        // Measuring time taken for generating matcher.
        uint64_t begin_time = timeGetTime();

        uint32_t err_code = generate_uri_matcher_(
            g_gateway.get_sc_log_handle(),
            root_function_name, 
            uri_infos,
            num_uris,
            uri_matching_code_,
            &uri_code_size_bytes_);

        // Asserting that URI matcher code generation always succeeds.
        GW_ASSERT(0 == err_code);

        // Printing how much time it took for generating the matcher.
        std::cout << "Wrapper URI matcher generation(" << num_uris << ") took: " << timeGetTime() - begin_time << " ms." << std::endl;

        /*std::ifstream config_file_stream(L"codegen_uri_matcher.cpp");
        std::stringstream str_stream;
        str_stream << config_file_stream.rdbuf();
        std::string tmp_str = str_stream.str();
        strcpy_s(uri_matching_code_, tmp_str.size() + 1, tmp_str.c_str());*/

        return err_code;
    }

    // Compile given code into native dll.
    uint32_t CompileIfNeededAndLoadDll(
        UriMatchCodegenCompilerType comp_type,
        const std::wstring& gen_file_name,
        const char* const root_function_name,
        void** clang_engine,
        MixedCodeConstants::MatchUriType* out_match_uri_func,
        HMODULE* out_codegen_dll_handle);

    ~CodegenUriMatcher()
    {
        if (uri_matching_code_)
        {
            delete [] uri_matching_code_;
            uri_matching_code_ = NULL;
        }
    }
};
} // namespace network
} // namespace starcounter

#endif // URIMATCH_CODEGEN_HPP