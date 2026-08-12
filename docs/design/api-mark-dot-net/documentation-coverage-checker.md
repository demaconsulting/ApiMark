## DocumentationCoverageChecker

![DocumentationCoverageChecker Structure](ApiMarkDotNetView.svg)

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

`DocumentationCoverageChecker` scans a parsed .NET assembly for types and
members that lack an XML documentation `<summary>`, at a caller-supplied
visibility tier that is independent of the tier used for Markdown emission.
It powers the opt-in `--enforce-docs` CLI flag and `ApiMarkEnforceDocs`
MSBuild property, giving teams a way to enforce documentation completeness
without changing what gets emitted into the generated Markdown.

### Data Model

**DocumentationCoverageResult** (public sealed class): The immutable result
of a `Check` scan. Exposes `UndocumentedItems` (the list of violations),
`CheckedCount` (total types/members examined, documented or not),
`UndocumentedCount`, and `HasViolations`.

**UndocumentedApiItem** (public record): A single violation — `Kind`
(`UndocumentedApiItemKind`: `Type`, `Method`, `Property`, `Field`, `Event`) and
`DisplayName` (fully qualified type name, or `Type.Member` for a member, using
the same display-name formatting as `DotNetEmitter`).

**"Documented" bar (v1)**: A type or member is considered documented when
`XmlDocReader.GetSummary` returns a non-null, non-whitespace string for its
XML doc member identifier. Checking for complete `<param>`, `<returns>`, or
`<exception>` coverage is explicitly out of scope for v1 and is noted as a
possible future enhancement.

### Key Methods

**Check** (internal static): Scans `assembly` for types and members at or
above `visibility` that lack a non-empty XML doc `<summary>`.

- *Parameters*: `AssemblyDefinition assembly` — the parsed assembly;
  `XmlDocReader xmlDocs` — the documentation index used for summary lookups;
  `ApiVisibility visibility` — the enforcement visibility tier, independent
  of any emission visibility tier; `bool includeObsolete` — when `false`
  (the default enforcement behavior), obsolete types/members are skipped;
  `IReadOnlyList<string> excludePatterns` — wildcard patterns identifying
  namespaces/types to skip entirely, mirroring the emission exclude filter.
- *Preconditions*: `assembly` and `xmlDocs` must be non-null and already
  fully parsed/loaded.
- *Postconditions*: Returns a `DocumentationCoverageResult` describing every
  undocumented item found and the total number of items checked. Does not
  mutate `assembly` or `xmlDocs`.
- *Algorithm*: Enumerates every top-level, non-nested type that is not
  compiler-generated, is not a `NamespaceDoc` carrier, passes the enforcement
  visibility tier, the obsolete filter, and the exclude-pattern filter —
  the same filter chain `DotNetGenerator.Parse` applies for emission, but
  re-evaluated at the (possibly different) enforcement tier. Each qualifying
  type is checked via `CheckType`.

**CheckType** (private): Checks a single type (incrementing `checkedCount`
and recording a `Type` violation when its own summary is missing), then
checks all of its visible members via `GetVisibleMembers`, then recurses into
visible nested types. Nested-type visibility uses the `IsNested*` Mono.Cecil
flags rather than the top-level `IsPublic` flag, via `IsNestedTypeVisible`.

**GetVisibleMembers** (private): Enumerates methods, properties, fields, and
events of a type that satisfy the enforcement visibility tier and the
obsolete filter, reusing `DotNetEmitter.IsSpecialNameNonConstructor`,
`IsCompilerGenerated`, `IsCompilerGeneratedField`, and the `value__` backing
field exclusion for enums — the same shape as
`DotNetEmitter.GetVisibleMembers` but parameterized on the enforcement tier
instead of an emitter instance's fixed emission tier.

**IsTypeVisible / IsNestedTypeVisible / IsMemberVisible** (private): Re-derive
a local three-way visibility switch (`Public`, `PublicAndProtected`, `All`)
for top-level types, nested types, and members respectively. These are
intentionally re-derived rather than reused from `DotNetEmitter`, because
`DotNetEmitter`'s visibility-checking methods are instance-bound to a single,
fixed emission tier, while the enforcement tier is an independent,
caller-supplied parameter that may differ from it.

**ToKind** (private): Maps a Mono.Cecil member definition
(`MethodDefinition`, `PropertyDefinition`, `FieldDefinition`,
`EventDefinition`) to the corresponding `UndocumentedApiItemKind`.

### Error Handling

`Check` does not throw for missing documentation — every undocumented item is
simply recorded in the result rather than raising an exception. Malformed or
missing summaries are treated identically: `XmlDocReader.GetSummary`
returning `null` or an empty/whitespace-only string both count as
undocumented. Any exceptions from `XmlDocReader` construction (e.g. a missing
XML doc file) occur upstream in `DotNetGenerator.Parse`, before `Check` is
ever invoked.

### Dependencies

- **Mono.Cecil** — used to enumerate assembly-level type and member metadata
  (`AssemblyDefinition`, `TypeDefinition`, `IMemberDefinition` and its
  concrete subtypes).
- **DotNetEmitter** (static predicates) — reused for compiler-generated
  detection, `NamespaceDoc`-carrier detection, obsolete detection, member-id
  construction (`BuildTypeId`/`BuildMemberId`), member display-name
  formatting, and the public/public-or-protected member visibility
  predicates.
- **DotNetGenerator** (static helpers) — reused for exclude-pattern
  compilation (`CompileExcludePatterns`) and matching (`IsExcluded`).
- **XmlDocReader** — used to look up each candidate type/member's `<summary>`
  text via `GetSummary`.

### Callers

- **DotNetGenerator.CheckDocumentationCoverage** — the sole caller. Must be
  invoked strictly between `Parse()` returning and `Emit()` being called,
  because `DotNetEmitter.Emit` disposes the parsed `AssemblyDefinition` via a
  `using` block that wraps its entire body. Throws `InvalidOperationException`
  when called before `Parse()` or when `EnforceDocsVisibility` was not
  configured.
- **Program.ReportDocumentationCoverage** (indirectly, via
  `DotNetGenerator.CheckDocumentationCoverage`) — reports the resulting
  `DocumentationCoverageResult` to the console and sets the process exit
  code when severity is `Error` and violations were found.

### External Interfaces

N/A — this is an internal class with no external interfaces exposed beyond
its assembly. It is surfaced to end users only indirectly, through the
`--enforce-docs`/`--enforce-docs-severity` CLI options and the
`ApiMarkEnforceDocs`/`ApiMarkEnforceDocsSeverity` MSBuild properties.
