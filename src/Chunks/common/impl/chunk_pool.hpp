//
// impl/chunk_pool.hpp
//
// Copyright © 2006-2012 Starcounter AB. All rights reserved.
// Starcounter® is a registered trademark of Starcounter AB.
//
// Thread-Safety
// The thread-safety of the chunk_pool is the same as the thread-safety of
// containers in most STL implementations. This means the chunk_pool is not
// thread-safe.
//

#ifndef STARCOUNTER_CORE_IMPL_CHUNK_POOL_HPP
#define STARCOUNTER_CORE_IMPL_CHUNK_POOL_HPP

// Implementation

namespace starcounter {
namespace core {

template<class T, class Alloc>
inline chunk_pool<T, Alloc>::chunk_pool(size_type buffer_capacity,
const allocator_type& alloc)
: circular_buffer(buffer_capacity, alloc) {}

//------------------------------------------------------------------------------
// Client
template<class T, class Alloc>
inline bool chunk_pool<T, Alloc>::acquire_linked_chunks(chunk_type* chunk,
chunk_index& head, std::size_t size) {
	std::size_t chunks_to_acquire = (size +chunk_type::static_data_size -1)
	/ chunk_type::static_data_size;
	
	// Check if enough space is available, assuming it is.
	if (chunks_to_acquire <= this->size()) {
		// Enough space is available, start linking chunks together.
		chunk_index prev;
		chunk_index current;
		pop_back(&current);
		head = current;
		
		for (std::size_t i = 1; i < chunks_to_acquire; ++i) {
			prev = current;
			
			// Get the next chunk.
			pop_back(&current);
			chunk[prev].set_link(current);
		}
		
		// Terminate the last chunk.
		chunk[current].terminate_link();
		
		// Successfully acquired the chunks and linked them.
		return true;
	}
	else {
		// Not enough space available. Failed to acquire linked chunks.
		return false;
	}
}

template<class T, class Alloc>
inline bool chunk_pool<T, Alloc>::acquire_linked_chunks_counted(chunk_type* chunk,
chunk_index& head, std::size_t num_chunks_to_acquire) {
	// Check if enough space is available, assuming it is.
	if (num_chunks_to_acquire <= this->size()) {
		// Enough space is available, start linking chunks together.
		chunk_index prev;
		chunk_index current;
		pop_back(&current);
		head = current;
		
		for (std::size_t i = 1; i < num_chunks_to_acquire; ++i) {
			prev = current;
			
			// Get the next chunk.
			pop_back(&current);
			chunk[prev].set_link(current);
		}
		
		// Terminate the last chunk.
		chunk[current].terminate_link();
		
		// Successfully acquired the chunks and linked them.
		return true;
	}
	else {
		// Not enough space available. Failed to acquire linked chunks.
		return false;
	}
}

template<class T, class Alloc>
inline bool chunk_pool<T, Alloc>::release_linked_chunks(chunk_type* chunk_base,
chunk_index& head) {
	chunk_index next;
	
	while (head != chunk_type::link_terminator) {
		next = chunk_base[head].get_link();
		
		// Try to push the head.
		if (push_front(head) == true) {
			head = next;
		}
		else {
			// Failed to release all linked chunks because the chunk_pool is
			// full. The head points to the head of the remaining linked chunks.
			return false;
		}
	}
	
	// Successfully released all linked chunks.
	return true;
}

#if 0
//------------------------------------------------------------------------------
// For schedulers - to be tested.
template<class T, class Alloc>
inline bool chunk_pool<T, Alloc>::acquire_linked_chunks(chunk_type* chunk_base,
chunk_index& head, std::size_t size, client_interface_type*
client_interface_ptr) {
	std::size_t chunks_to_acquire = (size +chunk_type::static_data_size -1)
	/ chunk_type::static_data_size;
	
	// Check if enough space is available, assuming it is.
	if (chunks_to_acquire <= this->size()) {
		// Enough space is available, start linking chunks together.
		chunk_index prev;
		chunk_index current;
		pop_back(&current);
		_mm_mfence(); // TODO: Figure if _mm_sfence() is enough.
		client_interface_ptr->set_chunk_flag(current);
		head = current;
		
		for (std::size_t i = 1; i < chunks_to_acquire; ++i) {
			prev = current;
			pop_back(&current);
			
			_mm_mfence(); // TODO: Figure if _mm_mfence() is enough/required.
			
			// This must never occur before the pop_back() above, because if
			// it occurs before pop_back() and the client process terminates
			// unexpectedly (crashes), then the clean up will be messed up
			// because of duplicates. Only way to avoid that would be if the
			// shared_chunk_pool used a bit map in conjunction with the queue,
			// to mark free chunks, because then duplicates could not appear
			// obviously.
			client_interface_ptr->set_chunk_flag(current);
			chunk_base[prev].set_link(current);
		}
		
		// Terminate the last chunk.
		chunk_base[current].terminate_link();
		
		// Successfully acquired the chunks and linked them.
		return true;
	}
	else {
		// Not enough space available. Failed to acquire linked chunks.
		return false;
	}
}

template<class T, class Alloc>
inline bool chunk_pool<T, Alloc>::acquire_linked_chunks_counted(chunk_type* chunk_base,
chunk_index& head, std::size_t num_chunks_to_acquire, client_interface_type*
client_interface_ptr) {
	// Check if enough space is available, assuming it is.
	if (num_chunks_to_acquire <= this->size()) {
		// Enough space is available, start linking chunks together.
		chunk_index prev;
		chunk_index current;
		pop_back(&current);
		_mm_mfence(); // TODO: Figure if _mm_sfence() is enough.
		client_interface_ptr->set_chunk_flag(current);
		head = current;
		
		for (std::size_t i = 1; i < num_chunks_to_acquire; ++i) {
			prev = current;
			pop_back(&current);
			
			_mm_mfence(); // TODO: Figure if _mm_mfence() is enough/required.

			// This must never occur before the pop_back() above, because if
			// it occurs before pop_back() and the client process terminates
			// unexpectedly (crashes), then the clean up will be messed up
			// because of duplicates. Only way to avoid that would be if the
			// shared_chunk_pool used a bit map in conjunction with the queue,
			// to mark free chunks, because then duplicates could not appear
			// obviously.
			client_interface_ptr->set_chunk_flag(current);
			chunk_base[prev].set_link(current);
		}
		
		// Terminate the last chunk.
		chunk_base[current].terminate_link();
		
		// Successfully acquired the chunks and linked them.
		return true;
	}
	else {
		// Not enough space available. Failed to acquire linked chunks.
		return false;
	}
}

template<class T, class Alloc>
inline bool chunk_pool<T, Alloc>::release_linked_chunks(chunk_type* chunk_base,
chunk_index& head, client_interface_type* client_interface_ptr) {
	chunk_index next;
	
	while (head != chunk_type::LINK_TERMINATOR) {
		next = chunk_base[head].get_link();
		
		// Try to push the head.
		if (push_front(head) == true) {
			_mm_mfence(); // TODO: Figure if _mm_mfence() is enough/required.
			client_interface_ptr->clear_chunk_flag(head);
			head = next;
		}
		else {
			// Failed to release all linked chunks because the chunk_pool is
			// full. The head points to the head of the remaining linked chunks.
			return false;
		}
	}
	
	// Successfully released all linked chunks.
	return true;
}
#endif

} // namespace core
} // namespace starcounter

#endif // STARCOUNTER_CORE_IMPL_CHUNK_POOL_HPP






