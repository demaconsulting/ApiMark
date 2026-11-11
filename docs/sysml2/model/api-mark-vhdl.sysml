package ApiMarkVhdl {
    private import SoftwareItemMetadata::*;

    import OtsDependencies::*;

    doc
    /*
     * ApiMarkVhdl provides VHDL language support by parsing VHDL source files with an ANTLR4 grammar and emitting Markdown documentation.
     */

    part def ApiMarkVhdlSystem {
        doc /* The ApiMarkVhdl system as a whole. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/api-mark-vhdl.md");
            verificationFiles = ("docs/verification/api-mark-vhdl.md");
            requirementFiles = ("docs/reqstream/api-mark-vhdl.yaml");
        }

        part vhdlGenerator : VhdlGenerator;
        part vhdlAstModel : VhdlAstModel;
        part vhdlAstParser : VhdlAstParser;
        part vhdlEmitter : VhdlEmitter;
        part vhdlEmitterGradualDisclosure : VhdlEmitterGradualDisclosure;
        part vhdlEmitterSingleFile : VhdlEmitterSingleFile;
        part antlr4RuntimeStandard : Antlr4RuntimeStandard;
        part documentationCoverageChecker : DocumentationCoverageChecker;
    }
}
