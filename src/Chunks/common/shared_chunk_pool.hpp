//
// shared_chunk_pool.hpp
//
// Copyright © 2006-2013 Starcounter AB. All rights reserved.
// Starcounter® is a registered trademark of Starcounter AB.
//

#ifndef STARCOUNTER_CORE_SHARED_CHUNK_POOL_HPP
#define STARCOUNTER_CORE_SHARED_CHUNK_POOL_HPP

#if defined(_MSC_VER) && (_MSC_VER >= 1200)
# pragma once
#endif // defined(_MSC_VER) && (_MSC_VER >= 1200)

// Use -DBOOST_CB_DISABLE_DEBUG or -DNDEBUG flags
#define BOOST_CB_DISABLE_DEBUG

#include <cstdint>
#include <cstddef>
#include <memory>
#include <ios> /// For debug - remove.
#include <iomanip> /// For debug - remove.
#include <boost/circular_buffer.hpp>
#if defined(_MSC_VER) // Windows
# include <windows.h>
#else
# error Compiler not supported.
#endif // (_MSC_VER)
#include "spinlock.hpp"
#include <boost/interprocess/sync/interprocess_mutex.hpp>
#include <boost/interprocess/sync/scoped_lock.hpp>
#include <boost/interprocess/sync/interprocess_condition.hpp>
#include <boost/date_time/posix_time/posix_time_types.hpp> // No I/O just types.
#include <boost/date_time/microsec_time_clock.hpp>
#include <boost/thread/thread.hpp>
#include <boost/call_traits.hpp>
#include <boost/bind.hpp>
#include "../common/interprocess.hpp"
#include "../common/name_definitions.hpp"
#include "../common/chunk.hpp"
#include "../common/chunk_pool.hpp"
#include "../common/client_interface.hpp"
#include "../common/macro_definitions.hpp"

namespace starcounter {
namespace core {

/// class shared_chunk_pool
/**
 * @param T The type of the elements stored in the shared_chunk_pool.
 * @par Type Requirements T
 *		The T has to be SGIAssignable (SGI STL defined combination of Assignable
 *		and CopyConstructible), and EqualityComparable and/or LessThanComparable
 *		if the shared_chunk_pool will be compared with another container.
 * @param Alloc The allocator type used for all internal memory management.
 * @par Type Requirements Alloc
 *		The Alloc has to meet the allocator requirements imposed by STL.
 * @par Default Alloc
 *		std::allocator<T>
 */
template<class T, class Alloc = std::allocator<T> >
class shared_chunk_pool {
public:
	// Basic types
	
	// The type of the underlying container used in the shared_chunk_pool.
	typedef boost::circular_buffer<T, Alloc> container_type;
	
	// The type of elements stored in the shared_chunk_pool.
	typedef typename container_type::value_type value_type;
	
	// A pointer to an element.
	typedef typename container_type::pointer pointer;
	
	// A const pointer to the element.
	typedef typename container_type::const_pointer const_pointer;
	
	// A reference to an element.
	typedef typename container_type::reference reference;
	
	// A const reference to an element.
	typedef typename container_type::const_reference const_reference;
	
	// The distance type. (A signed integral type used to represent the distance
	// between two iterators.)
	typedef typename container_type::difference_type difference_type;
	
	// The size type. (An unsigned integral type that can represent any non-
	// negative value of the container's distance type.)
	typedef typename container_type::size_type size_type;
	
	// The type of an allocator used in the shared_chunk_pool.
	//typedef Alloc allocator_type;
	typedef typename container_type::allocator_type allocator_type;
	
	// Helper types
	
	// A type representing the "best" way to pass the value_type to a method.
	typedef typename boost::call_traits<value_type>::param_type param_type;
	
	// A type representing the "best" way to return the value_type from a const
	// method.
	typedef typename boost::call_traits<value_type>::param_type return_type;
	
	// Construction/Destruction.
	
