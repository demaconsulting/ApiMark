## VhdlAstModel

### Verification Approach

`VhdlAstModel` records are verified implicitly through `VhdlAstParser` and emitter tests
that construct and consume these records. No dedicated unit tests target the model records
directly because they are plain data types with no behavioral logic of their own.

### Test Environment

Standard .NET test runner (`dotnet test`). No external tools, services, or privileged
configuration are required.

### Acceptance Criteria

- All record fields populated by `VhdlAstParser` are readable and correctly typed, as
  demonstrated indirectly by the parser and emitter tests that assert on those fields.
- No `ArgumentNullException` or invalid-cast exception is raised when model records are
  constructed and accessed by the surrounding pipeline.

### Test Scenarios

N/A - `VhdlAstModel` is validated indirectly by all tests that exercise `VhdlAstParser`
and the VHDL emitters, since those tests construct and assert on record fields. The five
`VhdlAstParser_Parse_FixtureFile_*` tests and all `VhdlEmitter*` tests serve as the
implicit verification evidence for this unit.
