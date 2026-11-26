--! @brief Synchronous binary counter.
ENTITY counter IS
    GENERIC (
        WIDTH : INTEGER := 8 --! Width of the count in bits
    );
    PORT (
        clk : IN STD_LOGIC; --! Rising-edge clock
        rst : IN STD_LOGIC  --! Synchronous reset, active high
    );
END ENTITY counter;
