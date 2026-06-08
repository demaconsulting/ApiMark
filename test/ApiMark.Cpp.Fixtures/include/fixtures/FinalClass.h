#pragma once

namespace fixtures {

/// @brief A class that cannot be subclassed.
class FinalClass final {
public:
    /// @brief Gets the value.
    int value() const;
};

} // namespace fixtures
