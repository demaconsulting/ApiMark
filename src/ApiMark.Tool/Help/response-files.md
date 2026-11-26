---
title: Response files
summary: Keep the arguments of a run in a file and pass it as @file.
aliases:
  - config
  - configuration
  - rsp
  - at-file
tags:
  - reference
order: 8
see-also:
  - command-line
  - vhdl
examples:
  - name: apimark-rsp
    kind: source
    language: text
    save-as: apimark.rsp
    description: A response file that documents the VHDL files of a folder.
    content: |
      vhdl
      --source
      *.vhd
      --output
      api
      --format
      single-file
      --library-name
      Counters
  - name: run
    kind: command
    language: shell
    description: Run the tool with the arguments of apimark.rsp.
    content: '{{tool}} "@apimark.rsp"'
    expect: 'counter.vhd'
  - name: missing
    kind: command
    language: shell
    description: A response file that does not exist. Exits 1.
    content: '{{tool}} "@missing.rsp"'
    exit: 1
    expect: "Error: Unable to read response file 'missing.rsp'"
---
{{tool}} reads no configuration file of its own. To keep a long or
repeated command in a file, write one argument per line, as
[[#apimark-rsp]] shows, and pass the file as `@<file>`
([[#run]]). The arguments of the
file take the place of the `@<file>` argument, so the file can hold a
whole run or only part of one, such as a long list of `--reference-paths`.

**Rules.**

- One argument per line, used exactly as written: no quotes, and no spaces
  before or after it. A value such as `*.vhd` needs no quotes because no
  shell reads the file.
- Blank lines are skipped.
- A line that starts with `@` is not read as another response file.
- `@@text` passes `@text`, on the command line and in a file.
- A file that cannot be read is an argument error: it is printed even
  with `--silent` and exits 1 ([[#missing]]).

**Shells.** PowerShell reads `@name` as splatting, so quote the argument:
`{{tool}} "@apimark.rsp"`. bash and cmd accept it with or without quotes.

The MSBuild package writes such a file itself when it passes many
reference assemblies; see [[msbuild]].
