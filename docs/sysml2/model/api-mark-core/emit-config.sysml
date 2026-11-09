package ApiMarkCore {
    private import SoftwareItemMetadata::*;

    part def EmitConfig {
        doc /* EmitConfig and OutputFormat together define the shared output-configuration contract passed to every emitter. */

        metadata info : SoftwareInfo {
            description = "EmitConfig and OutputFormat together define the shared output-configuration contract passed to every emitter.";
            sourceFiles = ("src/ApiMark.Core/EmitConfig.cs", "src/ApiMark.Core/OutputFormat.cs");
            testFiles = ("test/ApiMark.Core.Tests/EmitConfigTests.cs");
            designFiles = ("docs/design/api-mark-core/emit-config.md");
            verificationFiles = ("docs/verification/api-mark-core/emit-config.md");
            requirementFiles = ("docs/reqstream/api-mark-core/emit-config.yaml");
        }
    }
}
