# ApiMarkCore

## Verification Approach

ApiMark.Core is verified with unit tests in `test/ApiMark.Core.Tests/` that exercise the
core output-contract interfaces in isolation. Tests use in-memory test doubles from
`test/ApiMark.Core.TestHelpers/` to confirm that callers can invoke the full range of
structured operations through the `IApiGenerator` and `IMarkdownWriter` interfaces in
a predictable sequence without depending on a specific renderer or generator
implementation. No MSBuild or external process involvement is required.

## Test Environment

N/A — standard test environment using the .NET test runner is sufficient for ApiMark.Core
unit tests.

## Acceptance Criteria

- All ApiMark.Core unit tests pass with zero failures.
- `IApiGenerator` implementations satisfy the contract and can be invoked through the
  interface reference.
- `IMarkdownWriterFactory` can create writers for root and subfolder paths.
- `IMarkdownWriter` structured operations (headings, paragraphs, code blocks, tables,
  links) are forwarded with the correct values and sequence.

## Test Scenarios

**Generator contract is satisfied**: Verifies that a language-generator implementation
compiles against the `IApiGenerator` interface and can be invoked through an interface
reference, confirming the contract is correctly defined for downstream generators to
fulfill. This scenario is tested by
`ApiMarkCore_GeneratorContract_SupportedLanguage_CanBeInvoked`.

**Writer factory creates root and subfolder writers**: Verifies that
`IMarkdownWriterFactory` can create writers for both the root path and a subfolder
path, confirming that the factory contract supports the full output hierarchy required
by generators. This scenario is tested by
`ApiMarkCore_WriterFactory_CanCreate_RootAndSubfolderWriters`.

**Markdown writer contract renders consistently**: Verifies that structured content
operations (headings, paragraphs, code blocks, tables, links) are forwarded through
the `IMarkdownWriter` contract with the correct values and without loss of
structure. This scenario is tested by
`ApiMarkCore_MarkdownWriterContract_FileSections_RenderConsistently`.
