# ApiMark Architecture

## Vision

ApiMark generates compact, AI-friendly API reference documentation in Markdown
from source code and its associated metadata (XML doc comments, header files,
docstrings, etc.). The output is designed for gradual disclosure: an AI can
read a lightweight index, drill into a namespace summary, and then read a full
type page — consuming only as much context as the task requires.

### Design Goals

- **Tight output** — no backslash escapes, no HTML anchors, no breadcrumb
  navigation, no redundant type-restatement sections, no verbose namespace
  prefixes
- **Gradual disclosure** — up to four file levels (root → namespace → type →
  member), each self-contained, each linking explicitly to the next level so
  an AI navigates by reading and following links with no directory listing
- **Simple stays simple** — members that fit in a table row stay in the parent;
  any member that needs parameters, exceptions, multi-line remarks, or examples
  gets its own file regardless of kind
- **Minimal shared core** — the core provides output format rules, file layout
  helpers, and a markdown writing interface; language modules own everything
  else
- **Two delivery mechanisms** — per-language MSBuild task packages and a
  single CLI tool (`apimark`) covering all languages

---

## Output Format

### File Layout

The same structural pattern repeats at every level: a `.md` file summarizes
a "thing" and a same-named folder contains its children. There are no
`index.md` files anywhere.

```text
api-docs/
├── api.md                                 ← fixed entrypoint (always this name)
├── DemaConsulting.TestResults.md          ← root namespace summary
└── DemaConsulting.TestResults/
    ├── TestResult.md                      ← type summary (simple: no member files)
    ├── TestOutcome.md                     ← enum (no member files)
    ├── IO.md                              ← child namespace summary
    └── IO/
        ├── JUnitSerializer.md             ← type summary (complex: links to member files)
        ├── JUnitSerializer/
        │   ├── Deserialize.md             ← method detail
        │   └── Serialize.md
        ├── Serializer.md
        └── Serializer/
            ├── Deserialize.md
            ├── Deserialize.2.md           ← second overload
            └── Identify.md
```

The assembly root namespace is treated as atomic — its full dotted name becomes
the root file and folder. The folder hierarchy applies only to sub-namespaces
beneath it.

Relative links between files work naturally in this layout and render correctly
in any Markdown viewer, documentation site, or IDE preview. Every parent file
links explicitly to its children so an AI navigates purely by reading and
following links — no directory listing required. `api.md` is always the
fixed, predictable entrypoint regardless of what the root namespace is named.

### Languages Without Namespaces

Not all languages have a namespace construct. For languages where namespaces
do not exist (C, Lua, VHDL, etc.), the generator uses the project or library
name as a single synthetic namespace. This preserves the uniform file layout
at every level — `api.md` always links to at least one grouping file, and that
grouping file lists types and members. No special-casing is needed in the core
or in tooling that consumes the output.

| Language | Grouping used as namespace |
| --- | --- |
| C# | Actual namespace hierarchy |
| C++ | Actual namespace hierarchy |
| C | Library/project name (synthetic) |
| Lua | Module name (synthetic) |
| VHDL | Library name (e.g. `work`) |
| Python | Package/module name |

The type kind column in parent tables (`struct`, `enum`, `function`, `entity`,
etc.) is always language-supplied content — it never appears in filenames.

### The Complexity Rule

A member stays as a table row in its parent if its summary fits on one line and
it has no parameters, no documented exceptions, no multi-line remarks, and no
examples. Any member that exceeds this — regardless of kind — gets its own
file, and the parent links to it.

| Always a table row | Gets a file when complex |
| --- | --- |
| Fields | Properties (setter exceptions, asymmetric access) |
| Simple events | Indexers (always: they have parameters) |
| Simple properties | Methods and constructors |
| | Operators |

### File Naming

Member files sit inside a folder named after their parent type. The first
overload uses the plain member name; subsequent overloads append `.2`, `.3`,
etc. The link text in the parent always shows the full signature so the
filename opacity does not matter. Language modules are responsible for
defining a safe filename convention for any constructs that cannot use their
natural name directly (e.g. operators).

