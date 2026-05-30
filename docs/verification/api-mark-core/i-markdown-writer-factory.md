## IMarkdownWriterFactory

### Verification Approach

`IMarkdownWriterFactory` is verified via the `InMemoryMarkdownWriterFactory` test
double in `ApiMark.Core.TestHelpers`. If the interface definition is incorrect or
incomplete the test double fails to compile, providing immediate compile-time
feedback. Runtime contract tests confirm that CreateMarkdown returns a usable
IMarkdownWriter for each call.

### Test Environment

N/A — standard test environment using the .NET test runner is sufficient.
Interface contract compliance is enforced at compile time through the test double
in `ApiMark.Core.TestHelpers`.

### Acceptance Criteria

- All `IMarkdownWriterFactory` contract tests pass with zero failures.
- The interface exposes `CreateMarkdown(string subFolder, string name)` returning
  an `IMarkdownWriter`.
- An in-memory test double that implements `IMarkdownWriterFactory` compiles
  without errors and can be instantiated and injected into `IApiGenerator.Generate`.

### Test Scenarios

**Test double implements IMarkdownWriterFactory without errors**: Verifies that
`InMemoryMarkdownWriterFactory` compiles cleanly and can be instantiated,
confirming the interface contract has no hidden dependencies. Tested by
`MarkdownWriterFactoryContract_TestDoubleCompiles`.

**CreateMarkdown returns a usable IMarkdownWriter**: Verifies that calling
`CreateMarkdown` with a subFolder and name returns a non-null IMarkdownWriter on
which write methods can be called without error. Tested by
`MarkdownWriterFactoryContract_CreateMarkdown_ReturnsWriter`.

**Root-level file created with empty subFolder**: Verifies that passing an empty
string for subFolder produces a root-level writer (i.e. no subdirectory prefix in
the captured path). Tested by
`MarkdownWriterFactoryContract_CreateMarkdown_EmptySubFolder_IsRootLevel`.
