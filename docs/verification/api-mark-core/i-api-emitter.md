## IApiEmitter

### Verification Approach

`IApiEmitter` is a contract interface. Verification confirms that all language-emitter
implementations compile and pass type-checking against the interface, and that the
contract behavior is validated through dedicated `IApiEmitterTests` covering format
selection and the `api.md` entrypoint contract. If the interface signature is wrong,
the consuming implementation fails to compile and the tests cannot run. Supporting
coverage is also provided by `IApiGeneratorTests`, which exercises the same code path
end-to-end.

### Test Environment

N/A - standard test environment using the .NET test runner is sufficient for IApiEmitter
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
factory, config, and context argument. This scenario is tested primarily by
`IApiEmitter_Emit_WithGradualDisclosure_ProducesMultipleFiles` and
`IApiEmitter_Emit_WithSingleFile_ProducesSingleApiMd`; supplemental coverage is
provided by `IApiGenerator_Parse_WithMinimalStub_ExecutesSuccessfully`.

**GradualDisclosure format produces multiple files**: Verifies that when `EmitConfig.Format`
is `GradualDisclosure`, calling `Emit` causes the factory to receive more than one
`CreateMarkdown` call, confirming that the gradual-disclosure multi-file contract is
honored. This scenario is tested by `IApiEmitter_Emit_GradualDisclosure_ProducesMultipleFiles`
and `IApiEmitter_Emit_WithGradualDisclosure_ProducesMultipleFiles`.

**SingleFile format produces only api.md**: Verifies that when `EmitConfig.Format` is
`SingleFile`, calling `Emit` causes the factory to receive exactly one `CreateMarkdown`
call writing only `api.md` at the root, confirming that the single-file consolidation
contract is honored. This scenario is tested by `IApiEmitter_Emit_SingleFile_ProducesSingleApiMd` and
`IApiEmitter_Emit_WithSingleFile_ProducesSingleApiMd`.

**Format-aware emitter reads config.Format to determine output shape**: Verifies that a
single `FormatAwareStubEmitter` implementation correctly produces multi-file output when
`config.Format` is `GradualDisclosure` and single-file output when `config.Format` is
`SingleFile`, confirming that the format-selection decision belongs at the `IApiEmitter`
level. These scenarios are tested by `IApiEmitter_Emit_GradualDisclosure_ProducesMultipleFiles`
and `IApiEmitter_Emit_SingleFile_ProducesSingleApiMd`.

**api.md entrypoint is always produced**: Verifies that every conformant `IApiEmitter`
implementation calls `factory.CreateMarkdown("", "api")` to produce the fixed top-level
entrypoint required by all callers. This scenario is tested by
`IApiGenerator_Emit_OutputDirectory_ContainsApiMd`, `IApiEmitter_Emit_WithGradualDisclosure_ProducesMultipleFiles`,
and `IApiEmitter_Emit_WithSingleFile_ProducesSingleApiMd`.

**Emit rejects null factory**: Verifies that `IApiEmitter.Emit` throws `ArgumentNullException`
when `null` is passed for the factory argument, confirming the null-precondition contract at
the interface level. Tested by `IApiEmitter_Emit_NullFactory_ThrowsArgumentNullException`.

**Emit rejects null config**: Verifies that `IApiEmitter.Emit` throws `ArgumentNullException`
when `null` is passed for the config argument, confirming the null-precondition contract at
the interface level. Tested by `IApiEmitter_Emit_NullConfig_ThrowsArgumentNullException`.

**Emit rejects null context**: Verifies that `IApiEmitter.Emit` throws `ArgumentNullException`
when `null` is passed for the context argument, confirming the null-precondition contract at
the interface level. Tested by `IApiEmitter_Emit_NullContext_ThrowsArgumentNullException`.