### Level 0 — `api.md`

Fixed entrypoint — always this name regardless of library. Contains a **file naming
and path convention** section so an AI can infer file paths directly from
fully-qualified symbol names, and lists only **root namespaces** so the hierarchy
is disclosed one level at a time.

```markdown
# ApiMark API Reference

## File Naming and Path Convention

Paths are derived deterministically from fully-qualified symbol names. ...

| Symbol kind | Path pattern |
|---|---|
| Root namespace | `{Namespace}.md` |
| Child namespace | `{ParentPath}/{ChildName}.md` |
| Type | `{NamespacePath}/{TypeName}.md` |
| Member (unique name) | `{NamespacePath}/{TypeName}/{MemberName}.md` |
| Member (overloaded) | `{NamespacePath}/{TypeName}/{MemberName}-{ParamTypes}.md` |

| Namespace | Description |
|---|---|
| [DemaConsulting.TestResults](DemaConsulting.TestResults.md) | Test result model and serialization |
```

`DemaConsulting.TestResults.IO` does **not** appear in `api.md`; it is listed in
`DemaConsulting.TestResults.md` instead.

### Level 1 — `{RootNamespace}.md`

Root namespace summary. Lists immediate child namespaces and any types declared
directly in the root namespace.

```markdown
# DemaConsulting.TestResults

Test result model and serialization for .NET.

| Namespace | Description |
|---|---|
| [DemaConsulting.TestResults.IO](DemaConsulting.TestResults/IO.md) | TRX and JUnit serializers |

| Type | Description |
|---|---|
| [TestResult](DemaConsulting.TestResults/TestResult.md) | A single test case execution result |
| [TestResults](DemaConsulting.TestResults/TestResults.md) | A collection of test results |
| [TestOutcome](DemaConsulting.TestResults/TestOutcome.md) | Possible outcomes of a test execution |
```

### Level 2 — `{Namespace}/` folder, `{Parent}/{ChildName}.md`

Namespace summary. Lists immediate child namespaces and types declared in this
namespace.

```markdown
# DemaConsulting.TestResults.IO

Serializers for reading and writing TRX and JUnit XML formats.

| Type | Description |
|---|---|
| [JUnitSerializer](IO/JUnitSerializer.md) | Serializes and deserializes JUnit XML |
| [TrxSerializer](IO/TrxSerializer.md) | Serializes and deserializes TRX XML |
| [Serializer](IO/Serializer.md) | Auto-detecting deserializer |
| [TestResultFormat](IO/TestResultFormat.md) | Supported test result file formats |
```

### Level 3 — `{Namespace}/{TypeName}.md`

Type summary. Simple members appear as table rows. Complex members appear as
linked rows — the link text carries the full signature.

```markdown
# JUnitSerializer

`public static class JUnitSerializer`

Serializes and deserializes test results in the JUnit XML format.

JUnit XML is the de-facto standard accepted by Jenkins, GitHub Actions, and
GitLab CI. For auto-detecting deserialization of unknown formats, prefer
[`Serializer.Deserialize`](Serializer/Deserialize.md). Both methods are
stateless and safe for concurrent calls.

## Methods

| Method | Returns | Description |
|---|---|---|
| [`Deserialize(string)`](JUnitSerializer/Deserialize.md) | `TestResults` | Deserializes JUnit XML to test results |
| [`Serialize(TestResults)`](JUnitSerializer/Serialize.md) | `string` | Serializes test results to JUnit XML |
```

And a type where all members are simple and no sub-files are needed:

