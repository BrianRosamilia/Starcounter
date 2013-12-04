//
// monitor.hpp
//
// Copyright © 2006-2012 Starcounter AB. All rights reserved.
// Starcounter® is a registered trademark of Starcounter AB.
//

#ifndef STARCOUNTER_CORE_MONITOR_HPP
#define STARCOUNTER_CORE_MONITOR_HPP

#if defined(_MSC_VER) && (_MSC_VER >= 1200)
# pragma once
#endif // defined(_MSC_VER) && (_MSC_VER >= 1200)

#include <ctime>
#include <map> /// TODO: map or unordered_map
#include <set>
#include <iostream>
#include <ios>
#include <fstream>
#include <string>
#include <sstream>
#include <algorithm>
#include <cstddef>
#include <cstdlib>
#include <memory> /// ?
#include <utility>
#include <stdexcept>
#include <boost/unordered_map.hpp> /// TODO: map or unordered_map
#include <boost/cstdint.hpp>
#include <boost/interprocess/managed_shared_memory.hpp>
#include <boost/interprocess/allocators/allocator.hpp>
#include <boost/interprocess/sync/interprocess_mutex.hpp>
#include <boost/interprocess/sync/scoped_lock.hpp>
#include <boost/interprocess/sync/interprocess_condition.hpp>
#include <boost/date_time.hpp> // maybe not neccessary?
#include <boost/date_time/posix_time/posix_time_types.hpp> // No I/O just types.
#include <boost/date_time/microsec_time_clock.hpp>
#include <boost/thread/thread.hpp>
#include <boost/thread/win32/thread_primitives.hpp>
#include <boost/shared_ptr.hpp>
#include <boost/call_traits.hpp>
#include <boost/bind.hpp>
#include <boost/lexical_cast.hpp>
#include <boost/timer.hpp>
#if defined(_MSC_VER)
# define WIN32_LEAN_AND_MEAN
# include <windows.h>
//# include <intrin.h>
# undef WIN32_LEAN_AND_MEAN
#endif // (_MSC_VER)
# include "../common/thread.hpp"
#include "../common/pid_type.hpp"
#include "../common/owner_id.hpp"
#include "../common/macro_definitions.hpp"
#include "../common/interprocess.hpp"
#include "../common/config_param.hpp"
#include "../common/monitor_interface.hpp"
#include "../common/shared_interface.hpp"
#include "../common/bounded_buffer.hpp"
#include "bounded_message_buffer.hpp"
#include "process_info.hpp"
#include "../common/log.hpp"
#include "../common/spinlock.hpp"
#include "../common/macro_definitions.hpp"
#include "event.hpp"

#pragma intrinsic(_InterlockedIncrement)
#pragma intrinsic(_InterlockedDecrement)
#pragma intrinsic(_InterlockedCompareExchange)
#pragma intrinsic(_InterlockedExchange)
#pragma intrinsic(_InterlockedExchangeAdd)

namespace starcounter {
namespace core {

/// Exception class.
class ipc_monitor_exception {
public:
	typedef uint32_t error_code_type;
	
	explicit ipc_monitor_exception(error_code_type err)
	: err_(err) {}
	
	error_code_type error_code() const {
		return err_;
	}
	
private:
	error_code_type err_;
};

/// Class monitor.
/**
 * @throws monitor_exception when something can not be achieved.
 */
// Objects of type boost::thread are not copyable.
class monitor : private boost::noncopyable {
public:
	typedef std::map<owner_id, process_info> process_register_type;
	
	enum state {
		stopped,
		running
	};
	
	enum { /// TODO: rename these enums.
		// The maximum number of object handles per thread is in the range
		// 1..MAXIMUM_WAIT_OBJECTS. This parameter cannot be zero.
		events_per_group = MAXIMUM_WAIT_OBJECTS,
		
		// Up to (events_per_group * database_process_event_groups) database
		// processes can be monitored.
		database_process_event_groups = 1,

#if 0		
		// Up to (events_per_group * client_process_event_groups) client
		// processes can be monitored.
		client_process_event_groups = 4
#endif
	};
	
	enum {
		max_number_of_monitored_database_processes = max_number_of_databases,

		max_number_of_clients_per_database = max_number_of_clients,

		max_number_of_monitored_client_processes
		= max_number_of_monitored_database_processes
		+max_number_of_monitored_database_processes
		* max_number_of_clients_per_database,

		max_number_of_monitored_processes
		= max_number_of_monitored_database_processes
		+max_number_of_monitored_client_processes
	};

	enum {
		active_segments_buffer_capacity = 256
	};
	
