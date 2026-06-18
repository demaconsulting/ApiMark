## CppGenerator

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

CppGenerator implements IApiGenerator for C++ libraries. It accepts a
configured set of public include roots and parse-environment options, collects
the selected header files via `GlobFileCollector`, invokes
`clang -Xclang -ast-dump=json` on a temporary combined header, and returns a
`CppEmitter` ready to write the complete Markdown output tree. The output
structure mirrors DotNetGenerator: a library-level entrypoint, per-namespace
summaries, per-type pages, and per-member detail pages for every visible member.

The implementation is split across six files in the `ApiMark.Cpp` package:

- **CppGenerator.cs** — thin `IApiGenerator` that collects headers, invokes
  clang, and returns a `CppEmitter`.
- **CppEmitter.cs** — `IApiEmitter` dispatcher, shared helper methods (visibility
  filters, comment extractors, signature builders, include-path resolution), and
  the nested `NamespaceDeclarations` data class.
- **CppEmitterGradualDisclosure.cs** — all gradual-disclosure page writers
  (API index, namespace, type, member, operator, enum, type-alias pages).
- **CppEmitterSingleFile.cs** — all single-file page writers.
- **CppTypeLinkResolver.cs** — resolves C++ type strings to Markdown link text
  for table cells and tracks non-std external type references.
- **CppExternalTypeInfo.cs** — holds external type documentation records
  (`CppExternalTypeInfo`).

### Data Model

**CppGeneratorOptions**: Configuration value object passed to the CppGenerator
constructor. All properties must be set before the constructor is called.

**CppGeneratorOptions.Description**: `string` — optional brief description of the
library, emitted as an introductory paragraph in `api.md`. Omitted when empty.

**CppGeneratorOptions.LibraryName**: `string` — name of the library, used as
the top-level heading in `api.md`.

**CppGeneratorOptions.PublicIncludeRoots**: `IReadOnlyList<string>` — one or
more directories that define the compiler include roots. This property serves
two purposes:

1. *Parse environment*: each root is passed to the Clang parser as an `-I`
   include directory so that `#include <mylib/types.h>` resolves correctly
   when parsing headers under that root.
2. *#include path derivation*: a declaration's canonical `#include` directive
   is computed by taking its source file path relative to the matching root and
   normalizing separators to forward slashes. For example, if the root is
   `C:\project\include` and the declaration source is
   `C:\project\include\mylib\renderer.h`, the canonical include path is
   `mylib/renderer.h`, yielding `#include "mylib/renderer.h"` in the type
   page output.

When multiple roots are configured and a file matches more than one, the
longest matching root path (most specific) wins.

**CppGeneratorOptions.ApiHeaderPatterns**: `IList<string>` — ordered list of
glob and exclusion pattern strings that determine which header files contribute to the
documented public API. Gitignore-style semantics apply: patterns are evaluated
in order; the last matching pattern wins. Entries without a `!` prefix are
include patterns; entries with a `!` prefix are exclusion patterns (the `!`
is stripped before glob matching). Relative patterns are resolved against the
current working directory (CWD), matching the behavior of all other CLI glob tools
and allowing users to write patterns reflecting their project layout directly
(e.g. `include/**` when invoked from the project root). When empty, all headers
with recognized C++ extensions (`.h`, `.hpp`, `.hxx`, `.h++`) under all configured
roots are documented automatically without any pattern filtering.

Example — all headers except `detail/`, with one re-included:
`["include/**", "!include/detail/**", "include/detail/public_api.h"]`

**CppGeneratorOptions.SystemIncludePaths**: `IReadOnlyList<string>` — toolchain
and SDK include directories passed to the Clang parser as system include paths
(equivalent to `-isystem` or `-I`) so that system and runtime headers
(`<vector>`, `<windows.h>`, etc.) resolve during parsing. These directories
are used for type resolution only; declarations found within them are never
documented.

**CppGeneratorOptions.Defines**: `IReadOnlyList<string>` — preprocessor symbol
definitions passed to the Clang parser as `-D` flags, in the form `"NAME"` or
`"NAME=value"`. Export macros (e.g. `MYLIB_API`, `__declspec(dllexport)`
wrappers) must be defined as empty strings (e.g. `"MYLIB_API="`) so the parser
sees them as no-ops and does not misinterpret them as type annotations.

**CppGeneratorOptions.CppStandard**: `string` — C++ language standard passed
to Clang (e.g. `"c++17"`, `"c++20"`). Defaults to `"c++17"` when not
specified.

