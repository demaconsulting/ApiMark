## Microsoft.Extensions.FileSystemGlobbing

`Microsoft.Extensions.FileSystemGlobbing` is a Microsoft-provided NuGet package that
implements gitignore-style glob pattern matching against the file system. In ApiMark,
it is consumed exclusively by `GlobFileCollector` in `ApiMark.Core`, which is shared
by all language generators that require filesystem-based file discovery (`CppGenerator`
and `VhdlGenerator`).

### Purpose

FileSystemGlobbing was chosen because it provides production-quality glob matching
semantics — including `**/` recursive wildcards, single-segment `*` wildcards, and
character ranges — without requiring a custom pattern parser. It is a stable,
officially maintained .NET component with a minimal public API surface, reducing the
integration risk compared to third-party glob libraries.

### Features Used

- **`Matcher` class** — accumulates include glob patterns via `AddInclude`, then
  executes them against a directory root using `GetResultsInFullPath(root)` to
  return matching absolute file paths as `IEnumerable<string>`.
- **Include-only matching** — `GlobFileCollector` always calls `AddInclude` for
  every pattern. Exclusion semantics are implemented in `GlobFileCollector` itself
  by calling `collected.Remove()` on paths returned for `!`-prefixed patterns,
  rather than relying on `Matcher.AddExclude`.

### Integration Pattern

`GlobFileCollector.Collect` uses one `Matcher` instance per pattern:

1. For each pattern, strip the optional `!` exclusion prefix and resolve the
   filesystem root and glob tail via `ParsePattern`.
2. Create a new `Matcher(StringComparison.OrdinalIgnoreCase)` instance.
3. Call `matcher.AddInclude(globTail)` with the pattern's glob portion.
4. Call `matcher.GetResultsInFullPath(root)` to obtain an `IEnumerable<string>`
   of absolute paths.
5. For inclusion patterns, add results to the `collected` hash set. For exclusion
   patterns, call `collected.Remove()` for each result.

Non-existent roots are skipped before calling `GetResultsInFullPath` so the
library is never asked to traverse a missing directory.
