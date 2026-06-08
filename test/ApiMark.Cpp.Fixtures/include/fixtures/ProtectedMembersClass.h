#pragma once

namespace fixtures {

/// @brief A class with public, protected, and private members for visibility testing.
class ProtectedMembersClass {
public:
    /// @brief A public method.
    void PublicMethod();

protected:
    /// @brief A protected method.
    /// @param value The value to process.
    void ProtectedMethod(int value);

private:
    /// @brief A private method.
    /// @param value The value to process.
    void PrivateMethod(int value);
};

} // namespace fixtures
