# crc32

```cpp
// fixtures::crc32
#include "fixtures/DefaultParamFixtures.h"
uint32_t crc32(const uint8_t * data, uint32_t length, uint32_t seed = 0)
```

Computes the CRC-32 checksum of a buffer.

> **Note:** Result depends on the seed value; use seed=0 for a standard CRC-32.

## Parameters

| Parameter | Type | Description |
| --- | --- | --- |
| data | const uint8_t * | Pointer to the input data. |
| length | uint32_t | Number of bytes to process. |
| seed | uint32_t | Initial CRC value. |

## Returns

The computed CRC-32 checksum.
