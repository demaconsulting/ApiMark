package ApiMarkTool_Validation {
    private import SoftwareItemMetadata::*;

    part def Validation {
        doc /* Validation runs in-process self-tests that run the tool end to end on sample sources it writes itself, asserting exact exit codes, reports, and generated Markdown, and reports results via DemaConsulting.TestResults. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/ApiMark.Tool/SelfTest/Validation.cs", "src/ApiMark.Tool/SelfTest/Validation.DotNet.cs", "src/ApiMark.Tool/SelfTest/Validation.DotNetSample.cs", "src/ApiMark.Tool/SelfTest/Validation.Cpp.cs", "src/ApiMark.Tool/SelfTest/Validation.Vhdl.cs", "src/ApiMark.Tool/SelfTest/Validation.EnforceDocs.DotNet.cs", "src/ApiMark.Tool/SelfTest/Validation.EnforceDocs.Cpp.cs", "src/ApiMark.Tool/SelfTest/Validation.EnforceDocs.Vhdl.cs");
            testFiles = ("test/ApiMark.Tool.Tests/SelfTest/ValidationTests.cs");
            designFiles = ("docs/design/api-mark-tool/self-test/validation.md");
            verificationFiles = ("docs/verification/api-mark-tool/self-test/validation.md");
            requirementFiles = ("docs/reqstream/api-mark-tool/self-test/validation.yaml");
        }
    }
}
