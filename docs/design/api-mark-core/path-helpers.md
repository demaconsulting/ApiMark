## PathHelpers

![PathHelpers Structure](ApiMarkCoreView.svg)

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

PathHelpers is the single, auditable point for two related path-safety and
path-reliability concerns shared across ApiMark:

- Combining user-supplied path segments with a base directory without permitting
  directory-traversal escapes.
- Resolving a path to its actual on-disk casing so that two differently-cased
  spellings of the same file or directory can be recognized as identical, regardless
  of platform or file-system case sensitivity.

Centralizing both concerns here prevents them from being re-implemented (or forgotten)
independently by each caller.

### Data Model

PathHelpers is a `public static` utility with no fields or properties. All behavior
is stateless and thread-safe.

### Key Methods

**PathHelpers.SafePathCombine** (`internal`): Safely combines a base path with one or
more validated relative path segments.

- *Parameters*: `string basePath` — trusted base directory. `params string[] relativePaths`
  — zero or more caller-supplied relative path segments appended in order.
- *Returns*: `string` — the combined path. When no segments are supplied, returns
  `basePath` unchanged.
- *Preconditions*: `basePath` must not be null. `relativePaths` must not be null. Each
  segment must not be null.
- *Postconditions*: The returned path is the result of joining all segments to `basePath`
  in order and resolves within the normalized base path.
- *Throws*: `ArgumentNullException` when `basePath`, `relativePaths`, or any individual
  segment is null. `ArgumentException` when the combined path resolves outside `basePath`.

**PathHelpers.Comparer** (`public`): The `StringComparer` (always
`StringComparer.Ordinal`) to use once paths have been normalized via `NormalizeCase`.
There is no reliable way to infer file-system case sensitivity from the operating
system alone (both macOS/Windows can host case-sensitive volumes; Linux can host
case-insensitive file systems) — comparing normalized results case-sensitively is the
only combination correct regardless of platform or file system.

**PathHelpers.NormalizeCase** (`public`): Resolves an absolute path to its actual
on-disk casing by querying the file system directly, one path segment at a time,
rather than guessing based on the operating system.

- *Parameters*: `string path` — an absolute path to normalize (e.g. via
  `Path.GetFullPath`). `Dictionary<string, string[]>? directoryEntryCache` — an
  optional, caller-owned cache of parent-directory entry listings reused across
  multiple calls to avoid redundant directory enumeration when many paths share
  common ancestor directories; pass `null` to normalize a single path with no caching.
- *Returns*: `string` — `path` with every segment that exists on disk replaced by its
  actual on-disk casing. Segments that do not exist are left exactly as supplied.
- *Root handling*: A drive-letter or UNC root prefix is never a directory entry that
  can be looked up, so it is canonicalized separately: drive letters are converted to
  uppercase (they carry no meaningful case identity); UNC server/share roots are
  converted to lowercase (a stable, deterministic form, since there is no
  locally-queryable "real" casing without a network round trip). Roots that are
  neither (e.g. Unix's `/`) are returned unchanged.
- *Ambiguity handling*: For each subsequent segment, an exact (ordinal) match against
  the parent directory's actual entries always wins when one exists. Failing that, a
  case-insensitive match is only accepted when exactly one distinct entry matches —
  if two or more case variants coexist (a case-sensitive file system) and none matches
  exactly, the method refuses to guess and returns the segment unresolved rather than
  letting unspecified directory-enumeration order silently pick one.

### Error Handling

**SafePathCombine**: joins all segments using `Path.Join`, then applies a single
escape check: `Path.GetFullPath` is called on both the base and the combined path, and
`Path.GetRelativePath` confirms the result still resolves under the base.
`ArgumentException` is thrown when and only when the normalized combined path escapes
(resolves above) the base directory — that is, when the relative path from the base
to the combined result starts with `..` or is itself rooted.

Individual segments may contain `..` components or be rooted provided the combined
result does not escape — for example segments `["baa", ".."]` on base `C:\foo` resolve
back to `C:\foo` and are accepted. Only the final resolved position matters.

`Path.Join` is used rather than `Path.Combine` so no segment can silently replace the
base path.

**NormalizeCase**: normalization is best-effort and never throws for a not-yet-existing
path or path segment — a directory that does not exist, or one whose contents cannot
be enumerated (e.g. a transient permissions failure), is simply left unresolved without
caching the failure, so a later, successful lookup is not permanently blocked by an
earlier transient error.

### Dependencies

N/A - PathHelpers has no dependencies on other units, subsystems, OTS items, or shared
packages.

### Callers

- **FileMarkdownWriterFactory** — combines the configured output root with caller-supplied
  subfolder and file-name segments before creating directories and files.
- **ApiMark.DotNet.DotNetGenerator** — deduplicates reference-path search directories via
  `NormalizeCase`/`Comparer` before seeding a Mono.Cecil `DefaultAssemblyResolver`.
- **ApiMark.DotNet.ExternalXmlDocResolver** — keys its per-reference-path documentation
  cache via `NormalizeCase`/`Comparer` so differently-cased spellings of the same
  reference assembly path collapse to a single cache entry.
