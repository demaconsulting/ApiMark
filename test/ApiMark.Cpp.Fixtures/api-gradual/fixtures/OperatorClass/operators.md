# Operators

```cpp
// fixtures::OperatorClass
#include "fixtures/OperatorClass.h"
```

Operator overloads for OperatorClass.

## operator+(const OperatorClass &)

```cpp
// fixtures::OperatorClass::operator+
OperatorClass operator+(const OperatorClass & rhs)
```

Adds two OperatorClass values.

### Parameters

| Parameter | Type | Description |
| --- | --- | --- |
| rhs | const [OperatorClass](../OperatorClass.md) & | The right-hand side operand. |

### Returns

The sum of the two values.

## operator==(const OperatorClass &)

```cpp
// fixtures::OperatorClass::operator==
bool operator==(const OperatorClass & rhs)
```

Compares two OperatorClass values for equality.

### Parameters

| Parameter | Type | Description |
| --- | --- | --- |
| rhs | const [OperatorClass](../OperatorClass.md) & | The right-hand side operand. |

### Returns

True when both values are equal.
