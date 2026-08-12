package ApiMarkCore {
    part def IDocumentationCoverageCapable {
        doc /* IDocumentationCoverageCapable is an optional capability interface implemented by language generators that support documentation-coverage enforcement, letting ApiMarkTool invoke enforcement without downcasting to a specific language generator type. The shared DocumentationCoverageResult/UndocumentedApiItem result types are intentionally modeled as part of this interface rather than as separate part definitions, since they exist solely as the return shape of CheckDocumentationCoverage and have no independent identity or behavior of their own. */

        comment sourceRef /* Source: src/ApiMark.Core/IDocumentationCoverageCapable.cs, src/ApiMark.Core/DocumentationCoverageResult.cs */
        comment testRef /* Test: test/ApiMark.Core.Tests/IDocumentationCoverageCapableTests.cs */
        comment designRef /* Design: docs/design/api-mark-core/i-documentation-coverage-capable.md */
        comment verificationRef /* Verification: docs/verification/api-mark-core/i-documentation-coverage-capable.md */
        comment reqRef /* Requirements: docs/reqstream/api-mark-core/i-documentation-coverage-capable.yaml */
    }
}
