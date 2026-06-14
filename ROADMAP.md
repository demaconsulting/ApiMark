# ApiMark Roadmap

<!-- cspell:ignore Verilog rustdoc -->

This document captures ideas for future features.

---

## Python Document Generation

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

## VHDL Deeper Type Documentation

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

## SystemVerilog / Verilog Document Generation

Document modules, interfaces, and packages from SystemVerilog (or Verilog) source.
An ANTLR4 SystemVerilog grammar exists in the grammars-v4 repository — the same
in-process approach used for VHDL applies directly. Doc comments use the `//!` or
`/** */` Doxygen convention common in SystemVerilog codebases.

Complements VHDL support for mixed-HDL projects.

---

## Rust Document Generation

Document public items (structs, enums, traits, functions, type aliases) from Rust
source. `rustdoc --output-format json` produces a stable, richly typed JSON tree —
analogous to `clang -ast-dump=json` for C++. A new `ApiMark.Rust` assembly consumes
the JSON and feeds the two-stage pipeline. Requires `rustdoc` (ships with the
standard Rust toolchain).

---
