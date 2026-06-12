# Fixtures API Reference

## fixtures

### CaseCollisionClass

```cpp
// fixtures::CaseCollisionClass
#include "fixtures/CaseCollisionClass.h"
```

A class demonstrating case-insensitive member name collision.

Used to verify that the generator combines members whose names differ only in case (e.g. method Name() and field name) onto a single shared Markdown page so that no two output files collide on case-insensitive file systems.

- **Name()**: Gets the formatted name.
- **name**: The backing name field.

#### Name()

```cpp
// fixtures::CaseCollisionClass::Name
std::string Name()
```

Gets the formatted name.

**Returns:** The name string.

#### name

```cpp
// fixtures::CaseCollisionClass::name
std::string name;
```

The backing name field.

### DeletedMembersClass

```cpp
// fixtures::DeletedMembersClass
#include "fixtures/DeletedMembersClass.h"
```

A class with explicitly deleted special member functions.

- **DeletedMembersClass(int)**: Constructs a DeletedMembersClass with the given value.
- **DeletedMembersClass(const DeletedMembersClass &)**: Deleted copy constructor — this type is not copyable.
- **GetValue()**: Gets the stored value.
- **operator=(const DeletedMembersClass &)**: Deleted copy-assignment operator — this type is not copyable.

#### DeletedMembersClass(int)

```cpp
// fixtures::DeletedMembersClass::DeletedMembersClass
DeletedMembersClass(int value)
```

Constructs a DeletedMembersClass with the given value.

| Parameter | Type | Description |
| --- | --- | --- |
| value | int | The initial value. |

#### DeletedMembersClass(const DeletedMembersClass &)

```cpp
// fixtures::DeletedMembersClass::DeletedMembersClass
DeletedMembersClass(const DeletedMembersClass & other) = delete
```

Deleted copy constructor — this type is not copyable.

| Parameter | Type | Description |
| --- | --- | --- |
| other | const DeletedMembersClass & | The instance that would have been copied. |

#### GetValue()

```cpp
// fixtures::DeletedMembersClass::GetValue
int GetValue()
```

Gets the stored value.

**Returns:** The value.

#### operator=(const DeletedMembersClass &)

```cpp
// fixtures::DeletedMembersClass::operator=
DeletedMembersClass & operator=(const DeletedMembersClass & other) = delete
```

Deleted copy-assignment operator — this type is not copyable.

| Parameter | Type | Description |
| --- | --- | --- |
| other | const DeletedMembersClass & | The instance that would have been assigned from. |

**Returns:** Reference to this instance.

### FinalClass

```cpp
// fixtures::FinalClass
#include "fixtures/FinalClass.h"
```

A class that cannot be subclassed.

- **value()**: Gets the value.

#### value()

```cpp
// fixtures::FinalClass::value
int value()
```

Gets the value.

**Returns:** int

### Shape

```cpp
// fixtures::Shape
#include "fixtures/InheritanceClass.h"
```

Abstract base shape.

- **Area()**: Computes the area of the shape.
- **Name()**: Returns the name of the shape.

#### Area()

```cpp
// fixtures::Shape::Area
double Area()
```

Computes the area of the shape.

**Returns:** The area as a double.

#### Name()

```cpp
// fixtures::Shape::Name
std::string Name()
```

Returns the name of the shape.

**Returns:** A display name string.

### Circle

```cpp
// fixtures::Circle
#include "fixtures/InheritanceClass.h"
```

A circle shape.

- **Circle(double)**: Constructs a circle with the given radius.
- **Area()**: Computes the circle area.
- **Name()**: Returns "Circle".

#### Circle(double)

```cpp
// fixtures::Circle::Circle
Circle(double radius)
```

Constructs a circle with the given radius.

| Parameter | Type | Description |
| --- | --- | --- |
| radius | double | The circle radius. |

#### Area()

```cpp
// fixtures::Circle::Area
double Area()
```

Computes the circle area.

