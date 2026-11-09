package ApiMarkCore {
    private import SoftwareItemMetadata::*;

    part def IApiGenerator {
        doc /* IApiGenerator is the first stage of the two-stage generation pipeline: it parses source input into a language AST model. */

        metadata info : SoftwareInfo {
            description = "IApiGenerator is the first stage of the two-stage generation pipeline: it parses source input into a language AST model.";
            sourceFiles = ("src/ApiMark.Core/IApiGenerator.cs");
            testFiles = ("test/ApiMark.Core.Tests/IApiGeneratorTests.cs");
            designFiles = ("docs/design/api-mark-core/i-api-generator.md");
            verificationFiles = ("docs/verification/api-mark-core/i-api-generator.md");
            requirementFiles = ("docs/reqstream/api-mark-core/i-api-generator.yaml");
        }
    }
}
