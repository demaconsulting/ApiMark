## CppAstModel

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

CppAstModel defines the immutable record types that represent the C++ abstract
syntax tree (AST) as parsed by `ClangAstParser`. Each record maps directly to a
construct in C++ source code and carries the documentation extracted from
Doxygen-style comments.

### Data Model

The following record types are defined (all in `ApiMark.Cpp`):

| Record | Description |
| --- | --- |
| `CppAccessibility` | Enum: `Public`, `Protected`, `Private`. |
| `CppSourceLocation` | File path and line number of a declaration. |
| `CppParamDoc` | Name and description of a documented parameter. |
| `CppDocComment` | Parsed Doxygen comment: Summary, Details, Params, Returns, Note, Example. |
| `CppBaseType` | Name of a base class. |
| `CppTemplateParam` | Name of a template parameter. |
| `CppEnumValue` | Enumerator name and optional doc comment. |
| `CppParameter` | Function parameter: name, type name, optional default value. |
| `CppField` | Class data member: name, type, accessibility, static/deprecated flags, location, doc. |
| `CppFunction` | Function or method: name, return type, parameters, accessibility, static/virtual/constructor/variadic/deprecated/deleted flags, location, doc. |
| `CppClass` | Class or struct: name, base types, template params, methods, fields, nested classes, type aliases, deprecated/final flags, location, doc. |
| `CppEnum` | Scoped or unscoped enum: name, enumerators, deprecated flag, location, doc. |
| `CppTypeAlias` | `using` type alias: name, underlying type name, deprecated flag, location, doc. |
| `CppNamespaceDecl` | One namespace: qualified name, classes, free functions, enums, type aliases, doc. |
| `CppCompilationResult` | Top-level result: list of namespace declarations and list of error strings. |

### Key Methods

N/A — CppAstModel contains only record type definitions. All construction is
performed by the C# record compiler-generated constructors.

### Error Handling

N/A — CppAstModel records perform no validation in their constructors; all
values are accepted as provided. Validation is the responsibility of
`ClangAstParser`, which constructs these records from verified JSON data.

### External Interfaces

N/A — CppAstModel records are purely in-process value types. They are
constructed exclusively by `ClangAstParser` and consumed by `CppEmitter` and
`CppEmitterGradualDisclosure`.

### Dependencies

N/A — CppAstModel has no external dependencies. All types are self-contained
immutable records with no constructor logic beyond field assignment.

### Callers

- **ClangAstParser** — constructs all CppAstModel records during AST walking.
- **CppEmitter** — reads CppAstModel records to build shared helper outputs
  (signatures, visibility filters, include-path derivation).
- **CppEmitterGradualDisclosure** — reads CppAstModel records to write all
  gradual-disclosure Markdown pages.
- **CppEmitterSingleFile** — reads CppAstModel records to write the single-file
  `api.md` output.
