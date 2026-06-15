## GlobFileCollector

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

GlobFileCollector is a stateless utility class in ApiMarkCore that provides
flexible, filesystem-based file discovery using gitignore-style glob patterns.
It is shared across all ApiMark language generators that require pattern-driven
file collection (currently C++ and VHDL). Callers supply an ordered list of
inclusion and exclusion patterns, a set of language-specific file extensions,
and a working directory for relative pattern resolution; the collector returns a
sorted, deduplicated list of absolute file paths.

### Data Model

`GlobFileCollector` is a `public static class` with no instance state. All
members are stateless and thread-safe.

Internal fields:

- `GlobMetacharacters` (`char[]`, private static readonly): the set of glob
  metacharacters (`*`, `?`, `[`, `{`) used to locate the boundary between the
  static path prefix and the glob tail in absolute patterns.

### Key Methods

**GlobFileCollector.Collect** (public static): Collects files from the
filesystem matching the specified patterns.

- *Parameters*:
  - `IEnumerable<string> patterns` — ordered list of glob patterns; entries
    prefixed with `!` are exclusion patterns (the `!` is stripped before
    processing).
  - `IEnumerable<string> languageExtensions` — file extensions including the
    leading dot (e.g. `.vhd`, `.h`) used to filter results when a pattern's
    final segment is a bare `*`.
  - `string workingDirectory` — absolute path used as the root for relative
    patterns.
- *Returns*: `IReadOnlyList<string>` — sorted, deduplicated absolute file paths.
- *Preconditions*: none — empty patterns return an empty list; non-existent
  roots are silently skipped.
- *Algorithm*:
  1. For each pattern, strip a leading `!` (marks exclusion) and trim whitespace.
  2. Call `ParsePattern` to obtain a `(root, globTail)` pair. Relative patterns
     use `workingDirectory` as root; absolute patterns derive their root from the
     longest non-glob path prefix (see `SplitAbsolutePattern`).
  3. Skip the pattern silently if `globTail` is empty or `root` does not exist.
  4. Call `HasBareStarFinalSegment` to determine whether extension inference
     applies (final segment is exactly `*`).
  5. Run `Microsoft.Extensions.FileSystemGlobbing.Matcher.GetResultsInFullPath`
     from the resolved root.
  6. If extension inference applies, filter results to files whose extension
     (case-insensitive) is in `languageExtensions`.
  7. Delegate to `AccumulateResults`: inclusion patterns add matching paths to the
     result set; exclusion patterns remove matching paths from the result set.
  8. Return results sorted by ordinal string order.

**GlobFileCollector.AccumulateResults** (private static): Adds or removes a set of
matched file paths from the collected set.

- *Parameters*: `HashSet<string> collected`, `IEnumerable<string> results`,
  `bool isExclusion`.
- *Algorithm*: when `isExclusion` is true, calls `collected.Remove` for each
  full path; otherwise calls `collected.Add`.

**GlobFileCollector.ParsePattern** (private static): Splits a pattern body into
a filesystem root and a glob tail.

- Relative patterns: root = `workingDirectory`, tail = full pattern body.
- Absolute patterns: delegates to `SplitAbsolutePattern` after normalizing
  backslashes to forward slashes.

**GlobFileCollector.SplitAbsolutePattern** (private static): Splits a
forward-slash-normalized absolute pattern at the boundary between the longest
non-glob path prefix and the first glob metacharacter.

- Returns `(root, tail)` where root is the longest static prefix (a real
  directory) and tail is passed to `Matcher.AddInclude`.
- If no metacharacter is found, returns `(pattern, "")` — no glob tail.
- If no `/` precedes the first metacharacter, returns `("", pattern)` — no
  static root.

**GlobFileCollector.HasBareStarFinalSegment** (private static): Returns `true`
when the final path segment of the glob tail is exactly `*` (a bare wildcard
with no extension), triggering language-extension filtering.

### Error Handling

`GlobFileCollector.Collect` never throws for missing directories or empty
pattern lists. Non-existent roots are skipped silently. All other exceptions
(e.g. I/O errors from `Matcher.GetResultsInFullPath`) propagate to the caller.

### Dependencies

- **Microsoft.Extensions.FileSystemGlobbing** (NuGet) — `Matcher` class used
  for recursive glob evaluation.

### Callers

- **CppGenerator** — calls `GlobFileCollector.Collect` with C++ header
  extensions (`.h`, `.hpp`, `.hxx`, `.h++`) and the configured
  `ApiHeaderPatterns` to determine which headers are passed to Clang and
  documented.
- **VhdlGenerator** — calls `GlobFileCollector.Collect` with VHDL extensions
  (`.vhd`, `.vhdl`) and the configured `Sources` patterns to discover which
  VHDL files are parsed.
