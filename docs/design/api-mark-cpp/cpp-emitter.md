## CppEmitter

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

CppEmitter implements `IApiEmitter` for C++ documentation. It holds pre-parsed
namespace declarations and dispatches Markdown generation to the appropriate
format-specific emitter: `CppEmitterGradualDisclosure` for the multi-file
gradual-disclosure layout, or `CppEmitterSingleFile` for the compact single-file
layout. CppEmitter also provides all shared helper methods used by both emitters:
visibility and deprecated filters, doc-comment extractors, signature builders,
include-path resolution, and filename sanitization.

### Data Model

**CppEmitter.NamespaceDeclarations** (internal class): Mutable accumulator used
during `CppGenerator.Parse` to group owned declarations by namespace. Converted
by the caller into the immutable data passed to `CppEmitter`.

- `DisplayName`: `string` — the C++ qualified namespace name using `::` separators
  (e.g. `"mylib::rendering"`). Used in heading text.
- `Doc`: `CppDocComment?` — optional namespace-level doc comment.
- `Classes`: `List<CppClass>` — owned classes and structs in this namespace.
- `FreeFunctions`: `List<CppFunction>` — owned free functions (non-member) in
  this namespace.
- `Enums`: `List<CppEnum>` — owned enum declarations in this namespace.
- `TypeAliases`: `List<CppTypeAlias>` — owned `using` type aliases in this namespace.

### Key Methods

**CppEmitter.Emit** (implements `IApiEmitter`): Dispatches to the appropriate
format-specific emitter.

- *Parameters*: `IMarkdownWriterFactory factory` — must not be null; throws
  `ArgumentNullException` immediately when null is passed. `EmitConfig config` —
  includes `Format` (GradualDisclosure or SingleFile) and `HeadingDepth`.
  `IContext context` — logging channel.
- *Returns*: `void`
- *Algorithm*: when `config.Format == OutputFormat.SingleFile`, creates a new
  `CppEmitterSingleFile` and calls its `Emit`; otherwise creates a new
  `CppEmitterGradualDisclosure` and calls its `Emit`.

**CppEmitter.SanitizeFileName** (internal static): Replaces characters that are
invalid in file names on Windows or Unix with underscore.

- *Parameters*: `string name` — C++ declaration name to sanitize. Must not be null.
- *Returns*: A copy of `name` with every character from
  `Path.GetInvalidFileNameChars()` replaced by `_`.
- *Algorithm*: Converts `name` to a char array, iterates, replaces invalid chars,
  returns a new string.

**CppEmitter.BuildClassDeclaration** (internal static): Builds the one-line class
declaration shown in the signature block.

- *Parameters*: `CppClass cls` — the class to describe.
- *Returns*: A string of the form `"class ClassName"`,
  `"class ClassName final"`, or `"class ClassName : public Base1, public Base2"`.
- *Algorithm*: starts with `"class {cls.Name}"`; appends `" final"` when
  `cls.IsFinal`; appends `" : public {b.Name}"` for each base type.

**CppEmitter.WriteCombinedMemberPage** (internal): Writes a single combined page for
members whose base names collide on case-insensitive filesystems.

- *Parameters*: factory, nsKey, nsDisplayName, cls, lowerKey (the shared lowercase
  key), members list (at least two; functions or fields).
- *Returns*: `void`
- *Algorithm*: Creates `{nsKey}/{cls.Name}/{lowerKey}.md`; writes H1 heading using
  `lowerKey`; for each function member writes an H2 heading and delegates to
  `WriteFunctionContent`; for each field member writes an H2 heading and delegates
  to `WriteFieldContent`.

### Error Handling

- `ArgumentNullException` — thrown by `Emit` when `factory` is null.
- Format-specific exceptions are propagated from `CppEmitterGradualDisclosure`
  or `CppEmitterSingleFile` without wrapping.

### External Interfaces

**IApiEmitter (provided)**: CppEmitter implements this interface from ApiMarkCore.

- *Type*: In-process .NET public API.
- *Role*: Provider — `CppGenerator.Parse` returns a `CppEmitter` to the caller,
  which then invokes `IApiEmitter.Emit`.
- *Contract*: `Emit(factory, config, context)` must write a complete Markdown
  tree via the supplied factory and must not throw except for null arguments.

### Dependencies

- **IMarkdownWriterFactory** (ApiMarkCore) — received through `Emit`; each
  format-specific emitter calls `CreateMarkdown` to obtain per-file writers.
- **CppEmitterGradualDisclosure** — instantiated and called by `Emit` when
  `config.Format` is `GradualDisclosure`.
- **CppEmitterSingleFile** — instantiated and called by `Emit` when
  `config.Format` is `SingleFile`.
- **CppTypeLinkResolver** — held and forwarded to both format-specific emitters
  for Markdown link generation in table cells.
- **CppAstModel** — consumes `CppNamespaceDeclarations`, `CppClass`,
  `CppFunction`, `CppField`, `CppEnum`, `CppTypeAlias` record types.

### Callers

- **CppGenerator** — constructs a `CppEmitter` in `Parse` and returns it to the
  caller as `IApiEmitter`.
