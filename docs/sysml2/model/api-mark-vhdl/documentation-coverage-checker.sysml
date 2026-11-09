package ApiMarkVhdl {
    private import SoftwareItemMetadata::*;

    part def DocumentationCoverageChecker {
        doc /* DocumentationCoverageChecker scans entities, entity ports/generics, architectures (own summary only), and packages (and their exported types/constants/components/subprograms) for a missing documentation summary. Public-interface-only: architecture internals are not parsed into the AST model and are a deferred future enhancement. CLI-only: no MSBuild support. */

        metadata info : SoftwareInfo {
            description = "DocumentationCoverageChecker scans entities, entity ports/generics, architectures (own summary only), and packages (and their exported types/constants/components/subprograms) for a missing documentation summary. Public-interface-only: architecture internals are not parsed into the AST model and are a deferred future enhancement. CLI-only: no MSBuild support.";
            sourceFiles = ("src/ApiMark.Vhdl/DocumentationCoverageChecker.cs");
            testFiles = ("test/ApiMark.Vhdl.Tests/DocumentationCoverageCheckerTests.cs");
            designFiles = ("docs/design/api-mark-vhdl/documentation-coverage-checker.md");
            verificationFiles = ("docs/verification/api-mark-vhdl/documentation-coverage-checker.md");
            requirementFiles = ("docs/reqstream/api-mark-vhdl/documentation-coverage-checker.yaml");
        }
    }
}
