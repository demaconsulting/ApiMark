## CppEmitterGradualDisclosure

![CppEmitterGradualDisclosure Structure](../generated/ApiMarkCppView.svg)

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

CppEmitterGradualDisclosure writes the multi-file Markdown layout for C++ API
documentation. It produces one library entry page, one namespace summary page,
one type page per class or nested class, one detail page per non-operator member,
and dedicated pages for enums, type aliases, and grouped operator overloads.

### Data Model

**PathConventionRows** (private static): path-convention table shown on `api.md`.

| Symbol kind | Path pattern |
| --- | --- |
| Namespace | `{Namespace}.md` |
| Type | `{Namespace}/{TypeName}.md` |
| Member | `{Namespace}/{TypeName}/{MemberName}.md` |
| Nested type | `{Namespace}/{OuterType}/{NestedType}.md` |
| Class-scoped type alias | `{Namespace}/{TypeName}/{AliasName}.md` |
| Free function | `{Namespace}/{FunctionName}.md` |
| Enum | `{Namespace}/{EnumName}.md` |
| Type alias | `{Namespace}/{AliasName}.md` |
| Operators (class) | `{Namespace}/{TypeName}/operators.md` |
| Operators (namespace) | `{Namespace}/operators.md` |

**CppEmitterGradualDisclosure instance fields** (private): state supplied at construction.

- `_emitter`: `CppEmitter` — parent emitter providing options, visibility helpers,
  comment extractors, and signature builders.
- `_namespaceDecls`: `SortedDictionary<string, CppEmitter.NamespaceDeclarations>` —
  sorted map of namespace key → declarations passed in from `CppEmitter`.
- `_cppResolver`: `CppTypeLinkResolver` — type link resolver; used to linkify type
  strings in table cells and to track external type references per page.

### Key Methods

**CppEmitterGradualDisclosure(emitter, namespaceDecls, cppResolver)** (internal constructor):
stores all three arguments as private fields. None of the parameters are null-guarded
in the constructor; the caller `CppEmitter.Emit` always supplies non-null values.

- **Emit / EmitGradualDisclosure**: `public void Emit(IMarkdownWriterFactory factory,
  EmitConfig config, IContext context)` — writes `api.md`, then iterates namespaces,
  types, nested types, free functions, enums, aliases, and operator groups.

  - *Parameters*: `IMarkdownWriterFactory factory`, `EmitConfig config`,
    `IContext context`.
  - *Returns*: void.
  - *Preconditions*: `factory` must not be null.
  - *Algorithm*: calls `WriteApiPage`, then for each namespace calls
    `WriteNamespacePage`, then for each class calls `WriteTypePage` and
    `WriteNestedTypePages`, then calls `WriteFreeFunctionPage`,
    `WriteEnumPage`, `WriteTypeAliasPage`, `WriteClassOperatorsPage`, and
    `WriteNamespaceOperatorsPage` as applicable.

- **WriteApiPage**: `private void WriteApiPage(IMarkdownWriterFactory factory,
  SortedDictionary<string, CppEmitter.NamespaceDeclarations> namespaces)` — writes
  the library entrypoint `api.md`.
  - *Parameters*: `IMarkdownWriterFactory factory`, sorted `namespaces` dictionary.
  - *Preconditions*: `factory` must not be null.
  - *Algorithm*: creates a writer at `("", "api")`, emits the library title heading,
    optional description paragraph, the namespace summary table (Namespace, Declarations,
    Description columns), and the file-naming path-convention appendix table. Emits a
    fallback paragraph when `namespaces` is empty.

- **WriteNamespacePage**: `private static void WriteNamespacePage(IMarkdownWriterFactory factory,
  string nsKey, CppEmitter.NamespaceDeclarations nsDecls, CppTypeLinkResolver cppResolver)` —
  writes the namespace summary page.
  - *Parameters*: `IMarkdownWriterFactory factory`, `string nsKey`, `CppEmitter.NamespaceDeclarations nsDecls`,
    `CppTypeLinkResolver cppResolver`.
  - *Preconditions*: `factory`, `nsKey`, and `nsDecls` must not be null.
  - *Algorithm*: creates a writer at `("", nsKey)`, emits an H1 namespace heading, then
    emits Types, Enums, Type Aliases, Functions, and Operators sub-tables in that order —
    each sub-table is omitted when the corresponding collection is empty. Accumulates external
    type references for the trailing External Types section.

