## PathHelpers

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

PathHelpers is the single, auditable point of path-safety enforcement for combining
user-supplied path segments. It centralizes the validation needed to prevent
directory-traversal vulnerabilities so callers do not re-implement the check
independently.

### Data Model

PathHelpers is an `internal static` utility with no fields or properties. All behavior
is stateless and thread-safe.

### Key Methods

**PathHelpers.SafePathCombine**: Safely combines a base path with one or more validated
relative path segments.

- *Parameters*: `string basePath` — trusted base directory. `params string[] relativePaths`
  — one or more caller-supplied relative path segments appended in order.
- *Returns*: `string` — the combined path.
- *Preconditions*: `basePath` must not be null. `relativePaths` must not be null. Each
  segment must not be null, must not be rooted, and must not contain `..`.
- *Postconditions*: The returned path is constructed by appending each validated segment
  to `basePath` in order and remains within the normalized base path.
- *Throws*: `ArgumentNullException` when `basePath`, `relativePaths`, or any individual
  segment is null. `ArgumentException` when any segment is rooted, contains `..`, or
  normalizes outside the base path.

### Error Handling

PathHelpers applies two validation layers for each caller-supplied segment:

1. An upfront string validation rejects any segment that contains `..` or for which
   `Path.IsPathRooted` returns `true`.
2. A defense-in-depth validation uses `Path.GetFullPath` and `Path.GetRelativePath` to
   confirm the normalized combined path still resolves under the normalized base path.

After validation, the method uses `Path.Join` rather than `Path.Combine` so a rooted
segment cannot silently replace the trusted base path.

### Dependencies

N/A - PathHelpers has no dependencies on other units, subsystems, OTS items, or shared
packages.

### Callers

- **FileMarkdownWriterFactory** — combines the configured output root with caller-supplied
  subfolder and file-name segments before creating directories and files.
- **ApiMarkTask** — validates candidate tool and output-path fragments before passing them
  to process-launch or file-system operations.
- **Validation** — validates generated log-file and results-file path segments used by the
  self-test workflow before reading or writing files.
