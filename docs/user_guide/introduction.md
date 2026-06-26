# Introduction

This guide describes how to install, configure, and use ApiMark.

## Purpose

Provides installation, configuration, and usage instructions for ApiMark users
and integrators. ApiMark generates compact, AI-friendly API reference
documentation in Markdown from source code and its associated metadata (XML doc
comments, header files, docstrings, etc.).

## Scope

Covers the `apimark` CLI tool and the `DemaConsulting.ApiMark.MSBuild` NuGet package
integration. Excludes internal architecture and design details. This guide includes
installation instructions, a global CLI reference, MSBuild integration concepts, and
dedicated language sections for .NET, C++, and VHDL — each covering CLI options,
documented constructs, doc comment format, and output structure. The C++ and VHDL
sections also cover file discovery (those generators accept source-file paths or glob
patterns); the .NET section uses compiled assembly input instead and does not have a
file-discovery step. The .NET and C++ sections also cover language-specific MSBuild
configuration; VHDL has no MSBuild integration.

## References

None.
