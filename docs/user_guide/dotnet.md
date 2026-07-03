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
| `--visibility <value>` | Visibility filter: `Public`, `PublicAndProtected`, `All` (default: `Public`) |
| `--include-obsolete` | Include obsolete members in generated output |
| `--exclude <pattern>` | Exclude namespaces/types matching a wildcard pattern (repeatable) |

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

### Types

Public classes, interfaces, structs, enums, and delegates are documented. The set of
visible types is controlled by `--visibility`:

- `Public` — public types only (default)
- `PublicAndProtected` — public and protected types
- `All` — all types including internal

### Members

Methods, properties, fields, events, and constructors are documented on the type page.
Operator overloads are grouped together on a dedicated `operators.md` page within the
type folder.

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

## Doc Comments

ApiMark reads standard C# XML doc comments (`///`).

| Tag | Purpose |
| --- | --- |
| `<summary>` | One-line description shown in index tables and at the top of detail pages |
| `<remarks>` | Extended description rendered below the summary |
| `<param name="...">` | Description for a method or constructor parameter |
| `<returns>` | Description for the return value |
| `<exception cref="...">` | Documents an exception the method may throw |
| `<example>` | Code example rendered in a fenced code block |
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
| `ApiMarkVisibility` | `Public` | Visibility filter: `Public`, `PublicAndProtected`, `All` |
| `ApiMarkIncludeObsolete` | `false` | Include `[Obsolete]` members in generated output |
| `ApiMarkExclude` | (empty) | Semicolon-separated wildcard patterns identifying namespaces/types to exclude, e.g. `Antlr4.*;MyNamespace.Generated.*` |

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

  <!-- Include the generated api/ folder in the NuGet package -->
  <ApiMarkPackDocs>true</ApiMarkPackDocs>

  <!-- Disable generation entirely (e.g., for test projects) -->
  <DisableApiMark>true</DisableApiMark>
</PropertyGroup>
```
