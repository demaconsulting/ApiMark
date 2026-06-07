namespace ApiMark.Cpp;

/// <summary>Configuration options for <see cref="CppGenerator"/>.</summary>
/// <remarks>
///     Configures <see cref="CppGenerator"/>, which generates API documentation from C++
///     headers using clang. All properties must be set before passing this object to the
///     <see cref="CppGenerator"/> constructor. The object is not copied; do not mutate it
///     after construction.
/// </remarks>
public sealed class CppGeneratorOptions
{
    /// <summary>
    ///     Gets or sets a brief description of the library, emitted as an introductory
    ///     paragraph in <c>api.md</c>. Optional — omitted when empty.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the name of the library, used as the top-level heading in <c>api.md</c>.
    ///     Must be non-empty before passing to <see cref="CppGenerator"/>.
    /// </summary>
    public string LibraryName { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the public include root directories that define the documented API boundary.
    /// </summary>
    /// <remarks>
    ///     Each root serves two purposes: (1) it is passed to Clang as an <c>-I</c> path so headers
    ///     can find each other, and (2) a declaration is considered "owned" only when its source file
    ///     falls under one of these roots. Must contain at least one entry.
    /// </remarks>
    public IReadOnlyList<string> PublicIncludeRoots { get; set; } = [];

    /// <summary>
    ///     Gets or sets glob patterns (relative to each <see cref="PublicIncludeRoots"/> entry)
    ///     selecting which header files contribute to the documented API.
    ///     When empty, all files under the roots are included.
    /// </summary>
    public IReadOnlyList<string> IncludePatterns { get; set; } = [];

    /// <summary>
    ///     Gets or sets glob patterns (relative to each <see cref="PublicIncludeRoots"/> entry)
    ///     for header files to exclude from the documented API.
    ///     Evaluated after <see cref="IncludePatterns"/>; a file matching both is excluded.
    /// </summary>
    public IReadOnlyList<string> ExcludePatterns { get; set; } = [];

    /// <summary>
    ///     Gets or sets toolchain and SDK include directories passed to Clang as system include
    ///     paths so that system headers (<c>&lt;vector&gt;</c>, <c>&lt;windows.h&gt;</c>, etc.)
    ///     resolve during parsing. Declarations from these paths are never documented.
    /// </summary>
    public IReadOnlyList<string> SystemIncludePaths { get; set; } = [];

    /// <summary>
    ///     Gets or sets additional include directories for third-party headers that are included
    ///     by public headers but are not part of the documented API.
    ///     Declarations from these paths are never documented.
    /// </summary>
    public IReadOnlyList<string> AdditionalIncludePaths { get; set; } = [];

    /// <summary>
    ///     Gets or sets preprocessor symbol definitions passed to Clang as <c>-D</c> flags,
    ///     in the form <c>"NAME"</c> or <c>"NAME=value"</c>.
    ///     Export macros must be defined as empty strings (e.g. <c>"MYLIB_API="</c>)
    ///     so the parser treats them as no-ops.
    /// </summary>
    public IReadOnlyList<string> Defines { get; set; } = [];

    /// <summary>
    ///     Gets or sets the C++ language standard passed to Clang (e.g. <c>"c++17"</c>,
    ///     <c>"c++20"</c>). Defaults to <c>"c++17"</c>.
    /// </summary>
    public string CppStandard { get; set; } = "c++17";

    /// <summary>
    ///     Gets or sets raw Clang compiler arguments appended after all structured options.
    ///     Provides an escape-hatch for toolchain-specific flags not covered by the structured fields.
    /// </summary>
    public IReadOnlyList<string> AdditionalCompilerArguments { get; set; } = [];

    /// <summary>
    ///     Gets or sets which class members are included in the generated output.
    ///     Free functions in namespaces are always included when owned.
    ///     Defaults to <see cref="ApiVisibility.Public"/>.
    /// </summary>
    public ApiVisibility Visibility { get; set; } = ApiVisibility.Public;

    /// <summary>
    ///     Gets or sets a value indicating whether declarations marked with
    ///     <c>[[deprecated]]</c> are included in the output. Defaults to <see langword="false"/>.
    /// </summary>
    public bool IncludeDeprecated { get; set; }

    /// <summary>
    ///     Gets or sets the path to the clang executable. When null or empty, clang is
    ///     located automatically (PATH, xcrun on macOS, vswhere on Windows).
    /// </summary>
    public string? ClangPath { get; set; }
}
