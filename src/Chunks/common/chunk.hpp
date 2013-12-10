//
// chunk.hpp
//
// Copyright © 2006-2013 Starcounter AB. All rights reserved.
// Starcounter® is a registered trademark of Starcounter AB.
//

#ifndef STARCOUNTER_CORE_CHUNK_HPP
#define STARCOUNTER_CORE_CHUNK_HPP

#if defined(_MSC_VER) && (_MSC_VER >= 1200)
# pragma once
#endif // defined(_MSC_VER) && (_MSC_VER >= 1200)

#include <cstddef>
#include <cstdint>
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <intrin.h>
#undef WIN32_LEAN_AND_MEAN
#include "../common/owner_id.hpp"
#include "../common/config_param.hpp"

namespace starcounter {
namespace core {

/// TODO: Make a class.
typedef uint32_t chunk_index;

#ifdef _M_X64
# pragma intrinsic (_InterlockedExchange64)
#endif

typedef uint64_t bmx_handler_type;

// Current chunk layout:
// +-----------------------------+ 0..7
// | 64-bit bmx handler          |
// +-----------------------------+ 8..11
// | 32-bit request size         |
// +-----------------------------+ 12..
// | Request message. . .        |
// +-----------------------------+
// | 32-bit response size        |
// +-----------------------------+
// | Response message. . .       |
// +-----------------------------+ 4088..4091
// | 32-bit next link            |
// +-----------------------------+ 4092..4095
// | 32-bit (stream) link        |
// +-----------------------------+ 4096
// Next chunk. . .


// Chunk layout after task #993 is done:
// +-----------------------------+ 0..3
// | 32-bit next link            |
// +-----------------------------+ 4..7
// | 32-bit request size         |
// +-----------------------------+ 8..
// | Request message. . .        |
// | +-------------------------+ | 8..15
// | | 64-bit bmx handler      | |
// | +-------------------------+ | 16
// +-----------------------------+
// | 32-bit response size        |
// +-----------------------------+
// | Response message. . .       |
// +-----------------------------+ 4092..4095
// | 32-bit (stream) link        |
// +-----------------------------+ 4096
// Next chunk. . .


// This chunk layout shall be implemented later on, because the request can, and
// therefore should, be overwritten with the response. There is no "request" and
// "response" terminology - it is just called "message." The bmx handler field
// is stored as part of the message data in the beginning.
// +-------------------------+ 0..3
// | 32-bit next link        |
// +-------------------------+ 4..7
// | 32-bit message size     |
// +-------------------------+ 8..
// | Message. . .            |
// | +---------------------+ | 8..15
// | | 64-bit bmx handler  | |
// | +---------------------+ | 16
// +-------------------------+ 4092
// | 32-bit (stream) link    |
// +-------------------------+ 4096
// Next chunk. . .


template<class T, std::size_t N>
class chunk {
public:
	// Type definitions.
	typedef T value_type;
	typedef T* iterator;
	typedef const T* const_iterator;
	typedef T& reference;
	typedef const T& const_reference;
	typedef std::size_t size_type;
	typedef std::ptrdiff_t difference_type;
	
	// The kernel used the user_data value for various things, but not any longer.
	// TODO: Remove user_data in chunk.
	typedef uint64_t user_data_type;
	
	// The message size is a 32-bit unsigned int, same for request and response size.
	typedef uint32_t message_size_type;
	
	typedef chunk_index link_type;
	
	enum {
		static_size = N
	};
	
	enum {
		// There are two links: The link (rename it to stream_link), and the next_link.
		// The links have no pad bytes in between.
		// link_size represents the space required for these two links.
		link_size = 2 * sizeof(link_type),
		bmx_handler_size = sizeof(bmx_handler_type),
		
		// The last chunk in a stream (chain) of 1..N chunks is terminated by setting
		// the link (stream_link) to link_terminator. Likewise, to terminate the "overflow"
		// linked list the next_link is set to link_terminator.
		link_terminator = MixedCodeConstants::INVALID_CHUNK_INDEX
	};
	
	enum {
		bmx_protocol_begin = 0,
		request_size_begin = bmx_protocol_begin +bmx_handler_size,
		stream_link_begin = static_size -(1 * sizeof(link_type)),
		next_link_begin = static_size -(2 * sizeof(link_type))
	};
	
	// header size is constant
	enum {
		// Think about padding.
		static_header_size =
		+bmx_handler_size
		+sizeof(owner_id) // TODO: Not used, remove it.
		+sizeof(user_data_type) // TODO: Not used, remove it.
		+sizeof(message_size_type) // request_size
		+4 // What's this?
	};
	
