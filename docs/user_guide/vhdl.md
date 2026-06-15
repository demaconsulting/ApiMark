# VHDL Documentation

<!-- cspell:ignore INOUT -->

## Prerequisites

VHDL documentation generation has no additional prerequisites beyond the .NET SDK.
Parsing is done in-process using the ANTLR4 vhdl2008 grammar — no external tools or
runtimes are required. There is no MSBuild integration for VHDL projects; use the
`apimark vhdl` CLI command directly.

## CLI Options

```text
apimark vhdl [options]
```

| Option | Description |
| --- | --- |
| `--source <glob>` | Source glob pattern — repeatable, prefix with `!` to exclude (required) |
| `--output <dir>` | Output directory for Markdown files (required) |
| `--format <value>` | Output format: `gradual` (file-per-entity) or `single-file` (single `api.md`) (default: `gradual`) |
| `--library-name <name>` | Library name used as the top-level heading (default: output directory name) |
| `--library-description <d>` | Optional description for the library `api.md` introduction |

## File Discovery

### `--source <pattern>` (repeatable, at least one required)

Glob patterns that select which VHDL source files are documented.

- Patterns may be **absolute** (e.g. `/data/hdl/**/*.vhd`, `C:\projects\hdl\**\*`)
  or **relative** (resolved from the current working directory).
- Patterns ending with a bare `*` (no extension) automatically select both `.vhd` and
  `.vhdl` files.
- Patterns ending with a specific extension (e.g. `**/*.vhd`) select only that extension.
- Prefix a pattern with `!` to exclude matching files. All include patterns are applied
  first to build the file set; exclusion patterns then remove files from the result.

### Examples

```text
# Include all .vhd and .vhdl files under src/
apimark vhdl --source "src/**/*" --output docs/api

# Include .vhd files only, exclude testbenches
apimark vhdl \
  --source "src/**/*.vhd" \
  --source "!src/tb/**" \
  --output docs/api

# Absolute path
apimark vhdl \
  --source "C:\projects\hdl\src\**\*" \
  --output docs/api
```

If the evaluated patterns match no `.vhd` or `.vhdl` files, ApiMark writes a diagnostic
error to standard error and produces no output files.

## Documented Constructs

ApiMark parses VHDL source files and produces documentation for the following
constructs when they carry `--!` doc comments.

### Entities

Each entity gets its own page containing:

- A generics table (name, type, default value, description)
- A ports table (name, direction, type, description)
- An inline list of all architectures declared for that entity

### Packages

Each package gets its own page. The following member kinds are documented:

- Type declarations
- Constant declarations
- Component declarations
- Subprograms (procedures and functions) — subprograms get their own detail pages in
  gradual-disclosure format

## Doc Comments

ApiMark reads `--!` prefix block comments placed immediately before the construct they
describe. Trailing inline `--!` comments are also supported for ports, generics, and
component ports.

| Tag | Purpose |
| --- | --- |
| `@brief` | One-line description shown in index tables and at the top of detail pages |
| `@param <name>` | Description for a subprogram parameter, port, or generic |
| `@return` | Description for a function return value |

If `@brief` is absent, the first non-empty line of the doc comment block becomes the
summary. Missing descriptions render as *No description provided.* in the output.

For subprogram parameters, object-class keywords (`SIGNAL`, `VARIABLE`, `CONSTANT`,
`FILE`) are stripped from display and direction keywords (`IN`, `OUT`, `INOUT`,
`BUFFER`) are prepended to the type name in the parameters table. Entity ports are
shown with separate Direction and Type columns.

### Entity Example

```vhdl
--! @brief Synchronous binary counter entity.
--!
--! Note that changes to maxcount_in should only be performed
--! when the counter is cleared.
ENTITY counter IS
    GENERIC (
        width : natural := 1 --! Width of the counter
    );
    PORT (
        clk_in : IN std_logic; --! Module clock
        rst_in : IN std_logic  --! Asynchronous reset
    );
END ENTITY counter;
```

### Subprogram Example

```vhdl
--! @brief Convert a std_logic_vector to natural.
--! @param v The vector to convert.
--! @return The natural value.
FUNCTION to_natural(v : IN STD_LOGIC_VECTOR) RETURN natural;
```

## Output Structure

ApiMark supports two output formats selectable via `--format`.

### Gradual Disclosure (default: `--format gradual`)

A hierarchy of Markdown files designed for incremental context loading.

| File | Description |
| --- | --- |
| `api.md` | Root index — lists all entities and packages with one-line summaries |
| `{entity-name}.md` | Entity page — generics table, ports table, and inline architecture list |
| `{package-name}.md` | Package page — types, constants, components, and subprogram index |
| `{package-name}/{subprogram-name}.md` | Subprogram detail page — parameters table, optional returns, and signature |

An AI agent can read the root index first, drill into the relevant entity or package
page, and then read the subprogram detail — consuming only as much context as the task
requires.

### Single File (`--format single-file`)

All content is written to a single `api.md` file using a flat heading hierarchy:

| Level | Content |
| --- | --- |
| H1 | Library name |
| H2 | Entity or package name |
| H3 | Member (subprogram, type, constant, component) with signature and description |
| H4 | Individual parameter (name, type, description) |

Single-file output is best suited for contexts where a complete, linear reference is
preferred over a navigable multi-file tree, such as attaching documentation to a chat
context window.
