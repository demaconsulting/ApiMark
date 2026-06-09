#pragma once

namespace fixtures {

/// @brief A class with explicitly deleted special member functions.
///
/// Documents the pattern of a move-only type where copying is explicitly
/// forbidden.
class DeletedMembersClass {
public:
    /// @brief Constructs a DeletedMembersClass with the given value.
    /// @param value The initial value.
    explicit DeletedMembersClass(int value);

    /// @brief Deleted copy constructor — this type is not copyable.
    /// @param other The instance that would have been copied.
    DeletedMembersClass(const DeletedMembersClass& other) = delete;

    /// @brief Deleted copy-assignment operator — this type is not copyable.
    /// @param other The instance that would have been assigned from.
    /// @return Reference to this instance.
    DeletedMembersClass& operator=(const DeletedMembersClass& other) = delete;

    /// @brief Gets the stored value.
    /// @return The value.
    int GetValue() const;
};

} // namespace fixtures
