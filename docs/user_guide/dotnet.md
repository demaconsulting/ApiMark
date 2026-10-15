# .NET Documentation

## Prerequisites

The ApiMark CLI tool requires the .NET SDK 8.0 or later. See the *Installation* section
for CLI tool and MSBuild package installation steps.

To enable XML documentation generation so ApiMark can read doc comments, add the
following to your `.csproj`:

```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
</PropertyGroup>
```

Without this setting, the compiler does not emit the XML doc file and ApiMark has no
documentation to read. The assembly is still documented structurally (types, signatures)
but all doc-comment content will be absent.

## CLI Options

```text
apimark dotnet [options]
```

| Option | Description |
| --- | --- |
| `--assembly <path>` | Path to the .NET assembly (required) |
| `--xml-doc <path>` | Path to the XML documentation file |
| `--output <dir>` | Output directory for Markdown files (required) |
| `--format <value>` | Output format: `gradual` (file-per-type) or `single-file` (single `api.md`) (default: `gradual`) |
| `--visibility <value>` | Visibility filter: `Public`, `PublicAndProtected`, `All` (default: `PublicAndProtected`) |
| `--include-obsolete` | Include obsolete members in generated output |
| `--exclude <pattern>` | Exclude namespaces/types matching a wildcard pattern (repeatable) |
| `--external-link <Assembly=path>` | Link types from the named referenced assembly to its own ApiMark documentation at `path` (a path relative to this API root, or an `http(s)` base URL). The assembly's DLL must also be supplied via `--reference-paths`. Repeatable |
| `--reference-paths <path>` | Path to a referenced assembly DLL, used to resolve cross-assembly `<inheritdoc/>` (repeatable; also accepts `@<file>` to read paths/flags from a response file, one argument per line) |
| `--library-description <text>` | Optional description for the `api.md` introduction paragraph; overrides the assembly's compiled `AssemblyDescriptionAttribute` when set |
| `--enforce-docs <value>` | Enable documentation-coverage enforcement at the given visibility tier: `Public`, `PublicAndProtected`, `All` (default: disabled) |
| `--enforce-docs-severity <value>` | Severity when undocumented items are found: `Warning` (report only) or `Error` (fail the build) (default: `Warning`) |

## Documented Constructs

ApiMark reflects over a .NET assembly and produces documentation for the following
constructs.

### Namespaces

Each namespace that contains at least one documented type gets a namespace summary page.
To provide a description for the namespace itself, declare an
`internal static class NamespaceDoc` inside that namespace and attach the
namespace-level XML doc comment (`/// <summary>`, and optionally `/// <remarks>` and
`/// <example>`) to it. ApiMark recognizes this carrier class by its exact shape
(`internal static class NamespaceDoc` declared within the namespace) and excludes it
from the output. Its `<summary>`, `<remarks>`, and `<example>` are all surfaced on the
namespace page — the summary and remarks as paragraphs and the example as its structured
parts: prose is rendered as paragraphs and code as fenced code blocks (a single example
may therefore produce multiple paragraphs and/or code blocks).

