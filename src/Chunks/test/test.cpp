//
// test.cpp
// IPC test
//
// Copyright � 2006-2013 Starcounter AB. All rights reserved.
// Starcounter� is a registered trademark of Starcounter AB.
//
// This IPC test is for the Windows platform.
//

#include <cstdint>
#include <iostream>
#include <iomanip>
#include <ios>
#include <boost/interprocess/sync/interprocess_mutex.hpp>
#include <boost/interprocess/sync/scoped_lock.hpp>
#include <boost/interprocess/sync/interprocess_condition.hpp>
#include <boost/thread/thread.hpp>
#include <boost/thread/mutex.hpp>
#include <boost/thread/condition.hpp>
#include <boost/shared_ptr.hpp>
#include <boost/call_traits.hpp>
#include <boost/bind.hpp>
#include <boost/utility.hpp>
#include <boost/noncopyable.hpp>
#include <boost/scoped_ptr.hpp>
#include <sccoreerr.h>
#include "../common/shared_interface.hpp"
#include "../common/config_param.hpp"
#include "../common/macro_definitions.hpp"
#include "test.hpp"

// For testing tiny tuple
#include "tiny_tuple/test.hpp"
#include "tiny_tuple/tiny_tuple.hpp"
#include "tiny_tuple/record_data.hpp"
#include "decimal/decimal.hpp"
#include "decimal/uint128_t.hpp"

//int64_t int2dec(int64_t v) { return v * 1E6; }

namespace {

// Exponents from 1e1 to 1e28 expressed as 128-bit constants.
const starcounter::numerics::uint128_t _1e1(0x0000000000000000ULL, 0x000000000000000AULL);
const starcounter::numerics::uint128_t _1e2(0x0000000000000000ULL, 0x0000000000000064ULL);
const starcounter::numerics::uint128_t _1e3(0x0000000000000000ULL, 0x00000000000003E8ULL);
const starcounter::numerics::uint128_t _1e4(0x0000000000000000ULL, 0x0000000000002710ULL);
const starcounter::numerics::uint128_t _1e5(0x0000000000000000ULL, 0x00000000000186A0ULL);
const starcounter::numerics::uint128_t _1e6(0x0000000000000000ULL, 0x00000000000F4240ULL);
const starcounter::numerics::uint128_t _1e7(0x0000000000000000ULL, 0x0000000000989680ULL);
const starcounter::numerics::uint128_t _1e8(0x0000000000000000ULL, 0x0000000005f5e100ULL);
const starcounter::numerics::uint128_t _1e9(0x0000000000000000ULL, 0x000000003B9ACA00ULL);
const starcounter::numerics::uint128_t _1e10(0x0000000000000000ULL, 0x00000002540BE400ULL);
const starcounter::numerics::uint128_t _1e11(0x0000000000000000ULL, 0x000000174876E800ULL);
const starcounter::numerics::uint128_t _1e12(0x0000000000000000ULL, 0x000000E8D4A51000ULL);
const starcounter::numerics::uint128_t _1e13(0x0000000000000000ULL, 0x000009184E72A000ULL);
const starcounter::numerics::uint128_t _1e14(0x0000000000000000ULL, 0x00005AF3107A4000ULL);
const starcounter::numerics::uint128_t _1e15(0x0000000000000000ULL, 0x00038D7EA4C68000ULL);
const starcounter::numerics::uint128_t _1e16(0x0000000000000000ULL, 0x002386F26FC10000ULL);
const starcounter::numerics::uint128_t _1e17(0x0000000000000000ULL, 0x016345785D8A0000ULL);
const starcounter::numerics::uint128_t _1e18(0x0000000000000000ULL, 0x0DE0B6B3A7640000ULL);
const starcounter::numerics::uint128_t _1e19(0x0000000000000000ULL, 0x8AC7230489E80000ULL);
const starcounter::numerics::uint128_t _1e20(0x0000000000000005ULL, 0x6BC75E2D63100000ULL);
const starcounter::numerics::uint128_t _1e21(0x0000000000000036ULL, 0x35C9ADC5DEA00000ULL);
const starcounter::numerics::uint128_t _1e22(0x000000000000021EULL, 0x19E0C9BAB2400000ULL);
const starcounter::numerics::uint128_t _1e23(0x000000000000152DULL, 0x02C7E14AF6800000ULL);
const starcounter::numerics::uint128_t _1e24(0x000000000000D3C2ULL, 0x1BCECCEDA1000000ULL);
const starcounter::numerics::uint128_t _1e25(0x0000000000084595ULL, 0x161401484A000000ULL);
const starcounter::numerics::uint128_t _1e26(0x000000000052B7D2ULL, 0xDCC80CD2E4000000ULL);
const starcounter::numerics::uint128_t _1e27(0x00000000033B2E3CULL, 0x9FD0803CE8000000ULL);
const starcounter::numerics::uint128_t _1e28(0x00000000204FCE5EULL, 0x3E25026110000000ULL);

} // namespace

