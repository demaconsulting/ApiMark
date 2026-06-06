## CppGenerator

<!-- All sections below are MANDATORY. If a section do not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

CppGenerator implements IApiGenerator for C++ libraries. It accepts a
configured set of public include roots and parse-environment options, invokes
CppAst.Net to obtain a fully resolved C++ AST, applies a file-provenance filter
to identify declarations that belong to the documented public API, derives the
canonical `#include` path for each owned type from its source file relative to
its matching include root, and writes the complete gradual-disclosure Markdown
tree through IMarkdownWriterFactory. The output structure mirrors
DotNetGenerator: a library-level entrypoint, per-namespace summaries, per-type
pages, and per-member detail pages for every visible member.

### Data Model

**CppGeneratorOptions**: Configuration value object passed to the CppGenerator
constructor. All properties must be set before the constructor is called.

**CppGeneratorOptions.Description**: `string` — optional brief description of the
library, emitted as an introductory paragraph in `api.md`. Omitted when empty.

**CppGeneratorOptions.LibraryName**: `string` — name of the library, used as
the top-level heading in `api.md`.

**CppGeneratorOptions.PublicIncludeRoots**: `IReadOnlyList<string>` — one or
more directories that define the public include root(s) of the library. This
property serves a dual purpose:

1. *Parse environment*: each root is passed to the Clang parser as an `-I`
   include directory so that `#include <mylib/types.h>` resolves correctly
   when parsing headers under that root.
2. *#include path derivation*: a declaration's canonical `#include` directive
   is computed by taking its source file path relative to the matching root and
   normalizing separators to forward slashes. For example, if the root is
   `C:\project\include` and the declaration source is
   `C:\project\include\mylib\renderer.h`, the canonical include path is
   `mylib/renderer.h`, yielding `#include <mylib/renderer.h>` in the type
   page output.

When multiple roots are configured and a file matches more than one, the
longest matching root path (most specific) wins.

**CppGeneratorOptions.IncludePatterns**: `IReadOnlyList<string>` — glob
patterns relative to each PublicIncludeRoot selecting which header files
contribute to the documented public API. Defaults to `["**/*"]` when empty,
which selects all files under all roots.

**CppGeneratorOptions.ExcludePatterns**: `IReadOnlyList<string>` — glob
patterns relative to each PublicIncludeRoot for files to exclude from the
documented API. Common values: `"detail/**"`, `"*_impl.h"`, `"internal/**"`.
ExcludePatterns are evaluated after IncludePatterns; a file matching both is
excluded.

**CppGeneratorOptions.SystemIncludePaths**: `IReadOnlyList<string>` — toolchain
and SDK include directories passed to the Clang parser as system include paths
(equivalent to `-isystem` or `-I`) so that system and runtime headers
(`<vector>`, `<windows.h>`, etc.) resolve during parsing. These directories
are used for type resolution only; declarations found within them are never
documented.

**CppGeneratorOptions.AdditionalIncludePaths**: `IReadOnlyList<string>` —
additional include directories for third-party headers included by public
headers but not part of the documented API (e.g. a vendored library's include
directory). Passed to the Clang parser as `-I` flags. Declarations from these
paths are never documented. Note: this option is currently not wired through the
CLI; it is available for direct API use only (v1 scope).

**CppGeneratorOptions.Defines**: `IReadOnlyList<string>` — preprocessor symbol
definitions passed to the Clang parser as `-D` flags, in the form `"NAME"` or
`"NAME=value"`. Export macros (e.g. `MYLIB_API`, `__declspec(dllexport)`
wrappers) must be defined as empty strings (e.g. `"MYLIB_API="`) so the parser
sees them as no-ops and does not misinterpret them as type annotations.

**CppGeneratorOptions.CppStandard**: `string` — C++ language standard passed
to Clang (e.g. `"c++17"`, `"c++20"`). Defaults to `"c++17"` when not
specified.

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
  non-empty; `PublicIncludeRoots` must contain at least one entry and each
  entry must be an existing directory.
- *Postconditions*: The generator instance is ready to call Generate.

**CppGenerator.Generate**: Parses the public headers, applies the ownership
filter, and writes the full Markdown output tree.

- *Parameters*: `IMarkdownWriterFactory factory` — factory used to create each
  Markdown output file.
- *Returns*: `void`
- *Preconditions*: All paths in PublicIncludeRoots must exist on disk; system
  headers must be resolvable via SystemIncludePaths; `factory` must not be null.
  Public headers are required to be self-contained — each header must parse
  successfully on its own under the configured options.