Types declared without a namespace (the C# global namespace) are documented under a
synthetic namespace named `(global namespace)`: its page is `global-namespace.md`, its
types live in `global-namespace/{type}.md`, and it is listed in `api.md` like any other
namespace. The page name cannot collide with a real namespace, because a C# namespace
identifier cannot contain a hyphen.

### Types

Public classes, interfaces, structs, enums, and delegates are documented. The set of
visible types is controlled by `--visibility`:

- `Public` — public types only
- `PublicAndProtected` — public and protected members (default)
- `All` — all types including internal

> **Default visibility.** The default for C# is `PublicAndProtected`, so protected members appear in
> output unless you pass `--visibility Public` (or set `ApiMarkVisibility` to `Public`). C++ and VHDL
> default to `Public`. This setting is independent of `--enforce-docs`, which has its own tier and
> stays disabled unless set.

### Members

Methods, properties, fields, events, and constructors are documented on the type page.
Operator overloads are grouped together on a dedicated `operators.md` page within the
type folder. `const` fields and enum members render their compile-time value in the
signature (e.g. `public const string DefaultName = "default"`, `Active = 0`).

A type that derives from other types also gets an **Inherited Members** section (gradual disclosure and
single file). It lists, per declaring type, the properties, methods, fields, and events the type inherits
and that are visible at the active visibility tier, excluding constructors, operators, `Object` members,
and members the type overrides or hides. Inherited members get no pages of their own and are not counted
by `--enforce-docs`. The visibility tier applies to inherited members too: `Public` lists public members,
`PublicAndProtected` also lists protected and protected internal members (for example a protected field of an
external base class), and `All` lists every non-private member. A base type that has a page is linked in the
"Inherited from" heading, including nested base types; an unmapped external base appears as plain text. If a
base type's assembly cannot be resolved, a note says so; supply the assembly with
`--reference-paths` to list its members.

The primary constructor of a positional `record` or `record struct` is described as "Initializes a new
instance of the `Type` record." instead of repeating the type summary the compiler copies onto it;
explicitly documented constructors keep their own text.

### Signature Details

Signatures follow C# source syntax: parameter default values (`int count = 5`), per-type
nullability (`Func<Wire, WireStyle?>?`, `Point?`), `record`, `record struct`, `readonly struct`,
`ref struct` and `readonly ref struct` type kinds, `init` accessors, and generic method type
parameters with `where` constraints (`Map<T>(...) where T : class, new()`). Display names used for
file names and overload grouping include type parameters but never default values.

Additional details rendered in signatures:

- `params` arrays and collections (`Sum(params int[] values)`); `void` is never marked nullable.
- Tuples use tuple syntax with element names and per-element nullability
  (`(int Id, string? Name)?`).
- `virtual`, `override`, `sealed override` and `abstract` modifiers on methods, properties and
  events; interface members and interface implementations carry no modifier.
- Property accessors that are not visible at the selected `--visibility` tier are omitted
  (a `private set` or `internal set` does not appear at the default tiers).
- Types from referenced assemblies always appear by their short name (`ExternalBaseClass`, never
  `External.ExternalBaseClass`).
- The `new` modifier is recovered for fields, constants, properties, events, and methods that hide an
  accessible base member, and `where` clauses show `notnull`, `unmanaged`, `struct`, `class`, `class?`,
  `new()`, and nullable type constraints. Compiled metadata does not record some source constructs, so
  these limits remain: `new` on a member that hides nothing cannot be reproduced; `new` is omitted when the
  base type's assembly is not resolved (use `--reference-paths`) and when the hidden member is `internal` in
  another assembly; `notnull` is not recoverable from code compiled without nullable annotations; and
  `allows ref struct` constraints are not shown. When in doubt the tool omits a modifier rather than
  inventing one.
- Overview rows list parameter names, defaults, nullability and `params`, and prefix non-public
  members with their accessibility (for example `protected OnResized()`) so overloads are
  distinguishable; page headings use the type-only form.

### Generic types and navigation

Generic types use escaped headings (`PropertyEditorRegistry\<TEditor\>`), a Type Parameters
table taken from `<typeparam>`, and `where` constraints in signatures. Their file names use brace arity, `Name{arity}` (for example `PropertyEditorRegistry{1}.md` for
`PropertyEditorRegistry<TEditor>` and `KeyedPair{2}.md` for `KeyedPair<TKey, TValue>`). Braces cannot
appear in a C# identifier, so `Foo{1}.md` (for `Foo<T>`) never collides with `Foo1.md` (for a type named
`Foo1`), and `Foo.md`, `Foo{1}.md` and `Foo{2}.md` stay distinct. Earlier releases wrote `Foo-1.md` (and
before that `Name1.md`); links that point at the old names must be updated. Overload rows
list every distinct return type (including `void`); the row shows the summary only when every overload has the same summary; when they differ, or any overload has
none, it shows `Overloaded; see the member page.` instead of one overload's summary. Cross-reference links to an overload
include the signature in the link text (`UndoHistory.Transact(string, Action)`) and end with an anchor to
that overload's heading on the shared page. `<paramref>`, `<typeparamref>`, and `<see langword>` render as
code spans. In cref link text a single type parameter is shown as `T` and several as `T1`..`Tn`, because the
real names are not in the cref. A text-only `<example>` is shown as prose. Namespace pages start with
a breadcrumb back to `api.md`, and `api.md` has a Start here link and a Key Types column.

