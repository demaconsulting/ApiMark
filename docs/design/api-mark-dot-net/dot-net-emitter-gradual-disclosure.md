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
- Type page: `factory.CreateMarkdown(namespaceFolderPath, typeSimpleName)`
- Member detail: `factory.CreateMarkdown("{namespaceFolderPath}/{typeSimpleName}", memberName)`
- Operators page: `factory.CreateMarkdown("{namespaceFolderPath}/{typeSimpleName}", "operators")`
- Combined page: `factory.CreateMarkdown("{namespaceFolderPath}/{typeSimpleName}", lowerKey)`

### Error Handling

Exceptions from `IMarkdownWriterFactory.CreateMarkdown` or from writer methods
propagate unchanged to the caller. No exceptions are caught or suppressed by
this class.

### Dependencies

- **DotNetEmitter** — parent emitter providing shared static helpers.
- **DotNetAstModel** — provides assembly data.
- **TypeLinkResolver** — used to resolve type references to Markdown links in
  table cells.
- **TypeNameSimplifier** — used to build type signature strings.
- **XmlDocReader** — used to retrieve documentation text for each member.
- **IMarkdownWriterFactory** — received from `DotNetEmitter.Emit`.

### Callers

- **DotNetEmitter.Emit** — constructs and calls this class when the format is
  not SingleFile.
