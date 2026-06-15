## ANTLR4

### Verification Approach

ANTLR4 is verified in ApiMark indirectly through the `VhdlAstParser` unit tests
in `test/ApiMark.Vhdl.Tests/`. Because ANTLR4 was used once to generate
committed C# source files, there is no ongoing tool dependency to verify. The
`Antlr4.Runtime.Standard` runtime library is exercised by every test that
invokes the parser.

Any defect in the generated parser code — whether introduced at generation time
or by a manual edit — would cause one or more of the VhdlAstParser tests to
fail, making the test suite the effective acceptance gate for the generated
output.

### Test Scenarios

**Entity declarations are parsed from VHDL-2008 source**: Verifies that the
generated lexer and parser can process a representative VHDL-2008 fixture file
and produce at least one entity declaration in the resulting model. This
scenario is tested by `VhdlAstParser_Parse_FixtureFile_ReturnsEntity`.

**Generic and port declarations are extracted correctly**: Verifies that the
parser correctly identifies generic parameters and port declarations within an
entity, confirming that the grammar's structural rules are correctly represented
in the generated code. This scenario is tested by
`VhdlAstParser_Parse_FixtureFile_EntityHasGenerics` and
`VhdlAstParser_Parse_FixtureFile_EntityHasPorts`.

**Token positions support doc-comment extraction**: Verifies that the token
stream produced by the generated lexer contains the line-number information
needed for `VhdlAstParser` to associate `--!` doc comments with the correct
declarations. This scenario is tested by
`VhdlAstParser_Parse_FixtureFile_EntityDocCommentParsed` and
`VhdlAstParser_Parse_FixtureFile_PortsHaveInlineDocComments`.
