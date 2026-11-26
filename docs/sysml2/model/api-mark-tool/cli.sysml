package ApiMarkTool_CliSubsystem {
    public import ApiMarkTool_Context::*;
    public import ApiMarkTool_HelpCommand::*;
    private import OtsDependencies::*;
    private import SoftwareItemMetadata::*;

    part def CliSubsystem {
        doc /* Cli subsystem: command-line argument parsing, I/O ownership, and the built-in help command for ApiMarkTool. */

        metadata info : SoftwareInfo {
            testFiles = ("test/ApiMark.Tool.Tests/Cli/CliSubsystemTests.cs");
            designFiles = ("docs/design/api-mark-tool/cli.md");
            verificationFiles = ("docs/verification/api-mark-tool/cli.md");
            requirementFiles = ("docs/reqstream/api-mark-tool/cli.yaml");
        }

        part context : Context;
        part helpCommand : HelpCommand;
        part helpCards : HelpCards;
    }
}
