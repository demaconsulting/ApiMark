## DotNetEmitter

### Verification Approach

`DotNetEmitter` is unit-tested in `test/ApiMark.DotNet.Tests/` by parsing a
real fixture assembly and then calling `Emit` with an
`InMemoryMarkdownWriterFactory`. Tests verify format dispatch (gradual-disclosure
vs. single-file), null-factory rejection, and namespace path computation. No
internal production components are mocked beyond the in-memory factory.

### Test Environment

Tests require the compiled fixture assembly, its XML documentation file, and
the `InMemoryMarkdownWriterFactory` from `ApiMark.Core.TestHelpers`. No external
service or network dependency is needed.

### Acceptance Criteria

- All `DotNetEmitter` tests pass with zero failures.
- Passing a null factory to `Emit` throws `ArgumentNullException` before any I/O.
- Passing a null config to `Emit` throws `ArgumentNullException` before any I/O.
- Passing a null context to `Emit` throws `ArgumentNullException` before any I/O.
- `OutputFormat.GradualDisclosure` produces more than one Markdown writer.
- `OutputFormat.SingleFile` produces exactly one writer keyed `api`.
- `GetNamespaceFolderPath` returns the full dotted name for a root namespace and
  a slash-separated path for a child namespace.
- `GetNamespaceFolderPath` returns the full dotted name when no root namespace matches.
- `BuildTypeSignature` includes the `abstract` modifier for abstract non-sealed classes.
- `BuildTypeSignature` includes the `sealed` modifier for sealed non-abstract classes.
- `BuildTypeSignature` includes the `static` modifier for static classes (abstract and sealed in IL).
- `IsNamespaceDocCarrier` returns `true` for a class named `NamespaceDoc` that is internal and static.
- `IsNamespaceDocCarrier` returns `false` for a regular public class.
- `BuildPropertyAccessors` does not prefix accessors that share the property's declared
  accessibility (e.g., a protected property with protected get and set renders as
  `protected { get; set; }` without redundant prefixes).
- `BuildPropertyAccessors` emits `init;` for init-only (C# 9+) property setters.
- `ToXmlDocTypeName` converts Cecil generic type names (e.g. `List\`1`) to XML doc member-ID format (e.g.`List{T}`) so XML doc lookups use the correct key format.

### Test Scenarios

**Null factory throws ArgumentNullException**: Verifies that calling
`DotNetEmitter.Emit` with a null factory throws `ArgumentNullException` before
any I/O is attempted. This scenario is tested by
`DotNetEmitter_Emit_NullFactory_ThrowsArgumentNullException`.

**Null config throws ArgumentNullException**: Verifies that calling
`DotNetEmitter.Emit` with a null config throws `ArgumentNullException` before
any I/O is attempted. This scenario is tested by
`DotNetEmitter_Emit_NullConfig_ThrowsArgumentNullException`.

**Null context throws ArgumentNullException**: Verifies that calling
`DotNetEmitter.Emit` with a null context throws `ArgumentNullException` before
any I/O is attempted. This scenario is tested by
`DotNetEmitter_Emit_NullContext_ThrowsArgumentNullException`.

**ToXmlDocTypeName converts Cecil generic notation**: Verifies that
`DotNetEmitter.ToXmlDocTypeName` converts Cecil-encoded generic instantiations
(using angle brackets) to the XML doc ID encoding (using curly braces), and that
nested-type separators are normalized from `/` to `.`. This scenario is tested by
`DotNetEmitter_ToXmlDocTypeName_ConvertsGenericNotation`.

**GradualDisclosure format produces multiple files**: Verifies that when
`OutputFormat.GradualDisclosure` is configured the emitter produces more than one
Markdown writer. This scenario is tested by
`DotNetEmitter_Emit_GradualDisclosureFormat_ProducesMultipleFiles`.

**SingleFile format produces exactly one api file**: Verifies that when
`OutputFormat.SingleFile` is configured the emitter produces exactly one writer
keyed `api`. This scenario is tested by
`DotNetEmitter_Emit_SingleFileFormat_ProducesSingleApiFile`.

**GetNamespaceFolderPath returns the dotted name for a root namespace**: Verifies
that a namespace that is itself a configured root namespace returns its full dotted
name as the folder path. This scenario is tested by
`DotNetEmitter_GetNamespaceFolderPath_RootNamespace_ReturnsDottedName`.

**GetNamespaceFolderPath returns a slash-separated path for a child namespace**:
Verifies that a namespace that is a child of a configured root returns a
slash-separated path. This scenario is tested by
`DotNetEmitter_GetNamespaceFolderPath_ChildNamespace_ReturnsSlashSeparated`.

**GetNamespaceFolderPath returns the full name for an unknown namespace**: Verifies
that a namespace that matches no configured root returns its full dotted name as a
safe fallback. This scenario is tested by
`DotNetEmitter_GetNamespaceFolderPath_UnknownNamespace_ReturnsFullName`.

**Abstract class type signature contains abstract modifier**: Verifies that
`DotNetEmitter.BuildTypeSignature` includes the `abstract` keyword for a class that
is abstract but not sealed, so that readers can see the correct modifier at a glance.
This scenario is tested by
`DotNetEmitter_BuildTypeSignature_AbstractClass_ContainsAbstractModifier`.

**Sealed class type signature contains sealed modifier**: Verifies that
`DotNetEmitter.BuildTypeSignature` includes the `sealed` keyword for a class that is
sealed but not abstract, so the modifier is visible in generated documentation. This
scenario is tested by
`DotNetEmitter_BuildTypeSignature_SealedClass_ContainsSealedModifier`.

**Static class type signature contains static modifier**: Verifies that
`DotNetEmitter.BuildTypeSignature` includes the `static` keyword for a static class
(which compiles to abstract+sealed in IL), so the static nature of the class is
accurately reflected in generated documentation. This scenario is tested by
`DotNetEmitter_BuildTypeSignature_StaticClass_ContainsStaticModifier`.

**IsNamespaceDocCarrier returns true for NamespaceDoc class**: Verifies that
`DotNetEmitter.IsNamespaceDocCarrier` correctly identifies an `internal static class
NamespaceDoc` as a carrier type so it can be excluded from type listings and its
summary can be promoted to the namespace description. This scenario is tested by
`DotNetEmitter_IsNamespaceDocCarrier_NamespaceDocClass_ReturnsTrue`.

**IsNamespaceDocCarrier returns false for a regular class**: Verifies that
`DotNetEmitter.IsNamespaceDocCarrier` does not falsely classify a regular public
class as a carrier type. This scenario is tested by
`DotNetEmitter_IsNamespaceDocCarrier_RegularClass_ReturnsFalse`.

**Init-only property accessor renders as `init;`**: Verifies that `BuildPropertyAccessors` emits
`init;` rather than `set;` for properties declared with the C# 9 `init` accessor keyword, so
generated signatures correctly distinguish init-only properties (used by records and immutable types)
from regular settable properties. This scenario is tested by
`DotNetEmitter_BuildPropertyAccessors_InitOnlySetter_EmitsInit`.

**Shared-accessibility property accessors are not prefixed**: Verifies that `BuildPropertyAccessors`
returns `"get; set;"` (without a prefix) for a protected property whose get and set accessors are
both protected, confirming that redundant accessor prefixes are suppressed when they match the
property's declared accessibility. This scenario is tested by
`DotNetEmitter_BuildPropertyAccessors_ProtectedProperty_DoesNotPrefixAccessors`.
