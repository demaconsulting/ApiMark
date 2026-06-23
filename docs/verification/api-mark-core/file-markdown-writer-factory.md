## FileMarkdownWriterFactory

### Verification Approach

`FileMarkdownWriterFactory` is verified through direct unit tests in
`test/ApiMark.Core.Tests/FileMarkdownWriterFactoryTests.cs`. Each test creates a
uniquely named temporary directory under `Path.GetTempPath()`, exercises the factory
against the real file system, and deletes the directory in `IDisposable.Dispose` to
prevent test pollution. Tests cover constructor validation, file and directory creation,
and subfolder handling.

### Test Environment

N/A - standard .NET test runner is sufficient. Tests use isolated temporary directories
so they are safe to run in parallel.

### Acceptance Criteria

- All `FileMarkdownWriterFactoryTests` test cases pass with zero failures.
- Constructor throws `ArgumentException` for null, empty string, and whitespace-only
  output directory arguments.
- `CreateMarkdown` throws `ArgumentException` for null, empty string, and
  whitespace-only file names.
- `CreateMarkdown` with an empty subFolder creates the file directly under the output root.
- `CreateMarkdown` with a whitespace-only subFolder also creates the file at the output root
  (whitespace is treated as equivalent to empty).
- `CreateMarkdown` with a non-empty subFolder creates the required subdirectory and file.
- `CreateMarkdown` creates the output root directory if it does not yet exist at the time
  of the call.

### Test Scenarios

**Constructor with null output directory throws ArgumentException**: Verifies that
`new FileMarkdownWriterFactory(null!)` throws `ArgumentException`, preventing
misconfiguration at construction time. Tested by
`FileMarkdownWriterFactory_Constructor_NullDirectory_ThrowsArgumentException`.

**Constructor with empty string output directory throws ArgumentException**: Verifies that
`new FileMarkdownWriterFactory("")` throws `ArgumentException`. Tested by
`FileMarkdownWriterFactory_Constructor_EmptyDirectory_ThrowsArgumentException`.

**Constructor with whitespace output directory throws ArgumentException**: Verifies that
`new FileMarkdownWriterFactory("   ")` throws `ArgumentException` because whitespace-only
strings are not valid directory paths. Tested by
`FileMarkdownWriterFactory_Constructor_WhitespaceDirectory_ThrowsArgumentException`.

**CreateMarkdown with null name throws ArgumentException**: Verifies that
`factory.CreateMarkdown("", null!)` throws `ArgumentException` before any I/O is
attempted, so the misuse is immediately attributable. Tested by
`FileMarkdownWriterFactory_CreateMarkdown_NullName_ThrowsArgumentException`.

**CreateMarkdown with empty name throws ArgumentException**: Verifies that
`factory.CreateMarkdown("", "")` throws `ArgumentException`. Tested by
`FileMarkdownWriterFactory_CreateMarkdown_EmptyName_ThrowsArgumentException`.

**CreateMarkdown with whitespace name throws ArgumentException**: Verifies that
`factory.CreateMarkdown("", "   ")` throws `ArgumentException`. Tested by
`FileMarkdownWriterFactory_CreateMarkdown_WhitespaceName_ThrowsArgumentException`.

**CreateMarkdown with empty subFolder creates root-level file**: Verifies that passing
an empty string as `subFolder` places the output file directly under the configured
output root and that the file exists after the writer is disposed. Tested by
`FileMarkdownWriterFactory_CreateMarkdown_RootLevel_CreatesFile`.

**CreateMarkdown with whitespace subFolder creates root-level file**: Verifies that
passing a whitespace-only string as `subFolder` is treated the same as an empty string,
placing the output file directly under the configured output root. This matches the
design contract which states that null or whitespace in `subFolder` routes to the root.
Tested by `FileMarkdownWriterFactory_CreateMarkdown_WhitespaceSubFolder_CreatesRootLevelFile`.

**CreateMarkdown with subFolder creates directory and file**: Verifies that a non-empty
`subFolder` causes the factory to create the required subdirectory and write the file
inside it. Tested by
`FileMarkdownWriterFactory_CreateMarkdown_WithSubFolder_CreatesDirectoryAndFile`.

**CreateMarkdown creates non-existent output directory on first use**: Verifies that the
factory creates the output root directory when it does not yet exist at the time of the
first `CreateMarkdown` call, without requiring callers to pre-create directories. Tested
by `FileMarkdownWriterFactory_CreateMarkdown_NonExistentDirectory_CreatesDirectory`.
