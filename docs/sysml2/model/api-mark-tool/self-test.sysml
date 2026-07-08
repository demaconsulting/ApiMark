package ApiMarkTool {
    import OtsDependencies::*;

    part def SelfTestSubsystem {
        doc /* SelfTest subsystem: runs in-process self-validation tests to verify core ApiMarkTool functionality. */

        comment designRef /* Design: docs/design/api-mark-tool/self-test.md */
        comment verificationRef /* Verification: docs/verification/api-mark-tool/self-test.md */
        comment reqRef /* Requirements: docs/reqstream/api-mark-tool/self-test.yaml */

        part validation : Validation;
        part testResults : TestResults;
    }
}
