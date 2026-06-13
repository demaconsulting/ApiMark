# VhdlAstModel

## Verification Approach

VhdlAstModel records are verified implicitly through `VhdlAstParser` and emitter tests
that construct and consume these records.

## Test Scenarios

VhdlAstModel is validated indirectly by all tests that exercise `VhdlAstParser` and
the VHDL emitters, since those tests construct and assert on record fields.
