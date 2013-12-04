//
// impl/shared_chunk_pool.hpp
//
// Copyright © 2006-2013 Starcounter AB. All rights reserved.
// Starcounter® is a registered trademark of Starcounter AB.
//
// Implementation of class shared_chunk_pool.
//

#ifndef STARCOUNTER_CORE_IMPL_SHARED_CHUNK_POOL_HPP
#define STARCOUNTER_CORE_IMPL_SHARED_CHUNK_POOL_HPP

// Implementation

namespace starcounter {
namespace core {

template<class T, class Alloc>
inline shared_chunk_pool<T, Alloc>::shared_chunk_pool(const char* segment_name,
size_type buffer_capacity, const allocator_type& alloc)
: container_(buffer_capacity, alloc), unread_(0) {
}

template<class T, class Alloc>
inline typename shared_chunk_pool<T, Alloc>::size_type
shared_chunk_pool<T, Alloc>::size() const {
	return unread_;
}

template<class T, class Alloc>
inline typename shared_chunk_pool<T, Alloc>::size_type
shared_chunk_pool<T, Alloc>::capacity() const {
	return container_.capacity();
}

#if 0
template<class T, class Alloc>
inline bool shared_chunk_pool<T, Alloc>::empty() const {
	return unread_ == 0;
}

template<class T, class Alloc>
inline bool shared_chunk_pool<T, Alloc>::full() const {
	return unread_ == capacity();
}
#endif

// For clients and schedulers.
template<class T, class Alloc>
inline bool shared_chunk_pool<T, Alloc>::acquire_linked_chunks(chunk_type*
chunk_base, chunk_index& head, std::size_t size, client_interface_type*
client_interface_ptr, smp::spinlock::milliseconds timeout) {
	std::size_t chunks_to_acquire = (size +chunk_type::static_data_size -1)
	/ chunk_type::static_data_size;
	
	// No recovery is done here in IPC version 1.0 so not locking with owner_id.
	smp::spinlock::scoped_lock lock(spinlock(), timeout -timeout.tick_count());

	if (!lock.owns()) {
		// The timeout_milliseconds time period has elapsed. Failed to acquire
		// linked chunks.
		return false;
	}
	
	// Check if enough space is available, assuming it is.
	if (chunks_to_acquire <= this->size()) {
		// Enough space is available, start linking chunks together.
		chunk_index prev;
		chunk_index current;
		current = container_[--unread_];
		_mm_mfence(); // TODO: Figure if _mm_sfence() is enough.
#if 0
		client_interface_ptr->set_chunk_flag(current);
#endif
		head = current;
		
		for (std::size_t i = 1; i < chunks_to_acquire; ++i) {
			prev = current;
			current = container_[--unread_];
			_mm_mfence(); // TODO: Figure if _mm_mfence() is enough/required.

#if 0			
			// This must never occur before the pop_back() above, because if
			// it occurs before pop_back() and the client process terminates
			// unexpectedly (crashes), then the clean up will be messed up
			// because of duplicates. Only way to avoid that would be if the
			// shared_chunk_pool used a bit map in conjunction with the queue,
			// to mark free chunks, because then duplicates could not appear
			// obviously.
			client_interface_ptr->set_chunk_flag(current);
#endif
			chunk_base[prev].set_link(current);
		}
		
		// Terminate the last chunk.
		chunk_base[current].terminate_link();
		
		// Notify that the queue is not full.
		lock.unlock();

		// Successfully acquired the chunks and linked them.
		return true;
    }
    else {
		// Not enough space available. Failed to acquire linked chunks.
		return false;
	}
}

//------------------------------------------------------------------------------
template<class T, class Alloc>
inline bool shared_chunk_pool<T, Alloc>::acquire_linked_chunks_counted(chunk_type*
chunk_base, chunk_index& head, std::size_t num_chunks_to_acquire, client_interface_type*
client_interface_ptr, smp::spinlock::milliseconds timeout) {
	// No recovery is done here in IPC version 1.0 so not locking with owner_id.
	smp::spinlock::scoped_lock lock(spinlock(), timeout -timeout.tick_count());
	
	if (!lock.owns()) {
		// The timeout_milliseconds time period has elapsed. Failed to acquire
		// linked chunks.
		return false;
	}
	
	// Check if enough space is available, assuming it is.
	if (num_chunks_to_acquire <= this->size()) {
		// Enough space is available, start linking chunks together.
		chunk_index prev;
		chunk_index current;
		current = container_[--unread_];
		_mm_mfence(); // TODO: Figure if _mm_sfence() is enough.
#if 0
		client_interface_ptr->set_chunk_flag(current);
#endif
		head = current;
		
		for (std::size_t i = 1; i < num_chunks_to_acquire; ++i) {
			prev = current;
			current = container_[--unread_];
			
			_mm_mfence(); // TODO: Figure if _mm_mfence() is enough/required.

#if 0			
			// This must never occur before the pop_back() above, because if
			// it occurs before pop_back() and the client process terminates
			// unexpectedly (crashes), then the clean up will be messed up
			// because of duplicates. Only way to avoid that would be if the
			// shared_chunk_pool used a bit map in conjunction with the queue,
			// to mark free chunks, because then duplicates could not appear
			// obviously.
			client_interface_ptr->set_chunk_flag(current);
#endif
			chunk_base[prev].set_link(current);
		}
		
		// Terminate the last chunk.
		chunk_base[current].terminate_link();
		lock.unlock();

		// Successfully acquired the chunks and linked them.
		return true;
    }
    else {
		// Not enough space available. Failed to acquire linked chunks.
		return false;
	}
}

//------------------------------------------------------------------------------
template<class T, class Alloc>
inline bool shared_chunk_pool<T, Alloc>::release_linked_chunks(chunk_type*
chunk_, chunk_index& head, client_interface_type* client_interface_ptr,
smp::spinlock::milliseconds timeout) {
	// No recovery is done here in IPC version 1.0 so not locking with owner_id.
	smp::spinlock::scoped_lock lock(spinlock(), timeout -timeout.tick_count());
	
	if (!lock.owns()) {
		// The timeout_milliseconds time period has elapsed. Failed to release
		// the linked chunks.
		return false;
	}
	
	// Release linked chunks.
	chunk_index current = head;
	chunk_index this_chunks_link;
	
	do {
		this_chunks_link = chunk_[current].get_link();
		
		// Only chunks that are not pre-allocated in the channels are released.
		if (current >= channels) {
			// The queue is not full so the item can be pushed.
			container_.push_front(current);
#if 0
			client_interface_ptr->clear_chunk_flag(current);
#endif
			++unread_;
			// Can access the chunk since the lock have not been released yet.
		}
		
		current = this_chunks_link;
	} while (this_chunks_link != chunk_type::link_terminator);
	
	head = current;
	lock.unlock();

	// Successfully released all linked chunks.
	return true;
}

//------------------------------------------------------------------------------
// For clients:

//------------------------------------------------------------------------------
template<class T, class Alloc>
template<typename U>
inline std::size_t shared_chunk_pool<T, Alloc>::acquire_to_chunk_pool(U&
private_chunk_pool, std::size_t chunks_to_acquire, client_interface_type*
client_interface_ptr, smp::spinlock::milliseconds timeout) {
	// No recovery is done here in IPC version 1.0 so not locking with owner_id.
	smp::spinlock::scoped_lock lock(spinlock(), timeout -timeout.tick_count());

	if (!lock.owns()) {
		// The timeout_milliseconds time period has elapsed. Failed to acquire
		// the lock, therefore no chunks could be acquired.
		return 0;
	}
	
	chunk_index current;
	std::size_t acquired;
	
	for (acquired = 0; acquired < chunks_to_acquire; ++acquired) {
		if (is_not_empty()) {
			// The shared_chunk_pool is not empty. Get a chunk_index.
			current = container_[--unread_];
			
			// If the process terminates here, this chunk is leaked and will not
			// be recovered. It is most important to be able to recover all
			// chunks in most cases. TODO: Fixed it later.
			
			// Make sure the CPU (and compiler) don't re-order instructions.
			_mm_mfence();
			
			if (private_chunk_pool.push_front(current)) {
#if 0
				// Mark the chunk as owned by this client.
				client_interface_ptr->set_chunk_flag(current);

				// Havning reached this point the chunk can be recovered.
#endif			
				
				_mm_mfence(); // Remove if uneccessary.
			}
			else {
				// The private_chunk_pool is full. Put the current chunk_index
				// back.
				container_[unread_++] = current;
				lock.unlock();
				return acquired;
			}
		}
		else {
			// The shared_chunk_pool is empty.
			break;
		}
	}
	
	// Successfully acquired chunks_to_acquire number of chunks.
	lock.unlock();
	return acquired;
}

template<class T, class Alloc>
template<typename U>
inline std::size_t shared_chunk_pool<T, Alloc>::acquire(U& private_chunk_pool,
std::size_t chunks_to_acquire, client_interface_type* client_interface_ptr,
smp::spinlock::milliseconds timeout) {
	// No recovery is done here in IPC version 1.0 so not locking with owner_id.
	smp::spinlock::scoped_lock lock(spinlock(), timeout -timeout.tick_count());

	if (!lock.owns()) {
		// The timeout_milliseconds time period has elapsed. Failed to acquire
		// the lock, therefore no chunks could be acquired.
		return 0;
	}
	
	chunk_index current;
	std::size_t acquired;
	
	for (acquired = 0; acquired < chunks_to_acquire; ++acquired) {
		if (is_not_empty()) {
			// The shared_chunk_pool is not empty. Get a chunk_index.
			current = container_[--unread_];
			
			// If the process terminates here, this chunk is leaked and will not
			// be recovered. It is most important to be able to recover all
			// chunks in most cases. TODO: Fixed it later.
			
			// Make sure the CPU (and compiler) don't re-order instructions.
			_mm_mfence();
			
			private_chunk_pool.push_front(current);

#if 0
			// Mark the chunk as owned by this client.
			client_interface_ptr->set_chunk_flag(current);
#endif
			
			// Havning reached this point the chunk can be recovered.
			
			_mm_mfence(); // Remove if uneccessary.
		}
		else {
			// The shared_chunk_pool is empty.
			break;
		}
	}
	
	// Successfully acquired chunks_to_acquire number of chunks.
	lock.unlock();
	return acquired;
}

//------------------------------------------------------------------------------
template<class T, class Alloc>
template<typename U>
std::size_t shared_chunk_pool<T, Alloc>::release_from_chunk_pool(U&
private_chunk_pool, std::size_t chunks_to_release, client_interface_type*
client_interface_ptr, smp::spinlock::milliseconds timeout) {
	// No recovery is done here in IPC version 1.0 so not locking with owner_id.
	smp::spinlock::scoped_lock lock(spinlock(), timeout -timeout.tick_count());
	
	if (!lock.owns()) {
		// The timeout_milliseconds time period has elapsed. Failed to acquire
		// the lock, therefore no chunks could be released.
		return 0;
	}
	
	chunk_index current;
	std::size_t released;
	
	for (released = 0; released < chunks_to_release; ++released) {
		if (private_chunk_pool.pop_back(&current)) {
#if 0
			// Mark the chunk as not owned by this client.
			client_interface_ptr->clear_chunk_flag(current);
#endif
			
			// Make sure the CPU (and compiler) don't re-order instructions.
			_mm_mfence();
			
			// Push the current chunk_index.
			container_.push_front(current);
			++unread_;
		}
		else {
			// The private_chunk_pool is empty.
			break;
		}
	}
	
	lock.unlock();
	return released;
}

template<class T, class Alloc>
template<typename U>
std::size_t shared_chunk_pool<T, Alloc>::release(U& private_chunk_pool,
std::size_t chunks_to_release, client_interface_type* client_interface_ptr,
smp::spinlock::milliseconds timeout) {
	// No recovery is done here in IPC version 1.0 so not locking with owner_id.
	smp::spinlock::scoped_lock lock(spinlock(), timeout -timeout.tick_count());
	
	if (!lock.owns()) {
		// The timeout_milliseconds time period has elapsed. Failed to acquire
		// the lock, therefore no chunks could be released.
		return 0;
	}
	
	chunk_index current;
	std::size_t released;
	
	for (released = 0; released < chunks_to_release; ++released) {
		if (!private_chunk_pool.empty()) {
			current = private_chunk_pool.front();
			private_chunk_pool.pop_front();

			// Mark the chunk as not owned by this client.
			client_interface_ptr->clear_chunk_flag(current);
			
			// Make sure the CPU (and compiler) don't re-order instructions.
			_mm_mfence();
			
			// Push the current chunk_index.
			container_.push_front(current);
			++unread_;
		}
		else {
			// The private_chunk_pool is empty.
			break;
		}
	}
	
	lock.unlock();
	return released;
}

//------------------------------------------------------------------------------
// For schedulers:

//------------------------------------------------------------------------------
template<class T, class Alloc>
template<typename U>
inline std::size_t shared_chunk_pool<T, Alloc>::acquire_to_chunk_pool(U&
private_chunk_pool, std::size_t chunks_to_acquire,
smp::spinlock::milliseconds timeout) {
	// No recovery is done here in IPC version 1.0 so not locking with owner_id.
	smp::spinlock::scoped_lock lock(spinlock(), timeout -timeout.tick_count());
	
	if (!lock.owns()) {
		// The timeout_milliseconds time period has elapsed. Failed to acquire
		// the lock, therefore no chunks could be acquired.
		return 0;
	}
	
	chunk_index current;
	std::size_t acquired;
	
	for (acquired = 0; acquired < chunks_to_acquire; ++acquired) {
		if (is_not_empty()) {
			// The shared_chunk_pool is not empty. Get a chunk_index.
			current = container_[--unread_];
			
			// If the process terminates here, this chunk is leaked and will not
			// be recovered. It is most important to be able to recover all
			// chunks in most cases. TODO: Fixed it later.
			
			// Make sure the CPU (and compiler) don't re-order instructions.
			_mm_mfence();
			
			if (private_chunk_pool.push_front(current)) {
				// Havning reached this point the chunk can be recovered.
				_mm_mfence(); // Remove if uneccessary.
			}
			else {
				// The private_chunk_pool is full. Put the current chunk_index
				// back.
				container_[unread_++] = current;
				lock.unlock();
				return acquired;
			}
		}
		else {
			// The shared_chunk_pool is empty.
			break;
		}
	}
	
	// Successfully acquired chunks_to_acquire number of chunks.
	lock.unlock();
	return acquired;
}

//------------------------------------------------------------------------------
template<class T, class Alloc>
template<typename U>
std::size_t shared_chunk_pool<T, Alloc>::release_from_chunk_pool(U&
private_chunk_pool, std::size_t chunks_to_release,
smp::spinlock::milliseconds timeout) {
	// No recovery is done here in IPC version 1.0 so not locking with owner_id.
	smp::spinlock::scoped_lock lock(spinlock(), timeout -timeout.tick_count());
	
	if (!lock.owns()) {
		// The timeout_milliseconds time period has elapsed. Failed to acquire
		// the lock, therefore no chunks could be released.
		return 0;
	}
	
	chunk_index current;
	std::size_t released;
	
	for (released = 0; released < chunks_to_release; ++released) {
		if (private_chunk_pool.pop_back(&current)) {
			// Make sure the CPU (and compiler) don't re-order instructions.
			_mm_mfence();
			
			// Push the current chunk_index.
			container_.push_front(current);
			_mm_mfence();
			++unread_;
			_mm_mfence(); // Remove if uneccessary.
		}
		else {
			// The private_chunk_pool is empty.
			break;
		}
	}
	
	lock.unlock();
	return released;
}

#if 0
//------------------------------------------------------------------------------
// For schedulers (and monitor), doing clean up:

//------------------------------------------------------------------------------
template<class T, class Alloc>
bool shared_chunk_pool<T, Alloc>::release_clients_chunks(client_interface_type*
client_interface_ptr, smp::spinlock::milliseconds timeout) {
	// No recovery is done here in IPC version 1.0 so not locking with owner_id.
	smp::spinlock::scoped_lock lock(spinlock(), timeout -timeout.tick_count());
	
	if (!lock.owns()) {
		// The timeout_milliseconds time period has elapsed. Failed to acquire
		// the lock, therefore no chunks could be released.
		return false;
	}
	
	chunk_index current;
	resource_map::mask_type mask;
	std::size_t released = 0;
	
	for (std::size_t i = 0; i < resource_map::chunks_mask_size; ++i) {
		for (mask = client_interface_ptr->get_resource_map()
		.get_owned_chunks_mask(i); mask; mask &= mask -1) {
			current = bit_scan_forward(mask);
			current += i << 5;
			
			// Push the current chunk_index.
			container_.push_front(current);
			_mm_mfence();
			++unread_;
			++released;
		}
	}
	
	lock.unlock();
	
	// Clear the resource_map. The channels was already cleared, by the order of
	// releasing resources.
	client_interface_ptr->get_resource_map().clear();
	
	// Successfully released all chunks.
	return true;
}
#endif

//------------------------------------------------------------------------------
template<class T, class Alloc>
void shared_chunk_pool<T, Alloc>::show_linked_chunks(chunk_type* chunk_,
chunk_index head) {
	smp::spinlock::milliseconds timeout(1000);

	// No recovery is done here in IPC version 1.0 so not locking with owner_id.
	smp::spinlock::scoped_lock lock(spinlock(), timeout -timeout.tick_count());

	if (!lock.owns()) {
		// The timeout_milliseconds time period has elapsed. Failed to acquire
		// linked chunks.
		return;
	}
	
	for (chunk_index current = head; current != chunk_type::link_terminator;
	current = chunk_[current].get_link()) {
		chunk_index next = chunk_[current].get_link();
		std::cout << "chunk[" << current << "] links to chunk[" << next << "]" << std::endl;
	}
	std::cout << "end" << std::endl;
}

//------------------------------------------------------------------------------
template<class T, class Alloc>
inline bool shared_chunk_pool<T, Alloc>::push_front(param_type item, uint32_t
spin_count, smp::spinlock::milliseconds timeout) {
	// Spin at most spin_count number of times, and try to push the item...
	for (std::size_t s = 0; s < spin_count; ++s) {
		if (try_push_front(item)) {
			// The item was successfully pushed.
			return true;
		}
	}
	
	// Failed to aquire the lock while spinning. If failing to aquire the lock
	// when trying again now, the thread waits until the queue is not full.
	{
		// The timeout is used multiple times below, while time passes, so all
		// synchronization must be completed before the timeout time period has
		// elapsed.

		// No recovery is done here in IPC version 1.0 so not locking with owner_id.
		smp::spinlock::scoped_lock lock(spinlock(), timeout -timeout.tick_count());
		
		if (!lock.owns()) {
			// The timeout time period has elapsed. Failed to push the item.
			return false;
		}
		
		// The queue is not full because it has enough capacity to hold all
		// items, so the item can be pushed.
		container_.push_front(item);
		++unread_;
		lock.unlock();

		// Successfully pushed the item.
		return true;
	}
	return false;
}

//------------------------------------------------------------------------------
// No pop_back()

//------------------------------------------------------------------------------
template<class T, class Alloc>
inline bool shared_chunk_pool<T, Alloc>::try_push_front(param_type item) {
	smp::spinlock::scoped_lock lock(spinlock(),
	smp::spinlock::scoped_lock::try_to_lock_type());
	
	if (lock.owns()) {
		// If the buffer is full, we shall not block.
		if (unread_ == container_.capacity()) {
			// The buffer is full so we can't push the item.
			lock.unlock();
			return false;
		}
		
		container_.push_front(item);
		++unread_;
		lock.unlock();
		
		// The item was pushed.
		return true;
	}
	// The lock was not aquired so the item was not pushed.
	return false;
}

//------------------------------------------------------------------------------
template<class T, class Alloc>
inline bool shared_chunk_pool<T, Alloc>::is_not_empty() const {
	return unread_ > 0;
}

template<class T, class Alloc>
inline bool shared_chunk_pool<T, Alloc>::is_not_full() const {
	return unread_ < container_.capacity();
}

//------------------------------------------------------------------------------
template<class T, class Alloc>
inline const wchar_t* shared_chunk_pool<T, Alloc>::not_empty_notify_name() const {
	return not_empty_notify_name_;
}

template<class T, class Alloc>
inline const wchar_t* shared_chunk_pool<T, Alloc>::not_full_notify_name() const {
	return not_full_notify_name_;
}

template<class T, class Alloc>
inline bool shared_chunk_pool<T, Alloc>::if_locked_with_id_recover_and_unlock
(smp::spinlock::locker_id_type id) {
	if (spinlock().is_locked_with_id(id)) {
		// Recover...NOT! We accept a resource leak in IPC version 1.0.
		// TODO: Fix recovery of chunks in IPC version 2.0.
		spinlock().unlock();
		return true;
	}

	// The shared_chunk_pool was not locked with id. No recovery to be done here.
	return false;
}

} // namespace core
} // namespace starcounter

#endif // STARCOUNTER_CORE_IMPL_SHARED_CHUNK_POOL_HPP
