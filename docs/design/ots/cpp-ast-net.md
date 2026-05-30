## CppAst.Net

CppAst.Net is a .NET library that wraps libclang (LLVM's C parsing API) to
provide a full C++ abstract syntax tree from header files without requiring a
compiler installation beyond libclang itself. In ApiMark, it is used exclusively
for reading — it enables CppGenerator to enumerate types, functions, and their
source-file locations from public C++ headers without executing a C++ build.

### Purpose

CppAst.Net was chosen because it provides full C++ AST access via libclang,
including declaration source-file provenance (`ICppDeclaration.Span.Start.File`)
which is the primary mechanism by which CppGenerator identifies declarations
belonging to the documented public API. No other .NET library provides
equivalent libclang-backed parsing with per-declaration file location data.
CppAst.Net ships with a bundled libclang binary, so no separate LLVM installation
is required on the host.

### Features Used

- **Header parsing** — `CppParser.ParseFiles(files, options)` accepts a list of
  header file paths and a `CppParserOptions` object; returns a `CppCompilation`
  containing the fully resolved AST for all parsed translation units.
- **Declaration provenance** — `ICppDeclaration.Span.Start.File` returns the
  absolute path of the source file in which each declaration is defined. This
  is the key property used by IsOwnedDeclaration to determine whether a
  declaration belongs to a public include root.
- **Namespace and type traversal** — `CppCompilation.Namespaces` and
  `CppCompilation.Classes` (and their children) provide the declaration tree
  used to enumerate namespaces, classes, structs, enums, and free functions.
- **Member enumeration** — `CppClass.Fields`, `CppClass.Functions`,
  `CppClass.Constructors`, and `CppClass.Destructors` enumerate the members of
  each class or struct.
- **Function signatures** — `CppFunction.Parameters`, `CppFunction.ReturnType`,
  and `CppFunction.IsVariadic` provide the information needed to render accurate
  C++ function signatures.
- **Access specifiers** — `ICppMember.Visibility` (`CppVisibility.Public`,
  `Protected`, `Private`) supports the Visibility filter in CppGeneratorOptions.
- **Clang options** — `CppParserOptions.IncludeFolders`, `SystemIncludeFolders`,
  `Defines`, `AdditionalArguments`, and `CppStandard` accept the parse
  environment provided by CppGeneratorOptions.

### Integration Pattern

CppAst.Net is consumed via direct API calls in CppGenerator. No wrapper class
is introduced.

1. CppGenerator builds a `CppParserOptions` instance from CppGeneratorOptions:
   - `IncludeFolders` ← PublicIncludeRoots + AdditionalIncludePaths
   - `SystemIncludeFolders` ← SystemIncludePaths
   - `Defines` ← Defines
   - `CppStandard` ← CppStandard
   - `AdditionalArguments` ← AdditionalCompilerArguments
2. CppGenerator enumerates the candidate header files (applying IncludePatterns
   and ExcludePatterns against each PublicIncludeRoot) and passes the resulting
   list to `CppParser.ParseFiles(files, options)`.
3. CppGenerator inspects `CppCompilation.Diagnostics` for errors; any Clang
   parse errors are collected and reported before output generation proceeds.
4. CppGenerator walks the `CppCompilation` AST, visiting namespaces, classes,
   enums, and free functions. For each declaration, it reads
   `ICppDeclaration.Span.Start.File` to determine ownership.
5. CppAst.Net holds native libclang resources. CppGenerator disposes the
   `CppCompilation` (if it implements `IDisposable`) or otherwise releases
   references after the AST walk completes.
