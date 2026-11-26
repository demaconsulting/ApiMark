---
title: .NET libraries
summary: Document a .NET assembly from the DLL and the XML documentation file.
aliases:
  - csharp
  - net
  - assembly
  - xml-doc
tags:
  - reference
order: 3
see-also:
  - output
  - doc-coverage
  - msbuild
examples:
  - name: doc-file
    kind: snippet
    language: xml
    save-as: doc-file.xml
    description: Make the compiler write the XML documentation file.
    content: |
      <PropertyGroup>
        <GenerateDocumentationFile>true</GenerateDocumentationFile>
      </PropertyGroup>
  - name: generate
    kind: command
    language: shell
    description: Write the gradual pages of MyLib.dll to the folder api.
    content: '{{tool}} dotnet --assembly MyLib.dll --xml-doc MyLib.xml --output api'
    expect: 'Parsing assembly: MyLib.dll'
  - name: single-file
    kind: command
    language: shell
    description: Write the whole reference to api/api.md.
    content: '{{tool}} dotnet --assembly MyLib.dll --xml-doc MyLib.xml --output api --format single-file'
  - name: public-only
    kind: command
    language: shell
    description: Leave out the protected members.
    content: '{{tool}} dotnet --assembly MyLib.dll --xml-doc MyLib.xml --output api --visibility Public'
  - name: missing-xml-doc
    kind: command
    language: shell
    description: The XML documentation file is required. Exits 1.
    content: '{{tool}} dotnet --assembly MyLib.dll --output api'
    exit: 1
    expect: 'Error: --xml-doc is required for the dotnet subcommand.'
---
`{{tool}} dotnet` reads a compiled assembly and the XML documentation file
that the compiler writes beside it. Both must exist; turn the file on in
the project as [[#doc-file]] shows. The examples read `MyLib.dll` and
`MyLib.xml`; use your own assembly and its XML file.

| Option | Meaning |
| --- | --- |
| `--assembly <path>` | The assembly to document. Required. |
| `--xml-doc <path>` | Its XML documentation file. Required. |
| `--output <dir>` | The folder for the pages. Required. |
| `--visibility <value>` | `Public`, `PublicAndProtected` (default), or `All`. |
| `--include-obsolete` | Also document `[Obsolete]` members. |
| `--exclude <pattern>` | Leave out a namespace or full type name; `*` is a wildcard. |
| `--reference-paths <dll>` | A referenced assembly, for `<inheritdoc/>`. |
| `--external-link <A=path>` | Link the types of assembly `A` to its pages. |
| `--library-description <d>` | The text at the top of `api.md`. |

`--exclude`, `--reference-paths`, and `--external-link` may be repeated.
An `--external-link` path is relative to the output folder or an `http(s)`
address of the reference that {{tool}} wrote for that assembly; its DLL
must also be given with `--reference-paths`. Without
`--library-description`, `api.md` uses the description of the assembly.

The tool prints `Parsing assembly: <file>` and the number of types and
namespaces it found ([[#generate]]). `--format single-file` writes one
`api.md` instead of a tree of pages ([[#single-file]]); see [[output]].
`--enforce-docs` lists the items without a `<summary>`; see
[[doc-coverage]].
