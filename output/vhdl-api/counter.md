# counter

Configurable synchronous counter entity.

A simple up-counter that resets synchronously and wraps at a
configurable maximum count value.

## Generics

| Name | Type | Default | Description |
| --- | --- | --- | --- |
| DATA_WIDTH | INTEGER | 8 | Width of the counter data bus in bits |
| MAX_COUNT | INTEGER | 255 | Maximum count value before wrap-around |

## Ports

| Name | Direction | Type | Description |
| --- | --- | --- | --- |
| count | out | STD_LOGIC_VECTOR(DATA_WIDTH - 1 DOWNTO 0) | Current count value |
| wrap | out | STD_LOGIC | Asserted when counter wraps to zero |

## Architectures

**behavioral**: Behavioral architecture for the counter entity.
