# ApiMark Roadmap

This document captures planned future features at a high level. Ordering reflects
anticipated demand, not commitment to schedule.

---

## VHDL Document Generation

Document the public contract of VHDL design units - entities, architectures,
packages, and components - from source files and associated Doxygen-style doc
comments. Internal signal and process details are excluded; this is documentation
for IP consumers, not implementors. Likely CLI-only given the niche toolchain.

**Proposed implementation - Option A (ANTLR4, pure managed)**: Parse VHDL source
using an [ANTLR4 VHDL grammar](https://github.com/antlr/grammars-v4) and a
`ParseTreeVisitor` walking entity declarations, port maps, and generic lists.
Doc comments (lines starting with `--!`) are harvested from the preceding comment
block. No external tool dependency; all processing stays in-process.

**Proposed implementation - Option B (Python + hdlConvertor, preferred)**: Invoke
`python` as a subprocess using [hdlConvertor](https://github.com/Nic30/hdlConvertor),
a battle-tested HDL parser that natively preserves Doxygen-style doc comments
attached to ports and generics - no comment scraping needed:

```text
python -c "from hdlConvertor import HdlConvertor; import json, sys; ..."  design.vhd
```

Consistent with the `clang -ast-dump=json` and Python `ast` subprocess patterns;
the JSON output is consumed by `ApiMark.Vhdl` feeding the existing
`IContext`/`IMarkdownWriterFactory` pipeline. Requires `pip install hdlConvertor`
on the host.

---

## Python Document Generation

Document public classes, functions, and module-level constants from Python source.
Type annotations and docstrings (Google, NumPy, or reStructuredText style) are
the primary documentation source. Likely CLI-only.

**Proposed implementation**: Invoke `python` as a subprocess with a one-liner that
uses the built-in `ast` module - no native DLLs, no grammar, no separate helper
script to deploy:

```text
python -c "import ast, sys; print(ast.dump(ast.parse(open(sys.argv[1]).read()), indent=4))" module.py
```

The JSON-like AST dump is parsed by a new `ApiMark.Python` assembly using the same
pattern as `ApiMark.Cpp` with `clang -ast-dump=json` - a visitor walks the tree and
feeds the existing `IContext`/`IMarkdownWriterFactory` pipeline.

---

## Configurable Output Formats

Allow users to choose between output strategies depending on their use case:

- **Gradual disclosure** *(current default)* - index page linking to per-namespace
  and per-type pages; optimized for AI agents that navigate progressively.
- **Single-file** - entire API rendered into one `api.md`; suitable for dropping
  directly into a system prompt, replacing a Doxygen HTML output, or archiving.

The current architecture couples symbol extraction and Markdown rendering tightly
inside each language generator. Supporting multiple output formats cleanly requires
separating these two concerns into a two-phase pipeline:

### Phase 1 — Symbol Extraction (language-specific)

Each language `IApiGenerator` implementation parses its source and populates a
shared, language-agnostic in-memory symbol tree:

```text
ApiLibrary
  ApiNamespace (one per namespace / package / VHDL library)
    ApiType     (class, struct, enum, interface, entity, component, module, ...)
      ApiMember (method, property, field, port, generic, constant, ...)
        DocComment (summary, parameters, returns, remarks — Doxygen or XML)
```

The tree carries only semantic information — no formatting decisions are made here.
This is analogous to a compiler front-end producing an IR: the same tree can be
consumed by any number of back-ends.

### Phase 2 — Document Rendering (format-specific)

An `IDocumentGenerator` receives the populated `ApiLibrary`, the `IContext`, and
an `IMarkdownWriterFactory`, and is solely responsible for deciding how to lay out
the tree as Markdown files:

| Implementation | Behaviour |
| --- | --- |
| `GradualDisclosureDocumentGenerator` | Current behaviour — index → namespace → type pages |
| `SingleFileDocumentGenerator` | Entire tree in one `api.md`; heading levels offset by depth |
| *(future)* `DoxygenStyleDocumentGenerator` | Per-type files with full member detail, cross-links |

The `IDocumentGenerator` to instantiate is selected by an `--output-format` CLI
option (`gradual` \| `single-file`) and a corresponding MSBuild property
`ApiMarkOutputFormat`. Language generators need no changes — they only populate
the symbol tree. New output formats are added by implementing `IDocumentGenerator`
without touching any language-specific code.

This separation also enables **multi-language libraries**: a future `apimark multi`
command could merge symbol trees from several generators (e.g. a C# core + C++
extension layer) and pass the combined `ApiLibrary` to a single document renderer.

---
