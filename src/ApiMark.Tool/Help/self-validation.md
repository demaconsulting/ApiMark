---
title: Self-validation
summary: Prove that the installed tool works, and save the evidence as test results.
aliases:
  - validate
tags:
  - reference
order: 10
see-also:
  - command-line
examples:
  - name: run
    kind: command
    language: shell
    description: Run the self-tests. Prints one line per test and exits 0 when none fails.
    content: '{{tool}} --validate'
    expect: 'Failed: 0'
  - name: trx
    kind: command
    language: shell
    description: Also write the results as a TRX file.
    content: '{{tool}} --validate --results validation.trx'
  - name: junit
    kind: command
    language: shell
    description: Write the results as JUnit XML instead.
    content: '{{tool}} --validate --results validation.xml'
  - name: depth
    kind: command
    language: shell
    description: Start the report at heading level 2 to embed it in a larger document.
    content: '{{tool}} --validate --depth 2'
    expect: '## '
  - name: bad-extension
    kind: command
    language: shell
    description: Any other extension is an error. Exits 1.
    content: '{{tool}} --validate --results validation.txt'
    exit: 1
    expect: 'Unsupported results file format'
---
`--validate` runs a short set of tests of the tool itself in a temporary
folder, so a regulated project can show that the installed version works on
its build machine. The tests check the version and usage text, generate
pages from a small .NET assembly that they create, from C++ headers, and
from VHDL files, and check the documentation coverage report of each
language.

**C++ tests.** `ApiMark_CppGeneration` and `ApiMark_CppEnforceDocs` need
clang. When clang is not found they are marked `Skipped` with the reason,
and they do not change the exit code.

**Report.** The output is Markdown. It starts with a heading and a table of
the tool version, machine name, operating system, .NET runtime, and time
stamp. Each test prints one line marked `Passed`, `Skipped`, or `Failed`
with a reason, and the report ends with `Total Tests`, `Passed`,
`Skipped`, and `Failed` lines. The exit code is 0 only when no test fails.
`--depth` sets the level of the first heading, as [[#depth]] shows.

**Results files.** `--results <file>` also writes the outcome; the
extension chooses the format. `.trx` writes a Visual Studio TRX file and
`.xml` writes JUnit XML, both readable by CI test-report tasks. Any other
extension is an error after the tests ran ([[#bad-extension]]). The folder
of the file must exist.

**Evidence.** The tests have stable names, such as
`ApiMark_VersionDisplay`, `ApiMark_DotNetGeneration`, and
`ApiMark_VhdlGeneration`, so a requirements file can list them as the
tests that prove the tool works in its environment.
