## DotNetEmitterGradualDisclosure

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
After writing the H1 heading, the method emits the `AssemblyDescriptionAttribute`
value as a paragraph when the attribute is present on the assembly. The
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
  present in `NamespaceDescriptions`, emits it as a paragraph; writes a table of
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
 via `writer.WriteSignature("csharp", ...)`, which produces a fenced C# code block; emits the XML summary and remarks paragraphs; emits structured example blocks via `ctx.XmlDocs.GetExampleParts(typeMemberId)`; groups all
  visible members by kind (Constructors, Properties, Fields, Events, Methods,
  Operators, Nested Types) and writes one table row per member (or one representative row
  per method overload group) with a link to the member's dedicated page; calls per-kind page writers for each member or member group.

**WriteMethodDocumentation** (private static): Writes the XML documentation content
for a single method (or one overload) onto the caller-supplied writer.

- *Parameters*: `IMarkdownWriter writer` — writer receiving the documentation sections;
  `MethodDefinition method` — the method to document; `string memberId` — the
  XML-doc member ID used for lookup; `MethodDocContext context` — namespace name,
  XML doc reader, resolver, current folder, and external type accumulator.
- *Returns*: `void`
- *Algorithm*: Emits the C# method signature via `writer.WriteSignature("csharp", ...)`, which produces a fenced C# code block; writes the XML
  summary as a paragraph (or the placeholder when absent); if the method has parameters,
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
- *Algorithm*: Emits the C# member signature via `writer.WriteSignature("csharp", ...)`, which produces a fenced C# code block; writes the XML
  XML summary as a paragraph (or the placeholder when absent); emits returns,
  exceptions, remarks, and example sections when present, following the same
  pattern as `WriteMethodDocumentation` for non-method member kinds.

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
- **DotNetAstModel** — provides assembly data.
- **TypeLinkResolver** — used to resolve type references to Markdown links in
  table cells.
- **XmlDocReader** — used to retrieve documentation text for each member.
- **IMarkdownWriterFactory** — received from `DotNetEmitter.Emit`.

### Callers

- **DotNetEmitter.Emit** — constructs and calls this class when the format is
  not SingleFile.

### External Interfaces

N/A — this is an internal class with no external interfaces exposed beyond its assembly.
