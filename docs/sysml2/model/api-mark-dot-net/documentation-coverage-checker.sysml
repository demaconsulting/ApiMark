package ApiMarkDotNet {
    private import SoftwareItemMetadata::*;

    part def DocumentationCoverageChecker {
        doc /* DocumentationCoverageChecker scans a parsed .NET assembly for types and members lacking an XML doc summary, at a caller-supplied visibility tier independent of the emission visibility tier. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/ApiMark.DotNet/DocumentationCoverageChecker.cs");
            testFiles = ("test/ApiMark.DotNet.Tests/DocumentationCoverageCheckerTests.cs");
            designFiles = ("docs/design/api-mark-dot-net/documentation-coverage-checker.md");
            verificationFiles = ("docs/verification/api-mark-dot-net/documentation-coverage-checker.md");
            requirementFiles = ("docs/reqstream/api-mark-dot-net/documentation-coverage-checker.yaml");
        }
    }
}