	/// Create an empty shared_chunk_pool with the specified capacity.
	/**
	 * @param buffer_capacity The maximum number of elements which can be stored
	 *		in the shared_chunk_pool.
	 * @param alloc The allocator.
	 * @throws "An allocation error" if memory is exhausted (std::bad_alloc if
	 *		the standard allocator is used).
	 * @par Complexity
	 *		Constant.
	 */
	explicit shared_chunk_pool(const char* segment_name, size_type buffer_capacity,
	const allocator_type& alloc = allocator_type());
	
	// Size and capacity
	
	/// Get the number of elements currently stored in the shared_chunk_pool.
	/**
	 * @return The number of elements stored in the shared_chunk_pool.
	 *		This is the number of unread elements.
	 * @throws Nothing.
	 * @par Exception Safety
	 *		No-throw.
	 * @par Complexity
	 *		Constant (in the size of the shared_chunk_pool).
	 */
	size_type size() const;
	
	/// Get the capacity of the shared_chunk_pool.
	/**
	 * @return The maximum number of elements which can be stored in the
	 *		shared_chunk_pool.
	 * @throws Nothing.
	 * @par Exception Safety
	 *		No-throw.
	 * @par Complexity
	 *		Constant (in the size of the shared_chunk_pool).
	 */
	size_type capacity() const;
	
	/// Is the shared_chunk_pool empty?
	/**
	 * @return true if there are no elements stored in the shared_chunk_pool;
	 *		false otherwise.
	 * @throws Nothing.
	 * @par Exception Safety
	 *		No-throw.
	 * @par Complexity
	 *		Constant (in the size of the shared_chunk_pool).
	 */
	//bool empty() const;
	
	/// Is the shared_chunk_pool full?
	/**
	 * @return true if the number of elements stored in the shared_chunk_pool
	 *		equals the capacity of the shared_chunk_pool; false otherwise.
	 * @throws Nothing.
	 * @par Exception Safety
	 *		No-throw.
	 * @par Complexity
	 *		Constant (in the size of the shared_chunk_pool).
	 */
	//bool full() const;
	
	/// Acquire linked chunks that fit the requested size - for schedulers and
	/// clients.
	/**
	 * @param chunk_base Points to the first element of the array of chunks.
	 * @param head Will upon return contain the head of a linked chain of chunks
	 *		if successfuly acquired the requested size.
	 * @param size The number of bytes to allocate as 1..N linked chunks. The
	 *		chunks require some space for header data and this is taken into
	 *		account.
	 * @param client_interface_ptr A pointer to the client_interface where the
	 *		chunk will be marked as owned by the client.
	 * @param timeout The number of milliseconds to wait before a
	 *		timeout may occur.
	 * @return true if successfully acquired the linked chunks with the
	 *		requested amount of memory before the time period specified by
	 *		timeout has elapsed, otherwise false if not enough
	 *		space or the time period has elapsed.
	 */
	bool acquire_linked_chunks(chunk_type* chunk_base, chunk_index& head,
	std::size_t size, client_interface_type* client_interface_ptr,
	smp::spinlock::milliseconds timeout = 10000);
	
	/// Acquire linked chunks that fit the requested size - for schedulers and
	/// clients.
	/**
	 * @param chunk_base Points to the first element of the array of chunks.
	 * @param head Will upon return contain the head of a linked chain of chunks
	 *		if successfuly acquired the requested size.
	 * @param num_chunks_to_acquire The number of chunks to allocate.
	 * @param client_interface_ptr A pointer to the client_interface where the
	 *		chunk will be marked as owned by the client.
	 * @param timeout The number of milliseconds to wait before a
	 *		timeout may occur.
	 * @return true if successfully acquired the linked chunks with the
	 *		requested amount of memory before the time period specified by
	 *		timeout has elapsed, otherwise false if not enough
	 *		space or the time period has elapsed.
	 */
	bool acquire_linked_chunks_counted(chunk_type* chunk_base, chunk_index& head,
	std::size_t num_chunks_to_acquire, client_interface_type* client_interface_ptr,
	smp::spinlock::milliseconds timeout = 10000);
	
