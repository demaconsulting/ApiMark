## DotNetEmitterSingleFile

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

DotNetEmitterSingleFile writes all .NET API documentation into a single
`api.md` file using heading levels offset by `EmitConfig.HeadingDepth`. It
is created exclusively by `DotNetEmitter.Emit` when `EmitConfig.Format` is
`OutputFormat.SingleFile`.

### Data Model

DotNetEmitterSingleFile holds references to:

- *_emitter* (`DotNetEmitter`): Parent emitter providing shared static helpers.
- *_model* (`DotNetAstModel`): Pre-parsed assembly data.

### Key Methods

**DotNetEmitterSingleFile.Emit** (internal): Entry point called by
`DotNetEmitter.Emit`. Dispatches to `EmitSingleFile`.

**EmitSingleFile** (private): Opens a single writer via
`factory.CreateMarkdown("", "api")` and writes the complete documentation tree
into that writer at heading levels `HeadingDepth` (assembly title),
`HeadingDepth+1` (namespace), `HeadingDepth+2` (type), and `HeadingDepth+3`
(individual members).

- Each type section includes a compact bullet list of members
  (`- **Name**: summary`) before the H(depth+3) member sections.
- No group headings (`Constructors`, `Methods`, `Properties`) or convention
  appendix are emitted.
- A `TypeLinkResolver` with `generateLinks: false` is used so that parameter
  type cells contain plain text rather than relative file links that are
  meaningless inside a single document.

**WriteSingleFileTypeSections** (private): Writes all content for a single type —
type heading, signature code block, summary, remarks, example, compact member
bullet list, and then dispatches to `WriteSingleFileMemberSection` for each
visible member. Recursively calls `WriteSingleFileNestedTypes` when the type
contains nested types.

**WriteSingleFileMemberSection** (private): Writes the full per-member block for
one member — member heading, signature code block, summary, parameter table,
returns documentation, exception table, and example block.

**WriteSingleFileNestedTypes** (private): Recursively emits documentation for
nested types within a type section by calling `WriteSingleFileTypeSections` for
each visible nested type at the appropriate heading level.

### Error Handling

Exceptions from `IMarkdownWriterFactory.CreateMarkdown` or from writer methods
propagate unchanged to the caller. No exceptions are caught or suppressed by
this class.

### Dependencies

- **DotNetEmitter** — parent emitter providing shared static helpers.
- **DotNetAstModel** — provides assembly data.
- **TypeNameSimplifier** — used to build type signature strings.
- **XmlDocReader** — used to retrieve documentation text for each member.
- **IMarkdownWriterFactory** — received from `DotNetEmitter.Emit`.

### External Interfaces

N/A — DotNetEmitterSingleFile is an internal class with no external interfaces.

### Callers

- **DotNetEmitter.Emit** — constructs and calls this class when
  `config.Format == OutputFormat.SingleFile`.
