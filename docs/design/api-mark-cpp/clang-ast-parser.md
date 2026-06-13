## ClangAstParser

<!-- All sections below are MANDATORY. If a section do not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

ClangAstParser is an internal utility class used exclusively by CppGenerator.
It provides a single static entry point, `Parse`, that:

1. Writes a temporary combined header file that `#include`s every supplied
   header, so that all headers are processed as a single translation unit.
2. Invokes `clang -Xclang -ast-dump=json -fparse-all-comments -fsyntax-only`
   on the combined header, capturing JSON on stdout and diagnostics on stderr
   concurrently to avoid deadlock.
3. Deserializes the `TranslationUnitDecl` JSON tree, walking its inner nodes
   and accumulating only declarations whose source file is one of the selected
   (explicitly passed) header files.
4. Returns a `CppCompilationResult` containing the grouped namespaces and any
   stderr error lines for the caller to log.

ClangAstParser has a private constructor; callers must use the static `Parse`
method. The class holds no mutable state between parse calls.

### Data Model

**CppCompilationResult**: Immutable record returned by `Parse`.

- `Namespaces`: `IReadOnlyList<CppNamespaceDecl>` — parsed namespaces, each
  containing the owned declarations grouped by namespace.
- `Errors`: `IReadOnlyList<string>` — error and warning lines emitted by clang
  to stderr. A non-empty list does not necessarily mean the output is unusable;
  clang may emit warnings for system or third-party headers while successfully
  parsing owned headers.

**NamespaceBuilder** (private inner class): Mutable accumulator used during AST
walking. One builder per unique fully-qualified namespace name. Converted to an
immutable `CppNamespaceDecl` via `Build()` once the full AST walk is complete.

### Key Methods

**ClangAstParser.Parse** (static): Invokes clang, parses the JSON AST, and
returns structured results.

- *Parameters*: `IReadOnlyList<string> headers` — absolute paths of the header
  files to parse and use as the owned-symbol filter. Must not be null or empty.
  `CppGeneratorOptions options` — generator options controlling include paths,
  defines, C++ standard, and clang discovery. Must not be null.
- *Returns*: `CppCompilationResult` containing parsed namespaces and any stderr
  error lines.
- *Preconditions*: `headers` must not be null or empty; `options` must not be
  null; the clang executable must be locatable (see clang discovery order in
  CppGeneratorOptions.ClangPath).
- *Postconditions*: The temporary combined header file is deleted from the
  system temp directory. The returned `CppCompilationResult.Namespaces` contains
  only declarations whose source file is both listed in the selected headers set
  and under one of the `PublicIncludeRoots` entries.
- *Exceptions*: `InvalidOperationException` — thrown when the clang executable
  cannot be located, when clang exits non-zero and produces no usable JSON, or
  when the JSON output cannot be parsed.

**FindClangExecutable** (private static): Resolves the clang executable path
using the following discovery order:

1. `CppGeneratorOptions.ClangPath` — used directly when non-empty; must exist.
2. `APIMARK_CLANG_PATH` environment variable.
3. `clang` on the system PATH.
4. `xcrun clang` (macOS only).
5. vswhere LLVM discovery → `C:\Program Files\LLVM\bin\clang.exe` (Windows).

Returns `(fileName, prefix)` where `prefix` is an empty list for direct clang
invocations and `["clang"]` for `xcrun`-wrapped invocations.

**BuildArguments** (private static): Assembles the full argument list passed to
the clang process from structured options: AST-dump/syntax flags
(`-Xclang -ast-dump=json -fparse-all-comments -fsyntax-only -x c++`) are emitted
first, then `-std`, `-I` include roots, `-isystem` system paths, `-D` defines,
extra args, then header file paths.

**CollectStderrErrors** (private static): filters stderr lines containing
`": error:"` or `": fatal error:"` and returns them as an immutable
list for the caller to evaluate.

### External Interfaces

**clang (consumed)**: ClangAstParser spawns clang as a child process and
communicates with it via its standard streams.

- *Type*: Out-of-process OTS executable (LLVM/Clang).
- *Role*: Consumer — ClangAstParser launches clang, captures stdout (JSON AST)
  and stderr (diagnostics) concurrently, and waits for exit.
- *Contract*: `clang -Xclang -ast-dump=json -fparse-all-comments -fsyntax-only
  [options] <combined-header>` produces a single `TranslationUnitDecl` JSON
  object on stdout and diagnostic messages on stderr. Exit code 0 indicates
  success; non-zero may still yield usable JSON if warnings were present.
- *Constraints*: A clang executable must be locatable at call time via the
  configured discovery order. The minimum supported clang version is whatever
  produces `TranslationUnitDecl` JSON via `-ast-dump=json`; in practice clang
  7+ is required.

### Dependencies

N/A - ClangAstParser has no injected dependencies. All configuration is
supplied via the `options` parameter to `Parse`. The only external runtime
dependency is the clang executable resolved at call time.

### Error Handling

- `InvalidOperationException` — thrown by `FindClangExecutable` when no clang
  executable can be located via any discovery step.
- `InvalidOperationException` — thrown when clang exits non-zero and stdout is
  empty or whitespace (clang is unavailable or catastrophically broken), using
  the stderr output as the diagnostic message.
- `InvalidOperationException` — thrown when the stdout cannot be parsed as a
  valid JSON `TranslationUnitDecl` object (e.g. clang version is too old or
  output is corrupt).
- The temporary combined header file is always deleted in a `finally` block
  regardless of whether an exception is thrown.

### Callers

- **CppGenerator** — the sole caller of `ClangAstParser.Parse`; invokes it
  during `CppGenerator.Parse(context)` with the collected candidate header list
  and the configured `CppGeneratorOptions`.
