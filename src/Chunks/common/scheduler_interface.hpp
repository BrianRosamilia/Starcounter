//
// scheduler_interface.hpp
//
// Copyright © 2006-2013 Starcounter AB. All rights reserved.
// Starcounter® is a registered trademark of Starcounter AB.
//

#ifndef STARCOUNTER_CORE_SCHEDULER_INTERFACE_HPP
#define STARCOUNTER_CORE_SCHEDULER_INTERFACE_HPP

#if defined(_MSC_VER) && (_MSC_VER >= 1200)
# pragma once
#endif // defined(_MSC_VER) && (_MSC_VER >= 1200)

#include <cstdint>
#include <cstddef>
#include <iostream> /// debug
#include <memory>
#include <boost/interprocess/managed_shared_memory.hpp>
#include <boost/interprocess/allocators/allocator.hpp>
#include <boost/interprocess/sync/interprocess_mutex.hpp>
#include <boost/interprocess/sync/scoped_lock.hpp>
#include <boost/interprocess/sync/interprocess_condition.hpp>
#include <boost/date_time/posix_time/posix_time_types.hpp> // No I/O just types.
#include <boost/date_time/microsec_time_clock.hpp>
#include <boost/thread/thread.hpp>
#include <boost/call_traits.hpp>
#include <boost/bind.hpp>
#define WIN32_LEAN_AND_MEAN
# include <windows.h>
# include <intrin.h>
#undef WIN32_LEAN_AND_MEAN
#include "../common/scheduler_number_pool.hpp"
#include "../common/channel.hpp"
#include "../common/channel_mask.hpp"
#include "../common/client_interface.hpp"
#include "../common/overflow_buffer.hpp"
#include "../common/chunk_pool.hpp"
#include "../common/spinlock.hpp"
#include "../common/owner_id.hpp"
#include "../common/macro_definitions.hpp"

namespace starcounter {
namespace core {

typedef uint32_t channel_scan_counter_type;

/// class scheduler_interface.
/**
 * @param T The type of the elements stored in the channel_number_queue.
 * @par Type Requirements T
 *		The T has to be SGIAssignable (SGI STL defined combination of Assignable
 *		and CopyConstructible), and EqualityComparable and/or LessThanComparable
 *		if the bounded_buffer will be compared with another container.
 * @param Alloc The allocator type used for all internal memory management.
 * @par Type Requirements Alloc
 *		The Alloc has to meet the allocator requirements imposed by STL.
 * @par Default Alloc
 *		std::allocator<T>
 */
template<class T, class T2,
class Alloc = std::allocator<T>, class Alloc2 = std::allocator<T2> >
class scheduler_interface {
public:
	// Basic types
	
	// The type of queue for channel_number.
	typedef scheduler_number_pool<T, channel_bits> channel_number_queue_type;
	
	// The type of queue for chunk_pool.
	typedef chunk_pool<T2, Alloc2> chunk_pool_type;
	
	// The type of queue for overflow_pool.
	typedef overflow_buffer<T2, Alloc2> overflow_pool_type;
	
	// The type of an allocator used in the channel_number_queue.
	//typedef typename channel_number_queue_type::allocator_type
	//channel_number_queue_allocator_type;
	
	// The type of an allocator used in the chunk_pool.
	typedef typename chunk_pool_type::allocator_type
	chunk_pool_allocator_type;
	
	// The type of an allocator used in the overflow_buffer.
	typedef typename overflow_pool_type::allocator_type
	overflow_pool_allocator_type;
	
	enum {
		channel_scan_mask_words = 4 // 4 * 64-bit = 256 (channels)
	};
	
	// Construction/Destruction.
	