### Legal File Names

Every generated file and folder name is legal on Windows and Linux, and unique when compared without
regard to case. All names are decided up front by one registry, before any file or link is written; the
complete numbered rules are in `docs/design/api-mark-core/file-naming-rules.md`. In short:

- A name that is already legal is used as written (`Widget.md`).
- Generic types use brace arity: `Foo<T>` is `Foo{1}.md` and `Foo<T1,T2>` is `Foo{2}.md`. Generic methods
  get no arity.
- Explicit interface members keep their type arguments: `IFoo<int>.Get` is `IFoo{int}.Get.md`, and several
  arguments are joined with `-` (`IPair{int-string}.Get.md`).
- Illegal or link-hostile characters (`: " | ? * \ /`, control characters, spaces, brackets) become `_`.
  Reserved Windows names (`CON`, `NUL`, `PRN`, `AUX`, `COM1`, `LPT1`, and so on) get a `_` suffix
  (`CON_.md`).
- When two symbols in one folder would get the same name (compared case-insensitively), the symbol whose
  own name is unchanged keeps it, and a namespace beats a type, which beats a member. The other symbol is
  displaced to a transformed name and only as a last resort gets a numeric suffix: `Name.md` and
  `name-2.md`. This covers namespaces that clash with types, case twins, members that clash with the
  reserved `operators` page, and a root namespace named `api`.
- Symbols that are not visible are named as if they were visible, so names do not change when you change
  the visibility filter.

Limitation: a link to a type in an external (mapped) assembly uses that type's predictable name, and is
wrong if the other package had to displace it with a numeric suffix. This is a breaking change from
earlier releases (`Foo-1.md` is now `Foo{1}.md`, hash-suffixed names are now `_` and `-N` names); update
any links that point at the old file names.

### Breadcrumbs

In gradual-disclosure output, every page except `api.md` begins with a breadcrumb line directly under the
heading, such as `[API Reference](../../api.md) › [Namespace](../../Namespace.md) › [Type](../Type.md)`, to
navigate back up the tree.
Single-file output has no breadcrumbs.

### Nested Types

Nested types get their own page in the gradual-disclosure tree and also appear in a
*Nested Types* section on the parent type page.

### Obsolete Members

Members annotated with `[Obsolete]` are excluded from the output by default. Pass
`--include-obsolete` to include them.

### Excluding Namespaces and Types

Pass one or more `--exclude <pattern>` flags to omit namespaces and types from
generated documentation, such as ANTLR-generated parser code. Each pattern may
contain `*` as a wildcard matching any sequence of characters and is matched
against both a type's full namespace-qualified name and its containing
namespace. For example, `--exclude "Antlr4.*"` excludes every namespace and
type under `Antlr4`. A namespace whose every type is excluded (whether by
`--exclude` or by the visibility/obsolete filters above) does not appear in
any generated index or page.

### Cross-Assembly `<inheritdoc/>` Resolution

A member documented with a bare `<inheritdoc/>` (or an explicit
`<inheritdoc cref="..."/>`) normally inherits its documentation from a base
class, base interface, or overridden member within the *same* assembly being
documented. When the base class or interface instead lives in an externally
referenced assembly — for example a NuGet package dependency — pass one or
more `--reference-paths <path>` flags pointing at that assembly's DLL so
ApiMark can resolve the reference and inherit its documentation too.

For each configured reference path, ApiMark looks for that assembly's XML
documentation file next to the DLL (the same `<name>.xml` convention used for
the primary assembly). If no sibling XML file is found, ApiMark also tries
swapping a `ref`/`lib` path segment (some NuGet packages ship the reference
assembly under a `ref/` folder but the matching XML documentation only under
the corresponding `lib/` folder, or vice versa). If ApiMark still cannot find
documentation for the referenced assembly, or the referenced assembly itself
cannot be resolved for type-hierarchy purposes (for example, because it is
missing from all configured reference paths), the affected `<inheritdoc/>`
elements are left unresolved (no inherited content), exactly as before this
capability existed. When the inherited documentation cannot be found, the member is still counted as
documented by `--enforce-docs` and its description renders as "Inherits documentation from base member.". Pass
`--reference-paths` with the base assembly (and keep its XML documentation file beside it) to render the real text.