**Returns:** pi * radius^2.

#### Name()

```cpp
// fixtures::Circle::Name
std::string Name()
```

Returns "Circle".

**Returns:** The string "Circle".

### Outer

```cpp
// fixtures::Outer
#include "fixtures/NestedClassFixtures.h"
```

Outer class that contains a nested class and a class-scoped type alias.

### Inner

Nested type of `Outer`.

```cpp
// fixtures::Inner
#include "fixtures/NestedClassFixtures.h"
```

Inner nested class.

- **value()**: Gets the value.

#### value()

```cpp
// fixtures::Inner::value
int value()
```

Gets the value.

**Returns:** int

### Other

```cpp
// fixtures::Other
#include "fixtures/NestedClassFixtures.h"
```

Another class that also declares a size_type alias (different from Outer::size_type).

### OperatorClass

```cpp
// fixtures::OperatorClass
#include "fixtures/OperatorClass.h"
```

A class with operator overloads for testing grouped operator page generation.

- **operator+(const OperatorClass &)**: Adds two OperatorClass values.
- **operator==(const OperatorClass &)**: Compares two OperatorClass values for equality.

#### operator+(const OperatorClass &)

```cpp
// fixtures::OperatorClass::operator+
OperatorClass operator+(const OperatorClass & rhs)
```

Adds two OperatorClass values.

| Parameter | Type | Description |
| --- | --- | --- |
| rhs | const OperatorClass & | The right-hand side operand. |

**Returns:** The sum of the two values.

#### operator==(const OperatorClass &)

```cpp
// fixtures::OperatorClass::operator==
bool operator==(const OperatorClass & rhs)
```

Compares two OperatorClass values for equality.

| Parameter | Type | Description |
| --- | --- | --- |
| rhs | const OperatorClass & | The right-hand side operand. |

**Returns:** True when both values are equal.

### ProtectedMembersClass

```cpp
// fixtures::ProtectedMembersClass
#include "fixtures/ProtectedMembersClass.h"
```

A class with public, protected, and private members for visibility testing.

- **PublicMethod()**: A public method.

#### PublicMethod()

```cpp
// fixtures::ProtectedMembersClass::PublicMethod
void PublicMethod()
```

A public method.

### RemarksClass

```cpp
// fixtures::RemarksClass
#include "fixtures/RemarksClass.h"
```

A class with detailed remarks documentation.

- **Compute()**: Computes a result.

#### Compute()

```cpp
// fixtures::RemarksClass::Compute
int Compute()
```

Computes a result.

**Returns:** The computed integer result.

### SampleClass

```cpp
// fixtures::SampleClass
#include "fixtures/SampleClass.h"
```

A sample class for testing the C++ API generator.

- **SampleClass(const std::string &)**: Constructs a SampleClass with the given name.
- **GetGreeting(const std::string &)**: Gets a greeting for the specified name.
- **Refresh()**: *No description provided.*
- **Reset()**: Resets this instance to its default state.
- **DefaultName**: A default name constant.
- **name**: Gets or sets the name.

#### SampleClass(const std::string &)

```cpp
// fixtures::SampleClass::SampleClass
SampleClass(const std::string & name)
```

Constructs a SampleClass with the given name.

| Parameter | Type | Description |
| --- | --- | --- |
| name | const std::string & | The initial name value. |

#### GetGreeting(const std::string &)

```cpp
// fixtures::SampleClass::GetGreeting
static std::string GetGreeting(const std::string & name)
```

Gets a greeting for the specified name.

| Parameter | Type | Description |
| --- | --- | --- |
| name | const std::string & | The name to greet. |

**Returns:** A greeting string.

#### Refresh()

```cpp
// fixtures::SampleClass::Refresh
void Refresh()
```

*No description provided.*

#### Reset()

```cpp
// fixtures::SampleClass::Reset
void Reset()
```

Resets this instance to its default state.

#### DefaultName

```cpp
// fixtures::SampleClass::DefaultName
const char *const DefaultName;
```

