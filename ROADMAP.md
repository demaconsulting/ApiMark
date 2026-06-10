# ApiMark Roadmap

This document captures planned future features in implementation order — each
phase builds on the foundation laid by the previous.

---

## Phase 1 — Configurable Output Formats

Allow users to choose between output strategies depending on their use case:

- **Gradual disclosure** *(current default)* — index page linking to per-namespace
  and per-type pages; optimized for AI agents that navigate progressively.
- **Single-file** — entire API rendered into one `api.md`; suitable for dropping
  directly into a system prompt, replacing a Doxygen HTML output, or archiving.

The current architecture couples symbol extraction and Markdown rendering tightly
inside each language generator. Supporting multiple output formats cleanly requires
separating these two concerns into a two-stage pipeline. This refactor is done
simultaneously across DotNet and Cpp — two working emitters against two existing
languages is the proof that the abstraction is correct.

### Stage 1 — Symbol Extraction (language-specific)

Each language `IApiGenerator` parses its source and populates its own
language-specific symbol tree. These trees are intentionally not unified — C# has
interfaces, generics, and XML doc comments; C++ has templates and preprocessor
defines; VHDL has entities, architectures, and port maps. Forcing them into a
common abstraction loses the fidelity needed for accurate documentation.

### Stage 2 — Document Rendering (language-specific, format-selectable)

Each language provides its own `IDocumentGenerator` implementation that understands
its symbol tree. The output *format* is then an orthogonal strategy injected into
that generator — it controls file layout decisions without touching symbol
interpretation:

| Format strategy | Behavior |
| --- | --- |
| `GradualDisclosureStrategy` | Current behavior — index → namespace → type pages |
| `SingleFileStrategy` | Entire tree in one `api.md`; heading levels offset by depth |
| *(future)* `DoxygenStyleStrategy` | Per-type files with full member detail, cross-links |

Because Stage 1 (source parsing) is the expensive step, the symbol tree can be
passed to **multiple format strategies in a single build** — each writing to its
own output directory with its own visibility filter — at negligible extra cost.
The symbol tree is populated at the broadest useful visibility (`All`); each
emitter then filters independently based on its configured audience. For example,
a `.csproj` could simultaneously produce internal developer docs (all members) and
a public NuGet consumer reference (public only):

```xml
<ItemGroup>
  <ApiMarkOutput Include="InternalDocs">
    <OutputDir>$(MSBuildProjectDirectory)\docs\api</OutputDir>
    <Format>single-file</Format>
    <Visibility>All</Visibility>
  </ApiMarkOutput>
  <ApiMarkOutput Include="PublicDocs">
    <OutputDir>$(MSBuildProjectDirectory)\api</OutputDir>
    <Format>gradual</Format>
    <Visibility>Public</Visibility>
  </ApiMarkOutput>
</ItemGroup>
```

This means visibility is no longer a global parse-time gate — it becomes a
per-output rendering concern. Adding a new audience (e.g. `PublicAndProtected`
for subclass authors) costs nothing in parse time and requires no additional
source analysis.

The existing scalar `ApiMarkOutputDir` / `--output` properties default to a single
gradual-disclosure output and remain fully backward-compatible. The `ApiMarkOutput`
item group is opt-in for multi-format scenarios.

**Definition of done**: Both DotNet and Cpp generate correct output under both
`GradualDisclosureStrategy` and `SingleFileStrategy`. New languages drop straight
into the proven two-stage architecture without any further restructuring.

---

## Phase 2 — VHDL Document Generation

Document the public contract of VHDL design units - entities, architectures,
packages, and components - from source files and associated Doxygen-style doc
comments. Internal signal and process details are excluded; this is documentation
for IP consumers, not implementors. Likely CLI-only given the niche toolchain.

The first greenfield two-stage implementation — validates that the Phase 1
architecture holds for a language with a fundamentally different symbol model.

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
the JSON output is consumed by `ApiMark.Vhdl` feeding the two-stage pipeline.
Requires `pip install hdlConvertor` on the host.

---

## Phase 3 — Python Document Generation

Document public classes, functions, and module-level constants from Python source.
Type annotations and docstrings (Google, NumPy, or reStructuredText style) are
the primary documentation source. Likely CLI-only.

**Proposed implementation**: Invoke `python` as a subprocess with a one-liner that
uses the built-in `ast` module - no native DLLs, no grammar, no separate helper
script to deploy:

```text
python -c "import ast, sys; print(ast.dump(ast.parse(open(sys.argv[1]).read()), indent=4))" module.py
```

The AST dump is parsed by a new `ApiMark.Python` assembly using the same subprocess
pattern as `ApiMark.Cpp` with `clang -ast-dump=json` — a visitor walks the tree and
feeds the two-stage pipeline.

---
