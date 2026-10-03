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

    /// @brief The maximum allowed count.
    static constexpr int MaxCount = 42;

    /// @brief A computed limit (regression guard - must not render a value).
    static constexpr int ComputedLimit = 2 * 21;

    /// @brief A negative offset constant (regression guard - must still render a value).
    static constexpr int NegativeOffset = -5;

    /// @brief A constexpr boolean flag constant.
    static constexpr bool IsEnabledByDefault = true;

    /// @brief A constexpr character constant exercising NUL-character escaping
    /// (regression guard - mirrors the C# NulSeparator const char coverage).
    static constexpr char NulSeparator = '\0';

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
