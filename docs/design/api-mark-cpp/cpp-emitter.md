## CppEmitter

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

CppEmitter implements `IApiEmitter` for C++ documentation. It stores the parsed
namespace declarations, dispatches to the format-specific emitter selected by
`EmitConfig.Format`, and centralizes the shared helper methods used by both the
single-file and gradual-disclosure layouts.

### Data Model

**CppEmitter.NamespaceDeclarations** (internal class): mutable namespace-grouping
accumulator populated during `CppGenerator.Parse`.

- `DisplayName`: `string` — qualified namespace display name using `::`.
- `Doc`: `CppDocComment?` — optional namespace documentation.
- `Classes`: `List<CppClass>` — owned classes and structs.
- `Enums`: `List<CppEnum>` — owned enums.
- `TypeAliases`: `List<CppTypeAlias>` — owned namespace-level aliases.
- `FreeFunctions`: `List<CppFunction>` — owned free functions.

**CppExternalTypeInfo** (internal record, defined in `CppExternalTypeInfo.cs`):
per-page external-type entry emitted in an `External Types` section — see
_CppTypeLinkResolver Data Model_ for the full definition.

**CppTypePageWriteContext** (internal record): bundles per-type-page constants for
member page and table generation.

- `Factory`: `IMarkdownWriterFactory`
- `NsKey`: `string`
- `NsDisplayName`: `string`
- `Class`: `CppClass`
- `CppResolver`: `CppTypeLinkResolver`

**CppFunctionWriteContext** (internal record): bundles function-page constants used
by `WriteFunctionContent`.

- `NsDisplayName`: `string`
- `ClassName`: `string`
- `CppResolver`: `CppTypeLinkResolver`
- `CurrentFolder`: `string`
- `ExternalTypes`: `ISet<CppExternalTypeInfo>`
- `ParametersHeadingLevel`: `int`

### Key Methods

#### Dispatch

- **CppEmitter.Emit**: `public void Emit(IMarkdownWriterFactory factory, EmitConfig
  config, IContext context)` — chooses `CppEmitterSingleFile` when
  `config.Format == OutputFormat.SingleFile`; otherwise chooses
  `CppEmitterGradualDisclosure`.
  - _IContext description_: output channel for diagnostic messages; not used by the
    emitter itself but satisfies the interface contract.
  - _Preconditions_: `factory` must not be null.
  - _Exceptions_: `ArgumentNullException` when `factory` is null.

#### Visibility filtering helpers

- **GetVisibleConstructors** — returns visible constructors after applying
  `ApiVisibility` and deprecated filtering.
- **GetVisibleMethods** — returns visible non-constructor methods after applying
  `ApiVisibility` and deprecated filtering.
- **GetVisibleFields** — returns visible fields after applying `ApiVisibility` and
  deprecated filtering.
- **IsVisibleMember** — evaluates a single `CppAccessibility` against
  `CppGeneratorOptions.Visibility`.

#### Comment extraction helpers

- **GetSummary** — returns the `@brief` or first-paragraph summary.
- **GetDetails** — returns `@details` / `@remarks` text.
- **GetNote** — returns `@note` text.
- **GetExample** — returns `@code` / `@endcode` example text.
- **GetParamDescription** — resolves a parameter description by name.
- **GetReturnDescription** — returns `@return` / `@returns` text.
- **GetNamespaceDescription** — returns the namespace summary or the standard
  no-description placeholder.

#### Signature builders

- **BuildMethodSignature** — builds method, constructor, and variadic signatures,
  including default values and `= delete` when `CppFunction.IsDeleted` is true.
- **BuildClassDeclaration** — renders `class Name`, optional `final`, and direct
  inheritance (`: public Base1, public Base2`).
- **BuildTemplateParamDisplay** — renders `<T, U>` for headings and qualified names.
- **BuildTemplateDeclaration** — renders `template<typename T, ...>` for signature
  blocks.
- **SimplifyTypeName** — replaces verbose clang STL spellings with user-facing
  equivalents such as `std::string`.

#### File and page helpers

- **SanitizeFileName** — replaces any invalid filesystem character reported by
  `Path.GetInvalidFileNameChars()` with `_`.
- **GetIncludePath** — derives the canonical `#include` path relative to the
  longest matching public include root.
- **GetMemberBaseName** — returns the class name for constructors and the member
  name for methods or fields.
- **FileSystemPathComparison** / **FileSystemPathComparer** — static properties that
  select `OrdinalIgnoreCase` (Windows/macOS) or `Ordinal` (Linux) for all path
  comparisons in the emitter, ensuring include-path and page-key matching respects
  native file-system case-sensitivity.
- **WriteCombinedMemberPage**: `internal static void WriteCombinedMemberPage(
  IMarkdownWriterFactory factory, string nsKey, string nsDisplayName, CppClass cls,
  string lowerKey, IReadOnlyList<object> members, CppTypeLinkResolver cppResolver)` —
  writes the shared page for case-insensitive member-name collisions.
- **WriteExternalTypesSection** — writes the trailing `## External Types` table when
  at least one `CppExternalTypeInfo` entry was collected on the page.

### Error Handling

- `ArgumentNullException` — thrown by `Emit` when `factory` is null.
- `ArgumentNullException` — thrown by `GetIncludePath` when `sourceFile` is null.
- `ArgumentException` — thrown by `WriteCombinedMemberPage` when `members` contains fewer than two elements.
- All writer and factory exceptions are propagated without wrapping.

### External Interfaces

#### IApiEmitter (provided)

`CppEmitter` implements the ApiMarkCore emission contract.

- _Type_: in-process .NET interface.
- _Role_: provider.
- _Contract_: `Emit` writes the full Markdown output using the supplied factory.
- _Constraints_: callers must supply a non-null factory and a fully parsed emitter.

### Dependencies

- **IMarkdownWriterFactory** — creates per-page writers.
- **CppEmitterGradualDisclosure** — multi-file output implementation.
- **CppEmitterSingleFile** — single-file output implementation.
- **CppTypeLinkResolver** — linkifies table-cell types and tracks external types.
- **CppAstModel** — consumes `CppClass`, `CppFunction`,
  `CppField`, `CppEnum`, and `CppTypeAlias` records.

### Callers

- **CppGenerator** — constructs and returns `CppEmitter` from `Parse`.
