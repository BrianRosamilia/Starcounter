//
// client_number_pool.hpp
//
// Copyright © 2006-2012 Starcounter AB. All rights reserved.
// Starcounter® is a registered trademark of Starcounter AB.
//

#ifndef STARCOUNTER_CORE_CLIENT_NUMBER_POOL_HPP
#define STARCOUNTER_CORE_CLIENT_NUMBER_POOL_HPP

#if defined(_MSC_VER) && (_MSC_VER >= 1200)
# pragma once
#endif // defined(_MSC_VER) && (_MSC_VER >= 1200)

#include "macro_definitions.hpp"

#include <cstddef>
#include <boost/call_traits.hpp>
#if defined(_MSC_VER) // Windows
# include <intrin.h>
// Declaring interlocked functions for use as intrinsics.
# pragma intrinsic (_InterlockedIncrement)
# pragma intrinsic (_InterlockedDecrement)
#else
# error Compiler not supported.
#endif // (_MSC_VER)
#include "bit_operations.hpp"
#include "scheduler_number.hpp"
#include "spinlock.hpp"
#include "macro_definitions.hpp"

namespace starcounter {
namespace core {

/// class client_number_pool
/**
 * @param T The type of the elements stored in the client_number_pool.
 * @param N The buffer capacity will be 2^N. If N is 6, the capacity is 64, etc.
 */
template<class T, std::size_t N>
class client_number_pool {
public:
	// Basic types

	// The type of elements stored in the client_number_pool.
	typedef T value_type;

	// A pointer to an element.
	typedef T* iterator;
	
	// A const pointer to an element.
	typedef const T* const_iterator;
	
	// A reference to an element.
	typedef T& reference;
	
	// A const reference to an element.
	typedef const T& const_reference;
	
	// The size type. (An unsigned integral type that can represent any non-
	// negative value of the container's distance type.)
	typedef std::size_t size_type;
	
	// The distance type. (A signed integral type used to represent the distance
	// between two iterators.)
	typedef std::ptrdiff_t difference_type;

	// Helper types
	
	// A type representing the "best" way to pass the value_type to a method.
	typedef typename boost::call_traits<value_type>::param_type param_type;
	
	// A type representing the "best" way to return the value_type from a const method.
	typedef typename boost::call_traits<value_type>::param_type return_type;
	
	typedef uint64_t mask_type;
	
	enum {
		buffer_capacity = 1 << N,
		mask_bits = sizeof(mask_type) * CHAR_BIT,
		masks = (buffer_capacity +mask_bits -1) / mask_bits
	};

	// Construction/Destruction.
	
	/// Create an empty client_number_pool.
	/**
	 * @param segment_name The name of the database IPC shared memory segment.
	 */
	explicit client_number_pool(const char* segment_name);
	
	// Size and capacity
	
	/// Get the number of elements currently stored in the client_number_pool.
	/**
	 * @return The number of elements stored in the client_number_pool.
	 *		This is the number of unread elements.
	 */
	size_type size() const;
	
	/// Get the capacity of the client_number_pool.
	/**
	 * @return The maximum number of elements which can be stored in the
	 *		client_number_pool.
	 * @throws Nothing.
	 * @par Exception Safety
	 *		No-throw.
	 */
	size_type capacity() const;
	
	/// Is the client_number_pool empty?
	/**
	 * @return true if there are no elements stored in the client_number_pool;
	 *		false otherwise.
	 * @throws Nothing.
	 * @par Exception Safety
	 *		No-throw.
	 */
	bool empty() const;
	
	/// Is the client_number_pool full?
	/**
	 * @return true if the number of elements stored in the client_number_pool
	 *		equals the capacity of the client_number_pool; false otherwise.
	 * @throws Nothing.
	 * @par Exception Safety
	 *		No-throw.
	 */
	bool full() const;
	
	/// Insert an item to the container.
	/**
	 * @param item The item to be inserted to the container.
	 *		param_type represents the "best" way to pass a parameter of type
	 *		value_type to a method.
	 * @return true if the item was inserted, false otherwise.
	 */
	bool insert(value_type item, client_interface_type* client_interface_base,
	owner_id id, smp::spinlock::milliseconds timeout);
	
	/// Erase an item from the container.
	/**
	 * @param item The item to be reased from the container.
	 *		param_type represents the "best" way to pass a parameter of type
	 *		value_type to a method.
	 * @return true if the item was reased, false otherwise.
	 */
	bool erase(value_type item, client_interface_type* client_interface_base,
	owner_id id, smp::spinlock::milliseconds timeout);
	
