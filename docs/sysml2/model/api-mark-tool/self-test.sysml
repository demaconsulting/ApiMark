package ApiMarkTool_SelfTestSubsystem {
    public import ApiMarkTool_Validation::*;
    private import OtsDependencies::*;
    private import SoftwareItemMetadata::*;

    part def SelfTestSubsystem {
        doc /* SelfTest subsystem: runs in-process self-validation tests to verify core ApiMarkTool functionality. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/api-mark-tool/self-test.md");
            verificationFiles = ("docs/verification/api-mark-tool/self-test.md");
            requirementFiles = ("docs/reqstream/api-mark-tool/self-test.yaml");
        }

        part validation : Validation;
        part testResults : TestResults;
    }
}