**CppGeneratorOptions.ClangPath**: `string?` — optional explicit path to the clang executable.
When non-empty, this path is used directly (and must exist on disk); no discovery is performed.
When null or empty, the discovery order is: `APIMARK_CLANG_PATH` environment variable →
`clang` on PATH → `xcrun clang` on macOS → vswhere LLVM / `C:\Program Files\LLVM\bin\clang.exe` on Windows.

**CppGeneratorOptions.AdditionalCompilerArguments**: `IReadOnlyList<string>` —
raw Clang compiler arguments appended after all structured options. Provides an
escape-hatch for toolchain-specific flags, forced includes, platform macros, or
other Clang options not covered by the structured fields (e.g.
`"--target=x86_64-pc-windows-msvc"`, `"-fms-extensions"`). Note: this option is
currently not wired through the CLI; it is available for direct API use only
(v1 scope).

**CppGeneratorOptions.Visibility**: `ApiVisibility` — controls which class
members are included in the output. Values: `Public` (public members only),
`PublicAndProtected` (public and protected members), `All` (all members
regardless of access specifier). Applies to class and struct members; free
functions in namespaces are always included when owned.

**CppGeneratorOptions.IncludeDeprecated**: `bool` — when false, declarations
marked with `[[deprecated]]` or compiler-specific deprecated attributes are
excluded from the output.

### Key Methods

**CppGenerator constructor**: Accepts and stores a CppGeneratorOptions instance
for use during Generate.

- *Parameters*: `CppGeneratorOptions options` — fully populated options object.
- *Preconditions*: `options` must not be null; `LibraryName` must be
  non-empty; `PublicIncludeRoots` must contain at least one entry.
- *Postconditions*: The generator instance is ready to call Parse.

**CppGenerator.Parse**: Collects candidate header files, invokes clang to obtain
a fully resolved C++ AST, applies visibility and deprecation filters, and returns
a `CppEmitter` holding all parsed data.

- *Parameters*: `IContext context` — output channel for diagnostic and progress
  messages emitted during parsing. Must not be null.
- *Returns*: `IApiEmitter` — a `CppEmitter` instance holding the parsed
  namespace, type, and member data ready for emission.
- *Preconditions*: Each path in `PublicIncludeRoots` must exist on disk when
  `ApiHeaderPatterns` is empty (`DirectoryNotFoundException` is thrown if any
  path is missing); system headers must be resolvable via `SystemIncludePaths`;
  `context` must not be null. Public headers are required to be self-contained —
  each header must parse successfully on its own under the configured options.
- *Postconditions*: The returned `CppEmitter` contains all namespaces, types,
  and members parsed from the selected headers, filtered by `Visibility` and
  `IncludeDeprecated`. The caller must subsequently invoke `IApiEmitter.Emit` to
  write Markdown output.

Execution steps: call `CollectHeaderFiles()` which uses `GlobFileCollector.Collect()`
to build the selected-header set from `ApiHeaderPatterns` and `PublicIncludeRoots`;
when `ApiHeaderPatterns` is empty all headers under all roots are used directly;
when `ApiHeaderPatterns` is non-empty relative patterns are resolved against the
current working directory (CWD) via `ExpandExplicitPatterns`; build Clang options from all configured
paths, defines, standard, and additional arguments; write a temporary combined
header that `#include`s all selected headers; invoke
`clang -Xclang -ast-dump=json -fparse-all-comments -fsyntax-only` on it, parse the
resulting JSON AST; `ClangAstParser` rejects non-selected declarations during AST
walking using the pre-built selected-header set; apply `Visibility` and
`IncludeDeprecated` filters; delete the temporary combined header file; return a
`CppEmitter` holding all parsed data.

**CppGenerator.ExpandExplicitPatterns** (private): Expands relative
`ApiHeaderPatterns` entries against the current working directory (CWD).

- *Parameters*: `string cwd` — the absolute current working directory path, used as the
  base for resolving relative patterns.
- *Returns*: `List<string>` — all patterns with relative entries resolved to
  absolute paths under the CWD; absolute patterns passed through unchanged;
  exclusion prefix `!` preserved.

**CppEmitter.Emit** (implements `IApiEmitter`): Writes the full Markdown output tree using the
format specified by `config.Format`.

- *Parameters*: `IMarkdownWriterFactory factory` — factory used to create each Markdown output
  file; must not be null. `EmitConfig config` — output configuration including format and heading
  depth. `IContext context` — output channel for diagnostic messages; not used by the emitter
  itself but satisfies the interface contract.