	/// Construct a scheduler_interface.
	/**
	 * @param channel_number_queue_capacity The maximum number of elements which
	 *		can be stored in the bounded_buffer.
	 * @param channel_number_queue_alloc The channel_number_queue allocator.
	 * @throws "An allocation error" if memory is exhausted (std::bad_alloc if
	 *		the standard allocator is used).
	 */
	explicit scheduler_interface(
	std::size_t channel_number_queue_capacity,
	std::size_t chunk_pool_capacity,
	std::size_t overflow_pool_capacity,
	const chunk_pool_allocator_type& chunk_pool_alloc
	= chunk_pool_allocator_type(),
	const overflow_pool_allocator_type& overflow_pool_alloc
	= overflow_pool_allocator_type(),
	const char* segment_name = 0,
	const char* server_name = 0,
	int32_t id = -1)
	: channel_number_(segment_name, id),
	chunk_pool_(chunk_pool_capacity, chunk_pool_alloc),
	overflow_pool_(overflow_pool_capacity, overflow_pool_alloc),
	channel_scan_mask_(),
	channel_scan_counter_(0),
	notify_(false),
	predicate_(false),
	client_interface_(0),
	owner_id_(),
	ipc_monitor_cleanup_event_() {
		if (segment_name != 0) {
			char work_notify_name[segment_and_notify_name_size];
			std::size_t length;

			// Format: "Local\<segment_name>_notify_scheduler_<id>".
			if ((length = _snprintf_s(work_notify_name, _countof
			(work_notify_name), segment_and_notify_name_size -1 /* null */,
			"Local\\%s_notify_scheduler_%u", segment_name, id)) < 0) {
				return; // Throw exception error_code.
			}
			work_notify_name[length] = '\0';

			/// TODO: Fix insecure
			if ((length = mbstowcs(work_notify_name_, work_notify_name,
			segment_name_size)) < 0) {
				// Failed to convert work_notify_name to multi-byte string.
				return; // Throw exception error_code.
			}
			work_notify_name_[length] = L'\0';

			if ((work_ = ::CreateEvent(NULL, TRUE, FALSE, work_notify_name_))
			== NULL) {
				// Failed to create event.
				return; // Throw exception error_code.
			}
		}
		else {
			// Error: No segment name. Throw exception error_code.
		}

		if (server_name == 0) {
			// Error: No server name. Throw exception error_code.
		}

#if 0
		///=====================================================================
		/// Open the ipc_monitor_cleanup_event.
		///=====================================================================

		// Number of characters in the multibyte string after being converted.
		std::size_t length;

		// Construct the ipc_monitor_cleanup_event_name.
		char ipc_monitor_cleanup_event_name[ipc_monitor_cleanup_event_name_size];

		// Format: "Local\<server_name>_ipc_monitor_cleanup_event".
		// Example: "Local\PERSONAL_ipc_monitor_cleanup_event"
		if ((length = _snprintf_s(ipc_monitor_cleanup_event_name, _countof
		(ipc_monitor_cleanup_event_name), ipc_monitor_cleanup_event_name_size
		-1 /* null */, "Local\\%s_ipc_monitor_cleanup_event", server_name)) < 0) {
			return; // Throw exception error_code: "failed to format the ipc_monitor_cleanup_event_name"
		}
		ipc_monitor_cleanup_event_name[length] = '\0';

		wchar_t w_ipc_monitor_cleanup_event_name[ipc_monitor_cleanup_event_name_size];

		/// TODO: Fix insecure
		if ((length = mbstowcs(w_ipc_monitor_cleanup_event_name,
		ipc_monitor_cleanup_event_name, segment_name_size)) < 0) {
			// Failed to convert ipc_monitor_cleanup_event_name to multi-byte string.
			return; // Throw exception error_code: "failed to convert ipc_monitor_cleanup_event_name to multi-byte string"
		}
		w_ipc_monitor_cleanup_event_name[length] = L'\0';

		// Open the ipc_monitor_cleanup_event_name.
		if ((ipc_monitor_cleanup_event() = ::OpenEvent(SYNCHRONIZE | EVENT_MODIFY_STATE,
		FALSE, w_ipc_monitor_cleanup_event_name)) == NULL) {
			// Failed to open the event.
			unsigned int err = ::GetLastError();
			switch (err) {
			case 2:
				std::cout << "scheduler_interface::scheduler_interface(): "
				"Failed to open ipc_monitor_cleanup event. The system cannot find the file specified. "
				"OS error: " << err << "\n"; /// DEBUG
				break;
			case 6:
				std::cout << "scheduler_interface::scheduler_interface(): "
				"Failed to open ipc_monitor_cleanup event. The handle is invalid. "
				"OS error: " << err << "\n"; /// DEBUG
				break;
			
			default:
				std::cout << "scheduler_interface::scheduler_interface(): "
				"Failed to open ipc_monitor_cleanup event. OS error: " << err << "\n"; /// DEBUG
			}
			return; // TODO: Should throw an exception.
		}
		else {
			//std::cout << "Successfully opened the ipc_monitor_cleanup_event_name." << std::endl;
		}
#endif
	}
	
