//
// impl/worker.hpp
// interprocess_communication/test
//
// Copyright © 2006-2013 Starcounter AB. All rights reserved.
// Starcounter® is a registered trademark of Starcounter AB.
//
// Implementation of class worker.
//

#ifndef STARCOUNTER_INTERPROCESS_COMMUNICATION_IMPL_WORKER_HPP
#define STARCOUNTER_INTERPROCESS_COMMUNICATION_IMPL_WORKER_HPP

// Implementation

namespace starcounter {
namespace interprocess_communication {

using namespace starcounter::core;

worker::worker()
: state_(stopped),
shared_(),
worker_id_(0),
num_active_schedulers_(0),
//chunk_pool_(chunks_total_number_max),
#if !defined (IPC_VERSION_2_0)
num_channels_(0),
#endif // !defined (IPC_VERSION_2_0)
pushed_(0),
popped_(0) {}

worker::~worker() {
	join();
	/// TODO: Release resources.
	state_ = stopped;
}

/// Start the worker.
void worker::start() {
	auto worker_number = worker_id_;

	///=========================================================================
	/// Initialize
	///=========================================================================
	if (!shared().acquire_client_number2((client_number)worker_number)) {
		// Failed to acquire client number.
		throw worker_exception(4000);
	}

#if 0
	if (!shared().acquire_client_number()) {
		// Failed to acquire client number.
		throw worker_exception(4000);
	}
#endif
	
	chunk_type* chunk_ptr = &shared().chunk(0);
	//get_chunk_pool_list(scheduler_num).set_chunk_ptr(chunk_ptr);

	size_t chunks_per_worker_scheduler_pair = 64;
	//= shared().shared_chunk_pool().size() / 2 / num_active_schedulers_;
	
	size_t acquired_chunks = 0;
	
	// Acquire a channel for each scheduler.
	for (scheduler_number scheduler_num = 0; scheduler_num < num_active_schedulers_; ++scheduler_num) {
		// Try to acquire a bunch of chunks from the shared chunk
		// pool to the private chunk pool.
		get_chunk_pool_list(scheduler_num).set_chunk_ptr(chunk_ptr);
		
		acquired_chunks += shared().acquire_from_shared_chunk_pool
		(get_chunk_pool_list(scheduler_num), chunks_per_worker_scheduler_pair,
		&shared().client_interface(), 100 /* milliseconds timeout */);
		
#if 0 // Testing chunk_pool_list() functions acquire_linked_chunks() and release_linked_chunks()
		chunk_index head;
		
		// Testing the chunk_pool_list. First try to link together 3 chunks:
		if (get_chunk_pool_list(scheduler_num).acquire_linked_chunks(head, 3) == true) {
			std::cout << "Successfully linked together 3 chunks!\n";
			show_linked_chunks(chunk_ptr, head);
			
			// Then unlink them:
			get_chunk_pool_list(scheduler_num).release_linked_chunks(head);
			
			// And link again:
			if (get_chunk_pool_list(scheduler_num).acquire_linked_chunks(head, 3) == true) {
				std::cout << "Successfully linked together 3 chunks again!\n";
				show_linked_chunks(chunk_ptr, head);
			
				// Then unlink them:
				get_chunk_pool_list(scheduler_num).release_linked_chunks(head);
			}
			else {
				std::cout << "Failed to link together 3 chunks again.\n";
			}
		}
		else {
			std::cout << "Failed to link together 3 chunks.\n";
		}
#endif // Testing chunk_pool_list() functions acquire_linked_chunks() and release_linked_chunks()

#if defined (IPC_VERSION_2_0)
		/// NOTE: shared_interface::acquire_channel() must not be used!
		/// TODO: disable shared_interface::acquire_channel().
		
		// Mark this channel as owned by this client.
		shared().client_interface().set_channel_flag(scheduler_num, scheduler_num);
		shared().client_interface().increment_number_of_allocated_channels();
		
		// Set the chunk base address relative to the clients address space.
		shared().channel(scheduler_num).in_overflow().set_chunk_ptr(&shared().chunk(0));
		
		// Set index to the scheduler_interface.
		shared().channel(scheduler_num).set_scheduler_number(scheduler_num);
		
		// Set pointer to the scheduler_interface.
		shared().channel(scheduler_num).set_scheduler_interface
		(&scheduler_interface_[the_scheduler_number]);
		
		// Set index to the client_interface.
		shared().channel(scheduler_num).set_client_number(client_number_);
		
		// Set pointer to the client_interface.
		shared().channel(scheduler_num).set_client_interface_as_qword
		(scheduler_interface_[the_scheduler_number].get_client_interface_as_qword()
		+(client_number_ * sizeof(client_interface_type)));
		
		// Set the channel number flag after having set pointers to the
		// scheduler_interface and the client_interface.
		scheduler_interface_[the_scheduler_number].set_channel_number_flag(scheduler_num);
		
#else // !defined (IPC_VERSION_2_0)
		channel_[scheduler_num] = (worker_number * num_active_schedulers_) +
		scheduler_num ;

		if (!shared().acquire_channel2(channel_[scheduler_num], scheduler_num)) {
			std::cout << " worker[" << worker_id_ << "] error: "
			"invalid channel number." << std::endl;
		}

#if 0
		channel_[scheduler_num] = invalid_channel_number;

		if (!shared().acquire_channel(&channel_[scheduler_num], scheduler_num)) {
			std::cout << " worker[" << worker_id_ << "] error: "
			"invalid channel number." << std::endl;
		}
#endif
#endif // defined (IPC_VERSION_2_0)
		++num_channels_;
	}
	
	channel_[num_channels_] = invalid_channel_number;

	shared().client_interface().set_available();
	
	std::cout << "worker[" << id() << "]: acquired " << acquired_chunks
	<< " chunks, divided evenly over " << num_active_schedulers_
	<< " private chunk pools." << std::endl;

	///=========================================================================
	/// Start worker thread
	///=========================================================================
	thread_.create((thread::start_routine_type) &worker::work, this); 
	thread_handle_ = thread_.native_handle();	
}

inline worker::state worker::get_state() {
	return state_;
}

inline void worker::set_state(state s) {
	::InterlockedExchange((LONG*) &state_, s);
}

inline bool worker::is_running() {
	return get_state() == running;
}

void worker::work(worker* worker)
try {
	chunk_index request_message;
	chunk_index response_message;
	//uint32_t scan_out_buffers = 0;

	// worked indicates if this worker did some work during the last iteration,
	// scanning the set of channels to push or pop chunks, etc.
	bool worked = false;
	bool scanned_channel_out_buffers = false;
	uint64_t scan_counter = scan_counter_preset;
	uint64_t statistics_counter = 0;
    uint64_t popped_from_channels[32] = { 0 };
    uint64_t pushed_to_channels[32] = { 0 };

	worker->set_state(running);

	///=========================================================================
	/// Worker loop
	///=========================================================================
	
	while (worker->is_running()) {
		///=====================================================================
		/// For each channel, try to push a BMX ping request message. If the
		/// channel::in_overflow queue is not empty, the request message is
		/// pushed to it in order to preserve the order of production.
		///
		/// Regardless if a new request message was created or not:
		/// If there are any request messages in the channel::in_overflow queue,
		/// try to move all of them to the channel::in buffer.
		///=====================================================================
		
		for (std::size_t n = 0; n < worker->num_channels(); ++n) {
			size_t channel_num = worker->channel(n);
			
			// Reference used as shorthand.
			channel_type& the_channel
			= worker->shared().channel(channel_num);
			
			scheduler_number scheduler_num = the_channel.get_scheduler_number();
			
			// Reference used as shorthand.
			chunk_pool_list_type& chunk_pool_list_for_this_scheduler
			= worker->get_chunk_pool_list(scheduler_num);
			
			// Try to acquire chunk(s) for the request message from private
			// chunk pool.
			if (!chunk_pool_list_for_this_scheduler.empty()) {
				request_message = chunk_pool_list_for_this_scheduler.front();
				chunk_pool_list_for_this_scheduler.pop_front();
				
				// Acquired enough chunk(s) for the request message.
				// Construct a BMX ping and write it into the chunk.
				shared_memory_chunk* smc = static_cast<shared_memory_chunk*>
				(&worker->shared().chunk(request_message));
				
				sc_bmx_construct_ping(0, smc);
				
				if (the_channel.in_overflow().empty()) {
					if (the_channel.in.try_push_front(request_message)) {
						// Pushed the request message to the channel::in
						// buffer. Notify the scheduler that the channel::in
						// buffer might not be empty.
						the_channel.scheduler()->notify
						(worker->shared().scheduler_work_event
						(the_channel.get_scheduler_number()));
						
						++pushed_to_channels[n];
						++worker->pushed_; // Used for statistics.
						worked = true;
						
						// Continue with the next channel.
						continue;
					}
					else {
						// Failed to push the request message to the channel::in.
						// Therefore the request message is pushed to the
						// channel::in_overflow queue. The order of request
						// message production is preserved.
						the_channel.in_overflow().push_back(request_message);
					}
					
					// Continue with the next channel.
					continue;
				}
				else {
					// The channel::in_overflow queue is not empty. Therefore
					// the request message is pushed to the channel::in_overflow
					// queue. The order of request message production is
					// preserved.
					the_channel.in_overflow().push_back(request_message);
				}
			}
			else {
				// The IPC test will not attempt to acquire more chunks.
			}
			
			// Try to empty the channel::in_overflow queue by moving all request
			// messages from it to the channel::in buffer.
			while (!the_channel.in_overflow().empty()) {
				if (!the_channel.in.try_push_front(the_channel.in_overflow()
				.front())) {
					// Failed to push the request message. It is therefore not
					// removed from the channel::in_overflow queue.
					break;
				}
				
				// The request message was successfully pushed to the
				// channel::in buffer. Notify that the channel::in buffer might
				// not be empty.
				the_channel.scheduler()->notify
				(worker->shared().scheduler_work_event
				(the_channel.get_scheduler_number()));
				
				++pushed_to_channels[n];
				++worker->pushed_; // Used for statistics.
				worked = true;
				
				// Remove the request message from the channel::in_overflow
				// queue.
				the_channel.in_overflow().pop_front();
			}
		}
		
		///=====================================================================
		/// Round-robin check channel::out buffers and see if there are any
		/// response messages and process them.
		///=====================================================================
		
		//if (++scan_out_buffers > 13) { // Slow down scanning of out buffers
scan_channel_out_buffers:
			//scan_out_buffers = 0;
			for (std::size_t n = 0; n < worker->num_channels(); ++n) {
				size_t channel_num = worker->channel(n);
				
				// Reference used as shorthand.
				channel_type& the_channel
				= worker->shared().channel(channel_num);
				
				scheduler_number scheduler_num = the_channel.get_scheduler_number();
				
				// Reference used as shorthand.
				chunk_pool_list_type& chunk_pool_list_for_this_scheduler
				= worker->get_chunk_pool_list(scheduler_num);
				
				// Check if there is a response message and process it.
				if (the_channel.out.try_pop_back(&response_message) == true) {
					// A response message on the channel was received. Notify
					// the scheduler that the channel::out buffer might not be
					// full.
					the_channel.scheduler()->notify(worker->shared()
					.scheduler_work_event(the_channel.get_scheduler_number()));
					
					++popped_from_channels[n];
					++worker->popped_; // Used for statistics.
					worked = true;
					
					// Handle all responses in this chunk.
					shared_memory_chunk* smc = (shared_memory_chunk*)
					&(worker->shared().chunk(response_message));
					
					uint64_t ping_data;
					uint32_t error_code = sc_bmx_parse_pong(smc, &ping_data);
					
					if (error_code == 0) {
						// Successfully processed the response.
					}
					else {
						// An error occurred.
						std::cout << "worker[" << worker->id() << "] error: "
						"handle_responses() failed with the error code "
						<< error_code << std::endl;
					}
					
					// Release the response chunk.
					chunk_pool_list_for_this_scheduler.push_front(response_message);
				}
			}
			scanned_channel_out_buffers = true;
		//} // Slow down scanning of out buffers
		
#if 0
		/// Show some statistics
		if ((++statistics_counter & ((1 << 24) -1)) == 0) {
			std::cout << "worker[" << worker->id() << "]: pushed "
			<< worker->pushed_ << ", popped " << worker->popped_ << std::endl;
			
			for (int i = 0; i < worker->num_channels(); i++) {
				std::cout << "worker[" << worker->id() << "]: pushed to channel "
				<< i << ": " << pushed_to_channels[i] << std::endl;
			}
			
			for (int i = 0; i < worker->num_channels(); i++) {
				std::cout << "worker[" << worker->id() << "]: popped from channel "
				<< i << ": " << popped_from_channels[i] << std::endl;
			}

			std::cout << std::endl;
		}
#endif
		
		// Check if this worker wait for work. Assuming not.
		if (worked) {
			// Did some work (pushing or popping, etc.) during last scan.
			scan_counter = scan_counter_preset;
			worked = false;
			continue;
		}
		else {
			// Did not find work to do during last scan.
			if (scan_counter > 1) {
				--scan_counter;
				_mm_pause();
				continue;
			}
			else if (scan_counter == 1) {
				// Preparing to wait.
				worker->shared().client_interface().set_notify_flag(true);
				// Therefore need to scan one more time, just in case a
				// scheduler read the notify flag as false just before it was
				// set, and pushed or popped on any channel without notifying.
				--scan_counter;
				_mm_pause();
				continue;
			}
			else if (scan_counter == 0) {
				// Nothing was pushed or popped for scan_count_reset number of
				// iterations. This thread will now wait for any scheduler to
				// push or pop on any of this worker's channels.
				
				// Must not go to sleep if have not scanned out buffers.
				if (scanned_channel_out_buffers) {
					std::cout << "worker[" << worker->id() << "]: waits. . ." << std::endl;
					if (worker->shared().client_interface().wait_for_work
					(worker->shared().client_work_event(), wait_for_work_milli_seconds)
					== true) {
						// This worker was notified there is work to do.
						worker->shared().client_interface().set_notify_flag(false);
						scan_counter = scan_counter_preset;
					}
					else {
						// A timeout occurred.
						worker->shared().client_interface().set_notify_flag(false);
						scan_counter = scan_counter_preset; // or 1?
						
						std::cout << "worker[" << worker->id() << "]: timeout; not waiting" << std::endl;
					}
					
					scanned_channel_out_buffers = false;
				}
				else {
					goto scan_channel_out_buffers;
				}
			}
		}
	}

#if 0	
	// Call this before exit is called for the thread.
	worker->release_all_resources();
#endif
	
	/// Exit thread.
	return;
}
catch (starcounter::interprocess_communication::worker_exception& e) {
	std::cout << " worker[" << worker->id() << "] error: worker exception "
	<< "caught with error code " << e.error_code() << std::endl;
}

void worker::join() {
	thread_.join();
}

inline worker& worker::set_worker_number(std::size_t n) {
	worker_id_ = n;
	return *this;
}

inline worker& worker::set_active_schedulers(std::size_t n) {
	num_active_schedulers_ = n;
	return *this;
}

inline worker& worker::set_shared_interface() {
	_mm_mfence();
	shared().init(segment_name_, monitor_interface_name_, pid_, owner_id_);
	_mm_mfence();
	return *this;
}

inline worker& worker::set_segment_name(const std::string& segment_name) {
	segment_name_ = segment_name;
	return *this;
}

inline std::string worker::get_segment_name() const {
	return segment_name_;
}

inline worker& worker::set_monitor_interface_name(const std::string& monitor_interface_name) {
	monitor_interface_name_ = monitor_interface_name;
	return *this;
}

inline std::string worker::get_monitor_interface_name() const {
	return monitor_interface_name_;
}

inline worker& worker::set_pid(const pid_type pid) {
	pid_ = pid;
	return *this;
}

inline pid_type worker::get_pid() const {
	return pid_;
}

inline worker& worker::set_owner_id(const owner_id oid) {
	owner_id_ = oid;
	return *this;
}

inline owner_id worker::get_owner_id() const {
	return owner_id_;
}

inline void worker::show_linked_chunks(chunk_type* chunk_base, chunk_index head)
{
	for (chunk_index current = head; current != chunk_type::link_terminator;
	current = chunk_base[current].get_link()) {
		chunk_index next = chunk_base[current].get_link();
		std::cout << "chunk[" << current << "] links to chunk[" << next
		<< "]" << std::endl;
	}
	std::cout << "end" << std::endl;
}

#if 0 // NOT COMPLETE
inline void worker::release_all_resources() {
	///-------------------------------------------------------------------------
	for (std::size_t n = 0; n < num_channels_; ++n) {
		channel_type& the_channel = shared().channel(channel_[n]);
		the_channel.set_to_be_released();
		the_channel.scheduler()->notify();
	}
	///-------------------------------------------------------------------------
	shared().common_client_interface().increment_client_interfaces_to_clean_up();
	// I think it is important that the increment above is done before
	// marking for clean up below. Not sure about the order, the monitor does it
	// in this order.
	///-------------------------------------------------------------------------
	_mm_mfence();
	shared().client_interface().get_owner_id().mark_for_clean_up();
}
#endif // NOT COMPLETE

#if 0
inline void worker::release_all_resources() {
	return; // Let the IPC monitor start the cleanup instead.

	shared().common_client_interface().increment_client_interfaces_to_clean_up();
	
	// I think it is important that the increment above is done before marking
	// for clean up below.
	_mm_mfence();
	
	// Mark the client_interface for clean up.
	shared().client_interface().get_owner_id().mark_for_clean_up();
	
	// For each of the channels the client thread own, try to notify the
	// scheduler(s) via those channels.
	
	// For each mask word, bitscan to find the channel indices.
	for (uint32_t ch_index = 0; ch_index < resource_map::channels_mask_size;
	++ch_index) {
		for (resource_map::mask_type mask = shared().client_interface()
		.get_resource_map().get_owned_channels_mask(ch_index); mask;
		mask &= mask -1) {
			uint32_t ch = bit_scan_forward(mask);
			ch += ch_index << resource_map::shift_bits_in_mask_type;
			channel_type& the_channel = shared().channel(ch);
			scheduler_number the_scheduler_number = the_channel
			.get_scheduler_number();
			
			/// TODO: Check if the_scheduler_number is out of range!!!
			
			scheduler_interface_type* scheduler_interface_ptr = &shared()
			.scheduler_interface(the_scheduler_number);
			
			// A fence is needed so that all accesses to the channel is
			// completed when marking it to be released.
			_mm_mfence();
			
			// Mark channel to be released. After this the channel cannot be
			// accessed.
			the_channel.set_to_be_released();
			
			if (scheduler_interface_ptr) {
				// The scheduler may be waiting so notify it.
				if ((scheduler_interface_ptr->notify_scheduler_to_do_clean_up
				(shared().scheduler_work_event(the_channel.get_scheduler_number())))
				== true) {
					// Succeessfully notified the scheduler on this channel.
				}
			}
		}
	}
}
#endif

} // namespace interprocess_communication
} // namespace starcounter

#endif // STARCOUNTER_INTERPROCESS_COMMUNICATION_IMPL_WORKER_HPP
