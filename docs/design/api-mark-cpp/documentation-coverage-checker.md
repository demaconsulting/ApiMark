## DocumentationCoverageChecker

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

`DocumentationCoverageChecker` scans the namespace declarations parsed by
`CppGenerator` for classes, functions, fields, enums, and type aliases that
lack a Doxygen `@brief`/summary, at a caller-supplied visibility tier that is
independent of the tier used for Markdown emission. It powers the opt-in
`--enforce-docs` CLI flag and `ApiMarkEnforceDocs` MSBuild property for the
`cpp` language, mirroring `ApiMark.DotNet.DocumentationCoverageChecker` as
closely as the C++ AST model allows.

### Data Model

Reuses the shared `ApiMark.Core.DocumentationCoverageResult` and
`ApiMark.Core.UndocumentedApiItem` types (see the IDocumentationCoverageCapable
design document under the ApiMarkCore subsystem).
`Kind` values produced by this checker: `Class`, `Function`, `Field`, `Enum`,
`EnumValue`, `TypeAlias`.

**"Documented" bar (v1)**: A declaration is considered documented when its
`CppDocComment.Summary` is a non-null, non-whitespace string. Checking for
complete `@param`/`@return` coverage is out of scope and noted as a possible
future enhancement, exactly as for .NET.

**Known limitation — no exclude-pattern support**: `CppGeneratorOptions` has
no namespace/type exclude-pattern option analogous to
`DotNetGeneratorOptions.ExcludePatterns` — only file-selection glob patterns
(`ApiHeaderPatterns`). Adding such a feature is out of scope for
documentation-coverage enforcement alone, so this checker has no exclude
parameter at all. This is a deliberate, documented gap versus the .NET
checker, not an oversight.

**Nested classes need no visibility gate of their own**: `CppClass.NestedClasses`
is already pre-filtered to public nested classes/structs only by
`ClangAstParser` — unlike .NET, where nested-type visibility must be
re-evaluated with `IsNestedTypeVisible`. This checker therefore recurses into
every nested class unconditionally, applying the visibility tier only to each
nested class's own members.

### Key Methods

**Check** (internal static): Scans `namespaceDecls` for classes, functions,
fields, enums, enum values, and type aliases at or above `visibility` that
lack a non-empty Doxygen summary.

- *Parameters*: `SortedDictionary<string, CppEmitter.NamespaceDeclarations> namespaceDecls`
  — the namespace declarations collected by `CppGenerator.Parse`;
  `ApiVisibility visibility` — the enforcement visibility tier, independent of
  any emission visibility tier; `bool includeDeprecated` — when `false` (the
  default enforcement behavior), declarations carrying `[[deprecated]]` are
  skipped, mirroring the emission deprecated filter.
- *Preconditions*: `namespaceDecls` must be non-null and already populated by
  `CppGenerator.Parse`.
- *Postconditions*: returns a `DocumentationCoverageResult` describing every
  undocumented declaration found and the total number of declarations
  checked. Does not mutate `namespaceDecls`.
- *Algorithm*: for each namespace, walks its `Classes` (recursing into
  `NestedClasses` unconditionally), `FreeFunctions` (always public, still
  deprecated-filtered), `Enums` (and each `CppEnumValue`), and `TypeAliases`,
  applying the deprecated filter at every recursion level within the checker
  itself (a stricter behavior than `CppGenerator`'s own emission-path
  recursion depth, which does not re-apply the filter to nested classes).

**CheckClass** (private): Checks a single class for a missing summary, then
checks its visible members and fields (tier-filtered via `IsVisibleMember`),
its type aliases, and — unconditionally, since already pre-filtered to
public — its nested classes.

**CheckFunction / CheckEnum / CheckTypeAlias** (private): Check a single free
function, enum (and its values), or type alias for a missing summary.

**FormatParameterSignature** (private): Formats a parenthesized,
comma-separated parameter-type signature (e.g. `(int, const std::string&)`)
for a `CppFunction`, appended to the reported `DisplayName` of every
undocumented free function, method, or constructor so that distinct
overloads of the same name are reported as separate, unambiguous entries
rather than colliding on an identical `DisplayName`.

**IsVisibleMember** (private): Re-derives a local three-way visibility switch
(`Public`, `PublicAndProtected`, `All`) over `CppAccessibility`, independent of
`CppEmitter`'s emission-scoped visibility tier.

### Error Handling

`Check` does not throw for missing documentation — every undocumented item is
simply recorded in the result rather than raising an exception. A `null`
`Doc` and an empty/whitespace-only `Doc.Summary` are treated identically as
undocumented.

### Dependencies

- **CppAstModel** — `CppClass`, `CppFunction`, `CppField`, `CppEnum`,
  `CppEnumValue`, `CppTypeAlias`, `CppAccessibility`, `CppDocComment`.
- **CppEmitter.NamespaceDeclarations** — the namespace-grouped declaration
  container produced by `CppGenerator.Parse`.
- **ApiMark.Core** — `DocumentationCoverageResult`, `UndocumentedApiItem`.

### Callers

- **CppGenerator.CheckDocumentationCoverage** — the sole caller. Must be
  invoked after `Parse()` has completed successfully (unlike .NET, there is no
  `Emit`-time disposal constraint since C++ parsing does not hold an open
  file handle equivalent to `AssemblyDefinition`). Throws
  `InvalidOperationException` when called before `Parse()` or when no
  enforcement tier is configured; throws `ArgumentException` for an
  unrecognized tier value.
- **Program.ReportDocumentationCoverage** (indirectly, via
  `CppGenerator.CheckDocumentationCoverage`) — reports the resulting
  `DocumentationCoverageResult` to the console and sets the process exit
  code when severity is `Error` and violations were found.

### External Interfaces

N/A — this is an internal class with no external interfaces exposed beyond
its assembly. It is surfaced to end users only indirectly, through the
`--enforce-docs`/`--enforce-docs-severity` CLI options and the
`ApiMarkEnforceDocs`/`ApiMarkEnforceDocsSeverity` MSBuild properties.
