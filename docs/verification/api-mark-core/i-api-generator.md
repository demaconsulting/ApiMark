## IApiGenerator

### Verification Approach

`IApiGenerator` is a contract interface. Verification confirms that all language-generator
implementations compile and pass type-checking against the interface, and that the contract
behavior is validated indirectly through `DotNetGenerator` tests: if the interface signature
is wrong, the consuming implementation fails to compile and integration tests cannot run. No
additional mocking is required because `IApiGenerator` carries no logic of its own.

### Test Environment

N/A — standard test environment using the .NET test runner is sufficient for IApiGenerator
verification. Interface contract compliance is enforced at compile time.

### Acceptance Criteria

- All `IApiGenerator` contract tests pass with zero failures.
- Every language-generator implementation compiles against the `IApiGenerator` interface.
- The interface methods accept the documented input parameters and return the documented result
  types without runtime errors.

### Test Scenarios

**All generator implementations satisfy the IApiGenerator contract**: Verifies that each
language-generator class compiles against the `IApiGenerator` interface and can be assigned to an
`IApiGenerator` reference without a cast, proving the contract is fulfilled and usable by host
tooling without knowing the concrete type. This scenario is tested by
`IApiGenerator_Generate_WithMinimalStub_ExecutesSuccessfully`.

**Contract methods are invocable through the interface reference**: Verifies that calling the
`Generate` method through an `IApiGenerator` reference dispatches correctly and does not throw for
the expected inputs, confirming that the interface contract is invocable end-to-end. This scenario
is tested by `ApiMarkCore_GeneratorContract_SupportedLanguage_CanBeInvoked`.

**Implementation retains construction-time configuration during Generate**: Verifies that a
language-generator implementation that stores a configuration value at construction time can
access that value inside `Generate`, confirming that the interface permits construction-time
injection of options. This scenario is tested by
`IApiGenerator_Implementation_UsesConstructionConfiguration`.

**Generate produces the mandatory api.md root entrypoint**: Verifies that a conformant
`IApiGenerator` implementation calls `factory.CreateMarkdown("", "api")` to produce the fixed
top-level entrypoint required by all callers. This scenario is tested by
`IApiGenerator_Generate_OutputDirectory_ContainsApiMd`.
