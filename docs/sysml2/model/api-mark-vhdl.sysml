package ApiMarkVhdl {
    import OtsDependencies::*;

    doc
    /*
     * ApiMarkVhdl provides VHDL language support by parsing VHDL source files with an ANTLR4 grammar and emitting Markdown documentation.
     */

    part def ApiMarkVhdlSystem {
        doc /* The ApiMarkVhdl system as a whole. */

        comment designRef /* Design: docs/design/api-mark-vhdl.md */
        comment verificationRef /* Verification: docs/verification/api-mark-vhdl.md */
        comment reqRef /* Requirements: docs/reqstream/api-mark-vhdl.yaml */

        part vhdlGenerator : VhdlGenerator;
        part vhdlAstModel : VhdlAstModel;
        part vhdlAstParser : VhdlAstParser;
        part vhdlEmitter : VhdlEmitter;
        part vhdlEmitterGradualDisclosure : VhdlEmitterGradualDisclosure;
        part vhdlEmitterSingleFile : VhdlEmitterSingleFile;
        part antlr4RuntimeStandard : Antlr4RuntimeStandard;
    }
}
