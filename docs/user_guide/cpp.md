# C++ Documentation

## Prerequisites

C++ documentation generation requires `clang` to be installed.

- **Windows**: Install [LLVM](https://releases.llvm.org/) or add "C++ Clang tools for Windows"
  via the Visual Studio Installer.
- **macOS**: Install Xcode Command Line Tools: `xcode-select --install`
- **Linux**: Install via your package manager, e.g. `sudo apt install clang` or
  `sudo dnf install clang`.

### Clang Executable Discovery

ApiMark locates the clang executable using the following priority order:

1. `--clang-path <path>` — explicit path, used as-is (must exist on disk).
2. `APIMARK_CLANG_PATH` environment variable — set this in CI or shell profiles
   to configure clang project-wide without repeating the path on every invocation.
3. `clang` on the system `PATH`.
4. `xcrun clang` — macOS only, selects the active Xcode SDK automatically.
5. vswhere-located LLVM clang / `C:\Program Files\LLVM\bin\clang.exe` — Windows only.

## CLI Options

```text
apimark cpp [options]
```

| Option | Description |
| --- | --- |
| `--includes <path>` | Include directory for clang `-I` (repeatable, required) |
| `--api-headers <pattern>` | Glob pattern for documented headers; supports `!` exclusions (repeatable, ordered) |
| `--output <dir>` | Output directory for Markdown files (required) |
| `--format <value>` | Output format: `gradual` (file-per-type) or `single-file` (single `api.md`) (default: `gradual`) |
| `--library-name <name>` | Library name used as the top-level heading (default: output directory name) |
| `--library-description <d>` | Optional description for the library `api.md` introduction |
| `--defines <values>` | Comma-separated preprocessor definitions (e.g. `MYLIB_API=,NDEBUG`) |
| `--cpp-standard <std>` | C++ language standard passed to Clang (default: `c++17`) |
| `--clang-path <path>` | Path to clang executable (default: auto-discovered, see *Prerequisites*) |
| `--visibility <value>` | Visibility filter: `Public`, `PublicAndProtected`, `All` (default: `Public`) |
| `--include-obsolete` | Include deprecated members in generated output |

## File Discovery

Two options work together to control which headers are passed to Clang and which appear
in the generated documentation.

### `--includes <path>` (repeatable)

Passed to Clang as `-I` paths so `#include` statements in headers can be resolved.
Also serves as the default search root when `--api-headers` is not specified. Accepts
absolute or relative directory paths. At least one `--includes` is required.

### `--api-headers <pattern>` (repeatable, optional)

Glob patterns that determine which header files appear in the generated documentation.

- Patterns may be **absolute** (e.g. `/usr/local/include/**/*.h`, `C:\SDK\include\**\*`)
  or **relative** (resolved from the current working directory).
- Patterns ending with a bare `*` (no extension) automatically select recognized C++ header
  extensions: `.h`, `.hpp`, `.hxx`, `.h++`.
- Patterns ending with a specific extension (e.g. `**/*.h`) select only that extension.
- Prefix a pattern with `!` to exclude matching files. All include patterns are applied
  first to build the file set; exclusion patterns then remove files from the result.
- When `--api-headers` is not specified, all recognized header files under every
  `--includes` directory are documented.

### Examples

```text
# Document all headers under include/
apimark cpp --includes include/ --output docs/api

# Document only public API headers, excluding detail/ subtree
apimark cpp \
  --includes include/ \
  --api-headers "include/**" \
  --api-headers "!include/detail/**" \
  --output docs/api

# Use an absolute SDK path alongside local headers
apimark cpp \
  --includes include/ \
  --includes /opt/sdk/include \
  --api-headers "include/**" \
  --api-headers "/opt/sdk/include/mylib/**" \
  --output docs/api
```

| Pattern | Effect |
| --- | --- |
| `include/**` | Include all headers under `include/` |
| `!include/detail/**` | Exclude all headers under `include/detail/` |

## Documented Constructs

ApiMark parses header files using Clang and produces documentation for the following
constructs.

- **Namespaces** — all public namespaces containing at least one documented declaration
- **Classes and structs** — with base classes, template parameters, and `final` specifier
- **Functions** — free functions and member functions; variadic, deleted, and
  const-qualified signatures are all represented
- **Type aliases** — `using` declarations
- **Enumerations** — scoped (`enum class`) and unscoped (`enum`) enumerations

Constructs are filtered by the `--api-headers` patterns — only declarations from
matched headers appear in the output.

## Doc Comments

ApiMark reads Doxygen-style doc comments placed immediately before a declaration.

Single-line form:

```cpp
/// @brief Opens a file for reading.
```

Block form:

```cpp
/**
 * @brief Opens a file for reading.
 * @param path Absolute or relative path to the file.
 * @return A file handle on success, or nullptr on failure.
 * @throws std::runtime_error if the path is inaccessible.
 */
```

| Tag | Purpose |
| --- | --- |
| `@brief` | One-line description shown in index tables and at the top of detail pages |
| `@param <name>` | Description for a function parameter |
| `@return` | Description for the return value |
| `@throws <type>` | Documents an exception the function may throw |

Missing `@brief` tags render as *No description provided.* in the output.

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
| H1 | Library name |
| H2 | Namespace |
| H3 | Type (with prototype code block and member bullet list) |
| H4 | Individual member (signature, parameters, return value, example) |

Single-file output is best suited for contexts where a complete, linear reference is
preferred over a navigable multi-file tree, such as attaching documentation to a chat
context window.

## MSBuild Integration

Add the `DemaConsulting.ApiMark.MSBuild` NuGet package to your `.vcxproj`:

```xml
<ItemGroup>
  <PackageReference Include="DemaConsulting.ApiMark.MSBuild" Version="x.y.z" />
</ItemGroup>
```

ApiMark discovers include paths from `AdditionalIncludeDirectories` automatically for
projects where that property is set in the conventional way. For projects with unusual
include structures, generated headers, or complex NuGet arrangements, use the
`apimark cpp` CLI command directly for full control.

### C++-Specific MSBuild Properties

| Property | Default | Description |
| --- | --- | --- |
| `ApiMarkIncludePaths` | *(auto-detected)* | Semicolon-separated include directory paths passed to Clang as `-I` paths. Defaults to the resolved `AdditionalIncludeDirectories` from all `ClCompile` items (including NuGet-injected paths). Set explicitly to override auto-detection. When `ApiMarkApiHeaders` is not set, all headers with recognized C++ extensions under these paths are documented. |
| `ApiMarkApiHeaders` | *(unset)* | Semicolon-separated, order-preserved list of glob and exclusion pattern strings. Entries with `!` are exclusion patterns; gitignore-style last-match-wins semantics apply. When unset, all headers with recognized C++ extensions under `ApiMarkIncludePaths` are documented. |
| `ApiMarkLibraryName` | `$(MSBuildProjectName)` | Library name used as the top-level heading in `api.md` |
| `ApiMarkLibraryDescription` | *(unset)* | Optional description for the `api.md` introduction paragraph |
| `ApiMarkDefines` | *(unset)* | Semicolon-separated preprocessor definitions (e.g. `MYLIB_API=;NDEBUG`) |
| `ApiMarkCppStandard` | `c++17` | C++ language standard passed to Clang |
| `ApiMarkClangPath` | *(auto-discovered)* | Path to clang executable; overrides PATH / xcrun / vswhere discovery |
| `ApiMarkVisibility` | `Public` | Visibility filter: `Public`, `PublicAndProtected`, `All` |
| `ApiMarkIncludeObsolete` | `false` | Include deprecated members in generated output |

See the *MSBuild Integration* section for common properties (`ApiMarkOutputDir`,
`ApiMarkFormat`, `DisableApiMark`) that apply to all project types.

### Configuration Example

```xml
<PropertyGroup>
  <!-- Change the output directory -->
  <ApiMarkOutputDir>$(MSBuildProjectDirectory)\docs\api</ApiMarkOutputDir>

  <!-- Set the library name used in api.md heading -->
  <ApiMarkLibraryName>MyLibrary</ApiMarkLibraryName>

  <!-- Add a one-line description to api.md -->
  <ApiMarkLibraryDescription>A fast, portable geometry library.</ApiMarkLibraryDescription>

  <!-- Use C++20 -->
  <ApiMarkCppStandard>c++20</ApiMarkCppStandard>

  <!-- Override clang path (optional; normally auto-discovered) -->
  <!-- <ApiMarkClangPath>C:\Program Files\LLVM\bin\clang.exe</ApiMarkClangPath> -->

  <!-- Document all headers except a detail/ subtree (gitignore-style last-match-wins) -->
  <!-- <ApiMarkApiHeaders>**/*;!**/detail/**</ApiMarkApiHeaders> -->

  <!-- Re-include one header from the excluded subtree -->
  <!-- <ApiMarkApiHeaders>**/*;!**/detail/**;**/detail/public_api.h</ApiMarkApiHeaders> -->

  <!-- Override include paths (optional; defaults to AdditionalIncludeDirectories) -->
  <!-- <ApiMarkIncludePaths>$(MSBuildProjectDirectory)\include</ApiMarkIncludePaths> -->
</PropertyGroup>
```
