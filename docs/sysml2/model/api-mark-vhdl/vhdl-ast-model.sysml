package ApiMarkVhdl {
    private import SoftwareItemMetadata::*;

    part def VhdlAstModel {
        doc /* VhdlAstModel defines the immutable record types that represent the parsed result of a VHDL source file. */

        metadata info : SoftwareInfo {
            description = "VhdlAstModel defines the immutable record types that represent the parsed result of a VHDL source file.";
            sourceFiles = ("src/ApiMark.Vhdl/VhdlAst/VhdlAstModel.cs");
            designFiles = ("docs/design/api-mark-vhdl/vhdl-ast-model.md");
            verificationFiles = ("docs/verification/api-mark-vhdl/vhdl-ast-model.md");
            requirementFiles = ("docs/reqstream/api-mark-vhdl/vhdl-ast-model.yaml");
        }
    }
}
