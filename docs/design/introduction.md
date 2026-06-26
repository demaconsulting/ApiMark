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
subsystem, Antlr4.Runtime.Standard with an ANTLR4 VHDL grammar for the Vhdl system, and
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
- **Antlr4.Runtime.Standard / ANTLR4 VHDL grammar**: integration and usage design.
- **Microsoft.Extensions.FileSystemGlobbing**: integration and usage design.
- **cpp-ast-net**: integration and usage design (archived; retained for historical reference).

Out of scope: design documents are not produced for test projects or build pipeline CI
configuration; the internal design of OTS items is also excluded.

## Software Structure

```text
ApiMarkCore (System)
├── IApiGenerator (Unit)
├── IApiEmitter (Unit)
├── EmitConfig + OutputFormat (Unit)
├── IContext (Unit)
├── IMarkdownWriterFactory (Unit)
├── FileMarkdownWriterFactory (Unit)
├── IMarkdownWriter (Unit)
├── FileMarkdownWriter (Unit)
├── PathHelpers (Unit)
└── GlobFileCollector (Unit)

ApiMarkDotNet (System)
├── DotNetGenerator (Unit)
├── DotNetAstModel (Unit)
├── DotNetEmitter (Unit)
├── DotNetEmitterGradualDisclosure (Unit)
├── DotNetEmitterSingleFile (Unit)
├── TypeLinkResolver (Unit)
├── XmlDocReader (Unit)
└── TypeNameSimplifier (Unit)

ApiMarkCpp (System)
├── CppGenerator (Unit)
├── CppAstModel (Unit)
├── ClangAstParser (Unit)
├── CppEmitter (Unit)
├── CppEmitterGradualDisclosure (Unit)
├── CppEmitterSingleFile (Unit)
└── CppTypeLinkResolver (Unit)

ApiMarkVhdl (System)
├── VhdlGenerator (Unit)
├── VhdlAstModel (Unit)
├── VhdlAstParser (Unit)
├── VhdlEmitter (Unit)
├── VhdlEmitterGradualDisclosure (Unit)
└── VhdlEmitterSingleFile (Unit)

ApiMarkMsbuild (System)
└── ApiMarkTask (Unit)

ApiMarkTool (System)
├── Cli (Subsystem)
│   └── Context (Unit)
├── SelfTest (Subsystem)
│   └── Validation (Unit)
└── Program (Unit)

OTS Dependencies:
├── Mono.Cecil (OTS)
├── DemaConsulting.TestResults (OTS)
├── clang -ast-dump=json (OTS)
├── Antlr4.Runtime.Standard / ANTLR4 VHDL grammar (OTS)
├── Microsoft.Extensions.FileSystemGlobbing (OTS)
└── cpp-ast-net (OTS) [archived]
```

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

