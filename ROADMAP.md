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

**Proposed implementation**: Invoke `python` as a subprocess with a one-liner that
uses the built-in `ast` module — no native DLLs, no grammar, no separate helper
script to deploy:

```text
python -c "import ast, sys; print(ast.dump(ast.parse(open(sys.argv[1]).read()), indent=4))" module.py
```

The JSON-like AST dump is parsed by a new `ApiMark.Python` assembly using the same
pattern as `ApiMark.Cpp` with `clang -ast-dump=json` — a visitor walks the tree and
feeds the existing `IContext`/`IMarkdownWriterFactory` pipeline.

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
