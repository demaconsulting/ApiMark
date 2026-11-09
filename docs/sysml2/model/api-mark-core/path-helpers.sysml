package ApiMarkCore {
    private import SoftwareItemMetadata::*;

    part def PathHelpers {
        doc /* PathHelpers is the single, auditable point for path-safety enforcement (combining
              and validating output paths) and for resolving a path to its actual on-disk casing
              so differently-cased spellings of the same file/directory compare as identical. */

        metadata info : SoftwareInfo {
            description = "PathHelpers is the single, auditable point for path-safety enforcement (combining and validating output paths) and for resolving a path to its actual on-disk casing so differently-cased spellings of the same file/directory compare as identical.";
            sourceFiles = ("src/ApiMark.Core/PathHelpers.cs");
            testFiles = ("test/ApiMark.Core.Tests/PathHelpersTests.cs");
            designFiles = ("docs/design/api-mark-core/path-helpers.md");
            verificationFiles = ("docs/verification/api-mark-core/path-helpers.md");
            requirementFiles = ("docs/reqstream/api-mark-core/path-helpers.yaml");
        }
    }
}
