package ApiMarkTool {
    private import SoftwareItemMetadata::*;

    part def CliSubsystem {
        doc /* Cli subsystem: command-line argument parsing and I/O ownership for ApiMarkTool. */

        metadata info : SoftwareInfo {
            description = "Cli subsystem: command-line argument parsing and I/O ownership for ApiMarkTool.";
            designFiles = ("docs/design/api-mark-tool/cli.md");
            verificationFiles = ("docs/verification/api-mark-tool/cli.md");
            requirementFiles = ("docs/reqstream/api-mark-tool/cli.yaml");
        }

        part context : Context;
    }
}
