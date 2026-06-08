#pragma once

namespace SampleLib {

/// @brief A sample class used as a fixture for ApiMark.MSBuild C++ package integration tests.
class SampleClass
{
public:
    /// @brief Gets the name of this sample instance.
    const char* name() const;
};

} // namespace SampleLib
