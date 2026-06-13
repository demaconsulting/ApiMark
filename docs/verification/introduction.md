# Introduction

This document describes how each software item in ApiMark is verified — local systems and their
units, and OTS software items that the product depends on. For each item it names the test scenarios
that verify its requirements. A reviewer can confirm coverage completeness without reading test code.

## Purpose

The purpose of this document is to describe how ApiMark verification is organized so reviewers can
confirm that each software item has a defined test strategy before implementation evidence is
reviewed. The document identifies the planned verification level for each item, the expected test
environment, and the scenarios that demonstrate correct Markdown generation behavior.

## Scope

Local items:

- **ApiMarkCore**: system-level verification of the core output-contract interfaces.
- **IApiGenerator**: unit verification of the language-generator contract interface.
- **IMarkdownWriterFactory**: unit verification of the Markdown writer factory interface.
- **IMarkdownWriter**: unit verification of the Markdown output interface.
- **IContext**: unit verification of the output-channel contract interface.
- **PathHelpers**: unit verification of the safe path-combination utility.
- **IApiEmitter**: unit verification of the language-emitter contract interface.
- **EmitConfig**: unit verification of the emit-configuration value object and output-format enum.
- **ApiMarkCpp**: system-level verification of the C++ documentation generation pipeline.
- **CppGenerator**: unit verification of header discovery, visibility filtering, Doxygen comment
  rendering, and Markdown output generation.
- **CppAstModel**: unit verification of the C++ AST data model records that hold parsed type,
  function, and namespace information.
- **ClangAstParser**: unit verification that the clang invocation, JSON AST parsing, and
  declaration-provenance filtering produce the expected structured AST.
- **CppEmitter**: unit verification of the dispatch logic that routes to the format-specific
  C++ emitter.
- **CppEmitterGradualDisclosure**: unit verification of multi-file gradual-disclosure C++ output
  generation across namespaces, types, and member detail pages.
- **CppEmitterSingleFile**: unit verification of combined single-file C++ API reference output.
- **CppTypeLinkResolver**: unit verification that C++ type strings are resolved to correct intra-doc
  Markdown links.
- **ApiMarkDotNet**: system-level verification of the .NET documentation generation pipeline.
- **DotNetGenerator**: unit verification of type discovery, visibility filtering, per-member
  detail-page emission, and Markdown output generation.
- **DotNetAstModel**: unit verification of the data structure that holds pre-parsed assembly data
  between the parse and emit phases.
- **DotNetEmitter**: unit verification of the dispatch logic that selects single-file or
  gradual-disclosure output based on the configured format.
- **DotNetEmitterGradualDisclosure**: unit verification of multi-file gradual-disclosure .NET output
  generation across namespaces, types, and member detail pages.
- **DotNetEmitterSingleFile**: unit verification of combined single-file .NET API reference output.
- **TypeLinkResolver**: unit verification that Mono.Cecil type references are resolved to correct
  intra-doc Markdown links.
- **XmlDocReader**: unit verification that XML documentation files produced by the C# compiler are
  parsed and indexed correctly for use during emission.
- **TypeNameSimplifier**: unit verification of the seven-rule CLR type-name simplification logic.
- **ApiMarkVhdl**: system-level verification of the VHDL documentation generation pipeline.
- **VhdlGenerator**: unit verification of source-file discovery via glob patterns, `--!` doc-comment
  extraction, and Markdown output generation.
- **VhdlAstModel**: unit verification of the data types that represent parsed VHDL entity,
  architecture, and package declarations.
- **VhdlAstParser**: unit verification that ANTLR4 vhdl2008 grammar parsing and `--!` doc-comment
  extraction produce the expected structured AST.
- **VhdlEmitter**: unit verification of the dispatch logic that routes to the format-specific
  VHDL emitter.
- **VhdlEmitterGradualDisclosure**: unit verification of multi-file gradual-disclosure VHDL output
  generation across entities, architectures, and packages.
