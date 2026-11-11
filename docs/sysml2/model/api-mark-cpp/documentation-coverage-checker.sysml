package ApiMarkCpp {
    private import SoftwareItemMetadata::*;

    part def DocumentationCoverageChecker {
        doc /* DocumentationCoverageChecker scans parsed C++ namespace declarations for classes, functions, fields, enums, and type aliases lacking a Doxygen summary, at a caller-supplied visibility tier independent of the emission visibility tier. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/ApiMark.Cpp/DocumentationCoverageChecker.cs");
            testFiles = ("test/ApiMark.Cpp.Tests/DocumentationCoverageCheckerTests.cs");
            designFiles = ("docs/design/api-mark-cpp/documentation-coverage-checker.md");
            verificationFiles = ("docs/verification/api-mark-cpp/documentation-coverage-checker.md");
            requirementFiles = ("docs/reqstream/api-mark-cpp/documentation-coverage-checker.yaml");
        }
    }
}
