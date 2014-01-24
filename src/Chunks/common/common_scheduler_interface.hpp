//
// common_scheduler_interface.hpp
//
// Copyright © 2006-2013 Starcounter AB. All rights reserved.
// Starcounter® is a registered trademark of Starcounter AB.
//

#ifndef STARCOUNTER_CORE_COMMON_SCHEDULER_INTERFACE_HPP
#define STARCOUNTER_CORE_COMMON_SCHEDULER_INTERFACE_HPP

#if defined(_MSC_VER) && (_MSC_VER >= 1200)
# pragma once
#endif // defined(_MSC_VER) && (_MSC_VER >= 1200)

#include <cstddef>
#include <cstdint>
#include <memory>
#include <boost/interprocess/managed_shared_memory.hpp>
#include <boost/interprocess/allocators/allocator.hpp>
#include <boost/bind.hpp>

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <intrin.h>
#undef WIN32_LEAN_AND_MEAN
#include "../common/scheduler_mask.hpp"
#include "../common/bit_operations.hpp"
#include "../common/config_param.hpp"
#include <scerrres.h>

namespace starcounter {
namespace core {

/// class common_scheduler_interface.
/**
 * @param T Is not used.
 * @param Alloc The allocator type used for all internal memory management.
 * @par Type Requirements Alloc
 *		The Alloc has to meet the allocator requirements imposed by STL.
 * @par Default Alloc
 *		std::allocator<T>
 */
template<class T, class Alloc = std::allocator<T> >
class common_scheduler_interface {
public:
	// Basic types
	
	typedef typename uint64_t value_type; // uint64_t is the mask type.
	typedef scheduler_mask<max_number_of_schedulers> scheduler_mask_type;
	
	// The type of an allocator used.
	typedef Alloc allocator_type;

#if 0	
	enum state {
		normal,
		at_least_one_client_is_down /// TODO: Think about it.
	};
#endif
	
	// Construction/Destruction.
	
	/**
	 * @param alloc The allocator.
	 * @throws "An allocation error" if memory is exhausted (std::bad_alloc if
	 *		the standard allocator is used).
	 * @par Complexity
	 *		Constant.
	 */
	explicit common_scheduler_interface(const char* server_name,
    uint32_t scheduler_count, const allocator_type& alloc = allocator_type())
	: scheduler_count_(scheduler_count), active_schedulers_mask_() {
		if (server_name != 0) {
			// Number of characters in the string after being converted.
			std::size_t length;
			
			//======================================================================
			// Construct the monitor_interface_name.
			
			// Concatenate the server_name with MONITOR_INTERFACE_SUFFIX.
			if ((length = _snprintf_s(monitor_interface_name_,
			_countof(monitor_interface_name_), sizeof(monitor_interface_name_) -1
			/* null */, "%s_%s", server_name, MONITOR_INTERFACE_SUFFIX)) < 0) {
				// Buffer overflow.
				return; // SCERRCONSTRMONITORINTERFACENAME;
			}
			
			// Null termination, just in case.
			monitor_interface_name_[length] = '\0';
			
			//======================================================================
			// Construct the ipc_monitor_cleanup_event_name.
			
			char ipc_monitor_cleanup_event_name[ipc_monitor_cleanup_event_name_size];

			// Format: "Local\<server_name>_ipc_monitor_cleanup_event".
			if ((length = _snprintf_s(ipc_monitor_cleanup_event_name, _countof
			(ipc_monitor_cleanup_event_name), ipc_monitor_cleanup_event_name_size
			-1 /* null */, "Local\\%s_ipc_monitor_cleanup_event", server_name))
			< 0) {
				//throw bad_monitor("failed to format the ipc_monitor_cleanup_event_name");
				return;
			}
			ipc_monitor_cleanup_event_name[length] = '\0';

			/// TODO: Fix insecure
			if ((length = mbstowcs(w_ipc_monitor_cleanup_event_name_,
			ipc_monitor_cleanup_event_name, segment_name_size)) < 0) {
				// Failed to convert ipc_monitor_cleanup_event_name to multi-byte string.
				//throw bad_monitor("failed to create the ipc_monitor_cleanup_event");
				return; // Throw exception error_code.
			}
			w_ipc_monitor_cleanup_event_name_[length] = L'\0';
		}
		else {
			// Error: No server name. Throw exception error_code.
		}
	}

#if 0	
	/// TODO: Think about multiple clients.
	void clients_state(state s) {
		_mm_mfence();
		state_ = s;
		_mm_mfence();
	}
	