- **VhdlEmitterSingleFile**: unit verification of combined single-file VHDL API reference output.
- **ApiMarkMsbuild**: system-level verification of the MSBuild task that spawns the
  ApiMark.Tool process for documentation generation.
- **ApiMarkTask**: unit verification of MSBuild property forwarding and task invocation.
- **ApiMarkTool**: system-level verification of the CLI entry point.
- **Cli**: subsystem-level verification of command-line argument parsing and context construction.
- **Context**: unit verification of argument parsing, option storage, and output routing.
- **SelfTest**: subsystem-level verification of the self-validation subsystem.
- **Validation**: unit verification of the self-test execution logic.
- **Program**: unit verification of CLI argument parsing, language dispatch, and error handling.

OTS items:

- **Mono.Cecil**: integration verification that assembly-reading and metadata APIs used by
  ApiMark.DotNet are available and interpreted correctly.
- **clang**: integration verification that the clang executable invoked by ApiMarkCpp is available
  and produces AST output that ApiMark can parse correctly.
- **DemaConsulting.TestResults**: integration verification that the test-result recording and
  serialization APIs used by the SelfTest subsystem produce correct TRX and JUnit XML output.
- **Antlr4.Runtime.Standard / ANTLR4 vhdl2008 grammar**: integration verification
  that the committed generated parser code correctly parses VHDL-2008 source files,
  verified indirectly through the VhdlAstParser test suite.
- **cpp-ast-net** (archived): retained for historical reference; see clang verification for the
  current approach.

## Companion Artifact Structure

Local items have parallel artifacts in:

- Requirements: `docs/reqstream/api-mark-core.yaml`,
  `docs/reqstream/api-mark-core/i-api-generator.yaml`,
  `docs/reqstream/api-mark-core/i-api-emitter.yaml`,
  `docs/reqstream/api-mark-core/emit-config.yaml`,
  `docs/reqstream/api-mark-core/i-context.yaml`,
  `docs/reqstream/api-mark-core/i-markdown-writer-factory.yaml`,
  `docs/reqstream/api-mark-core/i-markdown-writer.yaml`,
  `docs/reqstream/api-mark-core/path-helpers.yaml`,
  `docs/reqstream/api-mark-cpp.yaml`,
  `docs/reqstream/api-mark-cpp/cpp-generator.yaml`,
  `docs/reqstream/api-mark-cpp/cpp-ast-model.yaml`,
  `docs/reqstream/api-mark-cpp/clang-ast-parser.yaml`,
  `docs/reqstream/api-mark-cpp/cpp-emitter.yaml`,
  `docs/reqstream/api-mark-cpp/cpp-emitter-gradual-disclosure.yaml`,
  `docs/reqstream/api-mark-cpp/cpp-emitter-single-file.yaml`,
  `docs/reqstream/api-mark-cpp/cpp-type-link-resolver.yaml`,
  `docs/reqstream/api-mark-dot-net.yaml`,
  `docs/reqstream/api-mark-dot-net/dot-net-generator.yaml`,
  `docs/reqstream/api-mark-dot-net/type-name-simplifier.yaml`,
  `docs/reqstream/api-mark-dot-net/dot-net-ast-model.yaml`,
  `docs/reqstream/api-mark-dot-net/dot-net-emitter.yaml`,
  `docs/reqstream/api-mark-dot-net/dot-net-emitter-gradual-disclosure.yaml`,
  `docs/reqstream/api-mark-dot-net/dot-net-emitter-single-file.yaml`,
  `docs/reqstream/api-mark-dot-net/type-link-resolver.yaml`,
  `docs/reqstream/api-mark-dot-net/xml-doc-reader.yaml`,
  `docs/reqstream/api-mark-vhdl.yaml`,
  `docs/reqstream/api-mark-vhdl/vhdl-generator.yaml`,
  `docs/reqstream/api-mark-vhdl/vhdl-ast-model.yaml`,
  `docs/reqstream/api-mark-vhdl/vhdl-ast-parser.yaml`,
  `docs/reqstream/api-mark-vhdl/vhdl-emitter.yaml`,
  `docs/reqstream/api-mark-vhdl/vhdl-emitter-gradual-disclosure.yaml`,
  `docs/reqstream/api-mark-vhdl/vhdl-emitter-single-file.yaml`,
  `docs/reqstream/api-mark-msbuild.yaml`,
  `docs/reqstream/api-mark-msbuild/api-mark-task.yaml`,
  `docs/reqstream/api-mark-tool.yaml`,
  `docs/reqstream/api-mark-tool/program.yaml`,
  `docs/reqstream/api-mark-tool/cli.yaml`,
  `docs/reqstream/api-mark-tool/cli/context.yaml`,
  `docs/reqstream/api-mark-tool/self-test.yaml`,
  `docs/reqstream/api-mark-tool/self-test/validation.yaml`
