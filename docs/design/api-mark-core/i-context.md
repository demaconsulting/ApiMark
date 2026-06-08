## IContext

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

IContext is the minimal output channel that language generators use to emit
informational and error messages without depending on `Console`. Decoupling
generators from `Console` allows callers to capture, suppress, or redirect
output — for example in tests that use an in-memory implementation, or in hosts
that route messages to a log file or build-system diagnostic channel.

### Data Model

N/A — IContext is an interface with no fields or properties of its own. State,
if any, is managed entirely by the implementing class.

### Key Methods

**IContext.WriteLine**: Writes an informational message to the output channel.

- *Parameters*: `string message` — the message to write. Must not be null.
- *Returns*: `void`
- *Preconditions*: `message` must be non-null.
- *Postconditions*: The message has been delivered to the output channel in the
  order it was passed relative to other messages on the same instance.

**IContext.WriteError**: Writes an error or warning message to the output channel.

- *Parameters*: `string message` — the error or warning message to write. Must
  not be null.
- *Returns*: `void`
- *Preconditions*: `message` must be non-null.
- *Postconditions*: The error message has been delivered to the output channel
  in the order it was passed relative to other error messages on the same
  instance.

### Error Handling

IContext itself defines no error-handling contract; it is an interface.
Implementing classes are responsible for documenting any exceptions they may
throw in response to invalid arguments or I/O failures.

### Dependencies

N/A — IContext is an interface defined in ApiMarkCore; it has no dependencies
on other units, OTS items, or shared packages.

### Callers

- **IApiGenerator implementations** — receive an IContext instance via the
  `Generate` method parameter and call `WriteLine` to report informational
  messages and `WriteError` to report errors or warnings during generation.
- **Program** — passes a `Cli.Context` instance (which implements IContext) to
  the IApiGenerator implementation's `Generate` method.
- **InMemoryContext** (test helper) — implements IContext for unit tests,
  capturing messages in in-memory lists so test code can assert on emitted
  output.
