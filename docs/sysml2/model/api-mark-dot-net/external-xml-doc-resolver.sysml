package ApiMarkDotNet {
    private import SoftwareItemMetadata::*;

    part def ExternalXmlDocResolver {
        doc /* ExternalXmlDocResolver locates and lazily parses XML documentation files for externally referenced assemblies, providing a member lookup used as a fallback when resolving <inheritdoc/> across assembly boundaries. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/ApiMark.DotNet/ExternalXmlDocResolver.cs");
            testFiles = ("test/ApiMark.DotNet.Tests/ExternalXmlDocResolverTests.cs");
            designFiles = ("docs/design/api-mark-dot-net/external-xml-doc-resolver.md");
            verificationFiles = ("docs/verification/api-mark-dot-net/external-xml-doc-resolver.md");
            requirementFiles = ("docs/reqstream/api-mark-dot-net/external-xml-doc-resolver.yaml");
        }
    }
}