	/// Construction of the monitor application.
	/**
	 * @param argc Argument count.
	 * @param argv Argument vector.
	 * @throws starcounter::core::bad_monitor if the monitor can not start.
	 */
	explicit monitor(int argc, wchar_t* argv[]);
	
	/// Destruction of the monitor.
	// It waits for all threads to finnish.
	~monitor();
	
	//--------------------------------------------------------------------------
	/// The wait_for_database_process_event() will wait for up to 64 database
	/// process events per group.
	// This function is reentrant and is called by one thread for each group.
	/**
	 * @param group Is the group (0..N) of server process events to watch. Each
	 *		group has up to 64 process events.
	 */
	static void wait_for_database_process_event(std::pair<monitor*,std::size_t> arg);

#if 0	
	//--------------------------------------------------------------------------
	/// The wait_for_client_process_event() will wait for up to 64 client
	/// process events per group.
	// This function is reentrant and is called by one thread for each group.
	// Example: If monitoring up to 256 clients, there are 4 groups with up to
	// 64 client processes in each group and 4 threads (one per group) calls
	// this function. An event is eventually received when a client process
	// exit, or crash.
	/**
	 * @param group Is the group (0..N) of client process events to watch. Each
	 *		group has up to 64 client process events.
	 */
	static void wait_for_client_process_event(std::pair<monitor*,std::size_t> arg);
#endif
	
#if 0 // idea
	// Methods for updating the process_register_.
	void insert_into_process_register(pid_type, owner_id) {
		boost::mutex::scoped_lock lock(register_mutex_);
		//...
	}
#endif
	
	/// Start the monitor threads.
	void run();
	
	/// Stop the monitor threads.
	void stop();
	
	/// Remove database process event.
	void remove_database_process_event(process_info::handle_type e);
	void remove_database_process_event(std::size_t group, uint32_t event_code);

#if 0	
	/// Remove client process event.
	void remove_client_process_event(process_info::handle_type e);
	void remove_client_process_event(std::size_t group, uint32_t event_code);
#endif

	/// Print pid register requires the pid register to be locked for a
	/// relatively long time so this is only used for debug.
	void print_event_register();
	
#if defined (IPC_MONITOR_SHOW_ACTIVITY)
	/// Show statistics and resource usage.
	static void watch_resources(monitor*);
#endif // defined (IPC_MONITOR_SHOW_ACTIVITY)
	
	/// The apc_function() calls this so that we can access member variables
	/// without having to make getters and setters for all.
	//void do_registration();
	
	/// Extract the database name from the segment name. The format of the
	/// segment name is:
	/// <DATABASE_NAME_PREFIX>_<DATABASE_NAME>_<SEQUENCE_NUMBER>
	/// and the sub string <DATABASE_NAME> is returned.
	/**
	 * @param segment_name The segment name, which is the name of the shared
	 *		memory segment of the database, used for IPC.
	 * @return A std::string containing the database name.
	 */
	std::string segment_name_to_database_name(const std::string& segment_name);
	
	// The caller must lock the active_databases_mutex_.
	void print_active_databases();
	
	void set_active_databases_updated_flag(bool state) {
		active_databases_updated_flag_ = state;
	}
	
	bool active_databases_updated_flag() const {
		return active_databases_updated_flag_;
	}
	
	bool insert_database_name(const std::string& database_name);
	bool erase_database_name(const std::string& database_name);
	
	/// Get reference to the log.
	starcounter::log& log();

	/// Get const reference to the log.
	const starcounter::log& log() const;

#if 0
	HANDLE& ipc_monitor_cleanup_event() {
		return ipc_monitor_cleanup_event_;
	}
#endif

	monitor_interface* the_monitor_interface() {
		return the_monitor_interface_;
	}

	struct database_process_group_type {
		thread thread_; // TODO: Access method.
		thread::native_handle_type thread_handle_; // TODO: Access method.
		std::vector<::HANDLE> event_; // TODO: Access method.
	};

	database_process_group_type& database_process_group(std::size_t i) {
		return database_process_group_[i];
	}

#if 0
	struct client_process_group_type {
		thread thread_; // TODO: Access method.
		thread::native_handle_type thread_handle_; // TODO: Access method.
		std::vector<::HANDLE> event_; // TODO: Access method.
	};
#endif

#if 0
	client_process_group_type& client_process_group(std::size_t i) {
		return client_process_group_[i];
	}
#endif

	boost::mutex& register_mutex() {
		return register_mutex_;
	}
	
	process_register_type& process_register() {
		return process_register_;
	}

	boost::mutex& active_databases_mutex() {
		return active_databases_mutex_;
	}
	
	boost::condition& active_databases_updated() {
		return active_databases_updated_;
	}

