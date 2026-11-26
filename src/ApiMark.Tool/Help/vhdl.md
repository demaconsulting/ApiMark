---
title: VHDL libraries
summary: Document VHDL entities and packages from their --! comments.
aliases:
  - hdl
  - vhd
tags:
  - reference
order: 5
see-also:
  - output
  - doc-coverage
examples:
  - name: counter
    kind: source
    language: vhdl
    save-as: counter.vhd
    description: An entity with --! comments.
    content: |
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
  - name: generate
    kind: command
    language: shell
    description: Write the pages of every .vhd file in the folder to api.
    content: '{{tool}} vhdl --source "*.vhd" --output api'
    expect: 'counter.vhd'
  - name: single-file
    kind: command
    language: shell
    description: Write one api.md with the heading "Counters API Reference".
    content: '{{tool}} vhdl --source "*.vhd" --output api --format single-file --library-name Counters'
  - name: no-match
    kind: command
    language: shell
    description: Globs that match no file are an error. Exits 1.
    content: '{{tool}} vhdl --source "rtl/**/*.vhd" --output api'
    exit: 1
    expect: 'Error: no .vhd or .vhdl files matched the --source patterns.'
---
`{{tool}} vhdl` parses VHDL source files itself; it needs no simulator or
other tool. It documents entities with their generics, ports, and
architectures, and packages with their types, constants, components, and
subprograms.

| Option | Meaning |
| --- | --- |
| `--source <glob>` | The files to read; `!` excludes. Required. |
| `--output <dir>` | The folder for the pages. Required. |
| `--library-name <name>` | The top heading; default: the output folder name. |
| `--library-description <d>` | The text at the top of `api.md`. |

`--source` may be repeated. Globs are relative to the current folder, and a
`!` pattern removes files from those that the other patterns select. A
glob ending in a bare `*` selects `.vhd` and `.vhdl` files. Quote globs
so that the shell does not expand them. The tool prints
`Parsing <file>` for each file ([[#generate]]), and fails when no file
matches ([[#no-match]]).

**Comments.** Write `--!` lines just before an entity, package, or
subprogram, as [[#counter]] shows. `@brief` gives the summary (else the
first line), `@param <name>` describes a parameter of a function or
procedure, and `@return` the result of a function. A port or generic is
described only by a `--!` comment at the end of its line; `@param` does
not apply to it.

There is no MSBuild support for VHDL; run the tool from a script or CI.
