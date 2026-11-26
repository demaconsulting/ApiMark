---
title: Troubleshooting
summary: Fix the common error messages, and find out why a run printed nothing.
aliases:
  - errors
  - problems
  - faq
tags:
  - troubleshooting
order: 11
see-also:
  - command-line
  - cpp
  - vhdl
examples:
  - name: wrong-case
    kind: command
    language: shell
    description: Language names are lower case. Exits 1.
    content: '{{tool}} DotNet --output api'
    exit: 1
    expect: "Unrecognized language subcommand 'DotNet'"
  - name: flag-as-value
    kind: command
    language: shell
    description: An option whose value is missing takes the next option. Exits 1.
    content: '{{tool}} vhdl --output --source'
    exit: 1
    expect: "--output requires a directory path argument but got another flag '--source'"
  - name: missing-assembly
    kind: command
    language: shell
    description: An assembly that does not exist. Exits 1.
    content: '{{tool}} dotnet --assembly Missing.dll --xml-doc Missing.xml --output api'
    exit: 1
    expect: 'Error: Assembly file not found.'
  - name: unknown-topic
    kind: command
    language: shell
    description: A help topic that does not exist. Exits 2.
    content: '{{tool}} help no-such-topic'
    exit: 2
    expect: 'no-such-topic'
---
Errors are printed to standard error as `Error: ...` and set exit code 1;
see [[command-line]] for which of them `--silent` hides.

- `No language subcommand specified`: name `dotnet`, `cpp`, or `vhdl`.
- `--output is required`: every language needs `--output`. This is
  reported before an unknown language.
- `Unrecognized language subcommand`: the names are lower case
  ([[#wrong-case]]).
- `requires ... but got another flag`: the option before it has no
  value ([[#flag-as-value]]).
- `Unsupported argument`: check the spelling. Only one language word is
  allowed, and `help` must come first.
- `Assembly file not found` or `XML documentation file not found`: check
  the paths ([[#missing-assembly]]); build the project with
  `GenerateDocumentationFile` on.
- `no .vhd or .vhdl files matched`: globs are relative to the current
  folder; quote them so that the shell does not expand them.
- `Public include root not found`: an `--includes` folder is missing.
- `Clang executable not found`: install LLVM clang, also on the CI
  runner, or set `--clang-path` or `APIMARK_CLANG_PATH` to the clang
  program, not its folder; see [[cpp]] for the search order.
- clang did not finish in time: raise `--clang-timeout-ms`; see [[cpp]].
- `clang reported errors in public headers`: add the missing `--includes`
  or `--defines`, or fix the header.
- `--depth must be 1-3 for single-file output`: lower `--depth`.
- `Invalid visibility value` or `Invalid --enforce-docs value`: use
  `Public`, `PublicAndProtected`, or `All`.
- `Unable to read response file`: see [[response-files]]. In PowerShell,
  quote `"@file"`.
- `Unknown help card`: the topic does not exist; the message lists the
  topics and the exit code is 2 ([[#unknown-topic]]).

**Nothing printed, but exit code 1.** `--silent` hides the errors of a
run; add `--log <file>` to keep them. `{{tool}} --silent help` is such a
run: `help` that is not the first argument is read as a language.

**Pages without descriptions or without types.** For .NET, check that the
XML file belongs to the same build as the assembly. A reference without
types means that `--visibility` or `--exclude` left nothing to document.
For C++, some kinds of declarations, such as concepts and `typedef`
declarations, are never documented; [[cpp]] lists them.