A default name constant.

#### name

```cpp
// fixtures::SampleClass::name
std::string name;
```

Gets or sets the name.

### Stack

```cpp
// fixtures::Stack
template<typename T>
#include "fixtures/TemplateClass.h"
```

A generic stack container.

- **IsEmpty()**: Returns whether the stack is empty.
- **Pop()**: Pops and returns the top element.
- **Push(const T &)**: Pushes an element onto the stack.

#### IsEmpty()

```cpp
// fixtures::Stack::IsEmpty
bool IsEmpty()
```

Returns whether the stack is empty.

**Returns:** True if the stack has no elements.

#### Pop()

```cpp
// fixtures::Stack::Pop
T Pop()
```

Pops and returns the top element.

**Returns:** The top element.

#### Push(const T &)

```cpp
// fixtures::Stack::Push
void Push(const T & value)
```

Pushes an element onto the stack.

| Parameter | Type | Description |
| --- | --- | --- |
| value | const T & | The value to push. |

### TypeLinkClass

```cpp
// fixtures::TypeLinkClass
#include "fixtures/TypeLinkClass.h"
```

A fixture class for testing intra-doc type links.

- **CreateShape(const std::string &)**: Creates a Shape from a name string.
- **Reset()**: Resets the type link class state.

#### CreateShape(const std::string &)

```cpp
// fixtures::TypeLinkClass::CreateShape
Shape * CreateShape(const std::string & name)
```

Creates a Shape from a name string.

| Parameter | Type | Description |
| --- | --- | --- |
| name | const std::string & | The shape name. |

**Returns:** A pointer to the created shape.

#### Reset()

```cpp
// fixtures::TypeLinkClass::Reset
void Reset()
```

Resets the type link class state.

### Add(int, int)

```cpp
int fixtures::Add(int a, int b)
```

Adds two integers together.

| Parameter | Type | Description |
| --- | --- | --- |
| a | int | The first operand. |
| b | int | The second operand. |

### Format(const char *)

```cpp
int fixtures::Format(const char * format)
```

Formats a message with printf-style arguments.

| Parameter | Type | Description |
| --- | --- | --- |
| format | const char * | The format string. |

### FormatName(const std::string &)

```cpp
std::string fixtures::FormatName(const std::string & name)
```

Formats a name for display.

| Parameter | Type | Description |
| --- | --- | --- |
| name | const std::string & | The raw name string. |

### configure(bool, bool)

```cpp
void fixtures::configure(bool enabled, bool initial)
```

Configures the module with an optional initial-state flag.

| Parameter | Type | Description |
| --- | --- | --- |
| enabled | bool | Whether to enable the module. |
| initial | bool | Whether to apply the initial state (default: false). |

### count_capped(int, int)

```cpp
int fixtures::count_capped(int value, int max)
```

Counts occurrences with an optional maximum cap.

| Parameter | Type | Description |
| --- | --- | --- |
| value | int | The value to count. |
| max | int | Maximum count before capping (default: no cap). |

### crc32(const uint8_t *, uint32_t, uint32_t)

```cpp
uint32_t fixtures::crc32(const uint8_t * data, uint32_t length, uint32_t seed)
```

Computes the CRC-32 checksum of a buffer.

| Parameter | Type | Description |
| --- | --- | --- |
| data | const uint8_t * | Pointer to the input data. |
| length | uint32_t | Number of bytes to process. |
| seed | uint32_t | Initial CRC value. |

### scale(float, float)

```cpp
float fixtures::scale(float value, float factor)
```

Scales a value by an optional factor.

| Parameter | Type | Description |
| --- | --- | --- |
| value | float | The input value. |
| factor | float | Multiplier to apply (default: 1.5). |

### SampleStatus

Sample status values for testing enum documentation.

| Value | Description |
| --- | --- |
| Active | The operation is active. |
| Pending | The operation is pending. |
| Failed | The operation has failed. |