- **WriteTypePage**: `private void WriteTypePage(CppTypePageWriteContext ctx)` — writes a
  type page for a single C++ class or struct.
  - *Parameters*: `CppTypePageWriteContext ctx` — bundles factory, namespace key, namespace
    display name, class, and resolver.
  - *Preconditions*: `ctx` must not be null; `ctx.Class` must not be null.
  - *Algorithm*: creates a writer at `(nsKey, className)`, emits an H1 heading with the
    display name, the signature block (qualified name comment, optional template declaration,
    `#include` directive, optional class declaration line), summary paragraph, extended
    details, note, example, base-types paragraph, then calls `WriteClassMemberTables`. If
    the class has no visible members, no nested classes, and no type aliases, returns after
    emitting the heading, signature block, summary, details, note, example, and base-types
    paragraph; otherwise proceeds to `WriteClassMemberTables`.

- **WriteMemberPage**: `private static void WriteMemberPage(IMarkdownWriterFactory factory,
  string nsKey, string nsDisplayName, CppClass cls, object member, string fileName,
  CppTypeLinkResolver cppResolver)` — writes the detail page for a single class member.
  - *Parameters*: `IMarkdownWriterFactory factory`, `string nsKey`, `string nsDisplayName`,
    `CppClass cls`, `object member` (either `CppFunction` or `CppField`), `string fileName`,
    `CppTypeLinkResolver cppResolver`.
  - *Preconditions*: `factory`, `cls`, and `member` must not be null; `member` must be a
    `CppFunction` or `CppField`.
  - *Algorithm*: creates a writer at `("{nsKey}/{cls.Name}", fileName)`, dispatches to
    `WriteFunctionPage` or `WriteFieldPage` based on the concrete type of `member`, then
    calls `WriteExternalTypesSection`.

- **WriteEnumPage**: `private void WriteEnumPage(IMarkdownWriterFactory factory,
  string nsKey, string nsDisplayName, CppEnum cppEnum)` — writes the detail page for a
  single C++ enum.
  - *Parameters*: `IMarkdownWriterFactory factory`, `string nsKey`, `string nsDisplayName`,
    `CppEnum cppEnum`.
  - *Preconditions*: `factory` and `cppEnum` must not be null.
  - *Algorithm*: creates a writer at `(nsKey, cppEnum.Name)`, emits an H1 heading, the
    qualified-name comment and optional `#include` directive in a signature block, summary,
    extended details, note, example, and a Values table if the enum has enumerators.

- **WriteTypeAliasPage**: `private void WriteTypeAliasPage(IMarkdownWriterFactory factory,
  string nsKey, string nsDisplayName, CppTypeAlias alias, CppTypeLinkResolver cppResolver)` —
  writes a documentation page for a `using` type alias declaration.
  - *Parameters*: `IMarkdownWriterFactory factory`, `string nsKey`, `string nsDisplayName`,
    `CppTypeAlias alias`, `CppTypeLinkResolver cppResolver`.
  - *Preconditions*: `factory` and `alias` must not be null.
  - *Algorithm*: creates a writer at `(nsKey, alias.Name)`, emits an H1 heading, the
    qualified-name comment, optional `#include` directive, and the `using {name} = {underlying}`
    declaration in a signature block, then summary, details, note, example, and External Types
    section. Calls `cppResolver.Linkify` on the underlying type to track any external
    type references.

