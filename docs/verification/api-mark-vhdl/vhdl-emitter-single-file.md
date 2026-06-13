# VhdlEmitterSingleFile

## Verification Approach

VhdlEmitterSingleFile is verified with unit tests in
`test/ApiMark.Vhdl.Tests/VhdlEmitterSingleFileTests.cs` using in-memory test doubles.

## Test Scenarios

**Creates exactly one writer**: Tested by
`VhdlEmitterSingleFile_Emit_MinimalData_CreatesExactlyOneWriter`.

**Creates api file only**: Tested by
`VhdlEmitterSingleFile_Emit_MinimalData_CreatesApiFileOnly`.
