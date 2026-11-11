package ApiMarkTool {
    private import SoftwareItemMetadata::*;

    doc
    /*
     * ApiMarkTool is organized into two subsystems (Cli and SelfTest) and one top-level unit (Program); it is the .NET executable invoked by ApiMarkTask and directly by users or CI pipelines.
     */

    part def ApiMarkToolSystem {
        doc /* The ApiMarkTool system as a whole. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/api-mark-tool.md");
            verificationFiles = ("docs/verification/api-mark-tool.md");
            requirementFiles = ("docs/reqstream/api-mark-tool.yaml");
        }

        part program : Program;
        part cli : CliSubsystem;
        part selfTest : SelfTestSubsystem;
    }
}
