package ApiMarkCore {
    private import SoftwareItemMetadata::*;

    part def IContext {
        doc /* IContext is the minimal output channel that language generators use to emit Markdown writers and diagnostics. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/ApiMark.Core/IContext.cs");
            testFiles = ("test/ApiMark.Core.Tests/IContextTests.cs");
            designFiles = ("docs/design/api-mark-core/i-context.md");
            verificationFiles = ("docs/verification/api-mark-core/i-context.md");
            requirementFiles = ("docs/reqstream/api-mark-core/i-context.yaml");
        }
    }
}
