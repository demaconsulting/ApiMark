LIBRARY ieee;
USE ieee.std_logic_1164.ALL;

-- Entity with no doc comment, and generics/ports each missing their own doc comment,
-- used to exercise every VHDL "undocumented" violation kind for entities.
ENTITY undocumented_entity IS
    GENERIC (
        WIDTH : INTEGER := 8
    );
    PORT (
        clk : IN  STD_LOGIC;
        y   : OUT STD_LOGIC
    );
END ENTITY undocumented_entity;

--! @brief Architecture with an internal signal that is never parsed into the AST model
--! and therefore is never checked for documentation — this file exists specifically to
--! prove that scope boundary (see ApiMark.Vhdl.DocumentationCoverageChecker remarks).
ARCHITECTURE behavioral OF undocumented_entity IS
    -- This internal signal is intentionally undocumented; VhdlAstParser does not parse
    -- architecture-internal signal declarations at all today, so it can never appear as
    -- a violation regardless of documentation-coverage enforcement settings.
    SIGNAL internal_undocumented_signal : STD_LOGIC;
BEGIN
    y <= clk;
END ARCHITECTURE behavioral;

-- Package with no doc comment, and each exported declaration kind missing its own doc
-- comment, used to exercise every VHDL "undocumented" violation kind for packages.
PACKAGE undocumented_package IS

    SUBTYPE undocumented_type_t IS STD_LOGIC_VECTOR(7 DOWNTO 0);

    CONSTANT UNDOCUMENTED_CONSTANT : INTEGER := 0;

    COMPONENT undocumented_component IS
        PORT (
            clk : IN STD_LOGIC
        );
    END COMPONENT undocumented_component;

    FUNCTION undocumented_function(v : STD_LOGIC_VECTOR) RETURN NATURAL;

END PACKAGE undocumented_package;
