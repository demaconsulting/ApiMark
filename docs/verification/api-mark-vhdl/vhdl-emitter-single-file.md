## VhdlEmitterSingleFile

### Verification Approach

`VhdlEmitterSingleFile` is verified with unit tests in
`test/ApiMark.Vhdl.Tests/VhdlEmitterSingleFileTests.cs` using in-memory test doubles.
A minimal `VhdlFileModel` is constructed directly without invoking the parser, and an
`InMemoryMarkdownWriterFactory` captures emitted output. Tests confirm that output is
consolidated into a single file and that the file is keyed correctly.

### Test Environment

Standard .NET test runner (`dotnet test`). No external tools, services, or privileged
configuration are required.

### Acceptance Criteria

- `VhdlEmitterSingleFile.Emit` creates exactly one writer in the writer factory output,
  confirming that all content is consolidated into a single Markdown file.
- The single output file is keyed as `"api"`, confirming it is the expected api file.
- Both entities and packages appear in the single api file output.
- Architecture bodies are rendered inline within their entity section.
- Subprogram sections include a Signature heading and an optional Parameters table.

### Test Scenarios

**Creates exactly one writer**: Verifies that emitting minimal VHDL model data results in
exactly one writer being created by the factory, confirming that the single-file emitter does
not produce per-entity files.
This scenario is tested by `VhdlEmitterSingleFile_Emit_MinimalData_CreatesExactlyOneWriter`.

**Creates api file only**: Verifies that the single writer created is keyed as `"api"`,
confirming that the output file name matches the expected api entrypoint.
This scenario is tested by `VhdlEmitterSingleFile_Emit_MinimalData_CreatesApiFileOnly`.

**Two entities both appear in output**: Verifies that when two entities are present, both
entity headings appear in the single api file.
This scenario is tested by `VhdlEmitterSingleFile_Emit_TwoEntities_BothAppearInOutput`.

**Package with types emits Types section**: Verifies that a package containing type
declarations produces a Types heading and paragraph-per-type format in the api file.
This scenario is tested by `VhdlEmitterSingleFile_Emit_PackageWithTypes_EmitsTypesSection`.

**Package with subprograms has no kind attribution paragraph**: Verifies that the subprogram
section does not contain a standalone italic kind paragraph, as the kind is visible from the
Signature.
This scenario is tested by
`VhdlEmitterSingleFile_Emit_PackageWithSubprograms_NoKindAttributionParagraph`.

**Package with subprograms contains Signature heading**: Verifies that the subprogram section
contains a Signature heading for the fenced VHDL code block.
This scenario is tested by
`VhdlEmitterSingleFile_Emit_PackageWithSubprograms_SubprogramSectionContainsSignatureHeading`.

**Entity with architecture produces Architectures section**: Verifies that an entity with an
associated architecture body produces an Architectures sub-section in the api file.
This scenario is tested by
`VhdlEmitterSingleFile_Emit_EntityWithArchitecture_ArchitectureSectionAppearsInOutput`.

**Architecture paragraph contains filename**: Verifies that the architecture entry paragraph
contains both the bold architecture name and the source filename in backticks.
This scenario is tested by
`VhdlEmitterSingleFile_Emit_EntityWithArchitecture_ArchitectureParagraphContainsFilename`.

**Entity with no generics emits Generics heading**: Verifies that the Generics heading is
always written even when an entity has no generic declarations.
This scenario is tested by
`VhdlEmitterSingleFile_Emit_EntityWithNoGenerics_EmitsGenericsHeading`.

**Entity with no generics emits none-placeholder**: Verifies that the Generics section
contains the `NoItemsPlaceholder` paragraph when no generics are present.
This scenario is tested by
`VhdlEmitterSingleFile_Emit_EntityWithNoGenerics_EmitsNonePlaceholderInGenericsSection`.

**Entity section contains attribution paragraph**: Verifies that the entity section includes
an attribution paragraph naming the source file.
This scenario is tested by
`VhdlEmitterSingleFile_Emit_Entity_SectionContainsEntityAttributionParagraph`.

**Package section contains attribution paragraph**: Verifies that the package section includes
an attribution paragraph naming the source file.
This scenario is tested by
`VhdlEmitterSingleFile_Emit_Package_SectionContainsPackageAttributionParagraph`.

**Subprogram with parameters emits Parameters heading**: Verifies that a subprogram with
formal parameters produces a Parameters heading and table.
This scenario is tested by
`VhdlEmitterSingleFile_Emit_SubprogramWithParameters_EmitsParametersHeading`.

**Function subprogram emits Returns heading**: Verifies that a function subprogram produces
a Returns heading.
This scenario is tested by
`VhdlEmitterSingleFile_Emit_FunctionSubprogram_EmitsReturnsHeading`.

**Parameters table has correct headers**: Verifies that the parameters table headers are
Name, Type, Description (no Mode column).
This scenario is tested by
`VhdlEmitterSingleFile_Emit_SubprogramWithParameters_ParametersTableHasCorrectHeaders`.

**Plain parameter type cell is bare type name**: Verifies that a parameter with empty mode
shows only the bare type name without a direction prefix.
This scenario is tested by
`VhdlEmitterSingleFile_Emit_SubprogramWithPlainParameter_TypeCellIsBareTypeName`.

**Directed parameter type cell is prefixed with direction**: Verifies that a parameter with
an explicit direction has the direction keyword prepended to the type name in the Type cell.
This scenario is tested by
`VhdlEmitterSingleFile_Emit_SubprogramWithDirectedParameter_TypeCellPrefixedWithDirection`.