// 08-Hosting have DbState.cs with the functions:
// public static Decimal ReadDecimal(ulong oid, ulong address, Int32 index);
// public static void WriteDecimal(ulong oid, ulong address, Int32 index, Decimal value);

// Currently those functions call the kernel functions:
// sccoredb.SCObjectReadDecimal2(oid, address, index, &pArray4);
// sccoredb.Mdb_ObjectWriteDecimal(oid, address, index, bits[0], bits[1], bits[2], bits[3]);

// Instead, those functions shall call conversion functions in
// 07-VMDBMS, where a C++ DLL shall be created. It shall have the conversion functions:
// starcounter_x6_decimal_to_clr_decimal();
// clr_decimal_to_starcounter_x6_decimal();
// and these conversion functions will call the kernel functions.

// Before proceeding, I shall test the conversion functions here.

// I will simulate a CLR decimal with a C++ class for that.
// It will have output functions to see the bit pattern of the 128-bit CLR decimal, etc.

///=============================================================================
/// This is to be placed in 07-VMDBMS, in the new C++ DLL project scdecimal.
///=============================================================================
#if 0
namespace {

const int64_t x6_decimal_max = +4398046511103999999LL;
const int64_t x6_decimal_min = -4398046511103999999LL;

} // namespace

typedef int32_t* clr_decimal_pointer;
typedef clr_decimal_pointer* clr_decimal_handle;
typedef uint16_t data_value_flags_type;


/// Reading a decimal requires type conversion from starcounter X6 decimal to
/// CLR decimal. This function is called by ReadDecimal() in DbState.cs.
/**
 * @param object_id Object ID.
 * @param object_eti The object ETI (address.)
 * @param index The index.
 * @param clr_decimal_handle A handle to a CLR Decimal.
 * @return Data value flags.
 */
data_value_flags_type convert_x6_decimal_to_clr_decimal(uint64_t object_id,
uint64_t object_eti, int32_t index, clr_decimal_handle clr_decimal_handle) {
	data_value_flags_type flags = 0;
	
	// No data loss can occur. No multiplication or division is needed.
	// Store the x6 decimal 63-bit value in the integer part of CLR decimal,
	// and the x6 decimal sign flag (bit 63) in the sign flag (bit 127) of the
	// CLR decimal, and set the exponent to 6 in the CLR decimal.

	// Simulating that we got the x6 decimal (in raw format) via a kernel function.
	uint64_t x6_raw_decimal = x6_decimal_max;

	// Where is the storage of the CLR decimal going to be? clr_decimal shall
	// be directed to that.

	*clr_decimal_handle = new int32_t[4]; // Simulating. This is a bad id�a in C++.
	(*clr_decimal_handle)[0] = 0x11111111;
	(*clr_decimal_handle)[1] = 0x22222222;
	(*clr_decimal_handle)[2] = 0x33333333;
	(*clr_decimal_handle)[3] = 0x80060000;

	return flags;
}

/// Writing a decimal requires type conversion from CLR decimal to starcounter
/// X6 decimal. This function is called by WriteDecimal() in DbState.cs.
/**
 * @return An error code. If there would be a data loss, etc.
 */
