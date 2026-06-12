#pragma once

namespace fixtures {

/// @brief A class used to verify that @code/@endcode example blocks are parsed and rendered.
class ExampleDocClass {
public:
    /// @brief Returns a greeting string.
    ///
    /// @param name The name to greet.
    /// @return A greeting message.
    ///
    /// @code
    /// auto msg = ExampleDocClass{}.GetGreeting("World");
    /// @endcode
    const char* GetGreeting(const char* name);
};

} // namespace fixtures
