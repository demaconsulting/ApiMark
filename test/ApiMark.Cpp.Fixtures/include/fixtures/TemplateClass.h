#pragma once

namespace fixtures {

/// @brief A generic stack container.
/// @tparam T The element type.
template<typename T>
class Stack {
public:
    /// @brief Pushes an element onto the stack.
    /// @param value The value to push.
    void Push(const T& value);

    /// @brief Pops and returns the top element.
    /// @return The top element.
    T Pop();

    /// @brief Returns whether the stack is empty.
    /// @return True if the stack has no elements.
    bool IsEmpty() const;
};

} // namespace fixtures
