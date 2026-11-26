package ApiMarkTool {
    public import ApiMarkTool_CliSubsystem::*;
    public import ApiMarkTool_Program::*;
    public import ApiMarkTool_SelfTestSubsystem::*;
    private import SoftwareItemMetadata::*;

    doc
    /*
     * ApiMarkTool is organized into two subsystems (Cli and SelfTest) and one top-level unit (Program); it is the .NET executable invoked by ApiMarkTask and directly by users or CI pipelines, and it answers the built-in help command from embedded help cards.
     */

    part def ApiMarkToolSystem {
        doc /* The ApiMarkTool system as a whole. */

        metadata info : SoftwareInfo {
            testFiles = ("test/ApiMark.Tool.Tests/IntegrationTests.cs");
            designFiles = ("docs/design/api-mark-tool.md");
            verificationFiles = ("docs/verification/api-mark-tool.md");
            requirementFiles = ("docs/reqstream/api-mark-tool.yaml");
        }

        part program : Program;
        part cli : CliSubsystem;
        part selfTest : SelfTestSubsystem;
    }
}