```markdown
# TestResult

`public sealed class TestResult`

Represents the result of a single test case execution. All string properties
default to `string.Empty`; `Outcome` defaults to `NotExecuted`. Not thread-safe.

## Constructors

| Constructor | Description |
|---|---|
| `TestResult()` | Initializes a new result with default values |

## Properties

| Property | Type | Description |
|---|---|---|
| `ClassName` | `string` | Class containing the test. Defaults to `Empty` |
| `Duration` | `TimeSpan` | Execution duration. Defaults to `Zero` |
| `ErrorMessage` | `string` | Failure message. Defaults to `Empty` |
| `ExecutionId` | `Guid` | Unique execution ID. Defaults to `NewGuid()` |
| `Name` | `string` | Test case name. Defaults to `Empty` |
| `Outcome` | `TestOutcome` | Test outcome. Defaults to `NotExecuted` |
| `StartTime` | `DateTime` | Start time. Defaults to `UtcNow` at construction |
```

### Level 4 — `{TypeName}/{MemberName}.md`

Full member detail. Sections appear only when present in the source
documentation — a method with no examples omits the Examples section entirely.

```markdown
# JUnitSerializer.Deserialize

`public static TestResults Deserialize(string junitContents)`

Deserializes a JUnit XML string to a `TestResults` object.

## Parameters

| Parameter | Type | Description |
|---|---|---|
| `junitContents` | `string` | JUnit XML file contents |

## Returns

The deserialized `TestResults`.

## Exceptions

| Exception | Condition |
|---|---|
| `ArgumentNullException` | `junitContents` is null |
| `ArgumentException` | `junitContents` is whitespace |
| `InvalidOperationException` | XML structure is invalid |

## Remarks

Accepts both two-level (`testsuites → testsuite → testcase`) and bare
single-level (`testsuite → testcase`) structures.

Known round-trip losses: Timeout and Aborted both deserialize as Error;
Inconclusive deserializes as Passed.

## Example

```csharp
string xml = File.ReadAllText("results.xml");
TestResults results = JUnitSerializer.Deserialize(xml);
```

```text

---

## Project Structure

```text

ApiMark/
├── src/
│   ├── ApiMark.Core/           # Output format rules, file layout helpers, IMarkdownWriter
│   ├── ApiMark.DotNet/         # C#/.NET generator: reads assembly + XML doc comments
│   ├── ApiMark.MSBuild/         # Unified MSBuild task — spawns ApiMark.Tool out-of-process
│   ├── ApiMark.Tool/           # dotnet CLI tool entry point (all languages)
│   ├── ApiMark.Cpp/             # (future) C++ generator
│   ├── ApiMark.Python/         # (future) Python generator
│   └── ApiMark.Vhdl/           # (future) VHDL generator
└── test/
    ├── ApiMark.Core.Tests/
    ├── ApiMark.DotNet.Tests/
    └── ApiMark.Tool.Tests/

```text

---

## Core (`ApiMark.Core`)

The core provides three things: a contract for generators, file layout helpers,
and a markdown writing interface. It contains no language knowledge and no
in-memory document model.

### `IApiGenerator`

```csharp
public interface IApiGenerator
{
    void Generate(string outputDirectory);
}
```

A language module implements this interface. Language-specific options (input
paths, visibility filtering, etc.) are passed at construction time. The core
never knows what language it is processing.

### `IMarkdownWriter`

Interface for writing a single output file. Passed to language module helpers
so the output format can be customized (e.g. adding frontmatter for Hugo/Jekyll,
producing MDX) without touching any language module.

```csharp
public interface IMarkdownWriter
{
    void WriteHeading(int level, string text);
    void WriteSignature(string language, string code);
    void WriteParagraph(string text);
    void WriteTable(string[] headers, IEnumerable<string[]> rows);
    void WriteCodeBlock(string language, string code);
    void WriteLink(string text, string relativePath);
}
```

### `FileLayout`

Static helpers that encode the file layout rules so every generator produces
a consistent structure without reimplementing the naming conventions.

```csharp
public static class FileLayout
{
    // Returns "api.md"
    string EntrypointFile();

    // Returns "{RootNamespace}.md" relative to output root
    string RootFile(string rootNamespace);

    // Returns "{Parent}/{Namespace}.md"
    string NamespaceFile(string namespaceName);

    // Returns "{Namespace}/{TypeName}.md"
    string TypeFile(string namespaceName, string typeName);

    // Returns "{Namespace}/{TypeName}/{MemberName}.md"
    // or "{Namespace}/{TypeName}/{MemberName}.2.md" for the second overload, etc.
    string MemberFile(string namespaceName, string typeName,
                      string memberName, int overloadIndex = 1);
}
```