- *Returns*: `void`
- *Preconditions*: `factory` must not be null; throws `ArgumentNullException` immediately when null
  is passed.
- *Postconditions (GradualDisclosure)*: Delegates all page writing to a new
  `CppEmitterGradualDisclosure` instance, producing one file per namespace, type, member, and
  operator group as described in `CppGenerator.Parse`.
- *Postconditions (SingleFile)*: Delegates all page writing to a new `CppEmitterSingleFile`
  instance, producing a single `api.md` file with an H{depth} library title, H{depth+1} namespace
  heading, H{depth+2} type/function/enum heading (with signature and member bullet list), and
  H{depth+3} individual member headings. Type links are omitted to prevent anchor collisions in the
  single-file layout. The convention appendix is not included in single-file output.

**CppEmitter.WriteCombinedMemberPage** (internal static): Writes a single combined
Markdown page for a group of members whose base names collide on case-insensitive
filesystems.

- *Parameters*: `IMarkdownWriterFactory factory`, `string nsKey`, `string nsDisplayName`,
  `CppClass cls`, `string lowerKey` — the shared lowercase base name used as the page
  file name and H3 heading, `IReadOnlyList<member> members` — the ordered collision
  group (at least two elements; elements are functions or fields).
- *Returns*: `void`
- *Algorithm*: Creates `{nsKey}/{cls.Name}/{lowerKey}.md` via the factory; writes an H1
  heading using `lowerKey`; for each function member writes an H2 heading of the form
  `{fn.Name} (Constructor)` or `{fn.Name} (Method)` and delegates to
  `WriteFunctionContent`; for each field member writes an H2 heading of the form
  `{field.Name} (Field)` and delegates to `WriteFieldContent`.

**CppEmitterGradualDisclosure.WriteClassOperatorsPage** (private): Writes the combined operator
overloads page for a class at `{nsKey}/{cls.Name}/operators.md`.

- *Parameters*: `IMarkdownWriterFactory factory`, `string nsKey`, `string nsDisplayName`,
  `CppClass cls`, `IReadOnlyList<CppFunction> operators` — the ordered list of operator
  methods (names starting with `"operator"`); must contain at least one element.
- *Returns*: `void`
- *Algorithm*: Creates `{nsKey}/{cls.Name}/operators.md` via the factory; writes an H1
  heading `"operators"`; emits the qualified class name comment and `#include` directive
  from the first operator with a source location; writes an introductory paragraph naming
  the class; for each operator writes an H2 heading with the operator name and parameter
  types, then delegates to `WriteFunctionContent` for signature, summary, and parameters.

**CppEmitterGradualDisclosure.WriteNamespaceOperatorsPage** (private): Writes the combined operator
overloads page for namespace-level operator free functions at `{nsKey}/operators.md`.

- *Parameters*: `IMarkdownWriterFactory factory`, `string nsKey`, `string nsDisplayName`,
  `IReadOnlyList<CppFunction> operators` — the ordered list of namespace-level operator
  free functions (names starting with `"operator"`); must contain at least one element.
- *Returns*: `void`
- *Algorithm*: Creates `{nsKey}/operators.md` via the factory; writes an H1 heading
  `"operators"`; emits the qualified name comment and `#include` directive from the first
  operator with a source location; writes an introductory paragraph naming the namespace;
  for each operator writes an H2 heading with the operator name and parameter types, then
  delegates to `WriteFreeFunctionContent` for signature, summary, and parameters.

**CppEmitter.GetMemberBaseName** (internal static): Returns the base name used to derive the
output file name for a class member, applying the convention that constructors are
identified by the declaring class name.

- *Parameters*: `member` — the member whose base name to compute;
  `string className` — the name of the declaring class, used for constructors.
- *Returns*: `string` — the class name when `member` is a constructor function;
  the member's own `Name` for non-constructor functions and fields; the class
  name as a fallback for any other member type.
- *Algorithm*: Pattern-matches on the concrete type and `IsConstructor` flag: constructor
  function → `className`; non-constructor function → `fn.Name`; field → `field.Name`;
  any other type → `className`.

**CppTypeLinkResolver** (internal): Resolves C++ type strings to Markdown link text
for use in table cells.

- *Constructor*: Accepts `IReadOnlyDictionary<string, string> knownTypes` — maps
  fully-qualified C++ type names (e.g. `"fixtures::SampleClass"`) to documentation
  page keys (e.g. `"fixtures/SampleClass"`). Built in `CppGenerator.Parse` from
  the `namespaceDecls` dictionary.
