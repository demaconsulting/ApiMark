// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using Xunit;

namespace ApiMark.Cpp.Tests;

/// <summary>
///     xUnit collection definition that serializes tests which invoke real clang via
///     <see cref="ApiMark.Cpp.CppAst.ClangAstParser.Parse"/> (directly, or transitively through
///     <see cref="ApiMark.Cpp.CppGenerator"/>) against tests that mutate the process-wide
///     <c>APIMARK_CLANG_TIMEOUT_MS</c> environment variable.
/// </summary>
/// <remarks>
///     Without this, a test that temporarily sets <c>APIMARK_CLANG_TIMEOUT_MS</c> to an invalid
///     value (or to a short test-specific value) could run concurrently — on a different
///     thread, in a different xUnit collection — with another test's real clang invocation,
///     causing that unrelated invocation to fail or time out prematurely. Pair this definition
///     with <c>[Collection("ClangEnvironment")]</c> on each affected test class; xUnit runs all
///     test classes that share a collection definition serially with respect to each other.
/// </remarks>
[CollectionDefinition("ClangEnvironment")]
public class ClangEnvironmentCollection { }
