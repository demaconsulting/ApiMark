namespace ApiMark.Cpp.CppAst;

/// <summary>
///     Provides a narrow, public pre-flight capability check for clang availability, sharing
///     the exact discovery logic used by <see cref="ClangAstParser"/> when it actually invokes
///     clang. This is not a general-purpose clang locator; it exists so host tools can decide
///     whether to attempt C++ API generation before doing so.
/// </summary>
public static class ClangDiscovery
{
    /// <summary>
    ///     Determines whether a clang executable can be located using the same discovery order
    ///     as <see cref="ClangAstParser"/> (explicit path, <c>APIMARK_CLANG_PATH</c> environment
    ///     variable, PATH, macOS <c>xcrun</c>, or Windows vswhere/default LLVM install location).
    /// </summary>
    /// <param name="clangPath">
    ///     Optional explicit path to a clang executable. When non-empty, no discovery is
    ///     performed and only this path is checked.
    /// </param>
    /// <returns><see langword="true"/> when a clang executable was located; otherwise <see langword="false"/>.</returns>
    public static bool IsAvailable(string? clangPath = null)
    {
        return ClangAstParser.TryFindClangExecutable(clangPath, out _, out _);
    }
}
