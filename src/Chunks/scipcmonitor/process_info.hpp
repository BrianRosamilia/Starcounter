//
// process_info.hpp
//
// Copyright © 2006-2012 Starcounter AB. All rights reserved.
// Starcounter® is a registered trademark of Starcounter AB.
//

/// TODO: This hack "works" but fix it.

#ifndef STARCOUNTER_CORE_PROCESS_INFO_HPP
#define STARCOUNTER_CORE_PROCESS_INFO_HPP

#if defined(_MSC_VER) && (_MSC_VER >= 1200)
# pragma once
#endif // defined(_MSC_VER) && (_MSC_VER >= 1200)

#include <iostream>
#include <string>
#include <cstddef>
#include <boost/thread/win32/thread_primitives.hpp>
#include "../common/monitor_interface.hpp"
#include "../common/pid_type.hpp"

namespace starcounter {
namespace core {

/// Class process_info.
/// TODO: Fix this hack. No time to make it as it should be (not a struct.)
struct process_info {
	typedef boost::detail::win32::handle handle_type;
	typedef monitor_interface::process_type process_type_type;
	typedef pid_type pid_type_type;
	// The segment_name_type string format is:
	// starcounter_<segment_name>_<sequence_number>
	typedef std::string segment_name_type;
	
	process_info()
	: handle_(), process_type_(process_type_type()), pid_(pid_type()),
	segment_name_() {}
	
	process_info(const handle_type& h, const process_type_type& pt, const
	pid_type& pid, const segment_name_type& name)
	: handle_(h), process_type_(pt), pid_(pid), segment_name_(name) {}
	
	process_info(const process_info& p)
	: handle_(p.handle_), process_type_(p.process_type_), pid_(p.pid_),
	segment_name_(p.segment_name_) {}
	
	
	handle_type get_handle() const {
		return handle_;
	}
	
	process_type_type get_process_type() const {
		return process_type_;
	}
	
	pid_type_type get_pid() const {
		return pid_;
	}
	
	segment_name_type get_segment_name() const {
		return segment_name_;
	}
	
	handle_type handle_;
	process_type_type process_type_;
	pid_type_type pid_;
	// segment_name_ will just contain null when the process_type_ is client.
	segment_name_type segment_name_;
};

// input
template<class CharT, class Traits>
inline std::basic_istream<CharT, Traits>& 
operator>>(std::basic_istream<CharT, Traits>& is, process_info& u) {
	process_info::handle_type h;
	process_info::process_type_type pt;
	process_info::pid_type_type pid;
	process_info::segment_name_type segment_name;
	is >> h >> pt >> pid >> segment_name;
	u = process_info(h, pt, pid, segment_name);
	return is;
}

// output
template<class CharT, class Traits>
inline std::basic_ostream<CharT, Traits>&
operator<<(std::basic_ostream<CharT, Traits>& os, const process_info& u) {
	os << u.handle_ << "\t" << u.process_type_ << "\t";
#if 0
	if (u.process_type_ == monitor_interface::client_process) {
		os << "\t";
	}
#endif
	os << u.pid_ << "\t" << u.segment_name_;
	return os;
}

} // namespace core
} // namespace starcounter

//#include "impl/process_info.hpp"

#endif // STARCOUNTER_CORE_PROCESS_INFO_HPP
