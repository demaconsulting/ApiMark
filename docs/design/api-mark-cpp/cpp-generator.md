## CppGenerator

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

CppGenerator implements `IApiGenerator` for C++ libraries. It collects the public
headers selected by `CppGeneratorOptions.ApiHeaderPatterns`, invokes
`ClangAstParser` on a temporary combined header, groups the resulting owned
namespace declarations, and returns a `CppEmitter` that can write either the
single-file or gradual-disclosure Markdown output.

### Data Model

**CppGeneratorOptions**: Configuration value object passed to the constructor.

- `Description`: `string` — optional library description emitted on `api.md`.
- `LibraryName`: `string` — library name used in the top-level heading.
- `PublicIncludeRoots`: `IReadOnlyList<string>` — include roots used both for clang
  `-I` arguments and canonical `#include` path derivation. The longest matching
  root wins when roots overlap.
- `ApiHeaderPatterns`: `IList<string>` — ordered gitignore-style include/exclude
  patterns used to select the documented public headers. Relative patterns are
  resolved from `WorkingDirectory` or the process CWD when `WorkingDirectory` is
  null.
- `WorkingDirectory`: `string?` — optional working-directory anchor for resolving
  relative `ApiHeaderPatterns`; default `null`, which means the current process
  working directory is used.
- `SystemIncludePaths`: `IReadOnlyList<string>` — system include directories passed
  to clang for type resolution only.
- `Defines`: `IReadOnlyList<string>` — preprocessor definitions forwarded as `-D`
  arguments.
- `CppStandard`: `string` — language standard passed to clang; defaults to
  `c++17`.
- `ClangPath`: `string?` — optional explicit clang path. When null or empty,
  discovery falls back to `APIMARK_CLANG_PATH`, PATH, `xcrun clang`, and Windows
  LLVM discovery.
- `AdditionalCompilerArguments`: `IReadOnlyList<string>` — raw trailing clang
  arguments used as an escape hatch for toolchain-specific switches.
- `Visibility`: `ApiVisibility` — class-member visibility filter consumed later by
  `CppEmitter` during emission.
- `IncludeDeprecated`: `bool` — parse-time declaration filter controlling whether
  deprecated classes, free functions, enums, and type aliases are retained.

### Key Methods

**CppGenerator constructor**: validates and stores a `CppGeneratorOptions`
instance for later parsing.

- *Parameters*: `CppGeneratorOptions options`.
- *Preconditions*: `options` must not be null; `LibraryName` must be non-empty;
  `PublicIncludeRoots` must contain at least one entry.
- *Postconditions*: the generator is ready to call `Parse`.

**CppGenerator.Parse**: collects the selected headers, invokes clang, applies the
parse-time deprecated filter, builds the known-type map, and returns a
`CppEmitter`.

- *Parameters*: `IContext context` — output channel for parse diagnostics.
- *Returns*: `IApiEmitter` implemented by `CppEmitter`.
- *Preconditions*: `context` must not be null; when `ApiHeaderPatterns` is empty,
  each configured public include root must exist on disk.
- *Postconditions*: the returned emitter contains all owned namespaces, classes,
  free functions, enums, and type aliases from the selected headers; deprecated
  declarations are excluded during `CollectResultNamespace` when
  `IncludeDeprecated` is false; class-member visibility filtering is deferred to
  `CppEmitter` helper methods at emit time.
- *Algorithm*: `CollectHeaderFiles()` builds the selected header set; a temporary
  combined header includes every selected file; `ClangAstParser.Parse` returns
  `CppCompilationResult`; `CheckForErrors` separates public-header failures from
  system-header diagnostics; `CollectResultNamespace` groups declarations by
  namespace key; the known-type map is flattened from namespaces, nested classes,
  and type aliases; a `CppTypeLinkResolver` and `CppEmitter` are returned.
  The constructed `CppTypeLinkResolver` accumulates references to external
  (non-library, non-`std`) types encountered during `CppEmitter` execution; the
  emitter renders them in an `External Types` section on each affected page.

### External Interfaces

#### IApiGenerator (provided)

- *Type*: in-process .NET interface.
- *Role*: provider.
- *Contract*: `Parse(IContext)` returns an `IApiEmitter` (implemented by `CppEmitter`).
- *Constraints*: `options` must not be null; `LibraryName` must be non-empty;
  `PublicIncludeRoots` must contain at least one entry.

#### IContext (consumed)

- *Type*: in-process .NET interface.
- *Role*: consumer.
- *Contract*: output channel for parse diagnostics and progress messages.
- *Constraints*: must not be null.

### Error Handling

- `ArgumentNullException` — thrown by the constructor when `options` is null; thrown
  by `Parse` when `context` is null.
- `ArgumentException` — thrown by the constructor when `LibraryName` is empty or
  `PublicIncludeRoots` is empty.
- `DirectoryNotFoundException` — thrown when a configured include root does not
  exist and default header enumeration is used.
- `InvalidOperationException` — propagated when clang cannot be located, emits no
  usable JSON, or public headers produce hard parse failures.

### Dependencies

- **IApiGenerator** — implemented from ApiMarkCore.
- **CppAstModel** — immutable record types received from `ClangAstParser` and
  stored while building the known-type map and grouping namespaces.
- **GlobFileCollector** — performs gitignore-style header selection.
- **ClangAstParser** — parses the selected headers into `CppCompilationResult`.
- **CppEmitter** — returned by `Parse` to emit Markdown output.
- **CppTypeLinkResolver** — constructed from the flattened known-type map and
  passed into `CppEmitter`.
- **clang** — consumed indirectly through `ClangAstParser`.

### Callers

- **Program** — constructs `CppGenerator` from CLI or MSBuild options and calls
  `Parse` before invoking `IApiEmitter.Emit`.