	/// Release linked chunks - for schedulers and clients.
	/// NOTE: After calling this, the message data in the linked chunks may be
	/// unreadable even if unsuccessfull when trying to release the linked
	/// chunks, because some chunks may have been released and thus the message
	/// data may be cut.
	/**
	 * @param chunk_base Points to the first element of the array of chunks.
	 * @param head The head of the linked chunks, will upon return contain
	 *		chunk_type::link_terminator if successfully released all linked
	 *		chunks, otherwise it contains the chunk_index pointing to the head
	 *		of the linked chunks that are left.
	 * @param client_interface_ptr A pointer to the client_interface where the
	 *		chunk will be marked as not owned by any client.
	 * @param timeout_milliseconds The number of milliseconds to wait before a
	 *		timeout may occur.
	 * @return true if successfully released all linked chunks in which case
	 *		head is set to chunk_type::link_terminator, otherwise returns false
	 *		if failed to release all linked chunks, or the time period has
	 *		elapsed.
	 */
	bool release_linked_chunks(chunk_type* chunk_base, chunk_index& head,
	client_interface_type* client_interface_ptr,
	smp::spinlock::milliseconds timeout = 10000);
	
	//--------------------------------------------------------------------------
	/// Clients acquire chunks to a private chunk_pool, by moving chunks from
	/// the shared_chunk_pool to the private_chunk_pool. The chunks are marked
	/// as owned by the client.
	/**
	 * @param private_chunk_pool Reference to the private_chunk_pool to which
	 *		chunks are allocated.
	 * @param chunks_to_acquire The number of chunks to acquire.
	 * @param client_interface_ptr A pointer to the client_interface where the
	 *		chunk(s) will be marked as owned by the client.
	 * @param timeout The number of milliseconds to wait before a
	 *		timeout may occur while trying to lock the shared_chunk_pool.
	 * @return The number of acquired chunks. If the private_chunk_pool is full
	 *		or becomes full when acquiring chunks, the acquirement process is
	 *		stopped. This means that less than chunks_to_acquire was acquired.
	 */
	template<typename U>
	std::size_t acquire_to_chunk_pool(U& private_chunk_pool, std::size_t
	chunks_to_acquire, client_interface_type* client_interface_ptr,
	smp::spinlock::milliseconds timeout = 10000);
	
	/// Clients acquire chunks to a private chunk_pool, by moving chunks from
	/// the shared_chunk_pool to the private_chunk_pool. The chunks are marked
	/// as owned by the client.
	/**
	 * @param private_chunk_pool Reference to the private_chunk_pool to which
	 *		chunks are allocated.
	 * @param chunks_to_acquire The number of chunks to acquire.
	 * @param client_interface_ptr A pointer to the client_interface where the
	 *		chunk(s) will be marked as owned by the client.
	 * @param timeout The number of milliseconds to wait before a
	 *		timeout may occur while trying to lock the shared_chunk_pool.
	 * @return The number of acquired chunks. If the private_chunk_pool is full
	 *		or becomes full when acquiring chunks, the acquirement process is
	 *		stopped. This means that less than chunks_to_acquire was acquired.
	 */
	template<typename U>
	std::size_t acquire(U& private_chunk_pool, std::size_t
	chunks_to_acquire, client_interface_type* client_interface_ptr,
	smp::spinlock::milliseconds timeout = 10000);

	/// Clients release chunks from a private_chunk_pool, by moving chunks from
	/// the private_chunk_pool to the shared_chunk_pool. The chunks are marked
	/// as not owned by the client.
	/// shared_interface::release_from_private_to_shared() calls this one.
	/**
	 * @param private_chunk_pool Reference to the private_chunk_pool from which
	 *		chunks are released.
	 * @param chunks_to_release The number of chunks to release.
	 * @param client_interface_ptr A pointer to the client_interface where the
	 *		chunk(s) will be marked as not owned by the client.
	 * @param timeout The number of milliseconds to wait before a
	 *		timeout may occur while trying to lock the shared_chunk_pool.
	 * @return The number of released chunks.
	 */
	template<typename U>
	std::size_t release_from_chunk_pool(U& private_chunk_pool, std::size_t
	chunks_to_release, client_interface_type* client_interface_ptr,
	smp::spinlock::milliseconds timeout = 10000);
	
