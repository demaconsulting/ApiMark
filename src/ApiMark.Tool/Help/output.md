---
title: Generated pages
summary: The Markdown pages that the tool writes, in gradual or single-file format.
aliases:
  - api-reference
  - format
  - gradual
  - single-file
  - markdown
tags:
  - reference
order: 6
see-also:
  - dotnet
  - cpp
  - vhdl
examples:
  - name: gradual
    kind: command
    language: shell
    description: Write api/api.md and one page per entity of counter.vhd.
    content: '{{tool}} vhdl --source counter.vhd --output api'
  - name: single-file
    kind: command
    language: shell
    description: Write the whole reference to api/api.md.
    content: '{{tool}} vhdl --source counter.vhd --output api --format single-file'
  - name: depth
    kind: command
    language: shell
    description: Start the pages at heading level 2, to embed them.
    content: '{{tool}} vhdl --source counter.vhd --output api --depth 2'
  - name: depth-too-deep
    kind: command
    language: shell
    description: A single file allows a depth of 1 to 3 only. Exits 1.
    content: '{{tool}} vhdl --source counter.vhd --output api --format single-file --depth 4'
    exit: 1
    expect: '--depth must be 1-3 for single-file output'
---
The tool writes Markdown pages into the `--output` folder and creates the
folder when it does not exist. It does not delete pages from an earlier
run, so empty the folder first when items were removed or renamed.

**Gradual (default).** A tree of pages that a reader, or an AI agent,
opens one level at a time ([[#gradual]]):

- `api.md`: the index, with a one-line summary of each namespace (.NET and
  C++) or each entity and package (VHDL).
- One page per namespace, with its types and their summaries; for VHDL,
  one page per entity or package.
- .NET and C++: one page per type, in a folder named after the namespace,
  with its members and their signatures.
- One page per member or subprogram with its full signature, parameters,
  return value, and remarks.

**Single file.** `--format single-file` writes everything to one
`api.md`, which suits a document or a chat context ([[#single-file]]).

**Headings.** `--depth <n>` sets the level of the first heading, 1 to 6
([[#depth]]). Single-file output uses three more levels below it, so it
allows a depth of 1 to 3 only ([[#depth-too-deep]]).

**Names.** File and folder names are legal on Windows and Linux and unique
without regard to case: illegal characters become `_`, reserved device
names such as `aux` get a trailing `_`, and a second item with the same
name gets `-2`. Links use the same names; link text keeps the real name.
