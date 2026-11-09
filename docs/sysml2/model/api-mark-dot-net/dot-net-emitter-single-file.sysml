package ApiMarkDotNet {
    private import SoftwareItemMetadata::*;

    part def DotNetEmitterSingleFile {
        doc /* DotNetEmitterSingleFile writes all .NET API documentation into a single Markdown file. */

        metadata info : SoftwareInfo {
            description = "DotNetEmitterSingleFile writes all .NET API documentation into a single Markdown file.";
            sourceFiles = ("src/ApiMark.DotNet/DotNetEmitterSingleFile.cs");
            testFiles = ("test/ApiMark.DotNet.Tests/DotNetEmitterSingleFileTests.cs");
            designFiles = ("docs/design/api-mark-dot-net/dot-net-emitter-single-file.md");
            verificationFiles = ("docs/verification/api-mark-dot-net/dot-net-emitter-single-file.md");
            requirementFiles = ("docs/reqstream/api-mark-dot-net/dot-net-emitter-single-file.yaml");
        }
    }
}
