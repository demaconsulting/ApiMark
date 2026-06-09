#pragma once

#include <cstdint>

namespace fixtures {

/// @brief Outer class that contains a nested class and a class-scoped type alias.
class Outer {
public:
    /// @brief A size type scoped to Outer.
    using size_type = uint32_t;

    /// @brief Inner nested class.
    class Inner {
    public:
        /// @brief Gets the value.
        int value() const;
    };
};

/// @brief Another class that also declares a size_type alias (different from Outer::size_type).
class Other {
public:
    /// @brief A size type scoped to Other (should NOT collide with Outer::size_type in knownTypes).
    using size_type = uint16_t;
};

} // namespace fixtures