data_value_flags_type convert_clr_decimal_to_x6_decimal() {
	data_value_flags_type flags = 0;
	//...
	// Data loss may occur while doing multiplication or division, which is
	// needed if the exponent is not 6.
	//...
	return flags;
}

///=============================================================================
/// Here I simulate the ReadDecimal() and WriteDecimal() functions found in
/// DbState.cs under 08-Hosting.

typedef starcounter::numerics::clr::decimal Decimal;

/// Exception class.
class convert_x6_decimal_to_clr_decimal_excption {
public:
	typedef uint32_t error_code_type;
	
	explicit convert_x6_decimal_to_clr_decimal_excption(error_code_type err)
	: err_(err) {}
	
	error_code_type error_code() const {
		return err_;
	}
	
private:
	error_code_type err_;
};

Decimal ReadDecimal(uint64_t oid, uint64_t address, int32_t index) {
	uint32_t ec;

	{ // unsafe
		int32_t* pArray4;
		uint16_t flags = convert_x6_decimal_to_clr_decimal(oid, address, index,
		&pArray4);

		if ((flags & Mdb_DataValueFlag_Exceptional) == 0) {
			return /* new */ Decimal(
			pArray4[0],
			pArray4[1],
			pArray4[2],
			pArray4[3] >> 31,
			pArray4[3] >> 16);
		}
	}

	ec = 999L; // ec = sccoredb.Mdb_GetLastError();
	throw convert_x6_decimal_to_clr_decimal_excption(ec);
}

void WriteDecimal();
#endif

///=============================================================================

namespace {

const int64_t x6_decimal_max = +4398046511103999999LL;
const int64_t x6_decimal_min = -4398046511103999999LL;

} // namespace

typedef uint16_t data_value_flags_type;

data_value_flags_type sccoredb_get_encdec(uint64_t record_id, uint64_t record_addr,
uint32_t column_index, int64_t* pvalue) {
	*pvalue = x6_decimal_min +1;
	return 0;
}

uint32_t sccoredb_put_encdec(uint64_t record_id, uint64_t record_addr, uint32_t column_index, int64_t value) {
	uint32_t error_code = 0;
	std::cout << "sccoredb_put_encdec(): writing the value " << value << std::endl;
	return error_code;
}

#if 0
DLL_IMPORT extern data_value_flags_type __stdcall
convert_x6_decimal_to_clr_decimal(uint64_t record_id,
uint64_t record_addr, int32_t index, int32_t* decimal_part_ptr);

#else
/// Reading a decimal requires type conversion from starcounter X6 decimal to
/// CLR decimal. This function is called by ReadDecimal() in DbState.cs.
/**
 * @param record_id The ID of the record.
 * @param record_addr The address of the record.
 * @param column_index The column index.
 * @param decimal_part_ptr A pointer to the first element of int32_t[4],
 *		where the CLR Decimal value will be composed.
 * @return Data value flags.
 */
data_value_flags_type convert_x6_decimal_to_clr_decimal(uint64_t record_id,
uint64_t record_addr, int32_t column_index, int32_t* decimal_part_ptr) {
	int64_t value;

	data_value_flags_type flags
	= sccoredb_get_encdec(record_id, record_addr, column_index, &value);

	bool out_of_range = (value > x6_decimal_max) | (value < x6_decimal_min);

	*((uint64_t*) decimal_part_ptr) = value & 0x7FFFFFFFFFFFFFFFULL;
	*((uint64_t*) decimal_part_ptr +1) = value & 0x8000000000000000ULL
	| 0x0006000000000000ULL;
	
	return flags;
}
#endif

/// Writing a decimal requires type conversion from CLR decimal to starcounter
/// X6 decimal. This function is called by WriteDecimal() in DbState.cs.
/**
 * @param record_id The ID of the record.
 * @param record_addr The address of the record.
 * @param column_index The column index.
 * @param low Bit 31:0 of the 96-bit value.
 * @param middle Bit 63:32 of the 96-bit value.
 * @param high Bit 95:64 of the 96-bit value.
 * @param scale_sign Contains the scale (exponent) and the sign flag.
 * @return An error code.
 */
