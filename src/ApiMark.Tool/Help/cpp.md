---
title: C++ libraries
summary: Document the public C++ headers of a library with clang.
aliases:
  - clang
  - headers
tags:
  - reference
order: 4
see-also:
  - output
  - doc-coverage
  - msbuild
examples:
  - name: header
    kind: source
    language: cpp
    save-as: include/point.h
    description: A header with Doxygen comments.
    content: |
      #pragma once

      namespace geo
      {
      /// @brief A point in the plane.
      struct Point
      {
          /// @brief The horizontal coordinate.
          double x;

          /// @brief The vertical coordinate.
          double y;
      };

      /// @brief Returns the distance between two points.
      /// @param a The first point.
      /// @param b The second point.
      /// @return The straight-line distance.
      double distance(const Point& a, const Point& b);
      }  // namespace geo
  - name: generate
    kind: snippet
    language: shell
    save-as: generate.sh
    description: Document every header below include. Needs clang.
    content: |
      {{tool}} cpp --includes include --output api
  - name: api-headers
    kind: snippet
    language: shell
    save-as: api-headers.sh
    description: Document only the public headers. Needs clang.
    content: |
      {{tool}} cpp --includes include --output api \
        --api-headers "include/**" --api-headers "!include/detail/**"
  - name: clang-path
    kind: snippet
    language: shell
    save-as: clang-path.sh
    description: Choose the clang to run and give it more time.
    content: |
      export APIMARK_CLANG_PATH=/usr/bin/clang-18
      {{tool}} cpp --includes include --output api --clang-timeout-ms 300000
  - name: missing-includes
    kind: command
    language: shell
    description: An include folder is required. Exits 1.
    content: '{{tool}} cpp --output api'
    exit: 1
    expect: 'Error: --includes is required for the cpp subcommand.'
  - name: missing-root
    kind: command
    language: shell
    description: An include folder that does not exist. Exits 1.
    content: '{{tool}} cpp --includes inc --output api'
    exit: 1
    expect: 'Error: Public include root not found'
---
`{{tool}} cpp` runs clang to read headers with Doxygen comments, such as
`///` or `/** */` with `@brief`, `@param`, `@return`, and `@throws`, as
[[#header]] shows. The examples that generate pages need clang.

| Option | Meaning |
| --- | --- |
| `--includes <dir>` | An include folder for clang `-I`. Required. |
| `--api-headers <glob>` | The headers to document; `!` excludes. |
| `--output <dir>` | The folder for the pages. Required. |
| `--library-name <name>` | The top heading; default: the output folder name. |
| `--library-description <d>` | The text at the top of `api.md`. |
| `--defines <a,b>` | Preprocessor definitions, such as `MYLIB_API=,NDEBUG`. |
| `--cpp-standard <std>` | The language standard; default `c++17`. |
| `--visibility <value>` | `Public` (default), `PublicAndProtected`, or `All`. |
| `--include-obsolete` | Also document deprecated items. |
| `--clang-path <path>` | The clang to run. |
| `--clang-timeout-ms <ms>` | Time limit for clang, at least 1000. |

`--includes` and `--api-headers` may be repeated. Without `--api-headers`,
every `.h`, `.hpp`, `.hxx`, and `.h++` file below each include folder is
documented ([[#generate]]),
and each folder must exist ([[#missing-root]]).
`--api-headers` globs are relative to the current folder; a `!` pattern
removes files from those that the other patterns select ([[#api-headers]]).
A glob ending in a bare `*` selects only header files. Quote globs so that
the shell does not expand them.

**Clang.** The tool uses `--clang-path`, else the `APIMARK_CLANG_PATH`
environment variable, else `clang` on the `PATH`, else `xcrun` on macOS or
LLVM from Visual Studio or `C:\Program Files\LLVM\bin` on Windows
([[#clang-path]]). clang must finish within `--clang-timeout-ms`, else
`APIMARK_CLANG_TIMEOUT_MS`, else 120000 ms.
