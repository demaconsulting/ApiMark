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
`ApiGeneratorContract_ImplementationCompiles`.

**Contract methods are invocable through the interface reference**: Verifies that calling the
`Generate` method through an `IApiGenerator` reference dispatches correctly and does not throw a
`NotImplementedException` for the expected inputs, confirming that the interface is not partially
implemented. This scenario is tested by `ApiGeneratorContract_GenerateMethod_IsInvocable`.
