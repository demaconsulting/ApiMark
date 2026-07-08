## ClangAstParser

![ClangAstParser Structure](ApiMarkCppView.svg)

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

ClangAstParser is the internal C++ parsing unit used by `CppGenerator`. It builds
a temporary combined header, invokes `clang -ast-dump=json`, walks the returned
`TranslationUnitDecl`, and converts owned declarations into `CppAstModel`
records.

### Data Model

**CppCompilationResult**: immutable result returned by `Parse`.

- `Namespaces`: `IReadOnlyList<CppNamespaceDecl>` — parsed namespaces that contain
  owned declarations.
- `Errors`: `IReadOnlyList<string>` — stderr lines classified as `: error:` or
  `: fatal error:` by `CollectStderrErrors`.

**NamespaceBuilder** (private inner class): mutable namespace accumulator used
while walking the JSON AST.

### Key Methods

#### Process orchestration

- **Parse** (`IReadOnlyList<string> headers`, `CppGeneratorOptions options`) →
  `CppCompilationResult` — validates the header list, resolves clang, builds the
  temporary combined header, runs the process, parses the JSON document, and returns
  `CppCompilationResult`.

  - *Preconditions*: `headers` must not be null or empty; each entry must be
    non-whitespace. `options` must not be null.
  - *Postconditions*: returned `Namespaces` contains only declarations from the
    supplied header files; `Errors` contains only stderr `error` or `fatal error`
    lines.
- **FindClangExecutable** — resolves clang from explicit path, environment,
  PATH, `xcrun clang`, or Windows LLVM discovery.
- **RunProcess** — launches the clang subprocess and concurrently drains
  stdout (JSON AST) and stderr on background tasks to prevent pipe-buffer
  deadlock; throws `InvalidOperationException` when the process cannot be
  started or exits non-zero with no usable JSON present.
- **BuildArguments** — assembles the ordered clang argument list.
- **CollectStderrErrors** — filters stderr to `error` and `fatal error` lines
  only.

#### AST walking

- **WalkTranslationUnit** — starts traversal at the root `inner` array.
- **WalkNodes** — updates current-file tracking and dispatches each child node by
  `kind`.
- **WalkNamespace** — extends the namespace qualification and recurses.
- **WalkClassTemplate** — collects template parameters before delegating the
  primary `CXXRecordDecl`.

#### Declaration parsers

- **ParseClass / BuildClass** — build `CppClass` records, including methods,
  fields, nested classes, aliases, deprecation, `final`, and direct base types.
  Base-type extraction uses two paths: clang 18+ top-level `bases` array first,
  and older-clang `CXXBaseSpecifier` children through `HandleCxxBaseSpecifier`
  when the top-level array is absent.
- **HandleNestedCxxRecord** — parses public nested class definitions.
- **HandleClassTemplate** — parses public nested class templates.
- **HandleTypeAliasInClass** — parses public class-scoped aliases.
- **ParseFreeFunction** — unwraps `FunctionTemplateDecl` when needed and builds
  namespace-level `CppFunction` records.
- **ParseEnum** — builds `CppEnum` and enumerator records, including per-value
  comments.
- **ParseTypeAlias** — builds namespace-level `CppTypeAlias` records.
- **ParseMethod** — builds `CppFunction` records for constructors and methods,
  including `IsDeleted` and default parameters.
- **ParseField** — builds `CppField` records for instance and static members.
- **ParseParameter / ExtractDefaultValue** — extract parameter shapes and simple
  default-argument display strings.
- **ParseAccessSpec** — converts clang access specifiers to `CppAccessibility`.

#### Comment and text handling

- **ParseFullComment** — converts `FullComment` nodes into `CppDocComment`.
- **HandleBlockCommandComment** — processes `@brief`, `@details`, `@remarks`,
  `@return`, `@returns`, and `@note`.
- **HandleParamCommandComment** — extracts `@param` entries.
- **CollectText / CollectTextNodes** — recursively flatten text-comment content.
- **CollectVerbatimBlockText** — captures `@code` blocks as multi-line examples.
- **NormalizeSingleLine** — collapses multi-line text to a single paragraph line.

#### Type and location helpers

- **ExtractReturnType** — scans the function `qualType` string from right to left
  to find the outermost parameter-list opening parenthesis, allowing template
  return types such as `std::pair<int, int> (int)` to be reduced to
  `std::pair<int, int>`.
- **UpdateCurrentFile / GetCurrentSourceLocation** — preserve source-file and line
  context across clang nodes that omit `loc.file`.
- **IsOwned** — enforces the selected-header plus include-root ownership rule.
  Uses `FileSystemPathComparer` / `FileSystemPathComparison` (selecting
  `OrdinalIgnoreCase` on Windows/macOS and `Ordinal` on Linux) so that header
  path matching respects the native file-system case-sensitivity of the build
  host.
- **GetKind / GetName / GetQualType / GetNsBuilder / BuildNamespaces** — JSON and
  namespace-builder utilities.

### Error Handling

- `ArgumentNullException` — thrown when `headers` or `options` is null.
- `ArgumentException` — thrown when `headers` is empty or contains null/whitespace
  entries.
- `InvalidOperationException` — thrown when clang cannot be found, when clang exits
  non-zero and produces no usable JSON, or when stdout is not valid JSON.
- Temporary combined-header cleanup occurs in a `finally` block regardless of
  success or failure.

### External Interfaces

#### clang (consumed)

- *Type*: out-of-process OTS CLI tool.
- *Role*: consumer.
- *Contract*: launches clang as a subprocess via `RunProcess`, captures stdout
  (JSON AST) and stderr, and maps a non-zero exit code with no usable JSON to
  `InvalidOperationException`.
- *Constraints*: must be discoverable at runtime.

### Dependencies

- **CppGeneratorOptions** — supplies include roots, defines, standard, clang path,
  and additional compiler arguments.
- **CppAstModel** — destination record model for parsed output.
- **System.Text.Json** — used for JSON parsing and traversal.

### Callers

- **CppGenerator** — sole caller of `ClangAstParser.Parse`.
