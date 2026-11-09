package ApiMarkTool {
    private import SoftwareItemMetadata::*;

    part def Validation {
        doc /* Validation runs in-process self-tests that confirm core ApiMarkTool functionality and reports results via DemaConsulting.TestResults. */

        metadata info : SoftwareInfo {
            description = "Validation runs in-process self-tests that confirm core ApiMarkTool functionality and reports results via DemaConsulting.TestResults.";
            sourceFiles = ("src/ApiMark.Tool/SelfTest/Validation.cs");
            testFiles = ("test/ApiMark.Tool.Tests/SelfTest/ValidationTests.cs");
            designFiles = ("docs/design/api-mark-tool/self-test/validation.md");
            verificationFiles = ("docs/verification/api-mark-tool/self-test/validation.md");
            requirementFiles = ("docs/reqstream/api-mark-tool/self-test/validation.yaml");
        }
    }
}
