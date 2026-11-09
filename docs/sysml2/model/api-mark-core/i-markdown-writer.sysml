package ApiMarkCore {
    private import SoftwareItemMetadata::*;

    part def IMarkdownWriter {
        doc /* IMarkdownWriter is the per-file Markdown writing contract obtained from an IMarkdownWriterFactory. */

        metadata info : SoftwareInfo {
            description = "IMarkdownWriter is the per-file Markdown writing contract obtained from an IMarkdownWriterFactory.";
            sourceFiles = ("src/ApiMark.Core/IMarkdownWriter.cs");
            testFiles = ("test/ApiMark.Core.Tests/IMarkdownWriterTests.cs");
            designFiles = ("docs/design/api-mark-core/i-markdown-writer.md");
            verificationFiles = ("docs/verification/api-mark-core/i-markdown-writer.md");
            requirementFiles = ("docs/reqstream/api-mark-core/i-markdown-writer.yaml");
        }
    }
}
