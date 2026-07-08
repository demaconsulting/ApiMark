# Introduction

ApiMark generates compact, AI-friendly API reference documentation in Markdown from
source code and its associated metadata (XML doc comments, header files, docstrings,
etc.). The output is designed for gradual disclosure: an AI can read a lightweight
index, drill into a namespace summary, and then read a full type page — consuming only
as much context as the task requires. The project is structured as six independent
systems: ApiMark.Core (shared contracts and file-path helpers), ApiMark.DotNet
(C#/.NET language generator), ApiMark.Cpp (C++ language generator), ApiMark.Vhdl
(VHDL language generator), ApiMark.MSBuild
(unified MSBuild task that spawns ApiMark.Tool out-of-process), and ApiMark.Tool (the
.NET executable invoked by ApiMarkTask and directly by users or CI pipelines).
Several OTS items provide library support: Mono.Cecil for the DotNet system, clang (via
`clang -ast-dump=json`) for the Cpp system, DemaConsulting.TestResults for the SelfTest
subsystem, Antlr4.Runtime.Standard with an ANTLR4 vhdl2008 grammar for the Vhdl system, and
Microsoft.Extensions.FileSystemGlobbing for glob-based file discovery in ApiMarkCore.
An archived OTS item, cpp-ast-net, is retained for historical reference.

## Purpose

This document defines the design for each software item in ApiMark — full architectural
and detailed design for local items (systems and units), and integration/usage design
for OTS software items. A reviewer should be able to understand how each item satisfies
its requirements without reading source code.

## Scope

Local items cover system, subsystem, and unit design for: ApiMarkCore, ApiMarkDotNet,
ApiMarkCpp, ApiMarkVhdl, ApiMarkMsbuild, and ApiMarkTool.

OTS items:

- **Mono.Cecil**: integration and usage design.
- **clang**: integration and usage design (via `clang -ast-dump=json`).
- **DemaConsulting.TestResults**: integration and usage design.
- **Antlr4.Runtime.Standard / ANTLR4 vhdl2008 grammar**: integration and usage design.
- **Microsoft.Extensions.FileSystemGlobbing**: integration and usage design.
- **cpp-ast-net**: integration and usage design (archived; retained for historical reference).

Out of scope: design documents are not produced for test projects or build pipeline CI
configuration; the internal design of OTS items is also excluded.

## Software Structure

The software structure is modeled in SysML2 under `docs/sysml2/` and rendered to the
diagram below by SysML2Tools as part of the build pipeline. AI agents should query the
SysML2 model directly (see the `sysml2tools-query` skill) rather than parsing this
diagram or the prose below.

![Software Structure](SoftwareStructureView.svg)

## Folder Layout

```text
src/
├── ApiMark.Core/        - shared contracts, file-path helpers, and Markdown writer implementations
├── ApiMark.DotNet/      - C#/.NET language generator
├── ApiMark.Cpp/         - C++ language generator
│   └── CppAst/          - C++ AST data model and clang parser
├── ApiMark.Vhdl/        - VHDL language generator
│   └── VhdlAst/         - VHDL AST data model and ANTLR4 parser
├── ApiMark.MSBuild/     - MSBuild task that spawns ApiMark.Tool out-of-process
└── ApiMark.Tool/        - CLI entry point
    ├── Cli/             - command-line argument parsing and context construction
    └── SelfTest/        - self-validation subsystem
```

## Companion Artifact Structure

Each local software item has corresponding artifacts in parallel directory trees:

- Requirements: `docs/reqstream/{system-name}.yaml`,
  `docs/reqstream/{system-name}/{subsystem}/{item}.yaml`
- Design: `docs/design/{system-name}.md`,
  `docs/design/{system-name}/{subsystem}/{item}.md`
- Verification: `docs/verification/{system-name}.md`,
  `docs/verification/{system-name}/{subsystem}/{item}.md`
- Source: `src/ApiMark.{SystemName}/{Subsystem}/{Item}.cs`
- Tests: `test/ApiMark.{SystemName}.Tests/{Subsystem}/{Item}Tests.cs`

Fixtures used by generator tests live in `test/ApiMark.DotNet.Fixtures/` and
`test/ApiMark.Cpp.Fixtures/`.

OTS items have integration/usage design documentation parallel to system folders:

- Requirements: `docs/reqstream/ots/{ots-name}.yaml`
- Design: `docs/design/ots/{ots-name}.md`
- Verification: `docs/verification/ots/{ots-name}.md`

Review-sets: defined in `.reviewmark.yaml`

## References

N/A - this document set is derived from repository source materials and does not
introduce external specifications or standards requiring citation.
