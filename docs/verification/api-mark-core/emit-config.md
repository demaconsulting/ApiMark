## EmitConfig and OutputFormat

### Verification Approach

`EmitConfig` is a passive value object with no methods. Verification confirms that its
properties are accessible, carry the correct default values, and can be overridden at
construction time using C# `init` setters. Correctness of the `Format` and `HeadingDepth`
properties is validated indirectly through `IApiGeneratorTests`: the stub emitters read
`EmitConfig.Format` to decide how many files to create and `EmitConfig.HeadingDepth`
to determine the heading level, so an incorrectly defaulted or missing property would
cause a test failure.

### Test Environment

N/A — standard test environment using the .NET test runner is sufficient for EmitConfig
verification. Property-default compliance is enforced by the test assertions.

### Acceptance Criteria

- All `EmitConfig`-related test cases pass with zero failures.
- `EmitConfig.Format` defaults to `OutputFormat.GradualDisclosure` when no explicit value
  is supplied.
- `EmitConfig.HeadingDepth` defaults to `1` when no explicit value is supplied.
- Both properties can be overridden at object-initialization time using `init` setters.

### Test Scenarios

**GradualDisclosure is the default format**: Verifies that constructing `EmitConfig` with
`new EmitConfig()` and using it in a stub emitter causes multi-file (GradualDisclosure)
output to be produced, confirming the default value of `Format`. This scenario is tested
by `IApiGenerator_Emit_GradualDisclosure_ProducesMultipleFiles`.

**SingleFile format is selectable at construction time**: Verifies that constructing
`EmitConfig` with `new EmitConfig { Format = OutputFormat.SingleFile }` and using it in a
stub emitter causes exactly one `api.md` to be produced, confirming that the `Format`
property can be overridden and that `OutputFormat.SingleFile` is a valid value. This
scenario is tested by `IApiGenerator_Emit_SingleFile_ProducesSingleApiMd`.

**HeadingDepth defaults to 1**: Verifies that the `SingleFileEmitter` stub reads
`config.HeadingDepth` and passes it to `WriteHeading`, and that no exception is thrown
when the default value (`1`) is used, confirming that the default is a valid heading
level. This scenario is tested by `IApiGenerator_Emit_SingleFile_ProducesSingleApiMd`.
