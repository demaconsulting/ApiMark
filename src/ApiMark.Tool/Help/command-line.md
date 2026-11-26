---
title: Command-line options
summary: The global options, the order in which a run handles them, and the exit codes.
aliases:
  - options
  - flags
  - usage
  - exit-codes
tags:
  - reference
order: 2
see-also:
  - troubleshooting
  - response-files
examples:
  - name: usage
    kind: command
    language: shell
    description: Print the usage text. It ends with a pointer to this help.
    content: '{{tool}} --help'
    expect: "Run '{{tool}} help' for topics and examples."
  - name: silent-log
    kind: command
    language: shell
    description: Hide the console output and keep it in a log file.
    content: '{{tool}} --silent --log output.log --version'
  - name: unknown-option
    kind: command
    language: shell
    description: An unknown argument is printed even with --silent. Exits 1.
    content: '{{tool}} --silent --unknown'
    exit: 1
    expect: "Error: Unsupported argument '--unknown'"
  - name: no-language
    kind: command
    language: shell
    description: Without a language there is nothing to do. Exits 1.
    content: '{{tool}}'
    exit: 1
    expect: 'Error: No language subcommand specified.'
---
`{{tool}} [options] <language> [language-options]` runs one language: the
first word that is not an option, `dotnet`, `cpp`, or `vhdl`, written in
lower case. The options of [[dotnet]], [[cpp]], and [[vhdl]] may come
before or after it, in any order. A value may not start with `-`.

| Option | Meaning |
| --- | --- |
| `-v`, `--version` | Print only the version. |
| `-?`, `-h`, `--help` | Print the usage text. |
| `--silent` | Hide console output. Errors still set exit code 1. |
| `--log <file>` | Also write the output to a file; it is replaced. |
| `--output <dir>` | The folder for the pages; every language needs it. |
| `--format <value>` | `gradual` (default) or `single-file`; see [[output]]. |
| `--depth <n>` | Level of the first heading, 1 to 6; default 1. |
| `--visibility <value>` | `Public`, `PublicAndProtected`, or `All`; default per language. |
| `--enforce-docs <tier>` | Report undocumented items; see [[doc-coverage]]. |
| `--validate` | Run the self-tests; see [[self-validation]]. |
| `--results <file>` | Write the self-test results as `.trx` or `.xml`. |

Any argument can also come from a file as `@<file>`; see
[[response-files]].

**Order.** `--version`, when the arguments parse, prints only the version
and skips everything else, even a wrong value such as an unknown
visibility. Otherwise the tool prints a banner, then handles the first of
`--help`, `--validate`, or the language. The command that [[#silent-log]]
prints writes the version to the log file only.

**Errors.** An unknown argument, or an option without its value, prints
`Error: ...` and exits 1 ([[#unknown-option]]). These errors are found
before `--silent` and `--log` take effect, so they are always printed and
never logged. Later errors, such as a missing option ([[#no-language]]) or
an input file that does not exist, obey `--silent` and `--log`.

**Exit codes.** 0 on success; 1 for an error, a failed self-test, or
undocumented items at severity `Error`; 2 for a wrong `help` argument.

**Help.** `help` is a command, not an option, and must be the first
argument. `--silent` and `--log` do not apply to it, and an unknown topic
exits 2.