- *Postconditions*: The factory has produced a complete Markdown tree. Output
  file naming follows these conventions:
  - `factory.CreateMarkdown("", "api")` — library entrypoint listing all
    namespaces with a declaration count column (classes + enums + free
    functions per namespace) and one-line descriptions, giving AI agents a
    complete navigation map and scope signal in one read.
  - `factory.CreateMarkdown(qualifiedNamespace, qualifiedNamespace)` —
    namespace summary listing owned types and free functions, grouped by source
    header. `qualifiedNamespace` is the C++ qualified name with `::` replaced
    by `.` for file-path compatibility (e.g. `mylib.rendering`).
  - `factory.CreateMarkdown(qualifiedNamespace, typeName)` — type page with
    the canonical `#include <path>` at the top, followed by the class
    declaration, inheritance information, template parameters (for primary
    templates), and grouped sub-tables with links to all member detail pages.
  - `factory.CreateMarkdown($"{qualifiedNamespace}/{typeName}", memberName)` —
    dedicated page for every visible member. All members always receive their
    own page, making navigation fully deterministic.
  - Global-namespace declarations are collected under the reserved namespace
    name `"global"`.

Execution steps: enumerate candidate header files under each PublicIncludeRoot
applying IncludePatterns and ExcludePatterns; build Clang options from all
configured paths, defines, standard, and additional arguments; call CppAst.Net
to parse each candidate header as an independent translation unit; walk the
resulting AST and apply IsOwnedDeclaration to each declaration; apply
Visibility and IncludeDeprecated filters; write the library entrypoint; for
each namespace write the namespace summary; for each owned type: collect all
visible constructors, methods, and fields; build a case-insensitive map keyed
by the lowercase of each member's base name (see `GetMemberBaseName`); for
each key with a single member emit an individual detail page via
`WriteMemberPage`; for each key with multiple members (case-insensitive
collision) emit a single combined page via `WriteCombinedMemberPage`; emit
grouped sub-tables with links; dispose or release CppAst.Net resources.

**IsOwnedDeclaration** (internal): Determines whether a declaration belongs to
the documented public API.

- *Parameters*: declaration source file (absolute, normalized path).
- *Returns*: `(bool owned, string includeRoot, string relativePath)` — owned
  is true when the file falls under a PublicIncludeRoot and matches
  IncludePatterns without matching ExcludePatterns; includeRoot and
  relativePath are set when owned is true.
- *Algorithm*:
  1. Normalize the declaration source file path: resolve to absolute path,
     normalize directory separators to the OS separator, resolve `..` and
     symbolic links.
  2. For each PublicIncludeRoot (normalized to absolute path), test whether the
     declaration file starts with the root path. Collect all matching roots.
  3. Select the longest matching root (most specific path wins when roots
     overlap).
  4. Compute relative path: strip the root prefix and normalize separators to
     forward slashes.
  5. Test the relative path against IncludePatterns (must match at least one)
     and ExcludePatterns (must not match any). Return owned=true only when both
     conditions hold.

**CppGenerator.WriteCombinedMemberPage** (private): Writes a single combined
Markdown page for a group of members whose base names collide on case-insensitive
filesystems.

- *Parameters*: `IMarkdownWriterFactory factory`, `string nsKey`, `string nsDisplayName`,
  `CppClass cls`, `string lowerKey` — the shared lowercase base name used as the page
  file name and H3 heading, `IReadOnlyList<CppElement> members` — the ordered collision
  group (at least two elements; elements are `CppFunction` or `CppField`).
- *Returns*: `void`
- *Algorithm*: Creates `{nsKey}/{cls.Name}/{lowerKey}.md` via the factory; writes an H3
  heading using `lowerKey`; for each `CppFunction` writes an H4 heading of the form
  `{fn.Name} (Constructor)` or `{fn.Name} (Method)` and delegates to
  `WriteFunctionContent`; for each `CppField` writes an H4 heading of the form
  `{field.Name} (Field)` and delegates to `WriteFieldContent`.

**CppGenerator.GetMemberBaseName** (private): Returns the base name used to derive the
output file name for a class member, applying the convention that constructors are
identified by the declaring class name.

- *Parameters*: `CppElement member` — the member whose base name to compute;
  `string className` — the name of the declaring class, used for constructors.
- *Returns*: `string` — the class name when `member` is a constructor `CppFunction`;
  the member's own `Name` for non-constructor `CppFunction` and `CppField`; the class
  name as a fallback for any other element type.
- *Algorithm*: Pattern-matches on the concrete type and `IsConstructor` flag: constructor
  `CppFunction` → `className`; non-constructor `CppFunction` → `fn.Name`; `CppField` →
  `field.Name`; any other type → `className`.

### Error Handling

CppGenerator throws `DirectoryNotFoundException` when a path in
PublicIncludeRoots does not exist on disk. Parse errors returned by CppAst.Net
(unresolvable includes, syntax errors in public headers) are collected and
surfaced as an exception after parsing completes, listing affected files and
Clang diagnostic messages — they are not silently ignored. Missing doc comments
produce empty documentation fields rather than an error, consistent with
DotNetGenerator behavior.

### Dependencies

- **IApiGenerator** — CppGenerator implements this interface from ApiMarkCore.
- **IMarkdownWriterFactory** — CppGenerator receives an IMarkdownWriterFactory
  through Generate and calls CreateMarkdown to obtain each IMarkdownWriter.
- **CppAst.Net** — used to parse C++ headers via libclang without requiring
  a full C++ build — see CppAst.Net Integration Design.

### Callers

- **Program** — constructs CppGenerator from CLI options for the `cpp`
  subcommand and calls Generate.
