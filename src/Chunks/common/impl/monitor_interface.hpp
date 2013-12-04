//
// impl/monitor_interface.hpp
//
// Copyright © 2006-2012 Starcounter AB. All rights reserved.
// Starcounter® is a registered trademark of Starcounter AB.
//
// Implementation of class monitor_interface.
//

#ifndef STARCOUNTER_CORE_IMPL_MONITOR_INTERFACE_HPP
#define STARCOUNTER_CORE_IMPL_MONITOR_INTERFACE_HPP

// Implementation

namespace starcounter {
namespace core {

inline monitor_interface::in::in()
: data_available_(false) {}

inline monitor_interface::out::out()
: data_available_(false) {}

#if 0 /// TODO: Figure how to bind this.
inline bool monitor_interface::in::is_data_available() const {
	return data_available_;
}

inline bool monitor_interface::out::is_data_available() const {
	return data_available_;
}
#endif /// TODO: Figure how to bind this.

inline monitor_interface::monitor_interface()
: in_(),
out_(),
is_ready_flag_(false) {}

#if 0
inline monitor_interface::monitor_interface()
: in_(),
out_(),
is_ready_flag_(false),
cleanup_task_() {}
#endif

inline void monitor_interface::wait_until_ready() {
	boost::interprocess::scoped_lock<boost::interprocess
	::interprocess_mutex> lock(ready_mutex_);
	is_ready_.wait(lock, boost::bind(&monitor_interface
	::is_ready, this));
}

inline void monitor_interface::is_ready_notify_all() {
	boost::interprocess::scoped_lock<boost::interprocess
	::interprocess_mutex> lock(ready_mutex_);
	is_ready_flag_ = true;
	lock.unlock();
	is_ready_.notify_all();
}

inline bool monitor_interface::is_ready() const {
	return is_ready_flag_;
}

inline void monitor_interface::set_in_data_available_state(bool state) {
	in_.data_available_ = state;
}

inline void monitor_interface::set_out_data_available_state(bool state) {
	out_.data_available_ = state;
}

/// TODO: Remove when I have figured how to bind.
inline bool monitor_interface::in_is_data_available() const {
	return in_.data_available_;
}

/// TODO: Remove when I have figured how to bind.
inline bool monitor_interface::out_is_data_available() const {
	return out_.data_available_;
}

inline uint32_t monitor_interface::register_database_process(pid_type pid, std::string
segment_name, owner_id& oid, uint32_t timeout_milliseconds) {
	// The timeout is used multiple times below, while time passes, so all
	// synchronization must be completed before the timeout_milliseconds time
	// period has elapsed.
	const boost::system_time timeout = boost::posix_time::microsec_clock
	::universal_time() +boost::posix_time::milliseconds(timeout_milliseconds);
	
	// The calling database thread must acquire the monitor_interface_lock, to
	// ensure that only one thread except the monitor thread gets access to the
	// monitor_interface. The monitor never locks the monitor_interface_lock.
	boost::interprocess::scoped_lock<boost::interprocess::interprocess_mutex>
	monitor_interface_lock(monitor_interface_mutex_, timeout);
	
	if (!monitor_interface_lock.owns()) {
		// The timeout_milliseconds time period has elapsed. Failed to register
		// the database process.
		oid = owner_id::none;
		return SCERRDBACQUIREOWNERIDTIMEOUT;
	}
	
	{
		// This lock synchronizes access to the monitor_interface between this
		// calling database thread and the monitor wait_for_registration thread.
		boost::interprocess::scoped_lock<boost::interprocess
		::interprocess_mutex> lock(in_.mutex_, timeout);
		
		if (!lock.owns()) {
			// The timeout_milliseconds time period has elapsed. Failed to
			// register the database process.
			oid = owner_id::none;
			return SCERRDBACQUIREOWNERIDTIMEOUT;
		}
		
		set_pid(pid);
		set_segment_name(segment_name);
		set_process_type(database_process);
		set_operation(registration_request);
		set_in_data_available_state(true);
	}
	
	// This thread is holding the monitor_interface_lock so no other thread can
	// access in_ and out_. Notify the monitor wait_for_registration thread.
	in_.data_is_available_.notify_one();
	
	{
		// This lock synchronizes access to the monitor_interface between this
		// calling database thread and the monitor thread.
		boost::interprocess::scoped_lock<boost::interprocess
		::interprocess_mutex> lock(out_.mutex_, timeout);
		
		if (!lock.owns()) {
			// The timeout_milliseconds time period has elapsed. Failed to
			// register the database process.
			oid = owner_id::none;
			return SCERRDBACQUIREOWNERIDTIMEOUT;
		}
		
		// Wait until out data is available, or timeout occurs.
		if (out_.data_is_available_.timed_wait(lock, timeout,
		boost::bind(&monitor_interface::out_is_data_available, this)) == true) {
			// Now out data is available.
			oid = get_owner_id();
			set_out_data_available_state(false);
		}
		else {
			// The timeout_milliseconds time period has elapsed. Failed to
			// register the database process.
			oid = owner_id::none;
			/// TODO: Here is a bug, because:
			set_out_data_available_state(false);
			/// is not properly synchronized, since out_.mutex_ is not locked!
			/// Setting it to false anyway decreases the probablility of the
			/// bug to appear. Eventually the state is set to false, but if
			/// another thread registers while the state is still true,
			/// that thread will get bad registration data. A bad bug.
			return SCERRDBACQUIREOWNERIDTIMEOUT;
		}
	}
	
	// Check if the owner_id is invalid.
	if (get_owner_id() == owner_id::none) {
		// Failed to acquire an owner_id.
		return SCERRDBACQUIREOWNERID;
	}
	
	// Successfully registered the database process.
	return 0;
}

#if 0
inline uint32_t monitor_interface::register_client_process(pid_type pid, owner_id& oid,
uint32_t timeout_milliseconds) {
	// The timeout is used multiple times below, while time passes, so all
	// synchronization must be completed before the timeout_milliseconds time
	// period has elapsed.
	const boost::system_time timeout = boost::posix_time::microsec_clock
	::universal_time() +boost::posix_time::milliseconds(timeout_milliseconds);
	
	// The calling client thread must acquire the monitor_interface_lock, to
	// ensure that only one thread except the monitor thread gets access to the
	// monitor_interface. The monitor never locks the monitor_interface_lock.
	boost::interprocess::scoped_lock<boost::interprocess::interprocess_mutex>
	monitor_interface_lock(monitor_interface_mutex_, timeout);

	if (!monitor_interface_lock.owns()) {
		// The timeout_milliseconds time period has elapsed. Failed to register
		// the client process.
		oid = owner_id::none;
		return SCERRCACQUIREOWNERIDTIMEOUT;
	}
	
	{
		// This lock synchronizes access to the monitor_interface between this
		// calling client thread and the monitor thread.
		boost::interprocess::scoped_lock<boost::interprocess
		::interprocess_mutex> lock(in_.mutex_, timeout);

		if (!lock.owns()) {
			// A timeout occurred. Failed to register the client process.
			oid = owner_id::none;
			return SCERRCACQUIREOWNERIDTIMEOUT2;
		}

		set_pid(pid);
		set_segment_name("");
		set_process_type(client_process);
		set_operation(registration_request);
		set_in_data_available_state(true);
	}

	// This thread is holding the monitor_interface_lock so no other process can
	// access in_ and out_. Notify the monitor wait_for_registration thread.
	in_.data_is_available_.notify_one();
	
	{
		// This lock synchronizes access to the monitor_interface between this
		// calling client thread and the monitor thread.
		boost::interprocess::scoped_lock<boost::interprocess
		::interprocess_mutex> lock(out_.mutex_, timeout);
		
		if (!lock.owns()) {
			// The timeout_milliseconds time period has elapsed. Failed to
			// register the client process.
			oid = owner_id::none;
			return SCERRCACQUIREOWNERIDTIMEOUT;
		}

		// Wait until out data is available, or timeout occurs.
		if (out_.data_is_available_.timed_wait(lock, timeout,
		boost::bind(&monitor_interface::out_is_data_available, this)) == true) {
			// Now out data is available.
			oid = get_owner_id();
			set_out_data_available_state(false);
		}
		else {
			// The timeout_milliseconds time period has elapsed. Failed to
			// register the client process.
			oid = owner_id::none;
			/// TODO: Here is a bug, because:
			set_out_data_available_state(false);
			/// is not properly synchronized, since out_.mutex_ is not locked!
			/// Setting it to false anyway decreases the probablility of the
			/// bug to appear. Eventually the state is set to false, but if
			/// another thread registers while the state is still true,
			/// that thread will get bad registration data. A bad bug.
			return SCERRCACQUIREOWNERIDTIMEOUT;
		}
	}

	// Check if the owner_id is invalid.
	if (get_owner_id() == owner_id::none) {
		// Failed to acquire an owner_id.
		return SCERRCACQUIREOWNERID;
	}
	
	// Successfully registered the client process.
	return 0;
}
#endif

inline uint32_t monitor_interface::unregister_database_process(pid_type pid, owner_id&
oid, uint32_t timeout_milliseconds) {
	// The timeout is used multiple times below, while time passes, so all
	// synchronization must be completed before the timeout_milliseconds time
	// period has elapsed.
	const boost::system_time timeout = boost::posix_time::microsec_clock
	::universal_time() +boost::posix_time::milliseconds(timeout_milliseconds);

	// The calling database thread must acquire the monitor_interface_lock, to
	// ensure that only one thread except the monitor thread gets access to the
	// monitor_interface. The monitor never locks the monitor_interface_lock.
	boost::interprocess::scoped_lock<boost::interprocess::interprocess_mutex>
	monitor_interface_lock(monitor_interface_mutex_, timeout);

	if (!monitor_interface_lock.owns()) {
		// The timeout_milliseconds time period has elapsed. Failed to
		// unregister the database process.
		oid = owner_id::none;
		return SCERRDBRELEASEOWNERIDTIMEOUT;
	}

	{
		// This lock synchronizes access to the monitor_interface between this
		// calling database thread and the monitor thread.
		boost::interprocess::scoped_lock<boost::interprocess
		::interprocess_mutex> lock(in_.mutex_, timeout);

		if (!lock.owns()) {
			// A timeout occurred. Failed to unregister the database process.
			oid = owner_id::none;
			return SCERRDBRELEASEOWNERIDTIMEOUT;
		}

		set_pid(pid);
		set_owner_id(oid);
		set_process_type(database_process);
		set_operation(unregistration_request);
		set_in_data_available_state(true);
	}

	// This thread is holding the monitor_interface_lock so no other process can
	// access in_ and out_. Notify the monitor wait_for_registration thread.
	in_.data_is_available_.notify_one();

	{
		// This lock synchronizes access to the monitor_interface between this
		// calling database thread and the monitor thread.
		boost::interprocess::scoped_lock<boost::interprocess
		::interprocess_mutex> lock(out_.mutex_, timeout);
		
		if (!lock.owns()) {
			// The timeout_milliseconds time period has elapsed. Failed to
			// unregister the database process.
			oid = owner_id::none;
			return SCERRDBRELEASEOWNERIDTIMEOUT;
		}

		// Wait until out data is available, or timeout occurs.
		if (out_.data_is_available_.timed_wait(lock, timeout,
		boost::bind(&monitor_interface::out_is_data_available, this)) == true) {
			// Now out data is available.
			oid = get_owner_id();
			set_out_data_available_state(false);
		}
		else {
			// The timeout_milliseconds time period has elapsed. Failed to
			// unregister the database process.
			oid = owner_id::none;
			/// TODO: Here is a bug, because:
			set_out_data_available_state(false);
			/// is not properly synchronized, since out_.mutex_ is not locked!
			/// Setting it to false anyway decreases the probablility of the
			/// bug to appear. Eventually the state is set to false, but if
			/// another thread registers while the state is still true,
			/// that thread will get bad registration data. A bad bug.
			return SCERRDBRELEASEOWNERIDTIMEOUT;
		}
	}

	// Check that the owner_id is invalid.
	if (get_owner_id() != owner_id::none) {
		// Failed to release the owner_id.
		return SCERRDBRELEASEOWNERID;
	}
	
	// Successfully unregistered the database process.
	return 0;
}

#if 0
inline uint32_t monitor_interface::unregister_client_process(pid_type pid, owner_id&
oid, uint32_t timeout_milliseconds) {
	// The timeout is used multiple times below, while time passes, so all
	// synchronization must be completed before the timeout_milliseconds time
	// period has elapsed.
	const boost::system_time timeout = boost::posix_time::microsec_clock
	::universal_time() +boost::posix_time::milliseconds(timeout_milliseconds);

	// The calling client thread must acquire the monitor_interface_lock, to
	// ensure that only one thread except the monitor thread gets access to the
	// monitor_interface. The monitor never locks the monitor_interface_lock.
	boost::interprocess::scoped_lock<boost::interprocess::interprocess_mutex>
	monitor_interface_lock(monitor_interface_mutex_, timeout);

	if (!monitor_interface_lock.owns()) {
		// The timeout_milliseconds time period has elapsed. Failed to
		// unregister the client process.
		oid = owner_id::none;
		return SCERRCRELEASEOWNERIDTIMEOUT;
	}

	{
		// This lock synchronizes access to the monitor_interface between this
		// calling client thread and the monitor thread.
		boost::interprocess::scoped_lock<boost::interprocess
		::interprocess_mutex> lock(in_.mutex_, timeout);

		if (!lock.owns()) {
			// A timeout occurred. Failed to unregister the client process.
			oid = owner_id::none;
			return SCERRCRELEASEOWNERIDTIMEOUT;
		}

		set_pid(pid);
		set_owner_id(oid);
		set_process_type(client_process);
		set_operation(unregistration_request);
		set_in_data_available_state(true);
	}

	// This thread is holding the monitor_interface_lock so no other process can
	// access in_ and out_. Notify the monitor wait_for_registration thread.
	in_.data_is_available_.notify_one();
	
	{
		// This lock synchronizes access to the monitor_interface between this
		// calling client thread and the monitor thread.
		boost::interprocess::scoped_lock<boost::interprocess
		::interprocess_mutex> lock(out_.mutex_, timeout);

		if (!lock.owns()) {
			// The timeout_milliseconds time period has elapsed. Failed to
			// unregister the client process.
			oid = owner_id::none;
			return SCERRCRELEASEOWNERIDTIMEOUT;
		}

		// Wait until out data is available, or timeout occurs.
		if (out_.data_is_available_.timed_wait(lock, timeout,
		boost::bind(&monitor_interface::out_is_data_available, this)) == true) {
			// Now out data is available.
			oid = get_owner_id();
			set_out_data_available_state(false);
		}
		else {
			// The timeout_milliseconds time period has elapsed. Failed to
			// unregister the client process.
			oid = owner_id::none;
			/// TODO: Here is a bug, because:
			set_out_data_available_state(false);
			/// is not properly synchronized, since out_.mutex_ is not locked!
			/// Setting it to false anyway decreases the probablility of the
			/// bug to appear. Eventually the state is set to false, but if
			/// another thread registers while the state is still true,
			/// that thread will get bad registration data. A bad bug.
			return SCERRCRELEASEOWNERIDTIMEOUT;
		}
	}
	
	// Check that the owner_id is invalid.
	if (get_owner_id() != owner_id::none) {
		// Failed to release the owner_id.
		return SCERRCRELEASEOWNERID;
	}
	
	// Successfully unregistered the client process.
	return 0;
}
#endif

inline void monitor_interface::wait_for_registration() {
	// The monitor starts the registrar thread which calls this function.
	// The registrar waits for instance- and client processes to register and
	// unregister. When the process that registers have stored in data, it
	// notifies this thread.
	boost::interprocess::scoped_lock
	<boost::interprocess::interprocess_mutex> lock(in_.mutex_);
	
	in_.data_is_available_.wait(lock, boost::bind(&monitor_interface
	::in_is_data_available, this));
	
	// The calling monitor thread knows that in data is available upon return.
	set_in_data_available_state(false);
}

inline void monitor_interface::set_pid(pid_type pid) {
	in_.pid_ = pid;
}

inline pid_type monitor_interface::get_pid() const {
	return in_.pid_;
}

inline void monitor_interface::set_segment_name(std::string segment_name) {
	// Copy segment_name to c-string in_.segment_name_, and 0 terminate.
	std::size_t length = segment_name.copy(in_.segment_name_,
	segment_name_size);
	
	in_.segment_name_[length] = '\0';
}

inline std::string monitor_interface::get_segment_name() const {
	return std::string(in_.segment_name_);
}

inline void monitor_interface::set_process_type(monitor_interface::process_type
pt) {
	in_.process_type_ = pt;
}

inline monitor_interface::process_type monitor_interface::get_process_type()
const {
	return in_.process_type_;
}
	
inline void monitor_interface::set_operation(monitor_interface::operation op) {
	in_.operation_ = op;
}

inline monitor_interface::operation monitor_interface::get_operation() const {
	return in_.operation_;
}

inline void monitor_interface::set_owner_id(owner_id oid) {
	out_.owner_id_ = oid;
}

inline owner_id monitor_interface::get_owner_id() const {
	return out_.owner_id_;
}

#if 0
inline monitor_interface::cleanup_task::cleanup_task()
: segment_name_mask_(0),
cleanup_mask_(0),
spinlock_() {
	// Initialize segment_name_[s] to 0. 
	for (std::size_t s = 0; s < max_number_of_databases; ++s) {
		*segment_name_[s] = 0;
	}
}

inline int32_t monitor_interface::cleanup_task::insert_segment_name
(const char* segment_name) {
	smp::spinlock::scoped_lock lock(spinlock());

	// bit_scan_forward() returns a valid index if at least one bit is set.
	if (~segment_name_mask_) {
		// Search for the first 0 bit.
		int32_t i = bit_scan_forward(~segment_name_mask_);
		segment_name_mask_ |= 1ULL << i;

		// Successfully acquired an index. Inserting the segment name.
		std::strcpy(segment_name_[i], segment_name);
		return i;
	}
	else {
		// Trying to insert more segment name's than can fit. A bug.
		return -1;
	}
}

inline void monitor_interface::cleanup_task::erase_segment_name
(const char* segment_name, HANDLE ipc_monitor_cleanup_event) {
	smp::spinlock::scoped_lock lock(spinlock());

	for (cleanup_task::mask_type segment_name_mask = segment_name_mask_;
	segment_name_mask; segment_name_mask &= segment_name_mask -1) {
		int32_t i = bit_scan_forward(segment_name_mask);
		if (!std::strcmp(segment_name_[i], segment_name)) {
			*segment_name_[i] = 0;
			segment_name_mask_ &= ~(1ULL << i);
			
			if ((cleanup_mask_ &= ~(1ULL << i)) == 0) {
				::ResetEvent(ipc_monitor_cleanup_event);
			}
		}
	}
}

inline const char* monitor_interface::cleanup_task::get_a_segment_name(HANDLE ipc_monitor_cleanup_event) {
	smp::spinlock::scoped_lock lock(spinlock());

	// bit_scan_forward() returns a valid index if at least one bit is set.
	if (cleanup_mask_) {
		int32_t i = bit_scan_forward(cleanup_mask_);

		if ((cleanup_mask_ &= ~(1ULL << i)) == 0) {
			::ResetEvent(ipc_monitor_cleanup_event);
		}

		// Found a segment_name.
		return segment_name_[i];
	}
	else {
		// There are no segment names in the table. Possibly a bug.
		return 0;
	}
}

inline void monitor_interface::cleanup_task::set_cleanup_flag(int32_t index, HANDLE ipc_monitor_cleanup_event) {
	smp::spinlock::scoped_lock lock(spinlock());
	cleanup_mask_ |= 1ULL << index;

	// Notify the IPC monitor to do the rest of the cleanup
	// (releasing chunks and the client_interface.)
	if (::SetEvent(ipc_monitor_cleanup_event)) {
		// Successfully notified the IPC monitor to do cleanup.
	}
	else {
		// Error. Failed to notify the IPC monitor to do cleanup.
	}
}

inline uint64_t monitor_interface::cleanup_task::get_cleanup_flag() {
	smp::spinlock::scoped_lock lock(spinlock());
	return cleanup_mask_;
}
#endif

#if 0
inline int32_t monitor_interface::insert_segment_name(const char* segment_name) {
	return cleanup_task_.insert_segment_name(segment_name);
}


inline void monitor_interface::erase_segment_name(const char* segment_name, HANDLE ipc_monitor_cleanup_event) {
	cleanup_task_.erase_segment_name(segment_name, ipc_monitor_cleanup_event);
}

inline const char* monitor_interface::get_a_segment_name(HANDLE ipc_monitor_cleanup_event) {
	return cleanup_task_.get_a_segment_name(ipc_monitor_cleanup_event);
}

inline void monitor_interface::print_segment_name_list() {
	for (int i = 0; i < 64; ++i) {
		std::cout << i << ": ";
		if (cleanup_task_.segment_name_[i]) {
			std::cout << cleanup_task_.segment_name_[i];
		}
		std::cout << std::endl;
	}
}

inline void monitor_interface::set_cleanup_flag(int32_t index, HANDLE ipc_monitor_cleanup_event) {
	cleanup_task_.set_cleanup_flag(index, ipc_monitor_cleanup_event);
}

inline uint64_t monitor_interface::get_cleanup_flag() {
	return cleanup_task_.get_cleanup_flag();
}

inline smp::spinlock& monitor_interface::spinlock() {
	return cleanup_task_.spinlock();
}
#endif

inline monitor_interface::active_databases::active_databases()
: size_(0),
spinlock_(),
active_databases_set_update_() {
	for (std::size_t i = 0; i < max_number_of_databases; ++i) {
		*database_name_[i] = 0;
	}
}

inline smp::spinlock& monitor_interface::active_databases::spinlock() {
	return spinlock_;
}

inline void monitor_interface::active_databases
::set_active_databases_set_update_event(HANDLE active_databases_set_update) {
	active_databases_set_update_ = active_databases_set_update;
}

inline monitor_interface::active_databases::size_type
monitor_interface::active_databases::update(const std::set<std::string>& databases) {
	smp::spinlock::scoped_lock lock(spinlock()); // TODO: This one needs a timeout
	std::size_t i = 0;

	for (std::set<std::string>::iterator it = databases.begin();
	it != databases.end(); ++it) {
		std::strncpy(database_name_[i], (*it).c_str(), database_name_size -1);
		database_name_[i][database_name_size -1] = 0;
		++i;
	}
			
	::SetEvent(active_databases_set_update_);
	return size_ = i;
}

inline void monitor_interface::active_databases::copy
(std::set<std::string>& databases, HANDLE active_databases_set_update_event) {
	smp::spinlock::scoped_lock lock(spinlock());
	::ResetEvent(active_databases_set_update_event);
	databases.clear();

	for (int i = 0; i < size_; ++i) {
		// This can be done more efficiently. TODO: Optimize insert.
		databases.insert(std::string(database_name_[i]));
	}
}

inline monitor_interface_ptr_exception::monitor_interface_ptr_exception(int err)
: err_(err) {}

inline int monitor_interface_ptr_exception::error_code() const {
	return err_;
}

inline monitor_interface_ptr::monitor_interface_ptr(const char* monitor_interface_name)
: ptr_(0) {
	// Try to open the monitor interface shared memory object.
	if (monitor_interface_name) {
		init(monitor_interface_name);
	}
}

inline void monitor_interface_ptr::init(const char* monitor_interface_name, const uint32_t timeout_ms) {
    uint64_t end_time = ::GetTickCount64() + timeout_ms;

	// Try to open the monitor interface shared memory object.
	shared_memory_object_.init_open(monitor_interface_name);
		
	while (!shared_memory_object_.is_valid()) {

        // Checking if given time has passed.
        if (::GetTickCount64() < end_time) {
            Sleep(1);
            shared_memory_object_.init_open(monitor_interface_name);
            continue;
        }

		// Failed to open monitor interface.
		throw monitor_interface_ptr_exception(SCERROPENMONITORINTERFACE);
	}
		
	// Map the whole database shared memory parameters shared memory object
	// in this process.
	mapped_region_.init(shared_memory_object_);
		
	while (!mapped_region_.is_valid()) {

        // Checking if given time has passed.
        if (::GetTickCount64() < end_time) {
            Sleep(1);
            mapped_region_.init(shared_memory_object_);
            continue;
        }

		// Failed to map monitor interface in shared memory.
		throw monitor_interface_ptr_exception
		(SCERRMAPMONITORINTERFACEINSHM);
	}
		
	// Obtain a pointer to the shared structure.
	ptr_ = static_cast<monitor_interface*>(mapped_region_.get_address());
}

inline monitor_interface_ptr::monitor_interface_ptr(monitor_interface* ptr) {
	ptr_ = ptr;
}

inline monitor_interface_ptr::~monitor_interface_ptr() {
	ptr_ = 0;
	// The shared_memory_object and mapped_region destructors are called.
}

inline monitor_interface& monitor_interface_ptr::operator*() const {
	return *ptr_;
}

inline monitor_interface* monitor_interface_ptr::operator->() const {
	return ptr_;
}

inline monitor_interface* monitor_interface_ptr::get() const {
	return ptr_;
}

// output operator for monitor_interface::process_type
template<class CharT, class Traits>
inline std::basic_ostream<CharT, Traits>&
operator<<(std::basic_ostream<CharT, Traits>& os,
const monitor_interface::process_type& u) {
	std::string name;
	switch (u) {
	case monitor_interface::database_process:
		name = "database";
		break;
#if 0
	case monitor_interface::client_process:
		name = "client";
		break;
#endif
	default:
		name = "?";
	}
	os << name;
	return os;
}

} // namespace core
} // namespace starcounter

#endif // STARCOUNTER_CORE_IMPL_MONITOR_INTERFACE_HPP
