## IMarkdownWriterFactory

### Verification Approach

`IMarkdownWriterFactory` is verified via the `InMemoryMarkdownWriterFactory` test
double in `ApiMark.Core.TestHelpers`. If the interface definition is incorrect or
incomplete the test double fails to compile, providing immediate compile-time
feedback. Runtime contract tests confirm that CreateMarkdown returns a usable
IMarkdownWriter for each call.

`FileMarkdownWriterFactory` is verified by concrete implementation tests in
`FileMarkdownWriterFactoryTests` that exercise disk I/O, on-demand directory
creation, constructor validation, and path safety. These tests use a unique
temporary directory per test run to avoid inter-test interference.

### Test Environment

N/A — standard test environment using the .NET test runner is sufficient.
Interface contract compliance is enforced at compile time through the test double
in `ApiMark.Core.TestHelpers`.

### Acceptance Criteria

- All `IMarkdownWriterFactory` contract tests pass with zero failures.
- The interface exposes `CreateMarkdown(string subFolder, string name)` returning
  an `IMarkdownWriter`.
- An in-memory test double that implements `IMarkdownWriterFactory` compiles
  without errors and can be instantiated and injected into `IApiEmitter.Emit`.
- `FileMarkdownWriterFactory` creates actual files and directories on disk when
  `CreateMarkdown` is called, including creating a non-existent output root on demand.
- `FileMarkdownWriterFactory` rejects null or whitespace constructor arguments and
  null file name arguments with `ArgumentException`.

### Test Scenarios

**CreateMarkdown returns a non-null writer through the interface**: Verifies that
calling `IMarkdownWriterFactory.CreateMarkdown` with valid subFolder and name
arguments through the interface reference returns a non-null `IMarkdownWriter`,
confirming the method is correctly declared in the contract. Tested by
`IMarkdownWriterFactory_HasCreateMarkdown_Method`.

**Test double implements IMarkdownWriterFactory without errors**: Verifies that
`InMemoryMarkdownWriterFactory` compiles cleanly and can be instantiated,
confirming the interface contract has no hidden dependencies. Tested by
`InMemoryMarkdownWriterFactory_Constructor_Default_ImplementsInterface`.

**CreateMarkdown returns a usable IMarkdownWriter**: Verifies that calling
`CreateMarkdown` with a subFolder and name returns a non-null IMarkdownWriter on
which write methods can be called without error. Tested by
`InMemoryMarkdownWriterFactory_CreateMarkdown_ValidArgs_ReturnsNonNullWriter`.

**Root-level file created with empty subFolder**: Verifies that passing an empty
string for subFolder produces a root-level writer (i.e. no subdirectory prefix in
the captured path). Tested by
`IMarkdownWriterFactory_CreateMarkdown_EmptySubFolder_IsRootLevel`.

**File-system factory creates root-level file on disk**: Verifies that
`FileMarkdownWriterFactory.CreateMarkdown` with an empty subFolder writes a real
file directly under the output root directory and that the file exists after the
writer is disposed. Tested by
`FileMarkdownWriterFactory_CreateMarkdown_RootLevel_CreatesFile`.

**File-system factory creates subfolder directory and nested file**: Verifies that
`FileMarkdownWriterFactory.CreateMarkdown` with a non-empty subFolder creates the
subdirectory on disk if it does not exist and writes the file inside it. Tested by
`FileMarkdownWriterFactory_CreateMarkdown_WithSubFolder_CreatesDirectoryAndFile`.

**File-system factory creates non-existent output directory on demand**: Verifies that
`FileMarkdownWriterFactory` creates the output root directory the first time
`CreateMarkdown` is called, so callers do not need to pre-create the output path.
Tested by
`FileMarkdownWriterFactory_CreateMarkdown_NonExistentDirectory_CreatesDirectory`.

**File-system factory rejects null output directory**: Verifies that constructing
`FileMarkdownWriterFactory` with a null output directory throws `ArgumentException`
immediately, preventing a confusing I/O failure later. Tested by
`FileMarkdownWriterFactory_Constructor_NullDirectory_ThrowsArgumentException`.

**File-system factory rejects whitespace output directory**: Verifies that constructing
`FileMarkdownWriterFactory` with a whitespace-only output directory throws
`ArgumentException`. Tested by
`FileMarkdownWriterFactory_Constructor_WhitespaceDirectory_ThrowsArgumentException`.

**File-system factory rejects null file name**: Verifies that
`FileMarkdownWriterFactory.CreateMarkdown` throws `ArgumentException` when the file
name is null. Tested by
`FileMarkdownWriterFactory_CreateMarkdown_NullName_ThrowsArgumentException`.
