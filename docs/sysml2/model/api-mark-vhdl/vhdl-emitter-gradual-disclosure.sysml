package ApiMarkVhdl {
    private import SoftwareItemMetadata::*;

    part def VhdlEmitterGradualDisclosure {
        doc /* VhdlEmitterGradualDisclosure writes one Markdown file per VHDL entity and an index for gradual disclosure. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/ApiMark.Vhdl/VhdlEmitterGradualDisclosure.cs");
            testFiles = ("test/ApiMark.Vhdl.Tests/VhdlEmitterGradualDisclosureTests.cs");
            designFiles = ("docs/design/api-mark-vhdl/vhdl-emitter-gradual-disclosure.md");
            verificationFiles = ("docs/verification/api-mark-vhdl/vhdl-emitter-gradual-disclosure.md");
            requirementFiles = ("docs/reqstream/api-mark-vhdl/vhdl-emitter-gradual-disclosure.yaml");
        }
    }
}