	~scheduler_interface() {
		::CloseHandle(work_);
	}
	
	void insert(channel_number the_channel_number, owner_id id, smp::spinlock::milliseconds timeout) {
		// Release the_channel_number to this channel_number_ queue.
		//channel_number_.push_front(the_channel_number);
		channel_number_.insert(the_channel_number, id, timeout);
	}

	void pop_back_channel_number(channel_number* the_channel_number, owner_id id) {
		channel_number_.acquire(the_channel_number, id, smp::spinlock::milliseconds(10000));
	}

	bool pop_back_channel_number(channel_number the_channel_number, owner_id id) {
		return channel_number_.acquire(the_channel_number, id, smp::spinlock::milliseconds(10000));
	}
	
	void push_front_channel_number(channel_number the_channel_number, owner_id id) {
		// Release the_channel_number to this channel_number_ queue.
		channel_number_.release(the_channel_number, id, smp::spinlock::milliseconds(10000));
	}
	
	/// Access to this schedulers channel_number queue. It is not synchronized,
	/// and can only be used by schedulers. It works without synchronization
	/// since scheduler threads are co-operative, so there is an indirect sync.
	channel_number_queue_type& channel_number_queue() {
		return channel_number_;
	}
	
	/// Access to this schedulers private chunk_pool. It is not synchronized,
	/// and can only be used by schedulers. It works without synchronization
	/// since scheduler threads are co-operative, so there is an indirect sync.
	chunk_pool_type& chunk_pool() {
		return chunk_pool_;
	}
	
	/// Access to this schedulers private overflow_pool. It is not synchronized,
	/// and can only be used by schedulers. It works without synchronization
	/// since scheduler threads are co-operative, so there is an indirect sync.
	overflow_pool_type& overflow_pool() {
		return overflow_pool_;
	}
	
	void set_channel_number_flag(channel_number the_channel_number) {
		channel_scan_mask_.set_channel_number_flag(the_channel_number);
	}
	
	void clear_channel_number_flag(channel_number the_channel_number) {
		channel_scan_mask_.clear_channel_number_flag(the_channel_number);
	}
	
	uint64_t get_channel_mask_word(std::size_t n) const {
		return channel_scan_mask_.get_channel_mask_word(n);
	}
	
	/// TODO: Remove this debug function.
	/**
	 * @param n Is the channel number the caller want to know if
	 *		it is owned (true) or not owned (false). The information may be
	 *		incorrect once it is returned. Use for debug output.
	 */
	bool is_channel_owner(std::size_t n) const {
		return channel_scan_mask_.get_channel_number_flag(n);
	}

	/// Get spin count.
	/**
	 * @return The spin_count.
	 */
	std::size_t get_spin_count() const {
		return channel_number_.get_spin_count();
	}
	
	//--------------------------------------------------------------------------
	// Each time a scan of a set of channels have been completed, regardless of
	// the number of channels that have been scanned, the scheduler calls
	// increment_channel_scan_counter(). The idéa is that when a client wants to
	// release a channel it want to make sure that the scheduler no longer will
	// access the channel. The scheduler must see that the channel_mask has been
	// updated by the client so that it no longer checks the channel.
	// One way to make sure this is the case is for the client to call
	// get_channel_scan_counter(), and then check the counter again sometime
	// later to see if the value has changed. When the client sees that the
	// counter has changed (since the counter may wrap, operator != is used),
	// then it knows for sure that the scheduler will no longer check the in
	// queue of the channel. But how do we guarantee that the scheduler will not
	// access the out queue of the channel?
	//--------------------------------------------------------------------------
	
	//the scheduler have seen the update of the channel_scan_mask_ and the client
	// can then release the channel. However, the scheduler may take a
	// relatively long time to complete a scan. We may enter a periodic sleep
	// of 1 ms and poll the get_channel_scan_counter() once every 1 ms.
	channel_scan_counter_type get_channel_scan_counter() const {
		return channel_scan_counter_;
	}
	
