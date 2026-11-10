# Introduction

This guide describes how to install, configure, and use ApiMark.

## Purpose

Provides installation, configuration, and usage instructions for ApiMark users
and integrators. ApiMark generates compact, AI-friendly API reference
documentation in Markdown from source code and its associated metadata (XML doc
comments, header files, docstrings, etc.).

The workflow below shows how ApiMark is used: document your code, run the `apimark` CLI or
MSBuild integration, and use the resulting Markdown with an AI agent, in your repository, or
in your NuGet package.

![ApiMark workflow: document your code, run ApiMark, get Markdown, then use it with an AI agent, your repository or your NuGet package](workflow.svg)

## Scope

Covers the `apimark` CLI tool and the `DemaConsulting.ApiMark.MSBuild` NuGet package
integration. Excludes internal design details; see the design documentation for those. This guide includes
installation instructions, a global CLI reference, MSBuild integration concepts, and
dedicated language sections for .NET, C++, and VHDL — each covering CLI options,
documented constructs, doc comment format, and output structure. The C++ and VHDL
sections also cover file discovery (those generators accept source-file paths or glob
patterns); the .NET section uses compiled assembly input instead and does not have a
file-discovery step. The .NET and C++ sections also cover language-specific MSBuild
configuration; VHDL has no MSBuild integration.

## References

None.
