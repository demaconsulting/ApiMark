# CLI Reference

## Synopsis

```text
Usage: apimark [options] [language [language-options]]
       apimark help [topic]
```

## Global Options

| Option | Description |
| --- | --- |
| `-v`, `--version` | Display version information |
| `-?`, `-h`, `--help` | Display the help message |
| `--silent` | Suppress console output |
| `--validate` | Run self-validation tests |
| `--results <file>` | Write validation results to file (`.trx` or `.xml`) |
| `--depth <#>` | Set the top-level heading depth for generated Markdown output (default: `1`; see below) |
| `--log <file>` | Write all output to log file |

The `--depth` value is restricted to `1`–`3` when `--format single-file` is used.

Argument errors (an unknown option, a missing option value, or a value that starts with `-`)
are found before `--silent` and `--log` take effect, so they are always printed and never
logged. Errors found later, such as a missing required option or a generation failure,
follow `--silent` and `--log`.

## Built-in Help

The `help` command prints help topics that explain each task with worked examples, for people
and for AI agents that have only the installed tool. The `--help` usage text ends with a
pointer to it. List the topics, then print one with its examples:

```bash
apimark help
apimark help getting-started
```

The topics cover getting started, the command line, each language (`dotnet`, `cpp`, `vhdl`),
the generated output, documentation coverage, response files, the MSBuild package,
self-validation, and troubleshooting. Add `--search <words>` to find a topic,
`--example <name>` to print one example of a topic, and `--json` to get any help answer as
JSON. `help` is a command, not an option or a language: it must be the first argument, and
`--silent`, `--log`, and `@file` response files do not apply to it. An unknown topic or a
wrong help argument prints a hint and exits with code 2 (see *Exit Codes*).

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
any API documentation for your project. Use this in CI to verify that the installed tool
is functioning correctly: the results file is evidence that the pinned tool does its job
on that machine, not only that it starts. Each test runs the tool end to end, in-process,
on small sample sources it writes into its own temporary folder (removed afterwards), and
passes only when the exit code, the reported findings, and the generated Markdown are
exactly as expected.

- `ApiMark_VersionDisplay` - `--version` prints exactly the tool version.
- `ApiMark_HelpDisplay` - `--help` prints the usage and the options of every subcommand.
- `ApiMark_DotNetGeneration` - `dotnet --format single-file` documents a sample assembly's types, members,
  signatures, and descriptions in one `api.md`.
- `ApiMark_GradualDisclosureOutput` - `dotnet` writes the expected index, namespace, type, and member pages, and
  every link between them, including heading anchors, resolves.
- `ApiMark_CppGeneration` - `cpp` documents a sample header's struct and field.
- `ApiMark_VhdlGeneration` - `vhdl` documents a sample entity's description, generics, and ports.
- `ApiMark_DotNetEnforceDocs` - `--enforce-docs` reports exactly a missing .NET doc comment and fails the run only
  at `Error` severity.
- `ApiMark_CppEnforceDocs` - `--enforce-docs` reports exactly an undocumented C++ field and fails the run.
- `ApiMark_VhdlEnforceDocs` - `--enforce-docs` reports exactly an undocumented VHDL port and fails the run.

The .NET sample assembly is built in memory by the tool itself, so no SDK or compiler is
needed. The two C++ tests need clang, which is not part of the tool package; they are
automatically skipped (not failed) when no clang installation can be found — install
LLVM clang or set `APIMARK_CLANG_PATH` to include them in validation.

```bash
apimark --validate
```

Combine with `--results` to write a structured results file for CI test reporters:

```bash
apimark --validate --results results/apimark.trx
```

The `--results` option accepts either a `.trx` file path (Visual Studio Test Results
format) or an `.xml` file path (JUnit-compatible XML). The summary output includes
`Total Tests`, `Passed`, `Skipped`, and `Failed` counts. The exit code is non-zero if
any validation test fails; skipped tests (such as the C++ check when clang is
unavailable) never affect the exit code.

## Platform Support

| Platform | `dotnet` | `cpp` | `vhdl` |
| --- | --- | --- | --- |
| Windows x64 | ✅ | ✅ | ✅ |
| Linux x64 | ✅ | ✅ | ✅ |
| macOS | ✅ | ✅ | ✅ |

## Exit Codes

| Code | Meaning |
| --- | --- |
| `0` | Success |
| `1` | Error |
| `2` | The `help` command was given an unknown topic or a wrong help argument |

Exit code `0` includes `--version`, `--help`, the `help` command, and a passing `--validate`
run. Exit code `1` covers argument errors, a missing required option, a generation error, a
failed self-validation test, and undocumented items found with `--enforce-docs-severity Error`.
Exit code `2` comes only from the `help` command.
