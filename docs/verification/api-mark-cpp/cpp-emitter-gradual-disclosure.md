## CppEmitterGradualDisclosure

### Verification Approach

`CppEmitterGradualDisclosure` is unit-tested in
`test/ApiMark.Cpp.Tests/CppEmitterGradualDisclosureTests.cs` using synthetic namespace/type
structures and an `InMemoryMarkdownWriterFactory` to capture every emitted page.

### Test Environment

No external services, network access, clang installation, or file system access are required.
Tests run with the standard xUnit.net test runner.

### Acceptance Criteria

- The emitter creates the api index, namespace summary, and type pages.
- The emitter creates detail pages for visible members and free functions.
- The emitter creates enum pages, type-alias pages, nested-type pages, and operator pages.
- Case-insensitive collisions are combined onto one page.
- Empty namespace collections still produce an `api.md` fallback page.

### Test Scenarios

**Class operators page**: Tested by `CppEmitterGradualDisclosure_Emit_ClassOperators_CreatesOperatorsPage`.

**Enum page**: Tested by `CppEmitterGradualDisclosure_Emit_Enum_CreatesEnumPage`.

**Type alias page**: Tested by `CppEmitterGradualDisclosure_Emit_TypeAlias_CreatesTypeAliasPage`.

**Nested class page**: Tested by `CppEmitterGradualDisclosure_Emit_NestedClass_CreatesNestedClassPage`.

**Case-insensitive collision**: Tested by
`CppEmitterGradualDisclosure_Emit_CaseInsensitiveCollision_CreatesCombinedPage`.

**Empty namespace fallback**: Tested by
`CppEmitterGradualDisclosure_Emit_EmptyNamespaces_ApiPageContainsFallbackParagraph`.

**Member detail page**: Tested by `CppEmitterGradualDisclosure_Emit_MethodMember_CreatesMemberDetailPage`.

**Free-function page**: Tested by `CppEmitterGradualDisclosure_Emit_FreeFunction_CreatesFreeFunctionPage`.

**Api index page creation**: Verifies that the emitter creates the api index page. Tested by
`CppEmitterGradualDisclosure_Emit_MinimalData_CreatesApiIndexPage`.

**Namespace page creation**: Verifies that a namespace summary page is created for each
namespace. Tested by `CppEmitterGradualDisclosure_Emit_MinimalData_CreatesNamespacePage`.

**Type page creation**: Verifies that a type page is created for each documented class.
Tested by `CppEmitterGradualDisclosure_Emit_MinimalData_CreatesTypePage`.

**Library name heading**: Verifies that the api index page heading contains the library
name. Tested by
`CppEmitterGradualDisclosure_Emit_MinimalData_ApiIndexContainsLibraryNameHeading`.

**Namespace operators page**: Verifies that namespace-level operator overloads are grouped
onto a shared `{namespace}/operators.md` page. Tested by
`CppEmitterGradualDisclosure_Emit_NamespaceOperators_CreatesOperatorsPage`.
