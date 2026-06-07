# ApiMarkCpp

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

## Architecture

ApiMarkCpp provides C++ language support. It reads a set of public C++ header
files by invoking clang with `clang -ast-dump=json`, parsing the resulting JSON
AST, applying a file-provenance filter to identify declarations that belong to the
documented public API, and producing the Markdown output defined by the Core
interfaces. The system contains one unit:

- **CppGenerator** — accepts `CppGeneratorOptions` specifying public include
  roots and parse environment, invokes `ClangAstParser` to obtain a fully resolved
  C++ AST as structured records, filters declarations to those physically defined in
  the public header files, and writes the complete gradual-disclosure Markdown tree
  through IMarkdownWriterFactory.

```mermaid
flowchart TD
    CppGenerator --> ClangAstParser["ClangAstParser (internal)"]
    ClangAstParser --> clang["clang -ast-dump=json (OTS)"]
    CppGenerator --> IMarkdownWriterFactory
```

CppGenerator depends on ClangAstParser and the ApiMarkCore interfaces.
ClangAstParser invokes clang as an external process and parses its JSON output.

## External Interfaces

**IApiGenerator (provided)**: CppGenerator implements IApiGenerator from
ApiMarkCore.

- *Type*: In-process .NET public API.
- *Role*: Provider — ApiMarkTool constructs CppGenerator and calls Generate
  through the IApiGenerator interface.
- *Contract*: `CppGenerator(CppGeneratorOptions options)` constructs a
  configured generator; `Generate(IMarkdownWriterFactory factory)` writes the
  full Markdown tree using the supplied factory.
- *Constraints*: CppGeneratorOptions must be fully populated before calling
  Generate; all paths in PublicIncludeRoots must exist on disk.

**clang (consumed)**: CppGenerator uses clang via `ClangAstParser` to parse C++ headers.

- *Type*: External process (system-installed or PATH-located executable).
- *Role*: Consumer — `ClangAstParser` invokes `clang -ast-dump=json` with the
  configured include paths, system include paths, preprocessor defines, and
  compiler flags, then parses the JSON output into structured C++ AST records.
- *Contract*: `clang -Xclang -ast-dump=json -fparse-all-comments -fsyntax-only
  -x c++ -std={standard} -I {roots} -isystem {sysroots} -D {defines} {headers}`.
  The clang executable is located automatically (PATH, xcrun on macOS, vswhere on
  Windows) or from `CppGeneratorOptions.ClangPath`.
- *Constraints*: clang must be installed and accessible; a clear
  `InvalidOperationException` is thrown when clang cannot be found.

**MSBuild (consumed via ApiMarkTask)**: The `.targets` file sets the following
MSBuild properties used to configure generation for C++ projects:

- `$(ApiMarkLibraryName)` — library name used as the top-level heading in
  `api.md`; defaults to `$(MSBuildProjectName)` via the `.targets` file.
- `$(ApiMarkLibraryDescription)` — optional description emitted as an
  introductory paragraph in `api.md`; omitted when empty or not set.
- `$(ApiMarkDefines)` — semicolon-separated list of preprocessor symbol
  definitions passed to the Clang parser; semicolons are converted to commas
  when forwarding to the `--defines` argument.
- `$(ApiMarkCppStandard)` — C++ language standard passed to Clang (e.g.
  `c++17`, `c++20`); defaults to `c++17` via the `.targets` file.

## Dependencies

- **clang**: used to parse C++ header files via `clang -ast-dump=json` — requires
  clang to be installed on the host machine. No NuGet package dependency.

## Risk Control Measures

N/A — not a safety-classified software item.

## Data Flow

1. The caller (ApiMarkTool) constructs `CppGeneratorOptions` with
   PublicIncludeRoots, IncludePatterns, ExcludePatterns, SystemIncludePaths,
   AdditionalIncludePaths, Defines, CppStandard, AdditionalCompilerArguments,
   Visibility, IncludeDeprecated, and LibraryName, then passes an
   IMarkdownWriterFactory to Generate.
2. CppGenerator enumerates all header files under each PublicIncludeRoot,
   applying IncludePatterns and ExcludePatterns, to produce the candidate file
   set. Each matched header is parsed as an independent translation unit so that
   headers are self-contained.
3. CppGenerator calls `ClangAstParser.Parse` with all candidate headers and the
   configured options. `ClangAstParser` invokes `clang -ast-dump=json` and parses
   the resulting JSON into `CppCompilationResult` containing `CppNamespaceDecl`
   records. The parser skips AST nodes whose source file is not under a public
   include root, avoiding processing of system and third-party headers.
4. `ClangAstParser` returns a `CppCompilationResult` containing all declarations
   physically located in the public headers, already filtered by ownership.
5. CppGenerator applies the IsOwnedDeclaration filter to each declaration:
   only declarations whose source file normalizes to a path under a
   PublicIncludeRoot, matches IncludePatterns, and does not match
   ExcludePatterns are documented. System and third-party declarations are
   used for type resolution only.
6. For each owned declaration, CppGenerator derives the canonical #include path
   as the source file path relative to its matching PublicIncludeRoot, expressed
   with forward slashes.
7. CppGenerator calls `factory.CreateMarkdown("", "api")` and writes the
   library-level entrypoint listing all namespaces.
8. For each namespace containing owned declarations, CppGenerator calls
   `factory.CreateMarkdown(qualifiedNamespace, qualifiedNamespace)` and writes
   a namespace summary listing types and free functions grouped by header.
9. For each owned type, CppGenerator writes the type page with the #include
   path, then emits a dedicated detail page for every visible member. All
   members always receive their own page, making navigation fully deterministic.

## Design Constraints

- Platform: targets net8.0, net9.0, net10.0 as a class library. No NuGet runtime
  packages are required; clang is a system dependency that must be installed
  separately on the host machine.
- Parse environment: the host machine must have clang installed (LLVM, VS-bundled,
  or Xcode on macOS). System include paths can be passed via SystemIncludePaths so
  that system headers resolve without requiring a full toolchain on PATH.
- No compilation required: ApiMarkCpp reads source headers without building the
  C++ project; no object files, link steps, or CMake configuration step is needed.
- Strict ownership model: only declarations whose source file is physically
  located under a configured PublicIncludeRoot are documented. Declarations
  re-exported from system or third-party headers are intentionally excluded
  from the documented surface; the clang JSON walker skips non-owned subtrees.
- v1 scope: primary class and function templates are documented; partial
  specializations, explicit instantiations, and C++ Concepts are out of scope.
  Preprocessor macros are excluded from the documented surface. Doc comments
  are parsed from the clang JSON AST via `-fparse-all-comments`.
