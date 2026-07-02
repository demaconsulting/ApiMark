## DotNetEmitterGradualDisclosure

### Verification Approach

`DotNetEmitterGradualDisclosure` is integration-tested by parsing a fixture
assembly and calling `Emit` with `OutputFormat.GradualDisclosure` and an
`InMemoryMarkdownWriterFactory`. Tests inspect the set of created writer keys
and the content written to specific pages. No internal production components
are mocked beyond the in-memory factory.

### Test Environment

Tests require the compiled fixture assembly, its XML documentation file, and
the `InMemoryMarkdownWriterFactory` from `ApiMark.Core.TestHelpers`. No external
service or network dependency is needed.

### Acceptance Criteria

- All `DotNetEmitterGradualDisclosure` tests pass with zero failures.
- The api index page is created with the expected assembly name heading.
- A namespace summary page is created for each namespace in the fixture assembly.
- A type page is created for each visible type in each namespace.
- At least one member detail page is created for a visible member of a visible type.
- A single combined page is created for members whose sanitized file names collide on a
  case-insensitive filesystem.
- A type with pure method overloads produces a consolidated method overload page.
- A type with operator overloads produces an `operators.md` page.
- A type with a nested type produces a dedicated page under the containing type's folder.
- When a namespace has NamespaceDoc remarks and example parts, they are emitted on the namespace page after the summary (remarks as a paragraph, example code as a fenced code block).
- A type whose `<remarks>` contains a `<list type="number">` renders the list as ordered Markdown items on the type page.

### Test Scenarios

**Api index page is created**: Verifies that the gradual-disclosure emitter
creates the `api` writer key, confirming that the top-level assembly entrypoint
is emitted as the first page in the output tree. This scenario is tested by
`DotNetEmitterGradualDisclosure_Emit_ValidModel_CreatesApiIndexPage`.

**Api index heading contains the assembly name**: Verifies that the api index
page includes a heading containing the fixture assembly name. This scenario is
tested by `DotNetEmitterGradualDisclosure_Emit_ValidModel_ApiIndexContainsAssemblyNameHeading`.

**Namespace page is created for the fixture namespace**: Verifies that a writer
whose key contains the fixture namespace name is created. This scenario is tested
by `DotNetEmitterGradualDisclosure_Emit_ValidModel_CreatesNamespacePage`.

**NamespaceDoc remarks appear on the namespace page**: Verifies that a namespace
carrying a NamespaceDoc carrier with `<remarks>` has that remarks text emitted as a
paragraph on the namespace page after the summary. This scenario is tested by
`DotNetEmitterGradualDisclosure_Emit_NamespaceWithDoc_EmitsNamespaceRemarks`.

**NamespaceDoc example is emitted as a code block on the namespace page**: Verifies
that a namespace carrying a NamespaceDoc carrier with `<example><code>` has that
example emitted as a fenced code block on the namespace page. This scenario is tested
by `DotNetEmitterGradualDisclosure_Emit_NamespaceWithDoc_EmitsNamespaceExampleCodeBlock`.

**Remarks numbered list renders as Markdown on the type page**: Verifies that a type
whose `<remarks>` contains a `<list type="number">` renders the list as `1. item`
ordered lines on its type page. This scenario is tested by
`DotNetEmitterGradualDisclosure_Emit_TypeWithListRemarks_RendersNumberedListInMarkdown`.

**Type page is created for SampleClass**: Verifies that a writer whose key
contains `SampleClass` is created, confirming that per-type pages are emitted for
all visible types. This scenario is tested by
`DotNetEmitterGradualDisclosure_Emit_ValidModel_CreatesTypePage`.

**Member detail page is created for SampleClass.Reset**: Verifies that a writer
whose key contains both `SampleClass` and `Reset` is created, confirming that
per-member detail pages are emitted for all visible members. This scenario is
tested by `DotNetEmitterGradualDisclosure_Emit_ValidModel_CreatesMemberDetailPage`.

**Combined collision page is created for CaseCollisionClass**: Verifies that when
`CaseCollisionClass` has members whose sanitized names differ only in case (`name`
and `Name`), the emitter creates a single combined page keyed by the lower-invariant
name rather than two separate colliding pages. This scenario is tested by
`DotNetEmitterGradualDisclosure_Emit_CaseCollision_CreatesCombinedPage`.

**Method overload page is created for overloaded methods**: Verifies that a type
with multiple overloads of the same method name produces a consolidated overload
page rather than separate pages per overload. This scenario is tested by
`DotNetEmitterGradualDisclosure_Emit_ValidModel_CreatesMethodOverloadPage`.

**Operators page is created for types with operator overloads**: Verifies that a
type defining operator overloads produces an `operators.md` page under the type
folder. This scenario is tested by
`DotNetEmitterGradualDisclosure_Emit_ValidModel_CreatesOperatorsPage`.

**Nested type page is created under the containing type's folder**: Verifies that
a type containing a nested type produces a dedicated page for that nested type
placed under the containing type's folder path. This scenario is tested by
`DotNetEmitterGradualDisclosure_Emit_ValidModel_CreatesNestedTypePage`.

**Child namespace page is created**: Verifies that a child namespace (such as
`ApiMark.DotNet.Fixtures.Inner`) also produces a dedicated Markdown summary page,
confirming that child namespace enumeration works correctly. This scenario is tested by
`DotNetEmitterGradualDisclosure_Emit_ValidModel_CreatesChildNamespacePage`.