	std::ofstream& monitor_active_databases_file() {
		return monitor_active_databases_file_;
	}

#if 0
	std::wstring& active_databases_file_path() {
		return active_databases_file_path_;
	}
#endif

	std::set<std::string>& active_databases() {
		return active_databases_;
	}

	bounded_buffer<std::string>& active_segments_update() {
		return active_segments_update_;
	}

private:
	// Controlling the console a bit makes it easier to read.
	void gotoxy(int16_t x, int16_t y);

	/// The registration thread calls this.
	static void registrar(monitor*);

#if 0	
	/// The cleanup_ thread calls this.
	static void cleanup(monitor*);
#endif
	
	static void __stdcall apc_function(uint64_t arg);
	
	enum {
		// The IPC monitor also have an owner_id and it is 2 because 1 is anonymous.
		ipc_monitor_owner_id = 2,

		single_client_owner_id = 3
	};

	/// Return a const reference to the IPC monitor's owner_id.
	const owner_id& get_owner_id() const;

	/// Get a new owner_id.
	owner_id get_new_owner_id();
	
	/// Open all databases shared memory, for each, scan through all chunks and
	/// look for the oid. Set active_databases_mutex_cleanup flag in those owner_id's matching.
	/**
	 * @param oid The owner_id of the crashed client process.
	 * @return Number of chunks marked for cleanup.
	 */
	std::size_t find_chunks_and_mark_them_for_cleanup(owner_id oid);

#if 0	
	/// Write active databases.
	static void update_active_databases_file(monitor*);
#endif
	
	// The monitor initializes the monitor_interface_shared_memory_object.
	shared_memory_object monitor_interface_;
	mapped_region monitor_interface_region_;
	monitor_interface* the_monitor_interface_;
	
	// The state of the monitor.
	state state_; /// TODO: implement shutdown.

#if 0	
	// Event to notify the monitor to do cleanup.
	HANDLE ipc_monitor_cleanup_event_;
#endif

	/// TODO Maybe put this in a nested struct. Saved time not doing it.
	// The register_mutex_ is locked whenever a thread need to update any of the
	// variables below: the process_register_, the owner_id_counter_.
	boost::mutex register_mutex_;
	
	// The process register.
	process_register_type process_register_;
	
	// The owner_id counter is incremented by 1 each time an owner_id is taken.
	owner_id owner_id_counter_;
	
	// The IPC monitor's owner_id
	owner_id owner_id_;

	// The exited processes list.
	//exited_processes_list_type exited_processes_list_;
	//boost::mutex exited_processes_list_mutex_;
	
	// The list of registered databases is updated when databases register and
	// unregister, or terminates.
	std::ofstream monitor_active_databases_file_;
	boost::mutex active_databases_mutex_;
	boost::condition active_databases_updated_;
	bool active_databases_updated_flag_;
	
	database_process_group_type database_process_group_[database_process_event_groups];
#if 0
	client_process_group_type client_process_group_[client_process_event_groups];
#endif
	
	// The name of the server that started this monitor.
	std::string server_name_;
	
	// Name of the database output dir (L".db.output".)
	std::wstring database_output_dir_name_;
	
#if 0
	// Path to the dir where the files related to the IPC monitor can be stored.
	std::wstring monitor_dir_path_;
#endif
	
	// Set with names of active databases.
	std::set<std::string> active_databases_;

#if 0	
	// The monitor's active databases file path.
	std::wstring active_databases_file_path_;
#endif
	
	bounded_buffer<std::string> active_segments_update_;
	
	starcounter::log log_;

	//--------------------------------------------------------------------------
	// Threads (also in database_process_group and client_process_group)
	
	// The registrar thread waits for database- and client processes to register
	// and unregister. It is notified when there is data available in the
	// monitor interface and it will read the data from there and communicate
	// the information to some thread in the
	// database_process_event_thread_group_ or the
	// client_process_event_thread_group_. Then it waits for that thread to
	// complete the wait_for_registration
	thread registrar_;

#if 0	
	// Cleanup thread.
	thread cleanup_;
#endif

#if 0
	// The active databases file updater thread waits for a notification from
	// any thread that updates the register, and will write a list of active
	// databases to the file:
	// %UserProfile%\AppData\Local\Starcounter\active_databases
	thread active_databases_file_updater_thread_;
#endif
	
	// The resources watching thread is used for debug, verifying that resources
	// are recovered. It will keep an eye of all registered databases shared
	// memory segments resources. Number of free: chunks, channels, and
	// client_interfaces.
	thread resources_watching_thread_;
};

} // namespace core
} // namespace starcounter

#include "impl/monitor.hpp"

#endif // STARCOUNTER_CORE_MONITOR_HPP
