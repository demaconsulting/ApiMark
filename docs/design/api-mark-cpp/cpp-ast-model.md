## CppAstModel

![CppAstModel Structure](ApiMarkCppView.svg)

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

CppAstModel defines the immutable record types that represent the parsed C++ AST
and extracted Doxygen comments returned by `ClangAstParser` and consumed by the
Cpp emitters.

### Data Model

| Record | Description |
| --- | --- |
| `CppAccessibility` | Enum: `Public`, `Protected`, `Private`. |
| `CppSourceLocation` | File path and line number of a declaration. |
| `CppParamDoc` | Documented parameter name and description. |
| `CppDocComment` | Parsed Doxygen comment: Summary, Details, Params, Returns, Note, Example. |
| `CppBaseType` | Direct base-class display name. |
| `CppTemplateParam` | Template parameter name. |
| `CppEnumValue` | Enumerator name and optional doc comment. |
| `CppParameter` | Function parameter: name, type name, optional default value. |
| `CppField` | Class field: name, type, accessibility, static/deprecated flags, location, doc, optional compile-time literal value. |
| `CppFunction` | Function or method: name, return type, parameters, accessibility, static/virtual/constructor/variadic/deprecated/deleted flags, location, doc. |
| `CppClass` | Class or struct: name, base types, template params, members, fields, nested classes, type aliases, deprecated/final flags, location, doc. |
| `CppEnum` | Scoped or unscoped enum: name, values, deprecated flag, location, doc. |
| `CppTypeAlias` | `using` alias: name, underlying type, deprecated flag, location, doc. |
| `CppNamespaceDecl` | Namespace: qualified name, classes, free functions, enums, type aliases, doc. |
| `CppCompilationResult` | Parsed namespaces plus clang error/fatal-error lines. |

`CppFunction.IsDeleted` records whether the declaration was explicitly written with
`= delete`; emitters use the flag to append the `= delete` suffix so deleted
operations remain visible in generated documentation.

`CppField.Value` is a nullable `string?` populated only when `ClangAstParser`
determines the field is `constexpr` and literal-initialized with a single simple
literal (integer, floating-point, character, boolean, or string), including a
leading unary `+`/`-` directly over a literal (e.g. `-5`); it is `null` for
computed expressions (e.g. `2 * 21`), references to other named constants,
non-`constexpr` fields, and fields with no initializer. Both C++ emitters append
`= {Value}` to the field signature line when it is non-null.

### Key Methods

N/A - the unit contains immutable record and enum definitions only.

### Error Handling

N/A - validation occurs in `ClangAstParser` before record construction.

### External Interfaces

N/A - in-process value model only.

### Dependencies

N/A - self-contained immutable records with no runtime dependencies beyond the BCL.

### Callers

- **ClangAstParser** — constructs all model records.
- **CppGenerator** — iterates model records when building the known-type map and
  grouping namespaces.
- **CppEmitter** — reads the model to build signatures, tables, and links.
- **CppEmitterGradualDisclosure** — reads the model to generate multi-file output.
- **CppEmitterSingleFile** — reads the model to generate single-file output.
