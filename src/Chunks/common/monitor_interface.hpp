//
// monitor_interface.hpp
//
// Copyright © 2006-2013 Starcounter AB. All rights reserved.
// Starcounter® is a registered trademark of Starcounter AB.
//

#ifndef STARCOUNTER_CORE_MONITOR_INTERFACE_HPP
#define STARCOUNTER_CORE_MONITOR_INTERFACE_HPP

#if defined(_MSC_VER) && (_MSC_VER >= 1200)
# pragma once
#endif // defined(_MSC_VER) && (_MSC_VER >= 1200)

#include <cstdint>
#include <iostream> // debug
#include <string>
#include <cstring>
#include <cstddef>
#include <memory>
#include <set>
#include <boost/interprocess/managed_shared_memory.hpp>
#include <boost/interprocess/allocators/allocator.hpp>
#include <boost/interprocess/sync/interprocess_mutex.hpp>
#include <boost/interprocess/sync/scoped_lock.hpp>
#include <boost/interprocess/sync/interprocess_condition.hpp>
#include <boost/date_time/posix_time/posix_time_types.hpp> // No I/O just types.
#include <boost/date_time/microsec_time_clock.hpp>
#include <boost/thread/thread.hpp>
#include <boost/thread/win32/thread_primitives.hpp>
#include <boost/bind.hpp>
#if defined(_MSC_VER)
# define WIN32_LEAN_AND_MEAN
# include <windows.h> /// TODO: thread_primitives.hpp might replace this include
//# include <intrin.h>
# undef WIN32_LEAN_AND_MEAN
#endif // defined(_MSC_VER)
#include "../common/bit_operations.hpp"
#include "../common/pid_type.hpp"
#include "../common/owner_id.hpp"
#include "../common/config_param.hpp"
#include "../../Starcounter.ErrorCodes/scerrres/scerrres.h"
#include "../common/spinlock.hpp"
#include "../common/macro_definitions.hpp"

namespace starcounter {
namespace core {

class monitor_interface {
public:
	typedef char segment_name_type[segment_name_size];

	explicit monitor_interface();
	
	uint32_t register_database_process(pid_type pid, std::string segment_name,
	owner_id& oid, uint32_t timeout_milliseconds);

#if 0	
	uint32_t register_client_process(pid_type pid, owner_id& oid,
	uint32_t timeout_milliseconds);
#endif
	
	uint32_t unregister_database_process(pid_type pid, owner_id& oid,
	uint32_t timeout_milliseconds);

#if 0	
	uint32_t unregister_client_process(pid_type pid, owner_id& oid,
	uint32_t timeout_milliseconds);
#endif
	
	// The monitor thread registrar_ calls this function in order to wait for
	// processes to register and unregister. It obtains the parameters.
	void wait_for_registration();
	
	enum process_type {
		database_process,
#if 0
		client_process
#endif
	};
	
	enum operation {
		registration_request,
		unregistration_request,
#if 0
		shutdown
#endif
	};
	
	void set_pid(pid_type pid); // make it private?
	pid_type get_pid() const;
	
	void set_segment_name(std::string segment_name);
	std::string get_segment_name() const;
	
	void set_process_type(process_type pt);
	process_type get_process_type() const;
	
	void set_operation(operation op);
	operation get_operation() const;
	
	void set_owner_id(owner_id oid);
	owner_id get_owner_id() const;
	
	void out_data_is_available_notify_one() {
		out_.data_is_available_.notify_one();
	}
	
	void wait_until_ready();
	void is_ready_notify_all();
	
	void set_in_data_available_state(bool state);
	void set_out_data_available_state(bool state);

#if 0	
	/// insert_segment_name() is used by the monitor::wait_for_client_process_event()
	/// to insert segment names that are involved in cleanup tasks. The index in the
	/// table is set in the client_interface[] where cleanup is to be done by the
	/// scheduler(s). The scheduler that releases the last channel in a given
	/// client_interface[] will use that index to set the corresponding flag in the
	/// cleanup_mask_, and then set the ipc_monitor_cleanup_event_. This will wake
	/// up the monitor::cleanup() thread which will check the cleanup_mask_ and open
	/// the IPC shared memory segment with the name stored in the table corresponing
	/// to the bit index. When done it will update the cleanup_task data.
	/**
	 * @param segment_name The name of the IPC shared memory segment.
	 * @return The index in the array where the name was inserted on success, otherwise
	 *		-1 if could not insert the name.
	 */
	int32_t insert_segment_name(const char* segment_name);

	/// erase_segment_name() is used by the monitor::wait_for_database_process_event()
	/// to erase segment names of terminated database processes.
	/**
	 * @param segment_name The name of the IPC shared memory segment.
	 * @param ipc_monitor_cleanup_event Event to be reset if no more cleanup tasks.
	 */
	void erase_segment_name(const char* segment_name, HANDLE ipc_monitor_cleanup_event);

