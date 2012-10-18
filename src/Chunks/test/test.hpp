//
// test.hpp
// interprocess_communication/test
//
// Copyright � 2006-2012 Starcounter AB. All rights reserved.
// Starcounter� is a registered trademark of Starcounter AB.
//

#ifndef STARCOUNTER_INTERPROCESS_COMMUNICATION_TEST_HPP
#define STARCOUNTER_INTERPROCESS_COMMUNICATION_TEST_HPP

#if defined(_MSC_VER) && (_MSC_VER >= 1200)
# pragma once
#endif // defined(_MSC_VER) && (_MSC_VER >= 1200)

#include <iostream>
#include <fstream> /// For debug - remove
#if defined (STARCOUNTER_CORE_ATOMIC_BUFFER_PERFORMANCE_COUNTERS)
# include <cmath> // log()
#endif //defined (STARCOUNTER_CORE_ATOMIC_BUFFER_PERFORMANCE_COUNTERS)
#include <ios>
#include <string>
#include <sstream>
#include <algorithm>
#include <cstddef>
#include <cstdlib>
#include <memory> /// ?
#include <utility>
#include <stdexcept>
#include <boost/cstdint.hpp>
//#include <boost/algorithm/string.hpp>
#include <boost/algorithm/string/case_conv.hpp>
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
#include <boost/call_traits.hpp>
#include <boost/bind.hpp>
#include <boost/lexical_cast.hpp>
#include <boost/timer.hpp>
#if defined(_MSC_VER)
# define WIN32_LEAN_AND_MEAN
# include <windows.h> /// TODO: thread_primitives.hpp might replace this include
# undef WIN32_LEAN_AND_MEAN
#endif // (_MSC_VER)
////#include "../common/pid_type.hpp"
////#include "../common/owner_id.hpp"
#include "../common/macro_definitions.hpp"
//#include "../common/interprocess.hpp"
#include "../common/config_param.hpp"
#include "../common/shared_interface.hpp"
#include "../common/database_shared_memory_parameters.hpp"
#include "../common/monitor_interface.hpp"

/// TODO Figure which of these to include. Maybe required if test opens a
/// database process managed shared memory.
#include "../common/circular_buffer.hpp"
#include "../common/bounded_buffer.hpp"
#include "../common/chunk.hpp"
#include "../common/shared_chunk_pool.hpp"
#include "../common/channel.hpp"
#include "../common/scheduler_channel.hpp"
#include "../common/common_scheduler_interface.hpp"
#include "../common/scheduler_interface.hpp"
#include "../common/common_client_interface.hpp"
#include "../common/client_interface.hpp"
#include "../common/client_number.hpp"
#include "../common/macro_definitions.hpp"
#include "../common/interprocess.hpp"
#include "../common/name_definitions.hpp"
#include "worker.hpp"

namespace starcounter {
namespace interprocess_communication {

using namespace starcounter::core;

/// Exception class.
class test_exception {
public:
	explicit test_exception(uint32_t err)
	: err_(err) {}
	
	uint32_t error_code() const {
		return err_;
	}
	
private:
	uint32_t err_;
};

/// Class test.
/**
 * @throws test_exception when something can not be achieved.
 */
// Objects of type boost::thread are not copyable.
class test : private boost::noncopyable {
public:
	enum {
		// Number of workers to be instantiated in the test.
		workers = 2
	};
	
	/// Construction of the test application.
	// Log messages are appended to the log file, it is never deleted.
	/**
	 * @param argc Argument count.
	 * @param argv Argument vector.
	 * @throws starcounter::core::bad_test if the test fails to start.
	 */
	explicit test(int argc, wchar_t* argv[]);
	
	/// Destruction of the test.
	// It waits for worker threads to finish.
	~test();
	
	/// Initialize opens the database, etc.
	void initialize(const char* database_name);
	
	test& set_segment_name(const std::string& segment_name);
	std::string get_segment_name() const;
	test& set_monitor_interface_name(const std::string& monitor_interface_name);
	std::string get_monitor_interface_name() const;
	test& set_pid(const pid_type pid);
	pid_type get_pid() const;
	test& set_owner_id(const owner_id oid);
	owner_id get_owner_id() const;

	/// Start the test.
	void run(uint32_t interval_time_milliseconds, uint32_t duration_time_milliseconds);
	
	/// Stop a worker.
    /**
	 * @param n Worker number to stop.
     */
	void stop_worker(std::size_t n);

	/// Stop the workers.
	void stop_all_workers();
	
	#if defined (STARCOUNTER_CORE_ATOMIC_BUFFER_PERFORMANCE_COUNTERS)
	int plot_dots(double rate);
	void print_rate(double rate);
	
	/// Show statistics.
	void show_statistics(uint32_t interval_time_milliseconds, uint32_t
	duration_time_milliseconds);
	#endif // defined (STARCOUNTER_CORE_ATOMIC_BUFFER_PERFORMANCE_COUNTERS)
	
	void gotoxy(int16_t x, int16_t y) {
		COORD coord;
		coord.X = x;
		coord.Y = y;
		SetConsoleCursorPosition(GetStdHandle(STD_OUTPUT_HANDLE), coord);
	}
	
private:
	shared_interface shared_;
	std::size_t active_schedulers_;
	
	std::string monitor_interface_name_;
	std::string segment_name_;
	pid_type pid_;
	owner_id owner_id_;

	// message queue - to simulate fetching messages from a interprocess_communication via Win32API
	
	// The workers.
	worker worker_[workers];

	#if defined (STARCOUNTER_CORE_ATOMIC_BUFFER_PERFORMANCE_COUNTERS)
	// Statistics thread.
	boost::thread statistics_thread_;
	#endif // defined (STARCOUNTER_CORE_ATOMIC_BUFFER_PERFORMANCE_COUNTERS)
};

} // namespace interprocess_communication
} // namespace starcounter

#include "impl/test.hpp"

#endif // STARCOUNTER_INTERPROCESS_COMMUNICATION_TEST_HPP