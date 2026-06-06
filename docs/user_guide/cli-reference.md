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
| `--visibility <value>` | Visibility filter: `Public`, `PublicAndProtected`, `All` (default: `Public`) |
| `--include-obsolete` | Include obsolete members in generated output |

### `cpp` — Generate API Documentation from C++ Headers

```text
apimark cpp [options]
```

| Option | Description |
| --- | --- |
| `--includes <paths>` | Comma-separated list of public include directories (required) |
| `--output <dir>` | Output directory for Markdown files (required) |
| `--library-name <name>` | Library name used as the top-level heading (default: output directory name) |
| `--library-description <d>` | Optional description for the library `api.md` introduction |
| `--defines <values>` | Comma-separated preprocessor definitions (e.g. `MYLIB_API=,NDEBUG`) |
| `--cpp-standard <std>` | C++ language standard passed to Clang (default: `c++17`) |
| `--visibility <value>` | Visibility filter: `Public`, `PublicAndProtected`, `All` (default: `Public`) |
| `--include-obsolete` | Include deprecated members in generated output |

## Platform Support

| Platform | `dotnet` | `cpp` |
| --- | --- | --- |
| Windows x64 | ✅ | ✅ |
| Linux x64 | ✅ | ✅ |
| macOS (Apple Silicon) | ✅ | ✅ |

## Output Structure

ApiMark uses a four-tier gradual disclosure layout:

| File | Description |
| --- | --- |
| `api.md` | Root index — lists all namespaces with type counts and one-line summaries |
| `{namespace}.md` | Namespace summary — lists all types, enums, and functions with one-line summaries |
| `{namespace}/{type}.md` | Type page — members grouped by kind with signatures and doc comment details |
| `{namespace}/{type}/{member}.md` | Member detail page — full signature, parameters, return value, remarks |

An AI agent can read the root index first, drill into the relevant namespace
summary, and then load a specific type or member page — consuming only as much
context as the task requires.
