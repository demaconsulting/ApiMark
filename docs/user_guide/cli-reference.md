# CLI Reference

## Synopsis

```text
Usage: apimark [options] [language [language-options]]
```

## Global Options

| Option | Description |
| --- | --- |
| `-v`, `--version` | Display version information |
| `-?`, `-h`, `--help` | Display the help message |
| `--silent` | Suppress console output |
| `--validate` | Run self-validation tests |
| `--results <file>` | Write validation results to file (`.trx` or `.xml`) |
| `--depth <#>` | Set heading depth for validation output (default: `1`) |
| `--log <file>` | Write all output to log file |

## Languages

### `dotnet` — Generate API Documentation from a .NET Assembly

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

### `cpp` — Generate API Documentation from C++ Headers

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
| `--clang-path <path>` | Path to clang executable (default: auto-discovered via `APIMARK_CLANG_PATH`, PATH / xcrun / vswhere) |
| `--visibility <value>` | Visibility filter: `Public`, `PublicAndProtected`, `All` (default: `Public`) |
| `--include-obsolete` | Include deprecated members in generated output |

#### `--includes` and `--api-headers`

`--includes <path>` is repeatable — provide it once per include directory. All directories
are passed to Clang as `-I` paths. When `--api-headers` is not specified, all recognized
header files under every `--includes` directory are documented automatically.

`--api-headers <pattern>` controls which headers appear in the generated documentation.
It is repeatable and ordered — patterns are evaluated in order with gitignore-style
last-match-wins semantics. Entries starting with `!` are exclusion patterns.

When `--api-headers` is not specified, all headers under the `--includes` directories
with recognized C++ header extensions (`.h`, `.hpp`, `.hxx`, `.h++`) are documented.

Patterns are evaluated as relative paths. When include roots lie within the current
working directory (the typical project layout), patterns are relative to the CWD —
so `include/**` targets exactly one root tree even when multiple `--includes` roots
are configured. When an include root is an absolute path outside the CWD (e.g.
`/usr/local/include`), patterns are matched against the path relative to that root
instead; filename-wildcard patterns such as `**/foo.h` work correctly in both cases.

Example — document all headers except a `detail/` subtree, then re-include one header:

```text
--includes include/ \
--api-headers "include/**" \
--api-headers "!include/detail/**" \
--api-headers "include/detail/public_api.h"
```

This is equivalent to the gitignore rule sequence:

| Pattern | Effect |
| --- | --- |
| `include/**` | Include all headers under `include/` |
| `!include/detail/**` | Exclude all headers under `include/detail/` |
| `include/detail/public_api.h` | Re-include `include/detail/public_api.h` specifically |

#### Clang executable discovery

ApiMark locates the clang executable using the following priority order:

1. `--clang-path <path>` — explicit path, used as-is (must exist on disk).
2. `APIMARK_CLANG_PATH` environment variable — set this in CI or shell profiles
   to configure clang project-wide without repeating the path on every invocation.
3. `clang` on the system `PATH`.
4. `xcrun clang` — macOS only, selects the active Xcode SDK automatically.
5. vswhere-located LLVM clang / `C:\Program Files\LLVM\bin\clang.exe` — Windows only.

## Platform Support

| Platform | `dotnet` | `cpp` |
| --- | --- | --- |
| Windows x64 | ✅ | ✅ |
| Linux x64 | ✅ | ✅ |
| macOS (Apple Silicon) | ✅ | ✅ |

## Output Structure

ApiMark supports two output formats selectable via `--format`.

### Gradual Disclosure (default: `--format gradual`)

A four-tier hierarchy of Markdown files designed for incremental context loading:

| File | Description |
| --- | --- |
| `api.md` | Root index — lists all namespaces with type counts and one-line summaries |
| `{namespace}.md` | Namespace summary — lists all types, enums, type aliases, and functions with one-line summaries |
| `{namespace}/{type}.md` | Type page — members grouped by kind with signatures and doc comment details |
| `{namespace}/{alias}.md` | Type alias page — `using` declaration, underlying type, and doc comment |
| `{namespace}/{type}/{member}.md` | Member detail page — full signature, parameters, return value, remarks, example |
| `{namespace}/{type}/{nested-type}.md` | Nested type page — same structure as a top-level type page |
| `{namespace}/{type}/{alias}.md` | Class-scoped type alias page — alias declared inside a class body |

An AI agent can read the root index first, drill into the relevant namespace
summary, and then load a specific type or member page — consuming only as much
context as the task requires.

### Single File (`--format single-file`)

All content is written to a single `api.md` file using a flat heading hierarchy:

| Level | Content |
| --- | --- |
| H1 | Assembly or library name |
| H2 | Namespace |
| H3 | Type (with prototype code block and member bullet list) |
| H4 | Individual member (signature, parameters, return value, example) |

Single-file output is best suited for contexts where a complete, linear reference
is preferred over a navigable multi-file tree, such as attaching documentation to
a chat context window.
