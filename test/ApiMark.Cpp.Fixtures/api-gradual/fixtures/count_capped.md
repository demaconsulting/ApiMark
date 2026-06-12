# count_capped

```cpp
// fixtures::count_capped
#include "fixtures/DefaultParamFixtures.h"
int count_capped(int value, int max = -1)
```

Counts occurrences with an optional maximum cap.

## Parameters

| Parameter | Type | Description |
| --- | --- | --- |
| value | int | The value to count. |
| max | int | Maximum count before capping (default: no cap). |

## Returns

The capped count.
