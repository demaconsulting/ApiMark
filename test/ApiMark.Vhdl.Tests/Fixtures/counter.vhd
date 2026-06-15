LIBRARY ieee;
USE ieee.std_logic_1164.ALL;
USE ieee.numeric_std.ALL;

--! @brief Configurable synchronous counter entity.
--!
--! A simple up-counter that resets synchronously and wraps at a
--! configurable maximum count value.
ENTITY counter IS
    GENERIC (
        DATA_WIDTH : INTEGER := 8;  --! Width of the counter data bus in bits
        MAX_COUNT  : INTEGER := 255 --! Maximum count value before wrap-around
    );
    PORT (
        clk   : IN  STD_LOGIC;                                   --! Rising-edge clock input
        rst   : IN  STD_LOGIC;                                   --! Synchronous reset, active high
        en    : IN  STD_LOGIC;                                   --! Count enable
        count : OUT STD_LOGIC_VECTOR(DATA_WIDTH - 1 DOWNTO 0);   --! Current count value
        wrap  : OUT STD_LOGIC                                    --! Asserted when counter wraps to zero
    );
END ENTITY counter;

--! @brief Behavioral architecture for the counter entity.
ARCHITECTURE behavioral OF counter IS
BEGIN
END ARCHITECTURE behavioral;
