package ApiMarkDotNet {
    private import SoftwareItemMetadata::*;

    part def CrefTargetResolver {
        doc /* CrefTargetResolver resolves a raw XML-doc cref attribute string to the Mono.Cecil type or member it names within a single, specific assembly. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/ApiMark.DotNet/CrefTargetResolver.cs");
            testFiles = ("test/ApiMark.DotNet.Tests/CrefTargetResolverTests.cs");
            designFiles = ("docs/design/api-mark-dot-net/cref-target-resolver.md");
            verificationFiles = ("docs/verification/api-mark-dot-net/cref-target-resolver.md");
            requirementFiles = ("docs/reqstream/api-mark-dot-net/cref-target-resolver.yaml");
        }
    }
}