- Design: `docs/design/api-mark-core.md`, `docs/design/api-mark-core/i-api-generator.md`,
  `docs/design/api-mark-core/i-api-emitter.md`,
  `docs/design/api-mark-core/emit-config.md`,
  `docs/design/api-mark-core/i-context.md`,
  `docs/design/api-mark-core/i-markdown-writer-factory.md`,
  `docs/design/api-mark-core/i-markdown-writer.md`,
  `docs/design/api-mark-core/path-helpers.md`,
  `docs/design/api-mark-cpp.md`, `docs/design/api-mark-cpp/cpp-generator.md`,
  `docs/design/api-mark-cpp/cpp-ast-model.md`,
  `docs/design/api-mark-cpp/clang-ast-parser.md`,
  `docs/design/api-mark-cpp/cpp-emitter.md`,
  `docs/design/api-mark-cpp/cpp-emitter-gradual-disclosure.md`,
  `docs/design/api-mark-cpp/cpp-emitter-single-file.md`,
  `docs/design/api-mark-cpp/cpp-type-link-resolver.md`,
  `docs/design/api-mark-dot-net.md`, `docs/design/api-mark-dot-net/dot-net-generator.md`,
  `docs/design/api-mark-dot-net/type-name-simplifier.md`,
  `docs/design/api-mark-dot-net/dot-net-ast-model.md`,
  `docs/design/api-mark-dot-net/dot-net-emitter.md`,
  `docs/design/api-mark-dot-net/dot-net-emitter-gradual-disclosure.md`,
  `docs/design/api-mark-dot-net/dot-net-emitter-single-file.md`,
  `docs/design/api-mark-dot-net/type-link-resolver.md`,
  `docs/design/api-mark-dot-net/xml-doc-reader.md`,
  `docs/design/api-mark-vhdl.md`,
  `docs/design/api-mark-vhdl/vhdl-generator.md`,
  `docs/design/api-mark-vhdl/vhdl-ast-model.md`,
  `docs/design/api-mark-vhdl/vhdl-ast-parser.md`,
  `docs/design/api-mark-vhdl/vhdl-emitter.md`,
  `docs/design/api-mark-vhdl/vhdl-emitter-gradual-disclosure.md`,
  `docs/design/api-mark-vhdl/vhdl-emitter-single-file.md`,
  `docs/design/api-mark-msbuild.md`,
  `docs/design/api-mark-msbuild/api-mark-task.md`,
  `docs/design/api-mark-tool.md`, `docs/design/api-mark-tool/program.md`,
  `docs/design/api-mark-tool/cli.md`, `docs/design/api-mark-tool/cli/context.md`,
  `docs/design/api-mark-tool/self-test.md`, `docs/design/api-mark-tool/self-test/validation.md`
