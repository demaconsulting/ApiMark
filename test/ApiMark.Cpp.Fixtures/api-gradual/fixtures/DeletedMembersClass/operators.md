# Operators

```cpp
// fixtures::DeletedMembersClass
#include "fixtures/DeletedMembersClass.h"
```

Operator overloads for DeletedMembersClass.

## operator=(const DeletedMembersClass &)

```cpp
// fixtures::DeletedMembersClass::operator=
DeletedMembersClass & operator=(const DeletedMembersClass & other) = delete
```

Deleted copy-assignment operator — this type is not copyable.

### Parameters

| Parameter | Type | Description |
| --- | --- | --- |
| other | const [DeletedMembersClass](../DeletedMembersClass.md) & | The instance that would have been assigned from. |

### Returns

Reference to this instance.
