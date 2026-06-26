#pragma once

// Forward-declare an external (non-library, non-std) type so clang accepts the
// method signature without requiring a separate include.
namespace external {
namespace ns {
class Logger;
} // namespace ns
} // namespace external

namespace fixtures {

/// @brief A fixture class for testing external type references in generated documentation.
class ExternalTypeFixture {
public:
    /// @brief Returns a pointer to an external logger.
    /// @return A pointer to the external Logger instance.
    external::ns::Logger* GetLogger();
};

} // namespace fixtures
