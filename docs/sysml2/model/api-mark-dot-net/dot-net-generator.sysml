package ApiMarkDotNet {
    private import SoftwareItemMetadata::*;

    part def DotNetGenerator {
        doc /* DotNetGenerator implements IApiGenerator for C#/.NET assemblies, reading a compiled assembly with Mono.Cecil. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/ApiMark.DotNet/DotNetGenerator.cs");
            testFiles = ("test/ApiMark.DotNet.Tests/DotNetGeneratorTests.cs");
            designFiles = ("docs/design/api-mark-dot-net/dot-net-generator.md");
            verificationFiles = ("docs/verification/api-mark-dot-net/dot-net-generator.md");
            requirementFiles = ("docs/reqstream/api-mark-dot-net/dot-net-generator.yaml");
        }
    }
}
