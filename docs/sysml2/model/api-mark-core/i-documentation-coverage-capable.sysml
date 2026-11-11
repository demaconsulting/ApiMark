package ApiMarkCore {
    private import SoftwareItemMetadata::*;

    part def IDocumentationCoverageCapable {
        doc /* IDocumentationCoverageCapable is an optional capability interface implemented by language generators that support documentation-coverage enforcement, letting ApiMarkTool invoke enforcement without downcasting to a specific language generator type. The shared DocumentationCoverageResult/UndocumentedApiItem result types are intentionally modeled as part of this interface rather than as separate part definitions, since they exist solely as the return shape of CheckDocumentationCoverage and have no independent identity or behavior of their own. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/ApiMark.Core/IDocumentationCoverageCapable.cs", "src/ApiMark.Core/DocumentationCoverageResult.cs");
            testFiles = ("test/ApiMark.Core.Tests/IDocumentationCoverageCapableTests.cs");
            designFiles = ("docs/design/api-mark-core/i-documentation-coverage-capable.md");
            verificationFiles = ("docs/verification/api-mark-core/i-documentation-coverage-capable.md");
            requirementFiles = ("docs/reqstream/api-mark-core/i-documentation-coverage-capable.yaml");
        }
    }
}
