package ApiMarkVhdl {
    part def DocumentationCoverageChecker {
        doc /* DocumentationCoverageChecker scans entities, entity ports/generics, architectures (own summary only), and packages (and their exported types/constants/components/subprograms) for a missing documentation summary. Public-interface-only: architecture internals are not parsed into the AST model and are a deferred future enhancement. CLI-only: no MSBuild support. */

        comment sourceRef /* Source: src/ApiMark.Vhdl/DocumentationCoverageChecker.cs */
        comment testRef /* Test: test/ApiMark.Vhdl.Tests/DocumentationCoverageCheckerTests.cs */
        comment designRef /* Design: docs/design/api-mark-vhdl/documentation-coverage-checker.md */
        comment verificationRef /* Verification: docs/verification/api-mark-vhdl/documentation-coverage-checker.md */
        comment reqRef /* Requirements: docs/reqstream/api-mark-vhdl/documentation-coverage-checker.yaml */
    }
}
