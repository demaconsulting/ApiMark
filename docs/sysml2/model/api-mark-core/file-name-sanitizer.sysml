package ApiMarkCore {
    part def FileNameSanitizer {
        doc /* FileNameSanitizer is the single, auditable point that converts declaration names into
              file and folder name segments that are legal on both Windows and Linux, deterministic,
              and distinct. */

        comment sourceRef /* Source: src/ApiMark.Core/FileNameSanitizer.cs */
        comment testRef /* Test: test/ApiMark.Core.Tests/FileNameSanitizerTests.cs */
        comment designRef /* Design: docs/design/api-mark-core/file-name-sanitizer.md */
        comment verificationRef /* Verification: docs/verification/api-mark-core/file-name-sanitizer.md */
        comment reqRef /* Requirements: docs/reqstream/api-mark-core/file-name-sanitizer.yaml */
    }
}
