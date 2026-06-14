# common_types

Common type definitions and utilities for the design.

## Types

| Name | Definition | Description |
| --- | --- | --- |
| data_bus_t | STD_LOGIC_VECTOR(31 DOWNTO 0) | 32-bit data bus type. |
| nibble_t | STD_LOGIC_VECTOR(3 DOWNTO 0) | 4-bit nibble type. |

## Constants

| Name | Type | Value | Description |
| --- | --- | --- | --- |
| DATA_WIDTH | INTEGER | 32 | *No description provided.* |
| RESET_VALUE | INTEGER | 0 | *No description provided.* |

## Components

| Name | Description |
| --- | --- |
| counter | Counter component declaration. |

## Subprograms

| Name | Kind | Signature | Description |
| --- | --- | --- | --- |
| to_natural | Function | FUNCTION to_natural(v : STD_LOGIC_VECTOR) RETURN NATURAL | Converts a std_logic_vector to a natural integer. |
| clear_vector | Procedure | PROCEDURE clear_vector(SIGNAL v : OUT STD_LOGIC_VECTOR) | Resets all bits of a std_logic_vector output to zero. |
