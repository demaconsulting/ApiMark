## DotNetEmitter

### Verification Approach

`DotNetEmitter` is unit-tested in `test/ApiMark.DotNet.Tests/` by parsing a
real fixture assembly and then calling `Emit` with an
`InMemoryMarkdownWriterFactory`. Tests verify format dispatch (gradual-disclosure
vs. single-file), null-factory rejection, and namespace path computation. No
internal production components are mocked beyond the in-memory factory.

### Test Environment

Tests require the compiled fixture assembly, its XML documentation file, and
the `InMemoryMarkdownWriterFactory` from `ApiMark.Core.TestHelpers`. No external
service or network dependency is needed.

### Acceptance Criteria

- All `DotNetEmitter` tests pass with zero failures.
- Passing a null factory to `Emit` throws `ArgumentNullException` before any I/O.
- `OutputFormat.GradualDisclosure` produces more than one Markdown writer.
- `OutputFormat.SingleFile` produces exactly one writer keyed `api`.
- `GetNamespaceFolderPath` returns the full dotted name for a root namespace and
  a slash-separated path for a child namespace.

### Test Scenarios

**Null factory throws ArgumentNullException**: Verifies that calling
`DotNetEmitter.Emit` with a null factory throws `ArgumentNullException` before
any I/O is attempted. This scenario is tested by
`DotNetEmitter_Emit_NullFactory_ThrowsArgumentNullException`.

**GradualDisclosure format produces multiple files**: Verifies that when
`OutputFormat.GradualDisclosure` is configured the emitter produces more than one
Markdown writer. This scenario is tested by
`DotNetEmitter_Emit_GradualDisclosureFormat_ProducesMultipleFiles`.

**SingleFile format produces exactly one api file**: Verifies that when
`OutputFormat.SingleFile` is configured the emitter produces exactly one writer
keyed `api`. This scenario is tested by
`DotNetEmitter_Emit_SingleFileFormat_ProducesSingleApiFile`.

**GetNamespaceFolderPath returns the dotted name for a root namespace**: Verifies
that a namespace that is itself a configured root namespace returns its full dotted
name as the folder path. This scenario is tested by
`DotNetEmitter_GetNamespaceFolderPath_RootNamespace_ReturnsDottedName`.

**GetNamespaceFolderPath returns a slash-separated path for a child namespace**:
Verifies that a namespace that is a child of a configured root returns a
slash-separated path. This scenario is tested by
`DotNetEmitter_GetNamespaceFolderPath_ChildNamespace_ReturnsSlashSeparated`.