- **WriteNestedTypePages**: `private void WriteNestedTypePages(IMarkdownWriterFactory factory,
  string parentKey, string parentDisplayName, CppClass cls, CppTypeLinkResolver cppResolver)` —
  recursively writes class-scoped type-alias pages and nested class pages.
  - *Parameters*: `IMarkdownWriterFactory factory`, `string parentKey`, `string parentDisplayName`,
    `CppClass cls`, `CppTypeLinkResolver cppResolver`.
  - *Preconditions*: `factory` and `cls` must not be null.
  - *Algorithm*: computes the class folder key as `{parentKey}/{cls.Name}`, writes one alias page
    per entry in `cls.TypeAliases`, writes one type page per entry in `cls.NestedClasses`, and
    recurses into each nested class.

- **WriteClassMemberTables**: `private void WriteClassMemberTables(IMarkdownWriter writer,
  CppTypePageWriteContext ctx, IReadOnlyList<CppFunction> operatorMethods,
  List<string[]> ctorRows, List<string[]> methodRows, List<string[]> fieldRows,
  SortedSet<CppExternalTypeInfo> externalTypes)` — emits all member sub-tables onto the type page.
  - *Parameters*: writer, ctx, operator methods list, pre-built row lists for constructors,
    methods, and fields, and accumulated external types.
  - *Algorithm*: emits Constructors, Methods, Fields, Operators, Nested Classes, and Type Aliases
    sub-tables in canonical order, each only when the corresponding collection is non-empty.
    Calls `WriteClassOperatorsPage` when operator methods are present. Appends the External Types
    section at the end.

- **ProcessClassConstructorMember**: `private static void ProcessClassConstructorMember(
  CppTypePageWriteContext ctx, CppFunction ctor, IReadOnlyDictionary<string, List<object>>
  caseInsensitiveGroups, HashSet<string> writtenKeys, List<string[]> ctorRows)` — processes a
  single constructor, writing its page and appending the table row.
  - *Parameters*: ctx, the constructor, the case-insensitive groups map, a set of already-written
    page keys, and the row accumulator.
  - *Algorithm*: computes the lowercase key and group; if the page has not been written, writes a
    single member page or a combined collision page; appends the constructor row with parameter
    types in the link text.

- **ProcessClassMethodMember**: `private static void ProcessClassMethodMember(
  CppTypePageWriteContext ctx, CppFunction method, IReadOnlyDictionary<string, List<object>>
  caseInsensitiveGroups, HashSet<string> writtenKeys, List<string[]> methodRows,
  SortedSet<CppExternalTypeInfo> externalTypes)` — processes a single regular method,
  writing its page and appending the table row.
  - *Parameters*: ctx, the method, case-insensitive groups, written-key tracker, row
    accumulator, and external-types set.
  - *Algorithm*: same as `ProcessClassConstructorMember` for page writing; linkifies the return
    type via the resolver and appends a row with member link, return type, and summary.

- **ProcessClassFieldMember**: `private static void ProcessClassFieldMember(
  CppTypePageWriteContext ctx, CppField field, IReadOnlyDictionary<string, List<object>>
  caseInsensitiveGroups, HashSet<string> writtenKeys, List<string[]> fieldRows,
  SortedSet<CppExternalTypeInfo> externalTypes)` — processes a single visible field,
  writing its page and appending the table row.
  - *Parameters*: ctx, the field, case-insensitive groups, written-key tracker, row
    accumulator, and external-types set.
  - *Algorithm*: same as `ProcessClassMethodMember`; linkifies the field type and appends a
    row with member link, type, and summary.

- **WriteClassSignatureBlock**: `private void WriteClassSignatureBlock(IMarkdownWriter writer,
  CppClass cls, string qualifiedClassName)` — emits the fenced C++ signature block for a class.
  - *Parameters*: `IMarkdownWriter writer`, `CppClass cls`, `string qualifiedClassName`.
  - *Algorithm*: calls `GetIncludePath` on the source file, builds a signature string from the
    qualified name comment, optional template declaration, `#include` directive, and optional
    class declaration line, then calls `writer.WriteSignature`. Returns immediately without
    emitting any signature content when `cls.Location` is null or `cls.Location.File` is null
    or empty.

