package ApiMarkDotNet {
    private import SoftwareItemMetadata::*;

    part def XmlDocReader {
        doc /* XmlDocReader reads and indexes a .NET XML documentation file for fast lookup of member documentation. */

        metadata info : SoftwareInfo {
            description = "XmlDocReader reads and indexes a .NET XML documentation file for fast lookup of member documentation.";
            sourceFiles = ("src/ApiMark.DotNet/XmlDocReader.cs");
            testFiles = ("test/ApiMark.DotNet.Tests/XmlDocReaderTests.cs");
            designFiles = ("docs/design/api-mark-dot-net/xml-doc-reader.md");
            verificationFiles = ("docs/verification/api-mark-dot-net/xml-doc-reader.md");
            requirementFiles = ("docs/reqstream/api-mark-dot-net/xml-doc-reader.yaml");
        }
    }
}
