## IApiGenerator

### Verification Approach

`IApiGenerator` is a contract interface. Verification confirms that all language-generator
implementations compile and pass type-checking against the interface, and that the contract
behavior is validated directly through `IApiGeneratorTests` using lightweight inline stubs
(`MinimalStubGenerator`, `ConfigurableStubGenerator`, `ApiMdProducingStubGenerator`,
`MultiFileStubGenerator`, `SingleFileStubGenerator`). Each stub exercises a specific aspect
of the interface contract — invocability, construction-time configuration, entrypoint
production, and format selection — without depending on any real language-generator
implementation.

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
`IApiGenerator_Parse_WithMinimalStub_ExecutesSuccessfully`.

**Contract methods are invocable through the interface reference**: Verifies that calling the
`Parse` method through an `IApiGenerator` reference dispatches correctly and does not throw for
the expected inputs, confirming that the interface contract is invocable end-to-end. This scenario
is tested by `ApiMarkCore_GeneratorContract_SupportedLanguage_CanBeInvoked`.

**Implementation retains construction-time configuration during Parse**: Verifies that a
language-generator implementation that stores a configuration value at construction time can
access that value inside `Parse`, confirming that the interface permits construction-time
injection of options. This scenario is tested by
`IApiGenerator_Implementation_UsesConstructionConfiguration`.

**Parse produces the mandatory api.md root entrypoint**: Verifies that a conformant
`IApiGenerator` implementation calls `factory.CreateMarkdown("", "api")` to produce the fixed
top-level entrypoint required by all callers. This scenario is tested by
`IApiGenerator_Emit_OutputDirectory_ContainsApiMd`.

**IApiEmitter.Emit supports configurable output format**: Verifies that when
`EmitConfig.Format` is `GradualDisclosure`, calling `Emit` through the `IApiGenerator`
interface causes the factory to receive more than one `CreateMarkdown` call (multi-file
output), and that when `EmitConfig.Format` is `SingleFile`, only one `CreateMarkdown` call
is made writing `api.md`. This scenario is tested by
`IApiGenerator_Emit_GradualDisclosure_ProducesMultipleFiles` and
`IApiGenerator_Emit_SingleFile_ProducesSingleApiMd`.