	/// Clients release chunks from a private_chunk_pool, by moving chunks from
	/// the private_chunk_pool to the shared_chunk_pool. The chunks are marked
	/// as not owned by the client.
	/// shared_interface::release_to_shared_chunk_pool() calls this one.
	/**
	 * @param private_chunk_pool Reference to the private_chunk_pool from which
	 *		chunks are released.
	 * @param chunks_to_release The number of chunks to release.
	 * @param client_interface_ptr A pointer to the client_interface where the
	 *		chunk(s) will be marked as not owned by the client.
	 * @param timeout The number of milliseconds to wait before a
	 *		timeout may occur while trying to lock the shared_chunk_pool.
	 * @return The number of released chunks.
	 */
	template<typename U>
	std::size_t release(U& private_chunk_pool, std::size_t chunks_to_release,
	client_interface_type* client_interface_ptr, smp::spinlock::milliseconds
	timeout);

	//--------------------------------------------------------------------------
	/// Schedulers acquire chunks to a private chunk_pool, by moving chunks from
	/// the shared_chunk_pool to the private_chunk_pool.
	/**
	 * @param private_chunk_pool Reference to the chunk_pool to which chunks are
	 *		allocated.
	 * @param chunks_to_acquire The number of chunks to acquire.
	 * @param timeout The number of milliseconds to wait before a
	 *		timeout may occur while trying to lock the shared_chunk_pool.
	 * @return The number of acquired chunks. If the private_chunk_pool is full
	 *		or becomes full when acquiring chunks, the acquirement process is
	 *		stopped. This means that less than chunks_to_acquire was acquired.
	 */
	template<typename U>
	std::size_t acquire_to_chunk_pool(U& private_chunk_pool, std::size_t
	chunks_to_acquire, smp::spinlock::milliseconds timeout = 10000);
	
	/// Schedulers release chunks from a private_chunk_pool, by moving chunks
	/// from the private_chunk_pool to the shared_chunk_pool.
	/**
	 * @param private_chunk_pool Reference to the private chunk_pool from which
	 *		chunks are released.
	 * @param chunks_to_release The number of chunks to release.
	 * @param timeout The number of milliseconds to wait before a
	 *		timeout may occur while trying to lock the shared_chunk_pool.
	 * @return The number of released chunks.
	 */
	template<typename U>
	std::size_t release_from_chunk_pool(U& private_chunk_pool, std::size_t
	chunks_to_release, smp::spinlock::milliseconds timeout = 10000);

#if 0	
	//--------------------------------------------------------------------------
	/// client_release_linked_chunks() is used by the scheduler to do the clean
	/// up, releasing chunks of a client_interface. The pointer to the
	/// client_interface is obtained via a channel and must belong to a client
	/// that has terminated. This function is only used during clean up.
	/**
	 * @param client_interface_ptr A pointer to the client_interface where the
	 *		chunk is marked as owned.
	 * @param timeout The number of milliseconds to wait before a
	 *		timeout may occur. TODO: implement timeout!?
	 * @return false if failing to release the chunk_index. It can happen if the
	 *		lock of the queue was not obtained.
	 */
	bool release_clients_chunks(client_interface_type* client_interface_ptr,
	smp::spinlock::milliseconds timeout = 10000);
#endif
	
	//--------------------------------------------------------------------------
	// For debug
	void show_linked_chunks(chunk_type* chunk_, chunk_index head);
	
