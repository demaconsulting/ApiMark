#pragma once
#include <string>

namespace fixtures {

/// @brief A class demonstrating case-insensitive member name collision.
/// @details Used to verify that the generator combines members whose names differ only in
///          case (e.g. method Name() and field name) onto a single shared Markdown page
///          so that no two output files collide on case-insensitive file systems.
class CaseCollisionClass {
public:
    /// @brief Gets the formatted name.
    /// @return The name string.
    std::string Name() const;

    /// @brief The backing name field.
    std::string name;
};

} // namespace fixtures
