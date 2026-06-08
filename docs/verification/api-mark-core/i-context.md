## IContext

### Verification Approach

`IContext` is a contract interface. Verification confirms that the in-memory
test double `InMemoryContext` correctly implements the interface and that
messages passed to `WriteLine` and `WriteError` are captured in the appropriate
in-memory lists for assertion. No mocking is required because `InMemoryContext`
itself is the subject under test, and its behavior can be asserted directly
after each call.

### Test Environment

N/A — standard test environment using the .NET test runner is sufficient for
IContext verification. Interface contract compliance is enforced at compile time.

### Acceptance Criteria

- All `IContext` contract tests pass with zero failures.
- `InMemoryContext.Lines` contains every message passed to `WriteLine` in call
  order.
- `InMemoryContext.Errors` contains every message passed to `WriteError` in
  call order.
- Messages written to `WriteLine` do not appear in `Errors`, and messages
  written to `WriteError` do not appear in `Lines`.

### Test Scenarios

**WriteLine captures informational message in Lines**: Verifies that a message
passed to `WriteLine` appears in `InMemoryContext.Lines` immediately after the
call, confirming the informational channel is correctly implemented. This
scenario is tested by `IContext_WriteLine_CapturesMessage_InLines`.

**WriteError captures error message in Errors**: Verifies that a message passed
to `WriteError` appears in `InMemoryContext.Errors` immediately after the call,
confirming the error channel is correctly implemented. This scenario is tested
by `IContext_WriteError_CapturesMessage_InErrors`.

**Written messages are routed to the correct channel without cross-contamination**:
Verifies that informational messages appear only in `Lines` and error messages
appear only in `Errors` when both channels are exercised on the same instance,
confirming channel isolation. This scenario is tested by
`ApiMarkCore_ContextContract_WrittenMessages_AreAccessibleForAssertion`.
