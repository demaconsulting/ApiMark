# ApiMark Roadmap

This document captures planned future features in implementation order — each
phase builds on the foundation laid by the previous.

---

## Completed

- **Two-stage pipeline** — symbol extraction separated from document rendering;
  gradual-disclosure and single-file formats proven across C#/.NET and C++.
- **VHDL support** — entities (generics, ports, inline architectures), packages
  (types, subtypes, constants, components, subprograms with parameters and returns)
  via ANTLR4 vhdl2008 grammar and `--!` Doxygen-style doc comments. CLI-only.

---

## Phase 3 — Python Document Generation

Document public classes, functions, and module-level constants from Python source.
Type annotations and docstrings (Google, NumPy, or reStructuredText style) are
the primary documentation source. CLI-only.

**Proposed implementation**: Invoke `python` as a subprocess with a one-liner that
uses the built-in `ast` module — no native DLLs, no grammar, no separate helper
script to deploy:

```text
python -c "import ast, sys; print(ast.dump(ast.parse(open(sys.argv[1]).read()), indent=4))" module.py
```

The AST dump is parsed by a new `ApiMark.Python` assembly using the same subprocess
pattern as `ApiMark.Cpp` with `clang -ast-dump=json` — a visitor walks the tree and
feeds the two-stage pipeline.

---

## Phase 4 — VHDL MSBuild Integration

Extend the MSBuild package to support VHDL projects (`.vhdpfile` or conventional
directory layouts). Mirrors the C++ integration: source globs and exclusion patterns
are configured via MSBuild properties; documentation is generated automatically
after every build.

Proposed MSBuild properties: `ApiMarkVhdlSource` (semicolon-separated glob
patterns, same gitignore-style semantics as `--source`), `ApiMarkLibraryName`,
`ApiMarkLibraryDescription`, and the existing `ApiMarkOutputDir` / `ApiMarkFormat`.

---

## Phase 5 — VHDL Deeper Type Documentation

Extend the VHDL model with richer type information currently captured but not
emitted:

- **Record type fields** — emit field name, type, and doc comment inline on the
  type paragraph rather than showing only the type definition string.
- **Enumeration literals** — list enumeration values with their doc comments.
- **Protected types** — document protected type members (subprograms exposed as
  part of the type's interface).
- **VHDL-2008 generic packages** — document generic-mapped package instantiations
  as a distinct construct.

---

## Phase 6 — SystemVerilog / Verilog Document Generation

Document modules, interfaces, and packages from SystemVerilog (or Verilog) source.
An ANTLR4 SystemVerilog grammar exists in the grammars-v4 repository — the same
in-process approach used for VHDL applies directly. Doc comments use the `//!` or
`/** */` Doxygen convention common in SystemVerilog codebases.

Complements VHDL support for mixed-HDL projects.

---

## Phase 7 — Rust Document Generation

Document public items (structs, enums, traits, functions, type aliases) from Rust
source. `rustdoc --output-format json` produces a stable, richly typed JSON tree —
analogous to `clang -ast-dump=json` for C++. A new `ApiMark.Rust` assembly consumes
the JSON and feeds the two-stage pipeline. Requires `rustdoc` (ships with the
standard Rust toolchain).

---

## Phase 8 — Cross-Language Type Linking

When a documented type in one language references a type defined in another
(e.g., a C++ wrapper header that exposes a VHDL register map, or a Python binding
that wraps a C++ class), emit hyperlinks between the two documentation trees rather
than plain text type names.

Requires a shared external-types registry (already established in the C++ emitter)
extended to accept contributions from multiple language generators in a single
multi-language run.

---
