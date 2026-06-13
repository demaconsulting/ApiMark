## VhdlEmitterSingleFile

### Verification Approach

`VhdlEmitterSingleFile` is verified with unit tests in
`test/ApiMark.Vhdl.Tests/VhdlEmitterSingleFileTests.cs` using in-memory test doubles.
A minimal `VhdlFileModel` is constructed directly without invoking the parser, and an
`InMemoryMarkdownWriterFactory` captures emitted output. Tests confirm that output is
consolidated into a single file and that the file is keyed correctly.

### Test Environment

Standard .NET test runner (`dotnet test`). No external tools, services, or privileged
configuration are required.

### Acceptance Criteria

- `VhdlEmitterSingleFile.Emit` creates exactly one writer in the writer factory output,
  confirming that all content is consolidated into a single Markdown file.
- The single output file is keyed as `"api"`, confirming it is the expected api file.

### Test Scenarios

**Creates exactly one writer**: Verifies that emitting minimal VHDL model data results in
exactly one writer being created by the factory, confirming that the single-file emitter does
not produce per-entity files.
This scenario is tested by `VhdlEmitterSingleFile_Emit_MinimalData_CreatesExactlyOneWriter`.

**Creates api file only**: Verifies that the single writer created is keyed as `"api"`,
confirming that the output file name matches the expected api entrypoint.
This scenario is tested by `VhdlEmitterSingleFile_Emit_MinimalData_CreatesApiFileOnly`.
