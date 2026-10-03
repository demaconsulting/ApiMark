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
- `ToXmlDocTypeName` converts Cecil byref type names (trailing `&`, used for `ref`/`out`/`in` parameters) to XML doc member-ID format (trailing `@`) so XML doc lookups use the correct key format.
- `IsImplicitDefaultConstructor` returns `true` for a compiler-synthesized implicit default
  constructor (with or without field/property initializers) and `false` for an explicit
  parameterless constructor (empty, expression-bodied, or otherwise) and for a constructor
  that takes a parameter.
- `IsImplicitDefaultConstructor` does not throw when a constructor stores a field whose
  declaring type is unresolvable (e.g. an external assembly not supplied via
  `ReferencePaths`), and classifies such a constructor as not implicit.
- `IsImplicitDefaultConstructor` returns `false` for a bodyless constructor (e.g. one whose
  implementation attributes mark it `InternalCall`, simulating a metadata-only/reference-
  assembly stub or an `extern`/P/Invoke-declared constructor), regardless of its sequence-point
  data.
- `BuildFieldSignature` appends `" = " + FormatConstantValue(field.Constant)` to the signature
  for a field with `HasConstant == true`, including enum members rendered via their underlying
  numeric constant.

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

**ToXmlDocTypeName converts Cecil-encoded type names to XML doc IDs**: Verifies
that `DotNetEmitter.ToXmlDocTypeName` converts Cecil-encoded generic
instantiations (using angle brackets) to the XML doc ID encoding (using curly
braces), normalizes nested-type separators from `/` to `.`, and converts
byref parameter types (trailing `&`, used for `ref`/`out`/`in` parameters) to
the XML doc ID trailing `@` encoding. This scenario is tested by
`DotNetEmitter_ToXmlDocTypeName_ConvertsCecilEncodingToXmlDocId`.

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

**IsImplicitDefaultConstructor distinguishes implicit from explicit constructors**: Verifies that
`DotNetEmitter.IsImplicitDefaultConstructor` returns `true` for a compiler-synthesized implicit
default constructor — both with no field/property initializers and with multiple initializers,
whose sequence points would otherwise be mistaken for the constructor's own body — and returns
`false` for an explicit constructor that takes a parameter, an explicit empty parameterless
constructor, an explicit expression-bodied parameterless constructor whose entire body is a
single field assignment, an explicit expression-bodied parameterless constructor whose
single source statement stores more than one field under one sequence point (a
tuple-deconstruction assignment), and an explicit constructor that stores a `protected` field
inherited from a resolvable external base class. This scenario is tested by
`DotNetEmitter_IsImplicitDefaultConstructor_DistinguishesImplicitFromExplicit`.

**IsImplicitDefaultConstructor does not throw for an unresolvable external field**: Verifies
that `DotNetEmitter.IsImplicitDefaultConstructor` does not propagate a
`Mono.Cecil.AssemblyResolutionException` when a constructor's field store targets a field
whose declaring type cannot be resolved (simulated by splicing a synthetic `stfld` instruction,
referencing a fabricated nonexistent assembly, into a real constructor's IL body), and instead
treats the store as not qualifying and classifies the constructor as not implicit. This
scenario is tested by
`DotNetEmitter_IsImplicitDefaultConstructor_UnresolvableExternalFieldDoesNotThrow`.

**IsImplicitDefaultConstructor returns false for a bodyless constructor**: Verifies that
`DotNetEmitter.IsImplicitDefaultConstructor` returns `false` for a constructor whose
implementation attributes are flipped to `InternalCall` (so `MethodDefinition.HasBody` is
`false` and it reports zero sequence points), reproducing a metadata-only/reference-assembly
stub or an `extern`/P/Invoke-declared constructor — scenarios that cannot be the
compiler-synthesized implicit default constructor, since that constructor always has an IL
body. This scenario is tested by
`DotNetEmitter_IsImplicitDefaultConstructor_BodylessConstructor_ReturnsFalse`.

**BuildFieldSignature appends a quoted string constant value**: Verifies that a `const
string` field's signature line appends `= "value"` with the value double-quoted. This
scenario is tested by `DotNetEmitter_BuildFieldSignature_ConstStringField_AppendsQuotedValue`.

**BuildFieldSignature appends a numeric constant value**: Verifies that a `const int`
field's signature line appends `= <number>` as plain numeric text. This scenario is tested
by `DotNetEmitter_BuildFieldSignature_ConstIntField_AppendsNumericValue`.

**BuildFieldSignature appends a lowercase boolean constant value**: Verifies that a `const
bool` field's signature line appends `= true` or `= false` in lowercase. This scenario is
tested by `DotNetEmitter_BuildFieldSignature_ConstBoolField_AppendsLowercaseBooleanValue`.

**BuildFieldSignature escapes a NUL char constant value**: Verifies that a `const char`
field initialized to `'\0'` has its signature line append the escaped `'\0'` display form
rather than an unprintable raw character. This scenario is tested by
`DotNetEmitter_BuildFieldSignature_ConstCharField_EscapesNulCharacter`.

**FormatConstantValue escapes all remaining control characters in a string constant**:
Verifies that a string constant containing bell, backspace, form-feed, and vertical-tab
control characters has each one rendered as its named C# short escape (`\a`, `\b`, `\f`,
`\v`) rather than as a raw control byte. This scenario is tested by
`DotNetEmitter_FormatConstantValue_StringWithControlCharacters_EscapesAllOfThem`.

**FormatConstantValue falls back to a Unicode escape for an unnamed control character**:
Verifies that a control character with no named C# short escape (e.g. `DEL`, `0x7f`) is
rendered as a `\uXXXX` escape rather than as a raw control byte. This scenario is tested by
`DotNetEmitter_FormatConstantValue_UnnamedControlCharacter_FallsBackToUnicodeEscape`.

**BuildFieldSignature appends an enum member's underlying numeric value**: Verifies that an
enum member field with an explicit numeric value has its signature line append that value
via the same `FormatConstantValue` path used for plain `const` fields, since Mono.Cecil
stores enum member constants using the enum's underlying primitive type. This scenario is
tested by
`DotNetEmitter_BuildFieldSignature_EnumMemberWithExplicitValue_AppendsUnderlyingNumericValue`.
