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

- **`Matcher` class** — accumulates include and exclude glob patterns via
  `AddInclude` / `AddExclude`, then executes them against a directory root using
  `Execute(DirectoryInfoWrapper)` to return the matched relative paths.
- **`DirectoryInfoWrapper`** — adapts a `DirectoryInfo` to the `IDirectoryInfo`
  interface expected by `Matcher.Execute`, allowing the library to traverse the
  physical file system.
- **Include/exclude semantics** — patterns registered with `AddExclude` remove files
  from the accumulated match set. `GlobFileCollector` maps its `!`-prefixed patterns
  to `AddExclude` calls and all other patterns to `AddInclude` calls.

### Integration Pattern

`GlobFileCollector.Collect` uses `Matcher` in a per-root loop:

1. For each pattern group sharing the same filesystem root, create a new `Matcher`
   instance.
2. Call `matcher.AddInclude` or `matcher.AddExclude` for each pattern's glob tail.
3. Call `matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(root)))` to
   obtain a `PatternMatchingResult`.
4. Iterate `result.Files` to collect matched absolute paths.

Non-existent roots are skipped before calling `Execute` so the library is never
asked to traverse a missing directory.

### Known Constraints

- The library evaluates patterns relative to a single root per `Execute` call.
  `GlobFileCollector` handles multi-root scenarios by grouping patterns by root and
  making one `Execute` call per root.
- Pattern matching is case-sensitive on case-sensitive file systems (Linux/macOS)
  and case-insensitive on Windows, consistent with OS file-system semantics.
