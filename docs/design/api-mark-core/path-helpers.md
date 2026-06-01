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
  segment must not be null.
- *Postconditions*: The returned path is the result of joining all segments to `basePath`
  in order and resolves within the normalized base path.
- *Throws*: `ArgumentNullException` when `basePath`, `relativePaths`, or any individual
  segment is null. `ArgumentException` when the combined path resolves outside `basePath`.

### Error Handling

PathHelpers joins all segments using `Path.Join`, then applies a single escape check:
`Path.GetFullPath` is called on both the base and the combined path, and
`Path.GetRelativePath` confirms the result still resolves under the base. If the
relative path starts with `..` or is itself rooted, the method throws `ArgumentException`.

Segments may contain `..` components or be rooted provided the combined result does not
escape — for example segments `["baa", ".."]` on base `C:\foo` resolve back to `C:\foo`
and are accepted. Only the final resolved position matters.

`Path.Join` is used rather than `Path.Combine` so no segment can silently replace the
base path.

### Dependencies

N/A - PathHelpers has no dependencies on other units, subsystems, OTS items, or shared
packages.

### Callers

- **FileMarkdownWriterFactory** — combines the configured output root with caller-supplied
  subfolder and file-name segments before creating directories and files.