uint32_t convert_clr_decimal_to_x6_decimal(uint64_t record_id, uint64_t record_addr, int32_t column_index,
int32_t low, int32_t middle, int32_t high, int32_t scale_sign) {
	using namespace starcounter::numerics;

	// Constructing a decimal as a 128-bit value with the integer part of the CLR Decimal, bit 95:0.
	// The value is treated as positive, so testing that it is not > x6_decimal_max is the same as
	// testing that the value is not < x6_decimal_min when the CLR Decimal holds a negative value.
	uint128_t decimal(0, high, middle, low);
	bool range_error;
	
	// The exponent (scale) value is 0 to 28. Change to 6 decimals if not already 6 decimals.
	switch ((scale_sign >> 16) & 255) {
	case 0: goto multiply_by_1e6;
	case 1: goto multiply_by_1e5;
	case 2: goto multiply_by_1e4;
	case 3: goto multiply_by_1e3;
	case 4: goto multiply_by_1e2;
	case 5: goto multiply_by_1e1;
	case 6: goto already_6_decimals;
	case 7: goto divide_by_1e1;
	case 8: goto divide_by_1e2;
	case 9: goto divide_by_1e3;
	case 10: goto divide_by_1e4;
	case 11: goto divide_by_1e5;
	case 12: goto divide_by_1e6;
	case 13: goto divide_by_1e7;
	case 14: goto divide_by_1e8;
	case 15: goto divide_by_1e9;
	case 16: goto divide_by_1e10;
	case 17: goto divide_by_1e11;
	case 18: goto divide_by_1e12;
	case 19: goto divide_by_1e13;
	case 20: goto divide_by_1e14;
	case 21: goto divide_by_1e15;
	case 22: goto divide_by_1e16;
	case 23: goto divide_by_1e17;
	case 24: goto divide_by_1e18;
	case 25: goto divide_by_1e19;
	case 26: goto divide_by_1e20;
	case 27: goto divide_by_1e21;
	case 28: goto divide_by_1e22;
	default: UNREACHABLE;
	}
multiply_by_1e6:
	decimal *= _1e6;
	range_error = decimal > x6_decimal_max;
	goto write_decimal;
multiply_by_1e5:
	decimal *= _1e5;
	range_error = decimal > x6_decimal_max;
	goto write_decimal;
multiply_by_1e4:
	decimal *= _1e4;
	range_error = decimal > x6_decimal_max;
	goto write_decimal;
multiply_by_1e3:
	decimal *= _1e3;
	range_error = decimal > x6_decimal_max;
	goto write_decimal;
multiply_by_1e2:
	decimal *= _1e2;
	range_error = decimal > x6_decimal_max;
	goto write_decimal;
multiply_by_1e1:
	decimal *= _1e1;
	range_error = decimal > x6_decimal_max;
	goto write_decimal;
already_6_decimals:
	range_error = decimal > x6_decimal_max;
	goto write_decimal;
divide_by_1e1:
	range_error = decimal.divide_and_get_remainder(_1e1) != 0 | decimal > x6_decimal_max;
	goto write_decimal;
divide_by_1e2:
	range_error = decimal.divide_and_get_remainder(_1e2) != 0 | decimal > x6_decimal_max;
	goto write_decimal;
divide_by_1e3:
	range_error = decimal.divide_and_get_remainder(_1e3) != 0 | decimal > x6_decimal_max;
	goto write_decimal;
divide_by_1e4:
	range_error = decimal.divide_and_get_remainder(_1e4) != 0 | decimal > x6_decimal_max;
	goto write_decimal;
divide_by_1e5:
	range_error = decimal.divide_and_get_remainder(_1e5) != 0 | decimal > x6_decimal_max;
	goto write_decimal;
divide_by_1e6:
	range_error = decimal.divide_and_get_remainder(_1e6) != 0 | decimal > x6_decimal_max;
	goto write_decimal;
divide_by_1e7:
	range_error = decimal.divide_and_get_remainder(_1e7) != 0 | decimal > x6_decimal_max;
	goto write_decimal;
divide_by_1e8:
	range_error = decimal.divide_and_get_remainder(_1e8) != 0 | decimal > x6_decimal_max;
	goto write_decimal;
divide_by_1e9:
	range_error = decimal.divide_and_get_remainder(_1e9) != 0 | decimal > x6_decimal_max;
	goto write_decimal;
divide_by_1e10:
	range_error = decimal.divide_and_get_remainder(_1e10) != 0 | decimal > x6_decimal_max;
	goto write_decimal;
divide_by_1e11:
	range_error = decimal.divide_and_get_remainder(_1e11) != 0 | decimal > x6_decimal_max;
	goto write_decimal;
divide_by_1e12:
	range_error = decimal.divide_and_get_remainder(_1e12) != 0 | decimal > x6_decimal_max;
	goto write_decimal;
divide_by_1e13:
	range_error = decimal.divide_and_get_remainder(_1e13) != 0 | decimal > x6_decimal_max;
	goto write_decimal;
divide_by_1e14:
	range_error = decimal.divide_and_get_remainder(_1e14) != 0 | decimal > x6_decimal_max;
	goto write_decimal;
divide_by_1e15:
	range_error = decimal.divide_and_get_remainder(_1e15) != 0 | decimal > x6_decimal_max;
	goto write_decimal;
divide_by_1e16:
	range_error = decimal.divide_and_get_remainder(_1e16) != 0 | decimal > x6_decimal_max;
	goto write_decimal;
divide_by_1e17:
	range_error = decimal.divide_and_get_remainder(_1e17) != 0 | decimal > x6_decimal_max;
	goto write_decimal;
divide_by_1e18:
	range_error = decimal.divide_and_get_remainder(_1e18) != 0 | decimal > x6_decimal_max;
	goto write_decimal;
divide_by_1e19:
	range_error = decimal.divide_and_get_remainder(_1e19) != 0 | decimal > x6_decimal_max;
	goto write_decimal;
divide_by_1e20:
	range_error = decimal.divide_and_get_remainder(_1e20) != 0 | decimal > x6_decimal_max;
	goto write_decimal;
divide_by_1e21:
	range_error = decimal.divide_and_get_remainder(_1e21) != 0 | decimal > x6_decimal_max;
	goto write_decimal;
divide_by_1e22:
	range_error = decimal.divide_and_get_remainder(_1e22) != 0 | decimal > x6_decimal_max;
write_decimal:
	if (range_error == false) {
		// The value fits in a X6 decimal.
		int64_t value = decimal.low() | (uint64_t(scale_sign) >> 31) << 63;
		return sccoredb_put_encdec(record_id, record_addr, column_index, value);
	}
	else {
		// The value doesn't fit in a X6 decimal.
		/// TODO: Error code for range error.
		return 999L;
	}
}

