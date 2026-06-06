# ApiMarkDotNet

## Verification Approach

ApiMark.DotNet is verified with integration-style tests in `test/ApiMark.DotNet.Tests/` that
exercise the full .NET generation pipeline using real compiled assemblies, XML documentation files,
and Markdown output directories. Mono.Cecil and XML documentation parsing are used as-is so
verification proves the interaction between assembly metadata discovery, type-name simplification,
and file emission. Only incidental infrastructure — such as disposable
output locations — is test-controlled; no internal production component is mocked or stubbed.

## Test Environment

Tests require a .NET runtime capable of loading the fixture assemblies, XML documentation files
that match those assemblies, and a writable output directory for generated Markdown. No external
service, network dependency, or machine-specific configuration is required.

## Acceptance Criteria

- All ApiMark.DotNet tests pass with zero failures.
- The generator discovers namespaces, types, and members from representative fixture assemblies.
- Type names and member signatures are simplified into the expected C#-friendly display form.
- Every visible member receives its own dedicated detail page and is linked from the type page.
- Visibility filtering excludes members outside the selected audience.
- Obsolete member filtering correctly excludes or includes deprecated APIs based on the IncludeObsolete option.

## Test Scenarios

**Representative assemblies generate the expected namespace and type pages**: Verifies that the
system can walk a sample assembly and emit the expected namespace and type documentation without
skipping discoverable items, confirming that the full generation path from metadata to Markdown
file is wired correctly. This scenario is tested by
`ApiMarkDotNet_Generate_ValidAssemblyAndXml_ProducesMarkdown`.

**All members receive dedicated detail pages**: Verifies that every visible member —
regardless of parameters or documentation content — is emitted as a separate file and linked
from its parent type page, making navigation fully deterministic. This scenario is tested by
`DotNetGenerator_AllMembers_GetSeparateFiles`.

**Visibility filters constrain the published API surface**: Verifies that the system honors the
selected visibility mode so generated output matches the intended audience and excludes hidden
members. This scenario is tested by `DotNetGenerator_Visibility_PublicPublicAndProtectedAll_FilterExpectedApis`.

**XML documentation content appears correctly in generated Markdown**: Verifies that the system
includes XML documentation content in the generated output and correctly handles assembly
documentation data during the generation pipeline. This scenario is tested by
`DotNetGenerator_ReadXmlComments_SummaryAndRemarks_AppearInMarkdown`.

**Obsolete member toggle controls whether deprecated APIs appear in output**: Verifies that setting
IncludeObsolete to false excludes obsolete types from generated output while setting it to true
includes them, confirming the option is correctly honoured end-to-end. This scenario is tested by
`DotNetGenerator_IncludeObsolete_Toggle_ControlsObsoleteOutput`.
