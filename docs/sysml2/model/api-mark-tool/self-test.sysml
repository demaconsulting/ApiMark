package ApiMarkTool {
    private import SoftwareItemMetadata::*;

    import OtsDependencies::*;

    part def SelfTestSubsystem {
        doc /* SelfTest subsystem: runs in-process self-validation tests to verify core ApiMarkTool functionality. */

        metadata info : SoftwareInfo {
            description = "SelfTest subsystem: runs in-process self-validation tests to verify core ApiMarkTool functionality.";
            designFiles = ("docs/design/api-mark-tool/self-test.md");
            verificationFiles = ("docs/verification/api-mark-tool/self-test.md");
            requirementFiles = ("docs/reqstream/api-mark-tool/self-test.yaml");
        }

        part validation : Validation;
        part testResults : TestResults;
    }
}
