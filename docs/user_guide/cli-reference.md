# CLI Reference

## Synopsis

```text
Usage: apimark [options] [language [language-options]]
```

## Global Options

| Option | Description |
|---|---|
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
|---|---|
| `--assembly <path>` | Path to the .NET assembly (required) |
| `--xml-doc <path>` | Path to the XML documentation file |
| `--output <dir>` | Output directory for Markdown files (required) |
| `--visibility <value>` | Visibility filter: `Public`, `PublicAndProtected`, `All` (default: `Public`) |
| `--include-obsolete` | Include obsolete members in generated output |

## Output Structure

ApiMark uses a three-tier gradual disclosure layout:

| File | Description |
|---|---|
| `api.md` | Root index — lists all namespaces with one-line summaries |
| `{namespace}/{namespace}.md` | Namespace summary — lists all types with one-line summaries |
| `{namespace}/{type}.md` | Full type page — members, signatures, and doc comment details |

An AI agent can read the root index first, drill into the relevant namespace
summary, and then load a specific type page — consuming only as much context
as the task requires.
