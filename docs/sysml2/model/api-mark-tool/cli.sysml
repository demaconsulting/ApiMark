package ApiMarkTool {
    part def CliSubsystem {
        doc /* Cli subsystem: command-line argument parsing and I/O ownership for ApiMarkTool. */

        comment designRef /* Design: docs/design/api-mark-tool/cli.md */
        comment verificationRef /* Verification: docs/verification/api-mark-tool/cli.md */
        comment reqRef /* Requirements: docs/reqstream/api-mark-tool/cli.yaml */

        part context : Context;
    }
}
