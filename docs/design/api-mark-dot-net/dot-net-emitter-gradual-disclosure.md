## DotNetEmitterGradualDisclosure

![DotNetEmitterGradualDisclosure Structure](ApiMarkDotNetView.svg)

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

DotNetEmitterGradualDisclosure writes the complete gradual-disclosure Markdown
tree for a .NET assembly: one assembly index page (`api.md`), one namespace
summary page per namespace, one type page per visible type, and one or more
detail pages per visible member. It is created exclusively by
`DotNetEmitter.Emit` when `EmitConfig.Format` is not
`OutputFormat.SingleFile`.

### Data Model

DotNetEmitterGradualDisclosure holds references to:

- *_emitter* (`DotNetEmitter`): Parent emitter providing shared static helpers
  such as `BuildTypeSignature`, `GetNamespaceFolderPath`, and
  `GetMemberDisplayName`.
- *_model* (`DotNetAstModel`): Pre-parsed assembly data (namespaces, types,
  XML docs, resolver, options).

### Key Methods

**DotNetEmitterGradualDisclosure.Emit** (internal): Entry point called by
`DotNetEmitter.Emit`. Dispatches to `EmitGradualDisclosure`.

**EmitGradualDisclosure** (private): Writes the assembly index page, then
iterates all namespaces and types, writing namespace summary and type pages.
After writing the H1 heading, the method emits the introductory description
paragraph resolved via `DotNetEmitter.GetAssemblyDescription`: an
explicitly-supplied `DotNetGeneratorOptions.LibraryDescription` option takes
precedence when present; otherwise the compiled `AssemblyDescriptionAttribute`
value is emitted as a fallback when present on the assembly. The
all-namespaces table uses three columns: `Namespace`, `Types`, and `Description`,
where `Types` contains the direct type count for each namespace.

**WriteNamespacePage** (private): Writes the Markdown summary page for a
single namespace.

- *Parameters*: `IMarkdownWriterFactory factory` — factory used to create the
  namespace page writer; `string namespaceName` — the full namespace name being
  documented; `NamespaceDocContext ctx` — bundled namespace documentation context
  shared across all namespace page writes.
