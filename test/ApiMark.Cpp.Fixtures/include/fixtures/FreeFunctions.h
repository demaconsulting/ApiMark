#pragma once
#include <string>

namespace fixtures {

/// @brief Adds two integers together.
/// @param a The first operand.
/// @param b The second operand.
/// @return The sum of a and b.
int Add(int a, int b);

/// @brief Formats a name for display.
/// @param name The raw name string.
/// @return A formatted display name.
std::string FormatName(const std::string& name);

} // namespace fixtures
