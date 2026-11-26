---
title: C++ libraries
summary: Document C++ headers with clang, and see what it skips, such as concepts.
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
  - name: clang-path-windows
    kind: snippet
    language: powershell
    save-as: clang-path-windows.ps1
    description: Use clang from a versioned LLVM folder on Windows.
    content: |
      $env:APIMARK_CLANG_PATH = 'C:\Program Files\LLVM\<version>\bin\clang.exe'
      {{tool}} cpp --includes include --output api
  - name: ci-clang
    kind: snippet
    language: yaml
    save-as: ci-clang.yaml
    description: GitHub Actions steps that install clang on an Ubuntu runner and run the tool.
    content: |
      - name: Install clang
        run: sudo apt-get update && sudo apt-get install --yes clang
      - name: Run {{tool}}
        run: dotnet {{tool}} cpp --includes include --output docs/api
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

**What is documented.** Namespaces; classes, structs, and unions that are
defined, not only declared; primary class templates; enums; `using`
aliases; and free functions and function templates, including operators.
In a class: constructors, methods, operators, fields, static data members,
nested classes and class templates, and `using` aliases.

**What is not documented.** Concepts, `typedef` declarations, alias
templates (`template <...> using`), variable templates, variables and
constants outside a class, macros, explicit and partial specializations,
destructors, friend declarations, enums and `typedef` declarations inside
a class, and any declaration inside an `extern "C"` block. The tool skips
them without a warning, even when they have Doxygen comments. Union members
count as private, so they appear only with `--visibility All`.

**Clang.** Install LLVM clang first. The tool runs `--clang-path`, else
the `APIMARK_CLANG_PATH` environment variable, else `clang` on the `PATH`,
else `xcrun clang` on macOS, else on Windows the LLVM clang of Visual
Studio, then `C:\Program Files\LLVM\bin\clang.exe`. `--clang-path` and
`APIMARK_CLANG_PATH` name the clang program itself, not its folder, and a
path that does not exist is an error; the search does not go on. On the
`PATH`, only the name `clang` is found, so name `clang-18` and the like
with the variable ([[#clang-path]]). For clang in another folder, such as
a versioned `C:\Program Files\LLVM\<version>\bin`, point the variable or
`--clang-path` at `clang.exe` in that folder ([[#clang-path-windows]]), or
put the folder on the `PATH`. clang must finish within
`--clang-timeout-ms`, else `APIMARK_CLANG_TIMEOUT_MS`, else 120000 ms.

**CI.** Install clang on the runner too. After the steps of
[[getting-started#ci-steps]], add the steps of [[#ci-clang]] on Ubuntu.
On Windows, install LLVM with its installer, which puts clang in
`C:\Program Files\LLVM\bin`.
