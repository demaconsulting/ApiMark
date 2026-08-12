#pragma once

#include <string>

namespace fixtures {

/// A fully-undocumented class exercising every DocumentationCoverageChecker kind label,
/// including overloaded constructors and methods so their reported DisplayName can be
/// verified as unambiguous.
class UndocumentedKindsClass {
public:
    /// @brief Documented default constructor overload, present only so the undocumented
    /// overload below is a genuine overload rather than the only constructor.
    UndocumentedKindsClass();

    /// Undocumented constructor overload taking a name — deliberately left without a
    /// Doxygen summary so DocumentationCoverageChecker reports it, distinguishable from
    /// the documented default constructor above by its parameter signature.
    explicit UndocumentedKindsClass(const std::string& name);

    /// @brief Documented overload of DoWork() taking no arguments, present only so the
    /// undocumented overload below is a genuine overload.
    void DoWork();

    /// Undocumented overload of DoWork() taking an integer — deliberately left without a
    /// Doxygen summary so its reported DisplayName can be checked against the
    /// no-argument overload above.
    void DoWork(int count);

    /// Undocumented field.
    int UndocumentedField;
};

/// Undocumented enum exercising the Enum and EnumValue kind labels.
enum class UndocumentedKindsEnum {
    /// Undocumented enum value.
    First,
};

/// Undocumented type alias exercising the TypeAlias kind label.
using undocumented_alias_t = int;

/// Undocumented free function overload taking no arguments.
void UndocumentedFreeFunction();

/// Undocumented free function overload taking an integer, deliberately overloading
/// UndocumentedFreeFunction() above so DocumentationCoverageChecker must report both
/// with distinguishable DisplayName values.
void UndocumentedFreeFunction(int value);

} // namespace fixtures
