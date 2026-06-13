## VhdlEmitter

### Verification Approach

`VhdlEmitter` is verified with unit tests in `test/ApiMark.Vhdl.Tests/VhdlEmitterTests.cs`.
The test confirms that the emitter validates its arguments before delegating to the format-specific
implementation, and that passing a null factory produces a clear exception rather than a
deferred null-reference failure.

### Test Environment

Standard .NET test runner (`dotnet test`). No external tools, services, or privileged
configuration are required.

### Acceptance Criteria

- `VhdlEmitter.Emit` with a null factory argument throws `ArgumentNullException` immediately,
  before any writer or pipeline logic is invoked.

### Test Scenarios

**Null factory throws ArgumentNullException**: Verifies that passing `null` as the factory
argument to `VhdlEmitter.Emit` throws `ArgumentNullException`, providing a clear error rather
than a null-reference failure during writer creation.
This scenario is tested by `VhdlEmitter_Emit_NullFactory_ThrowsArgumentNullException`.