- **WriteClassBaseTypesParagraph**: `private static void WriteClassBaseTypesParagraph(
  IMarkdownWriter writer, CppClass cls)` — emits the `**Inherits**: …` paragraph when the class
  has base types.
  - *Parameters*: `IMarkdownWriter writer`, `CppClass cls`.
  - *Algorithm*: returns immediately if `cls.BaseTypes` is empty; otherwise collects simplified
    base type names and emits a bold Inherits paragraph.

- **WriteFunctionPage / WriteFunctionContent**: `private static void WriteFunctionPage(...)` and
  `internal static void WriteFunctionContent(IMarkdownWriter writer, CppFunction method,
  CppFunctionWriteContext ctx)` — write the H1 heading and body for a class method page.
  - *Parameters*: writer, method, context bundling namespace, class name, resolver, folder,
    external types, and heading level.
  - *Algorithm*: emits a qualified-name comment + signature block, summary, details, note,
    example, optional Parameters table, and optional Returns section.

- **WriteFieldPage / WriteFieldContent**: `private static void WriteFieldPage(...)` and
  `internal static void WriteFieldContent(IMarkdownWriter writer, string nsDisplayName,
  string className, CppField field)` — write the H1 heading and body for a class field page.
  - *Parameters*: writer, namespace display name, class name, field.
  - *Algorithm*: emits a qualified-name comment + field-type signature, summary, details, note,
    and example.

- **WriteFreeFunctionPage / WriteFreeFunctionContent**: `private void WriteFreeFunctionPage(
  IMarkdownWriterFactory factory, string nsKey, string nsDisplayName, CppFunction fn,
  CppTypeLinkResolver cppResolver)` and the corresponding content helper — write the detail page
  for a namespace-level free function.
  - *Parameters*: factory, namespace key and display name, function, resolver.
  - *Algorithm*: creates a writer at `(nsKey, sanitizedFunctionName)`, emits a qualified-name
    comment + `#include` + signature block, summary, details, note, example, optional Parameters
    table, and optional Returns section. Appends External Types section.

- **WriteClassOperatorsPage**: `private void WriteClassOperatorsPage(IMarkdownWriterFactory
  factory, string nsKey, string nsDisplayName, CppClass cls, IReadOnlyList<CppFunction>
  operators, CppTypeLinkResolver cppResolver)` — writes the combined operator overloads page for
  a class.
  - *Parameters*: factory, namespace key and display name, class, list of operator methods,
    resolver.
  - *Preconditions*: `operators` must not be empty.
  - *Algorithm*: creates a writer at `("{nsKey}/{cls.Name}", "operators")`, emits an H1
    heading, optional qualified name + `#include` signature block, a description paragraph, then
    an H2 section per operator with its full function content. Appends External Types section.

- **WriteNamespaceOperatorsPage**: `private void WriteNamespaceOperatorsPage(IMarkdownWriterFactory
  factory, string nsKey, string nsDisplayName, IReadOnlyList<CppFunction> operators,
  CppTypeLinkResolver cppResolver)` — writes the combined operator overloads page for a
  namespace.
  - *Parameters*: factory, namespace key and display name, list of operator free functions,
    resolver.
  - *Preconditions*: `operators` must not be empty.
  - *Algorithm*: creates a writer at `(nsKey, "operators")`, emits an H1 heading, optional
    qualified name + `#include` signature block, a namespace description paragraph, then an H2
    section per operator using `WriteFreeFunctionContent`. Appends External Types section.

### Error Handling

N/A - factory and writer exceptions are propagated without additional wrapping.

### External Interfaces

#### IMarkdownWriterFactory (consumed)

- *Type*: in-process .NET interface.
- *Role*: consumer.
- *Contract*: provides one writer per generated page.
- *Constraints*: every requested page key must be creatable.

### Dependencies

- **CppEmitter** — provides shared helper methods and generator options.
- **CppTypeLinkResolver** — linkifies table-cell types and tracks external types.
- **CppAstModel** — supplies classes, functions, fields, enums, and aliases.
- **IMarkdownWriterFactory** — creates the individual output pages.

### Callers

- **CppEmitter.Emit** — instantiates this unit when
  `EmitConfig.Format == OutputFormat.GradualDisclosure`.