- **Linkify** method: resolves a simplified C++ type string to a Markdown link or plain text.
  - *Parameters*: `string cppTypeString`, `string currentFolder`, `ISet<CppExternalTypeInfo>
    externalTypes` accumulator.
  - *Returns*: a Markdown link `[Name](relative/path.md)` when the stripped base type
    is in `knownTypes`; the original string unchanged otherwise; non-std external types
    with a namespace are tracked in `externalTypes`.
  - *Algorithm*: strip qualifiers (`const`, `volatile`, `*`, `&`, `&&`, trailing-const,
    and template arguments) to isolate the base type name; reject primitives and `std::`
    types immediately; look up the base type in `knownTypes` by exact qualified match,
    then by short-name fallback; compute a relative path and return the link; if not
    found and the type has a non-std namespace, track as external.
- **StripQualifiers** (internal static): removes C++ cv and reference qualifiers and
  template arguments from a type string, returning the bare base type name.

**CppExternalTypeInfo** (internal record): Represents a non-standard external C++
type reference collected during table cell generation.

- *Properties*: `TypeString` (short type name without namespace), `Namespace`
  (the C++ namespace using `::` separators).
- *Ordering*: implements `IComparable<CppExternalTypeInfo>` by `TypeString` so
  `SortedSet<CppExternalTypeInfo>` produces alphabetically ordered tables.

**CppEmitter.WriteExternalTypesSection** (internal static): Emits the
`## External Types` section at the bottom of a page when at least one external
type was referenced in table cells.

- *Parameters*: `IMarkdownWriter writer`, `SortedSet<CppExternalTypeInfo>
  externalTypes`.
- *Algorithm*: Returns immediately when the set is empty; otherwise writes an
  H2 heading `"External Types"` and a two-column table (`Type`, `Namespace`).

**CppEmitterGradualDisclosure.WriteTypeAliasPage** (private): Writes a documentation page for a
`using` type alias declaration.

- *Parameters*: `IMarkdownWriterFactory factory`, `string nsKey`, `string nsDisplayName`,
  `CppTypeAlias alias`.
- *Returns*: `void`
- *Algorithm*: Creates `{nsKey}/{alias.Name}.md` via the factory; writes an H1 heading
  using `alias.Name`; emits the fully-qualified name comment
  (`// {nsDisplayName}::{alias.Name}`), the optional `#include` directive from the source
  location, and the `using {alias.Name} = {alias.UnderlyingTypeName}` declaration in a
  fenced `cpp` code block; emits the doc comment summary paragraph or the no-description
  placeholder; emits the extended details paragraph when a `@details` or `@remarks` block
  is present.

**CppFunction.IsDeleted** (data model property): `bool` — when `true`, the function
was declared with `= delete` in the source. `ClangAstParser` reads this from the
`"explicitlyDeleted"` field of `FunctionDecl` and `CXXMethodDecl` nodes in the clang JSON
AST. `BuildMethodSignature` appends the `= delete` suffix to the rendered signature when
this flag is set, making the intentional prohibition visible in the generated documentation.

### Error Handling

CppGenerator throws `DirectoryNotFoundException` when a path in
PublicIncludeRoots does not exist on disk. Parse errors returned by clang
(unresolvable includes, syntax errors in public headers) are collected and
surfaced as an exception after parsing completes, listing affected files and
Clang diagnostic messages — they are not silently ignored. Missing doc comments
produce empty documentation fields rather than an error, consistent with
DotNetGenerator behavior.

### Dependencies

- **IApiGenerator** — CppGenerator implements this interface from ApiMarkCore.
- **IApiEmitter** — CppEmitter implements this interface from ApiMarkCore.
- **IMarkdownWriterFactory** — CppEmitter receives an IMarkdownWriterFactory
  through Emit and calls CreateMarkdown to obtain each IMarkdownWriter.
- **ClangAstParser** — internal parser invoked by `CppGenerator.Parse` via
  `ClangAstParser.Parse(headers, options)` to obtain the parsed C++ AST as a
  `CppCompilationResult`.
- **clang** — the system clang executable (resolved via `CppGeneratorOptions.ClangPath`,
  `APIMARK_CLANG_PATH` environment variable, `clang` on PATH, `xcrun clang` on macOS, or
  vswhere-located clang on Windows) is invoked as a subprocess to parse C++ headers and
  produce a JSON AST — see Clang Integration Design.

### Callers

- **Program** — constructs CppGenerator from CLI options for the `cpp`
  subcommand and calls `Parse` and then `IApiEmitter.Emit`.
