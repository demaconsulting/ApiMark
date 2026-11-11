package ApiMarkVhdl {
    private import SoftwareItemMetadata::*;

    part def VhdlGenerator {
        doc /* VhdlGenerator is the public entry point for VHDL API documentation generation. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/ApiMark.Vhdl/VhdlGenerator.cs", "src/ApiMark.Vhdl/VhdlGeneratorOptions.cs");
            testFiles = ("test/ApiMark.Vhdl.Tests/VhdlGeneratorTests.cs");
            designFiles = ("docs/design/api-mark-vhdl/vhdl-generator.md");
            verificationFiles = ("docs/verification/api-mark-vhdl/vhdl-generator.md");
            requirementFiles = ("docs/reqstream/api-mark-vhdl/vhdl-generator.yaml");
        }
    }
}