- Requirements: `docs/reqstream/api-mark-core.yaml`, `docs/reqstream/api-mark-core/{item}.yaml`,  `docs/reqstream/api-mark-dot-net.yaml`,
  `docs/reqstream/api-mark-dot-net/dot-net-generator.yaml`,
  `docs/reqstream/api-mark-dot-net/type-name-simplifier.yaml`,
  `docs/reqstream/api-mark-dot-net/dot-net-ast-model.yaml`,
  `docs/reqstream/api-mark-dot-net/dot-net-emitter.yaml`,
  `docs/reqstream/api-mark-dot-net/dot-net-emitter-gradual-disclosure.yaml`,
  `docs/reqstream/api-mark-dot-net/dot-net-emitter-single-file.yaml`,
  `docs/reqstream/api-mark-dot-net/type-link-resolver.yaml`,
  `docs/reqstream/api-mark-dot-net/xml-doc-reader.yaml`,
  `docs/reqstream/api-mark-cpp.yaml`,
  `docs/reqstream/api-mark-cpp/cpp-generator.yaml`,
  `docs/reqstream/api-mark-cpp/cpp-ast-model.yaml`,
  `docs/reqstream/api-mark-cpp/clang-ast-parser.yaml`,
  `docs/reqstream/api-mark-cpp/cpp-emitter.yaml`,
  `docs/reqstream/api-mark-cpp/cpp-emitter-gradual-disclosure.yaml`,
  `docs/reqstream/api-mark-cpp/cpp-emitter-single-file.yaml`,
  `docs/reqstream/api-mark-cpp/cpp-type-link-resolver.yaml`,
  `docs/reqstream/api-mark-vhdl.yaml`,
  `docs/reqstream/api-mark-vhdl/vhdl-generator.yaml`,
  `docs/reqstream/api-mark-vhdl/vhdl-ast-model.yaml`,
  `docs/reqstream/api-mark-vhdl/vhdl-ast-parser.yaml`,
  `docs/reqstream/api-mark-vhdl/vhdl-emitter.yaml`,
  `docs/reqstream/api-mark-vhdl/vhdl-emitter-gradual-disclosure.yaml`,
  `docs/reqstream/api-mark-vhdl/vhdl-emitter-single-file.yaml`,
  `docs/reqstream/api-mark-msbuild.yaml`, `docs/reqstream/api-mark-msbuild/{item}.yaml`,
  `docs/reqstream/api-mark-tool.yaml`, `docs/reqstream/api-mark-tool/{item}.yaml`
- Design: `docs/design/api-mark-core.md`, `docs/design/api-mark-core/{item}.md`,
  `docs/design/api-mark-dot-net.md`,
  `docs/design/api-mark-dot-net/dot-net-generator.md`,
  `docs/design/api-mark-dot-net/type-name-simplifier.md`,
  `docs/design/api-mark-dot-net/dot-net-ast-model.md`,
  `docs/design/api-mark-dot-net/dot-net-emitter.md`,
  `docs/design/api-mark-dot-net/dot-net-emitter-gradual-disclosure.md`,
  `docs/design/api-mark-dot-net/dot-net-emitter-single-file.md`,
  `docs/design/api-mark-dot-net/type-link-resolver.md`,
  `docs/design/api-mark-dot-net/xml-doc-reader.md`,
  `docs/design/api-mark-cpp.md`,
  `docs/design/api-mark-cpp/cpp-generator.md`,
  `docs/design/api-mark-cpp/cpp-ast-model.md`,
  `docs/design/api-mark-cpp/clang-ast-parser.md`,
  `docs/design/api-mark-cpp/cpp-emitter.md`,
  `docs/design/api-mark-cpp/cpp-emitter-gradual-disclosure.md`,
  `docs/design/api-mark-cpp/cpp-emitter-single-file.md`,
  `docs/design/api-mark-cpp/cpp-type-link-resolver.md`,
  `docs/design/api-mark-vhdl.md`,
  `docs/design/api-mark-vhdl/vhdl-generator.md`,
  `docs/design/api-mark-vhdl/vhdl-ast-model.md`,
  `docs/design/api-mark-vhdl/vhdl-ast-parser.md`,
  `docs/design/api-mark-vhdl/vhdl-emitter.md`,
  `docs/design/api-mark-vhdl/vhdl-emitter-gradual-disclosure.md`,
  `docs/design/api-mark-vhdl/vhdl-emitter-single-file.md`,
  `docs/design/api-mark-msbuild.md`, `docs/design/api-mark-msbuild/{item}.md`,
  `docs/design/api-mark-tool.md`, `docs/design/api-mark-tool/{item}.md`
