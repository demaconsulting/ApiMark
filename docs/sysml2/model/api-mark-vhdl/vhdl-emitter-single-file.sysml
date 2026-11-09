package ApiMarkVhdl {
    private import SoftwareItemMetadata::*;

    part def VhdlEmitterSingleFile {
        doc /* VhdlEmitterSingleFile writes all VHDL API documentation into a single api.md file. */

        metadata info : SoftwareInfo {
            description = "VhdlEmitterSingleFile writes all VHDL API documentation into a single api.md file.";
            sourceFiles = ("src/ApiMark.Vhdl/VhdlEmitterSingleFile.cs");
            testFiles = ("test/ApiMark.Vhdl.Tests/VhdlEmitterSingleFileTests.cs");
            designFiles = ("docs/design/api-mark-vhdl/vhdl-emitter-single-file.md");
            verificationFiles = ("docs/verification/api-mark-vhdl/vhdl-emitter-single-file.md");
            requirementFiles = ("docs/reqstream/api-mark-vhdl/vhdl-emitter-single-file.yaml");
        }
    }
}
