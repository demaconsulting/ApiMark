package ApiMarkTool_HelpCommand {
    private import OtsDependencies::*;
    private import SoftwareItemMetadata::*;

    part def HelpCommand {
        doc /* HelpCommand answers the apimark help command from the help cards embedded in the tool assembly, before the options are parsed. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/ApiMark.Tool/Cli/HelpCommand.cs");
            testFiles = ("test/ApiMark.Tool.Tests/Cli/HelpCommandTests.cs");
            designFiles = ("docs/design/api-mark-tool/cli/help-command.md");
            verificationFiles = ("docs/verification/api-mark-tool/cli/help-command.md");
            requirementFiles = ("docs/reqstream/api-mark-tool/cli/help-command.yaml");
        }
    }

    // Serves the help command, validates the cards, and runs their examples through HelpCards
    dependency HelpCommand to HelpCards;
}
