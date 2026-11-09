package ApiMarkTool {
    private import SoftwareItemMetadata::*;

    part def Context {
        doc /* Context is the command-line argument parser and output router for ApiMarkTool. */

        metadata info : SoftwareInfo {
            description = "Context is the command-line argument parser and output router for ApiMarkTool.";
            sourceFiles = ("src/ApiMark.Tool/Cli/Context.cs");
            testFiles = ("test/ApiMark.Tool.Tests/Cli/ContextTests.cs");
            designFiles = ("docs/design/api-mark-tool/cli/context.md");
            verificationFiles = ("docs/verification/api-mark-tool/cli/context.md");
            requirementFiles = ("docs/reqstream/api-mark-tool/cli/context.yaml");
        }
    }
}
