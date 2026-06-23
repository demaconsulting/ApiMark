## CppEmitterGradualDisclosure

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

CppEmitterGradualDisclosure writes the multi-file gradual-disclosure Markdown
tree for C++ API documentation. It produces one file per namespace, one file per
type, one file per visible member (or combined page for case-insensitive
collisions), one file per enum, one file per type alias, and one operators page
per class and per namespace that contains operator free functions. This layout
allows AI agents and human readers to navigate progressively from the library
index to a namespace, then to a type, and finally to an individual member.

### Data Model

**PathConventionRows** (private static): Table rows listing the path convention
for each symbol kind, shown on the api index page.

| Symbol kind | Path pattern |
| --- | --- |
| Namespace | `{Namespace}.md` |
| Type | `{Namespace}/{TypeName}.md` |
| Member | `{Namespace}/{TypeName}/{MemberName}.md` |
| Free function | `{Namespace}/{FunctionName}.md` |
| Enum | `{Namespace}/{EnumName}.md` |
| Type alias | `{Namespace}/{AliasName}.md` |
| Operators (class) | `{Namespace}/{TypeName}/operators.md` |
| Operators (namespace) | `{Namespace}/operators.md` |

### Key Methods

**CppEmitterGradualDisclosure.Emit** (internal): Entry point; writes the complete
gradual-disclosure tree.

- *Parameters*: `IMarkdownWriterFactory factory`, `EmitConfig config`,
  `IContext context`.
- *Returns*: `void`
- *Algorithm*: Calls `EmitGradualDisclosure(factory)` which calls `WriteApiPage`,
  then iterates over namespace declarations calling `WriteNamespacePage`,
  `WriteTypePage`, free-function pages, enum pages, and type-alias pages.

**WriteApiPage** (private): Writes the library entrypoint `api.md`.

- Emits an H1 library-name heading, optional description paragraph, an
  all-namespaces table (Namespace, Declarations, Description columns), and the
  path convention appendix table.

**WriteNamespacePage** (private): Writes a namespace summary page at
`{nsKey}.md`.

- Lists all owned classes, enums, free functions, and type aliases grouped by
  source header. Operator free functions are referenced via a link to the
  namespace operators page rather than listed individually.

**WriteTypePage** (private): Writes a type page at `{nsKey}/{typeName}.md`.

- Emits the qualified name, optional template declaration, `#include` directive,
  summary, details, note, example, base types, and grouped member tables
  (Constructors, Methods, Fields). Partitions methods into regular and operator;
  builds a case-insensitive collision map for members; emits individual pages
  for collision-free members, combined pages for collision groups, and an
  operators page when operator overloads are present.

**WriteFreeFunctionPage** (private): Writes a free-function page at
`{nsKey}/{functionName}.md`.

**WriteEnumPage** (private): Writes an enum page at `{nsKey}/{enumName}.md`.

- Emits the qualified name comment, `#include` directive, summary, and a table
  of all enum values with their descriptions.

**WriteClassOperatorsPage** (private): Writes the class-level operators page at
`{nsKey}/{typeName}/operators.md`.

**WriteNamespaceOperatorsPage** (private): Writes the namespace-level operators
page at `{nsKey}/operators.md`.

**WriteTypeAliasPage** (private): Writes a type alias page at
`{nsKey}/{aliasName}.md`.

- Emits the qualified name comment, `#include` directive, `using` declaration,
  summary, and optional details.

### Error Handling

N/A - CppEmitterGradualDisclosure propagates exceptions from the factory and
writer without wrapping. No additional error handling is performed.

### External Interfaces

**IMarkdownWriterFactory (consumed)**: Received from `CppEmitter.Emit` and used
to create each per-file Markdown writer via `CreateMarkdown`.

- *Type*: In-process .NET interface.
- *Role*: Consumer — calls `CreateMarkdown(folder, fileName)` for each output page.
- *Contract*: The factory must produce a valid `IMarkdownWriter` for every call;
  callers `using`-dispose each writer after writing.

### Dependencies

- **CppEmitter** — parent emitter providing options, visibility helpers, comment
  extractors, signature builders, and `WriteCombinedMemberPage`.
- **CppTypeLinkResolver** — used to resolve type strings to Markdown links in
  table cells.
- **CppAstModel** — consumes all record types produced by `ClangAstParser`.
- **IMarkdownWriterFactory** (ApiMarkCore) — supplies per-file Markdown writers.

### Callers

- **CppEmitter.Emit** — instantiates and calls `CppEmitterGradualDisclosure.Emit`
  when `config.Format` is `GradualDisclosure`.
