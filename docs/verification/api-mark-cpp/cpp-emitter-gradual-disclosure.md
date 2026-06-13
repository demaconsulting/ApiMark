## CppEmitterGradualDisclosure

### Verification Approach

`CppEmitterGradualDisclosure` is unit-tested in
`test/ApiMark.Cpp.Tests/CppEmitterGradualDisclosureTests.cs` without invoking clang.
A `BuildMinimalData` helper constructs a `CppEmitter` and a controlled namespace
declaration containing one class (`Widget`), along with a `CppTypeLinkResolver`.
An `InMemoryMarkdownWriterFactory` test double captures all created writers and their
content. Tests cover the creation and content of the api index page, namespace page,
and type page using this minimal synthetic dataset.

### Test Environment

No external services, network access, clang installation, or file system access are
required. Tests run with the standard xUnit.net test runner.

### Acceptance Criteria

- `CppEmitterGradualDisclosure.Emit` creates a writer keyed as `("", "api")` for
  the api index page.
- The api index page heading contains the configured library name (`"TestLib"`).
- A namespace page is created for the `"testlib"` namespace key.
- A type page is created for the `Widget` class.

### Test Scenarios

**Emit creates the api index page**: Verifies that `CppEmitterGradualDisclosure.Emit`
produces a writer keyed as `("", "api")` in the factory, confirming that the api
index page generation path is wired correctly.
This scenario is tested by `CppEmitterGradualDisclosure_Emit_MinimalData_CreatesApiIndexPage`.

**Emit creates a namespace page**: Verifies that the emitter produces at least one
writer whose key contains `"testlib"`, confirming that per-namespace page generation
is wired correctly.
This scenario is tested by `CppEmitterGradualDisclosure_Emit_MinimalData_CreatesNamespacePage`.

**Emit creates a type page for Widget**: Verifies that the emitter produces at least
one writer whose key contains `"Widget"`, confirming that per-type page generation is
wired correctly.
This scenario is tested by `CppEmitterGradualDisclosure_Emit_MinimalData_CreatesTypePage`.

**Api index page heading contains library name**: Verifies that the api index page
produced by the emitter contains a heading whose text includes `"TestLib"`, confirming
that the library name from `CppGeneratorOptions` is correctly used in the top-level
heading.
This scenario is tested by
`CppEmitterGradualDisclosure_Emit_MinimalData_ApiIndexContainsLibraryNameHeading`.
