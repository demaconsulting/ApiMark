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

N/A - standard test environment using the .NET test runner is sufficient for IApiGenerator
verification. Interface contract compliance is enforced at compile time.

### Acceptance Criteria

- All `IApiGenerator` contract tests pass with zero failures.
- Every language-generator implementation compiles against the `IApiGenerator` interface.
- The interface methods accept the documented input parameters and return the documented result
  types without runtime errors.

### Test Scenarios

**All generator implementations satisfy the IApiGenerator contract**: Verifies that each
language-generator class compiles against the `IApiGenerator` interface and can be assigned to an
`IApiGenerator` reference without a cast, and that `Parse` accepts an `IContext` parameter as the
diagnostic channel without throwing. This scenario is tested by
`IApiGenerator_Parse_WithMinimalStub_ExecutesSuccessfully`.

**Parse returns an IApiEmitter that is callable through the interface reference**: Verifies that
calling `Parse` through an `IApiGenerator` reference returns an `IApiEmitter` that can subsequently
be invoked with `Emit`, confirming that the full dispatch path from `Parse` to `Emit` is
end-to-end callable. This scenario is tested by
`ApiMarkCore_GeneratorContract_SupportedLanguage_CanBeInvoked`.

**Implementation retains construction-time configuration during Parse**: Verifies that a
language-generator implementation that stores a configuration value at construction time can
access that value inside `Parse`, confirming that the interface permits construction-time
injection of options. This scenario is tested by
`IApiGenerator_Implementation_UsesConstructionConfiguration`.

**Format-selection and api.md entrypoint are responsibilities of IApiEmitter**: The format-selection
contract (GradualDisclosure produces multiple files; SingleFile produces one api.md) and the
mandatory `api.md` entrypoint production belong at the `IApiEmitter` level and are verified in
`docs/verification/api-mark-core/i-api-emitter.md`.
`IApiGenerator` implementations delegate both concerns to their returned `IApiEmitter`.

**Parse rejects null context**: Verifies that an `IApiGenerator` implementation throws
`ArgumentNullException` when `null` is passed as the context argument to `Parse`, confirming
the null-precondition contract at the interface level. The test uses a minimal stub generator
that calls `ArgumentNullException.ThrowIfNull(context)` before any other work to document
the expected behavior. Tested by `IApiGenerator_Parse_NullContext_ThrowsArgumentNullException`.
