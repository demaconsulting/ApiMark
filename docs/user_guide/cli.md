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
| `--depth <#>` | Set the top-level heading depth for generated Markdown output (default: `1`); restricted to `1`–`3` when `--format single-file` is used |
| `--log <file>` | Write all output to log file |

## Languages

| Subcommand | Description |
| --- | --- |
| `dotnet` | Generate API documentation from a .NET assembly |
| `cpp` | Generate API documentation from C++ headers using Clang |
| `vhdl` | Generate API documentation from VHDL source files |

See the *.NET Documentation*, *C++ Documentation*, and *VHDL Documentation* sections for
the full option reference and usage details for each language subcommand.

## Self-Validation

The `--validate` flag runs ApiMark's built-in self-validation suite without generating
any API documentation. Use this in CI to verify that the installed tool is functioning
correctly.

```bash
apimark --validate
```

Combine with `--results` to write a structured results file for CI test reporters:

```bash
apimark --validate --results results/apimark.trx
```

The `--results` option accepts either a `.trx` file path (Visual Studio Test Results
format) or an `.xml` file path (JUnit-compatible XML). The exit code is non-zero if
any validation test fails.

## Platform Support

| Platform | `dotnet` | `cpp` | `vhdl` |
| --- | --- | --- | --- |
| Windows x64 | ✅ | ✅ | ✅ |
| Linux x64 | ✅ | ✅ | ✅ |
| macOS | ✅ | ✅ | ✅ |