	/// get_a_segment_name() is used by the monitor::cleanup() thread trying to get
	/// a segment name, which it will use to open it and do the rest of the cleanup.
	/**
	 * @param ipc_monitor_cleanup_event Event to be reset if no more cleanup tasks.
	 * @return The segment_name, otherwise 0 if there are no segment names stored.
	 */
	const char* get_a_segment_name(HANDLE ipc_monitor_cleanup_event);

	void print_segment_name_list();

	/// set_cleanup_flag() is used by the last scheduler thread doing a cleanup task,
	/// to signal to the IPC monitor that it should start the cleanup of chunk(s) and
	/// client_interface(s). The schedulers have done their cleanup tasks related to
	/// this client process.
	/**
	 * @param index The index in the cleanup task table containing the segment name.
	 * @param ipc_monitor_cleanup_event Event to be reset if no more cleanup tasks.
	 * @return The segment_name, otherwise 0 if there are no segment names stored.
	 */
	void set_cleanup_flag(int32_t index, HANDLE ipc_monitor_cleanup_event);

	uint64_t get_cleanup_flag();

	/// Get reference to the spinlock.
	smp::spinlock& spinlock();
#endif

	class active_databases {
	public:	
		typedef char value_type[max_number_of_databases][database_name_size];
		typedef int32_t size_type;

		active_databases();

		void set_active_databases_set_update_event(HANDLE active_databases_set_update);

		/// The IPC monitor uses update() to insert or remove database names.
		/**
		 * @param databases A reference to a set of strings with the current active databases
		 *		which will be copied into the database_name_ array of c-strings in shared memory.
		 * @return The number of active databases (names.)
		 */
		size_type update(const std::set<std::string>& databases);

		/// The client uses copy() by passing in a reference to a set of strings
		/// that will be populated.
		/**
		 * @param databases A reference to a set of strings that upon return will contain
		 *		the names of the current active databases.
		 */
		void copy(std::set<std::string>& databases, HANDLE active_databases_set_update_event);
		
	private:
		/// Get reference to the spinlock.
		smp::spinlock& spinlock();

		value_type database_name_;
		
		// On multiple of cache-line boundary here.
		size_type size_;
		char cache_line_pad_0_[CACHE_LINE_SIZE -sizeof(size_type)]; // size_

		// spinlock_ synchronizes updates to active_databases_ and set/reset of the event.
		smp::spinlock spinlock_;
		char cache_line_pad_1_[CACHE_LINE_SIZE -sizeof(smp::spinlock)]; // spinlock_

		// Event to notify when the active databases list is updated.
		HANDLE active_databases_set_update_;
		char cache_line_pad_2_[CACHE_LINE_SIZE -sizeof(HANDLE)]; // active_databases_set_update_
	};

	active_databases& active_database_set() {
		return active_databases_;
	}

private:
	// Synchronization to check if the monitor_interface is ready or not.
	boost::interprocess::interprocess_mutex ready_mutex_;
	boost::interprocess::interprocess_condition is_ready_;
	bool is_ready() const;
	bool is_ready_flag_;

	// Synchronize access to the monitor_interface among all database- and
	// client processes. The monitor threads never locks this mutex.
	boost::interprocess::interprocess_mutex monitor_interface_mutex_;
	
	// Database- and client processes store the data they need to communicate to
	// the monitor in the in_ structure.
	struct in {
		in();
		
		// Synchronization between the process that registers and the monitor.
		boost::interprocess::interprocess_mutex mutex_;
		boost::interprocess::interprocess_condition data_is_available_;
		//bool is_data_available() const; /// TODO: Figure how to bind this.
		bool data_available_;
		
		// Both database- and client processes store their pid here.
		pid_type pid_;
		
		// A database process store it's segment_name here.
		char segment_name_[segment_name_size];
		
		// Type of process that is registering. The monitor uses it to decide to
		// which thread group to send the APC and if to store the segment_name.
		process_type process_type_;
		
		// Is the process registering or unregistering?
		operation operation_;
	} in_;
	
	// Hack until I can figure how to bind it.
	bool in_is_data_available() const;
	
	// The monitor store the data it need to the process that registers, in the
	// out_ structure.
	struct out {
		out();
		
		// Synchronization between the process that registers and the monitor.
		boost::interprocess::interprocess_mutex mutex_;
		boost::interprocess::interprocess_condition data_is_available_;
		//bool is_data_available() const; /// TODO: Figure how to bind this.
		bool data_available_;
		
		// The owner_id to be returned to the process that registers.
		owner_id owner_id_;
	} out_;
	
	// Hack until I can figure how to bind it.
	bool out_is_data_available() const;

#if 0
	struct cleanup_task {
		typedef uint64_t mask_type;

		cleanup_task();

