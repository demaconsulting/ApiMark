#pragma once
#include <string>

namespace fixtures {

/// @brief Abstract base shape.
class Shape {
public:
    /// @brief Computes the area of the shape.
    /// @return The area as a double.
    virtual double Area() const = 0;

    /// @brief Returns the name of the shape.
    /// @return A display name string.
    virtual std::string Name() const = 0;
};

/// @brief A circle shape.
class Circle : public Shape {
public:
    /// @brief Constructs a circle with the given radius.
    /// @param radius The circle radius.
    explicit Circle(double radius);

    /// @brief Computes the circle area.
    /// @return pi * radius^2.
    double Area() const override;

    /// @brief Returns "Circle".
    /// @return The string "Circle".
    std::string Name() const override;

private:
    double _radius;
};

} // namespace fixtures
