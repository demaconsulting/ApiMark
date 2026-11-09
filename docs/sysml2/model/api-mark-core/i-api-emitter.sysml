package ApiMarkCore {
    private import SoftwareItemMetadata::*;

    part def IApiEmitter {
        doc /* IApiEmitter is the second stage of the two-stage generation pipeline: it writes Markdown output from a parsed AST model. */

        metadata info : SoftwareInfo {
            description = "IApiEmitter is the second stage of the two-stage generation pipeline: it writes Markdown output from a parsed AST model.";
            sourceFiles = ("src/ApiMark.Core/IApiEmitter.cs");
            testFiles = ("test/ApiMark.Core.Tests/IApiEmitterTests.cs");
            designFiles = ("docs/design/api-mark-core/i-api-emitter.md");
            verificationFiles = ("docs/verification/api-mark-core/i-api-emitter.md");
            requirementFiles = ("docs/reqstream/api-mark-core/i-api-emitter.yaml");
        }
    }
}
