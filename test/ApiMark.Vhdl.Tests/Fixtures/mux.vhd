LIBRARY ieee;
USE ieee.std_logic_1164.ALL;

--! @brief 2-input multiplexer entity.
--!
--! Selects one of two input signals based on a select line.
ENTITY mux IS
    PORT (
        a   : IN  STD_LOGIC; --! First data input
        b   : IN  STD_LOGIC; --! Second data input
        sel : IN  STD_LOGIC; --! Select line: 0 selects a, 1 selects b
        y   : OUT STD_LOGIC  --! Selected output
    );
END ENTITY mux;

--! @brief Behavioral architecture using a process statement.
ARCHITECTURE behavioral OF mux IS
BEGIN
    process(a, b, sel)
    BEGIN
        IF sel = '0' THEN
            y <= a;
        ELSE
            y <= b;
        END IF;
    END process;
END ARCHITECTURE behavioral;

--! @brief Dataflow architecture using concurrent signal assignment.
ARCHITECTURE dataflow OF mux IS
BEGIN
    y <= a WHEN sel = '0' ELSE b;
END ARCHITECTURE dataflow;
