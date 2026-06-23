## EmitConfig and OutputFormat

### Verification Approach

`EmitConfig` is a passive value object with no methods. Verification confirms that its
properties are accessible, carry the correct default values, and can be overridden at
construction time using C# `init` setters. Default values are now verified directly by
dedicated unit tests (`EmitConfig_DefaultFormat_IsGradualDisclosure` and
`EmitConfig_DefaultHeadingDepth_IsOne`) in addition to indirect validation through the
stub emitter tests.

### Test Environment

N/A - standard test environment using the .NET test runner is sufficient for EmitConfig
verification. Property-default compliance is enforced by the test assertions.

### Acceptance Criteria

- All `EmitConfig`-related test cases pass with zero failures.
- `EmitConfig.Format` defaults to `OutputFormat.GradualDisclosure` when no explicit value
  is supplied.
- `EmitConfig.HeadingDepth` defaults to `1` when no explicit value is supplied.
- Both properties can be overridden at object-initialization time using `init` setters.
- Setting `HeadingDepth` to a value less than 1 throws `ArgumentOutOfRangeException`.
- Setting `HeadingDepth` to a value greater than 3 throws `ArgumentOutOfRangeException`.
- Setting `HeadingDepth` to a valid non-default value (e.g., `2`) is accepted and
  reflected by the property.

### Test Scenarios

**GradualDisclosure is the default format**: Verifies that constructing `EmitConfig` with
`new EmitConfig()` produces `Format == OutputFormat.GradualDisclosure`. This is verified
directly by `EmitConfig_DefaultFormat_IsGradualDisclosure` and also indirectly by
`IApiEmitter_Emit_GradualDisclosure_ProducesMultipleFiles`.

**SingleFile format is selectable at construction time**: Verifies that constructing
`EmitConfig` with `new EmitConfig { Format = OutputFormat.SingleFile }` and using it in a
stub emitter causes exactly one `api.md` to be produced, confirming that the `Format`
property can be overridden and that `OutputFormat.SingleFile` is a valid value. This
scenario is tested by `IApiEmitter_Emit_SingleFile_ProducesSingleApiMd`.

**HeadingDepth defaults to 1**: Verifies that `new EmitConfig().HeadingDepth == 1`. This
is verified directly by `EmitConfig_DefaultHeadingDepth_IsOne` and also indirectly by
`IApiEmitter_Emit_SingleFile_ProducesSingleApiMd`.

**HeadingDepth below minimum throws ArgumentOutOfRangeException**: Verifies that
constructing `new EmitConfig { HeadingDepth = 0 }` throws `ArgumentOutOfRangeException`,
confirming that values below 1 are rejected at init time. This scenario is tested by
`EmitConfig_HeadingDepth_BelowMinimum_ThrowsArgumentOutOfRangeException`.

**HeadingDepth above maximum throws ArgumentOutOfRangeException**: Verifies that
constructing `new EmitConfig { HeadingDepth = 4 }` throws `ArgumentOutOfRangeException`,
confirming that values above 3 are rejected (because depth 4 would produce H7 member
headings, which are unsupported by CommonMark). This scenario is tested by
`EmitConfig_HeadingDepth_AboveMaximum_ThrowsArgumentOutOfRangeException`.

**HeadingDepth valid non-default value is accepted**: Verifies that
`new EmitConfig { HeadingDepth = 2 }` succeeds and `HeadingDepth` reflects the
supplied value, confirming the full selectable range. This scenario is tested by
`EmitConfig_HeadingDepth_ValidNonDefault_SetsCorrectly`.
