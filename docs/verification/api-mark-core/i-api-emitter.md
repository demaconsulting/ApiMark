## IApiEmitter

### Verification Approach

`IApiEmitter` is a contract interface. Verification confirms that all language-emitter
implementations compile and pass type-checking against the interface, and that the
contract behavior is validated through `IApiGeneratorTests`: if the interface signature
is wrong, the consuming implementation fails to compile and the tests cannot run. No
additional mocking is required because `IApiEmitter` carries no logic of its own.

### Test Environment

N/A — standard test environment using the .NET test runner is sufficient for IApiEmitter
verification. Interface contract compliance is enforced at compile time.

### Acceptance Criteria

- All `IApiEmitter` contract tests pass with zero failures.
- Every language-emitter implementation compiles against the `IApiEmitter` interface.
- The interface methods accept the documented input parameters and return void without
  runtime errors.
- `GradualDisclosure` format causes more than one file to be created via the factory.
- `SingleFile` format causes exactly one file (`api.md`) to be created via the factory.

### Test Scenarios

**IApiEmitter.Emit is callable through the interface reference**: Verifies that a minimal
inline stub of `IApiEmitter` can be implemented and its `Emit` method called with a
factory, config, and context argument. This scenario is tested by
`IApiGenerator_Parse_WithMinimalStub_ExecutesSuccessfully`.

**GradualDisclosure format produces multiple files**: Verifies that when `EmitConfig.Format`
is `GradualDisclosure`, calling `Emit` causes the factory to receive more than one
`CreateMarkdown` call, confirming that the gradual-disclosure multi-file contract is
honored. This scenario is tested by `IApiGenerator_Emit_GradualDisclosure_ProducesMultipleFiles`.

**SingleFile format produces only api.md**: Verifies that when `EmitConfig.Format` is
`SingleFile`, calling `Emit` causes the factory to receive exactly one `CreateMarkdown`
call writing only `api.md` at the root, confirming that the single-file consolidation
contract is honored. This scenario is tested by
`IApiGenerator_Emit_SingleFile_ProducesSingleApiMd`.
