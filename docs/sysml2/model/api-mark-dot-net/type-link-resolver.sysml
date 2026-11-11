package ApiMarkDotNet {
    private import SoftwareItemMetadata::*;

    part def TypeLinkResolver {
        doc /* TypeLinkResolver resolves Mono.Cecil TypeReference instances to Markdown link targets. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/ApiMark.DotNet/TypeLinkResolver.cs");
            testFiles = ("test/ApiMark.DotNet.Tests/TypeLinkResolverTests.cs");
            designFiles = ("docs/design/api-mark-dot-net/type-link-resolver.md");
            verificationFiles = ("docs/verification/api-mark-dot-net/type-link-resolver.md");
            requirementFiles = ("docs/reqstream/api-mark-dot-net/type-link-resolver.yaml");
        }
    }
}
