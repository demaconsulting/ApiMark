package ApiMarkVhdl {
    private import SoftwareItemMetadata::*;

    part def VhdlAstParser {
        doc /* VhdlAstParser reads a VHDL source file, pre-processes doc comments, and drives the ANTLR4 vhdl2008 grammar. */

        metadata info : SoftwareInfo {
            description = "VhdlAstParser reads a VHDL source file, pre-processes doc comments, and drives the ANTLR4 vhdl2008 grammar.";
            sourceFiles = ("src/ApiMark.Vhdl/VhdlAst/VhdlAstParser.cs");
            testFiles = ("test/ApiMark.Vhdl.Tests/VhdlAstParserTests.cs");
            designFiles = ("docs/design/api-mark-vhdl/vhdl-ast-parser.md");
            verificationFiles = ("docs/verification/api-mark-vhdl/vhdl-ast-parser.md");
            requirementFiles = ("docs/reqstream/api-mark-vhdl/vhdl-ast-parser.yaml");
        }
    }
}
