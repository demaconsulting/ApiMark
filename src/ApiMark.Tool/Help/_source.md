---
name: apimark
schema: 2
title: ApiMark
summary: Built-in help of ApiMark, the Markdown API reference generator for .NET, C++, and VHDL.
---
{{tool}} writes compact Markdown API reference documentation from a .NET
assembly and its XML documentation file, from C++ headers, or from VHDL
source files. The output suits people and AI agents alike: a short index
links to pages with more and more detail.

The first word that is not an option names the language: `dotnet`, `cpp`,
or `vhdl`. The only command is `{{tool}} help`, which must be the first
argument and takes topic names, such as `{{tool}} help vhdl`.
