//
// impl/client_number_pool.hpp
//
// Copyright © 2006-2012 Starcounter AB. All rights reserved.
// Starcounter® is a registered trademark of Starcounter AB.
//
// Implementation of class client_number_pool.
//

#ifndef STARCOUNTER_CORE_IMPL_CLIENT_NUMBER_POOL_HPP
#define STARCOUNTER_CORE_IMPL_CLIENT_NUMBER_POOL_HPP

// Implementation

namespace starcounter {
namespace core {

template<class T, std::size_t N>
inline client_number_pool<T, N>::client_number_pool
(const char* segment_name)
: size_(0) {
	clear_buffer();
	clear_mask();

    if (segment_name != 0) {
		char notify_name[segment_and_notify_name_size];
		std::size_t length;

		// Create the not_empty_notify_name_ and the not_empty_ event.
		
		// Format: "Local\<segment_name>_client_number_pool_not_empty".
		if ((length = _snprintf_s(notify_name, _countof(notify_name),
		segment_and_notify_name_size -1 /* null */, "Local\\%s_"
		"client_number_pool_not_empty", segment_name)) < 0) {
			return; // Throw exception error_code.
		}
		notify_name[length] = '\0';

		/// TODO: Fix insecure
		if ((length = mbstowcs(not_empty_notify_name_, notify_name,
		segment_name_size)) < 0) {
			// Failed to convert notify_name to multi-byte string.
			return; // Throw exception error_code.
		}
		not_empty_notify_name_[length] = L'\0';
		
		if ((not_empty_ = ::CreateEvent(NULL, TRUE, FALSE,
		not_empty_notify_name_)) == NULL) {
			// Failed to create event.
			return; // Throw exception error_code.
		}

		// Create the not_full_notify_name_ and the not_full_ event.
		
		// Format: "Local\<segment_name>_client_number_pool_not_full".
		if ((length = _snprintf_s(notify_name, _countof(notify_name),
		segment_and_notify_name_size -1 /* null */, "Local\\%s_"
		"client_number_pool_not_full", segment_name)) < 0) {
			return; // Throw exception error_code.
		}
		notify_name[length] = '\0';

		/// TODO: Fix insecure
		if ((length = mbstowcs(not_full_notify_name_, notify_name,
		segment_name_size)) < 0) {
			// Failed to convert notify_name to multi-byte string.
			return; // Throw exception error_code.
		}
		not_full_notify_name_[length] = L'\0';
		
		if ((not_full_ = ::CreateEvent(NULL, TRUE, FALSE,
		not_full_notify_name_)) == NULL) {
			// Failed to create event.
			return; // Throw exception error_code.
		}
	}
	else {
		// Error: No segment name. Throw exception error_code.
	}
}

template<class T, std::size_t N>
inline typename client_number_pool<T, N>::size_type
client_number_pool<T, N>::size() const {
	return size_;
}

template<class T, std::size_t N>
inline typename client_number_pool<T, N>::size_type
client_number_pool<T, N>::capacity() const {
	return buffer_capacity;
}

template<class T, std::size_t N>
inline bool client_number_pool<T, N>::empty() const {
	return size_ == 0;
}

template<class T, std::size_t N>
inline bool client_number_pool<T, N>::full() const {
	return size_ == capacity();
}

template<class T, std::size_t N>
inline bool client_number_pool<T, N>::insert(value_type item,
client_interface_type* client_interface_base, owner_id id,
smp::spinlock::milliseconds timeout) {
	smp::spinlock::scoped_lock lock(spinlock(), id.get(),
	timeout -timeout.tick_count());

	if (lock.owns()) {
		if (item < capacity()) {
			if (elem_[item] == 0) {
				elem_[item] = 1;

				std::size_t i = item >> 6;
				std::size_t bit = item & 63;
				mask_[i] |= 1ULL << bit;

				// Successfully inserted.
				return true;
			}
		}
	}

	// Not inserted.
	return false;
}

template<class T, std::size_t N>
inline bool client_number_pool<T, N>::erase(value_type item,
client_interface_type* client_interface_base, owner_id id,
smp::spinlock::milliseconds timeout) {
	smp::spinlock::scoped_lock lock(spinlock(), id.get(),
	timeout -timeout.tick_count());

	if (lock.owns()) {
		if (item < capacity()) {
			if (elem_[item] == 1) {
				elem_[item] = 0;
				std::size_t i = item >> 6;
				std::size_t bit = item & 63;
				mask_[i] &= ~(1 << bit);

				// Successfully erased.
				return true;
			}
		}
	}
	
	// Not erased.
	return false;
}

template<class T, std::size_t N>
inline bool client_number_pool<T, N>::acquire(value_type* item,
client_interface_type* client_interface_base, owner_id id,
smp::spinlock::milliseconds timeout) {
	smp::spinlock::scoped_lock lock(spinlock(), id.get(),
	timeout -timeout.tick_count());
	
	if (lock.owns()) {
		for (std::size_t i = 0; i < masks; ++i) {
			for (mask_type mask = mask_[i]; mask; mask &= mask -1) {
				std::size_t bit = bit_scan_forward(mask);
				std::size_t n = (i << 6) +bit;

				if (elem_[n] == 1) {
					// 0 = not an entry (place holder.)
					// 1 = a free entry.
					// > 1 = an allocated entry.
					elem_[n] = id.get();
					mask_[i] = mask & ~(1 << bit);
                    ++size_;
					client_interface_base[n].set_owner_id(id);
					*item = n;

					// Successfully acquired.
					return true;
				}
				else {
					// The mask is not up to date. Should be impossible.
				}
			}
		}
	}

	// Not acquired.
	return false;
}

template<class T, std::size_t N>
inline bool client_number_pool<T, N>::acquire(value_type item,
client_interface_type* client_interface_base, owner_id id,
smp::spinlock::milliseconds timeout) {
	smp::spinlock::scoped_lock lock(spinlock(), id.get(),
	timeout -timeout.tick_count());
	
	if (lock.owns()) {
		auto n = item;
		if (elem_[n] == 1) {
			// 0 = not an entry (place holder.)
			// 1 = a free entry.
			// > 1 = an allocated entry.
			elem_[n] = id.get();
			mask_[n / 64] = mask_[n / 64] & ~(1 << (n % 64));
            ++size_;
			client_interface_base[n].set_owner_id(id);

			// Successfully acquired.
			return true;
		}
		else {
			// The mask is not up to date. Should be impossible.
		}
	}

	// Not acquired.
	return false;
}

template<class T, std::size_t N>
inline bool client_number_pool<T, N>::release(value_type item,
client_interface_type* client_interface_base, owner_id id,
smp::spinlock::milliseconds timeout) {
	smp::spinlock::scoped_lock lock(spinlock(), id.get(),
	timeout -timeout.tick_count());
	
	if (lock.owns()) {
		if (item < capacity()) {
			elem_[item] = 1;
			client_interface_base[item].set_owner_id(owner_id::none);
			std::size_t i = item >> 6;
			std::size_t bit = item & 63;
			mask_[i] |= 1ULL << bit;
            --size_;

			// Successfully released.
			return true;
		}
	}

	// Not released.
	return false;
}

template<class T, std::size_t N>
inline void client_number_pool<T, N>::adjust_mask() {
	clear_mask();
	
	for (size_type i = 0; i < buffer_capacity; ++i) {
		if (elem_[i] == 1) {
			mask_[i >> 6] |= 1ULL << (i & 63);
		}
	}
}

template<class T, std::size_t N>
inline void client_number_pool<T, N>::adjust_size() {
	size_type sum = 0;
	
	// Sum up the population count of the masks.
	for (std::size_t i = 0; i < masks; ++i) {
		sum += population_count(mask_[i]);
	}

	// Assign the sum to the size_.
	size_ = sum;
}

template<class T, std::size_t N>
inline bool client_number_pool<T, N>::if_locked_with_id_recover_and_unlock
(smp::spinlock::locker_id_type id) {
	if (spinlock().is_locked_with_id(id)) {
		// Release elements marked with id.
		for (size_type i = 0; i < buffer_capacity; ++i) {
			if (elem_[i] != id) {
				continue;
			}
			else {
				// The element is marked with id. Mark it as free.
				elem_[i] = 1;
			}
		}

		adjust_mask();
		adjust_size();
		spinlock().unlock();
		return true;
	}

	// The client_number_pool was not locked with id. No recovery to be done here.
	return false;
}

template<class T, std::size_t N>
inline bool client_number_pool<T, N>::is_not_empty() const {
	return size() > 0;
}

template<class T, std::size_t N>
inline bool client_number_pool<T, N>::is_not_full() const {
	return size() < capacity();
}

} // namespace core
} // namespace starcounter

#endif // STARCOUNTER_CORE_IMPL_CLIENT_NUMBER_POOL_HPP
