## VhdlEmitter

![VhdlEmitter Structure](ApiMarkVhdlView.svg)

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

VhdlEmitter implements `IApiEmitter`, validates the mandatory `factory` argument,
and dispatches to `VhdlEmitterGradualDisclosure` or `VhdlEmitterSingleFile` based
on `config.Format`. It also holds shared constants and a helper used by both
format-specific emitters.

### Data Model

**VhdlEmitter** (internal sealed class):

- `_options`: `VhdlGeneratorOptions` — generator options forwarded from `VhdlGenerator`.
- `_fileModels`: `IReadOnlyList<VhdlFileModel>` — all parsed file models to emit.
- `DescriptionColumnHeader`: `internal const string` — `"Description"`; column header
  shared by both format-specific emitters.
- `NoDescriptionPlaceholder`: `internal const string` — `"*No description provided.*"`;
  cell text used when no summary is available.
- `NoItemsPlaceholder`: `internal const string` — `"*None.*"`; placeholder text used
  when a section has no items.
- `ObjectClassKeywords`: `private static readonly HashSet<string>` — set of VHDL
  object-class keywords (`SIGNAL`, `VARIABLE`, `CONSTANT`, `FILE`) stripped from
  parameter modes by `FormatParamType`.
- `Options`: `VhdlGeneratorOptions` property (internal get) — exposes `_options` to
  format-specific emitters.

### Key Methods

**VhdlEmitter constructor**: Stores options and file models.

- *Parameters*: `VhdlGeneratorOptions options`, `IReadOnlyList<VhdlFileModel> fileModels`.
- *Returns*: a configured `VhdlEmitter` instance.

**VhdlEmitter.Emit** (implements `IApiEmitter`): Dispatches to the appropriate
format-specific emitter.

- *Parameters*: `IMarkdownWriterFactory factory` — must not be null; `EmitConfig config`
  — includes `Format` (`GradualDisclosure` or `SingleFile`) and `HeadingDepth`;
  `IContext context` — logging channel.
- *Returns*: `void`.
- *Preconditions*: `factory` is not null.
- *Postconditions*: all Markdown output files have been written via `factory`.
- *Algorithm*: validates `factory` is not null (throws `ArgumentNullException`);
  returns immediately without writing any output when `_fileModels.Count == 0`;
  when `config.Format == OutputFormat.SingleFile`, delegates to
  `new VhdlEmitterSingleFile(this, _fileModels).Emit(factory, config, context)`;
  otherwise delegates to
  `new VhdlEmitterGradualDisclosure(this, _fileModels).Emit(factory, config, context)`.

**VhdlEmitter.GetSummary** (internal static): Safely extracts the summary text from
an optional doc comment.

- *Parameters*: `VhdlDocComment? doc` — may be null.
- *Returns*: `string?` — `doc.Summary` when `doc` is non-null and `Summary` is
  non-empty; otherwise `null`.

**VhdlEmitter.FormatParamType** (internal static): Formats a subprogram parameter's
type string for display in the parameters table.

- *Parameters*: `VhdlParamDecl param`.
- *Returns*: `string` — object-class keywords (`SIGNAL`, `VARIABLE`, `CONSTANT`,
  `FILE`) are stripped; direction keywords (`IN`, `OUT`, `INOUT`, `BUFFER`) are
  prepended to the type name (e.g. `OUT STD_LOGIC`); bare type names are returned
  as-is.

**VhdlEmitter.SanitizeFileName** (internal static): Replaces characters that are
illegal on Windows or Linux by delegating to `ApiMark.Core.FileNameSanitizer.SanitizeSegment`;
  reserved device names (for example `aux`) receive a `-` suffix so the page is legal on every platform.

- *Parameters*: `string name` — the raw declaration name.
- *Returns*: `string` — safe for use as a file-system file name segment.
- *See also*: `SanitizePageName` (internal static) applies the same mapping to root-level entity and
  package pages and additionally appends `-` to a name equal to the reserved index page `api`
  (case-insensitive), so such a declaration never overwrites `api.md`; the index links use it too.
- *Known limitation*: `VhdlEmitter.cs` does not lowercase or otherwise case-normalize names
  (verified by reading the source). VHDL identifiers are case-insensitive, so entities whose
  names differ only in case produce page names differing only in case and are not
  disambiguated; behavior is unchanged and documented in the user guide.

### Error Handling

- `ArgumentNullException` — thrown by `Emit` when `factory` is null.
- Exceptions from `VhdlEmitterGradualDisclosure` or `VhdlEmitterSingleFile` propagate
  to the caller without wrapping.

### Dependencies

- **IApiEmitter** (ApiMarkCore) — the interface this class implements.
- **IMarkdownWriterFactory** (ApiMarkCore) — received through `Emit`; passed to
  format-specific emitters.
- **VhdlEmitterGradualDisclosure** (internal) — instantiated and called when
  `config.Format` is `GradualDisclosure`.
- **VhdlEmitterSingleFile** (internal) — instantiated and called when
  `config.Format` is `SingleFile`.
- **VhdlAstModel** (internal) — consumes `VhdlFileModel`, `VhdlEntityDecl`,
  `VhdlArchitectureDecl`, `VhdlPackageDecl`, and `VhdlDocComment` record types.

### Callers

- **VhdlGenerator** — constructs a `VhdlEmitter` in `Parse` and returns it to the
  caller as `IApiEmitter`.
