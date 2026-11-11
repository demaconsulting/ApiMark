package ApiMarkDotNet {
    private import SoftwareItemMetadata::*;

    part def DotNetEmitterGradualDisclosure {
        doc /* DotNetEmitterGradualDisclosure writes the complete gradual-disclosure Markdown layout for .NET API documentation. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/ApiMark.DotNet/DotNetEmitterGradualDisclosure.cs");
            testFiles = ("test/ApiMark.DotNet.Tests/DotNetEmitterGradualDisclosureTests.cs");
            designFiles = ("docs/design/api-mark-dot-net/dot-net-emitter-gradual-disclosure.md");
            verificationFiles = ("docs/verification/api-mark-dot-net/dot-net-emitter-gradual-disclosure.md");
            requirementFiles = ("docs/reqstream/api-mark-dot-net/dot-net-emitter-gradual-disclosure.yaml");
        }
    }
}
