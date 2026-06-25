## CppEmitterGradualDisclosure

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
| Free function | `{Namespace}/{FunctionName}.md` |
| Enum | `{Namespace}/{EnumName}.md` |
| Type alias | `{Namespace}/{AliasName}.md` |
| Class-scoped type alias | `{Namespace}/{TypeName}/{AliasName}.md` |
| Operators (class) | `{Namespace}/{TypeName}/operators.md` |
| Operators (namespace) | `{Namespace}/operators.md` |

### Key Methods

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
- **WriteApiPage** — writes the library entrypoint, namespace table, and path
  convention appendix.
- **WriteNamespacePage** — writes the namespace summary page using alphabetical
  sub-tables for Types, Enums, Type Aliases, Functions, and Operators; it does not
  group declarations by source header.
- **WriteTypePage** — writes a type page with signature block, summary, optional
  details/note/example, base-class paragraph, member tables, nested-class table,
  and type-alias table.
- **WriteNestedTypePages** — recursively writes nested class pages and class-scoped
  type-alias pages beneath the parent class folder.
- **WriteClassMemberTables** — writes Constructors, Methods, Fields, Operators,
  Nested Classes, and Type Aliases sections in canonical order.
- **ProcessClassConstructorMember / ProcessClassMethodMember /
  ProcessClassFieldMember** — build table rows and create member pages or combined
  collision pages.
- **WriteClassSignatureBlock / WriteClassBaseTypesParagraph** — render type-page
  signature context and inheritance text.
- **WriteMemberPage / WriteFunctionPage / WriteFieldPage / WriteFunctionContent /
  WriteFieldContent** — write per-member detail pages.
- **WriteFreeFunctionPage / WriteFreeFunctionContent** — write namespace-level free
  function pages.
- **WriteEnumPage** — writes enum pages and values tables.
- **WriteTypeAliasPage** — writes namespace-level and class-scoped alias pages.
- **WriteClassOperatorsPage** — writes `{nsKey}/{TypeName}/operators.md` for all
  class-scoped operators.
- **WriteNamespaceOperatorsPage** — writes `{nsKey}/operators.md` for all
  namespace-level operators.

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