	//--------------------------------------------------------------------------
	// IMPORTANT NOTE: Only a scheduler may use push_front() and pop_back()
	// directly during initialization of the shared_chunk_pool. Clients aren't
	// allowed to use these functions, because they don't mark chunks as owned.
	// Some other API functions in this class are allowed to use them.
	
	/// Push item to the front of the queue.
	/**
	 * @param item The item to be pushed to the front of the queue.
	 *		param_type represents the "best" way to pass a parameter of type
	 *		value_type to a method.
	 * @param spin_count The number of times to try locking (without blocking)
	 *		before waiting for the acquisition of the lock. It is affected by
	 *		the type of processor and the clock rate, etc.
	 * @param timeout The number of milliseconds to wait before a
	 *		timeout may occur.
	 * @param not_empty The not_empty event need to be passed in by any process
	 *		except the database.
	 * @return false if failing to push the item before the time period
	 *		specified by timeout has elapsed, true otherwise.
	 */
	bool push_front(param_type item, uint32_t spin_count,
	smp::spinlock::milliseconds timeout = 10000);
	
	//--------------------------------------------------------------------------
	// IMPORTANT NOTE: Only a scheduler may use try_push_front() and
	// try_pop_back() directly during initialization of the shared_chunk_pool.
	// Clients aren't allowed to use these functions, because they don't mark
	// chunks as owned. Some other API functions in this class are allowed to
	// use them.

	bool try_push_front(param_type item);
	
	//--------------------------------------------------------------------------
	
	/// Debug function.
	std::size_t unread() const {
		return unread_;
	}
	
	/// Get reference to the spinlock.
	smp::spinlock& spinlock() {
		return spinlock_;
	}

	/// if_locked_with_id_recover_and_unlock() is only used by the IPC monitor during recovery.
	// The spinlock must be in the locked state while doing this.
	bool if_locked_with_id_recover_and_unlock(smp::spinlock::locker_id_type id);
	
	/// Get the not_empty_notify_name, used to open the event. In order to
	/// reduce the time taken to open the not_empty_notify_name event the name
	/// is cached. Otherwise the not_empty_notify_name have to be formated
	/// before opening it.
	/**
	 * @return A const wchar_t pointer to the not_empty_notify_name string in the
	 *		format: L"Local\<segment_name>_shared_chunk_pool_not_empty".
	 *		For example:
	 *		L"Local\starcounter_PERSONAL_MYDB_64_shared_chunk_pool_not_empty".
	 */
	const wchar_t* not_empty_notify_name() const;
	
	/// Get the not_full_notify_name, used to open the event. In order to
	/// reduce the time taken to open the not_full_notify_name event the name
	/// is cached. Otherwise the not_full_notify_name have to be formated
	/// before opening it.
	/**
	 * @return A const wchar_t pointer to the not_full_notify_name string in the
	 *		format: L"Local\<segment_name>_shared_chunk_pool_not_full".
	 *		For example:
	 *		L"Local\starcounter_PERSONAL_MYDB_64_shared_chunk_pool_not_full".
	 */
	const wchar_t* not_full_notify_name() const;

private:
	shared_chunk_pool(const shared_chunk_pool&);
	shared_chunk_pool& operator=(const shared_chunk_pool&);
	
	bool is_not_empty() const;
	bool is_not_full() const;
	
	size_type unread_;
	container_type container_;
	
	// IPC synchronization:
	
	// SMP spinlock to protect access to the queue.
	smp::spinlock spinlock_;
	char cache_line_pad_0_[CACHE_LINE_SIZE -sizeof(smp::spinlock)];
};

typedef simple_shared_memory_allocator<chunk_index>
shm_alloc_for_the_shared_chunk_pool2;

typedef shared_chunk_pool
<chunk_index, shm_alloc_for_the_shared_chunk_pool2>
shared_chunk_pool_type;

} // namespace core
} // namespace starcounter

#include "impl/shared_chunk_pool.hpp"

#endif // STARCOUNTER_CORE_SHARED_CHUNK_POOL_HPP
