//
// channel_mask.hpp
//
// Copyright � 2006-2011 Starcounter AB. All rights reserved.
// Starcounter� is a registered trademark of Starcounter AB.
//

#ifndef STARCOUNTER_CORE_CHANNEL_MASK_HPP
#define STARCOUNTER_CORE_CHANNEL_MASK_HPP

#if defined(_MSC_VER) && (_MSC_VER >= 1200)
# pragma once
#endif // defined(_MSC_VER) && (_MSC_VER >= 1200)

#include <iostream>
#include <cstddef>
#include <boost/cstdint.hpp>
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
//#include <intrin.h>
#undef WIN32_LEAN_AND_MEAN
//#include "../common/channel.hpp"
#include "../common/channel_number.hpp"

namespace starcounter {
namespace core {

/// class channel_mask.
/**
 * @param Channels The number of channels.
 *      Use channels defined in config_param.hpp.
 */
template<std::size_t Channels>
class channel_mask {
public:
	#if defined(__x86_64__) || defined (__amd64__) || defined (_M_X64) \
	|| defined(_M_AMD64)
	typedef uint64_t mask_type;
	enum { mask_bits = 64 };
	#elif defined(__i386__) || defined (_M_IX86)
	typedef uint32_t mask_type;
	enum { mask_bits = 32 };
	#endif // defined(__x86_64__) || defined (__amd64__) || defined (_M_X64)
	// || defined(_M_AMD64)
	
	// Construction/Destruction.
	channel_mask() {
		for (std::size_t i = 0; i < channel_masks(); ++i) {
			channel_mask_[i] = 0;
		}
	}
	
	mask_type get_channel_mask_word(std::size_t n) const {
		// No fence is needed when reading it.
		return channel_mask_[n];
	}
	
	void set_channel_number_flag(channel_number ch) {
		// Set channel number flag in channel_mask_.
		#if defined(__x86_64__) || defined (__amd64__) || defined (_M_X64) \
		|| defined(_M_AMD64)
		std::size_t channel_mask_index = ch >> 6;
		mask_type mask = 1ULL << (ch & 0x3FULL);
		_InterlockedOr64((volatile __int64*)
		&channel_mask_[channel_mask_index], mask);
		
		#elif defined(__i386__) || defined (_M_IX86)
		std::size_t channel_mask_index = ch >> 5;
		mask_type mask = 1UL << (ch & 0x1FUL);
		_InterlockedOr((volatile long int*)
		&channel_mask_[channel_mask_index], mask);
		#endif // defined(__x86_64__) || defined (__amd64__) || defined (_M_X64)
		// || defined(_M_AMD64)
	}

	void clear_channel_number_flag(channel_number ch) {
		// Clear channel number flag in channel_mask_.
		#if defined(__x86_64__) || defined (__amd64__) || defined (_M_X64) \
		|| defined(_M_AMD64)
		std::size_t channel_mask_index = ch >> 6;
		uint64_t mask = ~(1ULL << (ch & 0x3FULL));
		_InterlockedAnd64((volatile __int64*)
		&channel_mask_[channel_mask_index], mask);
		
		#elif defined(__i386__) || defined (_M_IX86)
		std::size_t channel_mask_index = ch >> 5;
		mask_type mask = ~(1UL << (ch & 0x1FUL));
		_InterlockedAnd((volatile long int*)
		&channel_mask_[channel_mask_index], mask);
		#endif // defined(__x86_64__) || defined (__amd64__) || defined (_M_X64)
		// || defined(_M_AMD64)
	}

	std::size_t channel_masks() const {
		return sizeof channel_mask_ / sizeof(mask_type);
	}

private:
	volatile mask_type channel_mask_[(Channels +mask_bits -1) / mask_bits];
	// It's a bit messy to compute "32" here so to save time I hard code it. If
	// the number of channels are changed it will not yield otimal performance.
	char pad0[CACHE_LINE_SIZE -32]; // 32 * 8 = 256, one flag per channel.
};

} // namespace core
} // namespace starcounter

//#include "impl/channel_mask.hpp"

#endif // STARCOUNTER_CORE_CHANNEL_MASK_HPP