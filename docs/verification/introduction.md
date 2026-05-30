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

- **ApiMarkCore**: system-level verification of the core output-contract interfaces and file-naming
  helpers.
- **IApiGenerator**: unit verification of the language-generator contract interface.
- **IMarkdownWriter**: unit verification of the Markdown output interface.
- **FileLayout**: unit verification of file-path and file-naming helpers.
- **ApiMarkDotNet**: system-level verification of the .NET documentation generation pipeline.
- **DotNetGenerator**: unit verification of type discovery, visibility filtering, complexity-rule
  evaluation, and Markdown output generation.
- **TypeNameSimplifier**: unit verification of the seven-rule CLR type-name simplification logic.
- **ApiMarkMsbuild**: system-level verification of the MSBuild task that spawns the
  ApiMark.Tool process for documentation generation.
- **ApiMarkTask**: unit verification of MSBuild property forwarding and task invocation.
- **ApiMarkTool**: system-level verification of the CLI entry point.
- **Program**: unit verification of CLI argument parsing, language dispatch, and error handling.

OTS items:

- **Mono.Cecil**: integration verification that assembly-reading and metadata APIs used by
  ApiMark.DotNet are available and interpreted correctly.

Out of scope:

- Test projects as software items in their own right.
- CI orchestration, lint-only tooling, and formal review workflow configuration.
- Planned future language implementations outside the current .NET/MSBuild scope.

## Companion Artifact Structure

Local items have parallel artifacts in:

- Requirements: `docs/reqstream/api-mark-core.yaml`,
  `docs/reqstream/api-mark-core/i-api-generator.yaml`,
  `docs/reqstream/api-mark-core/i-markdown-writer.yaml`,
  `docs/reqstream/api-mark-core/file-layout.yaml`,
  `docs/reqstream/api-mark-dot-net.yaml`,
  `docs/reqstream/api-mark-dot-net/dot-net-generator.yaml`,
  `docs/reqstream/api-mark-dot-net/type-name-simplifier.yaml`,
  `docs/reqstream/api-mark-msbuild.yaml`,
  `docs/reqstream/api-mark-msbuild/api-mark-task.yaml`,
  `docs/reqstream/api-mark-tool.yaml`,
  `docs/reqstream/api-mark-tool/program.yaml`
- Design: `docs/design/api-mark-core.md`, `docs/design/api-mark-core/i-api-generator.md`,
  `docs/design/api-mark-core/i-markdown-writer.md`, `docs/design/api-mark-core/file-layout.md`,
  `docs/design/api-mark-dot-net.md`, `docs/design/api-mark-dot-net/dot-net-generator.md`,
  `docs/design/api-mark-dot-net/type-name-simplifier.md`,
  `docs/design/api-mark-msbuild.md`,
  `docs/design/api-mark-msbuild/api-mark-task.md`,
  `docs/design/api-mark-tool.md`, `docs/design/api-mark-tool/program.md`
- Verification: `docs/verification/api-mark-core.md`,
  `docs/verification/api-mark-core/i-api-generator.md`,
  `docs/verification/api-mark-core/i-markdown-writer.md`,
  `docs/verification/api-mark-core/file-layout.md`,
  `docs/verification/api-mark-dot-net.md`,
  `docs/verification/api-mark-dot-net/dot-net-generator.md`,
  `docs/verification/api-mark-dot-net/type-name-simplifier.md`,
  `docs/verification/api-mark-msbuild.md`,
  `docs/verification/api-mark-msbuild/api-mark-task.md`,
  `docs/verification/api-mark-tool.md`, `docs/verification/api-mark-tool/program.md`
- Source: `src/ApiMark.Core/`, `src/ApiMark.DotNet/`, `src/ApiMark.MSBuild/`,
  `src/ApiMark.Tool/`
- Tests: `test/ApiMark.Core.Tests/`, `test/ApiMark.DotNet.Tests/`, `test/ApiMark.Tool.Tests/`

OTS items have integration and usage artifacts parallel to the system folders:

- Requirements: `docs/reqstream/ots/mono-cecil.yaml`
- Design: `docs/design/ots/mono-cecil.md`
- Verification: `docs/verification/ots/mono-cecil.md`

Review-sets are defined in `.reviewmark.yaml`.

## References

N/A — no external specification is required to describe this repository-specific verification
baseline.
