#pragma once

namespace fixtures {

/// @brief A class with detailed remarks documentation.
class RemarksClass {
public:
    /// @brief Computes a result.
    /// @details Uses an iterative algorithm to compute the result.
    ///          May be called multiple times safely.
    /// @return The computed integer result.
    int Compute();
};

} // namespace fixtures
