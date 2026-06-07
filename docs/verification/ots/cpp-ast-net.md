> **Archived**: ApiMark.Cpp no longer uses CppAst.Net. This document is retained for historical
> reference. See `docs/verification/ots/clang.md` for the current verification approach.

## CppAst.Net

### Verification Approach

CppAst.Net was verified in ApiMark through integration tests in `test/ApiMark.Cpp.Tests/` that
exercised the header-parsing and metadata APIs used by `ApiMark.Cpp`. The verification focus was the
subset of capabilities the product depended on: parsing C++ headers without a compiler build step,
exposing per-declaration source file provenance, enumerating types and members with sufficient
metadata for documentation generation, providing structured Doxygen doc comment trees, and
accepting parse options for include paths, defines, and language standard. Evidence was collected from
automated integration tests that compared generated documentation behavior against representative
fixture headers. See `docs/verification/ots/clang.md` for the current clang-based verification approach.

### Test Scenarios

**Header files parse without a compiler build step**: Verifies that CppAst.Net can parse the fixture
headers using only the bundled libclang binary and produce a complete AST without a C++ toolchain
installation. This scenario is tested by `CppGenerator_Generate_ValidHeaders_CreatesApiEntrypoint`.

**Declaration provenance identifies public API declarations**: Verifies that the source file path
exposed by CppAst.Net per declaration is sufficient for CppGenerator to distinguish declarations
defined in the public include roots from system and third-party declarations. This scenario is
tested by `CppGenerator_Generate_ValidHeaders_CreatesTypePageForSampleClass`.

**Type and member metadata is complete enough for documentation generation**: Verifies that
CppAst.Net exposes the names, types, parameters, and access specifiers needed for CppGenerator to
produce accurate type pages, member detail pages, and function signatures. These scenarios are
tested by `CppGenerator_Generate_ValidHeaders_CreatesTypePageForSampleClass`,
`CppGenerator_Generate_ValidHeaders_CreatesEnumPage`, and
`CppGenerator_Generate_TemplateClass_CreatesTypePage`.

**Doxygen doc comments are accessible on parsed declarations**: Verifies that structured Doxygen
comment data is available on parsed declarations so CppGenerator can render `@brief` descriptions
as paragraphs in the output. This scenario is tested by
`CppGenerator_Generate_TypeWithDocComment_WritesSummaryToParagraph`.

**Parse options are forwarded to the Clang parser**: Verifies that include paths and parse options
provided through CppGeneratorOptions are accepted by CppAst.Net and applied during parsing so
headers that depend on other include directories resolve correctly. This scenario is tested by
`CppGenerator_Generate_ValidHeaders_CreatesApiEntrypoint`.
