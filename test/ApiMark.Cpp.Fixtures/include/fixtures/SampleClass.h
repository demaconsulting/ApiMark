#pragma once
#include <string>

namespace fixtures {

/// A sample class for testing the C++ API generator.
class SampleClass {
public:
    /// @brief Constructs a SampleClass with the given name.
    /// @param name The initial name value.
    explicit SampleClass(const std::string& name);

    /// @brief Gets or sets the name.
    std::string name;

    /// @brief A default name constant.
    static constexpr const char* DefaultName = "default";

    /// @brief Gets a greeting for the specified name.
    /// @param name The name to greet.
    /// @return A greeting string.
    static std::string GetGreeting(const std::string& name);

    /// @brief Resets this instance to its default state.
    void Reset();

    void Refresh();

protected:
    /// @brief Called when the name changes.
    virtual void OnNameChanged();
};

} // namespace fixtures
