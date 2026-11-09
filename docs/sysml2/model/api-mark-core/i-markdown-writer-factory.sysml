package ApiMarkCore {
    private import SoftwareItemMetadata::*;

    part def IMarkdownWriterFactory {
        doc /* IMarkdownWriterFactory is the factory contract for creating IMarkdownWriter instances rooted at an output directory. */

        metadata info : SoftwareInfo {
            description = "IMarkdownWriterFactory is the factory contract for creating IMarkdownWriter instances rooted at an output directory.";
            sourceFiles = ("src/ApiMark.Core/IMarkdownWriterFactory.cs");
            testFiles = ("test/ApiMark.Core.Tests/IMarkdownWriterFactoryTests.cs");
            designFiles = ("docs/design/api-mark-core/i-markdown-writer-factory.md");
            verificationFiles = ("docs/verification/api-mark-core/i-markdown-writer-factory.md");
            requirementFiles = ("docs/reqstream/api-mark-core/i-markdown-writer-factory.yaml");
        }
    }
}
