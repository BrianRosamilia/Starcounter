//
// impl/shared_interface.hpp
//
// Copyright © 2006-2013 Starcounter AB. All rights reserved.
// Starcounter® is a registered trademark of Starcounter AB.
//
// Implementation of class shared_interface.
//

#ifndef STARCOUNTER_CORE_IMPL_SHARED_INTERFACE_HPP
#define STARCOUNTER_CORE_IMPL_SHARED_INTERFACE_HPP

// Implementation

namespace starcounter {
namespace core {

#if defined (IPC_VERSION_2_0)
inline shared_interface::shared_interface()
: segment_name_(),
monitor_interface_name_(),
chunk_(),
shared_chunk_pool_(),
common_scheduler_interface_(),
scheduler_interface_(),
common_client_interface_(),
client_interface_(),
channel_(),
client_number_(no_client_number),
owner_id_(0),
pid_(0),
id_() {}

inline shared_interface::shared_interface(std::string segment_name, std::string
monitor_interface_name, pid_type pid, owner_id oid, uint32_t iid)
: segment_name_(segment_name),
monitor_interface_name_(monitor_interface_name),
chunk_(),
shared_chunk_pool_(),
common_scheduler_interface_(),
scheduler_interface_(),
common_client_interface_(),
client_interface_(),
channel_(),
client_number_(no_client_number),
owner_id_(oid),
pid_(pid),
id_() {
	init(segment_name, monitor_interface_name, pid, oid, iid);
}
#else // !defined (IPC_VERSION_2_0)
inline shared_interface::shared_interface()
: segment_name_(),
monitor_interface_name_(),
chunk_(),
shared_chunk_pool_(),
common_scheduler_interface_(),
scheduler_interface_(),
common_client_interface_(),
client_interface_(),
channel_(),
client_number_(no_client_number),
owner_id_(0),
pid_(0) {}

#if 0
inline shared_interface::shared_interface(std::string segment_name, std::string
monitor_interface_name, pid_type pid, owner_id oid)
: segment_name_(segment_name),
monitor_interface_name_(monitor_interface_name),
chunk_(),
shared_chunk_pool_(),
common_scheduler_interface_(),
scheduler_interface_(),
common_client_interface_(),
client_interface_(),
channel_(),
client_number_(no_client_number),
owner_id_(oid),
pid_(pid) {
	init(segment_name, monitor_interface_name, pid, oid);
}
#endif
#endif // defined (IPC_VERSION_2_0)

inline shared_interface::~shared_interface() {
	close_client_work_event();

	for (std::size_t i = 0; i < max_number_of_schedulers; ++i) {
		close_scheduler_work_event(i);
	}
}

#if defined (IPC_VERSION_2_0)
inline void shared_interface::init(std::string segment_name, std::string
monitor_interface_name, pid_type pid, owner_id oid, uint32_t iid) {
	segment_name_ = segment_name;
	monitor_interface_name_ = monitor_interface_name;
	owner_id_ = oid;
	pid_ = pid;
	
	// Open the managed segment with the segment_name, which has the format:
	// <DATABASE_NAME_PREFIX>_<DATABASE_NAME>_<SEQUENCE_NUMBER>.
	segment_.init_open(segment_name.c_str());
	
	// Check if it is valid.
	if (segment_.is_valid()) {
		mapped_region_.init(segment_);
		if (mapped_region_.is_valid()) {
			// Found the shared memory segment. Initialize pointers.
			init();
		}
	}
	else {
		// Invalid segment. The shared memory segment probably don't exist.
		throw shared_interface_exception(SCERRSHAREDINTERFACEOPENDBSHM);
	}
	
	client_work_event() = 0;
	
	for (std::size_t i = 0; i < max_number_of_schedulers; ++i) {
		scheduler_work_event(i) = 0;
		
		if (common_scheduler_interface().is_scheduler_active(i)) {
			if (!open_scheduler_work_event(i)) {
				// Failed to open the event.
				throw shared_interface_exception(SCERROPENSCHEDULERWORKEVENT);
			}
		}
	}
}
#else // !defined (IPC_VERSION_2_0)
inline void shared_interface::init(std::string segment_name, std::string
monitor_interface_name, pid_type pid, owner_id oid) {
	segment_name_ = segment_name;
	monitor_interface_name_ = monitor_interface_name;
	owner_id_ = oid;
	pid_ = pid;
	
	// Open the managed segment with the segment_name, which has the format:
	// <DATABASE_NAME_PREFIX>_<DATABASE_NAME>_<SEQUENCE_NUMBER>.
	segment_.init_open(segment_name.c_str());
	
	// Check if it is valid.
	if (segment_.is_valid()) {
		mapped_region_.init(segment_);
		if (mapped_region_.is_valid()) {
			// Found the shared memory segment. Initialize pointers.
			init();
		}
	}
	else {
		// Invalid segment. The shared memory segment probably don't exist.
		throw shared_interface_exception(SCERRSHAREDINTERFACEOPENDBSHM);
	}
	
	client_work_event() = 0;
	
	for (std::size_t i = 0; i < max_number_of_schedulers; ++i) {
		scheduler_work_event(i) = 0;
#if 0		
		if (common_scheduler_interface().is_scheduler_active(i)) {
			if (!open_scheduler_work_event(i)) {
				// Failed to open the event.
				throw shared_interface_exception(SCERROPENSCHEDULERWORKEVENT);
			}
		}
#endif
	}
}
#endif // defined (IPC_VERSION_2_0)

inline std::string shared_interface::get_segment_name() const {
	return segment_name_;
}

inline std::string shared_interface::get_monitor_interface_name() const {
	return monitor_interface_name_;
}

inline bool shared_interface::acquire_client_number(uint32_t spin_count,
uint32_t timeout_milliseconds) {
	common_client_interface_type::value_type v;
	bool have_client_number = common_client_interface_->acquire_client_number
	(&v, client_interface_, owner_id_, spin_count, timeout_milliseconds);
	
	client_number_ = v;

	if (get_client_number() != no_client_number) {
		open_client_work_event(get_client_number());
	}
	
	return have_client_number;
}

inline bool shared_interface::acquire_client_number2(client_number v, uint32_t spin_count,
uint32_t timeout_milliseconds) {
	bool have_client_number = common_client_interface_->acquire_client_number
	(v, client_interface_, owner_id_, spin_count, timeout_milliseconds);

	if (have_client_number) {
		client_number_ = v;

		if (get_client_number() != no_client_number) {
			open_client_work_event(get_client_number());
		}
	}
	return have_client_number;
}

inline bool shared_interface::release_client_number(uint32_t spin_count,
uint32_t timeout_milliseconds) {
	close_client_work_event();
	
	if ((common_client_interface_->release_client_number(client_number_,
	client_interface_, spin_count, timeout_milliseconds)) == true) {
		client_number_ = no_client_number;

		// Successfully released the client_number.
		return true;
	}
	else {
		// Failed to release the client_number, probably because a timeout
		// occurred. If a client fails to release a shared resource, the client
		// shall probably not unregister with the monitor, and instead rely on
		// that the monitor and schedulers clean up properly after the client.
		// It is probably the best strategy.

		// Flag that not all resources have been released can be tested later
		// when it is time to unregister with the monitor. TODO: Implement this:
		//failed_to_release_a_resource = true;
		return false;
	}
}

//------------------------------------------------------------------------------
inline bool shared_interface::acquire_channel(channel_number*
the_channel_number, scheduler_number the_scheduler_number, uint32_t spin_count,
uint32_t timeout_milliseconds) {
	//std::cout << "shared_interface::acquire_channel(): the_scheduler_number = " << the_scheduler_number << std::endl;
	channel_number temp_channel_number = invalid_channel_number;

	// Pop a channel number.
	scheduler_interface_[the_scheduler_number]
	.pop_back_channel_number(&temp_channel_number, get_owner_id());
	
	// TODO: Optimize.
	if (temp_channel_number == invalid_channel_number) {
		return false;
	}

	*the_channel_number = temp_channel_number;
	init_acquired_channel(temp_channel_number, the_scheduler_number);
	return true; /// TODO: Timeout doesn't work yet.
}

inline bool shared_interface::acquire_channel2(channel_number
the_channel_number, scheduler_number the_scheduler_number, uint32_t spin_count,
uint32_t timeout_milliseconds) {
	
	if (scheduler_interface_[the_scheduler_number].pop_back_channel_number(
	the_channel_number, get_owner_id()))
	{
		init_acquired_channel(the_channel_number, the_scheduler_number);
		return true;
	}
	return false;
}

inline void shared_interface::init_acquired_channel(channel_number the_channel_number,
scheduler_number the_scheduler_number)
{
#if 0
	// Mark this channel as owned by this client.
	client_interface().set_channel_flag(the_scheduler_number,
	the_channel_number);
#endif

#if 0	
	// The number of owned channels counter is incremented.
	client_interface().increment_number_of_allocated_channels();
#endif
	
    // Set the chunk base address relative to the clients address space.
	channel_[the_channel_number].in_overflow().set_chunk_ptr(chunk_);
    
	// Set index to the scheduler_interface.
	channel_[the_channel_number].set_scheduler_number(the_scheduler_number);
	
	// Set pointer to the scheduler_interface.
	channel_[the_channel_number].set_scheduler_interface
	(&scheduler_interface_[the_scheduler_number]);
	
	// Set index to the client_interface.
	channel_[the_channel_number].set_client_number(client_number_);
	
	// Set pointer to the client_interface.
	channel_[the_channel_number].set_client_interface_as_qword
	(scheduler_interface_[the_scheduler_number].get_client_interface_as_qword()
	+(client_number_ * sizeof(client_interface_type)));
	
	// Set the channel number flag after having set pointers to the
	// scheduler_interface and the client_interface.
	// This is done to enable the server scanning of the particular channel.
	scheduler_interface_[the_scheduler_number]
	.set_channel_number_flag(the_channel_number);
}

#if 0
inline void shared_interface::release_channel(channel_number the_channel_number)
{
	scheduler_interface_type* the_scheduler
	= channel_[the_channel_number].scheduler();
	
	// Force the load of the pointer to the scheduler before it is released.
	_mm_mfence();
	
	/// Mark the channel to be released. The scheduler is responsible for
	/// the release of the channel. Nobody else may use the channel until
	/// it is freed.
	channel_type& the_channel = channel_[the_channel_number];

	the_channel.set_to_be_released();

	/// Notify the scheduler. It may happen that the scheduler has already
	/// released the channel now, in which case the notification do no harm
	/// because it notifies an existing scheduler. Once a valid pointer to a
	/// scheduler have been obtained, it can always be used since a scheduler
	/// can not quit.
	the_channel.scheduler()->notify(scheduler_work_event(the_channel
	.get_scheduler_number()));
}
#endif

//------------------------------------------------------------------------------
// TODO: Rename to acquire_linked_chunks()
inline bool shared_interface::client_acquire_linked_chunks(chunk_index& head,
std::size_t size, uint32_t timeout_milliseconds) {
	return shared_chunk_pool_->acquire_linked_chunks(chunk_, head, size,
	client_interface_, timeout_milliseconds);
}

inline bool shared_interface::client_acquire_linked_chunks_counted(chunk_index&
head, std::size_t size, uint32_t timeout_milliseconds) {
	return shared_chunk_pool_->acquire_linked_chunks_counted(chunk_, head, size,
	client_interface_, timeout_milliseconds);
}

// TODO: Rename to release_linked_chunks()
inline bool shared_interface::client_release_linked_chunks(chunk_index& head,
uint32_t timeout_milliseconds) {
	return shared_chunk_pool_->release_linked_chunks(chunk_, head,
	client_interface_, timeout_milliseconds);
}

//------------------------------------------------------------------------------
template<typename U>
inline std::size_t shared_interface::acquire_from_shared_to_private(U&
private_chunk_pool, std::size_t chunks_to_acquire, client_interface_type*
client_interface_ptr, uint32_t timeout_milliseconds) {
	return shared_chunk_pool_->acquire_to_chunk_pool(private_chunk_pool,
	chunks_to_acquire, client_interface_ptr, timeout_milliseconds);
}

template<typename U>
inline std::size_t shared_interface::acquire_from_shared_chunk_pool(U&
private_chunk_pool, std::size_t chunks_to_acquire, client_interface_type*
client_interface_ptr, uint32_t timeout_milliseconds) {
	return shared_chunk_pool_->acquire(private_chunk_pool,
	chunks_to_acquire, client_interface_ptr, timeout_milliseconds);
}

template<typename U>
inline std::size_t shared_interface::release_from_private_to_shared(U&
private_chunk_pool, std::size_t chunks_to_release, client_interface_type*
client_interface_ptr, uint32_t timeout_milliseconds) {
	return shared_chunk_pool_->release_from_chunk_pool(private_chunk_pool,
	chunks_to_release, client_interface_ptr, timeout_milliseconds);
}

template<typename U>
inline std::size_t shared_interface::release_to_shared_chunk_pool(U&
private_chunk_pool, std::size_t chunks_to_release, client_interface_type*
client_interface_ptr, uint32_t timeout_milliseconds) {
	return shared_chunk_pool_->release(private_chunk_pool,
	chunks_to_release, client_interface_ptr, timeout_milliseconds);
}

//------------------------------------------------------------------------------
inline void shared_interface::show_linked_chunks(chunk_index head) {
	shared_chunk_pool_->show_linked_chunks(chunk_, head);
}

inline shared_chunk_pool_type& shared_interface::shared_chunk_pool() const {
	return *shared_chunk_pool_;
}

inline chunk_type& shared_interface::chunk(std::size_t n) const {
	return chunk_[n];
}

template<typename Ptr>
inline chunk_index shared_interface::get_index_from_pointer(Ptr ptr) const {
	return std::size_t(((char*) ptr) -((char*) chunk_)) / chunk_type::size();
}

inline common_scheduler_interface_type&
shared_interface::common_scheduler_interface() const {
	return *common_scheduler_interface_;
}

inline scheduler_interface_type&
shared_interface::scheduler_interface(std::size_t n) const {
	return scheduler_interface_[n];
}

inline common_client_interface_type&
shared_interface::common_client_interface() const {
	return *common_client_interface_;
}

inline client_interface_type&
shared_interface::client_interface(std::size_t n) const {
	return client_interface_[n];
}

inline client_interface_type& shared_interface::client_interface() const {
	return client_interface_[client_number_];
}

inline channel_type& shared_interface::channel(std::size_t n) const {
	return channel_[n];
}

#if 0
inline void
shared_interface::database_state(common_client_interface_type::state s) {
	common_client_interface_->database_state(s);
}

inline common_client_interface_type::state
shared_interface::database_state() const {
	return common_client_interface_->database_state();
}
#endif

inline void shared_interface::init() {
	simple_shared_memory_manager* pm = new(mapped_region_.get_address())
	simple_shared_memory_manager;
	
	// Try to find the shared memory objects.
	
	// Find the chunks.
	chunk_ = (chunk_type*) pm->find_named_block
	(starcounter_core_shared_memory_chunks_name);
	
	if (!chunk_) {
		// error: could not find the chunks
		throw shared_interface_exception(SCERRFINDCHUNKS);
	}
	
	// Find the shared_chunk_pool.
	shared_chunk_pool_ = (shared_chunk_pool_type*) pm->find_named_block
	(starcounter_core_shared_memory_shared_chunk_pool_name);
	
	if (!shared_chunk_pool_) {
		// error: could not find the shared_chunk_pool
		throw shared_interface_exception(SCERRFINDSHAREDCHUNKPOOL);
	}
	
	// Find the common_scheduler_interface.
	common_scheduler_interface_ = (common_scheduler_interface_type*)
	pm->find_named_block
	(starcounter_core_shared_memory_common_scheduler_interface_name);
	
	if (!common_scheduler_interface_) {
		// error: could not find the common_scheduler_interface
		throw shared_interface_exception(SCERRFINDCOMMONSCHEDINTERFACE);
	}
	
	// Find the scheduler_interfaces.
	scheduler_interface_ = (scheduler_interface_type*) pm->find_named_block
	(starcounter_core_shared_memory_scheduler_interfaces_name);
	
	if (!scheduler_interface_) {
		// Did not find the scheduler_interfaces.
		throw shared_interface_exception(SCERRFINDSCHEDULERINTERFACES);
	}
	
	// Find the common_client_interface.
	common_client_interface_ = (common_client_interface_type*)
	pm->find_named_block
	(starcounter_core_shared_memory_common_client_interface_name);
	
	if (!common_client_interface_) {
		// Did not find the common_client_interface.
		throw shared_interface_exception(SCERRFINDCOMMONCLIENTINTERFACE);
	}
	
	// Find the client_interfaces.
	client_interface_ = (client_interface_type*) pm->find_named_block
	(starcounter_core_shared_memory_client_interfaces_name);
	
	if (!client_interface_) {
		// Did not find the client_interfaces.
		throw shared_interface_exception(SCERRFINDCLIENTINTERFACES);
	}
	
	// Find the channels.
	channel_ = (channel_type*)
	pm->find_named_block(starcounter_core_shared_memory_channels_name);
	
#if defined (IPC_VERSION_2_0)
	if (channel_) {
		// Each worker have its own slice of the array of channels,
		// in the range channel(0) to channel(number_of_active_schedulers() -1).
		// channel_ is set to point to channel_ +id * number_of_active_schedulers()
		channel_ += id() * number_of_active_schedulers();
	}
	else {
		// error: could not find the channels
		throw shared_interface_exception(SCERRFINDCHANNELS);
	}
#else // !defined (IPC_VERSION_2_0)
	if (!channel_) {
		// error: could not find the channels
		throw shared_interface_exception(SCERRFINDCHANNELS);
	}
#endif // defined (IPC_VERSION_2_0)
}

inline pid_type shared_interface::get_pid() const {
	return pid_;
}

inline owner_id shared_interface::get_owner_id() const {
	return owner_id_;
}

inline client_number shared_interface::get_client_number() const {
	return client_number_;
}

inline ::HANDLE& shared_interface::open_client_work_event(std::size_t i) {
	// Not checking if the event is already open.
	if ((client_work_event() = ::OpenEvent(SYNCHRONIZE | EVENT_MODIFY_STATE,
	FALSE, client_interface(i).work_notify_name())) == NULL) {
		// Failed to open the event.
		client_work_event() = 0;
		throw shared_interface_exception(SCERROPENCLIENTWORKEVENT);
	}
	return client_work_event();
}

inline void shared_interface::close_client_work_event() {
	client_work_event() = 0;
}

inline ::HANDLE& shared_interface::client_work_event() {
	return client_work_;
}

inline const ::HANDLE& shared_interface::client_work_event() const {
	return client_work_;
}

inline ::HANDLE& shared_interface::open_scheduler_work_event(std::size_t i) {
	// Not checking if the event is already open.
	if ((scheduler_work_event(i) = ::OpenEvent(SYNCHRONIZE | EVENT_MODIFY_STATE,
	FALSE, scheduler_interface(i).work_notify_name())) == NULL) {
		// Failed to open the event.
		scheduler_work_event(i) = 0;
		throw shared_interface_exception(SCERROPENSCHEDULERWORKEVENT);
	}
	return scheduler_work_event(i);
}

inline void shared_interface::close_scheduler_work_event(std::size_t i) {
	scheduler_work_event(i) = 0;
}

inline ::HANDLE& shared_interface::scheduler_work_event(std::size_t i) {
	return scheduler_work_[i];
}

inline const ::HANDLE& shared_interface::scheduler_work_event(std::size_t i) const
{
	return scheduler_work_[i];
}

inline ::HANDLE& shared_interface::open_scheduler_number_pool_not_empty_event(std::size_t i) {
	// Not checking if the event is already open.
	if ((scheduler_number_pool_not_empty_event(i) = ::OpenEvent(SYNCHRONIZE | EVENT_MODIFY_STATE,
	FALSE, scheduler_interface(i).channel_number_queue().not_empty_notify_name())) == NULL) {
		// Failed to open the event.
		scheduler_number_pool_not_empty_event(i) = 0;
		throw shared_interface_exception(SCERROPENSCHEDNUMPOOLNOTEMPTYEV);
	}
	return scheduler_number_pool_not_empty_event(i);
}

inline void shared_interface::close_scheduler_number_pool_not_empty_event(std::size_t i) {
	scheduler_number_pool_not_empty_event(i) = 0;
}

inline ::HANDLE& shared_interface::scheduler_number_pool_not_empty_event(std::size_t i) {
	return scheduler_number_pool_not_empty_[i];
}

inline const ::HANDLE& shared_interface::scheduler_number_pool_not_empty_event
(std::size_t i) const {
	return scheduler_number_pool_not_empty_[i];
}

inline ::HANDLE& shared_interface::open_scheduler_number_pool_not_full_event(std::size_t i) {
	// Not checking if the event is already open.
	if ((scheduler_number_pool_not_full_event(i) = ::OpenEvent(SYNCHRONIZE | EVENT_MODIFY_STATE,
	FALSE, scheduler_interface(i).channel_number_queue().not_full_notify_name())) == NULL) {
		// Failed to open the event.
		scheduler_number_pool_not_full_event(i) = 0;
		throw shared_interface_exception(SCERROPENSCHEDNUMPOOLNOTFULLEV);
	}
	return scheduler_number_pool_not_full_event(i);
}

inline void shared_interface::close_scheduler_number_pool_not_full_event(std::size_t i) {
	scheduler_number_pool_not_full_event(i) = 0;
}

inline ::HANDLE& shared_interface::scheduler_number_pool_not_full_event(std::size_t i) {
	return scheduler_number_pool_not_full_[i];
}

inline const ::HANDLE& shared_interface::scheduler_number_pool_not_full_event
(std::size_t i) const {
	return scheduler_number_pool_not_full_[i];
}

#if defined (IPC_VERSION_2_0)
inline uint32_t shared_interface::id() const {
	return id_;
}
#endif // defined (IPC_VERSION_2_0)

} // namespace core
} // namespace starcounter

#endif // STARCOUNTER_CORE_IMPL_SHARED_INTERFACE_HPP
