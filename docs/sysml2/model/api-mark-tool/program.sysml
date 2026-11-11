package ApiMarkTool {
    private import SoftwareItemMetadata::*;

    part def Program {
        doc /* Program is the CLI entry point for ApiMarkTool; it parses command-line arguments and dispatches to the Cli and SelfTest subsystems. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/ApiMark.Tool/Program.cs");
            testFiles = ("test/ApiMark.Tool.Tests/ProgramTests.cs");
            designFiles = ("docs/design/api-mark-tool/program.md");
            verificationFiles = ("docs/verification/api-mark-tool/program.md");
            requirementFiles = ("docs/reqstream/api-mark-tool/program.yaml");
        }
    }
}