---

## Pipeline

```text
new DotNetGenerator(options).Generate(outputDirectory)
```

The CLI dispatcher constructs the right generator with language-specific
options baked in, then calls `Generate`. The core provides no orchestration
beyond utilities.

```csharp
// CLI dispatch
IApiGenerator generator = language switch
{
    "dotnet" => new DotNetGenerator(new DotNetGeneratorOptions { ... }),
    "cpp"    => new CppGenerator(new CppGeneratorOptions { ... }),
    ...
};
generator.Generate(outputDirectory);
```

---

## C# Implementation (`ApiMark.DotNet`)

### Inputs

- **Assembly** (`.dll`) — reflection source for types, members, signatures,
  and accessibility. Read via `Mono.Cecil` to avoid loading the assembly into
  the current process (and its transitive dependencies).
- **XML documentation file** (`.xml`) — summaries, remarks, parameters,
  returns, exceptions, and examples produced by the C# compiler's
  `/doc` output.

### `DotNetGeneratorOptions`

```csharp
public class DotNetGeneratorOptions
{
    public string AssemblyPath { get; init; }
    public string XmlDocPath { get; init; }        // defaults to AssemblyPath with .xml
    public string RootNamespace { get; init; }     // defaults to assembly name
    public ApiVisibility Visibility { get; init; } // Public | PublicAndProtected | All
    public bool IncludeObsolete { get; init; }
}
```

### Type Name Simplification

Applied when rendering any type reference in signatures, table rows, or
member detail. Rules applied in order:

1. C# primitive aliases: `System.String` → `string`, `System.Int32` → `int`,
   `System.Boolean` → `bool`, `System.Object` → `object`, `System.Void` →
   `void`, etc.
2. Same-assembly types: use unqualified name only (`SpdxDocument` not
   `DemaConsulting.SpdxModel.SpdxDocument`).
3. Well-known collection types: drop namespace prefix
   (`List<T>`, `Dictionary<K,V>`, `IEnumerable<T>`, `IReadOnlyList<T>`, etc.)
4. All other external types: unqualified name only.
5. Generic arguments: simplified recursively using the same rules.
6. Nullable reference types: `?` suffix (`string?`, `Widget?`).
7. Nullable value types: `int?` rather than `Nullable<int>`.

### Complexity Rule for C# Members

A member is **complex** (gets its own file) if any of the following are true:

- It has one or more parameters (methods, constructors, indexers)
- It has documented `<exception>` elements
- Its remarks span more than one sentence
- It has `<example>` elements
- It has asymmetric get/set accessibility with documentation on the difference

Everything else stays as a table row in the parent type file.

### Overload File Naming

The first overload of a name uses the plain name (`Deserialize.md`). Additional
overloads use `.2.md`, `.3.md` etc., ordered by the sequence in which they
appear in the source XML — which matches declaration order in the source file
and is stable as long as declarations are not reordered.

Operator overloads use the CIL `op_` name: `op_Equality.md`,
`op_Addition.md`, etc.

### C# Output — What Each File Contains

**Root namespace file** (`DemaConsulting.SpdxModel.md`) — lists child
namespaces and any types declared directly in the root namespace.

**Namespace file** (`IO.md`) — lists types in that namespace with kind and
one-line summary.

**Type file** (`SpdxDocument.md`) — type signature, inheritance chain, type
summary and remarks, then sections for Constructors, Properties, Fields,
Methods, Events, Operators — each as a table. Simple members appear as plain
rows. Complex members appear as linked rows with the full signature as link
text.

Type signature line examples:

```text
`public sealed class SpdxDocument : SpdxElement`
Inheritance: `object` → `SpdxElement` → `SpdxDocument`

`public static class JUnitSerializer`

`public enum TestOutcome`
```

