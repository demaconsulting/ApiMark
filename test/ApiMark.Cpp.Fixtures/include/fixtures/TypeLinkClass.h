#pragma once
#include "InheritanceClass.h"

namespace fixtures {

/// @brief A fixture class for testing intra-doc type links.
class TypeLinkClass {
public:
    /// @brief Creates a Shape from a name string.
    /// @param name The shape name.
    /// @return A pointer to the created shape.
    Shape* CreateShape(const std::string& name);

    /// @brief Resets the type link class state.
    void Reset();
};

} // namespace fixtures
