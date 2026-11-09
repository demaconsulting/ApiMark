package ApiMarkDotNet {
    private import SoftwareItemMetadata::*;

    part def DotNetAstModel {
        doc /* DotNetAstModel is an immutable data class holding all parsed .NET assembly metadata. */

        metadata info : SoftwareInfo {
            description = "DotNetAstModel is an immutable data class holding all parsed .NET assembly metadata.";
            sourceFiles = ("src/ApiMark.DotNet/DotNetAstModel.cs");
            testFiles = ("test/ApiMark.DotNet.Tests/DotNetAstModelTests.cs");
            designFiles = ("docs/design/api-mark-dot-net/dot-net-ast-model.md");
            verificationFiles = ("docs/verification/api-mark-dot-net/dot-net-ast-model.md");
            requirementFiles = ("docs/reqstream/api-mark-dot-net/dot-net-ast-model.yaml");
        }
    }
}
