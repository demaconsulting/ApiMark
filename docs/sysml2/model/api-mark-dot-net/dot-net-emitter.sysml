package ApiMarkDotNet {
    private import SoftwareItemMetadata::*;

    part def DotNetEmitter {
        doc /* DotNetEmitter implements IApiEmitter and dispatches between the gradual-disclosure and single-file emitters. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/ApiMark.DotNet/DotNetEmitter.cs");
            testFiles = ("test/ApiMark.DotNet.Tests/DotNetEmitterTests.cs");
            designFiles = ("docs/design/api-mark-dot-net/dot-net-emitter.md");
            verificationFiles = ("docs/verification/api-mark-dot-net/dot-net-emitter.md");
            requirementFiles = ("docs/reqstream/api-mark-dot-net/dot-net-emitter.yaml");
        }
    }
}