**Known limitation**: a bare `<inheritdoc/>` that follows a *second* bare
`<inheritdoc/>` hop after resolving into an externally referenced assembly is
not currently supported. A single hop from your primary assembly into an
externally referenced assembly resolves correctly, but if that external
member's own documentation is itself a bare `<inheritdoc/>` pointing at a
further member — even one in the very same external assembly — resolution
stops at that hop and no further content is inherited. If you need
documentation to flow through more than one hop, use an explicit
`<inheritdoc cref="..."/>` at each hop instead of a bare `<inheritdoc/>` —
explicit `cref` targets resolve correctly across any number of hops,
regardless of how many assembly boundaries they cross.

**Known limitation**: a bare `<inheritdoc/>` placed directly on a *type*
declaration (a class, interface, or struct) is not currently resolved,
whether the base type is in the same assembly or an externally referenced
one. Inheritance-chain resolution only builds chain entries for members
(methods, properties, and events) declared within a type, never for the type
declaration itself, so a type documented with only a bare `<inheritdoc/>`
is left unresolved. This is a pre-existing limitation of the whole
inheritdoc chain-building mechanism, not something introduced by
cross-assembly resolution — put the documentation directly on the type, or
use an explicit `<inheritdoc cref="..."/>` targeting the base type instead.

**Note on command-line length**: each entry in `ApiMarkReferencePaths` is
logically a `--reference-paths <path>` pair passed to the `apimark` tool
process. When `ApiMarkReferencePaths` is non-empty, `ApiMarkTask`
automatically writes the harvested paths to a temporary response file (one
argument per line) and passes a single `@<file>` argument in place of the
individual `--reference-paths` pairs, avoiding the OS command-line length
risk for the common (`dotnet`) case regardless of how many reference paths
are configured; the response file is deleted once the child process
completes. `ApiMarkIncludePaths` (C++) is unaffected by this change and
retains the pre-existing, already-accepted command-line-length risk — an
extremely large number of include paths could theoretically approach OS
command-line length limits (for example, roughly 32K characters on Windows).

When ApiMark is invoked via MSBuild, `ApiMarkReferencePaths` is normally
populated automatically from the project's resolved `@(ReferencePath)` items
(see the *MSBuild Properties* section below), so most projects do not need to
set it explicitly.

### Linking to Other Assemblies' Documentation

By default, types from referenced non-System assemblies are listed in an *External Types* table.
If the dependency is also documented with ApiMark, map it with `--external-link`:

```text
apimark dotnet ... --reference-paths path/to/Other.Lib.dll --external-link Other.Lib=../Other.Lib/api
```

Types from `Other.Lib` then render as relative links into that API tree (or into an `http(s)` base
URL) and are removed from the *External Types* table. The mapped assembly's DLL must be found via
`--reference-paths` so its namespace layout is known; otherwise the mapping is ignored. Links are
not verified to exist, and single-file output never emits them. In MSBuild, use
`ApiMarkExternalLinks` with semicolon-separated `Assembly=path` pairs.

A `<see cref="..."/>` to a type (or a member of a type) in a mapped assembly also becomes a link, to that
type's page in the mapped documentation, when the cref's namespace belongs to the mapped assembly. A cref
to an unmapped assembly (including the framework) stays a plain code span. Member crefs link to the type
page, and nested types (including generic nested types) link to their nested page, such as
`Outer/Inner.md` or `Outer{1}/Inner{1}.md`.

### Cross-Reference Linking

A `<see cref="..."/>` or `<seealso cref="..."/>` reference to a type or member
declared in the assembly being documented renders as a real relative Markdown
link to that target's own page, in addition to the existing code-span
styling, whenever the target will actually be emitted under the active
visibility and obsolete-filter settings. A reference whose target is external
to the assembly, filtered out of the generated documentation, or malformed
continues to render as plain code-span text with no link, exactly as it did
before this capability existed. A reference to a constructor renders as its
declaring type's bare name with no code span and no link, also unchanged from
before this capability existed.

This capability applies only to gradual-disclosure output (`--format gradual`,
the default), where each type and member has its own page to link to. In
single-file output (`--format single-file`), every `<see cref>`/`<seealso
cref>` reference always renders as code-span-only text, since relative links
are meaningless inside a single flat document — the same reason parameter
type cells are also rendered as plain text in that mode.

