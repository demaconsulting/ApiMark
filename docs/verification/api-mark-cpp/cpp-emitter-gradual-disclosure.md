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
- The api index page heading contains the configured library name.

### Test Scenarios

**Class operators page**: Verifies that a class containing operator overloads produces a shared
`operators.md` page at `{namespace}/{TypeName}/operators` so all operator overloads are navigable
from a single deterministic location. Tested by
`CppEmitterGradualDisclosure_Emit_ClassOperators_CreatesOperatorsPage`.

**Enum page**: Verifies that a namespace-owned enum declaration produces its own detail page at
`{namespace}/{EnumName}` with an H1 heading matching the enum name and a values table listing all
enumerators. Tested by `CppEmitterGradualDisclosure_Emit_Enum_CreatesEnumPage`.

**Type alias page**: Verifies that a namespace-level `using` type alias declaration produces its
own page at `{namespace}/{AliasName}` containing the `using` declaration in a signature block.
Tested by `CppEmitterGradualDisclosure_Emit_TypeAlias_CreatesTypeAliasPage`.

**Nested class page**: Verifies that a class nested inside a public outer class produces its own
page at `{namespace}/{OuterType}/{NestedType}` so readers can navigate to it directly from the
outer class page. Tested by
`CppEmitterGradualDisclosure_Emit_NestedClass_CreatesNestedClassPage`.

**Case-insensitive collision**: Verifies that two members whose names differ only in case are
combined onto a single lowercase-keyed page rather than producing two separately-named pages that
would collide on case-insensitive file systems. Tested by
`CppEmitterGradualDisclosure_Emit_CaseInsensitiveCollision_CreatesCombinedPage`.

**Empty namespace fallback**: Verifies that when the namespace collection is empty the emitter
still produces an `api.md` page containing a fallback paragraph so the output is never completely
empty. Tested by
`CppEmitterGradualDisclosure_Emit_EmptyNamespaces_ApiPageContainsFallbackParagraph`.

**Member detail page**: Verifies that a visible class method produces its own detail page at
`{namespace}/{TypeName}/{MemberName}` with an H1 heading and a signature block, confirming that
the member page emission path is wired correctly. Tested by
`CppEmitterGradualDisclosure_Emit_MethodMember_CreatesMemberDetailPage`.

**Free-function page**: Verifies that a namespace-level free function produces its own page at
`{namespace}/{FunctionName}` with a signature block, confirming that the free-function emission
path is separate from the class-member path. Tested by
`CppEmitterGradualDisclosure_Emit_FreeFunction_CreatesFreeFunctionPage`.

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
