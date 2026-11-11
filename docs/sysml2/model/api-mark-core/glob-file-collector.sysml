package ApiMarkCore {
    private import SoftwareItemMetadata::*;

    part def GlobFileCollector {
        doc /* GlobFileCollector is a stateless utility that discovers source files from glob patterns for filesystem-based generators. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/ApiMark.Core/GlobFileCollector.cs");
            testFiles = ("test/ApiMark.Core.Tests/GlobFileCollectorTests.cs");
            designFiles = ("docs/design/api-mark-core/glob-file-collector.md");
            verificationFiles = ("docs/verification/api-mark-core/glob-file-collector.md");
            requirementFiles = ("docs/reqstream/api-mark-core/glob-file-collector.yaml");
        }
    }
}