- Verification: `docs/verification/api-mark-core.md`, `docs/verification/api-mark-core/{item}.md`,
  `docs/verification/api-mark-dot-net.md`,
  `docs/verification/api-mark-dot-net/dot-net-generator.md`,
  `docs/verification/api-mark-dot-net/type-name-simplifier.md`,
  `docs/verification/api-mark-dot-net/dot-net-ast-model.md`,
  `docs/verification/api-mark-dot-net/dot-net-emitter.md`,
  `docs/verification/api-mark-dot-net/dot-net-emitter-gradual-disclosure.md`,
  `docs/verification/api-mark-dot-net/dot-net-emitter-single-file.md`,
  `docs/verification/api-mark-dot-net/type-link-resolver.md`,
  `docs/verification/api-mark-dot-net/xml-doc-reader.md`,
  `docs/verification/api-mark-cpp.md`,
  `docs/verification/api-mark-cpp/cpp-generator.md`,
  `docs/verification/api-mark-cpp/cpp-ast-model.md`,
  `docs/verification/api-mark-cpp/clang-ast-parser.md`,
  `docs/verification/api-mark-cpp/cpp-emitter.md`,
  `docs/verification/api-mark-cpp/cpp-emitter-gradual-disclosure.md`,
  `docs/verification/api-mark-cpp/cpp-emitter-single-file.md`,
  `docs/verification/api-mark-cpp/cpp-type-link-resolver.md`,
  `docs/verification/api-mark-vhdl.md`,
  `docs/verification/api-mark-vhdl/vhdl-generator.md`,
  `docs/verification/api-mark-vhdl/vhdl-ast-model.md`,
  `docs/verification/api-mark-vhdl/vhdl-ast-parser.md`,
  `docs/verification/api-mark-vhdl/vhdl-emitter.md`,
  `docs/verification/api-mark-vhdl/vhdl-emitter-gradual-disclosure.md`,
  `docs/verification/api-mark-vhdl/vhdl-emitter-single-file.md`,
  `docs/verification/api-mark-msbuild.md`, `docs/verification/api-mark-msbuild/{item}.md`,
  `docs/verification/api-mark-tool.md`, `docs/verification/api-mark-tool/{item}.md`
- Source: `src/ApiMark.Core/`, `src/ApiMark.DotNet/`, `src/ApiMark.Cpp/`, `src/ApiMark.Vhdl/`,
  `src/ApiMark.MSBuild/`, `src/ApiMark.Tool/`
- Tests: `test/ApiMark.Core.TestHelpers/`, `test/ApiMark.Core.Tests/`, `test/ApiMark.DotNet.Tests/`,
  `test/ApiMark.Cpp.Tests/`, `test/ApiMark.Vhdl.Tests/`,
  `test/ApiMark.MSBuild.Tests/`, `test/ApiMark.MSBuild.PackageTests/`, `test/ApiMark.Tool.Tests/`
- Fixtures: `test/ApiMark.DotNet.Fixtures/`, `test/ApiMark.Cpp.Fixtures/`

OTS items have integration/usage design documentation parallel to system folders:

- Design: `docs/design/ots/mono-cecil.md`
- Verification: `docs/verification/ots/mono-cecil.md`

And for clang:

- Requirements: `docs/reqstream/ots/clang.yaml`
- Design: `docs/design/ots/clang.md`
- Verification: `docs/verification/ots/clang.md`

And for DemaConsulting.TestResults:

- Requirements: `docs/reqstream/ots/dema-consulting-test-results.yaml`
- Design: `docs/design/ots/dema-consulting-test-results.md`
- Verification: `docs/verification/ots/dema-consulting-test-results.md`

And for ANTLR4:

- Requirements: `docs/reqstream/ots/antlr4.yaml`
- Design: `docs/design/ots/antlr4.md`
- Verification: `docs/verification/ots/antlr4.md`

And for Microsoft.Extensions.FileSystemGlobbing:

- Requirements: `docs/reqstream/ots/file-system-globbing.yaml`
- Design: `docs/design/ots/file-system-globbing.md`
- Verification: `docs/verification/ots/file-system-globbing.md`

And for cpp-ast-net (archived):

- Requirements: `docs/reqstream/ots/cpp-ast-net.yaml`
- Design: `docs/design/ots/cpp-ast-net.md`
- Verification: `docs/verification/ots/cpp-ast-net.md`

Review-sets: defined in `.reviewmark.yaml`

## References

N/A - this document set is derived from repository source materials and does not
introduce external specifications or standards requiring citation.
