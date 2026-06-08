#pragma once

namespace fixtures {

/// @brief A deprecated class for testing the IncludeDeprecated option.
class [[deprecated("Use NewClass instead.")]] DeprecatedClass {
public:
    /// @brief Does something deprecated.
    [[deprecated]] void DeprecatedMethod();
};

} // namespace fixtures