- Verification: `docs/verification/api-mark-core.md`,
  `docs/verification/api-mark-core/i-api-generator.md`,
  `docs/verification/api-mark-core/i-api-emitter.md`,
  `docs/verification/api-mark-core/emit-config.md`,
  `docs/verification/api-mark-core/i-context.md`,
  `docs/verification/api-mark-core/i-markdown-writer-factory.md`,
  `docs/verification/api-mark-core/i-markdown-writer.md`,
  `docs/verification/api-mark-core/path-helpers.md`,
  `docs/verification/api-mark-cpp.md`,
  `docs/verification/api-mark-cpp/cpp-generator.md`,
  `docs/verification/api-mark-cpp/cpp-ast-model.md`,
  `docs/verification/api-mark-cpp/clang-ast-parser.md`,
  `docs/verification/api-mark-cpp/cpp-emitter.md`,
  `docs/verification/api-mark-cpp/cpp-emitter-gradual-disclosure.md`,
  `docs/verification/api-mark-cpp/cpp-emitter-single-file.md`,
  `docs/verification/api-mark-cpp/cpp-type-link-resolver.md`,
  `docs/verification/api-mark-dot-net.md`,
  `docs/verification/api-mark-dot-net/dot-net-generator.md`,
  `docs/verification/api-mark-dot-net/type-name-simplifier.md`,
  `docs/verification/api-mark-dot-net/dot-net-ast-model.md`,
  `docs/verification/api-mark-dot-net/dot-net-emitter.md`,
  `docs/verification/api-mark-dot-net/dot-net-emitter-gradual-disclosure.md`,
  `docs/verification/api-mark-dot-net/dot-net-emitter-single-file.md`,
  `docs/verification/api-mark-dot-net/type-link-resolver.md`,
  `docs/verification/api-mark-dot-net/xml-doc-reader.md`,
  `docs/verification/api-mark-vhdl.md`,
  `docs/verification/api-mark-vhdl/vhdl-generator.md`,
  `docs/verification/api-mark-vhdl/vhdl-ast-model.md`,
  `docs/verification/api-mark-vhdl/vhdl-ast-parser.md`,
  `docs/verification/api-mark-vhdl/vhdl-emitter.md`,
  `docs/verification/api-mark-vhdl/vhdl-emitter-gradual-disclosure.md`,
  `docs/verification/api-mark-vhdl/vhdl-emitter-single-file.md`,
  `docs/verification/api-mark-msbuild.md`,
  `docs/verification/api-mark-msbuild/api-mark-task.md`,
  `docs/verification/api-mark-tool.md`, `docs/verification/api-mark-tool/program.md`,
  `docs/verification/api-mark-tool/cli.md`, `docs/verification/api-mark-tool/cli/context.md`,
  `docs/verification/api-mark-tool/self-test.md`,
  `docs/verification/api-mark-tool/self-test/validation.md`
- Source: `src/ApiMark.Core/`, `src/ApiMark.Cpp/`, `src/ApiMark.DotNet/`,
  `src/ApiMark.Vhdl/`, `src/ApiMark.MSBuild/`, `src/ApiMark.Tool/`
- Tests: `test/ApiMark.Core.Tests/`, `test/ApiMark.Cpp.Fixtures/`,
  `test/ApiMark.Cpp.Tests/`, `test/ApiMark.DotNet.Tests/`, `test/ApiMark.Vhdl.Tests/`,
  `test/ApiMark.MSBuild.Tests/`, `test/ApiMark.Tool.Tests/`

OTS items have integration and usage artifacts parallel to the system folders:

- Requirements: `docs/reqstream/ots/clang.yaml`, `docs/reqstream/ots/mono-cecil.yaml`,
  `docs/reqstream/ots/dema-consulting-test-results.yaml`, `docs/reqstream/ots/antlr4.yaml`,
  `docs/reqstream/ots/cpp-ast-net.yaml`
- Design: `docs/design/ots/clang.md`, `docs/design/ots/mono-cecil.md`,
  `docs/design/ots/dema-consulting-test-results.md`, `docs/design/ots/antlr4.md`,
  `docs/design/ots/cpp-ast-net.md`
- Verification: `docs/verification/ots/clang.md`, `docs/verification/ots/mono-cecil.md`,
  `docs/verification/ots/dema-consulting-test-results.md`, `docs/verification/ots/antlr4.md`,
  `docs/verification/ots/cpp-ast-net.md`

Review-sets are defined in `.reviewmark.yaml`.

## References

N/A — no external specification is required to describe this repository-specific verification
baseline.
