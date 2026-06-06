## Mono.Cecil

### Verification Approach

Mono.Cecil is verified in ApiMark through integration tests in `test/ApiMark.DotNet.Tests/` that
exercise the assembly-reading and metadata APIs used by `ApiMark.DotNet`. The verification focus is
the subset of capabilities needed by the product: opening managed assemblies without loading them
for execution, enumerating namespaces, types, and members, reading signatures and accessibility, and
exposing metadata needed for name simplification and member detail page generation. Evidence is collected
from automated tests that compare generated documentation behavior against representative sample
assemblies.

### Test Scenarios

**Assembly metadata discovery returns the expected public API surface**: Verifies that Mono.Cecil
can read fixture assemblies and expose the namespaces, types, and members required for
documentation generation. This scenario is tested by
`DotNetGenerator_ReadAssembly_WithMonoCecil_ReturnsTypesAndMembers`.

**Generic and nullable signatures are preserved accurately**: Verifies that the metadata exposed by
Mono.Cecil is rich enough for ApiMark to render generic arguments, nullable value types, and
nullable reference annotations in the expected display form. This scenario is tested by
`TypeNameSimplifier_GenericArguments_AreSimplifiedRecursively`,
`TypeNameSimplifier_NullableValueTypes_UseQuestionMarkForm`, and
`TypeNameSimplifier_Simplify_NullableAnnotatedReferenceType_AppendsQuestionMark`.

**Attributes and documentation-related metadata remain accessible**: Verifies that obsolete markers
and related member metadata can be read so ApiMark can apply inclusion and presentation rules
correctly. This scenario is tested by
`DotNetGenerator_IncludeObsolete_Toggle_ControlsObsoleteOutput`.

**Member metadata is sufficient for dedicated detail page generation**: Verifies that parameters,
exceptions, and member shape information exposed through Mono.Cecil are sufficient for DotNetGenerator
to produce a dedicated detail page for every visible member. This scenario is tested by
`DotNetGenerator_AllMembers_GetSeparateFiles`.