	/// TODO: Think about multiple clients.
	state clients_state() const {
		return state_;
	}
#endif

	bool is_scheduler_active(std::size_t index) {
		return active_schedulers_mask_.is_scheduler_active(index);
	}
	
	void set_scheduler_number_flag(std::size_t ch) {
		active_schedulers_mask_.set_scheduler_number_flag(ch);
	}

#if 0	
	void clear_scheduler_number_flag(std::size_t ch) {
		active_schedulers_mask_.clear_scheduler_number_flag(ch);
	}
#endif

#if 0	
	uint64_t get_scheduler_mask(std::size_t ch) const {
		return active_schedulers_mask_.get_scheduler_mask(ch);
	}
#endif
	
	std::size_t number_of_active_schedulers() const {
		std::size_t count = 0;
		
		for (std::size_t i = 0; i < scheduler_mask_type::masks; ++i) {
			count += population_count(active_schedulers_mask_.get_mask(i));
		}
		
		return count;
	}

    uint32_t scheduler_count() {
        return scheduler_count_;
    }
	
	const char* server_name() const {
		return server_name_;
	}

	const char* monitor_interface_name() const {
		return monitor_interface_name_;
	}

	const HANDLE& ipc_monitor_clean_up_event() const {
		return ipc_monitor_clean_up_event_;
	}

	const wchar_t* ipc_monitor_cleanup_event_name() const {
		return w_ipc_monitor_cleanup_event_name_;
	}

private:
	scheduler_mask_type active_schedulers_mask_;
    uint32_t scheduler_count_;
	char cache_line_pad_0_[CACHE_LINE_SIZE -((
	+sizeof(scheduler_mask_type) // active_schedulers_mask_
    +sizeof(uint32_t) // scheduler_count_
    ) % CACHE_LINE_SIZE)];
	
//	volatile state state_;

	char server_name_[server_name_size];
	char monitor_interface_name_[server_name_size +sizeof
	(MONITOR_INTERFACE_SUFFIX) +2 /* one delimiter and null */];

	// In order to reduce the time taken to open the work_ event the name is
	// cached. Otherwise the name have to be formated before opening it.
	wchar_t work_notify_name_[segment_and_notify_name_size];

	// In order to reduce the time taken to open the ipc_monitor_cleanup_event_
	// the name is cached. Otherwise the name have to be formated before opening it.
	wchar_t w_ipc_monitor_cleanup_event_name_[ipc_monitor_cleanup_event_name_size];

	char cache_line_pad_1_[CACHE_LINE_SIZE -((
//	+sizeof(state) // state_
	+server_name_size * sizeof(char) // server_name_
	+ipc_monitor_cleanup_event_name_size * sizeof(wchar_t) // ipc_monitor_cleanup_event_name_
	) % CACHE_LINE_SIZE)];
};

typedef starcounter::core::simple_shared_memory_allocator<std::size_t>
shm_alloc_for_the_common_scheduler_interface2;

typedef common_scheduler_interface
<std::size_t, shm_alloc_for_the_common_scheduler_interface2>
common_scheduler_interface_type;

} // namespace core
} // namespace starcounter

#endif // STARCOUNTER_CORE_COMMON_SCHEDULER_INTERFACE_HPP
