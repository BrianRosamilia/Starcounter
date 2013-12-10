
#ifndef STARCOUNTER_BLAST2_H
#define STARCOUNTER_BLAST2_H

#if defined(_MSC_VER) && (_MSC_VER >= 1200)
# pragma once
#endif // defined(_MSC_VER) && (_MSC_VER >= 1200)

#include <iostream>
#include <stdint.h>
#include "../../Starcounter.ErrorCodes/scerrres/scerrres.h"
#include "../common/config_param.hpp"
#include "../common/chunk.hpp"
#include "../common/macro_definitions.hpp"

// Bmx handler type.
typedef starcounter::core::bmx_handler_type BMX_HANDLER_TYPE;
typedef uint16_t BMX_HANDLER_INDEX_TYPE;
typedef uint32_t BMX_HANDLER_UNIQUE_NUM_TYPE;

inline BMX_HANDLER_INDEX_TYPE GetBmxHandlerIndex(BMX_HANDLER_TYPE handler_info)
{
    return (BMX_HANDLER_INDEX_TYPE)handler_info;
}

class chunk_part
{
private:
	starcounter::core::chunk_type::message_size_type offset_;
	uint8_t chunk_[1];

	template<typename T>
	void write(T value)
	{
		*((PACKED T*)(chunk_ + offset_)) = value;
		offset_ += sizeof(T);
	}

	template<typename T>
	T read()
	{
		T value = *((PACKED T*)(chunk_ + offset_));
		offset_ += sizeof(T);
		return value;
	}

	template<typename T>
	T peek()
	{
		return *((PACKED T*)(chunk_ + offset_));
	}


public:

	starcounter::core::chunk_type::message_size_type get_offset()
	{
		return offset_;
	}

	void reset_offset()
	{
		offset_ = 0;
	}

	void write_type_id(uint8_t type_id)
	{
		chunk_[offset_] = type_id << 1;
		offset_++;
	}

	uint8_t read_type_id()
	{
		uint8_t type_id = (chunk_[offset_] >> 1);
		offset_++;
		return type_id;
	}

    BMX_HANDLER_TYPE read_handler_info()
    {
        return read_uint64();
    }

    void write_handler_id(BMX_HANDLER_TYPE handler_id)
    {
        write(handler_id);
    }

	void skip(uint32_t n) { offset_ += n; }
	uint8_t* get_raw_chunk() { return chunk_ + offset_; }

	int8_t read_int8() { return read<int8_t>(); }
	uint8_t read_uint8() { return read<uint8_t>(); }
	int16_t read_int16() { return read<int16_t>(); }
	uint16_t read_uint16() { return read<uint16_t>(); }
	int32_t read_int32() { return read<int32_t>(); }
	uint32_t read_uint32() { return read<uint32_t>(); }
	int64_t read_int64() { return read<int64_t>(); }
	uint64_t read_uint64() { return read<uint64_t>(); }
	float read_float() { return read<float>(); }
	double read_double() { return read<double>(); }

    uint32_t read_wstring(wchar_t* dest_str, uint32_t num_chars_to_copy, uint32_t max_len_chars)
    {
        // Copying the URI string.
        _SC_ASSERT(0 == wcsncpy_s(dest_str, max_len_chars, (wchar_t *)get_raw_chunk(), num_chars_to_copy));

        // Skipping all read characters.
        skip(num_chars_to_copy << 1);

        return 0;
    }

    uint32_t read_string(char* dest_str, uint32_t num_chars_to_copy, uint32_t max_len_chars)
    {
        // Copying the URI string.
        _SC_ASSERT(0 == strncpy_s(dest_str, max_len_chars, (char *)get_raw_chunk(), num_chars_to_copy));

        // Skipping all read characters.
        skip(num_chars_to_copy);

        return 0;
    }

	int32_t peek_int32() { return peek<int32_t>(); }
	uint32_t peek_uint32() { return peek<uint32_t>(); }

	void write(int8_t value) { write<int8_t>(value); }
	void write(uint8_t value) { write<uint8_t>(value); }
	void write(int16_t value) { write<int16_t>(value); }
	void write(uint16_t value) { write<uint16_t>(value); }
	void write(int32_t value) { write<int32_t>(value); }
	void write(uint32_t value) { write<uint32_t>(value); }
	void write(int64_t value) { write<int64_t>(value); }
	void write(uint64_t value) { write<uint64_t>(value); }
	void write(float value) { write<float>(value); }
	void write(double value) { write<double>(value); }

    uint32_t write_wstring(const wchar_t* str, uint32_t num_chars_to_write)
    {
        // Writing number of characters in the string.
        write(num_chars_to_write);

        // Checking if enough space.
        _SC_ASSERT(num_chars_to_write < ((starcounter::core::chunk_size - offset_) >> 1));

        // Copying the URI string.
        _SC_ASSERT(0 == wcsncpy_s((wchar_t *)get_raw_chunk(), num_chars_to_write + 1, str, num_chars_to_write));

        // Skipping all written characters.
        skip(num_chars_to_write << 1);

        return 0;
    }

    uint32_t write_string(char* str, uint32_t num_chars_to_write)
    {
        // Writing number of characters in the string.
        write(num_chars_to_write);

        // Checking if enough space.
        _SC_ASSERT(num_chars_to_write < (starcounter::core::chunk_size - offset_));

        // Copying the URI string.
        _SC_ASSERT(0 == strncpy_s((char *)get_raw_chunk(), num_chars_to_write + 1, str, num_chars_to_write));

        // Skipping all written characters.
        skip(num_chars_to_write);

        return 0;
    }

	uint32_t write_data_and_size(uint8_t* data, uint32_t data_size)
	{
		// TODO: 
		// Check for buffer overrun
		uint8_t* p = chunk_ + offset_;

		*((PACKED int32_t*)p) = data_size;
		memcpy(p + 4, data, data_size);
		offset_ += data_size + 4;
		return 0;
	}

	uint32_t write_data_only(uint8_t* data, uint32_t data_size)
	{
		// TODO: 
		// Check for buffer overrun
		uint8_t* p = chunk_ + offset_;
		memcpy(p, data, data_size);
		offset_ += data_size;
		return 0;
	}

	uint32_t copy_data_to_buffer(uint8_t* buffer, uint32_t size)
	{
		uint8_t* p;

		p = chunk_ + offset_;
		memcpy(buffer, p, size);
		offset_ += size;
		return 0;
	}
};

typedef chunk_part request_chunk_part ;
typedef chunk_part response_chunk_part;

template<class T, std::size_t N>
class blast_chunk : public starcounter::core::chunk<T, N>
{
public:
	request_chunk_part* get_request_chunk() 
	{ 
		return (chunk_part*)(elems + request_size_begin);
	}

	response_chunk_part* get_response_chunk()
	{
		uint32_t offset = static_header_size + get_request_size();
		return (chunk_part*)(elems + offset);
	}

	// TODO:
	// Change this call. We don't want to have several places where the header
	// is written in case of changes to it.
	uint8_t* get_raw_chunk()
	{
		return (uint8_t*)elems;
	}
};

// TODO:
// rename shared_memory_chunk to something better.
typedef blast_chunk<uint8_t, starcounter::core::chunk_size> shared_memory_chunk;

#endif
