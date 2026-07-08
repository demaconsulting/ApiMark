package ApiMarkTool {
    doc
    /*
     * ApiMarkTool is organized into two subsystems (Cli and SelfTest) and one top-level unit (Program); it is the .NET executable invoked by ApiMarkTask and directly by users or CI pipelines.
     */

    part def ApiMarkToolSystem {
        doc /* The ApiMarkTool system as a whole. */

        comment designRef /* Design: docs/design/api-mark-tool.md */
        comment verificationRef /* Verification: docs/verification/api-mark-tool.md */
        comment reqRef /* Requirements: docs/reqstream/api-mark-tool.yaml */

        part program : Program;
        part cli : CliSubsystem;
        part selfTest : SelfTestSubsystem;
    }
}
