---
title: Getting started
summary: Install the tool, check that it works, and write a first API reference.
aliases:
  - start
  - quickstart
  - examples
  - install
tags:
  - tutorial
order: 1
see-also:
  - command-line
  - dotnet
  - msbuild
examples:
  - name: install-local
    kind: snippet
    language: shell
    save-as: install-local.sh
    description: Add the tool to the tool manifest of a repository.
    content: |
      dotnet new tool-manifest
      dotnet tool install --local DemaConsulting.ApiMark.Tool
  - name: ci-steps
    kind: snippet
    language: yaml
    save-as: ci-steps.yaml
    description: GitHub Actions steps that set up .NET and run the tool.
    content: |
      - name: Check out the repository
        uses: actions/checkout@v4
      - name: Set up .NET 10
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.x
      - name: Restore the local .NET tools
        run: dotnet tool restore
      - name: Build the library
        run: dotnet build src/MyLib --configuration Release --output build
      - name: Run {{tool}}
        run: dotnet {{tool}} dotnet --assembly build/MyLib.dll --xml-doc build/MyLib.xml --output docs/api
  - name: version
    kind: command
    language: shell
    description: Print the version and exit.
    content: '{{tool}} --version'
  - name: validate
    kind: command
    language: shell
    description: Run the built-in self-tests. Exits 0 when none fails.
    content: '{{tool}} --validate'
    expect: 'Failed: 0'
  - name: first-run
    kind: command
    language: shell
    description: Document the .NET library MyLib.dll in the folder api.
    content: '{{tool}} dotnet --assembly MyLib.dll --xml-doc MyLib.xml --output api'
    expect: 'Found 2 types across 1 namespace(s).'
  - name: topic
    kind: command
    language: shell
    description: Print a help topic, here the list of options.
    content: '{{tool}} help command-line'
    expect: '--output'
---
{{tool}} {{version}} is a .NET tool.
It reads a compiled .NET assembly, C++ headers, or VHDL source files and
writes Markdown API reference pages into the folder that `--output` names.

1. **Install it.** The tool needs the .NET 10 SDK or later. For a
   repository or CI, install it as a local tool. Run the commands
   that [[#install-local]] prints once, without
   `dotnet new tool-manifest` when `.config/dotnet-tools.json` exists, and
   commit that file. In GitHub Actions, copy the steps that
   [[#ci-steps]] prints. They set up .NET 10 with
   `actions/setup-dotnet`, run `dotnet tool restore`, and then run
   `dotnet {{tool}}`. For C++, also install LLVM clang on the runner; see
   [[cpp]]. For one machine, install it globally and run it
   as `{{tool}}`:

   ```bash
   dotnet tool install -g DemaConsulting.ApiMark.Tool
   ```

2. **Check it.** The command that [[#version]] prints shows the version.
   The command from [[#validate]] runs the built-in self-tests and exits 0
   when none fails; see [[self-validation]].
3. **Document a library.** Name the language first, then its inputs and
   the output folder. The command that [[#first-run]] prints documents
   `MyLib.dll` with its XML documentation file `MyLib.xml`; see [[dotnet]],
   [[cpp]], and [[vhdl]] for each language, and [[output]] for the pages.
   For .NET and C++ projects, the MSBuild package can run the tool after
   every build instead; see [[msbuild]].

Print any help topic with `{{tool}} help <topic>`, as in the command that
[[#topic]] prints. Add `--search <words>` to find a topic, and `--json` to
get any help answer as JSON.
