#pragma once
#include <ostream>

namespace fixtures {

/// @brief A class with operator overloads for testing grouped operator page generation.
class OperatorClass {
public:
    /// @brief Adds two OperatorClass values.
    /// @param rhs The right-hand side operand.
    /// @return The sum of the two values.
    OperatorClass operator+(const OperatorClass& rhs) const;

    /// @brief Compares two OperatorClass values for equality.
    /// @param rhs The right-hand side operand.
    /// @return True when both values are equal.
    bool operator==(const OperatorClass& rhs) const;
};

/// @brief Stream insertion operator for OperatorClass.
/// @param os The output stream.
/// @param obj The value to insert into the stream.
/// @return The output stream after insertion.
std::ostream& operator<<(std::ostream& os, const OperatorClass& obj);

} // namespace fixtures