		/// insert_segment_name() is used by the monitor::wait_for_client_process_event()
		/// to insert segment names that are involved in cleanup tasks. The index in the
		/// table is set in the client_interface[] where cleanup is to be done by the
		/// scheduler(s). The scheduler that releases the last channel in a given
		/// client_interface[] will use that index to set the corresponding flag in the
		/// cleanup_mask_, and then set the ipc_monitor_cleanup_event_. This will wake
		/// up the monitor::cleanup() thread which will check the cleanup_mask_ and open
		/// the IPC shared memory segment with the name stored in the table corresponing
		/// to the bit index. When done it will update the cleanup_task data.
		/**
		 * @param segment_name The name of the IPC shared memory segment.
		 * @return The index in the array where the name was inserted on success, otherwise
		 *		-1 if could not insert the name.
		 */
		int32_t insert_segment_name(const char* segment_name);
		
		/// erase_segment_name() is used by the monitor::wait_for_database_process_event()
		/// to erase segment names of terminated database processes.
		/**
		 * @param segment_name The name of the IPC shared memory segment.
		 * @param ipc_monitor_cleanup_event Event to be reset if no more cleanup tasks.
		 */
		void erase_segment_name(const char* segment_name, HANDLE ipc_monitor_cleanup_event);

		/// get_a_segment_name() is used by the monitor::cleanup() thread trying to get
		/// a segment name, which it will use to open it and do the rest of the cleanup.
		/**
		 * @param ipc_monitor_cleanup_event Event to be reset if no more cleanup tasks.
		 * @return The segment_name, otherwise 0 if there are no segment names stored.
		 */
		const char* get_a_segment_name(HANDLE ipc_monitor_cleanup_event);

		mask_type segment_name_mask() {
			return segment_name_mask_;
		}

		/// set_cleanup_flag() is used by the last scheduler thread doing a cleanup task,
		/// to signal to the IPC monitor that it should start the cleanup of chunk(s) and
		/// client_interface(s). The schedulers have done their cleanup tasks related to
		/// this client process.
		/**
		 * @param index The index in the cleanup task table containing the segment name.
		 * @param ipc_monitor_cleanup_event Event to be reset if no more cleanup tasks.
		 * @return The segment_name, otherwise 0 if there are no segment names stored.
		 */
		void set_cleanup_flag(int32_t index, HANDLE ipc_monitor_cleanup_event);

		uint64_t get_cleanup_flag();

		/// Get reference to the spinlock.
		smp::spinlock& spinlock() {
			return spinlock_;
		}

		// The segment_name_ table holds names of segments related to active cleanup tasks.
		segment_name_type segment_name_[max_number_of_databases];

		// The segment_name_mask_ flags segment_names related to active cleanup tasks.
		volatile mask_type segment_name_mask_;

		// The cleanup_mask_ flags segment_name_[s] that are ready to be
		// opened by the monitor::cleanup() thread and doing the rest of the
		// cleanup task.
		volatile mask_type cleanup_mask_;

		// spinlock_ synchronizes updates to cleanup_mask_ and set/reset of the event.
		smp::spinlock spinlock_;
	} cleanup_task_;
#endif

	active_databases active_databases_;
};

/// Exception class.
class monitor_interface_ptr_exception {
public:
	explicit monitor_interface_ptr_exception(int err);
	
	int error_code() const;
	
private:
	int err_;
};

/// class monitor_interface_ptr act as a smart pointer that opens a
/// monitor_interface and obtains a pointer to the shared structure.
class monitor_interface_ptr {
public:
	/// Constructor that open a monitor_interface. If it doesn't exist an
	/// exception is thrown with an error code.
	/**
	 * @param monitor_interface_name Has the format
	 *		<SERVER_NAME>_<MONITOR_INTERFACE_SUFFIX>
	 */
	monitor_interface_ptr(const char* monitor_interface_name = 0);
	
	void init(const char* monitor_interface_name, const uint32_t timeout_ms = 5000);

	monitor_interface_ptr(monitor_interface* ptr);
	
	/// Destructor.
	~monitor_interface_ptr();
	
	/// Dereferences the smart pointer.
	/**
	 * @return A reference to the shared structure.
	 */
	monitor_interface& operator*() const;
	
	/// Dereferences the smart pointer to get at a member of what it points to.
	/**
	 * @return A pointer to the shared structure.
	 */
	monitor_interface* operator->() const;
	
	/// Extract pointer.
	/**
	 * @return A pointer to the shared structure.
	 */
	monitor_interface* get() const;
	
private:
	/// The default copy constructor and assignment operator are made private.
	monitor_interface_ptr(monitor_interface_ptr&);
	monitor_interface_ptr& operator=(monitor_interface_ptr&);
	
	shared_memory_object shared_memory_object_;
	mapped_region mapped_region_;
	monitor_interface* ptr_;
};

} // namespace core
} // namespace starcounter

#include "impl/monitor_interface.hpp"

#endif // STARCOUNTER_CORE_MONITOR_INTERFACE_HPP

