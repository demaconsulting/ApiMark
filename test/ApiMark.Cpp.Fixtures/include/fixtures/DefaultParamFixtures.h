#pragma once

#include <cstdint>

namespace fixtures {

/// @brief Computes the CRC-32 checksum of a buffer.
/// @note Result depends on the seed value; use seed=0 for a standard CRC-32.
/// @param data Pointer to the input data.
/// @param length Number of bytes to process.
/// @param seed Initial CRC value.
/// @return The computed CRC-32 checksum.
uint32_t crc32(const uint8_t* data, uint32_t length, uint32_t seed = 0U);

/// @brief Counts occurrences with an optional maximum cap.
/// @param value The value to count.
/// @param max Maximum count before capping (default: no cap).
/// @return The capped count.
int count_capped(int value, int max = -1);

} // namespace fixtures