	// data size is constant
	enum {
		static_data_size = static_size -static_header_size -link_size,
	};
	
	// size is constant
	static size_type size() {
		return N;
	}
	
	static bool empty() {
		return false;
	}
	
	static size_type max_size() {
		return N;
	}
	
	reference operator[](size_type i) {
		return elems[i];
	}
	
	const_reference operator[](size_type i) const {
		return elems[i];
	}
	
	/// data_size() returns the number of bytes of the chunks data area. It does
	/// not take into consideration the amount of data in use.
	/**
	 * @return The number of bytes reserved for the data area of the chunk,
	 *		not including the header data.
	 */
	static size_type data_size() {
		return N -static_header_size;
	}
	
	/// Header operations (some are synchronized by using atomics, others are
	/// synchronized enough when pushed to and popped from the in or out buffer
	/// of a channel.)
	
	/**
	 * @param protocol The protocol value to write to the chunk.
	 * @return A reference to this chunk.
	 */
	const chunk& set_bmx_handler_info(bmx_handler_type protocol) {
		*((bmx_handler_type*)(elems +bmx_protocol_begin)) = protocol;
		return *this;
	}
	
	/**
	 * @return The bmx protocol type.
	 */
	bmx_handler_type get_bmx_handler_info() const {
		return *((bmx_handler_type*)(elems +bmx_protocol_begin));
	}
	
	/// set_request_size() writes the request size value to the chunk.
	/**
	 * @param sz The request size value to write to the chunk.
	 * @return A reference to this chunk.
	 */
	const chunk& set_request_size(message_size_type sz) {
		*((message_size_type*)(elems +request_size_begin)) = sz;
		return *this;
	}
	
	/// get_request_size() returns the request size value of the chunk.
	/**
	 * @return The request size.
	 */
	message_size_type get_request_size() const {
		return *((message_size_type*)(elems +request_size_begin));
	}
	
	/// set_link() writes a link value to the chunk.
	/**
	 * @param next The link value to write, that refers to the next chunk.
	 * @return A reference to this chunk.
	 */
	const chunk& set_link(link_type next) {
		// No need to synchronize here because it is synchronized enough
		// when pushed to and popped from a channels in or out buffer.
		*((link_type*)(elems +stream_link_begin)) = next;
		return *this;
	}
	
	/// get_link() returns the link value of the chunk.
	/**
	 * @return The link value of the chunk.
	 */
	chunk_index get_link() const {
		return *((chunk_index*)(elems +stream_link_begin));
	}
	
	/// terminate_link() terminates the link of the chunk.
	/**
	 * @return A reference to this chunk.
	 */
	chunk& terminate_link() {
		// No need to synchronize here because it is synchronized enough
		// when pushed to and popped from a channels in or out buffer.
		*((link_type*)(elems +stream_link_begin)) = link_terminator;
		return *this;
	}
	
	/// is_terminated() test if the link is terminated.
	/**
	 * @return true if the link value of the chunk is link_terminator, false
	 *		otherwise.
	 */
	bool is_terminated() const {
		return *((chunk_index*)(elems +stream_link_begin)) == link_terminator;
	}
	
	/// set_next() writes the next_ link value to the chunk.
	/**
	 * @param next The link value to write, that refers to the next chunk.
	 * @return A reference to this chunk.
	 */
	chunk& set_next(link_type next) {
		*((chunk_index*)(elems +next_link_begin)) = next;
		return *this;
	}
	
	/// get_next() returns the next_link value of the chunk.
	/**
	 * @return The link value of the chunk.
	 */
	chunk_index get_next() const {
		return *((chunk_index*)(elems +next_link_begin));
	}
	
	/// terminate_next_link() terminates the next_link of the chunk.
	/**
	 * @return A reference to this chunk.
	 */
	const chunk& terminate_next() {
		*((link_type*)(elems +next_link_begin)) = link_terminator;
		return *this;
	}
	
	/// is_next_terminated() test if the next_link is terminated.
	/**
	 * @return true if the next_link value of the chunk is link_terminator,
	 *		false otherwise.
	 */
	bool is_next_terminated() const {
		return *((link_type*)(elems +next_link_begin)) == link_terminator;
	}
	
	// Idéas:
	// Helper functions to read/write all built-in integer types: 8-, 16-, 32-
	// and 64-bit values.
	//
	// Read/write sequence of N bytes (a char array), and a c-style string.
	
public:
	// Fixed-size array of elements of type T.
	T elems[N];
};

typedef chunk<uint8_t, chunk_size> chunk_type;

} // namespace core
} // namespace starcounter

#endif // STARCOUNTER_CORE_CHUNK_HPP