## Documentation Coverage Enforcement

ApiMark can enforce that your public API surface (or a broader visibility tier)
carries an XML doc `<summary>`, in addition to generating documentation from
whatever comments exist today. This is opt-in and disabled by default.

Pass `--enforce-docs <tier>` to enable the check, where `<tier>` is one of:

- `Public` — flags public types and members missing a `<summary>`.
- `PublicAndProtected` — flags public and protected types and members.
- `All` — flags every type and member regardless of access modifier.

This enforcement tier is independent of `--visibility`, which only controls
what gets emitted into the generated Markdown; you can, for example, generate
`Public`-only documentation while enforcing coverage at the `All` tier to
catch undocumented internal helpers before they become public.

By default, undocumented items are reported to the console but do not fail
the build (`--enforce-docs-severity Warning`, the default). Pass
`--enforce-docs-severity Error` to fail the build (non-zero exit code) when
one or more undocumented items are found:

```text
apimark dotnet --assembly MyLib.dll --xml-doc MyLib.xml --output docs \
  --enforce-docs Public --enforce-docs-severity Error
```

The console report lists every undocumented type or member found, followed by
a summary count, for example:

```text
Documentation coverage check:
  [Undocumented] Type: MyLib.Widgets.Gadget
  [Undocumented] Method: MyLib.Widgets.Gadget.Reset()
Documentation coverage: 2 undocumented of 42 checked.
```

Types and members are excluded from the check exactly as they are excluded
from generated output: obsolete items are skipped unless `--include-obsolete`
is also specified, and `--exclude` patterns apply identically. A type/member
is considered "documented" when it has a non-empty `<summary>` element;
completeness of `<param>`, `<returns>`, or `<exception>` tags is not checked.

`--enforce-docs` and `--enforce-docs-severity` are also available for the
`cpp` and `vhdl` subcommands (see the C++ and VHDL user guides), each with a
scan scoped to what that language's parser understands as a documentable
declaration.

## Doc Comments

ApiMark reads standard C# XML doc comments (`///`).

| Tag | Purpose |
| --- | --- |
| `<summary>` | One-line description shown in index tables and at the top of detail pages |
| `<remarks>` | Extended description rendered below the summary |
| `<param name="...">` | Description for a method or constructor parameter |
| `<returns>` | Description for the return value |
| `<exception cref="...">` | Documents an exception the method may throw |
| `<example>` | Code example rendered in a fenced code block. A `<code lang="xml">` attribute selects the fence language; without it, or with an invalid value, the fence uses `csharp` |
| `<list>` | Bullet (`type="bullet"`), numbered (`type="number"`), and table (`type="table"`) lists rendered as their Markdown equivalents; `<item>` `<term>`/`<description>` pairs render as **term** — description |

Missing `<summary>` tags render as *No description provided.* in the output. Missing
`<param>` descriptions render as *No description provided.* in parameter tables.

## Output Structure

ApiMark supports two output formats selectable via `--format`.

### Gradual Disclosure (default: `--format gradual`)

A hierarchy of Markdown files designed for incremental context loading.

| File | Description |
| --- | --- |
| `api.md` | Root index — lists all namespaces with type counts and one-line summaries |
| `{namespace}.md` | Namespace summary — lists all types, enums, aliases, and functions with one-line summaries |
| `{namespace}/{type}.md` | Type page — members grouped by kind with signatures and doc comment details |
| `{namespace}/{alias}.md` | Type alias page — `using` declaration, underlying type, and doc comment |
| `{namespace}/{type}/{member}.md` | Member detail page — full signature, parameters, return value, remarks, example |
| `{namespace}/{type}/{nested-type}.md` | Nested type page — same structure as a top-level type page |
| `{namespace}/{type}/{alias}.md` | Class-scoped type alias page — alias declared inside a class body |

An AI agent can read the root index first, drill into the relevant namespace or type
page, and then read the member detail — consuming only as much context as the task
requires.

### Single File (`--format single-file`)

All content is written to a single `api.md` file using a flat heading hierarchy:

| Level | Content |
| --- | --- |
| H1 | Assembly name |
| H2 | Namespace |
| H3 | Type (with prototype code block and member bullet list) |
| H4 | Individual member (signature, parameters, return value, example) |

