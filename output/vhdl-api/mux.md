# mux

2-input multiplexer entity.

Selects one of two input signals based on a select line.

## Ports

| Name | Direction | Type | Description |
| --- | --- | --- | --- |
| a | in | STD_LOGIC | First data input |
| b | in | STD_LOGIC | Second data input |
| sel | in | STD_LOGIC | Select line: 0 selects a, 1 selects b |
| y | out | STD_LOGIC | Selected output |

## Architectures

**behavioral**: Behavioral architecture using a process statement.

**dataflow**: Dataflow architecture using concurrent signal assignment.
