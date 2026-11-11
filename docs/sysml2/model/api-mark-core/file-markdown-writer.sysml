package ApiMarkCore {
    private import SoftwareItemMetadata::*;

    part def FileMarkdownWriter {
        doc /* FileMarkdownWriter is the production file-system implementation of IMarkdownWriter. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/ApiMark.Core/FileMarkdownWriter.cs");
            testFiles = ("test/ApiMark.Core.Tests/FileMarkdownWriterTests.cs");
            designFiles = ("docs/design/api-mark-core/file-markdown-writer.md");
            verificationFiles = ("docs/verification/api-mark-core/file-markdown-writer.md");
            requirementFiles = ("docs/reqstream/api-mark-core/file-markdown-writer.yaml");
        }
    }
}
