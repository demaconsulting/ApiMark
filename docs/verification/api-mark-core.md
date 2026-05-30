# ApiMarkCore

## Verification Approach

ApiMark.Core is verified with unit tests in `test/ApiMark.Core.Tests/` that exercise the core
output-contract interfaces and file-naming helpers in isolation. Tests use real `FileLayout` logic
because its string output is the subject under test; `IMarkdownWriter` and `IApiGenerator`
interactions are verified through in-memory test doubles that confirm callers can request the full
range of structured operations in a predictable sequence without depending on a specific renderer or
generator implementation. No MSBuild or external process involvement is required.

## Test Environment

N/A — standard test environment using the .NET test runner is sufficient for ApiMark.Core unit
tests.

## Acceptance Criteria

- All ApiMark.Core unit tests pass with zero failures.
- `FileLayout` returns stable relative paths for the entrypoint, namespace, type, and member files.
- Overload naming rules append numeric suffixes only when additional overloads are present.
- `IApiGenerator` and `IMarkdownWriter` contract tests confirm that structured operations are
  forwarded with the correct values and sequence.

## Test Scenarios

**Entrypoint file naming is stable**: Verifies that the documentation entrypoint is always emitted
as `api.md`, ensuring all language generators publish the same root file name. This scenario is
tested by `EntrypointFile_ReturnsApiMarkdownFile`.

**Namespace and type paths follow the documented hierarchy**: Verifies that namespace and type file
helpers produce the expected relative paths for nested namespaces and types so all generators share
one layout contract. This scenario is tested by
`TypeFile_WithNestedNamespace_ReturnsExpectedRelativePath`.

**Member overloads receive deterministic suffixes**: Verifies that the first member overload uses
its plain file name and later overloads append `.2`, `.3`, and higher suffixes so links remain
stable across repeated runs. This scenario is tested by
`MemberFile_WithAdditionalOverloads_AppendsNumericSuffix`.

**Markdown writer calls preserve structured content**: Verifies that consumers can emit headings,
paragraphs, tables, code blocks, and links through the `IMarkdownWriter` contract without losing the
requested level, text, or relative path. This scenario is tested by
`MarkdownWriterContract_ForwardsStructuredBlocks`.

**Generator contract is satisfied by all implementations**: Verifies that every language-generator
implementation compiles against the `IApiGenerator` interface and passes type-checking, confirming
that the contract is correctly defined for downstream generators to fulfill. This scenario is tested
by `ApiGeneratorContract_ImplementationCompiles`.
