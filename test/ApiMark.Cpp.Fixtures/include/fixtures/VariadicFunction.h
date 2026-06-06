#pragma once

namespace fixtures {

/// @brief Formats a message with printf-style arguments.
/// @param format The format string.
/// @return Number of characters written.
int Format(const char* format, ...);

} // namespace fixtures
