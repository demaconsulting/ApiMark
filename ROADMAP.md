# ApiMark Roadmap

This document captures planned future features at a high level. Ordering reflects
anticipated demand, not commitment to schedule.

---

## VHDL Document Generation

Document the public contract of VHDL design units — entities, architectures,
packages, and components — from source files and associated Doxygen-style doc
comments. Internal signal and process details are excluded; this is documentation
for IP consumers, not implementors. Likely CLI-only given the niche toolchain.

**Proposed implementation**: Parse VHDL source using an
[ANTLR4 VHDL grammar](https://github.com/antlr/grammars-v4) and a
`ParseTreeVisitor` walking entity declarations, port maps, and generic lists.
Doc comments (lines starting with `--!`) are harvested from the preceding comment
block. The visitor emits the same `IContext`/`IMarkdownWriterFactory` pipeline as
the existing generators — no new output infrastructure needed.

---

## Python Document Generation

Document public classes, functions, and module-level constants from Python source.
Type annotations and docstrings (Google, NumPy, or reStructuredText style) are
the primary documentation source. Likely CLI-only.

**Proposed implementation**: Use Python's built-in `ast` module (via a small
Python helper invoked as a subprocess, similar to `clang -ast-dump=json`) rather
than ANTLR4 — the language ships its own reliable parser, making a grammar
unnecessary. The helper emits a JSON AST that a new `ApiMark.Python` assembly
consumes via the same visitor pattern used by `ApiMark.DotNet` and `ApiMark.Cpp`.

---

## Configurable Output Formats

Allow users to choose between output strategies depending on their use case:

- **Gradual disclosure** *(current default)* — index page linking to per-namespace
  and per-type pages; optimized for AI agents that navigate progressively.
- **Single-file** — entire API rendered into one `api.md`; suitable for dropping
  directly into a system prompt, replacing a Doxygen HTML output, or archiving.

**Proposed implementation**: Introduce an `--output-format` CLI option
(`gradual` | `single-file`) and a corresponding MSBuild property
`ApiMarkOutputFormat`. The format selection is threaded through `IContext` and
controls how `IMarkdownWriterFactory` is instantiated — single-file uses a
multiplexing writer that appends all output to one stream with injected heading
level offsets, leaving generator logic unchanged.

---
