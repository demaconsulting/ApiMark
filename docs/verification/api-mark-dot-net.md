# ApiMarkDotNet

## Verification Approach

ApiMark.DotNet is verified with integration-style tests in `test/ApiMark.DotNet.Tests/` that
exercise the full .NET generation pipeline using real compiled assemblies, XML documentation files,
and Markdown output directories. Mono.Cecil and XML documentation parsing are used as-is so
verification proves the interaction between assembly metadata discovery, type-name simplification,
complexity-rule evaluation, and file emission. Only incidental infrastructure — such as disposable
output locations — is test-controlled; no internal production component is mocked or stubbed.

## Test Environment

Tests require a .NET runtime capable of loading the fixture assemblies, XML documentation files
that match those assemblies, and a writable output directory for generated Markdown. No external
service, network dependency, or machine-specific configuration is required.

## Acceptance Criteria

- All ApiMark.DotNet tests pass with zero failures.
- The generator discovers namespaces, types, and members from representative fixture assemblies.
- Type names and member signatures are simplified into the expected C#-friendly display form.
- Complexity-rule decisions and output files match the documented generation rules.
- Visibility filtering excludes members outside the selected audience.
- Partial XML documentation coverage does not corrupt the output tree.

## Test Scenarios

**Representative assemblies generate the expected namespace and type pages**: Verifies that the
system can walk a sample assembly and emit the expected namespace and type documentation without
skipping discoverable items, confirming that the full generation path from metadata to Markdown
file is wired correctly. This scenario is tested by
`Generate_WithSampleAssembly_WritesExpectedNamespaceAndTypePages`.

**Complex members become dedicated detail pages**: Verifies that constructors, methods, and indexers
meeting the complexity rule are emitted as separate files rather than only table rows, preserving
full detail where required. This scenario is tested by
`Generate_WithComplexMembers_CreatesMemberDetailPages`.

**Visibility filters constrain the published API surface**: Verifies that the system honors the
selected visibility mode so generated output matches the intended audience and excludes hidden
members. This scenario is tested by `Generate_WithVisibilityFilter_ExcludesHiddenMembers`.

**Metadata-only generation remains stable when documentation coverage is partial**: Verifies that
the system still emits structurally correct Markdown when some XML documentation fields are absent,
preventing missing comments from corrupting the output tree. This scenario is tested by
`Generate_WithPartialXmlDocumentation_WritesStableMarkdown`.
