LIBRARY ieee;
USE ieee.std_logic_1164.ALL;
USE ieee.numeric_std.ALL;

--! @brief Common type definitions and utilities for the design.
PACKAGE common_types IS

    --! 32-bit data bus type.
    TYPE data_bus_t IS STD_LOGIC_VECTOR(31 DOWNTO 0);

    --! 4-bit nibble type.
    SUBTYPE nibble_t IS STD_LOGIC_VECTOR(3 DOWNTO 0);

    --! Width of the data bus in bits.
    CONSTANT DATA_WIDTH : INTEGER := 32;

    --! Reset value for counters.
    CONSTANT RESET_VALUE : INTEGER := 0;

    --! Counter component declaration.
    COMPONENT counter IS
        GENERIC (
            DATA_WIDTH : INTEGER := 8;
            MAX_COUNT  : INTEGER := 255
        );
        PORT (
            clk   : IN  STD_LOGIC;
            rst   : IN  STD_LOGIC;
            en    : IN  STD_LOGIC;
            count : OUT STD_LOGIC_VECTOR(DATA_WIDTH - 1 DOWNTO 0);
            wrap  : OUT STD_LOGIC
        );
    END COMPONENT counter;

    --! Converts a std_logic_vector to a natural integer.
    FUNCTION to_natural(v : STD_LOGIC_VECTOR) RETURN NATURAL;

    --! Resets all bits of a std_logic_vector output to zero.
    PROCEDURE clear_vector(SIGNAL v : OUT STD_LOGIC_VECTOR);

END PACKAGE common_types;
