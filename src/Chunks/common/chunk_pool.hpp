//
// chunk_pool.hpp
//
// Copyright © 2006-2012 Starcounter AB. All rights reserved.
// Starcounter® is a registered trademark of Starcounter AB.
//
// Thread-Safety
// The thread-safety of the chunk_pool is the same as the thread-safety of
// containers in most STL implementations. This means the chunk_pool is not
// thread-safe.
//

#ifndef STARCOUNTER_CORE_CHUNK_POOL_HPP
#define STARCOUNTER_CORE_CHUNK_POOL_HPP

#if defined(_MSC_VER) && (_MSC_VER >= 1200)
# pragma once
#endif // defined(_MSC_VER) && (_MSC_VER >= 1200)

#include "../common/interprocess.hpp"
#include "../common/circular_buffer.hpp"
#include "../common/client_interface.hpp"

namespace starcounter {
namespace core {

// The chunk_pool relies on Boost Threads and Boost Bind libraries and Boost
// call_traits utility.

// The push_front() method is called in order to insert a new item into the buffer.
// If there is a space in the buffer available, the execution continues and the
// method inserts the item at the end of the chunk_pool. Then it increments
// the number of unread items.

// The pop_back() method is called in order to read the next item from the buffer.
// If there is at least one unread item, the method decrements the number of unread
// items and reads the next item from the chunk_pool.

// The pop_back() method does not remove the item but the item is left in the
// chunk_pool which then replaces it with a new one (inserted by a producer)
// when the chunk_pool is full. This technique is more effective than removing
// the item explicitly by calling the pop_back() method of the chunk_pool.
// This claim is based on the assumption that an assignment (replacement) of a
// new item into an old one is more effective than a destruction (removal) of an
// old item and a consequent inplace construction (insertion) of a new item.

/// class chunk_pool
/**
 * @param T The type of the elements stored in the chunk_pool.
 * @par Type Requirements T
 *		The T has to be SGIAssignable (SGI STL defined combination of Assignable
 *		and CopyConstructible), and EqualityComparable and/or LessThanComparable
 *		if the chunk_pool will be compared with another container.
 * @param Alloc The allocator type used for all internal memory management.
 * @par Type Requirements Alloc
 *		The Alloc has to meet the allocator requirements imposed by STL.
 * @par Default Alloc
 *		std::allocator<T>
 */
template<class T, class Alloc = std::allocator<T> >
class chunk_pool : public circular_buffer<T,Alloc> {
public:
	// Construction/Destruction.
	
	/// Create an empty chunk_pool with the specified capacity.
	/**
	 * @param buffer_capacity The maximum number of elements which can be stored
	 *		in the chunk_pool.
	 * @param alloc The allocator.
	 * @throws "An allocation error" if memory is exhausted (std::bad_alloc if
	 *		the standard allocator is used).
	 * @par Complexity
	 *		Constant.
	 */
	explicit chunk_pool(size_type buffer_capacity = 0, const allocator_type&
	alloc = allocator_type());
	
	//--------------------------------------------------------------------------
	/// Acquire linked chunks - when the chunks are already owned by the client.
	/// A client can use this function, but not a scheduler.
	/**
	 * @param chunk_base Points to the first element of the array of chunks.
	 * @param head Will upon return contain the head of a linked chain of chunks
	 *		if successfuly acquired the requested size.
	 * @param size The number of bytes to allocate as 1..N linked chunks. The
	 *		chunks require some space for header data and this is taken into
	 *		account. Size 0 is not a valid value, and it doesn't make sense.
	 * @return true if successfully acquired the linked chunks, otherwise false.
	 */
	bool acquire_linked_chunks(chunk_type* chunk_base, chunk_index& head,
	std::size_t size);

    //--------------------------------------------------------------------------
	/// Acquire linked chunks - when the chunks are already owned by the client.
	/// A client can use this function, but not a scheduler.
	/**
	 * @param chunk_base Points to the first element of the array of chunks.
	 * @param head Will upon return contain the head of a linked chain of chunks
	 *		if successfuly acquired the requested size.
	 * @param num_chunks_to_acquire The number of chunks to allocate as 1..N linked chunks.
	 * @return true if successfully acquired the linked chunks, otherwise false.
	 */
	bool acquire_linked_chunks_counted(chunk_type* chunk_base, chunk_index& head,
	std::size_t num_chunks_to_acquire);
	
	/// Release linked chunks - when the chunks are already owned by the client.
	/// A client can use this function, but not a scheduler.
	/**
	 * @param chunk_base Points to the first element of the array of chunks.
	 * @param head The head of the linked chunks, will upon return contain
	 *		chunk_type::LINK_TERMINATOR if successfully released all linked
	 *		chunks, otherwise it contains the chunk_index pointing to the head
	 *		of the linked chunks that are left.
	 * @return true if successfully released all linked chunks in which case
	 *		head is set to chunk_type::LINK_TERMINATOR, otherwise false.
	 */
	bool release_linked_chunks(chunk_type* chunk_base, chunk_index& head);

#if 0	
	//--------------------------------------------------------------------------
	/// Acquire linked chunks - when the chunks are not owned by any client.
	/// A scheduler can use this function, but not a client.
	/**
	 * @param chunk_base Points to the first element of the array of chunks.
	 * @param head Will upon return contain the head of a linked chain of chunks
	 *		if successfuly acquired the requested size.
	 * @param size The number of bytes to allocate as 1..N linked chunks. The
	 *		chunks require some space for header data and this is taken into
	 *		account.
	 * @param client_interface_ptr A pointer to the client_interface where the
	 *		chunk will be marked as owned by the client.
	 * @return true if successfully acquired the linked chunks, otherwise false.
	 */
	bool acquire_linked_chunks(chunk_type* chunk_base, chunk_index& head,
	std::size_t size, client_interface_type* client_interface_ptr);

    //--------------------------------------------------------------------------
	/// Acquire linked chunks - when the chunks are not owned by any client.
	/// A scheduler can use this function, but not a client.
	/**
	 * @param chunk_base Points to the first element of the array of chunks.
	 * @param head Will upon return contain the head of a linked chain of chunks
	 *		if successfuly acquired the requested size.
     * @param num_chunks_to_acquire The number of chunks to allocate as 1..N linked chunks.
	 * @param client_interface_ptr A pointer to the client_interface where the
	 *		chunk will be marked as owned by the client.
	 * @return true if successfully acquired the linked chunks, otherwise false.
	 */
	bool acquire_linked_chunks_counted(chunk_type* chunk_base, chunk_index& head,
	std::size_t num_chunks_to_acquire, client_interface_type* client_interface_ptr);
	
	/// Release linked chunks - when the chunks are not owned by any client.
	/// A scheduler can use this function, but not a client.
	/**
	 * @param chunk_base Points to the first element of the array of chunks.
	 * @param head The head of the linked chunks, will upon return contain
	 *		chunk_type::LINK_TERMINATOR if successfully released all linked
	 *		chunks, otherwise it contains the chunk_index pointing to the head
	 *		of the linked chunks that are left.
	 * @param client_interface_ptr A pointer to the client_interface where the
	 *		chunk will be marked as not owned by any client.
	 * @return true if successfully released all linked chunks in which case
	 *		head is set to chunk_type::LINK_TERMINATOR, otherwise false.
	 */
	bool release_linked_chunks(chunk_type* chunk_base, chunk_index& head,
	client_interface_type* client_interface_ptr);
#endif
};

} // namespace core
} // namespace starcounter

#include "impl/chunk_pool.hpp"

#endif // STARCOUNTER_CORE_CHUNK_POOL_HPP
