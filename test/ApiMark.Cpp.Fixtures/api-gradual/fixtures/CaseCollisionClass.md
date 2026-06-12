# CaseCollisionClass

```cpp
// fixtures::CaseCollisionClass
#include "fixtures/CaseCollisionClass.h"
```

A class demonstrating case-insensitive member name collision.

Used to verify that the generator combines members whose names differ only in case (e.g. method Name() and field name) onto a single shared Markdown page so that no two output files collide on case-insensitive file systems.

## Methods

| Member | Returns | Description |
| --- | --- | --- |
| [Name()](CaseCollisionClass/name.md) | std::string | Gets the formatted name. |

## Fields

| Member | Type | Description |
| --- | --- | --- |
| [name](CaseCollisionClass/name.md) | std::string | The backing name field. |
