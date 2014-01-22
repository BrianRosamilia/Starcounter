//
// initialize.cpp
// server
//
// Copyright © 2006-2013 Starcounter AB. All rights reserved.
// Starcounter® is a registered trademark of Starcounter AB.
//

#include "../common/bounded_buffer.hpp"
#include "../common/config_param.hpp"
#include "../common/circular_buffer.hpp"
#include "../common/chunk.hpp"
#include "../common/shared_chunk_pool.hpp"
#include "../common/channel_interface.hpp"
#include "../common/channel.hpp"
#include "../common/scheduler_channel.hpp"
#include "../common/common_scheduler_interface.hpp"
#include "../common/scheduler_interface.hpp"
#include "../common/common_client_interface.hpp"
#include "../common/client_interface.hpp"
#include "../common/client_number.hpp"
#include "../common/config_param.hpp"
#include "../common/spinlock.hpp"
#include "../common/macro_definitions.hpp"
#include "../common/interprocess.hpp"
#include "../common/name_definitions.hpp"
#include "initialize.hpp"
#include <scerrres.h>

namespace {

const int _E_UNSPECIFIED = 999L;

} // namespace

namespace starcounter {
namespace core {

shared_memory_object global_segment_shared_memory_object;
mapped_region global_mapped_region;

//typedef DWORD affinity_mask; // experimenting

///=============================================================================
unsigned long initialize(const char* segment_name, const char* server_name,
std::size_t schedulers, bool is_system, uint32_t chunks_total_number,
uint8_t gateway_num_workers)
try {
	if (chunks_total_number > chunks_total_number_max) {
		chunks_total_number = chunks_total_number_max;
	}
	
#if defined (IPC_VERSION_2_0)
	size_t channels_size = schedulers * gateway_num_workers;
#else // !defined (IPC_VERSION_2_0)
	size_t channels_size = channels;
#endif // defined (IPC_VERSION_2_0)

	// Compute the memory required for all objects in shared memory.
	std::size_t shared_memory_segment_size =
	
	+sizeof(simple_shared_memory_manager)
	
	// chunk[s]
	+sizeof(chunk_type) * chunks_total_number
	
	// shared_chunk_pool
	+sizeof(shared_chunk_pool_type)
	+CACHE_LINE_SIZE // Not aligned to CACHE_LINE_SIZE byte boundary
	+sizeof(chunk_index) * chunks_total_number
	
	// common_scheduler_interface
	+sizeof(common_scheduler_interface_type)
	
	// scheduler_interface[s]
	+sizeof(scheduler_interface_type) * max_number_of_schedulers
	+CACHE_LINE_SIZE // Not aligned to CACHE_LINE_SIZE byte boundary
	
	// scheduler_interface[s] chunk_pool_
	+sizeof(chunk_index) * chunks_total_number * max_number_of_schedulers
	
	// scheduler_interface[s] overflow_pool_
	+sizeof(chunk_index) * chunks_total_number * max_number_of_schedulers
	
	// scheduler_interface[s] channel_number_
	+sizeof(channel_number) * channels * max_number_of_schedulers // TODO: Not allocated
	
	// client_interface[s]
	+sizeof(client_interface_type) * max_number_of_clients
	
	// common_client_interface
	+sizeof(common_client_interface_type)
	+sizeof(client_number) * max_number_of_clients
	
	// channel_interface
	+sizeof(channel_interface_type)

	// channel[s]
	+sizeof(channel_type) * channels_size
	
	// scheduler_task_channel[s]
	+sizeof(scheduler_channel_type) * max_number_of_schedulers
	+CACHE_LINE_SIZE // Not aligned to CACHE_LINE_SIZE byte boundary
	
	// scheduler_signal_channel[s]
	+sizeof(scheduler_channel_type) * max_number_of_schedulers
	+CACHE_LINE_SIZE; // Not aligned to CACHE_LINE_SIZE byte boundary
	
	//--------------------------------------------------------------------------
	// Create a new segment with given name and size.
	global_segment_shared_memory_object.init_create(segment_name, 
	shared_memory_segment_size, is_system);
	
	if (!global_segment_shared_memory_object.is_valid()) {
	    return SCERRINVALIDGLOBALSEGMENTSHMOBJ;
	}
	
	global_mapped_region.init(global_segment_shared_memory_object);
	
	if (!global_mapped_region.is_valid()) {
		return SCERRINVALIDGLOBALSEGMENTSHMOBJ;
	}
	
	simple_shared_memory_manager* psegment_manager
	= new (global_mapped_region.get_address()) simple_shared_memory_manager;
	psegment_manager->reset(shared_memory_segment_size);
	
	//--------------------------------------------------------------------------
	// Construct the chunk array in shared memory.
	void* p = psegment_manager->create_named_block
	(starcounter_core_shared_memory_chunks_name, sizeof(chunk_type)
    * chunks_total_number);
	
	chunk_type* chunk = static_cast<chunk_type*>(p);
	
	for (std::size_t i = 0; i < chunks_total_number; ++i) {
		new (chunk +i) chunk_type;
	}
	
	//--------------------------------------------------------------------------
	// Construct the shared_chunk_pool in shared memory.
	
	// Initialize shared memory STL-compatible allocator.
	const shm_alloc_for_the_shared_chunk_pool2 shared_chunk_pool_alloc_inst
	(global_mapped_region.get_address());
	
	p = psegment_manager->create_named_block
	(starcounter_core_shared_memory_shared_chunk_pool_name,
	sizeof(shared_chunk_pool_type));
	
	shared_chunk_pool_type* shared_chunk_pool = new (p) shared_chunk_pool_type
	(segment_name, chunks_total_number, shared_chunk_pool_alloc_inst);
	
	// Initialize the shared_chunk_pool by pushing in chunk_indexes.
	// These chunk_indexes represents free chunks.
	
	// Chunks from 0 to chunks_total_number -1 are put in the shared_chunk_pool.
	for (chunk_index i = 0; i < chunks_total_number; ++i) {
		shared_chunk_pool->push_front(i, 1000000 /* spin count */,
		10000 /* timeout ms */);
	}
	
	//--------------------------------------------------------------------------
	// Construct the common_scheduler_interface in shared memory.
	
	const shm_alloc_for_the_common_scheduler_interface2
	common_scheduler_interface_alloc_inst(global_mapped_region.get_address());
	
	p = psegment_manager->create_named_block
	(starcounter_core_shared_memory_common_scheduler_interface_name,
	sizeof(common_scheduler_interface_type));
	
	common_scheduler_interface_type* common_scheduler_interface = new (p)
	common_scheduler_interface_type(server_name, (uint32_t)schedulers,
	common_scheduler_interface_alloc_inst);
	
	//--------------------------------------------------------------------------
	// Construct the scheduler_interface array in shared memory.
	
	// Initialize shared memory STL-compatible allocator.
	const shm_alloc_for_the_scheduler_interfaces2
	scheduler_interface_alloc_inst(global_mapped_region.get_address()); /// remove?
	
	const shm_alloc_for_the_scheduler_interfaces2b
	scheduler_interface_alloc_inst2(global_mapped_region.get_address());
	
	// Allocate the scheduler_interface array.
	
	p = psegment_manager->create_named_block
	(starcounter_core_shared_memory_scheduler_interfaces_name,
	sizeof(scheduler_interface_type) * schedulers);
	
	scheduler_interface_type* scheduler_interface
	= (scheduler_interface_type*) p;
	
	for (std::size_t i = 0; i < schedulers; ++i) {
		new (scheduler_interface +i) scheduler_interface_type
        (channels_size, chunks_total_number, chunks_total_number,
		scheduler_interface_alloc_inst2, scheduler_interface_alloc_inst2,
		segment_name, server_name, i);
	}
	
	// Initialize the scheduler_interface by pushing in channel_number(s).
	// These channel_number(s) represents free channels. For now, the
	// channels are more or less evenly distributed among the schedulers.
	for (std::size_t n = 0; n < channels_size; ++n) {
		scheduler_interface[n % schedulers].insert(n, 1 /* user id */,
		smp::spinlock::milliseconds(10000));
	}
	
	//--------------------------------------------------------------------------
	// Construct the client_interface array in shared memory.
	
	// Initialize shared memory STL-compatible allocator.
	const shm_alloc_for_the_client_interfaces2
	client_interface_alloc_inst(global_mapped_region.get_address());
	
	// Allocate the client_interface array.
	p = psegment_manager->create_named_block
	(starcounter_core_shared_memory_client_interfaces_name,
	sizeof(client_interface_type) * max_number_of_clients);
	
	client_interface_type* client_interface = (client_interface_type*) p;
	
	for (std::size_t i = 0; i < max_number_of_clients; ++i) {
		new (client_interface +i) client_interface_type
		(client_interface_alloc_inst, segment_name, i);
	}
	
	//--------------------------------------------------------------------------
	// Construct the common_client_interface.
	
	// Initialize shared memory STL-compatible allocator.
	const shm_alloc_for_the_common_client_interface2
	common_client_interface_alloc_inst(global_mapped_region.get_address());
	
	// Allocate the common_client_interface.
	p = psegment_manager->create_named_block
	(starcounter_core_shared_memory_common_client_interface_name,
	sizeof(common_client_interface_type));
	
	common_client_interface_type* common_client_interface = new (p)
	common_client_interface_type(segment_name);
	
	// Initialize the client_number_pool queue with client numbers.
	for (client_number n = 0; n < max_number_of_clients; ++n) {
		common_client_interface->insert_client_number(n, client_interface,
		owner_id(1));
	}
	
	//--------------------------------------------------------------------------
	// Construct the channel_interface in shared memory.
	
	// Initialize shared memory STL-compatible allocator.
	const shm_alloc_for_the_channel_interface
	channel_interface_alloc_inst(global_mapped_region.get_address());
	
	// Allocate the channel_interface.
	p = psegment_manager->create_named_block
	(starcounter_core_shared_memory_channel_interface_name,
	sizeof(channel_interface_type));
	
	channel_interface_type* channel_interface = (channel_interface_type*) p;
	
	new (channel_interface) channel_interface_type(channel_interface_alloc_inst,
	channels_size);
	
	//--------------------------------------------------------------------------
	// Construct the channel array in shared memory.
	
	// Initialize shared memory STL-compatible allocator.
	const shm_alloc_for_channels channels_alloc_inst
	(global_mapped_region.get_address());
	
	// Allocate the channel array.
	p = psegment_manager->create_named_block
	(starcounter_core_shared_memory_channels_name, sizeof(channel_type)
	* channels_size);
	
	channel_type* channel = (channel_type*) p;
	
	for (std::size_t i = 0; i < channels_size; ++i) {
		new (channel +i) channel_type(channel_capacity, channels_alloc_inst);
		channel[i].out_overflow().set_chunk_ptr(chunk);
	}
	
	//--------------------------------------------------------------------------
	// Construct an array of scheduler_channels in shared memory.
	
	// Initialize shared memory STL-compatible allocator.
	const shm_alloc_for_channels
	scheduler_channels_alloc_inst(global_mapped_region.get_address());
	
	p = psegment_manager->create_named_block
	(starcounter_core_shared_memory_scheduler_task_channels_name,
	sizeof(scheduler_channel_type) * schedulers);
	
	scheduler_channel_type* scheduler_task_channel = (scheduler_channel_type*) p;
	
	p = psegment_manager->create_named_block
	(starcounter_core_shared_memory_scheduler_signal_channels_name,
	sizeof(scheduler_channel_type) * schedulers);
	
	scheduler_channel_type* scheduler_signal_channel = (scheduler_channel_type*) p;
	
	for (std::size_t i = 0; i < schedulers; ++i) {
		new (scheduler_task_channel +i) scheduler_channel_type(channel_capacity,
		scheduler_channels_alloc_inst);
		
		new (scheduler_signal_channel +i) scheduler_channel_type(
		channel_capacity, scheduler_channels_alloc_inst);
	}
	
	return 0;
}
catch (...) {
	return SCERRSERVERINITUNKNOWNEXCEPTION;
}

} // namespace core
} // namespace starcounter