	// A scheduler calls increment_channel_scan_counter() each time it has
	// completed a scan of all channels in the channel_scan_mask_.
	void increment_channel_scan_counter() {
		channel_scan_counter_type next = channel_scan_counter_ +1;
		_mm_mfence();
		channel_scan_counter_ = next;
		_mm_mfence();
	}
	
	bool get_notify_flag() const {
		return notify_;
	}
	
	void set_notify_flag(bool s) {
		_mm_mfence();
		notify_ = s;
		_mm_mfence();
	}
	
	/// Clients call notify() each time they push a message to channel::in,
	/// pop a message from channel::out, or mark the channel for release,
	/// in order to wake up the scheduler it communicates with, if but only
	/// if it is waiting for work.
	/**
	 * @param work The named event that the client have opened.
	 */
	void notify(::HANDLE work) {
		if (get_notify_flag() == false) {
			// No need to notify the scheduler because it is not waiting.
			return;
		}
		else {
			if (::SetEvent(work)) {
				// Successfully notified the scheduler.
				return;
			}
			else {
				// Error. Failed to notify the scheduler.
				return;
			}
		}
	}

	/// Schedulers call notify() each time they push a message to a task or
	/// signal channel, in order to wake up the scheduler it communicates with,
	/// if but only if it is waiting for work.
	void notify() { notify(work_); }
	
	//--------------------------------------------------------------------------
	// The monitor call notify_scheduler_to_do_clean_up() if a client
	// process has crashed, in order to wake up the scheduler if it is waiting.
	// A scheduler that is woken up is required to see if the channel is marked
	// for clean-up. Maybe this is not at all required, it may be irrelevent.
	/// notify_scheduler_to_do_clean_up() is used by the monitor only.
	/**
	 * @param work The named event that the monitor have opened.
	 * @return true if successfully notified, otherwise false.
	 */
	bool notify_scheduler_to_do_clean_up(::HANDLE work) {
		if (::SetEvent(work)) {
			// Successfully notified the scheduler.
			return true;
		}
		// Error. Failed to notify the scheduler.
		return false;
	}
	
	// Setting predicate to true means the condition is met
	// and the wait is over, the thread waiting will not wait any more.
	// Setting the predicate to false means the condition is not met
	// and the thread waiting will keep waiting.
	// Access to the predicate must be synchronized.
	void set_predicate(bool state) {
		predicate_ = state;
	}
	
	bool do_work() const {
		return predicate_;
	}
	
	/// Scheduler's call wait_for_work() if they don't find any work to do.
	/// It is a "timed" function that can fail.
	/**
	 * @param timeout_milliseconds The number of milliseconds to wait before
	 *		a timeout may occur.
	 * @return false if the call is returning because the time period specified
	 *		by timeout_milliseconds has elapsed, true otherwise.
	 */
	bool wait_for_work(unsigned int timeout_milliseconds) {
		switch (::WaitForSingleObject(work_, timeout_milliseconds)) {
		case WAIT_OBJECT_0:
			// The scheduler was notified that there is work to do.
			
			if (::ResetEvent(work_)) {
			    // Successfully reset the event.
				return true;
			}
			else {
			    // Failed to reset the event. return success anyway.
				return true;
			}
			return true;
		case WAIT_TIMEOUT:
			// The scheduler was not notified. A timeout occurred.
			return false;
		case WAIT_FAILED:
			// The scheduler was not notified. An error occurred.
			return false;
		}
		return false;
	}
	
	uint64_t get_client_interface_as_qword() {
		boost::interprocess::scoped_lock
		<boost::interprocess::interprocess_mutex> lock(client_interface_mutex_);
		return client_interface_;
	}
	
	//--------------------------------------------------------------------------
	// Only one thread ever accesses this - the scheduler thread calls this once
	// to set the client_interface pointer. After that it is not changed for as
	// long as the scheduler owns this scheduler_interface. Therefore, no
	// synchronization is needed. Such assumptions are dangerous and must be
	// thought through, so in the mean time I use synchronization anyway.
	void set_client_interface(client_interface_type* p) {
		boost::interprocess::scoped_lock
		<boost::interprocess::interprocess_mutex> lock(client_interface_mutex_);
		client_interface_ = reinterpret_cast<uint64_t>(p);
	}

	client_interface_type *client_interface() {
		return reinterpret_cast<client_interface_type *>(client_interface_);
	}
	
