## Clang

### Verification Approach

Clang is verified in ApiMark through integration tests in `test/ApiMark.Cpp.Tests/` that exercise
the header-parsing capabilities used by `ApiMark.Cpp`. The verification focus is the subset of
capabilities the product depends on: discovering a clang executable on the host system, producing a
parseable JSON AST from C++ header files, including per-declaration source file provenance,
exposing structured Doxygen doc comment nodes, and providing sufficient type and member metadata for
documentation generation. Evidence is collected from automated integration tests that compare
generated documentation behavior against representative fixture headers.

### Test Scenarios

**Header files parse and produce an API entrypoint**: Verifies that clang can be discovered,
invoked with the required flags, and that the resulting JSON AST is parsed successfully to produce
the top-level `api.md` entrypoint file. This scenario is tested by
`CppGenerator_Generate_ValidHeaders_CreatesApiEntrypoint`.

**Declaration provenance identifies public API declarations**: Verifies that the source file path
exposed by clang in the JSON AST is sufficient for ClangAstParser to distinguish declarations
defined in the public include roots from system and third-party declarations. This scenario is
tested by `CppGenerator_Generate_ValidHeaders_CreatesTypePageForSampleClass`.

**Type and member metadata is complete enough for documentation generation**: Verifies that clang
exposes the names, types, parameters, and access specifiers needed for ClangAstParser to produce
accurate type pages, member detail pages, and function signatures. These scenarios are tested by
`CppGenerator_Generate_ValidHeaders_CreatesTypePageForSampleClass`,
`CppGenerator_Generate_ValidHeaders_CreatesEnumPage`, and
`CppGenerator_Generate_TemplateClass_CreatesTypePage`.

**Doxygen doc comments are accessible on parsed declarations**: Verifies that structured Doxygen
comment data is available on parsed declarations so ClangAstParser can render `@brief` descriptions
as paragraphs in the output. This scenario is tested by
`CppGenerator_Generate_TypeWithDocComment_WritesSummaryToParagraph`.

**Parse options are forwarded to clang**: Verifies that include paths and parse options provided
through CppGeneratorOptions are accepted and applied during clang invocation so headers that depend
on other include directories resolve correctly. This scenario is tested by
`CppGenerator_Generate_ValidHeaders_CreatesApiEntrypoint`.