**Member file** (`Deserialize.md`) — member signature, summary, then sections
for Parameters, Returns, Exceptions, Remarks, Example — each present only if
the source documentation includes it.

Member signature line examples:

```text
`public static TestResults Deserialize(string junitContents)`

`public SpdxAnnotation[] Annotations { get; set; }`

`public string this[string key] { get; }`
```

---

## MSBuild Integration

A single `ApiMark.MSBuild` package handles both `.csproj` and `.vcxproj` builds.
The task targets `netstandard2.0` for compatibility with both the .NET SDK MSBuild
(`dotnet build`) and .NET Framework MSBuild inside Visual Studio. Generation is
always performed out-of-process: the task spawns `dotnet ApiMark.Tool.dll` with the
appropriate language subcommand and arguments.

The language is inferred from the project extension (`.vcxproj` → `cpp`, otherwise
→ `dotnet`) unless `ApiMarkLanguage` is set explicitly.

### `ApiMark.MSBuild` — for `.csproj` and `.vcxproj`

```xml
<ItemGroup>
  <PackageReference Include="ApiMark.MSBuild" Version="x.y.z" PrivateAssets="All" />
</ItemGroup>

<PropertyGroup>
  <!-- Output directory -->
  <ApiMarkOutputDir>$(MSBuildProjectDirectory)/bin/$(Configuration)/api-docs</ApiMarkOutputDir>

  <!-- Public | PublicAndProtected | All -->
  <ApiMarkVisibility>Public</ApiMarkVisibility>

  <!-- Set to true to suppress generation in specific configurations -->
  <!-- <DisableApiMark>true</DisableApiMark> -->
</PropertyGroup>
```

The task fires `AfterTargets="Build"` unless `DisableApiMark` is true. It resolves
the `dotnet` executable from `DOTNET_HOST_PATH` (set by the SDK) or `PATH`, then
spawns `dotnet <ToolDllPath> <language> [options]`. For `dotnet`, `ApiMarkAssemblyPath`
defaults to `$(TargetPath)` and `ApiMarkXmlDocPath` defaults to `$(DocumentationFile)`.
If `ApiMarkXmlDocPath` is not set, the task skips generation silently.

For `cpp` builds, `ApiMarkIncludePaths` provides a semicolon-separated list of
include directories passed to the C++ parser.

---

## CLI Integration (`ApiMark.Tool`)

Installed as a .NET global or local tool. Required for non-.NET languages and
useful in any CI pipeline.

```text
Usage: apimark <language> [options]

Languages:
  dotnet    .NET assembly + XML doc comments
  cpp       C++ header files + CppAst.Net parser    (planned)
  python    Python package AST + docstrings          (planned)
  vhdl      VHDL source + GHDL XML                   (planned)

Options (dotnet):
  --assembly <path>     Path to the compiled assembly (.dll)
  --xml-doc <path>      Path to the XML documentation file
  --output <dir>        Output directory for generated markdown
  --visibility <level>  Public | PublicAndProtected | All  (default: Public)
  --include-obsolete    Include members marked [Obsolete]

Options (cpp):
  --includes <paths>    Semicolon-separated list of include paths
  --output <dir>        Output directory for generated markdown
  --visibility <level>  Public | PublicAndProtected | All  (default: Public)
  --include-obsolete    Include deprecated members

Global options:
  --log-level <level>   Verbose | Info | Warning | Error  (default: Warning)
  --version             Print tool version
```

---

## Extensibility

Adding a new language requires only:

1. A new project `ApiMark.{Language}` containing:
   - An `IApiGenerator` implementation constructed with language-specific options
   - A plain options class carrying whatever that language needs
2. Registration of the language name in the CLI dispatcher in `ApiMark.Tool`.
3. Update `ApiMark.MSBuild` to forward language-specific MSBuild properties as
   CLI arguments to the `ApiMark.{Language}` subcommand.

The core, file layout rules, and `IMarkdownWriter` require no changes. Each
language module is fully self-contained and makes its own decisions about
internal structure, complexity, type name display, and section layout.
