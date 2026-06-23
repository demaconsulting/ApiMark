# ApiMarkCore

## Verification Approach

ApiMark.Core is verified with unit tests in `test/ApiMark.Core.Tests/` that exercise the
core output-contract interfaces and path-safety helper in isolation. Tests use in-memory
test doubles from `test/ApiMark.Core.TestHelpers/` to confirm that callers can invoke
the full range of structured operations through the `IApiGenerator` and
`IMarkdownWriter` interfaces in a predictable sequence without depending on a specific
renderer or generator implementation. Additional PathHelpers tests verify safe
combination of valid relative paths and rejection of traversal attempts and null input.
No MSBuild or external process involvement is required.

## Test Environment

N/A - standard test environment using the .NET test runner is sufficient for ApiMark.Core
unit tests.

## Acceptance Criteria

- All ApiMark.Core unit tests pass with zero failures.
- `IApiGenerator` implementations satisfy the contract and can be invoked through the
  interface reference.
- `IContext` implementations capture informational messages in `Lines` and error messages
  in `Errors` with no cross-channel contamination.
- `IMarkdownWriterFactory` can create writers for root and subfolder paths.
- `IMarkdownWriter` structured operations (headings, paragraphs, code blocks, tables,
  links) are forwarded with the correct values and sequence.
- `PathHelpers` combines valid relative paths and rejects traversal and null
  path input. Rooted (absolute) path segments are not rejected — they are
  concatenated as relative components within the base directory.
- `GlobFileCollector` collects files matching glob patterns: empty patterns return
  empty; bare-star segments apply language-extension filtering; absolute patterns work
  independently of working directory; exclusions remove files; non-existent roots
  return empty without throwing; results are sorted and deduplicated.

## Test Scenarios

**IContext contract routes messages to the correct channel**: Verifies that the
`IContext` interface contract is correctly implemented — informational messages reach
`Lines`, error messages reach `Errors`, and no cross-contamination occurs between
channels. This scenario is tested by `IContext_WriteLine_CapturesMessage_InLines`,
`IContext_WriteError_CapturesMessage_InErrors`, and
`InMemoryContext_WriteLineAndWriteError_RouteToSeparateChannels`.

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
`InMemoryMarkdownWriter_Write_AllOperations_RecordsInOrder`.

**Path helper enforces safe path combination**: Verifies that PathHelpers combines valid
relative segments while rejecting traversal attempts and null input. Rooted (absolute)
path segments are resolved as relative components within the base directory rather than
rejected. This scenario is tested by the `PathHelpers_SafePathCombine_*` test cases in
`PathHelpersTests`.

**GlobFileCollector discovers files via glob patterns**: Verifies that GlobFileCollector
correctly collects files matching inclusion patterns, applies language-extension filtering
for bare-star segments, supports absolute path patterns, removes files matched by
exclusion patterns, returns empty results for non-existent roots without throwing, and
returns a sorted, deduplicated list. These scenarios are tested by the
`GlobFileCollector_Collect_*` test cases in `GlobFileCollectorTests`.

**Emitter contract is callable through the interface**: Verifies that a language-emitter
implementation compiles against the `IApiEmitter` interface and that `Emit` can be
invoked through an interface reference, producing either multi-file or single-file output
depending on `EmitConfig.Format`. This scenario is tested by
`IApiEmitter_Emit_WithGradualDisclosure_ProducesMultipleFiles` and
`IApiEmitter_Emit_WithSingleFile_ProducesSingleApiMd`.

**Emit configuration defaults are correct**: Verifies that `EmitConfig` defaults to
`OutputFormat.GradualDisclosure` for `Format` and `1` for `HeadingDepth`, and that
out-of-range heading depths are rejected. This scenario is tested by
`EmitConfig_DefaultFormat_IsGradualDisclosure`, `EmitConfig_DefaultHeadingDepth_IsOne`,
`EmitConfig_HeadingDepth_BelowMinimum_ThrowsArgumentOutOfRangeException`, and
`EmitConfig_HeadingDepth_AboveMaximum_ThrowsArgumentOutOfRangeException`.