	/// Acquire an item from the container.
	/**
	 * @param item The item to be acquired from the container.
	 *		param_type represents the "best" way to pass a parameter of type
	 *		value_type to a method.
	 * @param id The owner's id.
	 * @return true if the item was acquired, false otherwise.
	 */
	bool acquire(value_type* item, client_interface_type* client_interface_base,
	owner_id id, smp::spinlock::milliseconds timeout);

	bool acquire(value_type item, client_interface_type* client_interface_base,
	owner_id id, smp::spinlock::milliseconds timeout);
	
	/// Release an item to the container.
	/**
	 * @param item The item to be released to the container.
	 *		param_type represents the "best" way to pass a parameter of type
	 *		value_type to a method.
	 * @param id The owner's id.
	 * @return true if the item was released, false otherwise.
	 */
	bool release(value_type item, client_interface_type* client_interface_base,
	owner_id id, smp::spinlock::milliseconds timeout);
	
	/// if_locked_with_id_recover_and_unlock() is only used by the IPC monitor during recovery.
	// The spinlock must be in the locked state while doing this.
	bool if_locked_with_id_recover_and_unlock(smp::spinlock::locker_id_type id);

	/// Get the not_empty_notify_name, used to open the event. In order to
	/// reduce the time taken to open the not_empty_notify_name event the name
	/// is cached. Otherwise the not_empty_notify_name have to be formated
	/// before opening it.
	/**
	 * @return A const wchar_t pointer to the not_empty_notify_name string in the
	 *		format: L"Local\<segment_name>_scheduler_number_pool_<scheduler_num>_not_empty".
	 *		For example:
	 *		L"Local\starcounter_PERSONAL_MYDB_64_scheduler_number_pool_9__not_empty".
	 */
	const wchar_t* not_empty_notify_name() const {
		return not_empty_notify_name_;
	}
	
	/// Get the not_full_notify_name, used to open the event. In order to
	/// reduce the time taken to open the not_full_notify_name event the name
	/// is cached. Otherwise the not_full_notify_name have to be formated
	/// before opening it.
	/**
	 * @return A const wchar_t pointer to the not_full_notify_name string in the
	 *		format: L"Local\<segment_name>_scheduler_number_pool_<scheduler_num>_not_full".
	 *		For example:
	 *		L"Local\starcounter_PERSONAL_MYDB_64_scheduler_number_pool_9__not_full".
	 */
	const wchar_t* not_full_notify_name() const {
		return not_full_notify_name_;
	}

private:
	client_number_pool(const client_number_pool&);
	client_number_pool& operator=(const client_number_pool&);
	
	// iterator support
	iterator begin() {
		return elem_;
	}
	
	const_iterator begin() const {
		return elem_;
	}
	
	const_iterator cbegin() const {
		return elem_;
	}
	
	iterator end() {
		return elem_ +buffer_capacity;
	}
	
	const_iterator end() const {
		return elem_ +buffer_capacity;
	}
	
	const_iterator cend() const {
		return elem_ +buffer_capacity;
	}
	
	/// Get reference to the spinlock.
	smp::spinlock& spinlock() {
		return spinlock_;
	}

	bool is_not_empty() const;
	bool is_not_full() const;
	
	/// Clear the buffer.
	void clear_buffer() {
		for (iterator it = begin(); it != end(); ++it) {
			*it = 0;
		}
	}

	/// Clear the mask.
	void clear_mask() {
		for (std::size_t i = 0; i < masks; ++i) {
			mask_[i] = 0;
		}
	}

	/// adjust_mask() is only used by the IPC monitor during recovery.
	// The spinlock must be in the locked state while doing this.
	void adjust_mask();
	
	/// adjust_size() is only used by the IPC monitor during recovery.
	// The spinlock must be in the locked state while doing this.
	void adjust_size();
	
	value_type elem_[buffer_capacity];
	
	// A mask is used to find free items quicker than a linear search.
	mask_type mask_[masks];

	size_type size_;
	//container_type container_;
	
	// IPC synchronization:
	
	// SMP spinlock to protect access to the queue.
	smp::spinlock spinlock_;
	
	// Event used by the scheduler to wait when the queue is not empty.
	// Client's have to open the event and pass it in as an argument.
	HANDLE not_empty_;
	
	// Event used by the scheduler to wait when the queue is not full.
	// Client's have to open the event and pass it in as an argument.
	HANDLE not_full_;

	// In order to reduce the time taken to open the not_empty_ and not_full_
	// events the names are cached. Otherwise the names have to be formated
	// before opening them.
	wchar_t not_empty_notify_name_[segment_and_notify_name_size];
	wchar_t not_full_notify_name_[segment_and_notify_name_size];
};

} // namespace core
} // namespace starcounter

#include "impl/client_number_pool.hpp"

#endif // STARCOUNTER_CORE_CLIENT_NUMBER_POOL_HPP
