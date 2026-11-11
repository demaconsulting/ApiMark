package ApiMarkVhdl {
    private import SoftwareItemMetadata::*;

    part def VhdlEmitter {
        doc /* VhdlEmitter implements IApiEmitter, validates emitter arguments, and dispatches to an emit strategy for VHDL documentation. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/ApiMark.Vhdl/VhdlEmitter.cs");
            testFiles = ("test/ApiMark.Vhdl.Tests/VhdlEmitterTests.cs");
            designFiles = ("docs/design/api-mark-vhdl/vhdl-emitter.md");
            verificationFiles = ("docs/verification/api-mark-vhdl/vhdl-emitter.md");
            requirementFiles = ("docs/reqstream/api-mark-vhdl/vhdl-emitter.yaml");
        }
    }
}
