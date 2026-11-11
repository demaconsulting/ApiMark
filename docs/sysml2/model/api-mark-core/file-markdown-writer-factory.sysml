package ApiMarkCore {
    private import SoftwareItemMetadata::*;

    part def FileMarkdownWriterFactory {
        doc /* FileMarkdownWriterFactory is the production file-system implementation of IMarkdownWriterFactory. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/ApiMark.Core/FileMarkdownWriterFactory.cs");
            testFiles = ("test/ApiMark.Core.Tests/FileMarkdownWriterFactoryTests.cs");
            designFiles = ("docs/design/api-mark-core/file-markdown-writer-factory.md");
            verificationFiles = ("docs/verification/api-mark-core/file-markdown-writer-factory.md");
            requirementFiles = ("docs/reqstream/api-mark-core/file-markdown-writer-factory.yaml");
        }
    }
}
