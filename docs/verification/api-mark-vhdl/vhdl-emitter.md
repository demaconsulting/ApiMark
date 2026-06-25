## VhdlEmitter

### Verification Approach

`VhdlEmitter` is verified with unit tests in `test/ApiMark.Vhdl.Tests/VhdlEmitterTests.cs`.
Tests confirm that the emitter validates its arguments, dispatches correctly to each
format-specific implementation, and returns early without output when no file models
are present.

### Test Environment

Standard .NET test runner (`dotnet test`). No external tools, services, or privileged
configuration are required.

### Acceptance Criteria

- `VhdlEmitter.Emit` with a null factory argument throws `ArgumentNullException` immediately,
  before any writer or pipeline logic is invoked.
- `VhdlEmitter.Emit` with the default (`GradualDisclosure`) format and at least one file model
  produces more than one output writer.
- `VhdlEmitter.Emit` with `SingleFile` format produces exactly one writer keyed `"api"`.
- `VhdlEmitter.Emit` with an empty file models list produces no output writers.

### Test Scenarios

**Null factory throws ArgumentNullException**: Verifies that passing `null` as the factory
argument to `VhdlEmitter.Emit` throws `ArgumentNullException`, providing a clear error rather
than a null-reference failure during writer creation.
This scenario is tested by `VhdlEmitter_Emit_NullFactory_ThrowsArgumentNullException`.

**GradualDisclosure format dispatches and produces multiple files**: Verifies that the default
format produces more than one output writer — the api index page and at least one entity page —
confirming that dispatch to `VhdlEmitterGradualDisclosure` is working.
This scenario is tested by `VhdlEmitter_Emit_GradualDisclosureFormat_ProducesMultipleOutputFiles`.

**SingleFile format dispatches and produces exactly one file**: Verifies that `SingleFile`
format produces exactly one writer keyed `"api"`, confirming dispatch to
`VhdlEmitterSingleFile`.
This scenario is tested by `VhdlEmitter_Emit_SingleFileFormat_ProducesSingleOutputFile`.

**Empty file models produces no output**: Verifies that when no file models are present,
`Emit` returns early and no writers are created by the factory.
This scenario is tested by `VhdlEmitter_Emit_EmptyFileModels_ProducesNoOutput`.