/// This represents C# user code calling ReadDecimal() and WriteDecimal(), etc.
int wmain(int argc, wchar_t* argv[], wchar_t* envp[])
try {
	using namespace starcounter::numerics;
	data_value_flags_type data_value_flags;
	int32_t decimal_part[4];
	
	uint32_t exponent;
	uint32_t sign;

	/// READ
	data_value_flags = convert_x6_decimal_to_clr_decimal(0, 0, 0, decimal_part);
	uint128_t d(decimal_part[3], decimal_part[2], decimal_part[1], decimal_part[0]);
	//decimal_part[2] = 1; // Force range error - test.

	exponent = decimal_part[3] >> 16 & 255;
	sign = decimal_part[3] >> 31 & 1;
	std::cout << "CLR decimal value (dec): " << std::dec << d << "\n";
	std::cout << "CLR decimal exponent: " << exponent << "\n";
	std::cout << "CLR decimal sign: " << sign << "\n";

	/// WRITE
	bool result = convert_clr_decimal_to_x6_decimal(0, 0, 0,
	decimal_part[0], decimal_part[1], decimal_part[2], decimal_part[3]);

	exponent = decimal_part[3] >> 16 & 255;
	sign = decimal_part[3] >> 31 & 1;
	std::cout << "CLR decimal value (dec): " << std::dec << d << "\n";
	std::cout << "CLR decimal exponent: " << exponent << "\n";
	std::cout << "CLR decimal sign: " << sign << "\n";
	
	//std::cout << "data_value_flags: " << data_value_flags << std::endl;

#if 0
	try {
		Decimal my_clr_decimal = ReadDecimal(0, 0, 0);
		my_clr_decimal.print();
		return 0;
	}
	catch (convert_x6_decimal_to_clr_decimal_excption) {
		std::cout << "error: convert_x6_decimal_to_clr_decimal_excption caught." << std::endl;
	}
#endif
#if 0
	//uint128_t a(_1e28);
	uint128_t a(1234);
	//a *= 7;
	uint128_t x = a;
	uint128_t remainder(x.divide_and_get_remainder(1235));
	
	if (remainder == 0) {
		std::cout << "No data loss. x = " << x << std::endl;
	}
	else {
		std::cout << "Data loss. x = " << x << std::endl;
		std::cout << "remainder = " << remainder << std::endl;
	}
#endif
#if 0
	void vec_u128_divide(
	const uint128_t* numerator,
	const uint128_t* divisor,
	uint128_t* result,
	uint128_t* remainder
	);

	// Specialized with divisor 1e22, etc:
	/**
	 * @param numerator The numerator.
	 * @param remainder The remainder.
	 * @return The result.
	 */
	uint128_t divide_by_1e22(const uint128_t& numerator, uint128_t* remainder);
#endif

#if 1
	clr::decimal ae;
	return 0;
#endif

#if 0
	///=========================================================================
	/// Tiny tuple test:
	///=========================================================================

	try {
		using namespace starcounter::core::tiny_tuple::record;

		// Pretend that the RECORD HEADER size is 3 bytes.
		uint32_t record_header_size = 3;

		// Get a pointer to the DATA HEADER in the record.
		data_header::pointer_type data_header_addr = data_header::pointer_type
		(get_pointer_to_record_data(record_header_size));

		std::cout << "DATA HEADER ADDR: " << data_header_addr << std::endl;
		
		data_header::size_type data_header_size
		= data_header::size(data_header_addr);

		std::cout << "DATA HEADER SIZE: " << data_header_size << std::endl;

		// Read the COLUMNS value from the DATA HEADER.
		uint32_t columns = data_header::get_columns(data_header_addr);
		std::cout << "COLUMNS: " << columns << std::endl;

		// Write a COLUMNS value to the DATA HEADER.
		data_header::set_columns(data_header_addr, 10);

		// Read the COLUMNS value from the DATA HEADER.
		columns = data_header::get_columns(data_header_addr);
		std::cout << "COLUMNS: " << columns << std::endl;

		// Read the OFFSET SIZE value from the DATA HEADER.
		uint32_t osize = data_header::get_offset_size(data_header_addr);
		std::cout << "OFFSET SIZE: " << osize << std::endl;

		// Write an OFFSET SIZE value to the DATA HEADER.
		data_header::set_offset_size(data_header_addr, 9);

		// Read the OFFSET SIZE value from the DATA HEADER.
		osize = data_header::get_offset_size(data_header_addr);
		std::cout << "OFFSET SIZE: " << osize << std::endl;

		defined_column_value::pointer_type value_ptr;
		data_header::index_type index = 0;
		defined_column_value::size_type sz = 0;

		// Get value to DEFINED COLUMN VALUE at index, or 0 if not defined.
		value_ptr = get_pointer_to_value(data_header_addr, index, &sz);
		
		std::cout << "DEFINED COLUMN VALUE POINTER: " << (void*) value_ptr << std::endl;
		std::cout << "DEFINED COLUMN VALUE SIZE: " << sz << std::endl;

		if (value_ptr) {
			uint64_t value = *((uint64_t*) value_ptr);
			value &= (1ULL << (sz << 3)) -1;
			std::cout << "DEFINED COLUMN VALUE: " << value << std::endl;
		}

		uint8_t data[16] = { 0xFF, 0xFF, 0xFF };
		bool is_defined;

		data_header::set_defined_column_flag(data_header::pointer_type(&data), 5, false);
		std::cout << "CLEARING FLAG AT INDEX " << 0 << std::endl;
		data_header::set_defined_column_flag(data_header::pointer_type(&data), 5, true);
		std::cout << "SETTING FLAG AT INDEX " << 0 << std::endl;
		
		for (index = 0; index < 10; ++index) {
			columns = 1;
			data_header::offset_type offset_size = 6;

			data_header::offset_distance_type distance
			= data_header::get_distance_to_offset(data_header_addr, index, columns, offset_size);
			std::cout << "DISTANCE FOR INDEX " << index
			<< ", COLUMNS " << columns
			<< ", OFFSET SIZE " << offset_size
			<< ": " << distance << std::endl;
		}

		data_header::set_offset(data_header_addr, index, -1);

		data_header::offset_type some_offset
		= data_header::get_offset(data_header_addr, index);
		std::cout << "OFFSET VALUE AT INDEX " << index << " = " << some_offset << std::endl;

		return 0;

		for (uint32_t j = 0; j < 10; ++j) {
			for (index = 0; index < 10; ++index) {
				is_defined = data_header::get_defined_column_flag(data_header::pointer_type(&data), index);
				std::cout << "COLUMN AT INDEX " << index << " IS DEFINED: " << is_defined << std::endl;
			}
			data_header::set_defined_column_flag(data_header::pointer_type(&data), index, false);
			std::cout << "CLEARED FLAG AT INDEX " << j << std::endl;
		}

		data_header::set_defined_column_flag(data_header::pointer_type(&data), index, true);

		is_defined = data_header::get_defined_column_flag(data_header::pointer_type(&data), index);
		std::cout << "DATA COLUMN IS DEFINED: " << is_defined << std::endl;

		data_header::set_defined_column_flag(data_header::pointer_type(&data), index, false);

		is_defined = data_header::get_defined_column_flag(data_header::pointer_type(&data), index);
		std::cout << "DATA COLUMN IS DEFINED: " << is_defined << std::endl;

		// Start the tiny_tuple_test application.
		//boost::scoped_ptr<starcounter::core::tiny_tuple::test> app
		//(new starcounter::core::tiny_tuple::test(argc, argv));

		//app->run();
	}
	catch (...) {
		// An unknown exception was caught.
		std::cout << "error: unknown exception caught" << std::endl;
	}
	return 0;
#endif
	///=========================================================================
	
	// Start the interprocess_communication test application.
	boost::scoped_ptr<starcounter::interprocess_communication::test> app
	(new starcounter::interprocess_communication::test(argc, argv));
	
	std::cout << "workers: " << starcounter::interprocess_communication::test::workers << std::endl;

	app->run(200 /* interval time milliseconds */,
	6000000 /* duration time milliseconds */);
	
	// Stop worker 0.
	//app->stop_worker(0);
	//app->stop_all_workers();
	
	Sleep(INFINITE);
	std::cout << "test: exit." << std::endl;
}
catch (starcounter::interprocess_communication::test_exception& e) {
	std::cout << "error: test_exception "
	<< "caught with error code " << e.error_code() << std::endl;
}
catch (starcounter::interprocess_communication::worker_exception& e) {
	std::cout << "error: worker_exception "
	<< "caught with error code " << e.error_code() << std::endl;
}
catch (starcounter::core::database_shared_memory_parameters_ptr_exception& e) {
	std::cout << "error: database_shared_memory_parameters_ptr_exception "
	<< "caught with error code " << e.error_code() << std::endl;
}
catch (starcounter::core::monitor_interface_ptr_exception& e) {
	std::cout << "error: monitor_interface_ptr_exception "
	<< "caught with error code " << e.error_code() << std::endl;
}
catch (starcounter::core::shared_interface_exception& e) {
	std::cout << "error: shared_interface_exception "
	<< "caught with error code " << e.error_code() << std::endl;
}
catch (boost::interprocess::interprocess_exception&) {
	std::cout << "error: shared_interface_exception caught" << std::endl;
}
catch (...) {
	// An unknown exception was caught.
	std::cout << "error: unknown exception caught" << std::endl;
}