- *Returns*: `void`
- *Algorithm*: Computes the namespace folder path and splits it into subfolder
  and short name; creates the namespace page writer via `factory.CreateMarkdown`;
  writes an H1 heading with the namespace name; if a namespace description is
  present in `NamespaceDescriptions`, emits its summary as a paragraph, then the
  remarks paragraph (when present) and the structured example parts (code parts as
  fenced C# blocks, prose parts as paragraphs), mirroring the type-level rendering;
  writes a table of
  immediate child namespaces with links when any exist; writes a table of all
  visible types in the namespace (columns: Type, Description), with each type
  name linked to its type page; calls `WriteTypePage` for each type.

**WriteTypePage** (private): Writes the Markdown type page for a single
`TypeDefinition`.

- *Parameters*: `TypePageWriteContext ctx` — type-level write context encapsulating
  factory, namespace name, namespace folder path, type definition, XML docs, and
  resolver.
- *Returns*: `void`
- *Algorithm*: Creates the type page writer internally via `ctx.Factory.CreateMarkdown`;
  writes an H1 heading with the type's simple name; emits the C# signature
 via `writer.WriteSignature("csharp", ...)`, which produces a fenced C# code block; fetches the
  XML summary via `GetSummaryMarkdown` (multi-line rendering) and the remarks text
  before deciding what to write: when a summary is present it is emitted as a
  paragraph; otherwise the "No description provided." placeholder is emitted ONLY
  when remarks are also absent (see "Placeholder Suppression" below) — when remarks
  alone are present, nothing is written here and the remarks paragraph (emitted next)
  carries the type's description; emits structured example blocks via `ctx.XmlDocs.GetExampleParts(typeMemberId)`; groups all
  visible members by kind (Constructors, Properties, Fields, Events, Methods,
  Operators, Nested Types) and writes one table row per member (or one representative row
  per method overload group) with a link to the member's dedicated page; calls per-kind page writers for each member or member group.
  The Nested Types section is written unconditionally via `WriteNestedTypesSection`,
  even when the containing type has no own members/operators, since a type can be a
  pure namespace-like container for nested types; this must match
  `DotNetGenerator.CollectTypeCrefLinkIndex`, which recurses into nested types
  unconditionally, to avoid dangling cref links to pages that were never written.

**WriteMethodDocumentation** (private static): Writes the XML documentation content
for a single method (or one overload) onto the caller-supplied writer.

- *Parameters*: `IMarkdownWriter writer` — writer receiving the documentation sections;
  `MethodDefinition method` — the method to document; `string memberId` — the
  XML-doc member ID used for lookup; `MethodDocContext context` — namespace name,
  XML doc reader, resolver, current folder, and external type accumulator.
- *Returns*: `void`
- *Algorithm*: Emits the C# method signature via `writer.WriteSignature("csharp", ...)`, which produces a fenced C# code block; fetches the XML
  summary via `GetSummaryMarkdown` and the remarks text before deciding what to
  write: a present summary is written as a paragraph; otherwise the "No
  description provided." placeholder is written only when remarks are also
  absent (see "Placeholder Suppression" below); if the method has parameters,
  writes a parameter table with Parameter, Type, and Description columns — type cells are
  resolved via `resolver.Linkify` and external types are accumulated into
  `context.ExternalTypes`; if a returns value is documented, writes a `**Returns:**`
  paragraph; writes exception and remarks sections when present; writes structured
  example blocks from `xmlDocs.GetExampleParts`.

**WriteMethodOverloadPage** (private static): Writes a single shared Markdown page for a
pure method overload group (all methods sharing the same exact case-sensitive sanitized file name).

- *Parameters*: `IMarkdownWriterFactory factory`, `string namespaceName`,
  `string namespaceFolderPath`, `TypeDefinition type`, `IReadOnlyList<MethodDefinition>
  overloads` — ordered list of overload methods (at least one element),
  `XmlDocReader xmlDocs`, `TypeLinkResolver resolver`.
- *Returns*: `void`
- *Algorithm*: Computes `sanitizedName = BuildMethodFileName(overloads[0], type)`;
  creates `{namespaceFolderPath}/{FlattenArity(type.Name)}/{sanitizedName}.md` via the
  factory; writes an H1 heading using `GetMethodGroupName(overloads[0])`; for each overload
  writes an H2 heading using `BuildMethodDisplayName(overload)` and delegates to
  `WriteMethodDocumentation`; accumulates external type references across all overloads and
  emits them via `WriteExternalTypesSection`.

**WriteCombinedMemberPage** (private static): Writes a single combined Markdown
page for a group of members whose sanitized file names collide on
case-insensitive file systems.

- *Algorithm*: Creates `{namespaceFolderPath}/{FlattenArity(type.Name)}/{lowerKey}.md` via the
  factory; writes an H1 heading using `lowerKey`; for each member writes an H2
  heading of the form `{displayName} ({kindLabel})`; delegates to
  `WriteMethodDocumentation` for `MethodDefinition` members and to
  `WriteNonMethodMemberContent` for all other member kinds.

**IsPureMethodOverloadGroup** (private static): Returns true when all members
in a collision group are `MethodDefinition` instances sharing the same exact
case-sensitive sanitized file name, indicating a classical method overload group
rather than a case-insensitive collision.

**GetMemberKindLabel** (private static): Maps an `IMemberDefinition` to a short
human-readable kind string (`"Field"`, `"Property"`, `"Event"`, `"Constructor"`,
`"Method"`, or `"Member"`).

**WriteExternalTypesSection** (private static): Emits the `## External Types`
section at the bottom of a page when at least one external type was referenced
in table cells.

**WriteTypeOperatorsPage** (private static): Writes the `operators.md` page for
a type that defines operator overloads.

- *Parameters*: `IMarkdownWriterFactory factory`, `string namespaceName`,
  `string namespaceFolderPath`, `TypeDefinition type`,
  `IReadOnlyList<MethodDefinition> operatorMethods` — the operator methods to
  document, `XmlDocReader xmlDocs`, `TypeLinkResolver resolver`.
- *Returns*: `void`
- *Algorithm*: Creates `{namespaceFolderPath}/{FlattenArity(type.Name)}/operators.md`
  via the factory; writes an H1 heading; for each operator method writes an H2
  heading using `BuildMethodDisplayName`, which produces a C# method-signature form
  including the operator keyword and full parameter list, and delegates to
  `WriteMethodDocumentation`; accumulates external type references and emits them
  via `WriteExternalTypesSection`.

**WriteNestedTypePage** (via recursive `WriteTypePage`): Writes a dedicated Markdown page for each visible nested type under its containing type's folder.

- *Parameters*: `TypePageWriteContext ctx` — context for the nested type, where `NamespaceFolderPath` is set to `{parentNamespaceFolderPath}/{FlattenArity(containingTypeName)}` and `Type` is the nested type definition.
- *Returns*: `void`
- *Path pattern*: `factory.CreateMarkdown("{namespaceFolderPath}/{FlattenArity(containingTypeName)}", nestedTypeName)` — the nested type's page is placed under its containing type's folder segment.
- *Algorithm*: Called from `WriteTypePage` for each element of `GetVisibleNestedTypes(ctx.Type)`. Each nested type's page is written by recursively calling `WriteTypePage` with the updated context; the nested type's own members are iterated and documented on that page following the same process as any other type page.

**WriteMemberPage** (private static): Creates the dedicated detail page for a single
non-overloaded, non-collision member.

- *Parameters*: `TypePageWriteContext ctx` — type-level write context providing factory,
  namespace name, namespace folder path, type definition, XML docs, and resolver.
  `IMemberDefinition member` — the member to document. `string memberId` — the pre-computed
  XML-doc member ID for documentation lookups.
- *Returns*: `void`
- *Algorithm*: Computes `sanitizedName` via `GetSanitizedMemberFileName`; creates the page
  writer at `{namespaceFolderPath}/{FlattenArity(type.Name)}/{sanitizedName}.md`; writes an H1
  heading using `GetMemberDisplayName`; if `member` is a `MethodDefinition`, creates a
  `SortedSet<ExternalTypeInfo>`, calls `WriteMethodDocumentation`, and emits the external types
  section via `WriteExternalTypesSection`; otherwise delegates to
  `WriteNonMethodMemberContent` with an empty external types accumulator.

**ProcessSingleMember** (private static): Handles a single non-overloaded,
non-collision member by writing its dedicated page and adding one row to the
appropriate per-kind accumulator on the containing type page.

- *Parameters*: `TypePageWriteContext ctx`, `IMemberDefinition member`,
  plus per-kind row accumulators and an `externalTypes` accumulator.
- *Returns*: `void`
- *Algorithm*: Computes the member ID, summary, type name, display name, and
  sanitized file name; creates the member's dedicated page via `WriteMemberPage`;
  adds one table row to `constructorRows`, `methodRows`, `propertyRows`,
  `fieldRows`, or `eventRows` depending on the member kind.

**ProcessOverloadGroup** (private static): Handles a pure method overload group
by writing a single consolidated overload page and adding one representative row
to the appropriate per-kind accumulator.

- *Parameters*: `TypePageWriteContext ctx`,
  `IReadOnlyList<IMemberDefinition> group` — all overloads sharing the same key,
  plus `constructorRows`, `methodRows`, and `externalTypes` accumulators.
- *Returns*: `void`
- *Algorithm*: Orders overloads by generic-parameter count, then by value-parameter
  count, then by parameter type name list (deterministic selection of representative);
  calls `WriteMethodOverloadPage`; adds one row linking to the shared overload page,
  using `GetMethodGroupDisplayName` for the display text.

**ProcessCollisionMember** (private static): Handles one member from a
case-insensitive filename collision group; writes the combined page on first
encounter and adds the member's row to the appropriate per-kind accumulator.

- *Parameters*: `TypePageWriteContext ctx`, `IMemberDefinition member`,
  `IReadOnlyList<IMemberDefinition> group`, `string lowerKey`,
  `HashSet<string> writtenLowerKeys`, per-kind row accumulators, and `externalTypes`.
- *Returns*: `void`
- *Algorithm*: Calls `WriteCombinedMemberPage` when `writtenLowerKeys.Add(lowerKey)`
  succeeds (first visit); regardless, adds one row for this member linking to the
  shared `{lowerKey}.md` page.

**WriteNonMethodMemberContent** (private static): Writes the signature, summary,
returns, exceptions, remarks, and example documentation sections for a single
non-method member (property, field, or event) to the supplied Markdown writer.

- *Parameters*: `IMarkdownWriter writer`, `IMemberDefinition member`,
  `string memberId`, `MethodDocContext ctx` — provides namespace name, XML docs,
  resolver, current folder, and external type accumulator.
- *Returns*: `void`
- *Algorithm*: Emits the C# member signature via `writer.WriteSignature("csharp", ...)`, which produces a fenced C# code block; fetches the XML
  summary via `GetSummaryMarkdown` and the remarks text before deciding what to
  write (same placeholder-suppression rule as `WriteMethodDocumentation` — see
  "Placeholder Suppression" below); emits returns,
  exceptions, remarks, and example sections when present, following the same
  pattern as `WriteMethodDocumentation` for non-method member kinds.

#### BuildMemberPageIndex and BuildCrefLinkContext

**BuildMemberPageIndex** (`internal static`): Pure function computing, for
every emitted member of a type, the page key it will end up on (overload
grouping, operator grouping, case-insensitive-filename-collision grouping,
single-member fallback) without writing any Markdown, returning a map from
each emitted member's XML-doc ID to its page key.

- *Parameters*: `TypeDefinition type` — the type whose members to index;
  `string namespaceFolderPath` — folder path of `type`'s own page; `Func
  <IMemberDefinition, bool> isMemberEmitted` — visibility predicate deciding
  which members are included (typically `DotNetEmitter.ShouldIncludeMember`).
- *Returns*: `IReadOnlyDictionary<string, string>` — `DotNetEmitter.BuildMemberId(member)
  -> "{folder}/{fileNameNoExt}"` for every member where `isMemberEmitted(member)`
  is `true`.
- *Why this exists*: `CrefTargetResolver`/`XmlDocReader` cross-reference
  linking needs to answer "what page will member X end up on" for every
  member in the assembly *before* any page is written. The actual page
  writer (`ProcessTypeMembers` and the methods it calls —
  `ProcessSingleMember`/`ProcessOverloadGroup`/`ProcessCollisionMember`)
  interleaves this grouping decision with immediate table-row accumulation
  and Markdown writing, in an order that does not separate cleanly into a
  single function both call. Rather than duplicate the grouping decision
  wholesale (which previously caused a real desync — see the
  `DotNetGenerator.CollectTypeCrefLinkIndex` note on nested-type pages), the
  three sub-decisions that actually determine a page's file name are each
  factored into their own small, shared, Markdown-free helper that both
  `BuildMemberPageIndex` and the page writer call identically:
  - `GroupMembersByFileName` — groups members by case-insensitive sanitized
    file name. Used by both `BuildMemberPageIndex` and `ProcessTypeMembers`.
  - `GetOrderedOverloads` — deterministically orders a pure method overload
    group so its first entry is always the chosen representative. Used by
    `BuildMemberPageIndex` (via `DecideGroupPageFileName`) and
    `ProcessOverloadGroup`.
  - `DecideGroupPageFileName` — given a group and its case-insensitive key,
    returns the single page file name every member in the group links to
    (the lone member's own name, the representative overload's name, or the
    shared collision key). Used directly by `BuildMemberPageIndex`; its
    per-branch logic mirrors what `ProcessOverloadGroup`/
    `ProcessCollisionMember` independently compute for the pages they write.

  This leaves the table-row accumulation and Markdown-writing responsibilities
  of `ProcessTypeMembers` and its helpers free to stay separate from indexing,
  while guaranteeing the file-name decision itself — the one fact that must
  match exactly for cref links to resolve to real pages — cannot drift between
  the index and the writer. Overall correctness is additionally verified by
  an end-to-end integration test (see `DotNetEmitterGradualDisclosureTests`)
  asserting that every page key `BuildMemberPageIndex` returns matches an
  actually-written Markdown file for the test fixtures.
- *Callers*: `DotNetGenerator.CollectTypeCrefLinkIndex` (merges every type's
  result into the assembly-wide `DotNetAstModel.MemberPageIndex`).

**BuildCrefLinkContext** (private): Constructs a `CrefLinkContext` scoped to a
given folder, bound to the ambient model's cref-linking indices and the
emitter's own visibility delegates.

- *Parameters*: `string currentFolder` — folder path of the Markdown file
  about to be rendered.
- *Returns*: `CrefLinkContext` — with `Targets` bound to
  `_model.CrefTargets`, `Resolver` bound to the model's `TypeLinkResolver`,
  `MemberPageIndex` bound to `_model.MemberPageIndex`, `IsTypeEmitted`/
  `IsMemberEmitted` bound to this emitter's own
  `IsTypeVisible`/`ShouldIncludeMember`-equivalent visibility methods (so
  visibility logic is never duplicated), and `CurrentFolder` set to the
  supplied folder.
- *Usage pattern*: Built once per type-processing pass at the folder level
  where it first becomes relevant (e.g. once per `WriteNamespacePage` call,
  once per `WriteTypePage`/`WriteNestedTypesSection` call) rather than freshly
  per XML-doc getter call site, since it is mostly stable per current folder.
  Page-writer methods that execute at a deeper folder than their caller (e.g.
  `WriteMemberPage` relative to `WriteTypePage`) derive a re-scoped
  `CrefLinkContext` via the C# record `with` expression (`linkContext with {
  CurrentFolder = memberFolder }`) rather than calling `BuildCrefLinkContext`
  again or threading the emitter instance itself into those (mostly `static`)
  page-writer methods — consistent with the existing `TypePageWriteContext`/
  `MethodDocContext` context-record pattern this class already uses to avoid
  passing the emitter instance into its static helpers.
- *Call sites*: Every `ctx.XmlDocs.Get*` call this class makes (except
  `GetExceptions`/`GetExceptionDetails`, explicitly out of scope for linking)
  passes the resulting (or re-scoped) `CrefLinkContext` as its trailing
  argument, so a resolvable intra-assembly `<see cref>`/`<seealso cref>`
  reference renders as a real Markdown link instead of the plain code-span
  fallback.

`WriteTypeHeaderSections`, `WriteNonMethodMemberContent`, and
`WriteMethodDocumentation` each fetch the member's (or type's) remarks text
*before* deciding whether to emit `DotNetEmitter.NoDescriptionPlaceholder`
("No description provided."): the placeholder is written only when BOTH the
summary AND the remarks are absent. When a summary is absent but remarks
content is present, nothing is written in the summary's place — the remarks
paragraph, emitted immediately afterward, carries the description instead.

This scoping applies ONLY to member-detail-page and type-page bodies, which
render both summary and remarks. Table-cell summary fallback (the nested-types
table, per-kind member-row tables, and overload-group tables on a type page)
deliberately keeps the unconditional placeholder-on-empty-summary behavior
unchanged, since table cells never render remarks — the placeholder remains
the only signal available in that compact context.

### Path Conventions

The assembly index page (`api.md`) also writes a `## File Naming and Path Convention`
appendix section containing a two-column table (`Symbol kind`, `Path pattern`) that
documents all path rules in human-readable form. This appendix is written at the end
of `api.md` after the all-namespaces table so the namespace table is the first visible
content. The convention table covers root namespace, child namespace, type, nested type,
member, and operators page patterns.

- Assembly index: `factory.CreateMarkdown("", "api")`
- Namespace summary: `factory.CreateMarkdown(subFolder, shortName)` where `subFolder`
  and `shortName` are produced by splitting `namespaceFolderPath` at the last separator
  (e.g. for `ApiMark.DotNet.Fixtures`, `subFolder=""` and `shortName="ApiMark.DotNet.Fixtures"`)
- Type page: `factory.CreateMarkdown(namespaceFolderPath, FlattenArity(type.Name))`
  where `FlattenArity` replaces backtick-arity notation (e.g. `` SampleGenericClass`1 ``)
  with a plain numeric suffix form (`SampleGenericClass1`) for file-system safety. This is distinct
  from `StripArity`, which removes the arity suffix entirely and is used for display names.
- Member detail: `factory.CreateMarkdown("{namespaceFolderPath}/{FlattenArity(type.Name)}", memberName)`
- Operators page: `factory.CreateMarkdown("{namespaceFolderPath}/{FlattenArity(type.Name)}", "operators")`
- Combined page: `factory.CreateMarkdown("{namespaceFolderPath}/{FlattenArity(type.Name)}", lowerKey)`

### Error Handling

Exceptions from `IMarkdownWriterFactory.CreateMarkdown` or from writer methods
propagate unchanged to the caller. No exceptions are caught or suppressed by
this class.

### Dependencies

- **DotNetEmitter** — parent emitter providing shared static helpers.
- **DotNetAstModel** — provides assembly data, including `CrefTargets` and
  `MemberPageIndex` used for cref cross-reference linking.
- **TypeLinkResolver** — used to resolve type references to Markdown links in
  table cells, and (via `CrefLinkContext`) to resolve `<see cref>`/`<seealso
  cref>` references to Markdown links.
- **CrefTargetResolver** (indirectly, via `_model.CrefTargets` bundled into a
  `CrefLinkContext`) — used to decide whether a cref targets a symbol
  declared in the documented assembly.
- **XmlDocReader** — used to retrieve documentation text for each member.
- **IMarkdownWriterFactory** — received from `DotNetEmitter.Emit`.

### Callers

- **DotNetEmitter.Emit** — constructs and calls this class when the format is
  not SingleFile.

### External Interfaces

N/A — this is an internal class with no external interfaces exposed beyond its assembly.
