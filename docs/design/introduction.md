# Introduction

ApiMark generates compact, AI-friendly API reference documentation in Markdown from
source code and its associated metadata (XML doc comments, header files, docstrings,
etc.). The output is designed for gradual disclosure: an AI can read a lightweight
index, drill into a namespace summary, and then read a full type page — consuming only
as much context as the task requires. The project is structured as five independent
systems: ApiMark.Core (shared contracts and file-path helpers), ApiMark.DotNet
(C#/.NET language generator), ApiMark.Cpp (C++ language generator), ApiMark.MSBuild
(unified MSBuild task that spawns ApiMark.Tool out-of-process), and ApiMark.Tool (the
.NET executable invoked by ApiMarkTask and directly by users or CI pipelines). Two OTS
items provide library reflection: Mono.Cecil for the DotNet system and clang (via
`clang -ast-dump=json`) for the Cpp system.

## Purpose

This document defines the design for each software item in ApiMark — full architectural
and detailed design for local items (systems and units), and integration/usage design
for OTS software items. A reviewer should be able to understand how each item satisfies
its requirements without reading source code.

## Scope

Local items:

- **ApiMarkCore**: system and unit design.
- **ApiMarkDotNet**: system and unit design.
- **ApiMarkCpp**: system and unit design.
- **ApiMarkMsbuild**: system and unit design.
- **ApiMarkTool**: system and unit design.

OTS items:

- **Mono.Cecil**: integration and usage design.
- **clang**: integration and usage design (via `clang -ast-dump=json`).

Out of scope: test projects, build pipeline CI configuration, and the internal design
of OTS items.

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
└── PathHelpers (Unit)

ApiMarkDotNet (System)
├── DotNetGenerator (Unit)
├── XmlDocReader (Unit)
└── TypeNameSimplifier (Unit)

ApiMarkCpp (System)
├── CppGenerator (Unit)
└── ClangAstParser (Unit)

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
└── clang -ast-dump=json (OTS)
```

## Folder Layout

```text
src/
├── ApiMark.Core/
│   ├── IApiGenerator.cs                - interface every language generator must implement
│   ├── IApiEmitter.cs                  - interface for writing parsed results as Markdown output
│   ├── EmitConfig.cs                   - value object controlling output format and heading depth
│   ├── OutputFormat.cs                 - enum selecting GradualDisclosure or SingleFile output
│   ├── IContext.cs                     - minimal output channel that generators use to emit messages
│   ├── IMarkdownWriterFactory.cs       - factory interface for creating per-file markdown writers
│   ├── IMarkdownWriter.cs              - per-file markdown writing interface (IDisposable)
│   ├── PathHelpers.cs                  - shared path-safety helper for combining validated relative paths
│   ├── FileMarkdownWriterFactory.cs    - file-system implementation of IMarkdownWriterFactory
│   └── FileMarkdownWriter.cs           - file-system implementation of IMarkdownWriter
├── ApiMark.DotNet/
│   ├── ApiVisibility.cs           - enum controlling which members are included in output
│   ├── DotNetGenerator.cs         - C#/.NET IApiGenerator implementation
│   ├── DotNetGeneratorOptions.cs  - configuration options for the .NET generator
│   ├── TypeNameSimplifier.cs      - simplifies rendered .NET type references
│   └── XmlDocReader.cs            - parses XML documentation files produced by the C# compiler
├── ApiMark.Cpp/
│   ├── ApiVisibility.cs           - enum controlling which members are included in output
│   ├── CppGenerator.cs            - C++ IApiGenerator implementation
│   ├── CppGeneratorOptions.cs     - configuration options for the C++ generator
│   └── CppAst/
│       ├── CppAstModel.cs         - C++ AST data model (types, functions, namespaces)
│       └── ClangAstParser.cs      - invokes clang -ast-dump=json and parses the AST
├── ApiMark.MSBuild/
│   └── ApiMarkTask.cs             - MSBuild task that spawns ApiMark.Tool out-of-process
└── ApiMark.Tool/
    ├── Cli/
    │   └── Context.cs                 - command-line context with standard flags and language options
    ├── SelfTest/
    │   └── Validation.cs              - self-validation tests for --validate
    └── Program.cs                     - dotnet CLI entry point dispatching to IApiGenerator

test/
├── ApiMark.Core.TestHelpers/
│   ├── InMemoryMarkdownWriterFactory.cs  - in-memory IMarkdownWriterFactory test double
│   ├── InMemoryMarkdownWriter.cs         - in-memory IMarkdownWriter test double
│   └── InMemoryContext.cs                - in-memory IContext test double
├── ApiMark.Core.Tests/            - unit tests for Core contracts
├── ApiMark.DotNet.Fixtures/       - multi-target fixture assembly for DotNet integration tests
├── ApiMark.DotNet.Tests/          - unit tests for DotNetGenerator and TypeNameSimplifier
├── ApiMark.Cpp.Fixtures/          - C++ fixture headers for CppGenerator integration tests
├── ApiMark.Cpp.Tests/             - unit tests for CppGenerator
├── ApiMark.MSBuild.Tests/         - unit tests for ApiMarkTask
└── ApiMark.Tool.Tests/            - integration tests for the CLI tool
```

## Companion Artifact Structure

Each local software item has corresponding artifacts in parallel directory trees:

- Requirements: `docs/reqstream/api-mark-core.yaml`, `docs/reqstream/api-mark-core/{item}.yaml`, `docs/reqstream/api-mark-dot-net.yaml`, `docs/reqstream/api-mark-dot-net/{item}.yaml`, `docs/reqstream/api-mark-cpp.yaml`, `docs/reqstream/api-mark-cpp/{item}.yaml`, `docs/reqstream/api-mark-msbuild.yaml`, `docs/reqstream/api-mark-msbuild/{item}.yaml`, `docs/reqstream/api-mark-tool.yaml`, `docs/reqstream/api-mark-tool/{item}.yaml`
- Design: `docs/design/api-mark-core.md`, `docs/design/api-mark-core/{item}.md`, `docs/design/api-mark-dot-net.md`, `docs/design/api-mark-dot-net/{item}.md`, `docs/design/api-mark-cpp.md`, `docs/design/api-mark-cpp/{item}.md`, `docs/design/api-mark-msbuild.md`, `docs/design/api-mark-msbuild/{item}.md`, `docs/design/api-mark-tool.md`, `docs/design/api-mark-tool/{item}.md`
- Verification: `docs/verification/api-mark-core.md`, `docs/verification/api-mark-core/{item}.md`, `docs/verification/api-mark-dot-net.md`, `docs/verification/api-mark-dot-net/{item}.md`, `docs/verification/api-mark-cpp.md`, `docs/verification/api-mark-cpp/{item}.md`, `docs/verification/api-mark-msbuild.md`, `docs/verification/api-mark-msbuild/{item}.md`, `docs/verification/api-mark-tool.md`, `docs/verification/api-mark-tool/{item}.md`
- Source: `src/ApiMark.Core/`, `src/ApiMark.DotNet/`, `src/ApiMark.Cpp/`, `src/ApiMark.MSBuild/`, `src/ApiMark.Tool/`
- Tests: `test/ApiMark.Core.TestHelpers/`, `test/ApiMark.Core.Tests/`, `test/ApiMark.DotNet.Tests/`, `test/ApiMark.Cpp.Fixtures/`, `test/ApiMark.Cpp.Tests/`, `test/ApiMark.MSBuild.Tests/`, `test/ApiMark.Tool.Tests/`

OTS items have integration/usage design documentation parallel to system folders:

- Requirements: `docs/reqstream/ots/mono-cecil.yaml`
- Design: `docs/design/ots/mono-cecil.md`
- Verification: `docs/verification/ots/mono-cecil.md`

And for clang:

- Requirements: `docs/reqstream/ots/clang.yaml`
- Design: `docs/design/ots/clang.md`
- Verification: `docs/verification/ots/clang.md`

Review-sets: defined in `.reviewmark.yaml`

## References

N/A — this document set is derived from repository source materials and does not
introduce external specifications or standards requiring citation.
