## DotNetGenerator

### Verification Approach

`DotNetGenerator` is integration-tested in `test/ApiMark.DotNet.Tests/` using controlled sample
assemblies and XML documentation fixtures. Mono.Cecil is used as-is because assembly metadata
interpretation is central to the unit's responsibility. Tests constrain inputs so each assertion
isolates one behavior at a time: type discovery, visibility filtering, type-name simplification,
complexity-rule classification, and Markdown file output. An `InMemoryMarkdownWriterFactory` test
double (from `ApiMark.Core.TestHelpers`) is supplied to capture emitted content without writing to
the file system.

### Test Environment

Tests require sample fixture assemblies compiled with XML documentation enabled and a writable test
output folder. The .NET SDK is required to build the fixture assemblies. No external service,
network dependency, or privileged configuration is needed.

### Acceptance Criteria

- All `DotNetGenerator` tests pass with zero failures.
- Type discovery returns the expected namespaces, types, and member sets for fixture assemblies.
- Visibility filtering correctly excludes non-public members when the public visibility mode is
  selected.
- Type-name simplification renders primitives, generics, nullable types, and common collections in
  the expected C# form.
- Complexity-rule evaluation consistently distinguishes simple table-row members from members that
  require a dedicated detail page.
- Generated Markdown content matches expected file names, headings, and signatures.
- Output files follow the naming convention: `api.md` entrypoint, `{Namespace}/{Namespace}.md`
  namespace summaries, `{Namespace}/{TypeName}.md` type pages, and
  `{Namespace}/{TypeName}/{MemberName}.md` complex member pages.
- Obsolete member filtering correctly excludes or includes types and members based on the
  IncludeObsolete option.

### Test Scenarios

**Type discovery finds the expected public API surface**: Verifies that the generator enumerates
the expected namespaces, types, and members from sample assemblies so no documented API surface is
silently missed. This scenario is tested by
`DotNetGenerator_ReadAssembly_WithMonoCecil_ReturnsTypesAndMembers`.

**Constructor accepts assembly and XML paths without error**: Verifies that DotNetGenerator can be
constructed with valid options and that Generate completes without exception, confirming the basic
end-to-end path is functional. This scenario is tested by
`DotNetGenerator_Constructor_AcceptsAssemblyAndXmlPaths`.

**XML documentation comments appear in generated Markdown**: Verifies that summary and remarks text
authored in the XML documentation file are emitted into the Markdown output so generated pages carry
developer-written descriptions. This scenario is tested by
`DotNetGenerator_ReadXmlComments_SummaryAndRemarks_AppearInMarkdown`.

**Visibility filtering excludes and includes members correctly**: Verifies that Public visibility
excludes protected and private members, PublicAndProtected includes protected members, and All
includes private members. This scenario is tested by
`DotNetGenerator_Visibility_PublicPublicAndProtectedAll_FilterExpectedApis`.

**Obsolete toggle controls whether obsolete APIs appear in output**: Verifies that obsolete types
and members are excluded when IncludeObsolete is false and included when it is true. This scenario
is tested by `DotNetGenerator_IncludeObsolete_Toggle_ControlsObsoleteOutput`.

**Error handling rejects a missing XML documentation file**: Verifies that Generate throws
FileNotFoundException when the configured XML documentation path does not exist on disk. This
scenario is tested by `DotNetGenerator_Generate_XmlDocMissing_ThrowsFileNotFoundException`.

**Type-name simplification renders readable C# signatures**: Verifies that primitive aliases,
nullable forms, generic arguments, and common collection types are simplified into compact,
C#-friendly display text. This scenario is tested by
`ApiMarkDotNet_TypeNames_CommonSignatures_RenderReadably`.

**Complexity rules identify members that need detail pages**: Verifies that parameters, exceptions,
examples, and extended remarks trigger dedicated member files while simple members remain inline.
This scenario is tested by
`DotNetGenerator_ComplexityRule_ComplexMembers_GetSeparateFiles`.

**Markdown generation writes expected files and content**: Verifies that generator output includes
expected headings, signatures, and file names for a representative assembly so downstream tools can
consume stable Markdown. This scenario is tested by
`DotNetGenerator_OutputFiles_FollowNamingConvention`.
