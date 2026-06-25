## DotNetAstModel

### Verification Approach

`DotNetAstModel` is unit-tested by parsing a real fixture assembly with
`DotNetGenerator.Parse` and then inspecting the resulting model properties.
No mocking is required because the data model is a plain value object; the
test simply calls `Parse` to obtain a populated instance. An
`InMemoryMarkdownWriterFactory` is not needed for these tests because no
Markdown output is written during model inspection.

### Test Environment

Tests require the compiled fixture assembly and its XML documentation file.
No external service, network dependency, or privileged configuration is needed.

### Acceptance Criteria

- All `DotNetAstModel` tests pass with zero failures.
- `AllNamespaces` returns namespace names in ordinal alphabetical order.
- `ByNamespace` contains an entry for each namespace present in the fixture assembly.
- `RootNamespaces` is non-empty after parsing.
- `Options` returns the same `DotNetGeneratorOptions` instance passed to the constructor.
- `Assembly` is non-null and has the expected name after parsing.
- `Resolver` is non-null after parsing.
- `XmlDocs` is non-null after parsing.
- `NamespaceDescriptions` is non-null after parsing.
- All collection-type properties (`AllNamespaces`, `ByNamespace`, `RootNamespaces`) expose read-only interfaces.

### Test Scenarios

**AllNamespaces returns namespaces in alphabetical order**: Verifies that
`DotNetAstModel.AllNamespaces` returns all parsed namespaces in ordinal
alphabetical order so downstream pages list namespaces in a stable, deterministic
sequence. This scenario is tested by
`DotNetAstModel_AllNamespaces_ReturnsAlphabeticallySorted`.

**ByNamespace contains the fixture namespace**: Verifies that
`DotNetAstModel.ByNamespace` contains an entry for the fixture namespace after
parsing, confirming that type-lookup by exact namespace name is reliable. This
scenario is tested by `DotNetAstModel_ByNamespace_ContainsFixtureNamespace`.

**RootNamespaces is non-empty**: Verifies that `DotNetAstModel.RootNamespaces`
is populated after parsing the fixture assembly, confirming that the root-namespace
index used when building namespace folder paths is correctly initialized. This
scenario is tested by `DotNetAstModel_RootNamespaces_ContainsFixtureNamespace`.

**Options returns the same instance passed at construction**: Verifies that
`DotNetAstModel.Options` returns the same `DotNetGeneratorOptions` instance that
was passed at construction, confirming that configuration is preserved through the
parse step for use during emission. This scenario is tested by
`DotNetAstModel_Options_ReturnsOptionsPassedAtConstruction`.

**Assembly property returns the loaded assembly**: Verifies that
`DotNetAstModel.Assembly` is non-null and has the expected name after parsing,
confirming that the Mono.Cecil `AssemblyDefinition` is retained in the model for
member iteration during emission. This scenario is tested by
`DotNetAstModel_Assembly_ReturnsLoadedAssembly`.

**Resolver property is non-null**: Verifies that `DotNetAstModel.Resolver` is
non-null after parsing, confirming that the type-reference resolver used during
link generation is initialized as part of the model. This scenario is tested by
`DotNetAstModel_Resolver_AfterParse_IsNotNull`.

**XmlDocs property is non-null**: Verifies that `DotNetAstModel.XmlDocs` is
non-null after construction, confirming that the XML documentation reader is
retained in the model for per-member lookups during emission. This scenario is
tested by `DotNetAstModel_XmlDocs_AfterParse_IsNotNull`.

**NamespaceDescriptions property is non-null**: Verifies that
`DotNetAstModel.NamespaceDescriptions` is non-null after construction, confirming
that the namespace description map (populated from NamespaceDoc carriers) is
initialized in the model even when no carriers are present. This scenario is
tested by `DotNetAstModel_NamespaceDescriptions_AfterParse_IsNotNull`.

**All collection-type properties expose read-only interfaces**: Verifies that
`AllNamespaces`, `ByNamespace`, and `RootNamespaces` properties all return
`IReadOnly*` interface types, confirmed by the C# compiler type system — no runtime
test is required for the static guarantee. Supplementary runtime test:
`DotNetAstModel_Collections_ExposeReadOnlyInterfaces` asserts that the runtime
types satisfy the interface constraints by calling `Assert.IsAssignableFrom` on
each collection property.
