# Operators

```cpp
// fixtures::operator<<
#include "fixtures/OperatorClass.h"
```

Operator overloads in the fixtures namespace.

## operator<<(std::ostream &, const OperatorClass &)

```cpp
// fixtures::operator<<
#include "fixtures/OperatorClass.h"
std::ostream & operator<<(std::ostream & os, const OperatorClass & obj)
```

Stream insertion operator for OperatorClass.

### Parameters

| Parameter | Type | Description |
| --- | --- | --- |
| os | std::ostream & | The output stream. |
| obj | const [OperatorClass](OperatorClass.md) & | The value to insert into the stream. |

### Returns

The output stream after insertion.