	/// Get the work notify name, used to open the event. In order to reduce the
	/// time taken to open the work_ event the name is cached. Otherwise the
	/// work notify name have to be formated before opening it.
	/**
	 * @return A const wchar_t pointer to the work notify name string in the
	 *		format: L"Local\<segment_name>_notify_scheduler_<id>". For example:
	 *		L"Local\starcounter_PERSONAL_MYDB_64_notify_scheduler_9".
	 */
	const wchar_t* work_notify_name() const {
		return work_notify_name_;
	}
	
	channel_number_queue_type& channel_number() {
		return channel_number_;
	}

	void set_owner_id(owner_id oid) {
		owner_id_ = oid;
	}

	owner_id& get_owner_id() {
		return owner_id_;
	}

	/// Get a reference to the ipc_monitor_cleanup_event.
	/**
	 * @return A reference to the ipc_monitor_cleanup_event.
	 */ 
	::HANDLE& ipc_monitor_cleanup_event() {
		return ipc_monitor_cleanup_event_;
	}
	
	/// Get a const reference to the ipc_monitor_cleanup_event.
	/**
	 * @param A const reference to the ipc_monitor_cleanup_event.
	 */ 
	const ::HANDLE& ipc_monitor_cleanup_event() const {
		return ipc_monitor_cleanup_event_;
	}

private:
	// Condition to wait when all of this scheduler's channels in queues,
	// and the scheduler channels in queue are empty.
	::HANDLE work_;
	
	// Sync access to client_interface - probably not needed.
	boost::interprocess::interprocess_mutex client_interface_mutex_;
	channel_number_queue_type channel_number_;
	chunk_pool_type chunk_pool_;
	overflow_pool_type overflow_pool_;
	char cache_line_pad_0_[CACHE_LINE_SIZE]; // Not needed if on another cache line.
	channel_mask<channels> channel_scan_mask_;
	char cache_line_pad_1_[CACHE_LINE_SIZE];
	volatile channel_scan_counter_type channel_scan_counter_;
	char cache_line_pad_2_[CACHE_LINE_SIZE -sizeof(channel_scan_counter_type)];
	volatile bool notify_;
	char cache_line_pad_3_[CACHE_LINE_SIZE -sizeof(bool)];
	volatile bool predicate_;
	char cache_line_pad_4_[CACHE_LINE_SIZE -sizeof(bool)];
	
	// The scheduler that have allocated this interface must store a pointer to
	// the client_interface here, which will be relative to its own address
	// space. This must be done before the client have a chance to access this
	// scheduler_interface. The client will copy this pointer to the channel
	// when a client allocates a channel.
	//
	// We store the pointer as uint64_t to provide compatibility between 64-bit
	// server and 32-bit client.
	uint64_t client_interface_; // client_interface_type*
	owner_id owner_id_;
	
	// Event to notify the monitor to do cleanup.
	::HANDLE ipc_monitor_cleanup_event_;
	
	char cache_line_pad_5_[CACHE_LINE_SIZE
	-sizeof(uint64_t) // client_interface_
	-sizeof(owner_id) // owner_id_
	-sizeof(::HANDLE) // ipc_monitor_cleanup_event_
	];

	// In order to reduce the time taken to open the work_ event the name is
	// cached. Otherwise the name have to be formated before opening it.
	wchar_t work_notify_name_[segment_and_notify_name_size];
	
	// In order to reduce the time taken to open the ipc_monitor_clean_up_event_
	// the name is cached. Otherwise the name have to be formated before opening it.
	//wchar_t ipc_monitor_clean_up_event_name_[segment_and_notify_name_size];
};

typedef starcounter::core::simple_shared_memory_allocator<channel_number>
shm_alloc_for_the_scheduler_interfaces2;

typedef starcounter::core::simple_shared_memory_allocator<chunk_index>
shm_alloc_for_the_scheduler_interfaces2b;

typedef scheduler_interface<channel_number, chunk_index,
shm_alloc_for_the_scheduler_interfaces2,
shm_alloc_for_the_scheduler_interfaces2b>
scheduler_interface_type;

} // namespace core
} // namespace starcounter

//#include "impl/scheduler_interface.hpp"

#endif // STARCOUNTER_CORE_SCHEDULER_INTERFACE_HPP