Single-file output is best suited for contexts where a complete, linear reference is
preferred over a navigable multi-file tree, such as attaching documentation to a chat
context window.

## MSBuild Integration

Add the `DemaConsulting.ApiMark.MSBuild` NuGet package to your `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="DemaConsulting.ApiMark.MSBuild" Version="x.y.z" />
</ItemGroup>
```

Enable XML documentation generation so ApiMark can read your doc comments:

```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
</PropertyGroup>
```

After the next `dotnet build`, documentation is written to `$(MSBuildProjectDirectory)\api`.

### C#-Specific MSBuild Properties

| Property | Default | Description |
| --- | --- | --- |
| `ApiMarkAssemblyPath` | `$(TargetPath)` | Path to the compiled assembly |
| `ApiMarkXmlDocPath` | `$(DocumentationFile)` | Path to the XML documentation file |
| `ApiMarkVisibility` | `PublicAndProtected` | Visibility filter: `Public`, `PublicAndProtected`, `All` |
| `ApiMarkExternalLinks` | (empty) | Semicolon-separated `Assembly=path` pairs linking referenced assemblies' types to their ApiMark documentation (relative path or `http(s)` URL); forwarded as `--external-link` |
| `ApiMarkIncludeObsolete` | `false` | Include `[Obsolete]` members in generated output |
| `ApiMarkExclude` | (empty) | Semicolon-separated wildcard patterns identifying namespaces/types to exclude, e.g. `Antlr4.*;MyNamespace.Generated.*` |
| `ApiMarkLibraryDescription` | *(unset)* | Optional description for the `api.md` introduction paragraph. When set, overrides the assembly's compiled `AssemblyDescriptionAttribute` |
| `ApiMarkReferencePaths` | Auto-populated from `@(ReferencePath)` | Semicolon-separated paths to referenced assembly DLLs, used to resolve cross-assembly `<inheritdoc/>`. Auto-populated from the project's resolved `@(ReferencePath)` items when not explicitly set; set explicitly to a non-empty list to override. When non-empty, `ApiMarkTask` automatically forwards the paths via a temporary `@<file>` response file rather than individual `--reference-paths` arguments, avoiding OS command-line length limits |
| `ApiMarkDisableReferencePathsHarvest` | `false` | Set to `true` to disable auto-population of `ApiMarkReferencePaths` from `@(ReferencePath)` without providing a replacement list. Needed because MSBuild cannot distinguish "never set" from "explicitly set to empty" for a plain property, so setting `ApiMarkReferencePaths=""` alone does not suppress auto-harvesting |
| `ApiMarkEnforceDocs` | (unset) | Enforcement visibility tier for documentation-coverage checking: `Public`, `PublicAndProtected`, `All`; omitted disables enforcement |
| `ApiMarkEnforceDocsSeverity` | `Warning` | Severity when undocumented items are found: `Warning` (report only) or `Error` (fail the build); only takes effect when `ApiMarkEnforceDocs` is also set |

See the *MSBuild Integration* section for common properties (`ApiMarkOutputDir`,
`ApiMarkFormat`, `DisableApiMark`, `ApiMarkPackDocs`) that apply to all project types.

### Configuration Example

```xml
<PropertyGroup>
  <!-- Required: enable XML documentation so ApiMark can read doc comments -->
  <GenerateDocumentationFile>true</GenerateDocumentationFile>

  <!-- Change the output directory -->
  <ApiMarkOutputDir>$(MSBuildProjectDirectory)\docs\api</ApiMarkOutputDir>

  <!-- Include protected members as well as public ones -->
  <ApiMarkVisibility>PublicAndProtected</ApiMarkVisibility>

  <!-- Add a one-line description to api.md (overrides AssemblyDescriptionAttribute) -->
  <ApiMarkLibraryDescription>A fast, reliable serialization library.</ApiMarkLibraryDescription>

  <!-- Include the generated api/ folder in the NuGet package -->
  <ApiMarkPackDocs>true</ApiMarkPackDocs>

  <!-- Disable generation entirely (e.g., for test projects) -->
  <DisableApiMark>true</DisableApiMark>
</PropertyGroup>
```
