using Xunit;

/// <summary>
///     xUnit collection definition that serializes tests that mutate
///     global <see cref="System.Console"/> streams (Out and Error).
/// </summary>
/// <remarks>
///     Tests that call <see cref="System.Console.SetOut"/> or
///     <see cref="System.Console.SetError"/> must run serially to avoid cross-test
///     interference with other tests in the same collection. Pair this definition with
///     <c>[Collection("Console")]</c> on each affected test class; xUnit runs all test
///     classes that share a collection definition serially with respect to each other.
/// </remarks>
[CollectionDefinition("Console")]
public class ConsoleCollection { }
