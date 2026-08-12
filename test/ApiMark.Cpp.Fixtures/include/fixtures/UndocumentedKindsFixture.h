// This fixture exercises every DocumentationCoverageChecker declaration kind by pairing
// a documented overload with an intentionally undocumented one for each construct.
// Undocumented declarations below deliberately have NO comment immediately preceding
// them (not even a plain "//" one) because clang's "-fparse-all-comments" mode treats
// any comment immediately preceding a declaration as its Doxygen doc comment,
// regardless of whether it uses "///" or "//".

#pragma once

#include <string>

namespace fixtures {

class UndocumentedKindsClass {
public:
    /// @brief Documented default constructor overload, present only so the undocumented
    /// overload below is a genuine overload rather than the only constructor.
    UndocumentedKindsClass();

    explicit UndocumentedKindsClass(const std::string& name);

    /// @brief Documented overload of DoWork() taking no arguments, present only so the
    /// undocumented overload below is a genuine overload.
    void DoWork();

    void DoWork(int count);

    int UndocumentedField;
};

enum class UndocumentedKindsEnum {
    First,
};

using undocumented_alias_t = int;

void UndocumentedFreeFunction();

void UndocumentedFreeFunction(int value);

} // namespace fixtures
